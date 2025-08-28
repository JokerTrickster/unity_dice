using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 전환 관리자
/// 로그인 플로우에 따른 화면 전환과 UI 상태 관리를 담당합니다.
/// </summary>
public class ScreenTransitionManager : MonoBehaviour
{
    #region Singleton
    private static ScreenTransitionManager _instance;
    public static ScreenTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ScreenTransitionManager");
                _instance = go.AddComponent<ScreenTransitionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// 화면 전환이 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action<ScreenType, ScreenType> OnScreenTransitionStarted;
    
    /// <summary>
    /// 화면 전환이 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action<ScreenType> OnScreenTransitionCompleted;
    
    /// <summary>
    /// 화면 전환이 실패할 때 발생하는 이벤트
    /// </summary>
    public static event Action<ScreenType, string> OnScreenTransitionFailed;
    #endregion

    #region Configuration
    [Header("Screen Configuration")]
    [SerializeField] private bool enableTransitionAnimation = true;
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Screen References")]
    [SerializeField] private Canvas splashScreen;
    [SerializeField] private Canvas loginScreen;
    [SerializeField] private Canvas nicknameScreen;
    [SerializeField] private Canvas mainMenuScreen;
    [SerializeField] private Canvas errorScreen;
    [SerializeField] private Canvas loadingScreen;
    #endregion

    #region Private Fields
    private ScreenType _currentScreen = ScreenType.None;
    private ScreenType _previousScreen = ScreenType.None;
    private readonly Dictionary<ScreenType, Canvas> _screenCanvases = new();
    private readonly Dictionary<LoginState, ScreenType> _stateToScreenMap = new();
    private bool _isTransitioning = false;
    private readonly CanvasGroup _transitionOverlay;
    #endregion

    #region Properties
    /// <summary>
    /// 현재 활성 화면
    /// </summary>
    public ScreenType CurrentScreen => _currentScreen;
    
    /// <summary>
    /// 이전 화면
    /// </summary>
    public ScreenType PreviousScreen => _previousScreen;
    
    /// <summary>
    /// 전환 진행 중 여부
    /// </summary>
    public bool IsTransitioning => _isTransitioning;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 전환 오버레이 생성
            CreateTransitionOverlay();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeScreenManager();
        SubscribeToStateChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromStateChanges();
        OnScreenTransitionStarted = null;
        OnScreenTransitionCompleted = null;
        OnScreenTransitionFailed = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 화면 관리자 초기화
    /// </summary>
    private void InitializeScreenManager()
    {
        SetupScreenCanvases();
        SetupStateToScreenMapping();
        
        // 초기 화면 설정
        ShowScreen(ScreenType.Splash, false);
        
        Debug.Log("[ScreenTransitionManager] Initialized");
    }

    /// <summary>
    /// 화면 캔버스 설정
    /// </summary>
    private void SetupScreenCanvases()
    {
        _screenCanvases.Clear();
        
        // 씬에서 캔버스들을 찾아서 등록
        RegisterCanvas(ScreenType.Splash, splashScreen ?? FindCanvasByName("SplashScreen"));
        RegisterCanvas(ScreenType.Login, loginScreen ?? FindCanvasByName("LoginScreen"));
        RegisterCanvas(ScreenType.NicknameSetup, nicknameScreen ?? FindCanvasByName("NicknameScreen"));
        RegisterCanvas(ScreenType.MainMenu, mainMenuScreen ?? FindCanvasByName("MainMenuScreen"));
        RegisterCanvas(ScreenType.Error, errorScreen ?? FindCanvasByName("ErrorScreen"));
        RegisterCanvas(ScreenType.Loading, loadingScreen ?? FindCanvasByName("LoadingScreen"));
        
        Debug.Log($"[ScreenTransitionManager] Registered {_screenCanvases.Count} screen canvases");
    }

