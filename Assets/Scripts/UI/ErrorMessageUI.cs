using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사용자 친화적 오류 메시지 UI 시스템
/// 다양한 타입의 오류 메시지를 표시하고 관리합니다.
/// </summary>
public class ErrorMessageUI : MonoBehaviour
{
    #region Singleton
    private static ErrorMessageUI _instance;
    public static ErrorMessageUI Instance
    {
        get
        {
            if (_instance == null)
            {
                // UI 프리팹에서 찾기 시도
                _instance = FindObjectOfType<ErrorMessageUI>();
                
                if (_instance == null)
                {
                    // 동적 생성
                    GameObject go = new GameObject("ErrorMessageUI");
                    _instance = go.AddComponent<ErrorMessageUI>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    #endregion

    #region UI Components
    [Header("UI References")]
    [SerializeField] private Canvas errorCanvas;
    [SerializeField] private GameObject errorPopupPrefab;
    [SerializeField] private GameObject errorToastPrefab;
    [SerializeField] private GameObject errorBannerPrefab;
    [SerializeField] private Transform popupParent;
    [SerializeField] private Transform toastParent;
    [SerializeField] private Transform bannerParent;

    [Header("Error Message Settings")]
    [SerializeField] private float toastDuration = 3f;
    [SerializeField] private float bannerDuration = 5f;
    [SerializeField] private int maxToastMessages = 3;
    [SerializeField] private Color lowSeverityColor = Color.yellow;
    [SerializeField] private Color mediumSeverityColor = Color.orange;
    [SerializeField] private Color highSeverityColor = Color.red;
    [SerializeField] private Color criticalSeverityColor = Color.magenta;
    #endregion

    #region Private Fields
    private readonly Queue<ErrorMessage> _messageQueue = new();
    private readonly List<GameObject> _activeToasts = new();
    private readonly Dictionary<ErrorSeverity, ErrorMessageTemplate> _messageTemplates = new();
    private bool _isInitialized = false;
    private Coroutine _messageProcessingCoroutine;
    #endregion

    #region Properties
    /// <summary>
    /// 활성 토스트 메시지 수
    /// </summary>
    public int ActiveToastCount => _activeToasts.Count;
    
    /// <summary>
    /// 대기 중인 메시지 수
    /// </summary>
    public int QueuedMessageCount => _messageQueue.Count;
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
    }

    private void Start()
    {
        InitializeErrorMessageUI();
        SubscribeToErrorEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromErrorEvents();
        
        if (_messageProcessingCoroutine != null)
        {
            StopCoroutine(_messageProcessingCoroutine);
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 오류 메시지 UI 초기화
    /// </summary>
    private void InitializeErrorMessageUI()
    {
        SetupCanvas();
        SetupMessageTemplates();
        CreateUIContainers();
        
        _messageProcessingCoroutine = StartCoroutine(ProcessMessageQueue());
        _isInitialized = true;
        
        Debug.Log("[ErrorMessageUI] Error message UI system initialized");
    }

    /// <summary>
    /// 캔버스 설정
    /// </summary>
    private void SetupCanvas()
    {
        if (errorCanvas == null)
        {
            GameObject canvasGO = new GameObject("ErrorMessageCanvas");
            canvasGO.transform.SetParent(transform);
            
            errorCanvas = canvasGO.AddComponent<Canvas>();
            errorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            errorCanvas.sortingOrder = 1000; // 최상위 표시
            
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
        }
    }

    /// <summary>
    /// UI 컨테이너 생성
    /// </summary>
    private void CreateUIContainers()
    {
        if (popupParent == null)
        {
            GameObject popupContainer = new GameObject("PopupContainer");
            popupContainer.transform.SetParent(errorCanvas.transform, false);
            popupParent = popupContainer.transform;
            
            var rect = popupContainer.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        if (toastParent == null)
        {
            GameObject toastContainer = new GameObject("ToastContainer");
            toastContainer.transform.SetParent(errorCanvas.transform, false);
            toastParent = toastContainer.transform;
            
            var rect = toastContainer.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -50);
        }

        if (bannerParent == null)
        {
            GameObject bannerContainer = new GameObject("BannerContainer");
            bannerContainer.transform.SetParent(errorCanvas.transform, false);
            bannerParent = bannerContainer.transform;
            
            var rect = bannerContainer.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// 메시지 템플릿 설정
    /// </summary>
    private void SetupMessageTemplates()
    {
        _messageTemplates[ErrorSeverity.Low] = new ErrorMessageTemplate
        {
            Title = "알림",
            DisplayType = ErrorDisplayType.Toast,
            Duration = toastDuration,
            Color = lowSeverityColor,
            ShowRetryButton = false
        };

        _messageTemplates[ErrorSeverity.Medium] = new ErrorMessageTemplate
        {
            Title = "경고",
            DisplayType = ErrorDisplayType.Toast,
            Duration = toastDuration + 2f,
            Color = mediumSeverityColor,
            ShowRetryButton = false
        };

        _messageTemplates[ErrorSeverity.High] = new ErrorMessageTemplate
        {
            Title = "오류",
            DisplayType = ErrorDisplayType.Popup,
            Duration = 0f, // 수동 닫기
            Color = highSeverityColor,
            ShowRetryButton = true
        };

        _messageTemplates[ErrorSeverity.Critical] = new ErrorMessageTemplate
        {
            Title = "심각한 오류",
            DisplayType = ErrorDisplayType.Popup,
            Duration = 0f, // 수동 닫기
            Color = criticalSeverityColor,
            ShowRetryButton = true
        };
    }

    /// <summary>
    /// 오류 이벤트 구독
    /// </summary>
    private void SubscribeToErrorEvents()
    {
        GlobalErrorHandler.OnShowErrorMessage += ShowErrorMessage;
    }

    /// <summary>
    /// 오류 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromErrorEvents()
    {
        GlobalErrorHandler.OnShowErrorMessage -= ShowErrorMessage;
    }
    #endregion

    #region Message Display
    /// <summary>
    /// 오류 메시지 표시 (GlobalErrorHandler에서 호출)
    /// </summary>
    private void ShowErrorMessage(string message, ErrorSeverity severity)
    {
        ShowMessage(message, severity);
    }

    /// <summary>
    /// 메시지 표시
    /// </summary>
    public void ShowMessage(string message, ErrorSeverity severity, Action onRetry = null, Action onDismiss = null)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[ErrorMessageUI] UI not initialized, queuing message");
        }

        var errorMessage = new ErrorMessage
        {
            Message = message,
            Severity = severity,
            OnRetry = onRetry,
            OnDismiss = onDismiss,
            Timestamp = DateTime.Now
        };

        _messageQueue.Enqueue(errorMessage);
    }

    /// <summary>
    /// 토스트 메시지 표시
    /// </summary>
    public void ShowToast(string message, float duration = 0f)
    {
        ShowMessage(message, ErrorSeverity.Low);
    }

    /// <summary>
    /// 팝업 메시지 표시
    /// </summary>
    public void ShowPopup(string message, string title = null, Action onRetry = null, Action onDismiss = null)
    {
        var errorMessage = new ErrorMessage
        {
            Message = message,
            Title = title,
            Severity = ErrorSeverity.High,
            OnRetry = onRetry,
            OnDismiss = onDismiss,
            Timestamp = DateTime.Now
        };

        _messageQueue.Enqueue(errorMessage);
    }

    /// <summary>
    /// 배너 메시지 표시
    /// </summary>
    public void ShowBanner(string message, ErrorSeverity severity = ErrorSeverity.Medium)
    {
        var errorMessage = new ErrorMessage
        {
            Message = message,
            Severity = severity,
            DisplayType = ErrorDisplayType.Banner,
            Timestamp = DateTime.Now
        };

        _messageQueue.Enqueue(errorMessage);
    }
    #endregion

    #region Message Processing
    /// <summary>
    /// 메시지 큐 처리 코루틴
    /// </summary>
    private IEnumerator ProcessMessageQueue()
    {
        while (true)
        {
            if (_messageQueue.Count > 0 && _isInitialized)
            {
                var errorMessage = _messageQueue.Dequeue();
                yield return StartCoroutine(DisplayMessage(errorMessage));
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// 실제 메시지 표시
    /// </summary>
    private IEnumerator DisplayMessage(ErrorMessage errorMessage)
    {
        var template = _messageTemplates[errorMessage.Severity];
        ErrorDisplayType displayType = errorMessage.DisplayType ?? template.DisplayType;

        switch (displayType)
        {
            case ErrorDisplayType.Toast:
                yield return StartCoroutine(ShowToastMessage(errorMessage, template));
                break;
                
            case ErrorDisplayType.Popup:
                yield return StartCoroutine(ShowPopupMessage(errorMessage, template));
                break;
                
            case ErrorDisplayType.Banner:
                yield return StartCoroutine(ShowBannerMessage(errorMessage, template));
                break;
        }
    }

    /// <summary>
    /// 토스트 메시지 표시
    /// </summary>
    private IEnumerator ShowToastMessage(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        // 최대 토스트 수 제한
        while (_activeToasts.Count >= maxToastMessages)
        {
            if (_activeToasts.Count > 0)
            {
                var oldestToast = _activeToasts[0];
                _activeToasts.RemoveAt(0);
                if (oldestToast != null)
                {
                    Destroy(oldestToast);
                }
            }
            yield return null;
        }

        GameObject toastGO = CreateToastUI(errorMessage, template);
        if (toastGO != null)
        {
            _activeToasts.Add(toastGO);
            
            // 애니메이션
            yield return StartCoroutine(AnimateToastIn(toastGO));
            
            // 대기
            float duration = errorMessage.Duration > 0 ? errorMessage.Duration : template.Duration;
            yield return new WaitForSeconds(duration);
            
            // 애니메이션 아웃
            yield return StartCoroutine(AnimateToastOut(toastGO));
            
            // 제거
            _activeToasts.Remove(toastGO);
            if (toastGO != null)
            {
                Destroy(toastGO);
            }
            
            errorMessage.OnDismiss?.Invoke();
        }
    }

    /// <summary>
    /// 팝업 메시지 표시
    /// </summary>
    private IEnumerator ShowPopupMessage(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        GameObject popupGO = CreatePopupUI(errorMessage, template);
        if (popupGO != null)
        {
            yield return StartCoroutine(AnimatePopupIn(popupGO));
            
            // 팝업은 수동으로 닫을 때까지 대기
            yield return new WaitUntil(() => popupGO == null);
        }
    }

    /// <summary>
    /// 배너 메시지 표시
    /// </summary>
    private IEnumerator ShowBannerMessage(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        GameObject bannerGO = CreateBannerUI(errorMessage, template);
        if (bannerGO != null)
        {
            yield return StartCoroutine(AnimateBannerIn(bannerGO));
            
            float duration = errorMessage.Duration > 0 ? errorMessage.Duration : bannerDuration;
            yield return new WaitForSeconds(duration);
            
            yield return StartCoroutine(AnimateBannerOut(bannerGO));
            
            if (bannerGO != null)
            {
                Destroy(bannerGO);
            }
            
            errorMessage.OnDismiss?.Invoke();
        }
    }
    #endregion

    #region UI Creation
    /// <summary>
    /// 토스트 UI 생성
    /// </summary>
    private GameObject CreateToastUI(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        GameObject toastGO = new GameObject("ErrorToast");
        toastGO.transform.SetParent(toastParent, false);
        
        // RectTransform 설정
        var rect = toastGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 60);
        
        // 배경 이미지
        var bg = toastGO.AddComponent<Image>();
        bg.color = new Color(template.Color.r, template.Color.g, template.Color.b, 0.8f);
        
        // 텍스트
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(toastGO.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        var text = textGO.AddComponent<Text>();
        text.text = errorMessage.Message;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        
        return toastGO;
    }

    /// <summary>
    /// 팝업 UI 생성
    /// </summary>
    private GameObject CreatePopupUI(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        GameObject popupGO = new GameObject("ErrorPopup");
        popupGO.transform.SetParent(popupParent, false);
        
        // RectTransform 설정
        var rect = popupGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        
        // 배경 (반투명)
        var bg = popupGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.5f);
        
        // 메인 팝업 패널
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(popupGO.transform, false);
        
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(350, 200);
        panelRect.anchoredPosition = Vector2.zero;
        
        var panelBg = panelGO.AddComponent<Image>();
        panelBg.color = Color.white;
        
        // 타이틀
        CreatePopupText(panelGO, "Title", errorMessage.Title ?? template.Title, 18, new Vector2(0, 50));
        
        // 메시지
        CreatePopupText(panelGO, "Message", errorMessage.Message, 14, Vector2.zero);
        
        // 버튼들
        CreatePopupButtons(panelGO, errorMessage, template);
        
        return popupGO;
    }

    /// <summary>
    /// 팝업 텍스트 생성
    /// </summary>
    private void CreatePopupText(GameObject parent, string name, string content, int fontSize, Vector2 position)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(300, fontSize == 18 ? 30 : 60);
        textRect.anchoredPosition = position;
        
        var text = textGO.AddComponent<Text>();
        text.text = content;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
    }

    /// <summary>
    /// 팝업 버튼들 생성
    /// </summary>
    private void CreatePopupButtons(GameObject parent, ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        float buttonY = -70f;
        float buttonWidth = template.ShowRetryButton ? 130f : 200f;
        
        // 확인 버튼
        CreatePopupButton(parent, "확인", new Vector2(template.ShowRetryButton ? 60f : 0f, buttonY), 
                         new Vector2(buttonWidth, 40f), () =>
        {
            errorMessage.OnDismiss?.Invoke();
            Destroy(parent.transform.parent.gameObject);
        });
        
        // 재시도 버튼
        if (template.ShowRetryButton)
        {
            CreatePopupButton(parent, "재시도", new Vector2(-60f, buttonY), 
                             new Vector2(buttonWidth, 40f), () =>
            {
                errorMessage.OnRetry?.Invoke();
                Destroy(parent.transform.parent.gameObject);
            });
        }
    }

    /// <summary>
    /// 팝업 버튼 생성
    /// </summary>
    private void CreatePopupButton(GameObject parent, string text, Vector2 position, Vector2 size, Action onClick)
    {
        GameObject buttonGO = new GameObject($"Button_{text}");
        buttonGO.transform.SetParent(parent.transform, false);
        
        var rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        
        var button = buttonGO.AddComponent<Button>();
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.8f, 0.8f, 0.8f);
        
        // 버튼 텍스트
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        var buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.color = Color.black;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 14;
        
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    /// <summary>
    /// 배너 UI 생성
    /// </summary>
    private GameObject CreateBannerUI(ErrorMessage errorMessage, ErrorMessageTemplate template)
    {
        GameObject bannerGO = new GameObject("ErrorBanner");
        bannerGO.transform.SetParent(bannerParent, false);
        
        var rect = bannerGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(0, 50);
        rect.anchoredPosition = new Vector2(0, -25);
        
        var bg = bannerGO.AddComponent<Image>();
        bg.color = template.Color;
        
        // 텍스트
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(bannerGO.transform, false);
        
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        var text = textGO.AddComponent<Text>();
        text.text = errorMessage.Message;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        
        return bannerGO;
    }
    #endregion

    #region Animations
    /// <summary>
    /// 토스트 인 애니메이션
    /// </summary>
    private IEnumerator AnimateToastIn(GameObject toast)
    {
        toast.transform.localScale = Vector3.zero;
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            toast.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }
        
        toast.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 토스트 아웃 애니메이션
    /// </summary>
    private IEnumerator AnimateToastOut(GameObject toast)
    {
        float elapsed = 0f;
        float duration = 0.2f;
        Vector3 startScale = toast.transform.localScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            toast.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
    }

    /// <summary>
    /// 팝업 인 애니메이션
    /// </summary>
    private IEnumerator AnimatePopupIn(GameObject popup)
    {
        var canvasGroup = popup.GetComponent<CanvasGroup>() ?? popup.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 배너 인 애니메이션
    /// </summary>
    private IEnumerator AnimateBannerIn(GameObject banner)
    {
        var rect = banner.GetComponent<RectTransform>();
        Vector2 startPos = new Vector2(0, 50);
        Vector2 endPos = new Vector2(0, -25);
        
        rect.anchoredPosition = startPos;
        
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        rect.anchoredPosition = endPos;
    }

    /// <summary>
    /// 배너 아웃 애니메이션
    /// </summary>
    private IEnumerator AnimateBannerOut(GameObject banner)
    {
        var rect = banner.GetComponent<RectTransform>();
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = new Vector2(0, 50);
        
        float elapsed = 0f;
        float duration = 0.2f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 모든 활성 메시지 클리어
    /// </summary>
    public void ClearAllMessages()
    {
        // 토스트 메시지 제거
        foreach (var toast in _activeToasts)
        {
            if (toast != null)
                Destroy(toast);
        }
        _activeToasts.Clear();
        
        // 큐 클리어
        _messageQueue.Clear();
        
        Debug.Log("[ErrorMessageUI] All messages cleared");
    }

    /// <summary>
    /// 특정 심각도의 메시지만 클리어
    /// </summary>
    public void ClearMessagesBySeverity(ErrorSeverity severity)
    {
        var filteredQueue = new Queue<ErrorMessage>();
        while (_messageQueue.Count > 0)
        {
            var message = _messageQueue.Dequeue();
            if (message.Severity != severity)
            {
                filteredQueue.Enqueue(message);
            }
        }
        
        while (filteredQueue.Count > 0)
        {
            _messageQueue.Enqueue(filteredQueue.Dequeue());
        }
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 오류 메시지 데이터
/// </summary>
[Serializable]
public class ErrorMessage
{
    public string Message { get; set; }
    public string Title { get; set; }
    public ErrorSeverity Severity { get; set; }
    public ErrorDisplayType? DisplayType { get; set; }
    public float Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public Action OnRetry { get; set; }
    public Action OnDismiss { get; set; }
}

/// <summary>
/// 오류 메시지 템플릿
/// </summary>
[Serializable]
public class ErrorMessageTemplate
{
    public string Title { get; set; }
    public ErrorDisplayType DisplayType { get; set; }
    public float Duration { get; set; }
    public Color Color { get; set; }
    public bool ShowRetryButton { get; set; }
}

/// <summary>
/// 오류 표시 타입
/// </summary>
public enum ErrorDisplayType
{
    Toast,
    Popup,
    Banner
}
#endregion