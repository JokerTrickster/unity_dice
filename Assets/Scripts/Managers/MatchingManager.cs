using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

/// <summary>
/// 매칭 시스템 관리자 - 매칭의 핵심 로직과 상태 관리를 담당하는 싱글톤
/// WebSocket 연동, EnergyManager 통합, 상태 관리, 타임아웃 처리를 포함합니다.
/// </summary>
public class MatchingManager : MonoBehaviour
{
    #region Singleton
    private static MatchingManager _instance;
    public static MatchingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 기존 인스턴스 찾기
                _instance = FindObjectOfType<MatchingManager>();
                
                if (_instance == null)
                {
                    // 새 GameObject 생성 및 컴포넌트 추가
                    GameObject go = new GameObject("MatchingManager");
                    _instance = go.AddComponent<MatchingManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>매칭 상태가 변경될 때 발생하는 이벤트</summary>
    public static event Action<MatchingState> OnStateChanged;
    
    /// <summary>매칭이 완료되었을 때 발생하는 이벤트</summary>
    public static event Action<List<PlayerInfo>> OnMatchFound;
    
    /// <summary>게임이 시작될 때 발생하는 이벤트</summary>
    public static event Action<GameStartData> OnGameStarting;
    
    /// <summary>매칭이 취소되었을 때 발생하는 이벤트</summary>
    public static event Action<string> OnMatchingCancelled; // reason
    
    /// <summary>매칭이 실패했을 때 발생하는 이벤트</summary>
    public static event Action<string> OnMatchingFailed; // error message
    
    /// <summary>에너지 부족으로 매칭을 시작할 수 없을 때 발생하는 이벤트</summary>
    public static event Action<int, int> OnInsufficientEnergy; // required, current
    
    /// <summary>매칭 진행 상황이 업데이트될 때 발생하는 이벤트</summary>
    public static event Action<float> OnMatchingProgressUpdate; // elapsed seconds
    #endregion

    #region Configuration
    [Header("Configuration")]
    [SerializeField] private MatchingConfig matchingConfig;
    [SerializeField] private bool autoInitialize = true;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool simulateNetworkLatency = false;
    [SerializeField] private float simulatedLatencySeconds = 1f;
    #endregion

    #region Private Fields
    private MatchingStateManager _stateManager;
    private bool _isInitialized = false;
    private Coroutine _matchingProgressCoroutine;
    
    // Dependencies
    private EnergyManager _energyManager;
    private UserDataManager _userDataManager;
    private NetworkManager _networkManager;
    
    // Current matching data
    private MatchingRequest _currentRequest;
    private MatchingStats _matchingStats;
    
    // WebSocket integration (placeholder for Issue #17)
    private IWebSocketClient _webSocketClient;
    #endregion

    #region Properties
    /// <summary>현재 매칭 상태</summary>
    public MatchingState CurrentState => _stateManager?.State ?? MatchingState.Idle;
    
    /// <summary>현재 매칭 요청</summary>
    public MatchingRequest CurrentRequest => _currentRequest;
    
    /// <summary>초기화 완료 여부</summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>매칭 진행 중인지 확인</summary>
    public bool IsMatching => CurrentState == MatchingState.Searching || 
                              CurrentState == MatchingState.Found ||
                              CurrentState == MatchingState.Starting;
    
    /// <summary>매칭 가능한 상태인지 확인</summary>
    public bool CanStartMatching => CurrentState == MatchingState.Idle && 
                                    _isInitialized && 
                                    _energyManager?.IsInitialized == true;
    
    /// <summary>현재 상태 정보</summary>
    public MatchingStateInfo StateInfo => _stateManager?.CurrentState;
    
    /// <summary>매칭 통계</summary>
    public MatchingStats Stats => _matchingStats;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Configuration 기본값 설정
        if (matchingConfig == null)
        {
            matchingConfig = ScriptableObject.CreateInstance<MatchingConfig>();
            matchingConfig.ResetToDefault();
        }
    }
    
    private void Start()
    {
        if (autoInitialize)
        {
            Initialize();
        }
    }
    
    private void OnDestroy()
    {
        Cleanup();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 매칭 매니저 초기화
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[MatchingManager] Already initialized");
            return;
        }
        