    /// <summary>
    /// 캔버스 등록
    /// </summary>
    private void RegisterCanvas(ScreenType screenType, Canvas canvas)
    {
        if (canvas != null)
        {
            _screenCanvases[screenType] = canvas;
            canvas.gameObject.SetActive(false); // 초기에는 모든 화면 비활성화
        }
        else
        {
            Debug.LogWarning($"[ScreenTransitionManager] Canvas not found for screen: {screenType}");
        }
    }

    /// <summary>
    /// 이름으로 캔버스 찾기
    /// </summary>
    private Canvas FindCanvasByName(string name)
    {
        GameObject found = GameObject.Find(name);
        return found?.GetComponent<Canvas>();
    }

    /// <summary>
    /// 상태-화면 매핑 설정
    /// </summary>
    private void SetupStateToScreenMapping()
    {
        _stateToScreenMap.Clear();
        
        _stateToScreenMap[LoginState.NotInitialized] = ScreenType.Splash;
        _stateToScreenMap[LoginState.Initializing] = ScreenType.Splash;
        _stateToScreenMap[LoginState.Ready] = ScreenType.Login;
        _stateToScreenMap[LoginState.Authenticating] = ScreenType.Loading;
        _stateToScreenMap[LoginState.Success] = ScreenType.Loading;
        _stateToScreenMap[LoginState.Failed] = ScreenType.Login;
        _stateToScreenMap[LoginState.NicknameSetup] = ScreenType.NicknameSetup;
        _stateToScreenMap[LoginState.Complete] = ScreenType.MainMenu;
        _stateToScreenMap[LoginState.Error] = ScreenType.Error;
        
        Debug.Log($"[ScreenTransitionManager] Setup {_stateToScreenMap.Count} state-to-screen mappings");
    }

    /// <summary>
    /// 전환 오버레이 생성
    /// </summary>
    private void CreateTransitionOverlay()
    {
        GameObject overlayGO = new GameObject("TransitionOverlay");
        overlayGO.transform.SetParent(transform);
        
        Canvas overlayCanvas = overlayGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 9999; // 최상위
        
        var canvasGroup = overlayGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        // 검은 배경 추가
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(overlayGO.transform, false);
        
        var rectTransform = bg.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        var image = bg.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
    }
    #endregion

    #region State Integration
    /// <summary>
    /// 상태 변경 이벤트 구독
    /// </summary>
    private void SubscribeToStateChanges()
    {
        LoginFlowStateMachine.OnStateChanged += OnLoginStateChanged;
    }

    /// <summary>
    /// 상태 변경 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromStateChanges()
    {
        LoginFlowStateMachine.OnStateChanged -= OnLoginStateChanged;
    }

    /// <summary>
    /// 로그인 상태 변경 처리
    /// </summary>
    private void OnLoginStateChanged(LoginState previousState, LoginState newState)
    {
        if (_stateToScreenMap.ContainsKey(newState))
        {
            ScreenType targetScreen = _stateToScreenMap[newState];
            ShowScreen(targetScreen);
        }
        else
        {
            Debug.LogWarning($"[ScreenTransitionManager] No screen mapping for state: {newState}");
        }
    }
    #endregion

