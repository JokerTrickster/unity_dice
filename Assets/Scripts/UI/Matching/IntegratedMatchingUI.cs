using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 통합 매칭 UI 컨트롤러
/// 새로운 매칭 UI 컴포넌트들을 기존 MatchingSection과 통합합니다.
/// MatchingSectionUI의 인터페이스를 유지하면서 새로운 컴포넌트들을 활용합니다.
/// </summary>
public class IntegratedMatchingUI : MonoBehaviour
{
    #region UI Component References
    [Header("New UI Components")]
    [SerializeField] private MatchingUI matchingUI;
    [SerializeField] private PlayerCountSelector playerCountSelector;
    [SerializeField] private MatchingStatusDisplay matchingStatusDisplay;
    [SerializeField] private MatchingProgressAnimator matchingProgressAnimator;
    
    [Header("Legacy UI Elements (for compatibility)")]
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button rankedMatchButton;
    [SerializeField] private Button customMatchButton;
    [SerializeField] private Button friendMatchButton;
    [SerializeField] private Text matchStatusText;
    [SerializeField] private Slider matchProgressSlider;
    [SerializeField] private Text waitTimeText;
    [SerializeField] private Button cancelMatchButton;
    #endregion
    
    #region Events (for MatchingSection compatibility)
    public event Action<MatchingRequest> OnMatchingRequested;
    public event Action<RoomCreationRequest> OnRoomCreationRequested;
    public event Action<RoomJoinRequest> OnRoomJoinRequested;
    public event Action OnMatchCancelRequested;
    public event Action<GameMode> OnGameModeSelected;
    #endregion
    
    #region Private Fields
    private bool _isInitialized = false;
    private MatchingState _currentState = MatchingState.Idle;
    private GameMode _currentGameMode = GameMode.Classic;
    private int _currentPlayerCount = 2;
    
