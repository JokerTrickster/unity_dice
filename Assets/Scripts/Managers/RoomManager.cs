using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 관리자 Singleton
/// 방 생성, 참여, 나가기, 실시간 상태 동기화를 담당하는 핵심 시스템
/// WebSocket 기반 실시간 통신과 방 생명주기 관리
/// </summary>
public class RoomManager : MonoBehaviour
{
    #region Singleton
    private static RoomManager _instance;
    public static RoomManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("RoomManager");
                _instance = go.AddComponent<RoomManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    // Room lifecycle events
    public static event Action<RoomData> OnRoomCreated;
    public static event Action<RoomData> OnRoomJoined;
    public static event Action<RoomData> OnRoomLeft;
    public static event Action<RoomData> OnRoomClosed;
    public static event Action<RoomData> OnRoomUpdated;
    
    // Player events
    public static event Action<RoomData, PlayerInfo> OnPlayerJoined;
    public static event Action<RoomData, PlayerInfo> OnPlayerLeft;
    public static event Action<RoomData, PlayerInfo> OnPlayerUpdated;
    public static event Action<string> OnHostChanged; // new host player ID
    
    // Game events
    public static event Action<string> OnGameStartRequested; // room code
    public static event Action<string> OnGameStarted; // room code
    public static event Action<string, string> OnGameStartFailed; // room code, reason
    
    // Error events
    public static event Action<string, string> OnRoomError; // operation, error message
    public static event Action<bool> OnConnectionStatusChanged; // connected
    #endregion

    #region Private Fields
    private RoomData _currentRoom;
    private HostManager _hostManager;
    private RoomCodeGenerator _codeGenerator;
    private string _localPlayerId;
    private string _localPlayerNickname;
    private bool _isConnectedToRoom;
    private Coroutine _roomExpirationChecker;
    private Dictionary<string, DateTime> _joinAttempts;
    private const int MAX_JOIN_ATTEMPTS_PER_MINUTE = 5;
    #endregion

