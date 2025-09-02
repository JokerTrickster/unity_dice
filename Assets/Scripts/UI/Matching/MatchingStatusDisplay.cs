using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매칭 상태 표시 컴포넌트
/// 실시간 매칭 진행 상태, 대기 시간, 상태 메시지를 표시합니다.
/// 다양한 매칭 상태에 따른 시각적 피드백을 제공합니다.
/// </summary>
public class MatchingStatusDisplay : MonoBehaviour
{
    #region UI References
    [Header("Status Text")]
    [SerializeField] private Text mainStatusText;
    [SerializeField] private Text subStatusText;
    [SerializeField] private Text waitTimeText;
    [SerializeField] private Text playerCountText;
    
    [Header("Visual Elements")]
    [SerializeField] private Image statusIcon;
    [SerializeField] private Image statusBackground;
    [SerializeField] private Slider progressBar;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Status Icons")]
    [SerializeField] private Sprite idleIcon;
    [SerializeField] private Sprite searchingIcon;
    [SerializeField] private Sprite foundIcon;
    [SerializeField] private Sprite errorIcon;
    [SerializeField] private Sprite successIcon;
    
    [Header("Status Colors")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color searchingColor = Color.yellow;
    [SerializeField] private Color foundColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color successColor = Color.cyan;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeTransitionDuration = 0.3f;
    [SerializeField] private float textTypewriterSpeed = 0.05f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion
    
    #region Private Fields
    private MatchingState _currentState = MatchingState.Idle;
    private DateTime _stateStartTime;
    private Coroutine _updateCoroutine;
    private Coroutine _transitionCoroutine;
    private Coroutine _typewriterCoroutine;
    private bool _isInitialized = false;
    
    // Cache for optimization
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    
    // Message queue for smooth transitions
    private System.Collections.Generic.Queue<StatusMessage> _messageQueue = 
        new System.Collections.Generic.Queue<StatusMessage>();
    private bool _isShowingMessage = false;
    
    // Status tracking
    private float _currentProgress = 0f;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private TimeSpan _estimatedTime = TimeSpan.Zero;
    private int _currentPlayerCount = 0;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        ValidateReferences();
    }
    
    private void Start()
    {
        SetState(MatchingState.Idle);
        _isInitialized = true;
    }
    
    private void OnEnable()
    {
        if (_isInitialized)
        {
            StartUpdateCoroutine();
        }
    }
    
    private void OnDisable()
    {
        StopUpdateCoroutine();
        StopAllTransitions();
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
    #endregion
    
    #region Initialization
    private void InitializeComponents()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        _rectTransform = GetComponent<RectTransform>();
    }
    
    private void ValidateReferences()
    {
        if (mainStatusText == null)
            Debug.LogError("[MatchingStatusDisplay] Main status text is missing!");
            
        if (statusBackground == null)
            Debug.LogWarning("[MatchingStatusDisplay] Status background is missing");
            
        if (progressBar == null)
            Debug.LogWarning("[MatchingStatusDisplay] Progress bar is missing");
    }
    
