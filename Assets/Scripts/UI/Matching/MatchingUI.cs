using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매칭 UI 메인 컨트롤러
/// 인원수 선택, 상태 표시, 애니메이션, 진행 상황을 통합 관리합니다.
/// 기존 MatchingSectionUI와 통합되어 완전한 매칭 시스템을 제공합니다.
/// </summary>
public class MatchingUI : MonoBehaviour
{
    #region UI Component References
    [Header("Core Components")]
    [SerializeField] private PlayerCountSelector playerCountSelector;
    [SerializeField] private MatchingStatusDisplay matchingStatusDisplay;
    [SerializeField] private MatchingProgressAnimator matchingProgressAnimator;
    
    [Header("UI Elements")]
    [SerializeField] private Button randomMatchingButton;
    [SerializeField] private Button roomMatchingButton;
    [SerializeField] private Button cancelMatchingButton;
    [SerializeField] private GameObject matchingControlsContainer;
    [SerializeField] private GameObject matchingStatusContainer;
    
    [Header("Visual Feedback")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color searchingColor = Color.yellow;
    [SerializeField] private Color foundColor = Color.green;
    [SerializeField] private Color failedColor = Color.red;
    
    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion
    
    #region Private Fields
    private MatchingState _currentState = MatchingState.Idle;
    private int _selectedPlayerCount = 2;
    private MatchType _currentMatchType = MatchType.Random;
    private Coroutine _stateTransitionCoroutine;
    private bool _isInitialized = false;
    
    // Cache frequently used components
    private UserDataManager _userDataManager;
    private EnergyManager _energyManager;
    private MatchingManager _matchingManager;
    #endregion
    
    #region Events
    public static event Action<MatchingRequest> OnMatchingRequested;
    public static event Action<int> OnPlayerCountChanged;
    public static event Action OnMatchingCancelled;
    public static event Action<MatchType> OnMatchTypeChanged;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializeReferences();
    }
    
    private void Start()
    {
        InitializeUI();
        SetupEventListeners();
        UpdateUIState();
        _isInitialized = true;
    }
    
