using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 페이지 캔버스 컨트롤러
/// 캔버스 레벨에서의 반응형 레이아웃과 터치 영역 관리를 담당합니다.
/// MainPageScreen과 연동하여 전체 화면 구성을 제어합니다.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class MainPageCanvas : MonoBehaviour
{
    #region Canvas Configuration
    [Header("Canvas Settings")]
    [SerializeField] private bool setupCanvasOnAwake = true;
    [SerializeField] private float referencePixelsPerUnit = 100f;
    
    [Header("Safe Area Settings")]
    [SerializeField] private bool enableSafeArea = true;
    [SerializeField] private RectTransform safeAreaContainer;
    
    [Header("Touch Settings")]
    [SerializeField] private bool enableTouchFeedback = true;
    [SerializeField] private float touchFeedbackScale = 0.95f;
    [SerializeField] private float touchAnimationDuration = 0.1f;
    #endregion

    #region Component References
    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private GraphicRaycaster _graphicRaycaster;
    private MainPageScreen _mainPageScreen;
    
    // Screen safe area cache
    private Rect _lastSafeArea;
    private Vector2 _lastScreenSize;
    #endregion

    #region Properties
    /// <summary>
    /// 캔버스 컴포넌트
    /// </summary>
    public Canvas Canvas => _canvas;
    
    /// <summary>
    /// 캔버스 스케일러 컴포넌트
    /// </summary>
    public CanvasScaler CanvasScaler => _canvasScaler;
    
    /// <summary>
    /// Safe Area가 활성화된 상태
    /// </summary>
    public bool IsSafeAreaEnabled => enableSafeArea;
    
    /// <summary>
    /// 현재 Safe Area 영역
    /// </summary>
    public Rect CurrentSafeArea => Screen.safeArea;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        CacheComponents();
        
        if (setupCanvasOnAwake)
        {
            SetupCanvas();
        }
    }

    private void Start()
    {
        _mainPageScreen = GetComponentInChildren<MainPageScreen>();
        
        if (enableSafeArea)
        {
            SetupSafeArea();
        }
    }

    private void Update()
    {
        if (enableSafeArea)
        {
            CheckSafeAreaChanges();
        }
    }
    #endregion

    #region Canvas Setup
    /// <summary>
    /// 컴포넌트 캐싱
    /// </summary>
    private void CacheComponents()
    {
        _canvas = GetComponent<Canvas>();
        _canvasScaler = GetComponent<CanvasScaler>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
        
        // 기본 컴포넌트가 없으면 추가
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();
            
        if (_canvasScaler == null)
            _canvasScaler = gameObject.AddComponent<CanvasScaler>();
            
        if (_graphicRaycaster == null)
            _graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 캔버스 기본 설정
    /// </summary>
    private void SetupCanvas()
    {
        // Canvas 설정
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.pixelPerfect = false;
        _canvas.sortingOrder = 0;
        _canvas.targetDisplay = 0;
        
        // CanvasScaler 설정 (MainPageScreen에서 더 세부적으로 조정됨)
        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _canvasScaler.matchWidthOrHeight = 0.5f;
        _canvasScaler.referencePixelsPerUnit = referencePixelsPerUnit;
        
        // GraphicRaycaster 설정
        _graphicRaycaster.ignoreReversedGraphics = true;
        _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        
        Debug.Log("[MainPageCanvas] Canvas setup completed");
    }
    #endregion

    #region Safe Area Management
    /// <summary>
    /// Safe Area 초기 설정
    /// </summary>
    private void SetupSafeArea()
    {
        if (safeAreaContainer == null)
        {
            // Safe Area 컨테이너가 없으면 찾아보거나 생성
            safeAreaContainer = transform.Find("SafeAreaContainer")?.GetComponent<RectTransform>();
            
            if (safeAreaContainer == null)
            {
                Debug.LogWarning("[MainPageCanvas] SafeAreaContainer not found, safe area will not be applied");
                enableSafeArea = false;
                return;
            }
        }
        
        ApplySafeArea();
    }

    /// <summary>
    /// Safe Area 변경 감지
    /// </summary>
    private void CheckSafeAreaChanges()
    {
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        
        if (_lastSafeArea != safeArea || _lastScreenSize != screenSize)
        {
            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            ApplySafeArea();
        }
    }

    /// <summary>
    /// Safe Area 적용
    /// </summary>
    private void ApplySafeArea()
    {
        if (safeAreaContainer == null) return;
        
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        
        // Safe Area를 정규화된 좌표로 변환
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;
        
        // Safe Area 컨테이너에 적용
        safeAreaContainer.anchorMin = anchorMin;
        safeAreaContainer.anchorMax = anchorMax;
        safeAreaContainer.offsetMin = Vector2.zero;
        safeAreaContainer.offsetMax = Vector2.zero;
        
        Debug.Log($"[MainPageCanvas] Applied safe area: {safeArea} on screen {screenSize}");
    }
    #endregion

    #region Touch Feedback
    /// <summary>
    /// 터치 피드백 효과 적용
    /// </summary>
    public void ApplyTouchFeedback(Transform target, bool pressed)
    {
        if (!enableTouchFeedback || target == null) return;
        
        Vector3 targetScale = pressed ? Vector3.one * touchFeedbackScale : Vector3.one;
        
        // 간단한 스케일 애니메이션 (Coroutine 대신 직접 적용)
        target.localScale = targetScale;
    }

    /// <summary>
    /// 터치 가능한 UI 요소에 최소 크기 적용
    /// </summary>
    public void EnsureMinimumTouchSize(RectTransform target, float minSize = 44f)
    {
        if (target == null) return;
        
        Vector2 sizeDelta = target.sizeDelta;
        sizeDelta.x = Mathf.Max(sizeDelta.x, minSize);
        sizeDelta.y = Mathf.Max(sizeDelta.y, minSize);
        target.sizeDelta = sizeDelta;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 캔버스 설정 업데이트
    /// </summary>
    public void UpdateCanvasSettings(Vector2 referenceResolution, float matchWidthOrHeight = 0.5f)
    {
        if (_canvasScaler != null)
        {
            _canvasScaler.referenceResolution = referenceResolution;
            _canvasScaler.matchWidthOrHeight = matchWidthOrHeight;
            
            Debug.Log($"[MainPageCanvas] Updated canvas settings: {referenceResolution}, match: {matchWidthOrHeight}");
        }
    }

    /// <summary>
    /// Safe Area 강제 업데이트
    /// </summary>
    public void RefreshSafeArea()
    {
        if (enableSafeArea)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// 터치 설정 업데이트
    /// </summary>
    public void UpdateTouchSettings(bool enableFeedback, float feedbackScale = 0.95f)
    {
        enableTouchFeedback = enableFeedback;
        touchFeedbackScale = feedbackScale;
    }

    /// <summary>
    /// 캔버스 상태 정보 반환
    /// </summary>
    public MainPageCanvasStatus GetCanvasStatus()
    {
        return new MainPageCanvasStatus
        {
            RenderMode = _canvas?.renderMode ?? RenderMode.ScreenSpaceOverlay,
            ReferenceResolution = _canvasScaler?.referenceResolution ?? Vector2.zero,
            MatchWidthOrHeight = _canvasScaler?.matchWidthOrHeight ?? 0.5f,
            CurrentSafeArea = Screen.safeArea,
            ScreenSize = new Vector2(Screen.width, Screen.height),
            IsSafeAreaEnabled = enableSafeArea,
            IsTouchFeedbackEnabled = enableTouchFeedback
        };
    }
    #endregion

    #region Validation
    private void OnValidate()
    {
        // Inspector에서 값이 변경될 때 검증
        touchFeedbackScale = Mathf.Clamp01(touchFeedbackScale);
        touchAnimationDuration = Mathf.Max(0.05f, touchAnimationDuration);
        referencePixelsPerUnit = Mathf.Max(1f, referencePixelsPerUnit);
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 메인 페이지 캔버스 상태 정보
/// </summary>
[System.Serializable]
public class MainPageCanvasStatus
{
    public RenderMode RenderMode;
    public Vector2 ReferenceResolution;
    public float MatchWidthOrHeight;
    public Rect CurrentSafeArea;
    public Vector2 ScreenSize;
    public bool IsSafeAreaEnabled;
    public bool IsTouchFeedbackEnabled;
}
#endregion