    public void Initialize()
    {
        if (_isInitialized) return;
        
        Debug.Log("[MatchingStatusDisplay] Initializing component");
        
        SetState(MatchingState.Idle);
        UpdateVisualElements();
        _isInitialized = true;
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 매칭 상태를 설정합니다
    /// </summary>
    public void SetState(MatchingState newState)
    {
        if (_currentState == newState) return;
        
        var previousState = _currentState;
        _currentState = newState;
        _stateStartTime = DateTime.Now;
        
        Debug.Log($"[MatchingStatusDisplay] State changed: {previousState} -> {newState}");
        
        UpdateStateDisplay();
        StartUpdateCoroutine();
    }
    
    /// <summary>
    /// 메시지를 표시합니다
    /// </summary>
    public void ShowMessage(string message, MessageType type = MessageType.Info, float duration = 3f)
    {
        var statusMessage = new StatusMessage
        {
            Text = message,
            Type = type,
            Duration = duration,
            Timestamp = DateTime.Now
        };
        
        _messageQueue.Enqueue(statusMessage);
        
        if (!_isShowingMessage)
        {
            StartCoroutine(ProcessMessageQueue());
        }
    }
    
    /// <summary>
    /// 에러 메시지를 표시합니다
    /// </summary>
    public void ShowError(string errorMessage)
    {
        ShowMessage(errorMessage, MessageType.Error, 5f);
    }
    
    /// <summary>
    /// 성공 메시지를 표시합니다
    /// </summary>
    public void ShowSuccess(string successMessage)
    {
        ShowMessage(successMessage, MessageType.Success, 3f);
    }
    
    /// <summary>
    /// 진행 상황을 업데이트합니다
    /// </summary>
    public void UpdateProgress(float progress, TimeSpan elapsed, TimeSpan estimated)
    {
        _currentProgress = Mathf.Clamp01(progress);
        _elapsedTime = elapsed;
        _estimatedTime = estimated;
        
        if (progressBar != null)
        {
            progressBar.value = _currentProgress;
        }
        
        UpdateTimeDisplay();
    }
    
    /// <summary>
    /// 플레이어 수 정보를 업데이트합니다
    /// </summary>
    public void UpdatePlayerCount(int playerCount)
    {
        _currentPlayerCount = playerCount;
        
        if (playerCountText != null)
        {
            playerCountText.text = $"대기 중인 플레이어: {playerCount}명";
        }
    }
    
    /// <summary>
    /// UI를 새로고침합니다
    /// </summary>
    public void RefreshUI()
    {
        UpdateVisualElements();
        UpdateTimeDisplay();
    }
    #endregion
    
    #region Private Methods
    private void UpdateStateDisplay()
    {
        string statusText = GetStatusText(_currentState);
        string subText = GetSubStatusText(_currentState);
        
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);
            
        _typewriterCoroutine = StartCoroutine(TypewriterText(statusText, subText));
        
        UpdateVisualElements();
        UpdateLoadingIndicator();
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
    
    private string GetSubStatusText(MatchingState state)
    {
        return state switch
        {
            MatchingState.Idle => "매칭 버튼을 눌러 게임을 시작하세요",
            MatchingState.Searching => "잠시만 기다려주세요",
            MatchingState.Found => "게임에 연결하는 중입니다",
            MatchingState.Starting => "곧 게임이 시작됩니다",
            MatchingState.Cancelled => "다시 매칭을 시도할 수 있습니다",
            MatchingState.Failed => "다시 시도해주세요",
            _ => ""
        };
    }
    
    private void UpdateVisualElements()
    {
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
            
        _transitionCoroutine = StartCoroutine(TransitionVisualElements());
    }
    
    private IEnumerator TransitionVisualElements()
    {
        Color targetColor = GetStateColor(_currentState);
        Sprite targetIcon = GetStateIcon(_currentState);
        
        float elapsed = 0f;
        Color startColor = statusBackground?.color ?? Color.white;
        
        while (elapsed < fadeTransitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = transitionCurve.Evaluate(elapsed / fadeTransitionDuration);
            
            if (statusBackground != null)
                statusBackground.color = Color.Lerp(startColor, targetColor, t);
                
            yield return null;
        }
        
        if (statusBackground != null)
            statusBackground.color = targetColor;
            
        if (statusIcon != null && targetIcon != null)
            statusIcon.sprite = targetIcon;
        
        _transitionCoroutine = null;
    }
    
    private Color GetStateColor(MatchingState state)
    {
        return state switch
        {
            MatchingState.Idle => idleColor,
            MatchingState.Searching => searchingColor,
            MatchingState.Found => foundColor,
            MatchingState.Starting => successColor,
            MatchingState.Cancelled => idleColor,
            MatchingState.Failed => errorColor,
            _ => idleColor
        };
    }
    
    private Sprite GetStateIcon(MatchingState state)
    {
        return state switch
        {
            MatchingState.Idle => idleIcon,
            MatchingState.Searching => searchingIcon,
            MatchingState.Found => foundIcon,
            MatchingState.Starting => successIcon,
            MatchingState.Cancelled => idleIcon,
            MatchingState.Failed => errorIcon,
            _ => idleIcon
        };
    }
    
    private void UpdateLoadingIndicator()
    {
        if (loadingIndicator != null)
        {
            bool shouldShow = _currentState == MatchingState.Searching || 
                             _currentState == MatchingState.Starting;
            loadingIndicator.SetActive(shouldShow);
        }
    }
    
    private void UpdateTimeDisplay()
    {
        if (waitTimeText == null) return;
        
        if (_currentState == MatchingState.Searching)
        {
            var elapsed = DateTime.Now - _stateStartTime;
            string timeText = $"대기 시간: {elapsed:mm\\:ss}";
            
            if (_estimatedTime > TimeSpan.Zero)
            {
                timeText += $" / 예상: {_estimatedTime:mm\\:ss}";
            }
            
            waitTimeText.text = timeText;
        }
        else
        {
            waitTimeText.text = "";
        }
    }
    
    private void StartUpdateCoroutine()
    {
        StopUpdateCoroutine();
        
        if (_currentState == MatchingState.Searching)
        {
            _updateCoroutine = StartCoroutine(UpdateCoroutine());
        }
    }
    
    private void StopUpdateCoroutine()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }
    