        try
        {
            // 설정 검증
            if (!matchingConfig.ValidateConfiguration())
            {
                throw new Exception("Invalid matching configuration");
            }
            
            // 종속성 초기화
            InitializeDependencies();
            
            // 상태 관리자 초기화
            InitializeStateManager();
            
            // 통계 초기화
            InitializeStats();
            
            // 이벤트 구독
            SubscribeToEvents();
            
            _isInitialized = true;
            
            if (enableDebugLogs)
            {
                Debug.Log("[MatchingManager] Initialized successfully");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] Initialization failed: {e.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 종속성 초기화
    /// </summary>
    private void InitializeDependencies()
    {
        // EnergyManager 참조
        _energyManager = EnergyManager.Instance;
        if (_energyManager == null)
        {
            throw new Exception("EnergyManager not found");
        }
        
        // UserDataManager 참조
        _userDataManager = UserDataManager.Instance;
        if (_userDataManager == null)
        {
            throw new Exception("UserDataManager not found");
        }
        
        // NetworkManager 참조
        _networkManager = NetworkManager.Instance;
        if (_networkManager == null)
        {
            throw new Exception("NetworkManager not found");
        }
        
        // WebSocket 클라이언트는 Issue #17에서 구현 예정
        // _webSocketClient = GetWebSocketClient();
    }
    
    /// <summary>
    /// 상태 관리자 초기화
    /// </summary>
    private void InitializeStateManager()
    {
        // StateManager 컴포넌트 추가 또는 기존 것 사용
        _stateManager = GetComponent<MatchingStateManager>();
        if (_stateManager == null)
        {
            _stateManager = gameObject.AddComponent<MatchingStateManager>();
        }
        
        _stateManager.Initialize(matchingConfig);
    }
    
    /// <summary>
    /// 통계 초기화
    /// </summary>
    private void InitializeStats()
    {
        _matchingStats = LoadStatsFromPersistence() ?? new MatchingStats();
    }
    
    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // 상태 관리자 이벤트
        MatchingStateManager.OnStateChanged += OnStateManagerStateChanged;
        MatchingStateManager.OnStateTimeout += OnStateManagerTimeout;
        
        // 에너지 매니저 이벤트
        EnergyManager.OnEnergyChanged += OnEnergyChanged;
    }
    #endregion

    #region Public Matching API
    /// <summary>
    /// 랜덤 매칭 시작
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="playerCount">플레이어 수</param>
    /// <returns>매칭 시작 성공 여부</returns>
    public async Task<bool> StartRandomMatchingAsync(GameMode gameMode, int playerCount = 2)
    {
        if (!ValidateMatchingStart(gameMode, playerCount, out string validationError))
        {
            OnMatchingFailed?.Invoke(validationError);
            return false;
        }
        
        // 에너지 검증
        var gameModeConfig = matchingConfig.GetGameModeConfig(gameMode);
        if (!_energyManager.CanStartGame())
        {
            OnInsufficientEnergy?.Invoke(gameModeConfig.energyCost, _energyManager.CurrentEnergy);
            return false;
        }
        
        // 매칭 요청 생성
        _currentRequest = new MatchingRequest
        {
            playerId = GetCurrentPlayerId(),
            playerCount = playerCount,
            gameMode = gameMode,
            matchType = MatchType.Random
        };
        
        // 상태를 검색 중으로 변경
        if (!_stateManager.ChangeState(MatchingState.Searching, "User started random matching"))
        {
            OnMatchingFailed?.Invoke("Failed to start matching - invalid state");
            return false;
        }
        
        // 상태 정보 업데이트
        UpdateStateInfo(gameMode, MatchType.Random, playerCount);
        
        try
        {
            // 에너지 소모
            if (!_energyManager.ConsumeEnergyForGame(gameModeConfig.energyCost))
            {
                _stateManager.ChangeState(MatchingState.Failed, "Energy consumption failed");
                OnMatchingFailed?.Invoke("Failed to consume energy for matching");
                return false;
            }
            
            // 서버에 매칭 요청 전송
            bool networkSuccess = await SendMatchingRequestToServer(_currentRequest);
            
            if (!networkSuccess)
            {
                // 에너지 환불 처리는 EnergyManager에서 별도 처리 필요
                _stateManager.ChangeState(MatchingState.Failed, "Network request failed");
                OnMatchingFailed?.Invoke("Failed to send matching request to server");
                return false;
            }
            
            // 매칭 진행 상황 추적 시작
            StartMatchingProgressTracking();
            
            // 통계 업데이트
            _matchingStats.totalMatchesAttempted++;
            SaveStatsToPersistence();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MatchingManager] Started random matching: {gameMode}, {playerCount} players");
            }
            
            return true;
        }
        catch (Exception e)
        {
            _stateManager.ChangeState(MatchingState.Failed, $"Exception: {e.Message}");
            OnMatchingFailed?.Invoke($"Matching failed with exception: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 매칭 취소
    /// </summary>
    /// <param name="reason">취소 이유</param>
    /// <returns>취소 성공 여부</returns>
    public bool CancelMatching(string reason = "User cancelled")
    {
        if (!IsMatching)
        {
            Debug.LogWarning("[MatchingManager] No active matching to cancel");
            return false;
        }
        
        try
        {
            // 서버에 취소 요청 전송 (비동기로 처리)
            _ = SendMatchingCancellationToServer();
            
            // 상태 변경
            _stateManager.ChangeState(MatchingState.Cancelled, reason);
            
            // 진행 상황 추적 중지
            StopMatchingProgressTracking();
            
            // 통계 업데이트
            _matchingStats.totalMatchesCancelled++;
            SaveStatsToPersistence();
            
            OnMatchingCancelled?.Invoke(reason);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MatchingManager] Matching cancelled: {reason}");
            }
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] Failed to cancel matching: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 매칭 재시도
    /// </summary>
    /// <returns>재시도 성공 여부</returns>
    public async Task<bool> RetryMatchingAsync()
    {
        if (_currentRequest == null)
        {
            OnMatchingFailed?.Invoke("No previous matching request to retry");
            return false;
        }
        
        if (CurrentState != MatchingState.Failed)
        {
            OnMatchingFailed?.Invoke("Can only retry from failed state");
            return false;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("[MatchingManager] Retrying previous matching request");
        }
        
        return await StartRandomMatchingAsync(_currentRequest.gameMode, _currentRequest.playerCount);
    }
    #endregion

    #region Network Integration
    /// <summary>
    /// 서버에 매칭 요청 전송
    /// </summary>
    /// <param name="request">매칭 요청</param>
    /// <returns>전송 성공 여부</returns>
    private async Task<bool> SendMatchingRequestToServer(MatchingRequest request)
    {
        try
        {
            if (simulateNetworkLatency)
            {
                await Task.Delay(Mathf.RoundToInt(simulatedLatencySeconds * 1000));
            }
            
            // WebSocket을 통한 실시간 매칭 (Issue #17 완료 후 구현)
            if (_webSocketClient != null)
            {
                return await SendMatchingRequestViaWebSocket(request);
            }
            
            // HTTP API를 통한 매칭 (fallback)
            return await SendMatchingRequestViaHTTP(request);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] Network request failed: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// WebSocket을 통한 매칭 요청 (Issue #17 구현 완료 후)
    /// </summary>
    /// <param name="request">매칭 요청</param>
    /// <returns>전송 성공 여부</returns>
    private async Task<bool> SendMatchingRequestViaWebSocket(MatchingRequest request)
    {
        // WebSocket 클라이언트가 구현되면 여기에 실제 로직 추가
        // 현재는 인터페이스만 정의
        await Task.Delay(100); // 임시
        return true;
    }
    
    /// <summary>
    /// HTTP API를 통한 매칭 요청
    /// </summary>
    /// <param name="request">매칭 요청</param>
    /// <returns>전송 성공 여부</returns>
    private async Task<bool> SendMatchingRequestViaHTTP(MatchingRequest request)
    {
        try
        {
            var response = await _networkManager.PostAsync(matchingConfig.MatchingEndpoint, request, 
                matchingConfig.ConnectionTimeoutSeconds);
            
            if (response.IsSuccess)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[MatchingManager] Matching request sent successfully via HTTP");
                }
                return true;
            }
            else
            {
                Debug.LogError($"[MatchingManager] HTTP matching request failed: {response.Error}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] HTTP matching request exception: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 서버에 매칭 취소 요청 전송
    /// </summary>
    /// <returns>전송 작업</returns>
    private async Task SendMatchingCancellationToServer()
    {
        if (_currentRequest == null) return;
        
        try
        {
            var cancellationRequest = new
            {
                playerId = _currentRequest.playerId,
                action = "cancel_matching"
            };
            
            await _networkManager.PostAsync($"{matchingConfig.MatchingEndpoint}/cancel", 
                cancellationRequest, 10f);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MatchingManager] Failed to send cancellation to server: {e.Message}");
        }
    }
    #endregion

    #region Message Handling (WebSocket Integration Points)
    /// <summary>
    /// 매칭 응답 처리 (WebSocket 메시지 핸들링)
    /// </summary>
    /// <param name="response">매칭 응답</param>
    public void HandleMatchingResponse(MatchingResponse response)
    {
        if (response == null) return;
        
        switch (response.type)
        {
            case "matching_found":
                HandleMatchFound(response);
                break;
                
            case "matching_cancelled":
                HandleMatchCancelled(response);
                break;
                
            case "matching_failed":
                HandleMatchFailed(response);
                break;
                
            case "game_starting":
                HandleGameStarting(response);
                break;
                
            default:
                Debug.LogWarning($"[MatchingManager] Unknown response type: {response.type}");
                break;
        }
    }
    
    /// <summary>
    /// 매칭 완료 처리
    /// </summary>
    /// <param name="response">매칭 응답</param>
    private void HandleMatchFound(MatchingResponse response)
    {
        if (!_stateManager.CanTransitionTo(MatchingState.Found))
        {
            Debug.LogWarning("[MatchingManager] Cannot transition to Found state");
            return;
        }
        
        // 상태 변경
        _stateManager.ChangeState(MatchingState.Found, "Server confirmed match found");
        
        // 매칭된 플레이어 정보 설정
        _stateManager.SetMatchedPlayers(response.players);
        _stateManager.SetCurrentRoomCode(response.roomId);
        
        // 진행 상황 추적 중지
        StopMatchingProgressTracking();
        
        // 통계 업데이트
        _matchingStats.totalMatchesCompleted++;
        var searchTime = _stateManager.CurrentState.CurrentSearchTime;
        UpdateAverageWaitTime(searchTime);
        _matchingStats.lastMatchTime = DateTime.Now;
        SaveStatsToPersistence();
        
        OnMatchFound?.Invoke(response.players);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[MatchingManager] Match found with {response.players?.Count ?? 0} players");
        }
    }
    
    /// <summary>
    /// 매칭 취소 처리
    /// </summary>
    /// <param name="response">매칭 응답</param>
    private void HandleMatchCancelled(MatchingResponse response)
    {
        _stateManager.ChangeState(MatchingState.Cancelled, "Server confirmed cancellation");
        StopMatchingProgressTracking();
        OnMatchingCancelled?.Invoke(response.error ?? "Server cancelled matching");
    }
    
    /// <summary>
    /// 매칭 실패 처리
    /// </summary>
    /// <param name="response">매칭 응답</param>
    private void HandleMatchFailed(MatchingResponse response)
    {
        _stateManager.ChangeState(MatchingState.Failed, $"Server error: {response.error}");
        StopMatchingProgressTracking();
        OnMatchingFailed?.Invoke(response.error ?? "Unknown server error");
    }
    
    /// <summary>
    /// 게임 시작 처리
    /// </summary>
    /// <param name="response">매칭 응답</param>
    private void HandleGameStarting(MatchingResponse response)
    {
        if (!_stateManager.CanTransitionTo(MatchingState.Starting))
        {
            Debug.LogWarning("[MatchingManager] Cannot transition to Starting state");
            return;
        }
        
        _stateManager.ChangeState(MatchingState.Starting, "Server initiated game start");
        
        var gameStartData = new GameStartData
        {
            roomId = response.roomId,
            players = response.players,
            gameMode = response.gameMode,
            gameSettings = response.metadata,
            startCountdown = response.estimatedStartTime
        };
        
        OnGameStarting?.Invoke(gameStartData);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[MatchingManager] Game starting in room: {response.roomId}");
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// 매칭 시작 유효성 검사
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="playerCount">플레이어 수</param>
    /// <param name="error">에러 메시지</param>
    /// <returns>유효성 검사 통과 여부</returns>
    private bool ValidateMatchingStart(GameMode gameMode, int playerCount, out string error)
    {
        error = "";
        
        // 초기화 확인
        if (!_isInitialized)
        {
            error = "MatchingManager not initialized";
            return false;
        }
        
        // 매칭 시스템 활성화 확인
        if (!matchingConfig.EnableMatching)
        {
            error = "Matching system is disabled";
            return false;
        }
        
        // 현재 상태 확인
        if (!CanStartMatching)
        {
            error = $"Cannot start matching from current state: {CurrentState}";
            return false;
        }
        
        // 게임 모드 유효성 확인
        if (!matchingConfig.IsGameModeEnabled(gameMode))
        {
            error = $"Game mode {gameMode} is not enabled";
            return false;
        }
        
        // 플레이어 수 확인
        if (!matchingConfig.IsPlayerCountAllowed(playerCount))
        {
            error = $"Player count {playerCount} is not allowed";
            return false;
        }
        
        // 사용자 정보 확인
        var currentUser = _userDataManager.CurrentUser;
        if (currentUser == null)
        {
            error = "User data not available";
            return false;
        }
        
        // 레벨 요구사항 확인
        var gameModeConfig = matchingConfig.GetGameModeConfig(gameMode);
        if (currentUser.Level < gameModeConfig.minimumLevel)
        {
            error = $"Minimum level {gameModeConfig.minimumLevel} required for {gameMode}";
            return false;
        }
        
        // 네트워크 연결 확인
        if (!_networkManager.IsNetworkAvailable)
        {
            error = "Network connection not available";
            return false;
        }
        
        return true;
    }
    #endregion

    #region Progress Tracking
    /// <summary>
    /// 매칭 진행 상황 추적 시작
    /// </summary>
    private void StartMatchingProgressTracking()
    {
        StopMatchingProgressTracking();
        _matchingProgressCoroutine = StartCoroutine(MatchingProgressCoroutine());
    }
    
    /// <summary>
    /// 매칭 진행 상황 추적 중지
    /// </summary>
    private void StopMatchingProgressTracking()
    {
        if (_matchingProgressCoroutine != null)
        {
            StopCoroutine(_matchingProgressCoroutine);
            _matchingProgressCoroutine = null;
        }
    }
    
    /// <summary>
    /// 매칭 진행 상황 추적 코루틴
    /// </summary>
    /// <returns></returns>
    private IEnumerator MatchingProgressCoroutine()
    {
        while (IsMatching)
        {
            float elapsedTime = _stateManager.CurrentState?.CurrentSearchTime ?? 0f;
            OnMatchingProgressUpdate?.Invoke(elapsedTime);
            
            yield return new WaitForSeconds(matchingConfig.UIUpdateIntervalSeconds);
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 상태 관리자의 상태 변경 이벤트 핸들러
    /// </summary>
    /// <param name="previousState">이전 상태</param>
    /// <param name="newState">새로운 상태</param>
    private void OnStateManagerStateChanged(MatchingState previousState, MatchingState newState)
    {
        OnStateChanged?.Invoke(newState);
        
        // 특정 상태 전환 시 추가 처리
        if (newState == MatchingState.Idle)
        {
            _currentRequest = null;
            StopMatchingProgressTracking();
        }
    }
    
    /// <summary>
    /// 상태 관리자의 타임아웃 이벤트 핸들러
    /// </summary>
    /// <param name="state">타임아웃된 상태</param>
    private void OnStateManagerTimeout(MatchingState state)
    {
        string timeoutMessage = $"{state} state timed out";
        OnMatchingFailed?.Invoke(timeoutMessage);
        
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[MatchingManager] {timeoutMessage}");
        }
    }
    
    /// <summary>
    /// 에너지 변경 이벤트 핸들러
    /// </summary>
    /// <param name="current">현재 에너지</param>
    /// <param name="max">최대 에너지</param>
    private void OnEnergyChanged(int current, int max)
    {
        // 매칭 중에 에너지가 부족해지면 처리
        if (IsMatching && !_energyManager.CanStartGame())
        {
            Debug.LogWarning("[MatchingManager] Energy depleted during matching");
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 현재 플레이어 ID 가져오기
    /// </summary>
    /// <returns>플레이어 ID</returns>
    private string GetCurrentPlayerId()
    {
        return _userDataManager?.CurrentUser?.UserId ?? "unknown_player";
    }
    
    /// <summary>
    /// 상태 정보 업데이트
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="matchType">매칭 타입</param>
    /// <param name="playerCount">플레이어 수</param>
    private void UpdateStateInfo(GameMode gameMode, MatchType matchType, int playerCount)
    {
        _stateManager.SetSelectedGameMode(gameMode);
        _stateManager.SetMatchType(matchType);
        _stateManager.SetSelectedPlayerCount(playerCount);
    }
    
    /// <summary>
    /// 평균 대기 시간 업데이트
    /// </summary>
    /// <param name="waitTime">대기 시간 (초)</param>
    private void UpdateAverageWaitTime(float waitTime)
    {
        if (_matchingStats.totalMatchesCompleted == 1)
        {
            _matchingStats.averageWaitTime = waitTime;
        }
        else
        {
            _matchingStats.averageWaitTime = 
                (_matchingStats.averageWaitTime * (_matchingStats.totalMatchesCompleted - 1) + waitTime) /
                _matchingStats.totalMatchesCompleted;
        }
    }
    #endregion

    #region Statistics Persistence
    /// <summary>
    /// 통계를 지속성 저장소에 저장
    /// </summary>
    private void SaveStatsToPersistence()
    {
        try
        {
            string json = JsonUtility.ToJson(_matchingStats);
            PlayerPrefs.SetString("MatchingStats", json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] Failed to save stats: {e.Message}");
        }
    }
    
    /// <summary>
    /// 지속성 저장소에서 통계 로드
    /// </summary>
    /// <returns>로드된 통계 또는 null</returns>
    private MatchingStats LoadStatsFromPersistence()
    {
        try
        {
            string json = PlayerPrefs.GetString("MatchingStats", "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<MatchingStats>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingManager] Failed to load stats: {e.Message}");
            return null;
        }
    }
    #endregion

    #region Public Query Methods
    /// <summary>
    /// 특정 게임 모드로 매칭 가능한지 확인
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <returns>매칭 가능 여부</returns>
    public bool CanMatchWithGameMode(GameMode gameMode)
    {
        return ValidateMatchingStart(gameMode, 2, out _);
    }
    
    /// <summary>
    /// 현재 매칭 정보 가져오기
    /// </summary>
    /// <returns>매칭 정보</returns>
    public MatchingStateInfo GetCurrentMatchingInfo()
    {
        return _stateManager?.CurrentState ?? new MatchingStateInfo();
    }
    
    /// <summary>
    /// 매칭 설정 가져오기
    /// </summary>
    /// <returns>매칭 설정</returns>
    public MatchingConfig GetConfiguration()
    {
        return matchingConfig;
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 리소스 정리
    /// </summary>
    private void Cleanup()
    {
        // 이벤트 구독 해제
        MatchingStateManager.OnStateChanged -= OnStateManagerStateChanged;
        MatchingStateManager.OnStateTimeout -= OnStateManagerTimeout;
        
        if (_energyManager != null)
        {
            EnergyManager.OnEnergyChanged -= OnEnergyChanged;
        }
        
        // 진행 중인 작업 중지
        StopMatchingProgressTracking();
        
        // 이벤트 정리
        OnStateChanged = null;
        OnMatchFound = null;
        OnGameStarting = null;
        OnMatchingCancelled = null;
        OnMatchingFailed = null;
        OnInsufficientEnergy = null;
        OnMatchingProgressUpdate = null;
        
        if (enableDebugLogs)
        {
            Debug.Log("[MatchingManager] Cleanup completed");
        }
    }
    #endregion
}

#region WebSocket Integration Interface (Issue #17)
/// <summary>
/// WebSocket 클라이언트 인터페이스 (Issue #17에서 구현 예정)
/// </summary>
public interface IWebSocketClient
{
    Task<bool> SendAsync(string message);
    bool IsConnected { get; }
    event Action<string> OnMessageReceived;
    event Action<bool> OnConnectionStatusChanged;
}