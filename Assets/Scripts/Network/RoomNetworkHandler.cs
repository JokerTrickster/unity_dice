using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 방 시스템 WebSocket 네트워크 핸들러
/// 방 생성/참여/나가기 및 실시간 상태 동기화를 위한 WebSocket 통신 관리
/// RoomManager와 연동하여 방 관련 모든 네트워크 통신을 담당
/// </summary>
public class RoomNetworkHandler : MonoBehaviour
{
    #region Events
    /// <summary>방 응답 수신 이벤트</summary>
    public event Action<RoomProtocolExtension.RoomResponse> OnRoomResponse;
    
    /// <summary>방 상태 동기화 이벤트</summary>
    public event Action<RoomProtocolExtension.RoomStateSyncData> OnRoomStateSync;
    
    /// <summary>플레이어 참여 이벤트</summary>
    public event Action<string, PlayerInfo> OnPlayerJoined; // roomCode, playerInfo
    
    /// <summary>플레이어 나가기 이벤트</summary>
    public event Action<string, PlayerInfo> OnPlayerLeft; // roomCode, playerInfo
    
    /// <summary>방장 변경 이벤트</summary>
    public event Action<string, string> OnHostChanged; // roomCode, newHostId
    
    /// <summary>게임 시작 이벤트</summary>
    public event Action<string> OnGameStarting; // roomCode
    
    /// <summary>방 오류 이벤트</summary>
    public event Action<string, string, string> OnRoomError; // errorCode, errorMessage, roomCode
    
    /// <summary>연결 상태 변경 이벤트</summary>
    public event Action<bool> OnConnectionStateChanged;
    #endregion

    #region Private Fields
    private NetworkManager _networkManager;
    private RoomTimeoutManager _timeoutManager;
    private RoomReconnectionManager _reconnectionManager;
    
    private bool _isInitialized = false;
    private bool _isProcessingMessage = false;
    
    private readonly Dictionary<string, DateTime> _pendingRoomRequests = new();
    private readonly Dictionary<string, string> _roomCodeMap = new(); // requestId -> roomCode
    private readonly object _lockObject = new();
    
    // Performance tracking
    private readonly Dictionary<string, DateTime> _requestStartTimes = new();
    private const float TARGET_RESPONSE_TIME = 2.0f; // 2초 응답 목표
    private const float TARGET_SYNC_TIME = 1.0f; // 1초 동기화 목표
    #endregion

    #region Properties
    public bool IsInitialized => _isInitialized;
    public bool IsConnected => _networkManager?.IsWebSocketConnected() ?? false;
    public int PendingRequestCount => _pendingRoomRequests.Count;
    public RoomTimeoutManager TimeoutManager => _timeoutManager;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _timeoutManager = gameObject.AddComponent<RoomTimeoutManager>();
        _reconnectionManager = gameObject.AddComponent<RoomReconnectionManager>();
        
        SetupTimeoutEvents();
        SetupReconnectionEvents();
        
        // MatchingProtocol에 방 메시지 타입 확장
        RoomProtocolExtension.ExtendMatchingProtocol();
        