    private IEnumerator UpdateCoroutine()
    {
        while (_currentState == MatchingState.Searching)
        {
            UpdateTimeDisplay();
            yield return new WaitForSecondsRealtime(0.5f); // Update twice per second for smooth display
        }
    }
    
    private IEnumerator TypewriterText(string mainText, string subText)
    {
        if (mainStatusText != null)
        {
            mainStatusText.text = "";
            foreach (char c in mainText)
            {
                mainStatusText.text += c;
                yield return new WaitForSecondsRealtime(textTypewriterSpeed);
            }
        }
        
        yield return new WaitForSecondsRealtime(0.2f);
        
        if (subStatusText != null)
        {
            subStatusText.text = "";
            foreach (char c in subText)
            {
                subStatusText.text += c;
                yield return new WaitForSecondsRealtime(textTypewriterSpeed);
            }
        }
        
        _typewriterCoroutine = null;
    }
    
    private IEnumerator ProcessMessageQueue()
    {
        _isShowingMessage = true;
        
        while (_messageQueue.Count > 0)
        {
            var message = _messageQueue.Dequeue();
            yield return StartCoroutine(DisplayMessage(message));
        }
        
        _isShowingMessage = false;
    }
    
    private IEnumerator DisplayMessage(StatusMessage message)
    {
        // Store original state
        var originalMainText = mainStatusText?.text ?? "";
        var originalSubText = subStatusText?.text ?? "";
        
        // Show message with appropriate styling
        if (mainStatusText != null)
        {
            mainStatusText.text = message.Text;
            mainStatusText.color = GetMessageColor(message.Type);
        }
        
        if (subStatusText != null)
        {
            subStatusText.text = GetMessageSubText(message.Type);
        }
        
        // Wait for message duration
        yield return new WaitForSecondsRealtime(message.Duration);
        
        // Restore original state if no state change occurred
        if (_currentState != MatchingState.Searching)
        {
            if (mainStatusText != null)
            {
                mainStatusText.text = originalMainText;
                mainStatusText.color = Color.white;
            }
            
            if (subStatusText != null)
            {
                subStatusText.text = originalSubText;
            }
        }
    }
    
    private Color GetMessageColor(MessageType type)
    {
        return type switch
        {
            MessageType.Info => Color.white,
            MessageType.Warning => Color.yellow,
            MessageType.Error => Color.red,
            MessageType.Success => Color.green,
            _ => Color.white
        };
    }
    
    private string GetMessageSubText(MessageType type)
    {
        return type switch
        {
            MessageType.Warning => "주의가 필요합니다",
            MessageType.Error => "오류가 발생했습니다",
            MessageType.Success => "성공적으로 완료되었습니다",
            _ => ""
        };
    }
    
    private void StopAllTransitions()
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }
        
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }
    }
    #endregion
    
    #region Data Structures
    [Serializable]
    private class StatusMessage
    {
        public string Text;
        public MessageType Type;
        public float Duration;
        public DateTime Timestamp;
    }
    #endregion
    
    #region Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Test Status States")]
    private void TestStatusStates()
    {
        if (!Application.isPlaying) return;
        
        StartCoroutine(TestStatesSequence());
    }
    
    private IEnumerator TestStatesSequence()
    {
        var states = new[] 
        { 
            MatchingState.Idle, 
            MatchingState.Searching, 
            MatchingState.Found, 
            MatchingState.Starting,
            MatchingState.Failed,
            MatchingState.Cancelled
        };
        
        foreach (var state in states)
        {
            Debug.Log($"Testing state: {state}");
            SetState(state);
            yield return new WaitForSeconds(2f);
        }
        
        SetState(MatchingState.Idle);
    }
    
    [ContextMenu("Test Messages")]
    private void TestMessages()
    {
        if (!Application.isPlaying) return;
        
        ShowMessage("정보 메시지입니다", MessageType.Info);
        ShowMessage("경고 메시지입니다", MessageType.Warning);
        ShowError("에러 메시지입니다");
        ShowSuccess("성공 메시지입니다");
    }
    #endif
    #endregion
}