    #region Screen Management
    /// <summary>
    /// 화면 표시
    /// </summary>
    public void ShowScreen(ScreenType screenType, bool animated = true)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[ScreenTransitionManager] Cannot show screen {screenType} - transition in progress");
            return;
        }

        if (!_screenCanvases.ContainsKey(screenType))
        {
            string errorMessage = $"Screen canvas not found: {screenType}";
            Debug.LogError($"[ScreenTransitionManager] {errorMessage}");
            OnScreenTransitionFailed?.Invoke(screenType, errorMessage);
            return;
        }

        if (animated && enableTransitionAnimation)
        {
            StartCoroutine(AnimatedScreenTransition(screenType));
        }
        else
        {
            PerformScreenTransition(screenType);
        }
    }

    /// <summary>
    /// 애니메이션이 있는 화면 전환
    /// </summary>
    private IEnumerator AnimatedScreenTransition(ScreenType targetScreen)
    {
        _isTransitioning = true;
        ScreenType fromScreen = _currentScreen;
        
        OnScreenTransitionStarted?.Invoke(fromScreen, targetScreen);
        
        // 페이드 아웃
        yield return StartCoroutine(FadeTransition(0f, 1f));
        
        // 화면 전환
        PerformScreenTransition(targetScreen);
        
        // 짧은 대기
        yield return new WaitForSeconds(0.1f);
        
        // 페이드 인
        yield return StartCoroutine(FadeTransition(1f, 0f));
        
        _isTransitioning = false;
        OnScreenTransitionCompleted?.Invoke(targetScreen);
    }

    /// <summary>
    /// 페이드 전환 애니메이션
    /// </summary>
    private IEnumerator FadeTransition(float from, float to)
    {
        CanvasGroup overlay = GetComponent<CanvasGroup>();
        if (overlay == null) yield break;
        
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            overlay.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        
        overlay.alpha = to;
    }

    /// <summary>
    /// 실제 화면 전환 수행
    /// </summary>
    private void PerformScreenTransition(ScreenType targetScreen)
    {
        // 이전 화면 비활성화
        if (_currentScreen != ScreenType.None && _screenCanvases.ContainsKey(_currentScreen))
        {
            _screenCanvases[_currentScreen].gameObject.SetActive(false);
        }

        // 새 화면 활성화
        if (_screenCanvases.ContainsKey(targetScreen))
        {
            _screenCanvases[targetScreen].gameObject.SetActive(true);
            
            _previousScreen = _currentScreen;
            _currentScreen = targetScreen;
            
            Debug.Log($"[ScreenTransitionManager] Screen transition: {_previousScreen} -> {_currentScreen}");
        }
    }

    /// <summary>
    /// 이전 화면으로 돌아가기
    /// </summary>
    public void ShowPreviousScreen()
    {
        if (_previousScreen != ScreenType.None)
        {
            ShowScreen(_previousScreen);
        }
        else
        {
            Debug.LogWarning("[ScreenTransitionManager] No previous screen to return to");
        }
    }

    /// <summary>
    /// 특정 화면이 활성화되어 있는지 확인
    /// </summary>
    public bool IsScreenActive(ScreenType screenType)
    {
        return _currentScreen == screenType;
    }

    /// <summary>
    /// 화면 캔버스 가져오기
    /// </summary>
    public Canvas GetScreenCanvas(ScreenType screenType)
    {
        return _screenCanvases.ContainsKey(screenType) ? _screenCanvases[screenType] : null;
    }
    #endregion

    #region Manual Control
    /// <summary>
    /// 수동 화면 등록 (런타임에 화면 추가할 때)
    /// </summary>
    public void RegisterScreen(ScreenType screenType, Canvas canvas)
    {
        if (canvas != null)
        {
            _screenCanvases[screenType] = canvas;
            canvas.gameObject.SetActive(false);
            Debug.Log($"[ScreenTransitionManager] Manually registered screen: {screenType}");
        }
    }

    /// <summary>
    /// 화면 등록 해제
    /// </summary>
    public void UnregisterScreen(ScreenType screenType)
    {
        if (_screenCanvases.ContainsKey(screenType))
        {
            _screenCanvases.Remove(screenType);
            Debug.Log($"[ScreenTransitionManager] Unregistered screen: {screenType}");
        }
    }

    /// <summary>
    /// 전환 설정 업데이트
    /// </summary>
    public void UpdateTransitionSettings(bool enableAnimation, float duration = 0.3f)
    {
        enableTransitionAnimation = enableAnimation;
        transitionDuration = duration;
    }
    #endregion
}

#region Enums
/// <summary>
/// 화면 타입
/// </summary>
public enum ScreenType
{
    None,
    Splash,
    Login,
    NicknameSetup,
    MainMenu,
    Error,
    Loading
}
#endregion