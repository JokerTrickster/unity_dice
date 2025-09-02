using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 관리 UI 컨트롤러
/// 방 생성/참여 모달, 플레이어 목록, 방장 권한 UI를 관리합니다.
/// RoomManager와 연동하여 실시간 방 상태를 표시합니다.
/// </summary>
public class RoomUI : MonoBehaviour
{
    #region UI References
    [Header("Main UI Components")]
    [SerializeField] private GameObject roomMainPanel;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button leaveRoomButton;
    
    [Header("Room Creation Modal")]
    [SerializeField] private GameObject createRoomModal;
    [SerializeField] private RoomCreateUI roomCreateUI;
    
    [Header("Room Join Modal")]
    [SerializeField] private GameObject joinRoomModal;
    [SerializeField] private RoomCodeInput roomCodeInput;
    
    [Header("Room Display")]
    [SerializeField] private GameObject roomDisplayPanel;
    [SerializeField] private Text roomCodeText;
    [SerializeField] private Button copyRoomCodeButton;
    [SerializeField] private Text roomStatusText;
    [SerializeField] private RoomPlayerList playerList;
    
    [Header("Host Controls")]
    [SerializeField] private GameObject hostControlsPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button kickPlayerButton;
    [SerializeField] private Text hostStatusText;
    
    [Header("UI Feedback")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Text errorMessageText;
    [SerializeField] private GameObject successMessage;
    [SerializeField] private Text successMessageText;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeTransitionDuration = 0.3f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Private Fields
    private RoomManager _roomManager;
    private UserDataManager _userDataManager;
    
    private RoomUIState _currentState = RoomUIState.Idle;
    private string _currentRoomCode;
    private bool _isHost = false;
    private bool _isInitialized = false;
    
    // Animation and feedback
    private Coroutine _messageCoroutine;
    private Coroutine _transitionCoroutine;
    private Dictionary<GameObject, CanvasGroup> _canvasGroups = new Dictionary<GameObject, CanvasGroup>();
    
    // Performance optimization
    private float _lastPlayerListUpdate = 0f;
    private const float PLAYER_LIST_UPDATE_INTERVAL = 0.5f; // 500ms max update rate
    #endregion

    #region Properties
    public bool IsInRoom => _roomManager?.IsInRoom ?? false;
    public bool IsHost => _isHost;
    public string CurrentRoomCode => _currentRoomCode;
    public RoomUIState CurrentState => _currentState;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializeCanvasGroups();
        SetupInitialUIState();
    }

    private void Start()
    {
        InitializeRoomSystem();
    }

    private void OnDestroy()
    {
        CleanupRoomSystem();
    }
    #endregion

    #region Initialization
    private void InitializeRoomSystem()
    {
        try
        {
            // Get manager references
            _roomManager = RoomManager.Instance;
            _userDataManager = UserDataManager.Instance;

            if (_roomManager == null)
            {
                ShowError("RoomManager를 찾을 수 없습니다");
                return;
            }

            // Subscribe to RoomManager events
            SubscribeToRoomEvents();
            
            // Setup UI event handlers
            SetupUIEventHandlers();
            
            // Initialize sub-components
            InitializeSubComponents();
            
            _isInitialized = true;
            UpdateUIState();
            
            Debug.Log("[RoomUI] Room system initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomUI] Failed to initialize room system: {e.Message}");
            ShowError("방 시스템 초기화 실패");
        }
    }