        if (FindObjectOfType<NetworkManager>() is NetworkManager networkManager)
        {
            InitializeAsync(networkManager);
        }
        else
        {
            Debug.LogError("[RoomNetworkHandler] NetworkManager not found in scene");
        }
    }

    private void OnDestroy()
    {
        CleanupEvents();
        ClearAllRequests();
        
        _timeoutManager?.StopAllTimeouts();
        _reconnectionManager?.StopReconnection();
    }
    #endregion

    #region Initialization
    public async Task InitializeAsync(NetworkManager networkManager)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[RoomNetworkHandler] Already initialized");
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError("[RoomNetworkHandler] NetworkManager cannot be null");
            return;
        }

        try
        {
            _networkManager = networkManager;
            
            // WebSocket 설정 로드
            var config = Resources.Load<WebSocketConfig>("DefaultWebSocketConfig");
            if (config == null)
            {
                Debug.LogError("[RoomNetworkHandler] WebSocket config not found");
                return;
            }

            // WebSocket 초기화
            bool initialized = await _networkManager.InitializeWebSocketAsync(config);
            if (!initialized)
            {
                Debug.LogError("[RoomNetworkHandler] Failed to initialize WebSocket");
                return;
            }

            // 이벤트 구독
            SetupNetworkEvents();
            
            // WebSocket 연결
            bool connected = await _networkManager.ConnectWebSocketAsync();
            if (!connected)
            {
                Debug.LogWarning("[RoomNetworkHandler] Failed to connect WebSocket initially, will retry");
                _reconnectionManager.StartReconnection();
            }

            _isInitialized = true;
            Debug.Log("[RoomNetworkHandler] Initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Initialization failed: {e.Message}");
        }
    }

    private void SetupNetworkEvents()
    {
        if (_networkManager == null) return;

        _networkManager.SubscribeToWebSocketEvents(
            onMessage: HandleWebSocketMessage,
            onConnectionChanged: HandleConnectionChanged,
            onError: HandleWebSocketError
        );
    }

    private void SetupTimeoutEvents()
    {
        if (_timeoutManager == null) return;

        _timeoutManager.OnRoomRequestTimeout += HandleRequestTimeout;
        _timeoutManager.OnTimeoutWarning += HandleTimeoutWarning;
    }

    private void SetupReconnectionEvents()
    {
        if (_reconnectionManager == null) return;

        _reconnectionManager.OnReconnectionStarted += () => Debug.Log("[RoomNetworkHandler] Reconnection started");
        _reconnectionManager.OnReconnectionSuccess += () => Debug.Log("[RoomNetworkHandler] Reconnection successful");
        _reconnectionManager.OnReconnectionFailed += (attempt) => Debug.LogWarning($"[RoomNetworkHandler] Reconnection failed (attempt {attempt})");
    }

    private void CleanupEvents()
    {
        _networkManager?.UnsubscribeFromWebSocketEvents();
        
        if (_timeoutManager != null)
        {
            _timeoutManager.OnRoomRequestTimeout -= HandleRequestTimeout;
            _timeoutManager.OnTimeoutWarning -= HandleTimeoutWarning;
        }
    }
    #endregion

    #region Public API - Room Operations
    /// <summary>방 생성 요청</summary>
    public async Task<bool> SendCreateRoomRequestAsync(string playerId, string nickname, int maxPlayers,
        string gameMode = "classic", int betAmount = 0, bool isPrivate = false)
    {
        if (!ValidateConnection("create room")) return false;

        try
        {
            string message = RoomProtocolExtension.CreateRoomCreateMessage(playerId, nickname, maxPlayers, gameMode, betAmount, isPrivate);
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[RoomNetworkHandler] Failed to create room creation message");
                return false;
            }

            // 성능 추적 시작
            var requestId = Guid.NewGuid().ToString("N")[..8];
            TrackRequestStart(requestId, "create_room");
            
            // 타임아웃 설정
            _timeoutManager.StartRoomRequestTimeout(requestId, playerId, TARGET_RESPONSE_TIME);
            
            // 전송
            bool sent = _networkManager.SendWebSocketMessage(message, MessagePriority.High);
            
            if (sent)
            {
                RegisterPendingRequest(requestId, "");
                Debug.Log($"[RoomNetworkHandler] Create room request sent for player {playerId}");
            }
            else
            {
                CancelRequest(requestId);
                Debug.LogError("[RoomNetworkHandler] Failed to send create room request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Create room request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>방 참여 요청</summary>
    public async Task<bool> SendJoinRoomRequestAsync(string playerId, string nickname, string roomCode)
    {
        if (!ValidateConnection("join room")) return false;

        try
        {
            string message = RoomProtocolExtension.CreateRoomJoinMessage(playerId, nickname, roomCode);
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[RoomNetworkHandler] Failed to create room join message");
                return false;
            }

            var requestId = Guid.NewGuid().ToString("N")[..8];
            TrackRequestStart(requestId, "join_room");
            
            _timeoutManager.StartRoomRequestTimeout(requestId, playerId, TARGET_RESPONSE_TIME);
            
            bool sent = _networkManager.SendWebSocketMessage(message, MessagePriority.High);
            
            if (sent)
            {
                RegisterPendingRequest(requestId, roomCode);
                Debug.Log($"[RoomNetworkHandler] Join room request sent for player {playerId}, room {roomCode}");
            }
            else
            {
                CancelRequest(requestId);
                Debug.LogError("[RoomNetworkHandler] Failed to send join room request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Join room request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>방 나가기 요청</summary>
    public async Task<bool> SendLeaveRoomRequestAsync(string playerId, string roomCode)
    {
        if (!ValidateConnection("leave room")) return false;

        try
        {
            string message = RoomProtocolExtension.CreateRoomLeaveMessage(playerId, roomCode);
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[RoomNetworkHandler] Failed to create room leave message");
                return false;
            }

            var requestId = Guid.NewGuid().ToString("N")[..8];
            TrackRequestStart(requestId, "leave_room");
            
            bool sent = _networkManager.SendWebSocketMessage(message, MessagePriority.Normal);
            
            if (sent)
            {
                Debug.Log($"[RoomNetworkHandler] Leave room request sent for player {playerId}, room {roomCode}");
            }
            else
            {
                Debug.LogError("[RoomNetworkHandler] Failed to send leave room request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Leave room request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>게임 시작 요청</summary>
    public async Task<bool> SendGameStartRequestAsync(string hostId, string roomCode)
    {
        if (!ValidateConnection("start game")) return false;

        try
        {
            string message = RoomProtocolExtension.CreateGameStartRequestMessage(hostId, roomCode);
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[RoomNetworkHandler] Failed to create game start message");
                return false;
            }

            var requestId = Guid.NewGuid().ToString("N")[..8];
            TrackRequestStart(requestId, "game_start_request");
            
            _timeoutManager.StartRoomRequestTimeout(requestId, hostId, TARGET_RESPONSE_TIME);
            
            bool sent = _networkManager.SendWebSocketMessage(message, MessagePriority.High);
            
            if (sent)
            {
                RegisterPendingRequest(requestId, roomCode);
                Debug.Log($"[RoomNetworkHandler] Game start request sent for host {hostId}, room {roomCode}");
            }
            else
            {
                CancelRequest(requestId);
                Debug.LogError("[RoomNetworkHandler] Failed to send game start request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Game start request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>플레이어 준비 상태 전송</summary>
    public bool SendPlayerReadyUpdate(string playerId, string roomCode, bool isReady)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[RoomNetworkHandler] Cannot send ready update: not connected");
            return false;
        }

        try
        {
            string message = RoomProtocolExtension.CreatePlayerReadyMessage(playerId, roomCode, isReady);
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[RoomNetworkHandler] Failed to create player ready message");
                return false;
            }

            return _networkManager.SendWebSocketMessage(message, MessagePriority.Normal);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Player ready update failed: {e.Message}");
            return false;
        }
    }

    /// <summary>방 상태 동기화 요청</summary>
    public bool RequestRoomStateSync(string roomCode)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[RoomNetworkHandler] Cannot request sync: not connected");
            return false;
        }

        try
        {
            var request = new { roomCode, requestType = "state_sync", timestamp = DateTime.UtcNow };
            var message = new MatchingMessage("room_state_sync", request, 1);
            string json = MatchingProtocol.SerializeMessage(message);
            
            return _networkManager.SendWebSocketMessage(json, MessagePriority.Normal);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Room state sync request failed: {e.Message}");
            return false;
        }
    }
    #endregion

    #region Message Processing
    private void HandleWebSocketMessage(string message)
    {
        if (_isProcessingMessage)
        {
            Debug.LogWarning("[RoomNetworkHandler] Already processing message, might need queuing");
            return;
        }

        _isProcessingMessage = true;

        try
        {
            var matchingMessage = MatchingProtocol.DeserializeMessage(message);
            if (matchingMessage == null)
            {
                Debug.LogError($"[RoomNetworkHandler] Failed to parse message: {message}");
                return;
            }

            if (RoomProtocolExtension.IsValidRoomMessageType(matchingMessage.type))
            {
                ProcessRoomMessage(matchingMessage);
            }
            else
            {
                // 비방 메시지는 다른 핸들러로 전달하지 않고 무시
                Debug.Log($"[RoomNetworkHandler] Non-room message ignored: {matchingMessage.type}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Message processing failed: {e.Message}");
        }
        finally
        {
            _isProcessingMessage = false;
        }
    }

    private void ProcessRoomMessage(MatchingMessage message)
    {
        var messageType = message.type.ToLower();
        Debug.Log($"[RoomNetworkHandler] Processing room message: {messageType}");

        // 성능 측정 - 응답 메시지인 경우 응답 시간 확인
        if (IsResponseMessage(messageType))
        {
            CheckResponseTime(messageType);
        }

        switch (messageType)
        {
            case "room_created":
            case "room_joined":
            case "room_left":
            case "room_updated":
            case "room_error":
                ProcessRoomResponse(message);
                break;

            case "room_state_sync":
                ProcessRoomStateSync(message);
                break;

            case "player_joined":
                ProcessPlayerJoined(message);
                break;

            case "player_left":
                ProcessPlayerLeft(message);
                break;

            case "host_changed":
                ProcessHostChanged(message);
                break;

            case "game_starting":
            case "game_started":
                ProcessGameStart(message);
                break;

            default:
                Debug.LogWarning($"[RoomNetworkHandler] Unhandled room message type: {messageType}");
                break;
        }
    }

    private void ProcessRoomResponse(MatchingMessage message)
    {
        var response = RoomProtocolExtension.ParseRoomResponse(message);
        if (response == null)
        {
            Debug.LogError("[RoomNetworkHandler] Invalid room response");
            return;
        }

        // 대기 중인 요청 정리
        ClearPendingRequestsForRoom(response.roomCode);

        // 성능 로깅
        LogResponseTime(message.type, response.success);

        OnRoomResponse?.Invoke(response);
        Debug.Log($"[RoomNetworkHandler] Room response processed: {response.GetSummary()}");
    }

    private void ProcessRoomStateSync(MatchingMessage message)
    {
        var syncData = RoomProtocolExtension.ParseRoomStateSyncData(message);
        if (syncData == null || !RoomProtocolExtension.ValidateSyncData(syncData))
        {
            Debug.LogError("[RoomNetworkHandler] Invalid room state sync data");
            return;
        }

        OnRoomStateSync?.Invoke(syncData);
        Debug.Log($"[RoomNetworkHandler] Room state synchronized: {syncData.roomCode} v{syncData.syncVersion}");
    }

    private void ProcessPlayerJoined(MatchingMessage message)
    {
        var eventData = RoomProtocolExtension.ParseRoomEventData<dynamic>(message);
        if (eventData == null) return;

        try
        {
            string roomCode = eventData.roomCode;
            var playerInfo = JsonUtility.FromJson<PlayerInfo>(JsonUtility.ToJson(eventData.playerInfo));
            
            OnPlayerJoined?.Invoke(roomCode, playerInfo);
            Debug.Log($"[RoomNetworkHandler] Player joined: {playerInfo.Nickname} in room {roomCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Failed to process player joined: {e.Message}");
        }
    }

    private void ProcessPlayerLeft(MatchingMessage message)
    {
        var eventData = RoomProtocolExtension.ParseRoomEventData<dynamic>(message);
        if (eventData == null) return;

        try
        {
            string roomCode = eventData.roomCode;
            var playerInfo = JsonUtility.FromJson<PlayerInfo>(JsonUtility.ToJson(eventData.playerInfo));
            
            OnPlayerLeft?.Invoke(roomCode, playerInfo);
            Debug.Log($"[RoomNetworkHandler] Player left: {playerInfo.Nickname} from room {roomCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Failed to process player left: {e.Message}");
        }
    }

    private void ProcessHostChanged(MatchingMessage message)
    {
        var eventData = RoomProtocolExtension.ParseRoomEventData<dynamic>(message);
        if (eventData == null) return;

        try
        {
            string roomCode = eventData.roomCode;
            string newHostId = eventData.newHostId;
            
            OnHostChanged?.Invoke(roomCode, newHostId);
            Debug.Log($"[RoomNetworkHandler] Host changed in room {roomCode}: {newHostId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Failed to process host changed: {e.Message}");
        }
    }

    private void ProcessGameStart(MatchingMessage message)
    {
        var eventData = RoomProtocolExtension.ParseRoomEventData<dynamic>(message);
        if (eventData == null) return;

        try
        {
            string roomCode = eventData.roomCode;
            OnGameStarting?.Invoke(roomCode);
            Debug.Log($"[RoomNetworkHandler] Game starting in room {roomCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomNetworkHandler] Failed to process game start: {e.Message}");
        }
    }
    #endregion

    #region Request Management
    private void RegisterPendingRequest(string requestId, string roomCode)
    {
        lock (_lockObject)
        {
            _pendingRoomRequests[requestId] = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(roomCode))
            {
                _roomCodeMap[requestId] = roomCode;
            }
        }
    }

    private void CancelRequest(string requestId)
    {
        _timeoutManager.CancelTimeout(requestId);
        lock (_lockObject)
        {
            _pendingRoomRequests.Remove(requestId);
            _roomCodeMap.Remove(requestId);
            _requestStartTimes.Remove(requestId);
        }
    }

    private void ClearPendingRequestsForRoom(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return;

        lock (_lockObject)
        {
            var requestsToRemove = new List<string>();
            foreach (var kvp in _roomCodeMap)
            {
                if (kvp.Value == roomCode)
                {
                    requestsToRemove.Add(kvp.Key);
                }
            }

            foreach (var requestId in requestsToRemove)
            {
                _pendingRoomRequests.Remove(requestId);
                _roomCodeMap.Remove(requestId);
                _requestStartTimes.Remove(requestId);
                _timeoutManager.CancelTimeout(requestId);
            }
        }
    }

    private void ClearAllRequests()
    {
        lock (_lockObject)
        {
            _pendingRoomRequests.Clear();
            _roomCodeMap.Clear();
            _requestStartTimes.Clear();
        }
    }
    #endregion

    #region Performance Tracking
    private void TrackRequestStart(string requestId, string requestType)
    {
        lock (_lockObject)
        {
            _requestStartTimes[requestId] = DateTime.UtcNow;
        }
        Debug.Log($"[RoomNetworkHandler] Request started: {requestType} [{requestId}]");
    }

    private void CheckResponseTime(string responseType)
    {
        // 간단한 응답 시간 체크 - 실제로는 더 정교한 매핑 필요
        var targetTime = IsStateUpdateMessage(responseType) ? TARGET_SYNC_TIME : TARGET_RESPONSE_TIME;
        // 현재는 로깅만, 실제 측정은 요청-응답 매핑 후 구현
    }

    private void LogResponseTime(string messageType, bool success)
    {
        var status = success ? "SUCCESS" : "FAILED";
        Debug.Log($"[RoomNetworkHandler] Response received: {messageType} [{status}]");
    }

    private bool IsResponseMessage(string messageType)
    {
        return RoomProtocolExtension.SERVER_ROOM_MESSAGE_TYPES.Contains(messageType);
    }

    private bool IsStateUpdateMessage(string messageType)
    {
        return messageType == "room_state_sync" || messageType == "room_updated";
    }
    #endregion

    #region Event Handlers
    private void HandleConnectionChanged(bool isConnected)
    {
        Debug.Log($"[RoomNetworkHandler] Connection state changed: {isConnected}");
        
        if (isConnected)
        {
            _reconnectionManager?.StopReconnection();
        }
        else
        {
            _reconnectionManager?.StartReconnection();
        }

        OnConnectionStateChanged?.Invoke(isConnected);
    }

    private void HandleWebSocketError(string error)
    {
        Debug.LogError($"[RoomNetworkHandler] WebSocket error: {error}");
        OnRoomError?.Invoke("WEBSOCKET_ERROR", error, "");
        
        _reconnectionManager?.StartReconnection();
    }

    private void HandleRequestTimeout(string requestId, string playerId)
    {
        Debug.LogWarning($"[RoomNetworkHandler] Room request timeout: {requestId} for player {playerId}");
        
        string roomCode = "";
        lock (_lockObject)
        {
            _roomCodeMap.TryGetValue(requestId, out roomCode);
        }
        
        CancelRequest(requestId);
        OnRoomError?.Invoke("REQUEST_TIMEOUT", $"Request timed out for player {playerId}", roomCode);
    }

    private void HandleTimeoutWarning(string requestId, int remainingSeconds)
    {
        Debug.Log($"[RoomNetworkHandler] Request timeout warning: {remainingSeconds}s remaining for {requestId}");
    }
    #endregion

    #region Utility Methods
    private bool ValidateConnection(string operation)
    {
        if (!_isInitialized)
        {
            Debug.LogError($"[RoomNetworkHandler] Cannot {operation}: Not initialized");
            return false;
        }

        if (!IsConnected)
        {
            Debug.LogWarning($"[RoomNetworkHandler] Cannot {operation}: Not connected");
            
            if (_reconnectionManager != null && !_reconnectionManager.IsReconnecting)
            {
                _reconnectionManager.StartReconnection();
            }
            
            return false;
        }

        return true;
    }

    /// <summary>강제 재연결</summary>
    public void ForceReconnect()
    {
        _reconnectionManager?.StartReconnection();
    }

    /// <summary>네트워크 상태 정보</summary>
    public Dictionary<string, object> GetNetworkStatus()
    {
        return new Dictionary<string, object>
        {
            {"initialized", _isInitialized},
            {"connected", IsConnected},
            {"pendingRequests", PendingRequestCount},
            {"processingMessage", _isProcessingMessage}
        };
    }
    #endregion
}

#region Supporting Classes
/// <summary>방 요청 타임아웃 관리자</summary>
public class RoomTimeoutManager : MonoBehaviour
{
    public event Action<string, string> OnRoomRequestTimeout; // requestId, playerId
    public event Action<string, int> OnTimeoutWarning; // requestId, remainingSeconds
    
    private readonly Dictionary<string, Coroutine> _timeouts = new();
    
    public void StartRoomRequestTimeout(string requestId, string playerId, float timeoutSeconds)
    {
        if (_timeouts.ContainsKey(requestId))
        {
            StopCoroutine(_timeouts[requestId]);
        }
        
        _timeouts[requestId] = StartCoroutine(TimeoutCoroutine(requestId, playerId, timeoutSeconds));
    }
    
    public void CancelTimeout(string requestId)
    {
        if (_timeouts.TryGetValue(requestId, out var coroutine))
        {
            StopCoroutine(coroutine);
            _timeouts.Remove(requestId);
        }
    }
    
    public void StopAllTimeouts()
    {
        foreach (var coroutine in _timeouts.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        _timeouts.Clear();
    }
    
    private System.Collections.IEnumerator TimeoutCoroutine(string requestId, string playerId, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
            yield return new UnityEngine.WaitForSeconds(1f);
            elapsed += 1f;
            
            int remaining = Mathf.CeilToInt(timeoutSeconds - elapsed);
            if (remaining <= 5 && remaining > 0) // 5초 이하 남으면 경고
            {
                OnTimeoutWarning?.Invoke(requestId, remaining);
            }
        }
        
        _timeouts.Remove(requestId);
        OnRoomRequestTimeout?.Invoke(requestId, playerId);
    }
}

/// <summary>방 재연결 관리자</summary>
public class RoomReconnectionManager : MonoBehaviour
{
    public event Action OnReconnectionStarted;
    public event Action OnReconnectionSuccess;
    public event Action<int> OnReconnectionFailed; // attemptCount
    
    public bool IsReconnecting { get; private set; }
    
    private Coroutine _reconnectionCoroutine;
    private const int MAX_RECONNECT_ATTEMPTS = 5;
    private const float BASE_RETRY_DELAY = 2f;
    
    public void StartReconnection()
    {
        if (IsReconnecting) return;
        
        StopReconnection();
        _reconnectionCoroutine = StartCoroutine(ReconnectionCoroutine());
    }
    
    public void StopReconnection()
    {
        if (_reconnectionCoroutine != null)
        {
            StopCoroutine(_reconnectionCoroutine);
            _reconnectionCoroutine = null;
        }
        IsReconnecting = false;
    }
    
    private System.Collections.IEnumerator ReconnectionCoroutine()
    {
        IsReconnecting = true;
        OnReconnectionStarted?.Invoke();
        
        for (int attempt = 1; attempt <= MAX_RECONNECT_ATTEMPTS; attempt++)
        {
            yield return new UnityEngine.WaitForSeconds(BASE_RETRY_DELAY * attempt);
            
            var networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager != null)
            {
                bool connected = await networkManager.ConnectWebSocketAsync();
                if (connected)
                {
                    IsReconnecting = false;
                    OnReconnectionSuccess?.Invoke();
                    yield break;
                }
            }
            
            OnReconnectionFailed?.Invoke(attempt);
        }
        
        IsReconnecting = false;
    }
}
#endregion