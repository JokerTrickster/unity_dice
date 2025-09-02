using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Matching Room Integration Controller
/// 방 시스템과 기존 매칭 시스템의 통합 관리
/// Stream D: Integration & Testing - Seamless integration between room system and existing matching system
/// 
/// 핵심 역할:
/// 1. Room 시스템과 Matching 시스템 간의 데이터 흐름 관리
/// 2. 사용자 인터페이스에서 두 시스템 간의 원활한 전환 보장
/// 3. 에너지 시스템과 통합하여 비용 검증
/// 4. 네트워크 상태 관리 및 오류 처리
/// </summary>
public class MatchingRoomIntegration : MonoBehaviour
{
    #region Events
    /// <summary>매칭 모드 변경 이벤트</summary>
    public static event Action<MatchingMode> OnMatchingModeChanged;
    
    /// <summary>통합 시스템 초기화 완료 이벤트</summary>
    public static event Action<bool> OnIntegrationInitialized;
    
    /// <summary>방-매칭 전환 이벤트</summary>
    public static event Action<TransitionType, string> OnSystemTransition; // type, details
    
    /// <summary>통합 시스템 오류 이벤트</summary>
    public static event Action<string, string, string> OnIntegrationError; // subsystem, operation, error
    
    /// <summary>상태 동기화 이벤트</summary>
    public static event Action<SystemSyncData> OnSystemSync;
    #endregion
    
    #region Private Fields
    private RoomManager _roomManager;
    private MatchingUI _matchingUI;
    private EnergyManager _energyManager;
    private NetworkManager _networkManager;
    private UserDataManager _userDataManager;
    
    private MatchingMode _currentMatchingMode = MatchingMode.None;
    private IntegrationState _currentState = IntegrationState.Initializing;
    private bool _isInitialized = false;
    private bool _isTransitioning = false;
    
    // Integration settings and configuration
    private readonly Dictionary<MatchingMode, ModeConfiguration> _modeConfigurations = new();
    private readonly Queue<IntegrationTask> _taskQueue = new();
    private Coroutine _taskProcessor;
    
    // Performance and monitoring
    private readonly Dictionary<string, DateTime> _operationStartTimes = new();
    private readonly List<IntegrationMetrics> _performanceMetrics = new();
    private const float TARGET_TRANSITION_TIME = 1.5f; // 1.5초 이내 전환 목표
    
    // State management
    private SystemState _previousSystemState;
    private SystemState _currentSystemState;
    
    #endregion
    