    // Cached references for performance
    private UserDataManager _userDataManager;
    private EnergyManager _energyManager;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeReferences();
        ValidateComponents();
    }
    
    private void Start()
    {
        SetupEventListeners();
        InitializeUI();
        _isInitialized = true;
    }
    
    private void OnEnable()
    {
        if (_isInitialized)
        {
            SubscribeToEvents();
        }
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }
    
    private void OnDestroy()
    {
        CleanupEvents();
    }
    #endregion
    
    #region Initialization
    private void InitializeReferences()
    {
        _userDataManager = UserDataManager.Instance;
        _energyManager = EnergyManager.Instance;
        
        // Auto-find components if not assigned
        if (matchingUI == null)
            matchingUI = GetComponentInChildren<MatchingUI>();
            
        if (playerCountSelector == null)
            playerCountSelector = GetComponentInChildren<PlayerCountSelector>();
            
        if (matchingStatusDisplay == null)
            matchingStatusDisplay = GetComponentInChildren<MatchingStatusDisplay>();
            
        if (matchingProgressAnimator == null)
            matchingProgressAnimator = GetComponentInChildren<MatchingProgressAnimator>();
    }
    
    private void ValidateComponents()
    {
        if (matchingUI == null)
            Debug.LogError("[IntegratedMatchingUI] MatchingUI component is missing!");
            
        if (playerCountSelector == null)
            Debug.LogError("[IntegratedMatchingUI] PlayerCountSelector component is missing!");
            
        if (matchingStatusDisplay == null)
            Debug.LogError("[IntegratedMatchingUI] MatchingStatusDisplay component is missing!");
            
        if (matchingProgressAnimator == null)
            Debug.LogError("[IntegratedMatchingUI] MatchingProgressAnimator component is missing!");
    }
    
    private void InitializeUI()
    {
        // Initialize all UI components
        if (matchingUI != null)
            matchingUI.Initialize();
            
        if (playerCountSelector != null)
            playerCountSelector.Initialize();
            
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.Initialize();
            
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.Initialize();
        
        Debug.Log("[IntegratedMatchingUI] UI initialized successfully");
    }
    #endregion
    
    #region Event Management
    private void SetupEventListeners()
    {
        // Legacy button compatibility
        if (quickMatchButton != null)
            quickMatchButton.onClick.AddListener(() => StartMatching(UIMatchType.Random, GameMode.Classic));
            
        if (rankedMatchButton != null)
            rankedMatchButton.onClick.AddListener(() => StartMatching(UIMatchType.Random, GameMode.Ranked));
            
        if (customMatchButton != null)
            customMatchButton.onClick.AddListener(() => StartMatching(UIMatchType.RoomCreate, GameMode.Classic));
            
        if (friendMatchButton != null)
            friendMatchButton.onClick.AddListener(() => StartMatching(UIMatchType.Room, GameMode.Classic));
            
        if (cancelMatchButton != null)
            cancelMatchButton.onClick.AddListener(CancelMatching);
    }
    
    private void SubscribeToEvents()
    {
        // Subscribe to new UI component events
        if (matchingUI != null)
        {
            MatchingUI.OnMatchingRequested += OnMatchingUIRequested;
            MatchingUI.OnPlayerCountChanged += OnPlayerCountUIChanged;
            MatchingUI.OnMatchingCancelled += OnMatchingUICancelled;
            MatchingUI.OnMatchTypeChanged += OnMatchTypeUIChanged;
        }
        
        if (playerCountSelector != null)
        {
            playerCountSelector.OnPlayerCountChanged += OnPlayerCountSelectorChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from new UI component events
        if (matchingUI != null)
        {
            MatchingUI.OnMatchingRequested -= OnMatchingUIRequested;
            MatchingUI.OnPlayerCountChanged -= OnPlayerCountUIChanged;
            MatchingUI.OnMatchingCancelled -= OnMatchingUICancelled;
            MatchingUI.OnMatchTypeChanged -= OnMatchTypeUIChanged;
        }
        
        if (playerCountSelector != null)
        {
            playerCountSelector.OnPlayerCountChanged -= OnPlayerCountSelectorChanged;
        }
    }
    
    private void CleanupEvents()
    {
        UnsubscribeFromEvents();
        
        // Clean up legacy button events
        if (quickMatchButton != null)
            quickMatchButton.onClick.RemoveAllListeners();
            
        if (rankedMatchButton != null)
            rankedMatchButton.onClick.RemoveAllListeners();
            
        if (customMatchButton != null)
            customMatchButton.onClick.RemoveAllListeners();
            
        if (friendMatchButton != null)
            friendMatchButton.onClick.RemoveAllListeners();
            
        if (cancelMatchButton != null)
            cancelMatchButton.onClick.RemoveAllListeners();
    }
    #endregion
    
    #region Public API (MatchingSection Compatibility)
    /// <summary>
    /// 사용자 정보를 업데이트합니다 (MatchingSection 호환성)
    /// </summary>
    public void UpdateUserInfo(string displayName, int level, int currentEnergy, int maxEnergy)
    {
        Debug.Log($"[IntegratedMatchingUI] Updating user info: {displayName} (Level {level}) - Energy: {currentEnergy}/{maxEnergy}");
        
        // Update components based on user info
        RefreshAvailability(level, currentEnergy);
    }
    
    /// <summary>
    /// 매칭 상태를 설정합니다 (MatchingSection 호환성)
    /// </summary>
    public void SetMatchingState(MatchingState state)
    {
        _currentState = state;
        
        // Update all UI components
        if (matchingUI != null)
            matchingUI.SetMatchingState(state);
            
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.SetState(state);
            
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.SetState(state);
        
        // Update legacy UI elements
        UpdateLegacyUI(state);
        
        Debug.Log($"[IntegratedMatchingUI] Matching state set to: {state}");
    }
    
    /// <summary>
    /// 메시지를 표시합니다 (MatchingSection 호환성)
    /// </summary>
    public void ShowMessage(string message, MessageType type = MessageType.Info)
    {
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.ShowMessage(message, type);
        else if (matchStatusText != null)
            matchStatusText.text = message;
            
        Debug.Log($"[IntegratedMatchingUI] Showing message ({type}): {message}");
    }
    
    /// <summary>
    /// 버튼 상호작용 가능성을 설정합니다 (MatchingSection 호환성)
    /// </summary>
    public void SetButtonsInteractable(bool interactable)
    {
        if (matchingUI != null)
        {
            // The MatchingUI handles this internally based on state
            matchingUI.RefreshUI();
        }
        
        // Update legacy buttons
        if (quickMatchButton != null) quickMatchButton.interactable = interactable;
        if (rankedMatchButton != null) rankedMatchButton.interactable = interactable;
        if (customMatchButton != null) customMatchButton.interactable = interactable;
        if (friendMatchButton != null) friendMatchButton.interactable = interactable;
    }
    
    /// <summary>
    /// 매칭 진행 상황을 표시합니다 (MatchingSection 호환성)
    /// </summary>
    public void ShowMatchingProgress(bool show)
    {
        if (matchingProgressAnimator != null)
        {
            var state = show ? MatchingState.Searching : MatchingState.Idle;
            matchingProgressAnimator.SetState(state);
        }
    }
    
    /// <summary>
    /// 게임 모드 정보를 업데이트합니다 (MatchingSection 호환성)
    /// </summary>
    public void UpdateGameModeInfo(GameMode gameMode, MatchConfig config)
    {
        _currentGameMode = gameMode;
        
        // Notify components of game mode change
        OnGameModeSelected?.Invoke(gameMode);
        
        Debug.Log($"[IntegratedMatchingUI] Updated game mode info: {gameMode} - {config?.DisplayName}");
    }
    
    /// <summary>
    /// 매칭 진행 상황을 업데이트합니다 (MatchingSection 호환성)
    /// </summary>
    public void UpdateMatchingProgress(TimeSpan elapsed, TimeSpan estimated)
    {
        float progress = estimated.TotalSeconds > 0 ? 
            (float)(elapsed.TotalSeconds / estimated.TotalSeconds) : 0f;
        
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.UpdateProgress(progress, elapsed, estimated);
            
        if (matchingUI != null)
            matchingUI.UpdateMatchingProgress(progress, elapsed, estimated);
        
        // Update legacy UI
        if (waitTimeText != null)
            waitTimeText.text = $"대기 시간: {elapsed:mm\\:ss}";
            
        if (matchProgressSlider != null)
            matchProgressSlider.value = progress;
    }
    
    /// <summary>
    /// 게임 모드 가용성을 설정합니다 (MatchingSection 호환성)
    /// </summary>
    public void SetGameModeAvailable(GameMode gameMode, bool available)
    {
        // Components handle availability internally based on energy/level
        RefreshAvailability();
    }
    
    /// <summary>
    /// 플레이어 수를 업데이트합니다 (MatchingSection 호환성)
    /// </summary>
    public void UpdatePlayerCount(int count)
    {
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.UpdatePlayerCount(count);
    }
    
    /// <summary>
    /// 예상 대기 시간을 업데이트합니다 (MatchingSection 호환성)
    /// </summary>
    public void UpdateEstimatedWaitTimes(Dictionary<GameMode, int> gameModePlayerCounts)
    {
        // This information could be used to update the status display
        Debug.Log($"[IntegratedMatchingUI] Updated wait times for {gameModePlayerCounts?.Count ?? 0} game modes");
    }
    
    /// <summary>
    /// 오프라인 모드를 설정합니다 (MatchingSection 호환성)
    /// </summary>
    public void SetOfflineMode(bool isOffline)
    {
        if (isOffline)
        {
            ShowMessage("오프라인 모드입니다. 매칭을 사용할 수 없습니다.", MessageType.Warning);
            SetButtonsInteractable(false);
        }
        else
        {
            RefreshAvailability();
        }
    }
    
    /// <summary>
    /// UI를 강제로 새로고침합니다 (MatchingSection 호환성)
    /// </summary>
    public void ForceRefresh()
    {
        if (matchingUI != null) matchingUI.RefreshUI();
        if (matchingStatusDisplay != null) matchingStatusDisplay.RefreshUI();
        if (matchingProgressAnimator != null) matchingProgressAnimator.RefreshUI();
        if (playerCountSelector != null) playerCountSelector.RefreshUI();
        
        RefreshAvailability();
    }
    #endregion
    
    #region Private Methods
    private void StartMatching(UIMatchType matchType, GameMode gameMode)
    {
        var request = CreateNetworkMatchingRequest(matchType, gameMode, _currentPlayerCount);
        OnMatchingRequested?.Invoke(request);
    }
    
    private void CancelMatching()
    {
        OnMatchCancelRequested?.Invoke();
    }
    
    private void UpdateLegacyUI(MatchingState state)
    {
        if (matchStatusText != null)
        {
            matchStatusText.text = GetStatusText(state);
        }
        
        if (cancelMatchButton != null)
        {
            cancelMatchButton.gameObject.SetActive(state == MatchingState.Searching);
        }
        
        bool buttonsEnabled = state == MatchingState.Idle;
        SetButtonsInteractable(buttonsEnabled);
    }
    
    private string GetStatusText(MatchingState state)
    {
        return state switch
        {
            MatchingState.Idle => "매칭 대기 중",
            MatchingState.Searching => "상대방을 찾는 중...",
            MatchingState.Found => "매칭 완료!",
            MatchingState.Starting => "게임 시작 중...",
            MatchingState.Cancelled => "매칭이 취소되었습니다",
            MatchingState.Failed => "매칭 실패",
            _ => "알 수 없는 상태"
        };
    }
    
    private void RefreshAvailability(int? level = null, int? energy = null)
    {
        // Get current user data if parameters not provided
        var userData = _userDataManager?.CurrentUser;
        if (userData != null)
        {
            level ??= userData.Level;
            energy ??= userData.CurrentEnergy;
        }
        
        // Update availability based on energy and level
        bool canMatch = (energy ?? 0) > 0 && (level ?? 0) > 0;
        SetButtonsInteractable(canMatch && _currentState == MatchingState.Idle);
    }
    #endregion
    
    #region Event Handlers
    private void OnMatchingUIRequested(UIMatchingRequest request)
    {
        // Convert the UI MatchingRequest to Network MatchingRequest
        var networkRequest = CreateNetworkMatchingRequest(
            request.MatchType,
            _currentGameMode,
            request.PlayerCount
        );
        
        OnMatchingRequested?.Invoke(networkRequest);
    }
    
    private void OnPlayerCountUIChanged(int playerCount)
    {
        _currentPlayerCount = playerCount;
        
        // Update player count selector if it wasn't the source
        if (playerCountSelector != null && playerCountSelector.GetSelectedPlayerCount() != playerCount)
        {
            playerCountSelector.SetPlayerCount(playerCount);
        }
    }
    
    private void OnMatchingUICancelled()
    {
        OnMatchCancelRequested?.Invoke();
    }
    
    private void OnMatchTypeUIChanged(UIMatchType matchType)
    {
        // Handle match type changes if needed
        Debug.Log($"[IntegratedMatchingUI] Match type changed to: {matchType}");
    }
    
    private void OnPlayerCountSelectorChanged(int playerCount)
    {
        _currentPlayerCount = playerCount;
        
        // Update main matching UI if it wasn't the source
        if (matchingUI != null && matchingUI.GetSelectedPlayerCount() != playerCount)
        {
            matchingUI.SetPlayerCount(playerCount);
        }
    }
    #endregion
}

