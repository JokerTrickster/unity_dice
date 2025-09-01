using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에너지 관련 알림을 표시하는 UI 컴포넌트
/// 에너지 변화, 구매, 경고 등의 알림을 시각적으로 표시합니다.
/// </summary>
public class EnergyNotification : MonoBehaviour
{
    #region UI Components
    [Header("UI References")]
    [SerializeField] private Text messageText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    
    [Header("Animation Settings")]
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private Vector2 slideOffset = new Vector2(0, 50f);
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Notification Assets
    [Header("Notification Assets")]
    [SerializeField] private Sprite energyGainIcon;
    [SerializeField] private Sprite energyConsumeIcon;
    [SerializeField] private Sprite energyPurchaseIcon;
    [SerializeField] private Sprite warningIcon;
    [SerializeField] private Sprite errorIcon;
    [SerializeField] private Sprite successIcon;
    [SerializeField] private Sprite infoIcon;
    #endregion

    #region Colors
    [Header("Notification Colors")]
    [SerializeField] private Color energyGainColor = Color.green;
    [SerializeField] private Color energyConsumeColor = Color.red;
    [SerializeField] private Color energyPurchaseColor = Color.blue;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color infoColor = Color.white;
    #endregion

    #region Private Fields
    private bool _isShowing = false;
    private Coroutine _showCoroutine;
    private Vector2 _originalPosition;
    private Vector2 _targetPosition;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializePositions();
        