    private void OnEnable()
    {
        if (_isInitialized)
        {
            SubscribeToEvents();
            RefreshUI();
        }
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvents();
        
        if (_currentState == MatchingState.Searching)
        {
            CancelMatching();
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupCoroutines();
    }
    #endregion
    
    #region Initialization
    private void ValidateComponents()
    {
        if (playerCountSelector == null)
        {
            playerCountSelector = GetComponentInChildren<PlayerCountSelector>();
            if (playerCountSelector == null)
                Debug.LogError("[MatchingUI] PlayerCountSelector component is missing!");
        }
        
        if (matchingStatusDisplay == null)
        {
            matchingStatusDisplay = GetComponentInChildren<MatchingStatusDisplay>();
            if (matchingStatusDisplay == null)
                Debug.LogError("[MatchingUI] MatchingStatusDisplay component is missing!");
        }
        
        if (matchingProgressAnimator == null)
        {
            matchingProgressAnimator = GetComponentInChildren<MatchingProgressAnimator>();
            if (matchingProgressAnimator == null)
                Debug.LogError("[MatchingUI] MatchingProgressAnimator component is missing!");
        }
        
        if (randomMatchingButton == null)
            Debug.LogError("[MatchingUI] Random matching button is missing!");
            
        if (roomMatchingButton == null)
            Debug.LogError("[MatchingUI] Room matching button is missing!");
            
        if (cancelMatchingButton == null)
            Debug.LogError("[MatchingUI] Cancel matching button is missing!");
    }
    
    private void InitializeReferences()
    {
        _userDataManager = UserDataManager.Instance;
        _energyManager = EnergyManager.Instance;
        _matchingManager = MatchingManager.Instance;
        
        if (mainCanvasGroup == null)
            mainCanvasGroup = GetComponent<CanvasGroup>();
    }
    
    private void InitializeUI()
    {
        // Initialize sub-components
        if (playerCountSelector != null)
        {
            playerCountSelector.Initialize();
            _selectedPlayerCount = playerCountSelector.GetSelectedPlayerCount();
        }
        
        if (matchingStatusDisplay != null)
        {
            matchingStatusDisplay.Initialize();
        }
        
        if (matchingProgressAnimator != null)
        {
            matchingProgressAnimator.Initialize();
        }
        
        // Set initial UI state
        SetMatchingState(MatchingState.Idle);
        UpdateButtonStates();
        UpdateVisualFeedback();
    }
    #endregion
    
    #region Event Management
    private void SetupEventListeners()
    {
        // Button events
        if (randomMatchingButton != null)
            randomMatchingButton.onClick.AddListener(() => StartMatching(MatchType.Random));
            
        if (roomMatchingButton != null)
            roomMatchingButton.onClick.AddListener(() => StartMatching(MatchType.Room));
            
        if (cancelMatchingButton != null)
            cancelMatchingButton.onClick.AddListener(CancelMatching);
        
        // Component events
        if (playerCountSelector != null)
            playerCountSelector.OnPlayerCountChanged += OnPlayerCountSelectorChanged;
    }
    
    private void SubscribeToEvents()
    {
        // Subscribe to matching manager events
        if (_matchingManager != null)
        {
            MatchingManager.OnStateChanged += OnMatchingStateChanged;
            MatchingManager.OnMatchFound += OnMatchFound;
            MatchingManager.OnMatchingError += OnMatchingError;
        }
        
        // Subscribe to energy manager events
        if (_energyManager != null)
        {
            EnergyManager.OnEnergyChanged += OnEnergyChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        // Button events cleanup
        if (randomMatchingButton != null)
            randomMatchingButton.onClick.RemoveAllListeners();
            
        if (roomMatchingButton != null)
            roomMatchingButton.onClick.RemoveAllListeners();
            
        if (cancelMatchingButton != null)
            cancelMatchingButton.onClick.RemoveAllListeners();
        
        // Component events cleanup
        if (playerCountSelector != null)
            playerCountSelector.OnPlayerCountChanged -= OnPlayerCountSelectorChanged;
        
        // Manager events cleanup
        if (_matchingManager != null)
        {
            MatchingManager.OnStateChanged -= OnMatchingStateChanged;
            MatchingManager.OnMatchFound -= OnMatchFound;
            MatchingManager.OnMatchingError -= OnMatchingError;
        }
        
        if (_energyManager != null)
        {
            EnergyManager.OnEnergyChanged -= OnEnergyChanged;
        }
    }
    #endregion
    
    #region State Management
    public void SetMatchingState(MatchingState newState)
    {
        if (_currentState == newState) return;
        
        var previousState = _currentState;
        _currentState = newState;
        
        Debug.Log($"[MatchingUI] State changed: {previousState} -> {newState}");
        
        UpdateUIState();
        UpdateVisualFeedback();
        
        // Update sub-components
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.SetState(newState);
            
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.SetState(newState);
    }
    
    private void UpdateUIState()
    {
        UpdateButtonStates();
        UpdateContainerVisibility();
    }
    
    private void UpdateButtonStates()
    {
        bool canStartMatching = CanStartMatching();
        bool isMatching = _currentState == MatchingState.Searching;
        
        if (randomMatchingButton != null)
            randomMatchingButton.interactable = canStartMatching && !isMatching;
            
        if (roomMatchingButton != null)
            roomMatchingButton.interactable = canStartMatching && !isMatching;
            
        if (cancelMatchingButton != null)
            cancelMatchingButton.gameObject.SetActive(isMatching);
    }
    
    private void UpdateContainerVisibility()
    {
        if (matchingControlsContainer != null)
            matchingControlsContainer.SetActive(_currentState != MatchingState.Searching);
            
        if (matchingStatusContainer != null)
            matchingStatusContainer.SetActive(_currentState != MatchingState.Idle);
    }
    
    private void UpdateVisualFeedback()
    {
        if (backgroundImage == null) return;
        
        Color targetColor = _currentState switch
        {
            MatchingState.Searching => searchingColor,
            MatchingState.Found => foundColor,
            MatchingState.Failed => failedColor,
            _ => normalColor
        };
        
        if (_stateTransitionCoroutine != null)
            StopCoroutine(_stateTransitionCoroutine);
            
        _stateTransitionCoroutine = StartCoroutine(TransitionBackgroundColor(targetColor));
    }
    
    private IEnumerator TransitionBackgroundColor(Color targetColor)
    {
        Color startColor = backgroundImage.color;
        float elapsed = 0f;
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            backgroundImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }
        
        backgroundImage.color = targetColor;
        _stateTransitionCoroutine = null;
    }
    #endregion
    
    #region Matching Operations
    public void StartMatching(MatchType matchType)
    {
        if (!CanStartMatching())
        {
            Debug.LogWarning($"[MatchingUI] Cannot start matching - conditions not met");
            return;
        }
        
        // Validate energy before starting
        int energyCost = GetEnergyCostForPlayerCount(_selectedPlayerCount);
        if (!_energyManager.CanUseEnergy(energyCost))
        {
            ShowEnergyInsufficientMessage();
            return;
        }
        
        var request = new MatchingRequest
        {
            PlayerCount = _selectedPlayerCount,
            MatchType = matchType,
            RequestTime = DateTime.Now
        };
        
        _currentMatchType = matchType;
        
        Debug.Log($"[MatchingUI] Starting {matchType} matching for {_selectedPlayerCount} players");
        
        // Consume energy
        _energyManager.ConsumeEnergy(energyCost);
        
        // Fire event
        OnMatchingRequested?.Invoke(request);
        
        // Update UI
        SetMatchingState(MatchingState.Searching);
    }
    
    public void CancelMatching()
    {
        if (_currentState != MatchingState.Searching)
            return;
        
        Debug.Log($"[MatchingUI] Cancelling matching");
        
        // Refund energy (partial refund for cancellation)
        int energyCost = GetEnergyCostForPlayerCount(_selectedPlayerCount);
        int refundAmount = energyCost / 2; // 50% refund
        if (refundAmount > 0)
        {
            _energyManager.AddEnergy(refundAmount);
        }
        
        // Fire event
        OnMatchingCancelled?.Invoke();
        
        // Update UI
        SetMatchingState(MatchingState.Idle);
    }
    
    private bool CanStartMatching()
    {
        // Check if user data is available
        if (_userDataManager?.CurrentUser == null)
            return false;
        
        // Check if not already matching
        if (_currentState == MatchingState.Searching)
            return false;
        
        // Check energy
        int energyCost = GetEnergyCostForPlayerCount(_selectedPlayerCount);
        if (!_energyManager.CanUseEnergy(energyCost))
            return false;
        
        // Check network connectivity
        if (NetworkManager.Instance?.IsConnected != true)
            return false;
        
        return true;
    }
    
    private int GetEnergyCostForPlayerCount(int playerCount)
    {
        // Energy cost increases with player count
        return playerCount switch
        {
            2 => 1,
            3 => 2,
            4 => 3,
            _ => 1
        };
    }
    #endregion
    
    #region Event Handlers
    private void OnPlayerCountSelectorChanged(int newPlayerCount)
    {
        _selectedPlayerCount = newPlayerCount;
        UpdateButtonStates();
        OnPlayerCountChanged?.Invoke(newPlayerCount);
        
        Debug.Log($"[MatchingUI] Player count changed to: {newPlayerCount}");
    }
    
    private void OnMatchingStateChanged(MatchingState newState)
    {
        SetMatchingState(newState);
    }
    
    private void OnMatchFound(MatchFoundData matchData)
    {
        Debug.Log($"[MatchingUI] Match found: {matchData.RoomId} with {matchData.PlayerCount} players");
        SetMatchingState(MatchingState.Found);
    }
    
    private void OnMatchingError(string error)
    {
        Debug.LogError($"[MatchingUI] Matching error: {error}");
        SetMatchingState(MatchingState.Failed);
        
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.ShowError(error);
        
        // Auto-reset to idle after showing error
        StartCoroutine(ResetToIdleAfterDelay(3f));
    }
    
    private void OnEnergyChanged(int newEnergy)
    {
        UpdateButtonStates();
    }
    
    private IEnumerator ResetToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetMatchingState(MatchingState.Idle);
    }
    #endregion
    