#region Data Structure Compatibility
// Use existing MatchingRequest from Network layer
// Convert our UI enums to string-based format for existing system

/// <summary>
/// Convert UI match type to string for MatchingRequest
/// </summary>
private string MatchTypeToString(UIMatchType matchType)
{
    return matchType switch
    {
        UIMatchType.Random => "random",
        UIMatchType.Room => "room",
        UIMatchType.RoomCreate => "room_create", 
        UIMatchType.Tournament => "tournament",
        _ => "random"
    };
}

/// <summary>
/// Convert GameMode enum to string for MatchingRequest
/// </summary>
private string GameModeToString(GameMode gameMode)
{
    return gameMode switch
    {
        GameMode.Classic => "classic",
        GameMode.Speed => "speed",
        GameMode.Challenge => "challenge",
        GameMode.Ranked => "ranked",
        _ => "classic"
    };
}

/// <summary>
/// Create MatchingRequest compatible with existing network layer
/// </summary>
private MatchingRequest CreateNetworkMatchingRequest(UIMatchType matchType, GameMode gameMode, int playerCount)
{
    var userData = _userDataManager?.CurrentUser;
    
    var request = new MatchingRequest
    {
        playerId = userData?.UserId ?? "unknown",
        matchType = MatchTypeToString(matchType),
        playerCount = playerCount,
        gameMode = GameModeToString(gameMode),
        playerLevel = userData?.Level ?? 1,
        language = "ko"
    };
    
    return request;
}

public enum MessageType
{
    Info,
    Warning,
    Error,
    Success
}
#endregion