    private void SubscribeToRoomEvents()
    {
        // Room lifecycle events
        RoomManager.OnRoomCreated += OnRoomCreated;
        RoomManager.OnRoomJoined += OnRoomJoined;
        RoomManager.OnRoomLeft += OnRoomLeft;
        RoomManager.OnRoomClosed += OnRoomClosed;
        RoomManager.OnRoomUpdated += OnRoomUpdated;
        
        // Player events
        RoomManager.OnPlayerJoined += OnPlayerJoined;
        RoomManager.OnPlayerLeft += OnPlayerLeft;
        RoomManager.OnPlayerUpdated += OnPlayerUpdated;
        RoomManager.OnHostChanged += OnHostChanged;
        
        // Game events
        RoomManager.OnGameStartRequested += OnGameStartRequested;
        RoomManager.OnGameStarted += OnGameStarted;
        RoomManager.OnGameStartFailed += OnGameStartFailed;
        
        // Error events
        RoomManager.OnRoomError += OnRoomError;
        RoomManager.OnConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void SetupUIEventHandlers()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(ShowCreateRoomModal);
            
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(ShowJoinRoomModal);
            
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(LeaveRoom);
            
        if (copyRoomCodeButton != null)
            copyRoomCodeButton.onClick.AddListener(CopyRoomCodeToClipboard);
            
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);
    }

    private void InitializeSubComponents()
    {
        // Initialize room creation UI
        if (roomCreateUI != null)
        {
            roomCreateUI.OnRoomCreationRequested += OnRoomCreationRequested;
            roomCreateUI.OnCreateRoomCancelled += OnCreateRoomCancelled;
        }

        // Initialize room code input
        if (roomCodeInput != null)
        {
            roomCodeInput.OnRoomJoinRequested += OnRoomJoinRequested;
            roomCodeInput.OnJoinRoomCancelled += OnJoinRoomCancelled;
        }

        // Initialize player list
        if (playerList != null)
        {
            playerList.OnPlayerKickRequested += OnPlayerKickRequested;
            playerList.OnPlayerReadyToggled += OnPlayerReadyToggled;
        }
    }

    private void InitializeCanvasGroups()
    {
        // Add CanvasGroup components for smooth transitions
        var panels = new GameObject[] 
        { 
            roomMainPanel, createRoomModal, joinRoomModal, roomDisplayPanel, 
            hostControlsPanel, loadingIndicator, successMessage 
        };

        foreach (var panel in panels)
        {
            if (panel != null && panel.GetComponent<CanvasGroup>() == null)
            {
                var canvasGroup = panel.AddComponent<CanvasGroup>();
                _canvasGroups[panel] = canvasGroup;
            }
        }
    }

    private void SetupInitialUIState()
    {
        // Hide all modals initially
        SetPanelVisibility(createRoomModal, false, false);
        SetPanelVisibility(joinRoomModal, false, false);
        SetPanelVisibility(roomDisplayPanel, false, false);
        SetPanelVisibility(hostControlsPanel, false, false);
        SetPanelVisibility(loadingIndicator, false, false);
        SetPanelVisibility(successMessage, false, false);
        
        // Show main panel
        SetPanelVisibility(roomMainPanel, true, false);
        
        // Clear initial text
        ClearMessages();
    }
    #endregion

    #region UI State Management
    private void UpdateUIState()
    {
        if (!_isInitialized) return;

        var newState = DetermineUIState();
        if (newState != _currentState)
        {
            TransitionToState(newState);
        }
    }

    private RoomUIState DetermineUIState()
    {
        if (_roomManager == null) return RoomUIState.Error;
        
        if (_roomManager.IsInRoom)
        {
            if (_roomManager.CurrentRoom?.Status == RoomStatus.Starting)
                return RoomUIState.GameStarting;
            else if (_roomManager.IsHost)
                return RoomUIState.InRoomAsHost;
            else
                return RoomUIState.InRoomAsPlayer;
        }
        
        return RoomUIState.Idle;
    }

    private void TransitionToState(RoomUIState newState)
    {
        Debug.Log($"[RoomUI] State transition: {_currentState} -> {newState}");
        
        var previousState = _currentState;
        _currentState = newState;
        
        // Handle state-specific UI updates
        switch (newState)
        {
            case RoomUIState.Idle:
                ShowIdleUI();
                break;
                
            case RoomUIState.CreatingRoom:
                ShowCreatingRoomUI();
                break;
                
            case RoomUIState.JoiningRoom:
                ShowJoiningRoomUI();
                break;
                
            case RoomUIState.InRoomAsHost:
                ShowInRoomAsHostUI();
                break;
                
            case RoomUIState.InRoomAsPlayer:
                ShowInRoomAsPlayerUI();
                break;
                
            case RoomUIState.GameStarting:
                ShowGameStartingUI();
                break;
                
            case RoomUIState.Error:
                ShowErrorUI();
                break;
        }
    }

    private void ShowIdleUI()
    {
        SetPanelVisibility(roomMainPanel, true, true);
        SetPanelVisibility(roomDisplayPanel, false, true);
        SetPanelVisibility(hostControlsPanel, false, true);
        SetPanelVisibility(createRoomModal, false, true);
        SetPanelVisibility(joinRoomModal, false, true);
        
        UpdateButtonStates(true, true, false);
    }

    private void ShowCreatingRoomUI()
    {
        SetPanelVisibility(loadingIndicator, true, true);
        UpdateButtonStates(false, false, false);
    }

    private void ShowJoiningRoomUI()
    {
        SetPanelVisibility(loadingIndicator, true, true);
        UpdateButtonStates(false, false, false);
    }

    private void ShowInRoomAsHostUI()
    {
        SetPanelVisibility(roomMainPanel, false, true);
        SetPanelVisibility(roomDisplayPanel, true, true);
        SetPanelVisibility(hostControlsPanel, true, true);
        
        _isHost = true;
        UpdateRoomDisplay();
        UpdateHostControls();
        UpdatePlayerList();
    }

    private void ShowInRoomAsPlayerUI()
    {
        SetPanelVisibility(roomMainPanel, false, true);
        SetPanelVisibility(roomDisplayPanel, true, true);
        SetPanelVisibility(hostControlsPanel, false, true);
        
        _isHost = false;
        UpdateRoomDisplay();
        UpdatePlayerList();
    }

    private void ShowGameStartingUI()
    {
        if (hostControlsPanel != null)
            SetPanelVisibility(hostControlsPanel, false, true);
            
        UpdateButtonStates(false, false, false);
        ShowMessage("게임이 시작됩니다...", MessageType.Info);
    }

    private void ShowErrorUI()
    {
        SetPanelVisibility(loadingIndicator, false, true);
        UpdateButtonStates(true, true, IsInRoom);
    }

    private void UpdateButtonStates(bool createEnabled, bool joinEnabled, bool leaveEnabled)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = createEnabled;
            
        if (joinRoomButton != null)
            joinRoomButton.interactable = joinEnabled;
            
        if (leaveRoomButton != null)
            leaveRoomButton.interactable = leaveEnabled;
    }
    #endregion

    #region Room Operations
    public void ShowCreateRoomModal()
    {
        if (_currentState != RoomUIState.Idle) return;
        
        SetPanelVisibility(createRoomModal, true, true);
        
        if (roomCreateUI != null)
            roomCreateUI.ResetToDefaults();
    }

    public void ShowJoinRoomModal()
    {
        if (_currentState != RoomUIState.Idle) return;
        
        SetPanelVisibility(joinRoomModal, true, true);
        
        if (roomCodeInput != null)
            roomCodeInput.ClearInput();
    }

    private void OnRoomCreationRequested(int maxPlayers)
    {
        SetPanelVisibility(createRoomModal, false, true);
        TransitionToState(RoomUIState.CreatingRoom);
        
        _roomManager.CreateRoom(maxPlayers, OnRoomCreationResult);
    }

    private void OnCreateRoomCancelled()
    {
        SetPanelVisibility(createRoomModal, false, true);
    }

    private void OnRoomJoinRequested(string roomCode)
    {
        SetPanelVisibility(joinRoomModal, false, true);
        TransitionToState(RoomUIState.JoiningRoom);
        
        _roomManager.JoinRoom(roomCode, OnRoomJoinResult);
    }

    private void OnJoinRoomCancelled()
    {
        SetPanelVisibility(joinRoomModal, false, true);
    }

    private void OnRoomCreationResult(bool success, string result)
    {
        SetPanelVisibility(loadingIndicator, false, true);
        
        if (success)
        {
            _currentRoomCode = result;
            ShowSuccess($"방이 생성되었습니다: {result}");
            // State will be updated by OnRoomCreated event
        }
        else
        {
            ShowError($"방 생성 실패: {result}");
            TransitionToState(RoomUIState.Idle);
        }
    }

    private void OnRoomJoinResult(bool success, string result)
    {
        SetPanelVisibility(loadingIndicator, false, true);
        
        if (success)
        {
            _currentRoomCode = result;
            ShowSuccess($"방에 참여했습니다: {result}");
            // State will be updated by OnRoomJoined event
        }
        else
        {
            ShowError($"방 참여 실패: {result}");
            TransitionToState(RoomUIState.Idle);
        }
    }

    public void LeaveRoom()
    {
        if (!IsInRoom) return;
        
        _roomManager.LeaveRoom((success) =>
        {
            if (success)
            {
                ShowSuccess("방을 나갔습니다");
            }
            else
            {
                ShowError("방 나가기 실패");
            }
        });
    }

    public void StartGame()
    {
        if (!_isHost || !IsInRoom) return;
        
        _roomManager.StartGame((success, message) =>
        {
            if (success)
            {
                ShowSuccess("게임 시작 요청을 보냈습니다");
            }
            else
            {
                ShowError($"게임 시작 실패: {message}");
            }
        });
    }

    public void CopyRoomCodeToClipboard()
    {
        if (string.IsNullOrEmpty(_currentRoomCode)) return;
        
        _roomManager.CopyRoomCodeToClipboard();
        ShowSuccess("방 코드가 클립보드에 복사되었습니다");
    }
    #endregion

    #region Room Event Handlers
    private void OnRoomCreated(RoomData roomData)
    {
        _currentRoomCode = roomData.RoomCode;
        UpdateUIState();
    }

    private void OnRoomJoined(RoomData roomData)
    {
        _currentRoomCode = roomData.RoomCode;
        UpdateUIState();
    }

    private void OnRoomLeft(RoomData roomData)
    {
        _currentRoomCode = null;
        _isHost = false;
        TransitionToState(RoomUIState.Idle);
    }

    private void OnRoomClosed(RoomData roomData)
    {
        _currentRoomCode = null;
        _isHost = false;
        ShowMessage("방이 종료되었습니다", MessageType.Warning);
        TransitionToState(RoomUIState.Idle);
    }

    private void OnRoomUpdated(RoomData roomData)
    {
        UpdateRoomDisplay();
        UpdatePlayerList();
        UpdateHostControls();
    }

    private void OnPlayerJoined(RoomData roomData, PlayerInfo playerInfo)
    {
        ShowMessage($"{playerInfo.Nickname}님이 입장했습니다", MessageType.Info);
        UpdatePlayerList();
    }

    private void OnPlayerLeft(RoomData roomData, PlayerInfo playerInfo)
    {
        ShowMessage($"{playerInfo.Nickname}님이 퇴장했습니다", MessageType.Info);
        UpdatePlayerList();
    }

    private void OnPlayerUpdated(RoomData roomData, PlayerInfo playerInfo)
    {
        UpdatePlayerList();
    }

    private void OnHostChanged(string newHostId)
    {
        _isHost = (_userDataManager?.GetUserId() == newHostId);
        ShowMessage(_isHost ? "방장이 되었습니다" : "방장이 변경되었습니다", MessageType.Info);
        UpdateUIState();
    }

    private void OnGameStartRequested(string roomCode)
    {
        ShowMessage("게임 시작 요청 중...", MessageType.Info);
    }

    private void OnGameStarted(string roomCode)
    {
        TransitionToState(RoomUIState.GameStarting);
    }

    private void OnGameStartFailed(string roomCode, string reason)
    {
        ShowError($"게임 시작 실패: {reason}");
        UpdateUIState();
    }

    private void OnRoomError(string operation, string error)
    {
        ShowError($"{operation} 오류: {error}");
        
        // Return to appropriate state based on current room status
        UpdateUIState();
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        if (!connected && IsInRoom)
        {
            ShowError("연결이 끊어졌습니다. 방 상태를 확인하세요");
        }
    }
    #endregion

    #region UI Updates
    private void UpdateRoomDisplay()
    {
        if (_roomManager?.CurrentRoom == null) return;
        
        var room = _roomManager.CurrentRoom;
        
        if (roomCodeText != null)
            roomCodeText.text = $"방 코드: {room.RoomCode}";
            
        if (roomStatusText != null)
        {
            string statusText = GetRoomStatusText(room);
            roomStatusText.text = statusText;
        }
    }

    private void UpdateHostControls()
    {
        if (!_isHost || _roomManager?.CurrentRoom == null) return;
        
        var room = _roomManager.CurrentRoom;
        bool canStartGame = room.CanStart;
        
        if (startGameButton != null)
            startGameButton.interactable = canStartGame;
            
        if (hostStatusText != null)
        {
            string statusText = canStartGame ? 
                "게임을 시작할 수 있습니다" : 
                $"최소 {room.MaxPlayers}명의 플레이어가 필요합니다";
            hostStatusText.text = statusText;
        }
    }

    private void UpdatePlayerList()
    {
        // Performance throttling
        if (Time.time - _lastPlayerListUpdate < PLAYER_LIST_UPDATE_INTERVAL)
            return;
            
        _lastPlayerListUpdate = Time.time;
        
        if (playerList != null && _roomManager?.CurrentRoom != null)
        {
            var room = _roomManager.CurrentRoom;
            playerList.UpdatePlayerList(room.Players, _isHost);
        }
    }

    private string GetRoomStatusText(RoomData room)
    {
        switch (room.Status)
        {
            case RoomStatus.Waiting:
                return $"대기 중 ({room.CurrentPlayerCount}/{room.MaxPlayers})";
            case RoomStatus.Starting:
                return "게임 시작 중...";
            case RoomStatus.InGame:
                return "게임 진행 중";
            case RoomStatus.Closed:
                return "방 종료됨";
            default:
                return "알 수 없는 상태";
        }
    }
    #endregion

    #region Player Management Event Handlers
    private void OnPlayerKickRequested(string playerId)
    {
        if (!_isHost) return;
        
        // TODO: Implement kick player functionality when available in RoomManager
        ShowMessage("플레이어 추방 기능은 곧 추가될 예정입니다", MessageType.Info);
    }

    private void OnPlayerReadyToggled(string playerId, bool isReady)
    {
        // TODO: Implement ready state toggle when available in RoomManager
        ShowMessage($"준비 상태 변경: {(isReady ? "준비" : "대기")}", MessageType.Info);
    }
    #endregion

    #region UI Helpers
    private void SetPanelVisibility(GameObject panel, bool visible, bool animated)
    {
        if (panel == null) return;
        
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }
        
        if (animated && _canvasGroups.TryGetValue(panel, out CanvasGroup canvasGroup))
        {
            _transitionCoroutine = StartCoroutine(AnimatePanelTransition(canvasGroup, visible));
        }
        else
        {
            panel.SetActive(visible);
        }
    }

    private IEnumerator AnimatePanelTransition(CanvasGroup canvasGroup, bool fadeIn)
    {
        if (!canvasGroup.gameObject.activeInHierarchy && fadeIn)
        {
            canvasGroup.gameObject.SetActive(true);
        }
        
        float startAlpha = canvasGroup.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeTransitionDuration;
            float curveValue = fadeCurve.Evaluate(progress);
            
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);
            yield return null;
        }
        
        canvasGroup.alpha = targetAlpha;
        
        if (!fadeIn)
        {
            canvasGroup.gameObject.SetActive(false);
        }
        
        _transitionCoroutine = null;
    }

    private void ShowMessage(string message, MessageType type)
    {
        switch (type)
        {
            case MessageType.Error:
                ShowError(message);
                break;
            case MessageType.Success:
                ShowSuccess(message);
                break;
            case MessageType.Info:
            case MessageType.Warning:
                if (_messageCoroutine != null)
                    StopCoroutine(_messageCoroutine);
                _messageCoroutine = StartCoroutine(ShowTemporaryMessage(message));
                break;
        }
    }

    private void ShowError(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.gameObject.SetActive(true);
            
            if (_messageCoroutine != null)
                StopCoroutine(_messageCoroutine);
            _messageCoroutine = StartCoroutine(HideMessageAfterDelay(errorMessageText.gameObject, 5f));
        }
        
        Debug.LogError($"[RoomUI] {message}");
    }

    private void ShowSuccess(string message)
    {
        if (successMessage != null && successMessageText != null)
        {
            successMessageText.text = message;
            SetPanelVisibility(successMessage, true, true);
            
            if (_messageCoroutine != null)
                StopCoroutine(_messageCoroutine);
            _messageCoroutine = StartCoroutine(HideMessageAfterDelay(successMessage, 3f));
        }
        
        Debug.Log($"[RoomUI] {message}");
    }

    private IEnumerator ShowTemporaryMessage(string message)
    {
        // Implementation depends on how temporary messages should be displayed
        Debug.Log($"[RoomUI] {message}");
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator HideMessageAfterDelay(GameObject messageObject, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (messageObject != null)
        {
            if (_canvasGroups.TryGetValue(messageObject, out CanvasGroup canvasGroup))
            {
                yield return StartCoroutine(AnimatePanelTransition(canvasGroup, false));
            }
            else
            {
                messageObject.SetActive(false);
            }
        }
        
        _messageCoroutine = null;
    }

    private void ClearMessages()
    {
        if (errorMessageText != null)
            errorMessageText.gameObject.SetActive(false);
            
        SetPanelVisibility(successMessage, false, false);
    }
    #endregion

    #region Validation and Cleanup
    private void ValidateComponents()
    {
        if (roomMainPanel == null)
            Debug.LogError("[RoomUI] Room Main Panel is not assigned!");
            
        if (roomDisplayPanel == null)
            Debug.LogError("[RoomUI] Room Display Panel is not assigned!");
            
        if (createRoomButton == null)
            Debug.LogWarning("[RoomUI] Create Room Button is not assigned");
            
        if (joinRoomButton == null)
            Debug.LogWarning("[RoomUI] Join Room Button is not assigned");
            
        if (playerList == null)
            Debug.LogWarning("[RoomUI] Player List component is not assigned");
    }

    private void CleanupRoomSystem()
    {
        // Unsubscribe from events
        if (_roomManager != null)
        {
            RoomManager.OnRoomCreated -= OnRoomCreated;
            RoomManager.OnRoomJoined -= OnRoomJoined;
            RoomManager.OnRoomLeft -= OnRoomLeft;
            RoomManager.OnRoomClosed -= OnRoomClosed;
            RoomManager.OnRoomUpdated -= OnRoomUpdated;
            RoomManager.OnPlayerJoined -= OnPlayerJoined;
            RoomManager.OnPlayerLeft -= OnPlayerLeft;
            RoomManager.OnPlayerUpdated -= OnPlayerUpdated;
            RoomManager.OnHostChanged -= OnHostChanged;
            RoomManager.OnGameStartRequested -= OnGameStartRequested;
            RoomManager.OnGameStarted -= OnGameStarted;
            RoomManager.OnGameStartFailed -= OnGameStartFailed;
            RoomManager.OnRoomError -= OnRoomError;
            RoomManager.OnConnectionStatusChanged -= OnConnectionStatusChanged;
        }
        
        // Cleanup sub-components
        if (roomCreateUI != null)
        {
            roomCreateUI.OnRoomCreationRequested -= OnRoomCreationRequested;
            roomCreateUI.OnCreateRoomCancelled -= OnCreateRoomCancelled;
        }
        
        if (roomCodeInput != null)
        {
            roomCodeInput.OnRoomJoinRequested -= OnRoomJoinRequested;
            roomCodeInput.OnJoinRoomCancelled -= OnJoinRoomCancelled;
        }
        
        if (playerList != null)
        {
            playerList.OnPlayerKickRequested -= OnPlayerKickRequested;
            playerList.OnPlayerReadyToggled -= OnPlayerReadyToggled;
        }
        
        // Stop coroutines
        if (_messageCoroutine != null)
            StopCoroutine(_messageCoroutine);
            
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
        
        Debug.Log("[RoomUI] Room system cleaned up");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 현재 UI 상태 반환
    /// </summary>
    public RoomUIState GetCurrentState()
    {
        return _currentState;
    }

    /// <summary>
    /// 특정 메시지 표시 (외부에서 호출 가능)
    /// </summary>
    public void DisplayMessage(string message, MessageType type)
    {
        ShowMessage(message, type);
    }

    /// <summary>
    /// UI 강제 새로고침
    /// </summary>
    public void ForceRefresh()
    {
        if (_isInitialized)
        {
            UpdateUIState();
            UpdateRoomDisplay();
            UpdatePlayerList();
            UpdateHostControls();
        }
    }
    #endregion
}

#region Enums and Data Classes
/// <summary>
/// 방 UI 상태
/// </summary>
public enum RoomUIState
{
    Idle,           // 방에 속하지 않은 상태
    CreatingRoom,   // 방 생성 중
    JoiningRoom,    // 방 참여 중
    InRoomAsHost,   // 방에 속해있음 (방장)
    InRoomAsPlayer, // 방에 속해있음 (일반 플레이어)
    GameStarting,   // 게임 시작 중
    Error          // 오류 상태
}

/// <summary>
/// 메시지 타입
/// </summary>
public enum MessageType
{
    Info,
    Warning,
    Error,
    Success
}
#endregion