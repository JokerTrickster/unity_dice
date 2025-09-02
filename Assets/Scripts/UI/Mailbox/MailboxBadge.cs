using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 우편함 뱃지 UI 컴포넌트
/// 읽지 않은 메시지 수를 표시하는 뱃지를 관리합니다.
/// MailboxManager의 이벤트를 구독하여 실시간으로 업데이트됩니다.
/// </summary>
public class MailboxBadge : MonoBehaviour
{
    #region UI References
    [Header("Badge UI")]
    [SerializeField] private GameObject badgeContainer;
    [SerializeField] private Text unreadCountText;
    [SerializeField] private Image badgeBackground;
    
    [Header("Animation")]
    [SerializeField] private AnimationCurve scaleAnimationCurve = AnimationCurve.EaseOutBounce(0, 0, 1, 1);
    [SerializeField] private AnimationCurve pulseAnimationCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.8f);
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float pulseDuration = 1.0f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalBadgeColor = Color.red;
    [SerializeField] private Color highPriorityColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    [SerializeField] private int highPriorityThreshold = 5;
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 highlightScale = Vector3.one * 1.1f;
    #endregion

    #region Private Fields
    private int _currentUnreadCount = 0;
    private bool _isAnimating = false;
    private Coroutine _pulseCoroutine;
    private Coroutine _scaleCoroutine;
    private Vector3 _originalScale;
    
    // 성능 최적화를 위한 캐시
    private readonly string _countFormat = "{0}";
    private readonly int _maxDisplayCount = 99;
    private readonly string _maxDisplayText = "99+";
    #endregion

    #region Properties
    /// <summary>
    /// 현재 읽지 않은 메시지 수
    /// </summary>
    public int UnreadCount => _currentUnreadCount;
    
    /// <summary>
    /// 뱃지가 표시되고 있는지 여부
    /// </summary>
    public bool IsVisible => badgeContainer != null && badgeContainer.activeInHierarchy;
    
    /// <summary>
    /// 애니메이션 진행 중 여부
    /// </summary>
    public bool IsAnimating => _isAnimating;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializeBadge();
    }

    private void Start()
    {
        SubscribeToMailboxEvents();
        RefreshBadgeFromManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromMailboxEvents();
        StopAllAnimations();
    }
    #endregion

    #region Initialization
    private void ValidateComponents()
    {
        if (badgeContainer == null)
        {
            Debug.LogError("[MailboxBadge] Badge container is not assigned!");
            return;
        }

        if (unreadCountText == null)
        {
            Debug.LogError("[MailboxBadge] Unread count text is not assigned!");
            return;
        }

        if (badgeBackground == null)
        {
            badgeBackground = badgeContainer.GetComponent<Image>();
            if (badgeBackground == null)
            {
                Debug.LogWarning("[MailboxBadge] Badge background image not found");
            }
        }
    }

    private void InitializeBadge()
    {
        if (badgeContainer != null)
        {
            _originalScale = badgeContainer.transform.localScale;
            badgeContainer.SetActive(false);
        }

        if (badgeBackground != null)
        {
            badgeBackground.color = normalBadgeColor;
        }

        _currentUnreadCount = 0;
        
        Debug.Log("[MailboxBadge] Badge initialized successfully");
    }
    #endregion

    #region Event Subscription
    private void SubscribeToMailboxEvents()
    {
        MailboxManager.OnUnreadCountChanged += HandleUnreadCountChanged;
        MailboxManager.OnMailboxLoaded += HandleMailboxLoaded;
        MailboxManager.OnNewMessageReceived += HandleNewMessage;
        
        Debug.Log("[MailboxBadge] Subscribed to MailboxManager events");
    }

    private void UnsubscribeFromMailboxEvents()
    {
        MailboxManager.OnUnreadCountChanged -= HandleUnreadCountChanged;
        MailboxManager.OnMailboxLoaded -= HandleMailboxLoaded;
        MailboxManager.OnNewMessageReceived -= HandleNewMessage;
        
        Debug.Log("[MailboxBadge] Unsubscribed from MailboxManager events");
    }
    #endregion

    #region Event Handlers
    private void HandleUnreadCountChanged(int newCount)
    {
        UpdateBadgeCount(newCount, true);
        
        Debug.Log($"[MailboxBadge] Unread count changed: {newCount}");
    }

    private void HandleMailboxLoaded(MailboxData mailboxData)
    {
        if (mailboxData != null)
        {
            UpdateBadgeCount(mailboxData.unreadCount, false);
        }
        
        Debug.Log($"[MailboxBadge] Mailbox loaded with {mailboxData?.unreadCount ?? 0} unread messages");
    }

    private void HandleNewMessage(MailboxMessage message)
    {
        if (message != null && !message.isRead)
        {
            // 새 메시지 도착 시 특별한 애니메이션
            StartHighlightAnimation();
        }
        
        Debug.Log($"[MailboxBadge] New message received: {message?.title}");
    }
    #endregion

    #region Badge Update
    /// <summary>
    /// 뱃지 카운트 업데이트
    /// </summary>
    /// <param name="newCount">새로운 읽지 않은 메시지 수</param>
    /// <param name="animate">애니메이션 여부</param>
    public void UpdateBadgeCount(int newCount, bool animate = true)
    {
        int previousCount = _currentUnreadCount;
        _currentUnreadCount = Mathf.Max(0, newCount);
        
        // UI 업데이트
        UpdateBadgeVisibility();
        UpdateBadgeText();
        UpdateBadgeColor();
        
        // 애니메이션
        if (animate && previousCount != _currentUnreadCount)
        {
            if (_currentUnreadCount > previousCount)
            {
                StartIncreaseAnimation();
            }
            else
            {
                StartDecreaseAnimation();
            }
        }
        
        Debug.Log($"[MailboxBadge] Badge updated: {previousCount} -> {_currentUnreadCount}");
    }

    private void UpdateBadgeVisibility()
    {
        if (badgeContainer == null) return;
        
        bool shouldShow = _currentUnreadCount > 0;
        
        if (badgeContainer.activeInHierarchy != shouldShow)
        {
            badgeContainer.SetActive(shouldShow);
            
            if (shouldShow)
            {
                // 뱃지가 처음 표시될 때 스케일 애니메이션
                badgeContainer.transform.localScale = Vector3.zero;
                StartCoroutine(AnimateScale(Vector3.zero, _originalScale, animationDuration));
            }
        }
    }

    private void UpdateBadgeText()
    {
        if (unreadCountText == null) return;
        
        string displayText = _currentUnreadCount <= _maxDisplayCount ? 
            string.Format(_countFormat, _currentUnreadCount) : _maxDisplayText;
        
        unreadCountText.text = displayText;
    }

    private void UpdateBadgeColor()
    {
        if (badgeBackground == null) return;
        
        Color targetColor = _currentUnreadCount >= highPriorityThreshold ? 
            highPriorityColor : normalBadgeColor;
        
        badgeBackground.color = targetColor;
    }
    #endregion

    #region Animations
    private void StartIncreaseAnimation()
    {
        if (_isAnimating) return;
        
        StopAllAnimations();
        _scaleCoroutine = StartCoroutine(AnimateScale(_originalScale, highlightScale, animationDuration * 0.5f, () => 
        {
            _scaleCoroutine = StartCoroutine(AnimateScale(highlightScale, _originalScale, animationDuration * 0.5f));
        }));
    }

    private void StartDecreaseAnimation()
    {
        if (_isAnimating) return;
        
        StopAllAnimations();
        _scaleCoroutine = StartCoroutine(AnimateScale(_originalScale, _originalScale * 0.9f, animationDuration * 0.3f, () => 
        {
            _scaleCoroutine = StartCoroutine(AnimateScale(_originalScale * 0.9f, _originalScale, animationDuration * 0.7f));
        }));
    }

    private void StartHighlightAnimation()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
        }
        
        _pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    private IEnumerator AnimateScale(Vector3 fromScale, Vector3 toScale, float duration, System.Action onComplete = null)
    {
        if (badgeContainer == null) yield break;
        
        _isAnimating = true;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / duration;
            float curveValue = scaleAnimationCurve.Evaluate(normalizedTime);
            
            Vector3 currentScale = Vector3.Lerp(fromScale, toScale, curveValue);
            badgeContainer.transform.localScale = currentScale;
            
            yield return null;
        }
        
        badgeContainer.transform.localScale = toScale;
        _isAnimating = false;
        
        onComplete?.Invoke();
    }

    private IEnumerator PulseAnimation()
    {
        if (badgeContainer == null) yield break;
        
        float elapsedTime = 0f;
        Vector3 originalScale = badgeContainer.transform.localScale;
        
        while (elapsedTime < pulseDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / pulseDuration;
            float pulseValue = pulseAnimationCurve.Evaluate(normalizedTime);
            
            Vector3 pulseScale = originalScale * pulseValue;
            badgeContainer.transform.localScale = pulseScale;
            
            yield return null;
        }
        
        badgeContainer.transform.localScale = originalScale;
        _pulseCoroutine = null;
    }

    private void StopAllAnimations()
    {
        if (_scaleCoroutine != null)
        {
            StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = null;
        }
        
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        
        _isAnimating = false;
    }
    #endregion

    #region Public API
    /// <summary>
    /// MailboxManager에서 현재 상태 새로고침
    /// </summary>
    public void RefreshBadgeFromManager()
    {
        if (MailboxManager.Instance != null && MailboxManager.Instance.IsInitialized)
        {
            UpdateBadgeCount(MailboxManager.Instance.UnreadCount, false);
        }
        else
        {
            UpdateBadgeCount(0, false);
        }
        
        Debug.Log("[MailboxBadge] Badge refreshed from MailboxManager");
    }

    /// <summary>
    /// 뱃지 강제 숨기기
    /// </summary>
    public void HideBadge()
    {
        if (badgeContainer != null)
        {
            badgeContainer.SetActive(false);
        }
        
        StopAllAnimations();
        Debug.Log("[MailboxBadge] Badge manually hidden");
    }

    /// <summary>
    /// 뱃지 강제 표시
    /// </summary>
    public void ShowBadge()
    {
        if (badgeContainer != null && _currentUnreadCount > 0)
        {
            badgeContainer.SetActive(true);
            badgeContainer.transform.localScale = _originalScale;
        }
        
        Debug.Log("[MailboxBadge] Badge manually shown");
    }

    /// <summary>
    /// 수동으로 뱃지 값 설정 (테스트용)
    /// </summary>
    public void SetBadgeCountManually(int count)
    {
        UpdateBadgeCount(count, true);
        Debug.Log($"[MailboxBadge] Badge count manually set to: {count}");
    }
    #endregion
}