    #region Properties
    public RoomData CurrentRoom => _currentRoom;
    public bool IsInRoom => _currentRoom != null;
    public bool IsHost => _hostManager?.IsHost ?? false;
    public bool IsConnected => _isConnectedToRoom;
    public string LocalPlayerId => _localPlayerId;
    public PlayerInfo LocalPlayer => _currentRoom?.GetPlayer(_localPlayerId);
    public HostManager HostManager => _hostManager;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        Initialize();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsInRoom)
        {
            // 앱이 백그라운드로 갈 때 연결 상태 보존 시도
            Debug.Log("[RoomManager] App paused - maintaining room connection");
        }
        else if (!pauseStatus && IsInRoom)
        {
            // 앱이 다시 활성화될 때 방 상태 복구 시도
            StartCoroutine(ReconnectToRoomCoroutine());
        }
    }
    #endregion

    #region Initialization
    private void Initialize()
    {
        _codeGenerator = RoomCodeGenerator.Instance;
        _joinAttempts = new Dictionary<string, DateTime>();
        _isConnectedToRoom = false;
        
        // UserDataManager에서 플레이어 정보 가져오기
        InitializePlayerInfo();
        
        // WebSocket 연결 이벤트 구독
        SubscribeToNetworkEvents();
        
        Debug.Log("[RoomManager] Initialized");
    }

    private void InitializePlayerInfo()
    {
        // UserDataManager.Instance가 있다고 가정
        if (UserDataManager.Instance != null)
        {
            _localPlayerId = UserDataManager.Instance.GetUserId();
            _localPlayerNickname = UserDataManager.Instance.GetNickname();
        }
        else
        {
            // 임시 ID 생성 (실제 구현시에는 UserDataManager에서 가져와야 함)
            _localPlayerId = System.Guid.NewGuid().ToString("N")[..8];
            _localPlayerNickname = "Player_" + _localPlayerId[..4];
            Debug.LogWarning($"[RoomManager] Using temporary player info: {_localPlayerId}/{_localPlayerNickname}");
        }

        Debug.Log($"[RoomManager] Player info initialized: {_localPlayerId}/{_localPlayerNickname}");
    }

    private void SubscribeToNetworkEvents()
    {
        // NetworkManager WebSocket 이벤트 구독
        if (NetworkManager.Instance != null)
        {
            // 실제 구현시 NetworkManager의 WebSocket 이벤트 구독
            Debug.Log("[RoomManager] Subscribed to network events");
        }
    }
    #endregion

    #region Room Creation
    /// <summary>
    /// 새 방 생성
    /// </summary>
    public void CreateRoom(int maxPlayers, Action<bool, string> callback = null)
    {
        StartCoroutine(CreateRoomCoroutine(maxPlayers, callback));
    }

    private IEnumerator CreateRoomCoroutine(int maxPlayers, Action<bool, string> callback)
    {
        Debug.Log($"[RoomManager] Creating room for {maxPlayers} players");

        // 1. 기존 방 나가기
        if (IsInRoom)
        {
            yield return StartCoroutine(LeaveRoomCoroutine());
        }

        // 2. 에너지 검증
        if (!ValidateEnergyForGameStart())
        {
            string error = "에너지가 부족합니다";
            Debug.LogWarning($"[RoomManager] Room creation failed: {error}");
            OnRoomError?.Invoke("create_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 3. 방 코드 생성
        string roomCode;
        try
        {
            roomCode = _codeGenerator.GenerateRoomCode();
        }
        catch (Exception ex)
        {
            string error = $"방 코드 생성 실패: {ex.Message}";
            Debug.LogError($"[RoomManager] {error}");
            OnRoomError?.Invoke("create_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 4. 방 데이터 생성
        var roomData = new RoomData(roomCode, _localPlayerId, maxPlayers);
        var hostPlayer = new PlayerInfo(_localPlayerId, _localPlayerNickname, true);
        roomData.AddPlayer(hostPlayer);

        // 5. 서버에 방 생성 요청
        yield return StartCoroutine(SendCreateRoomRequest(roomData));

        // 6. 성공시 로컬 상태 업데이트
        if (_currentRoom != null && _currentRoom.RoomCode == roomCode)
        {
            _hostManager = new HostManager(_localPlayerId);
            _hostManager.SetRoom(_currentRoom);
            SubscribeToHostEvents();

            _isConnectedToRoom = true;
            StartRoomExpirationChecker();

            OnRoomCreated?.Invoke(_currentRoom);
            OnConnectionStatusChanged?.Invoke(true);

            Debug.Log($"[RoomManager] Room created successfully: {_currentRoom.GetSummary()}");
            callback?.Invoke(true, roomCode);
        }
        else
        {
            string error = "방 생성 요청이 실패했습니다";
            Debug.LogError($"[RoomManager] {error}");
            callback?.Invoke(false, error);
        }
    }

    private IEnumerator SendCreateRoomRequest(RoomData roomData)
    {
        // 실제 구현시에는 NetworkManager를 통해 WebSocket으로 방 생성 요청
        // 현재는 시뮬레이션
        yield return new WaitForSeconds(0.5f); // 네트워크 지연 시뮬레이션

        // 성공 시뮬레이션
        _currentRoom = roomData;
        Debug.Log($"[RoomManager] Create room request sent: {roomData.RoomCode}");
    }
    #endregion

    #region Room Joining
    /// <summary>
    /// 방 참여
    /// </summary>
    public void JoinRoom(string roomCode, Action<bool, string> callback = null)
    {
        StartCoroutine(JoinRoomCoroutine(roomCode, callback));
    }

    private IEnumerator JoinRoomCoroutine(string roomCode, Action<bool, string> callback)
    {
        Debug.Log($"[RoomManager] Attempting to join room: {roomCode}");

        // 1. 참여 시도 횟수 제한 확인
        if (!CheckJoinRateLimit(roomCode))
        {
            string error = "너무 많은 참여 시도입니다. 잠시 후 다시 시도해주세요";
            Debug.LogWarning($"[RoomManager] {error}");
            OnRoomError?.Invoke("join_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 2. 방 코드 형식 검증
        if (!RoomCodeGenerator.IsValidRoomCodeFormat(roomCode))
        {
            string error = "잘못된 방 코드 형식입니다 (4자리 숫자)";
            Debug.LogWarning($"[RoomManager] {error}: {roomCode}");
            OnRoomError?.Invoke("join_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 3. 브루트포스 보안 검증
        if (!_codeGenerator.RecordBruteForceAttempt("127.0.0.1", roomCode)) // 실제로는 실제 IP 사용
        {
            string error = "보안상 일시적으로 차단되었습니다";
            Debug.LogWarning($"[RoomManager] {error}");
            OnRoomError?.Invoke("join_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 4. 기존 방 나가기
        if (IsInRoom)
        {
            yield return StartCoroutine(LeaveRoomCoroutine());
        }

        // 5. 에너지 검증
        if (!ValidateEnergyForGameStart())
        {
            string error = "에너지가 부족합니다";
            Debug.LogWarning($"[RoomManager] {error}");
            OnRoomError?.Invoke("join_room", error);
            callback?.Invoke(false, error);
            yield break;
        }

        // 6. 서버에 방 참여 요청
        yield return StartCoroutine(SendJoinRoomRequest(roomCode));

        // 7. 성공시 로컬 상태 업데이트
        if (_currentRoom != null && _currentRoom.RoomCode == roomCode)
        {
            _hostManager = new HostManager(_localPlayerId);
            _hostManager.SetRoom(_currentRoom);
            SubscribeToHostEvents();

            _isConnectedToRoom = true;
            StartRoomExpirationChecker();

            // 브루트포스 시도 초기화 (성공시)
            _codeGenerator.ResetBruteForceAttempts("127.0.0.1");

            OnRoomJoined?.Invoke(_currentRoom);
            OnConnectionStatusChanged?.Invoke(true);

            Debug.Log($"[RoomManager] Room joined successfully: {_currentRoom.GetSummary()}");
            callback?.Invoke(true, roomCode);
        }
        else
        {
            string error = "방 참여에 실패했습니다. 방이 존재하지 않거나 가득 찼을 수 있습니다";
            Debug.LogError($"[RoomManager] {error}");
            callback?.Invoke(false, error);
        }
    }

    private IEnumerator SendJoinRoomRequest(string roomCode)
    {
        // 실제 구현시에는 NetworkManager를 통해 WebSocket으로 방 참여 요청
        // 현재는 시뮬레이션
        yield return new WaitForSeconds(0.5f); // 네트워크 지연 시뮬레이션

        // 방이 존재한다고 가정하고 참여 시뮬레이션
        if (_codeGenerator.IsActiveCode(roomCode))
        {
            _currentRoom = new RoomData(roomCode, "host_player_id", 4);
            var localPlayer = new PlayerInfo(_localPlayerId, _localPlayerNickname, false);
            _currentRoom.AddPlayer(localPlayer);
            
            Debug.Log($"[RoomManager] Join room request sent: {roomCode}");
        }
        else
        {
            _currentRoom = null; // 방 참여 실패
        }
    }

    private bool CheckJoinRateLimit(string roomCode)
    {
        var now = DateTime.Now;
        var key = $"{_localPlayerId}:{roomCode}";

        // 1분 이전 시도 기록 정리
        var expiredKeys = new List<string>();
        foreach (var kvp in _joinAttempts)
        {
            if (now - kvp.Value > TimeSpan.FromMinutes(1))
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var expiredKey in expiredKeys)
        {
            _joinAttempts.Remove(expiredKey);
        }

        // 현재 시도 횟수 확인
        int currentAttempts = 0;
        foreach (var kvp in _joinAttempts)
        {
            if (kvp.Key.StartsWith(_localPlayerId + ":"))
            {
                currentAttempts++;
            }
        }

        if (currentAttempts >= MAX_JOIN_ATTEMPTS_PER_MINUTE)
        {
            return false;
        }

        // 현재 시도 기록
        _joinAttempts[key] = now;
        return true;
    }
    #endregion

    #region Room Leaving
    /// <summary>
    /// 방 나가기
    /// </summary>
    public void LeaveRoom(Action<bool> callback = null)
    {
        StartCoroutine(LeaveRoomCoroutine(callback));
    }

    private IEnumerator LeaveRoomCoroutine(Action<bool> callback = null)
    {
        if (!IsInRoom)
        {
            Debug.LogWarning("[RoomManager] Not in any room to leave");
            callback?.Invoke(false);
            yield break;
        }

        Debug.Log($"[RoomManager] Leaving room: {_currentRoom.RoomCode}");

        // 1. 서버에 방 나가기 요청
        yield return StartCoroutine(SendLeaveRoomRequest());

        // 2. 로컬 상태 정리
        var oldRoom = _currentRoom;
        CleanupRoomState();

        OnRoomLeft?.Invoke(oldRoom);
        OnConnectionStatusChanged?.Invoke(false);

        Debug.Log($"[RoomManager] Left room successfully");
        callback?.Invoke(true);
    }

    private IEnumerator SendLeaveRoomRequest()
    {
        // 실제 구현시에는 NetworkManager를 통해 WebSocket으로 방 나가기 요청
        yield return new WaitForSeconds(0.2f); // 네트워크 지연 시뮬레이션
        Debug.Log($"[RoomManager] Leave room request sent");
    }
    #endregion

    #region Game Start
    /// <summary>
    /// 게임 시작 (방장만 가능)
    /// </summary>
    public void StartGame(Action<bool, string> callback = null)
    {
        if (!IsHost)
        {
            string error = "방장만 게임을 시작할 수 있습니다";
            Debug.LogWarning($"[RoomManager] {error}");
            OnGameStartFailed?.Invoke(_currentRoom?.RoomCode ?? "", error);
            callback?.Invoke(false, error);
            return;
        }

        _hostManager.RequestStartGame();
        callback?.Invoke(true, "게임 시작 요청이 전송되었습니다");
    }

    private void OnGameStartRequestedHandler(string roomCode)
    {
        StartCoroutine(SendGameStartRequest(roomCode));
    }

    private IEnumerator SendGameStartRequest(string roomCode)
    {
        Debug.Log($"[RoomManager] Sending game start request for room: {roomCode}");

        // 실제 구현시에는 NetworkManager를 통해 게임 시작 요청
        yield return new WaitForSeconds(0.3f);

        // 성공 시뮬레이션
        if (_currentRoom != null && _currentRoom.RoomCode == roomCode)
        {
            _currentRoom.Status = RoomStatus.Starting;
            OnGameStarted?.Invoke(roomCode);
            OnRoomUpdated?.Invoke(_currentRoom);
            
            Debug.Log($"[RoomManager] Game started for room: {roomCode}");
        }
    }
    #endregion

    #region Room State Management
    /// <summary>
    /// 방 상태 정리
    /// </summary>
    private void CleanupRoomState()
    {
        if (_currentRoom != null && _codeGenerator != null)
        {
            _codeGenerator.ReleaseCode(_currentRoom.RoomCode);
        }

        _currentRoom = null;
        _hostManager?.Cleanup();
        _hostManager = null;
        _isConnectedToRoom = false;

        StopRoomExpirationChecker();
        UnsubscribeFromHostEvents();
    }

    /// <summary>
    /// 방 만료 시간 확인 시작
    /// </summary>
    private void StartRoomExpirationChecker()
    {
        if (_roomExpirationChecker != null)
        {
            StopCoroutine(_roomExpirationChecker);
        }
        
        _roomExpirationChecker = StartCoroutine(CheckRoomExpirationCoroutine());
    }

    /// <summary>
    /// 방 만료 시간 확인 중지
    /// </summary>
    private void StopRoomExpirationChecker()
    {
        if (_roomExpirationChecker != null)
        {
            StopCoroutine(_roomExpirationChecker);
            _roomExpirationChecker = null;
        }
    }

    /// <summary>
    /// 방 만료 확인 코루틴
    /// </summary>
    private IEnumerator CheckRoomExpirationCoroutine()
    {
        while (IsInRoom)
        {
            if (_currentRoom.IsExpired)
            {
                Debug.LogWarning($"[RoomManager] Room {_currentRoom.RoomCode} has expired");
                OnRoomError?.Invoke("room_expired", "방이 만료되었습니다");
                
                var expiredRoom = _currentRoom;
                CleanupRoomState();
                OnRoomClosed?.Invoke(expiredRoom);
                OnConnectionStatusChanged?.Invoke(false);
                
                yield break;
            }
            
            yield return new WaitForSeconds(30f); // 30초마다 확인
        }
    }
    #endregion

    #region Host Events
    private void SubscribeToHostEvents()
    {
        if (_hostManager != null)
        {
            _hostManager.OnGameStartRequested += OnGameStartRequestedHandler;
            _hostManager.OnGameStartFailed += OnGameStartFailedHandler;
            _hostManager.OnHostChanged += OnHostChangedHandler;
        }
    }

    private void UnsubscribeFromHostEvents()
    {
        if (_hostManager != null)
        {
            _hostManager.OnGameStartRequested -= OnGameStartRequestedHandler;
            _hostManager.OnGameStartFailed -= OnGameStartFailedHandler;
            _hostManager.OnHostChanged -= OnHostChangedHandler;
        }
    }

    private void OnGameStartFailedHandler(string reason)
    {
        OnGameStartFailed?.Invoke(_currentRoom?.RoomCode ?? "", reason);
    }

    private void OnHostChangedHandler(string newHostId)
    {
        OnHostChanged?.Invoke(newHostId);
        OnRoomUpdated?.Invoke(_currentRoom);
    }
    #endregion

    #region Network Reconnection
    /// <summary>
    /// 방 연결 재시도
    /// </summary>
    private IEnumerator ReconnectToRoomCoroutine()
    {
        if (!IsInRoom) yield break;

        Debug.Log($"[RoomManager] Attempting to reconnect to room: {_currentRoom.RoomCode}");

        yield return new WaitForSeconds(1f);

        // 실제 구현시에는 서버에 재연결 요청
        // 현재는 시뮬레이션
        _isConnectedToRoom = true;
        OnConnectionStatusChanged?.Invoke(true);

        Debug.Log($"[RoomManager] Reconnected to room: {_currentRoom.RoomCode}");
    }
    #endregion

    #region Validation
    /// <summary>
    /// 게임 시작을 위한 에너지 검증
    /// </summary>
    private bool ValidateEnergyForGameStart()
    {
        // 실제 구현시에는 EnergyManager.Instance.CanStartGame() 사용
        return true; // 현재는 항상 true
    }
    #endregion

    #region WebSocket Message Handling
    /// <summary>
    /// WebSocket 메시지 처리 (실제 구현시 NetworkManager에서 호출)
    /// </summary>
    public void HandleRoomWebSocketMessage(string message)
    {
        try
        {
            // 실제 구현시에는 JSON 파싱하여 적절한 이벤트 발생
            Debug.Log($"[RoomManager] Received WebSocket message: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomManager] Failed to handle WebSocket message: {ex.Message}");
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 정리
    /// </summary>
    public void Cleanup()
    {
        StopAllCoroutines();
        CleanupRoomState();
        
        _joinAttempts?.Clear();
        
        // 이벤트 정리
        OnRoomCreated = null;
        OnRoomJoined = null;
        OnRoomLeft = null;
        OnRoomClosed = null;
        OnRoomUpdated = null;
        OnPlayerJoined = null;
        OnPlayerLeft = null;
        OnPlayerUpdated = null;
        OnHostChanged = null;
        OnGameStartRequested = null;
        OnGameStarted = null;
        OnGameStartFailed = null;
        OnRoomError = null;
        OnConnectionStatusChanged = null;
        
        Debug.Log("[RoomManager] Cleaned up");
    }
    #endregion

    #region Public Utilities
    /// <summary>
    /// 현재 방 상태 요약 조회
    /// </summary>
    public string GetRoomStatusSummary()
    {
        if (!IsInRoom)
            return "Not in any room";
            
        return $"Room: {_currentRoom.RoomCode}, Status: {_currentRoom.Status}, " +
               $"Players: {_currentRoom.CurrentPlayerCount}/{_currentRoom.MaxPlayers}, " +
               $"Host: {IsHost}, Connected: {IsConnected}";
    }

    /// <summary>
    /// 방 코드 클립보드 복사
    /// </summary>
    public void CopyRoomCodeToClipboard()
    {
        if (!IsInRoom) return;

        GUIUtility.systemCopyBuffer = _currentRoom.RoomCode;
        Debug.Log($"[RoomManager] Room code {_currentRoom.RoomCode} copied to clipboard");
    }
    #endregion
}