    #region Public API
    /// <summary>
    /// 현재 매칭 상태를 반환합니다
    /// </summary>
    public MatchingState GetCurrentState()
    {
        return _currentState;
    }
    
    /// <summary>
    /// 선택된 플레이어 수를 반환합니다
    /// </summary>
    public int GetSelectedPlayerCount()
    {
        return _selectedPlayerCount;
    }
    
    /// <summary>
    /// 플레이어 수를 설정합니다
    /// </summary>
    public void SetPlayerCount(int playerCount)
    {
        if (playerCount >= 2 && playerCount <= 4)
        {
            if (playerCountSelector != null)
                playerCountSelector.SetPlayerCount(playerCount);
        }
    }
    
    /// <summary>
    /// UI를 강제로 새로고침합니다
    /// </summary>
    public void RefreshUI()
    {
        UpdateUIState();
        UpdateVisualFeedback();
        
        if (playerCountSelector != null)
            playerCountSelector.RefreshUI();
            
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.RefreshUI();
            
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.RefreshUI();
    }
    
    /// <summary>
    /// 매칭 진행 상황을 업데이트합니다
    /// </summary>
    public void UpdateMatchingProgress(float progress, TimeSpan elapsed, TimeSpan estimated)
    {
        if (matchingProgressAnimator != null)
            matchingProgressAnimator.UpdateProgress(progress, elapsed, estimated);
    }
    
    /// <summary>
    /// 매칭 상태 메시지를 표시합니다
    /// </summary>
    public void ShowStatusMessage(string message, MessageType type = MessageType.Info)
    {
        if (matchingStatusDisplay != null)
            matchingStatusDisplay.ShowMessage(message, type);
    }
    #endregion
    
    #region Utility Methods
    private void ShowEnergyInsufficientMessage()
    {
        int required = GetEnergyCostForPlayerCount(_selectedPlayerCount);
        int current = _energyManager.GetCurrentEnergy();
        
        string message = $"에너지가 부족합니다. (필요: {required}, 보유: {current})";
        ShowStatusMessage(message, MessageType.Warning);
    }
    
    private void CleanupCoroutines()
    {
        if (_stateTransitionCoroutine != null)
        {
            StopCoroutine(_stateTransitionCoroutine);
            _stateTransitionCoroutine = null;
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// 매칭 요청 데이터
/// </summary>
[Serializable]
public class MatchingRequest
{
    public int PlayerCount;
    public MatchType MatchType;
    public DateTime RequestTime;
}

/// <summary>
/// 매칭 타입
/// </summary>
public enum MatchType
{
    Random,  // 랜덤 매칭
    Room     // 방 매칭 (미래 구현)
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