        // 시작 시 비활성화 상태
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        StopShowAnimation();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 알림 표시
    /// </summary>
    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("[EnergyNotification] Cannot show notification with empty message");
            return;
        }

        // 기존 애니메이션 중지
        StopShowAnimation();
        
        // UI 설정
        SetupNotificationUI(message, type);
        
        // 애니메이션 시작
        _showCoroutine = StartCoroutine(ShowAnimationCoroutine());
        
        Debug.Log($"[EnergyNotification] Showing: {message} ({type})");
    }

    /// <summary>
    /// 알림 즉시 숨기기
    /// </summary>
    public void Hide()
    {
        StopShowAnimation();
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        _isShowing = false;
    }

    /// <summary>
    /// 알림 표시 중인지 확인
    /// </summary>
    public bool IsShowing => _isShowing;
    #endregion

    #region Setup and Configuration
    private void ValidateComponents()
    {
        if (messageText == null)
        {
            messageText = GetComponentInChildren<Text>();
            if (messageText == null)
                Debug.LogWarning("[EnergyNotification] Message text component not found");
        }
        
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
                Debug.LogWarning("[EnergyNotification] Background image component not found");
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void InitializePositions()
    {
        if (rectTransform != null)
        {
            _originalPosition = rectTransform.anchoredPosition;
            _targetPosition = _originalPosition + slideOffset;
        }
    }

    private void SetupNotificationUI(string message, NotificationType type)
    {
        // 메시지 설정
        if (messageText != null)
        {
            messageText.text = message;
        }
        
        // 아이콘과 색상 설정
        SetupNotificationStyle(type);
    }

    private void SetupNotificationStyle(NotificationType type)
    {
        Color notificationColor = GetNotificationColor(type);
        Sprite notificationIcon = GetNotificationIcon(type);
        
        // 배경 색상 설정
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(notificationColor.r, notificationColor.g, notificationColor.b, 0.8f);
        }
        
        // 아이콘 설정
        if (iconImage != null && notificationIcon != null)
        {
            iconImage.sprite = notificationIcon;
            iconImage.color = notificationColor;
            iconImage.gameObject.SetActive(true);
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }
        
        // 텍스트 색상 설정
        if (messageText != null)
        {
            messageText.color = GetContrastColor(notificationColor);
        }
    }
    #endregion

    #region Style Configuration
    private Color GetNotificationColor(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.EnergyGain:
                return energyGainColor;
            case NotificationType.EnergyConsume:
                return energyConsumeColor;
            case NotificationType.EnergyPurchase:
                return energyPurchaseColor;
            case NotificationType.Warning:
                return warningColor;
            case NotificationType.Error:
                return errorColor;
            case NotificationType.Success:
                return successColor;
            case NotificationType.Info:
            default:
                return infoColor;
        }
    }

    private Sprite GetNotificationIcon(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.EnergyGain:
                return energyGainIcon;
            case NotificationType.EnergyConsume:
                return energyConsumeIcon;
            case NotificationType.EnergyPurchase:
                return energyPurchaseIcon;
            case NotificationType.Warning:
                return warningIcon;
            case NotificationType.Error:
                return errorIcon;
            case NotificationType.Success:
                return successIcon;
            case NotificationType.Info:
            default:
                return infoIcon;
        }
    }

    private Color GetContrastColor(Color backgroundColor)
    {
        // 배경 색상에 따라 대비되는 텍스트 색상 반환
        float brightness = (backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f);
        return brightness > 0.5f ? Color.black : Color.white;
    }
    #endregion

    #region Animation
    private IEnumerator ShowAnimationCoroutine()
    {
        _isShowing = true;
        
        // 초기 설정
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = _targetPosition;
        }
        
        // Fade In + Slide In
        yield return StartCoroutine(FadeInCoroutine());
        
        // 표시 시간 대기
        yield return new WaitForSeconds(showDuration);
        
        // Fade Out + Slide Out
        yield return StartCoroutine(FadeOutCoroutine());
        
        // 완료 처리
        _isShowing = false;
        
        // 자동 파괴 (ObjectPool 사용 시 비활성화로 변경 가능)
        Destroy(gameObject);
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;
        Vector2 startPosition = rectTransform.anchoredPosition;
        
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInTime;
            
            // 애니메이션 커브 적용
            float curveValue = animationCurve.Evaluate(t);
            
            // 알파 애니메이션
            if (canvasGroup != null)
            {
                canvasGroup.alpha = curveValue;
            }
            
            // 슬라이드 애니메이션
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, _originalPosition, curveValue);
            }
            
            yield return null;
        }
        
        // 최종 상태 설정
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = _originalPosition;
        }
    }

    private IEnumerator FadeOutCoroutine()
    {
        float elapsed = 0f;
        Vector2 startPosition = rectTransform.anchoredPosition;
        
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;
            
            // 역방향 애니메이션 커브
            float curveValue = 1f - animationCurve.Evaluate(t);
            
            // 알파 애니메이션
            if (canvasGroup != null)
            {
                canvasGroup.alpha = curveValue;
            }
            
            // 슬라이드 애니메이션 (반대 방향)
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, _targetPosition, t);
            }
            
            yield return null;
        }
        
        // 최종 상태 설정
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void StopShowAnimation()
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
    }
    #endregion

    #region Configuration Methods
    /// <summary>
    /// 애니메이션 설정 변경
    /// </summary>
    public void SetAnimationSettings(float showDuration, float fadeInTime, float fadeOutTime)
    {
        this.showDuration = Mathf.Max(0.1f, showDuration);
        this.fadeInTime = Mathf.Max(0.1f, fadeInTime);
        this.fadeOutTime = Mathf.Max(0.1f, fadeOutTime);
    }

    /// <summary>
    /// 슬라이드 오프셋 설정
    /// </summary>
    public void SetSlideOffset(Vector2 offset)
    {
        slideOffset = offset;
        _targetPosition = _originalPosition + slideOffset;
    }

    /// <summary>
    /// 커스텀 색상 설정
    /// </summary>
    public void SetCustomColor(NotificationType type, Color color)
    {
        switch (type)
        {
            case NotificationType.EnergyGain:
                energyGainColor = color;
                break;
            case NotificationType.EnergyConsume:
                energyConsumeColor = color;
                break;
            case NotificationType.EnergyPurchase:
                energyPurchaseColor = color;
                break;
            case NotificationType.Warning:
                warningColor = color;
                break;
            case NotificationType.Error:
                errorColor = color;
                break;
            case NotificationType.Success:
                successColor = color;
                break;
            case NotificationType.Info:
                infoColor = color;
                break;
        }
    }
    #endregion
}