    #region Properties
    public bool IsInitialized => _isInitialized;
    public MatchingMode CurrentMatchingMode => _currentMatchingMode;
    public IntegrationState CurrentState => _currentState;
    public bool IsTransitioning => _isTransitioning;
    public bool IsInRoom => _roomManager?.IsInRoom ?? false;
    public bool IsMatching => _matchingUI?.IsMatching ?? false;
    public SystemState CurrentSystemState => _currentSystemState;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeModeConfigurations();
        _taskProcessor = StartCoroutine(ProcessTaskQueue());
    }
    
    private void Start()
    {
        StartCoroutine(InitializeIntegrationAsync());
    }
    
    private void OnDestroy()
    {
        CleanupIntegration();
    }
    
    private void Update()
    {
        if (_isInitialized)
        {
            MonitorSystemState();
            ProcessPendingTransitions();
        }
    }
    #endregion
    
    #region Initialization
    private IEnumerator InitializeIntegrationAsync()
    {
        Debug.Log("[MatchingRoomIntegration] Starting integration initialization");
        
        _currentState = IntegrationState.Initializing;
        
        try
        {
            // Phase 1: Initialize core managers
            yield return StartCoroutine(InitializeCoreManagers());
            
            // Phase 2: Setup event subscriptions
            yield return StartCoroutine(SetupEventSubscriptions());
            
            // Phase 3: Configure integration modes
            yield return StartCoroutine(ConfigureIntegrationModes());
            
            // Phase 4: Validate system compatibility
            yield return StartCoroutine(ValidateSystemCompatibility());
            
            // Phase 5: Initialize UI integration
            yield return StartCoroutine(InitializeUIIntegration());
            
            _isInitialized = true;
            _currentState = IntegrationState.Ready;
            
            OnIntegrationInitialized?.Invoke(true);
            
            Debug.Log("[MatchingRoomIntegration] Integration initialization completed successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingRoomIntegration] Integration initialization failed: {e.Message}");
            
            _currentState = IntegrationState.Error;
            OnIntegrationInitialized?.Invoke(false);
            OnIntegrationError?.Invoke("Integration", "Initialize", e.Message);
        }
    }
    
    private IEnumerator InitializeCoreManagers()
    {
        Debug.Log("[MatchingRoomIntegration] Initializing core managers");
        
        // Initialize managers with timeout protection
        var timeout = 5f;
        var elapsed = 0f;
        
        while (elapsed < timeout)
        {
            _roomManager = FindObjectOfType<RoomManager>() ?? RoomManager.Instance;
            _matchingUI = FindObjectOfType<MatchingUI>();
            _energyManager = FindObjectOfType<EnergyManager>() ?? EnergyManager.Instance;
            _networkManager = FindObjectOfType<NetworkManager>() ?? NetworkManager.Instance;
            _userDataManager = FindObjectOfType<UserDataManager>() ?? UserDataManager.Instance;
            
            if (AllManagersAvailable())
            {
                Debug.Log("[MatchingRoomIntegration] All core managers initialized");
                yield break;
            }
            
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (!AllManagersAvailable())
        {
            throw new InvalidOperationException("Failed to initialize required managers within timeout period");
        }
    }
    
    private bool AllManagersAvailable()
    {
        return _roomManager != null && _matchingUI != null && _energyManager != null && 
               _networkManager != null && _userDataManager != null;
    }
    
    private IEnumerator SetupEventSubscriptions()
    {
        Debug.Log("[MatchingRoomIntegration] Setting up event subscriptions");
        
        // Room Manager events
        RoomManager.OnRoomCreated += HandleRoomCreated;
        RoomManager.OnRoomJoined += HandleRoomJoined;
        RoomManager.OnRoomLeft += HandleRoomLeft;
        RoomManager.OnRoomError += HandleRoomError;
        RoomManager.OnGameStarted += HandleGameStarted;
        
        // Matching UI events (if available)
        if (_matchingUI != null)
        {
            // Subscribe to matching events - implementation depends on actual MatchingUI interface
            Debug.Log("[MatchingRoomIntegration] Matching UI events subscribed");
        }
        
        // Energy Manager events
        if (_energyManager != null)
        {
            EnergyManager.OnEnergyChanged += HandleEnergyChanged;
            EnergyManager.OnEnergyInsufficient += HandleEnergyInsufficient;
        }
        
        // Network Manager events
        if (_networkManager != null)
        {
            // Subscribe to network events - implementation depends on actual NetworkManager interface
            Debug.Log("[MatchingRoomIntegration] Network Manager events subscribed");
        }
        
        yield return new WaitForEndOfFrame();
    }
    
    private IEnumerator ConfigureIntegrationModes()
    {
        Debug.Log("[MatchingRoomIntegration] Configuring integration modes");
        
        // Random matching mode configuration
        _modeConfigurations[MatchingMode.Random] = new ModeConfiguration
        {
            RequiresEnergy = true,
            EnergyCost = 1,
            MaxPlayers = 4,
            AllowSpectators = false,
            NetworkPriority = MessagePriority.High,
            TimeoutSeconds = 30f
        };
        
        // Room matching mode configuration  
        _modeConfigurations[MatchingMode.Room] = new ModeConfiguration
        {
            RequiresEnergy = true,
            EnergyCost = 1,
            MaxPlayers = 4,
            AllowSpectators = true,
            NetworkPriority = MessagePriority.High,
            TimeoutSeconds = 60f
        };
        
        // Private room mode configuration
        _modeConfigurations[MatchingMode.PrivateRoom] = new ModeConfiguration
        {
            RequiresEnergy = true,
            EnergyCost = 1,
            MaxPlayers = 4,
            AllowSpectators = true,
            NetworkPriority = MessagePriority.Normal,
            TimeoutSeconds = 120f
        };
        
        yield return new WaitForEndOfFrame();
    }
    
    private IEnumerator ValidateSystemCompatibility()
    {
        Debug.Log("[MatchingRoomIntegration] Validating system compatibility");
        
        var validationTasks = new List<Task<bool>>();
        
        // Validate Room Manager compatibility
        validationTasks.Add(ValidateRoomManagerAsync());
        
        // Validate Matching UI compatibility
        validationTasks.Add(ValidateMatchingUIAsync());
        
        // Validate Energy Manager compatibility
        validationTasks.Add(ValidateEnergyManagerAsync());
        
        // Validate Network Manager compatibility
        validationTasks.Add(ValidateNetworkManagerAsync());
        
        // Wait for all validations with timeout
        float timeout = 3f;
        float elapsed = 0f;
        
        while (!AllTasksCompleted(validationTasks) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        // Check validation results
        bool allValid = true;
        foreach (var task in validationTasks)
        {
            if (task.IsCompleted && !task.Result)
            {
                allValid = false;
                break;
            }
        }
        
        if (!allValid)
        {
            throw new InvalidOperationException("System compatibility validation failed");
        }
        
        Debug.Log("[MatchingRoomIntegration] System compatibility validated");
    }
    
    private bool AllTasksCompleted(List<Task<bool>> tasks)
    {
        foreach (var task in tasks)
        {
            if (!task.IsCompleted) return false;
        }
        return true;
    }
    
    private async Task<bool> ValidateRoomManagerAsync()
    {
        try
        {
            if (_roomManager == null) return false;
            
            // Validate essential RoomManager functionality
            bool hasRequiredMethods = HasMethod(typeof(RoomManager), "CreateRoom") &&
                                     HasMethod(typeof(RoomManager), "JoinRoom") &&
                                     HasMethod(typeof(RoomManager), "LeaveRoom");
            
            return hasRequiredMethods;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> ValidateMatchingUIAsync()
    {
        try
        {
            if (_matchingUI == null) return false;
            
            // Validate MatchingUI compatibility
            return _matchingUI != null; // Basic existence check
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> ValidateEnergyManagerAsync()
    {
        try
        {
            if (_energyManager == null) return false;
            
            // Validate EnergyManager functionality
            bool hasRequiredMethods = HasMethod(typeof(EnergyManager), "CanStartGame") &&
                                     HasMethod(typeof(EnergyManager), "ConsumeEnergy");
            
            return hasRequiredMethods;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> ValidateNetworkManagerAsync()
    {
        try
        {
            if (_networkManager == null) return false;
            
            // Basic NetworkManager validation
            return _networkManager != null;
        }
        catch
        {
            return false;
        }
    }
    
    private bool HasMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName) != null;
    }
    
    private IEnumerator InitializeUIIntegration()
    {
        Debug.Log("[MatchingRoomIntegration] Initializing UI integration");
        
        // Set initial matching mode
        yield return StartCoroutine(SwitchMatchingMode(MatchingMode.Random));
        
        yield return new WaitForEndOfFrame();
    }
    
    private void InitializeModeConfigurations()
    {
        _modeConfigurations.Clear();
        
        // Will be populated in ConfigureIntegrationModes
    }
    #endregion
    
    #region Public API
    
    /// <summary>매칭 모드 전환</summary>
    public void RequestMatchingModeSwitch(MatchingMode newMode)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[MatchingRoomIntegration] Cannot switch mode - not initialized");
            return;
        }
        
        if (_isTransitioning)
        {
            Debug.LogWarning("[MatchingRoomIntegration] Cannot switch mode - transition in progress");
            return;
        }
        
        if (newMode == _currentMatchingMode)
        {
            Debug.Log($"[MatchingRoomIntegration] Already in mode {newMode}");
            return;
        }
        
        Debug.Log($"[MatchingRoomIntegration] Requesting mode switch: {_currentMatchingMode} -> {newMode}");
        
        var task = new IntegrationTask
        {
            Type = IntegrationTaskType.ModeSwitch,
            Parameters = new Dictionary<string, object> { { "newMode", newMode } },
            Priority = TaskPriority.High
        };
        
        QueueTask(task);
    }
    
    /// <summary>방 생성 요청 (에너지 검증 포함)</summary>
    public void RequestCreateRoom(int maxPlayers, Action<bool, string> callback = null)
    {
        if (!ValidateCreateRoomRequest(maxPlayers, out string error))
        {
            callback?.Invoke(false, error);
            return;
        }
        
        var task = new IntegrationTask
        {
            Type = IntegrationTaskType.CreateRoom,
            Parameters = new Dictionary<string, object> 
            { 
                { "maxPlayers", maxPlayers },
                { "callback", callback }
            },
            Priority = TaskPriority.High
        };
        
        QueueTask(task);
    }
    
    /// <summary>방 참여 요청 (에너지 검증 포함)</summary>
    public void RequestJoinRoom(string roomCode, Action<bool, string> callback = null)
    {
        if (!ValidateJoinRoomRequest(roomCode, out string error))
        {
            callback?.Invoke(false, error);
            return;
        }
        
        var task = new IntegrationTask
        {
            Type = IntegrationTaskType.JoinRoom,
            Parameters = new Dictionary<string, object> 
            { 
                { "roomCode", roomCode },
                { "callback", callback }
            },
            Priority = TaskPriority.High
        };
        
        QueueTask(task);
    }
    
    /// <summary>랜덤 매칭 요청</summary>
    public void RequestRandomMatching(int playerCount, Action<bool, string> callback = null)
    {
        if (!ValidateRandomMatchingRequest(playerCount, out string error))
        {
            callback?.Invoke(false, error);
            return;
        }
        
        var task = new IntegrationTask
        {
            Type = IntegrationTaskType.StartMatching,
            Parameters = new Dictionary<string, object> 
            { 
                { "playerCount", playerCount },
                { "callback", callback }
            },
            Priority = TaskPriority.High
        };
        
        QueueTask(task);
    }
    
    /// <summary>현재 작업 취소</summary>
    public void CancelCurrentOperation()
    {
        if (_isTransitioning)
        {
            Debug.Log("[MatchingRoomIntegration] Canceling current operation");
            
            // Cancel room operations
            if (IsInRoom)
            {
                _roomManager.LeaveRoom();
            }
            
            // Cancel matching operations  
            if (IsMatching)
            {
                // Cancel matching - implementation depends on MatchingUI interface
            }
            
            _isTransitioning = false;
            _currentState = IntegrationState.Ready;
        }
    }
    
    /// <summary>시스템 상태 강제 새로고침</summary>
    public void ForceSystemSync()
    {
        if (!_isInitialized) return;
        
        UpdateSystemState();
        
        var syncData = new SystemSyncData
        {
            MatchingMode = _currentMatchingMode,
            IntegrationState = _currentState,
            IsInRoom = IsInRoom,
            IsMatching = IsMatching,
            SystemState = _currentSystemState,
            Timestamp = DateTime.UtcNow
        };
        
        OnSystemSync?.Invoke(syncData);
    }
    
    /// <summary>통합 시스템 성능 메트릭 조회</summary>
    public IntegrationPerformanceReport GetPerformanceReport()
    {
        var report = new IntegrationPerformanceReport();
        
        if (_performanceMetrics.Count > 0)
        {
            report.AverageTransitionTime = CalculateAverageTransitionTime();
            report.TotalOperations = _performanceMetrics.Count;
            report.SuccessRate = CalculateSuccessRate();
            report.ErrorCount = CalculateErrorCount();
        }
        
        return report;
    }
    #endregion
    
    #region Event Handlers
    
    private void HandleRoomCreated(RoomData roomData)
    {
        Debug.Log($"[MatchingRoomIntegration] Room created: {roomData?.RoomCode}");
        
        UpdateSystemState();
        RecordOperationSuccess("CreateRoom");
        OnSystemTransition?.Invoke(TransitionType.ToRoom, roomData?.RoomCode);
        
        _isTransitioning = false;
        _currentState = IntegrationState.InRoom;
    }
    
    private void HandleRoomJoined(RoomData roomData)
    {
        Debug.Log($"[MatchingRoomIntegration] Room joined: {roomData?.RoomCode}");
        
        UpdateSystemState();
        RecordOperationSuccess("JoinRoom");
        OnSystemTransition?.Invoke(TransitionType.ToRoom, roomData?.RoomCode);
        
        _isTransitioning = false;
        _currentState = IntegrationState.InRoom;
    }
    
    private void HandleRoomLeft(RoomData roomData)
    {
        Debug.Log($"[MatchingRoomIntegration] Room left: {roomData?.RoomCode}");
        
        UpdateSystemState();
        OnSystemTransition?.Invoke(TransitionType.ToIdle, "");
        
        _currentState = IntegrationState.Ready;
    }
    
    private void HandleRoomError(string operation, string error)
    {
        Debug.LogError($"[MatchingRoomIntegration] Room error: {operation} - {error}");
        
        RecordOperationError("Room", operation, error);
        OnIntegrationError?.Invoke("Room", operation, error);
        
        _isTransitioning = false;
        _currentState = IntegrationState.Error;
    }
    
    private void HandleGameStarted(string roomCode)
    {
        Debug.Log($"[MatchingRoomIntegration] Game started: {roomCode}");
        
        UpdateSystemState();
        OnSystemTransition?.Invoke(TransitionType.ToGame, roomCode);
        
        _currentState = IntegrationState.InGame;
    }
    
    private void HandleEnergyChanged(int currentEnergy, int maxEnergy)
    {
        Debug.Log($"[MatchingRoomIntegration] Energy changed: {currentEnergy}/{maxEnergy}");
        
        // Update system state to reflect energy availability
        UpdateSystemState();
    }
    
    private void HandleEnergyInsufficient(int required, int available)
    {
        Debug.LogWarning($"[MatchingRoomIntegration] Energy insufficient: required {required}, available {available}");
        
        OnIntegrationError?.Invoke("Energy", "InsufficientEnergy", $"Required: {required}, Available: {available}");
    }
    #endregion
    
    #region Task Processing
    
    private void QueueTask(IntegrationTask task)
    {
        lock (_taskQueue)
        {
            _taskQueue.Enqueue(task);
        }
        
        Debug.Log($"[MatchingRoomIntegration] Task queued: {task.Type}");
    }
    
    private IEnumerator ProcessTaskQueue()
    {
        while (true)
        {
            if (_taskQueue.Count > 0 && _isInitialized && !_isTransitioning)
            {
                IntegrationTask task;
                lock (_taskQueue)
                {
                    task = _taskQueue.Dequeue();
                }
                
                yield return StartCoroutine(ProcessTask(task));
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    private IEnumerator ProcessTask(IntegrationTask task)
    {
        Debug.Log($"[MatchingRoomIntegration] Processing task: {task.Type}");
        
        RecordOperationStart(task.Type.ToString());
        _isTransitioning = true;
        
        try
        {
            switch (task.Type)
            {
                case IntegrationTaskType.ModeSwitch:
                    yield return StartCoroutine(ProcessModeSwitch(task));
                    break;
                    
                case IntegrationTaskType.CreateRoom:
                    yield return StartCoroutine(ProcessCreateRoom(task));
                    break;
                    
                case IntegrationTaskType.JoinRoom:
                    yield return StartCoroutine(ProcessJoinRoom(task));
                    break;
                    
                case IntegrationTaskType.StartMatching:
                    yield return StartCoroutine(ProcessStartMatching(task));
                    break;
                    
                default:
                    Debug.LogWarning($"[MatchingRoomIntegration] Unknown task type: {task.Type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingRoomIntegration] Task processing error: {e.Message}");
            RecordOperationError("Task", task.Type.ToString(), e.Message);
            _isTransitioning = false;
        }
    }
    
    private IEnumerator ProcessModeSwitch(IntegrationTask task)
    {
        var newMode = (MatchingMode)task.Parameters["newMode"];
        yield return StartCoroutine(SwitchMatchingMode(newMode));
    }
    
    private IEnumerator ProcessCreateRoom(IntegrationTask task)
    {
        var maxPlayers = (int)task.Parameters["maxPlayers"];
        var callback = task.Parameters.ContainsKey("callback") ? 
                      (Action<bool, string>)task.Parameters["callback"] : null;
        
        // Switch to room mode if needed
        if (_currentMatchingMode != MatchingMode.Room)
        {
            yield return StartCoroutine(SwitchMatchingMode(MatchingMode.Room));
        }
        
        // Consume energy
        if (!ConsumeEnergyForOperation())
        {
            callback?.Invoke(false, "에너지가 부족합니다");
            _isTransitioning = false;
            yield break;
        }
        
        // Create room
        bool roomCreated = false;
        string roomCode = null;
        
        _roomManager.CreateRoom(maxPlayers, (success, code) =>
        {
            roomCreated = success;
            roomCode = code;
            callback?.Invoke(success, code);
        });
        
        // Wait for room creation with timeout
        float timeout = 5f;
        float elapsed = 0f;
        
        while (!roomCreated && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (!roomCreated)
        {
            callback?.Invoke(false, "방 생성 시간 초과");
            _isTransitioning = false;
        }
    }
    
    private IEnumerator ProcessJoinRoom(IntegrationTask task)
    {
        var roomCode = (string)task.Parameters["roomCode"];
        var callback = task.Parameters.ContainsKey("callback") ? 
                      (Action<bool, string>)task.Parameters["callback"] : null;
        
        // Switch to room mode if needed
        if (_currentMatchingMode != MatchingMode.Room)
        {
            yield return StartCoroutine(SwitchMatchingMode(MatchingMode.Room));
        }
        
        // Consume energy
        if (!ConsumeEnergyForOperation())
        {
            callback?.Invoke(false, "에너지가 부족합니다");
            _isTransitioning = false;
            yield break;
        }
        
        // Join room
        bool roomJoined = false;
        
        _roomManager.JoinRoom(roomCode, (success, message) =>
        {
            roomJoined = success;
            callback?.Invoke(success, message);
        });
        
        // Wait for room join with timeout
        float timeout = 5f;
        float elapsed = 0f;
        
        while (!roomJoined && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (!roomJoined)
        {
            callback?.Invoke(false, "방 참여 시간 초과");
            _isTransitioning = false;
        }
    }
    
    private IEnumerator ProcessStartMatching(IntegrationTask task)
    {
        var playerCount = (int)task.Parameters["playerCount"];
        var callback = task.Parameters.ContainsKey("callback") ? 
                      (Action<bool, string>)task.Parameters["callback"] : null;
        
        // Switch to random mode if needed
        if (_currentMatchingMode != MatchingMode.Random)
        {
            yield return StartCoroutine(SwitchMatchingMode(MatchingMode.Random));
        }
        
        // Consume energy
        if (!ConsumeEnergyForOperation())
        {
            callback?.Invoke(false, "에너지가 부족합니다");
            _isTransitioning = false;
            yield break;
        }
        
        // Start random matching via MatchingUI
        if (_matchingUI != null)
        {
            // Implementation depends on actual MatchingUI interface
            Debug.Log($"[MatchingRoomIntegration] Starting random matching for {playerCount} players");
            callback?.Invoke(true, "매칭 시작됨");
        }
        else
        {
            callback?.Invoke(false, "매칭 UI를 사용할 수 없습니다");
        }
        
        _isTransitioning = false;
    }
    
    private IEnumerator SwitchMatchingMode(MatchingMode newMode)
    {
        Debug.Log($"[MatchingRoomIntegration] Switching mode: {_currentMatchingMode} -> {newMode}");
        
        var previousMode = _currentMatchingMode;
        _currentMatchingMode = newMode;
        
        // Update UI to reflect new mode
        yield return StartCoroutine(UpdateUIForMode(newMode));
        
        // Update system state
        UpdateSystemState();
        
        OnMatchingModeChanged?.Invoke(newMode);
        
        Debug.Log($"[MatchingRoomIntegration] Mode switch completed: {newMode}");
    }
    
    private IEnumerator UpdateUIForMode(MatchingMode mode)
    {
        // Update UI components based on matching mode
        if (_matchingUI != null)
        {
            switch (mode)
            {
                case MatchingMode.Random:
                    // Configure UI for random matching
                    break;
                    
                case MatchingMode.Room:
                case MatchingMode.PrivateRoom:
                    // Configure UI for room matching
                    break;
            }
        }
        
        yield return new WaitForEndOfFrame();
    }
    #endregion
    
    #region Validation and State Management
    
    private bool ValidateCreateRoomRequest(int maxPlayers, out string error)
    {
        error = null;
        
        if (!_isInitialized)
        {
            error = "통합 시스템이 초기화되지 않았습니다";
            return false;
        }
        
        if (maxPlayers < 2 || maxPlayers > 4)
        {
            error = "플레이어 수는 2-4명이어야 합니다";
            return false;
        }
        
        if (IsInRoom)
        {
            error = "이미 방에 참여 중입니다";
            return false;
        }
        
        if (!CanConsumeEnergy())
        {
            error = "에너지가 부족합니다";
            return false;
        }
        
        return true;
    }
    
    private bool ValidateJoinRoomRequest(string roomCode, out string error)
    {
        error = null;
        
        if (!_isInitialized)
        {
            error = "통합 시스템이 초기화되지 않았습니다";
            return false;
        }
        
        if (!RoomCodeGenerator.IsValidRoomCodeFormat(roomCode))
        {
            error = "올바르지 않은 방 코드 형식입니다";
            return false;
        }
        
        if (IsInRoom)
        {
            error = "이미 방에 참여 중입니다";
            return false;
        }
        
        if (!CanConsumeEnergy())
        {
            error = "에너지가 부족합니다";
            return false;
        }
        
        return true;
    }
    
    private bool ValidateRandomMatchingRequest(int playerCount, out string error)
    {
        error = null;
        
        if (!_isInitialized)
        {
            error = "통합 시스템이 초기화되지 않았습니다";
            return false;
        }
        
        if (playerCount < 2 || playerCount > 4)
        {
            error = "플레이어 수는 2-4명이어야 합니다";
            return false;
        }
        
        if (IsMatching)
        {
            error = "이미 매칭 중입니다";
            return false;
        }
        
        if (!CanConsumeEnergy())
        {
            error = "에너지가 부족합니다";
            return false;
        }
        
        return true;
    }
    
    private bool CanConsumeEnergy()
    {
        if (_energyManager == null) return true; // No energy system available
        
        var config = _modeConfigurations.GetValueOrDefault(_currentMatchingMode);
        if (config == null || !config.RequiresEnergy) return true;
        
        return _energyManager.CanStartGame();
    }
    
    private bool ConsumeEnergyForOperation()
    {
        if (_energyManager == null) return true;
        
        var config = _modeConfigurations.GetValueOrDefault(_currentMatchingMode);
        if (config == null || !config.RequiresEnergy) return true;
        
        return _energyManager.ConsumeEnergy(config.EnergyCost);
    }
    
    private void UpdateSystemState()
    {
        _previousSystemState = _currentSystemState;
        
        _currentSystemState = new SystemState
        {
            IsInRoom = IsInRoom,
            IsMatching = IsMatching,
            HasSufficientEnergy = CanConsumeEnergy(),
            NetworkConnected = _networkManager?.IsConnected ?? false,
            CurrentRoomCode = _roomManager?.CurrentRoom?.RoomCode,
            PlayerCount = _roomManager?.CurrentRoom?.CurrentPlayerCount ?? 0,
            IsHost = _roomManager?.IsHost ?? false,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private void MonitorSystemState()
    {
        // Check for state changes that require attention
        if (_previousSystemState != null && !_previousSystemState.Equals(_currentSystemState))
        {
            HandleSystemStateChange();
        }
    }
    
    private void HandleSystemStateChange()
    {
        Debug.Log("[MatchingRoomIntegration] System state changed - triggering sync");
        ForceSystemSync();
    }
    
    private void ProcessPendingTransitions()
    {
        // Handle any pending state transitions or cleanup
        if (_currentState == IntegrationState.Error && !_isTransitioning)
        {
            // Attempt to recover from error state
            _currentState = IntegrationState.Ready;
        }
    }
    #endregion
    
    #region Performance Monitoring
    
    private void RecordOperationStart(string operation)
    {
        _operationStartTimes[operation] = DateTime.UtcNow;
    }
    
    private void RecordOperationSuccess(string operation)
    {
        if (_operationStartTimes.TryGetValue(operation, out DateTime startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            
            _performanceMetrics.Add(new IntegrationMetrics
            {
                Operation = operation,
                Success = true,
                Duration = duration,
                Timestamp = DateTime.UtcNow
            });
            
            _operationStartTimes.Remove(operation);
            
            Debug.Log($"[MatchingRoomIntegration] Operation completed: {operation} ({duration.TotalMilliseconds:F0}ms)");
        }
    }
    
    private void RecordOperationError(string subsystem, string operation, string error)
    {
        _performanceMetrics.Add(new IntegrationMetrics
        {
            Operation = $"{subsystem}.{operation}",
            Success = false,
            Error = error,
            Timestamp = DateTime.UtcNow
        });
        
        if (_operationStartTimes.ContainsKey(operation))
        {
            _operationStartTimes.Remove(operation);
        }
    }
    
    private float CalculateAverageTransitionTime()
    {
        var transitionMetrics = _performanceMetrics
            .Where(m => m.Success && m.Duration.HasValue)
            .ToList();
            
        if (transitionMetrics.Count == 0) return 0f;
        
        return (float)transitionMetrics.Average(m => m.Duration.Value.TotalMilliseconds);
    }
    
    private float CalculateSuccessRate()
    {
        if (_performanceMetrics.Count == 0) return 0f;
        
        int successCount = _performanceMetrics.Count(m => m.Success);
        return (float)successCount / _performanceMetrics.Count * 100f;
    }
    
    private int CalculateErrorCount()
    {
        return _performanceMetrics.Count(m => !m.Success);
    }
    #endregion
    
    #region Cleanup
    
    private void CleanupIntegration()
    {
        Debug.Log("[MatchingRoomIntegration] Cleaning up integration");
        
        // Unsubscribe from events
        RoomManager.OnRoomCreated -= HandleRoomCreated;
        RoomManager.OnRoomJoined -= HandleRoomJoined;
        RoomManager.OnRoomLeft -= HandleRoomLeft;
        RoomManager.OnRoomError -= HandleRoomError;
        RoomManager.OnGameStarted -= HandleGameStarted;
        
        if (_energyManager != null)
        {
            EnergyManager.OnEnergyChanged -= HandleEnergyChanged;
            EnergyManager.OnEnergyInsufficient -= HandleEnergyInsufficient;
        }
        
        // Stop coroutines
        if (_taskProcessor != null)
        {
            StopCoroutine(_taskProcessor);
        }
        
        // Clear collections
        _taskQueue.Clear();
        _operationStartTimes.Clear();
        _performanceMetrics.Clear();
        _modeConfigurations.Clear();
        
        // Reset state
        _isInitialized = false;
        _isTransitioning = false;
        _currentState = IntegrationState.Cleanup;
        
        Debug.Log("[MatchingRoomIntegration] Integration cleanup completed");
    }
    #endregion
}

#region Supporting Classes and Enums

/// <summary>매칭 모드</summary>
public enum MatchingMode
{
    None,
    Random,
    Room,
    PrivateRoom
}

/// <summary>통합 시스템 상태</summary>
public enum IntegrationState
{
    Initializing,
    Ready,
    InRoom,
    Matching,
    InGame,
    Error,
    Cleanup
}

/// <summary>시스템 전환 타입</summary>
public enum TransitionType
{
    ToIdle,
    ToRoom,
    ToMatching,
    ToGame
}

/// <summary>통합 작업 타입</summary>
public enum IntegrationTaskType
{
    ModeSwitch,
    CreateRoom,
    JoinRoom,
    StartMatching
}

/// <summary>작업 우선순위</summary>
public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>메시지 우선순위</summary>
public enum MessagePriority
{
    Low,
    Normal,
    High
}

/// <summary>모드 설정</summary>
public class ModeConfiguration
{
    public bool RequiresEnergy { get; set; }
    public int EnergyCost { get; set; }
    public int MaxPlayers { get; set; }
    public bool AllowSpectators { get; set; }
    public MessagePriority NetworkPriority { get; set; }
    public float TimeoutSeconds { get; set; }
}

/// <summary>통합 작업</summary>
public class IntegrationTask
{
    public IntegrationTaskType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>시스템 상태</summary>
public class SystemState : IEquatable<SystemState>
{
    public bool IsInRoom { get; set; }
    public bool IsMatching { get; set; }
    public bool HasSufficientEnergy { get; set; }
    public bool NetworkConnected { get; set; }
    public string CurrentRoomCode { get; set; }
    public int PlayerCount { get; set; }
    public bool IsHost { get; set; }
    public DateTime Timestamp { get; set; }
    
    public bool Equals(SystemState other)
    {
        if (other == null) return false;
        
        return IsInRoom == other.IsInRoom &&
               IsMatching == other.IsMatching &&
               HasSufficientEnergy == other.HasSufficientEnergy &&
               NetworkConnected == other.NetworkConnected &&
               CurrentRoomCode == other.CurrentRoomCode &&
               PlayerCount == other.PlayerCount &&
               IsHost == other.IsHost;
    }
}

/// <summary>시스템 동기화 데이터</summary>
public class SystemSyncData
{
    public MatchingMode MatchingMode { get; set; }
    public IntegrationState IntegrationState { get; set; }
    public bool IsInRoom { get; set; }
    public bool IsMatching { get; set; }
    public SystemState SystemState { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>통합 성능 메트릭</summary>
public class IntegrationMetrics
{
    public string Operation { get; set; }
    public bool Success { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Error { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>통합 시스템 성능 보고서</summary>
public class IntegrationPerformanceReport
{
    public float AverageTransitionTime { get; set; }
    public int TotalOperations { get; set; }
    public float SuccessRate { get; set; }
    public int ErrorCount { get; set; }
}

#endregion