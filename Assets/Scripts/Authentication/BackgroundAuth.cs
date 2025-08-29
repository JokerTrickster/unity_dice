using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 백그라운드 인증 관리자
/// 앱 시작 시, 포커스 복귀 시 seamless 자동 인증을 처리합니다.
/// </summary>
public class BackgroundAuth : MonoBehaviour
{
    #region Singleton
    public static BackgroundAuth Instance { get; private set; }
    #endregion

    #region Configuration
    [Header("Background Auth Configuration")]
    [SerializeField] private bool enableBackgroundAuth = true;
    [SerializeField] private bool authenticateOnAppFocus = true;
    [SerializeField] private bool showSplashDuringAuth = true;
    [SerializeField] private float maxAuthenticationTime = 10f;
    [SerializeField] private float appFocusDelay = 0.5f; // 포커스 복귀 후 대기 시간
    
    [Header("UI References")]
    [SerializeField] private GameObject splashScreen;
    [SerializeField] private CanvasGroup splashCanvasGroup;
    #endregion

    #region Properties
    /// <summary>
    /// 백그라운드 인증 진행 중 여부
    /// </summary>
    public bool IsAuthenticationInProgress { get; private set; }
    
    /// <summary>
    /// 마지막 인증 시도 시간
    /// </summary>
    public DateTime LastAuthenticationAttempt { get; private set; }
    
    /// <summary>
    /// 앱 포커스 상태
    /// </summary>
    public bool IsAppFocused { get; private set; }
    #endregion

    #region Events
    /// <summary>
    /// 백그라운드 인증 시작 이벤트
    /// </summary>
    public static event Action OnBackgroundAuthStarted;
    
    /// <summary>
    /// 백그라운드 인증 완료 이벤트
    /// </summary>
    public static event Action<AutoLoginResult> OnBackgroundAuthCompleted;
    
    /// <summary>
    /// 스플래시 화면 표시 이벤트
    /// </summary>
    public static event Action<bool> OnSplashScreenToggle;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton 패턴 구현
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("[BackgroundAuth] Background authentication system initialized");
    }

    private void Start()
    {
        InitializeBackgroundAuth();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        IsAppFocused = hasFocus;
        Debug.Log($"[BackgroundAuth] Application focus changed: {hasFocus}");
        
        if (hasFocus && authenticateOnAppFocus && !IsAuthenticationInProgress)
        {
            StartCoroutine(HandleAppFocusAuthentication());
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) // 앱이 다시 활성화됨
        {
            IsAppFocused = true;
            Debug.Log("[BackgroundAuth] Application resumed from pause");
            
            if (authenticateOnAppFocus && !IsAuthenticationInProgress)
            {
                StartCoroutine(HandleAppFocusAuthentication());
            }
        }
        else
        {
            IsAppFocused = false;
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 백그라운드 인증 시스템 초기화
    /// </summary>
    private void InitializeBackgroundAuth()
    {
        if (!enableBackgroundAuth)
        {
            Debug.Log("[BackgroundAuth] Background authentication disabled");
            return;
        }

        // 스플래시 화면 초기화
        InitializeSplashScreen();
        
        // 자동 로그인 이벤트 구독
        if (AutoLoginManager.Instance != null)
        {
            AutoLoginManager.Instance.OnAutoLoginCompleted += HandleAutoLoginCompleted;
            AutoLoginManager.Instance.OnAutoLoginFailed += HandleAutoLoginFailed;
        }

        // 앱 시작 시 자동 인증 실행
        StartCoroutine(PerformInitialAuthentication());
    }

    /// <summary>
    /// 스플래시 화면 초기화
    /// </summary>
    private void InitializeSplashScreen()
    {
        if (splashScreen == null)
        {
            // 런타임에서 기본 스플래시 화면 찾기
            var splashObject = GameObject.Find("SplashScreen");
            if (splashObject != null)
            {
                splashScreen = splashObject;
                splashCanvasGroup = splashScreen.GetComponent<CanvasGroup>();
            }
        }

        if (splashScreen != null)
        {
            splashScreen.SetActive(false);
            Debug.Log("[BackgroundAuth] Splash screen initialized");
        }
    }
    #endregion

    #region Authentication Flow
    /// <summary>
    /// 초기 인증 수행 (앱 시작 시)
    /// </summary>
    private IEnumerator PerformInitialAuthentication()
    {
        Debug.Log("[BackgroundAuth] Starting initial authentication");
        
        // 앱이 완전히 로드될 때까지 대기
        yield return new WaitForSeconds(0.5f);
        
        // 백그라운드 인증 실행
        yield return StartCoroutine(PerformBackgroundAuthentication());
    }

    /// <summary>
    /// 앱 포커스 복귀 시 인증 처리
    /// </summary>
    private IEnumerator HandleAppFocusAuthentication()
    {
        Debug.Log("[BackgroundAuth] Handling app focus authentication");
        
        // 포커스 복귀 후 짧은 대기
        yield return new WaitForSeconds(appFocusDelay);
        
        // 마지막 인증으로부터 충분한 시간이 지났는지 확인
        var timeSinceLastAuth = DateTime.UtcNow - LastAuthenticationAttempt;
        if (timeSinceLastAuth.TotalMinutes < 1) // 1분 이내 재인증 방지
        {
            Debug.Log("[BackgroundAuth] Skipping authentication - too recent");
            yield break;
        }
        
        // 백그라운드 인증 실행
        yield return StartCoroutine(PerformBackgroundAuthentication());
    }

    /// <summary>
    /// 백그라운드 인증 수행
    /// </summary>
    private IEnumerator PerformBackgroundAuthentication()
    {
        if (IsAuthenticationInProgress)
        {
            Debug.Log("[BackgroundAuth] Authentication already in progress");
            yield break;
        }

        IsAuthenticationInProgress = true;
        LastAuthenticationAttempt = DateTime.UtcNow;

        OnBackgroundAuthStarted?.Invoke();

        try
        {
            // 스플래시 화면 표시
            if (showSplashDuringAuth)
            {
                ShowSplashScreen(true);
            }

            // 자동 로그인 매니저가 준비될 때까지 대기
            yield return new WaitUntil(() => AutoLoginManager.Instance != null);

            Debug.Log("[BackgroundAuth] Starting auto-login process");
            
            // 자동 로그인 시작
            var autoLoginTask = AutoLoginManager.Instance.TryAutoLoginAsync();
            
            // 타임아웃 처리
            float elapsedTime = 0f;
            while (!autoLoginTask.IsCompleted && elapsedTime < maxAuthenticationTime)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            AutoLoginResult result;
            if (autoLoginTask.IsCompleted)
            {
                result = autoLoginTask.Result;
                Debug.Log($"[BackgroundAuth] Auto-login completed with result: {result}");
            }
            else
            {
                Debug.LogWarning("[BackgroundAuth] Auto-login timed out");
                result = AutoLoginResult.Unknown;
            }

            // 결과 처리
            HandleAuthenticationResult(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BackgroundAuth] Authentication failed with exception: {ex.Message}");
            HandleAuthenticationResult(AutoLoginResult.Unknown);
        }
        finally
        {
            IsAuthenticationInProgress = false;
        }
    }

    /// <summary>
    /// 인증 결과 처리
    /// </summary>
    /// <param name="result">자동 로그인 결과</param>
    private void HandleAuthenticationResult(AutoLoginResult result)
    {
        Debug.Log($"[BackgroundAuth] Handling authentication result: {result}");

        // 스플래시 화면 숨김 (약간의 지연 후)
        if (showSplashDuringAuth && splashScreen != null && splashScreen.activeInHierarchy)
        {
            StartCoroutine(HideSplashScreenDelayed(0.5f));
        }

        // 결과에 따른 화면 전환
        HandleScreenTransition(result);

        // 이벤트 발생
        OnBackgroundAuthCompleted?.Invoke(result);
    }

    /// <summary>
    /// 자동 로그인 완료 이벤트 핸들러
    /// </summary>
    /// <param name="success">성공 여부</param>
    private void HandleAutoLoginCompleted(bool success)
    {
        Debug.Log($"[BackgroundAuth] Auto-login completed: {success}");
        
        if (success)
        {
            HandleAuthenticationResult(AutoLoginResult.Success);
        }
    }

    /// <summary>
    /// 자동 로그인 실패 이벤트 핸들러
    /// </summary>
    /// <param name="errorMessage">오류 메시지</param>
    private void HandleAutoLoginFailed(string errorMessage)
    {
        Debug.LogWarning($"[BackgroundAuth] Auto-login failed: {errorMessage}");
        HandleAuthenticationResult(AutoLoginResult.AuthenticationFailed);
    }
    #endregion

    #region Screen Management
    /// <summary>
    /// 스플래시 화면 표시/숨김
    /// </summary>
    /// <param name="show">표시 여부</param>
    private void ShowSplashScreen(bool show)
    {
        if (splashScreen == null) return;

        splashScreen.SetActive(show);
        OnSplashScreenToggle?.Invoke(show);

        if (show)
        {
            // 페이드 인 애니메이션
            if (splashCanvasGroup != null)
            {
                StartCoroutine(FadeCanvasGroup(splashCanvasGroup, 0f, 1f, 0.3f));
            }
            
            Debug.Log("[BackgroundAuth] Splash screen shown");
        }
        else
        {
            Debug.Log("[BackgroundAuth] Splash screen hidden");
        }
    }

    /// <summary>
    /// 지연된 스플래시 화면 숨김
    /// </summary>
    /// <param name="delay">지연 시간</param>
    private IEnumerator HideSplashScreenDelayed(float delay)
    {
        if (splashCanvasGroup != null)
        {
            // 페이드 아웃 애니메이션
            yield return StartCoroutine(FadeCanvasGroup(splashCanvasGroup, 1f, 0f, 0.3f));
        }
        else
        {
            yield return new WaitForSeconds(delay);
        }

        ShowSplashScreen(false);
    }

    /// <summary>
    /// CanvasGroup 페이드 애니메이션
    /// </summary>
    /// <param name="canvasGroup">캔버스 그룹</param>
    /// <param name="startAlpha">시작 알파값</param>
    /// <param name="endAlpha">끝 알파값</param>
    /// <param name="duration">지속 시간</param>
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// 인증 결과에 따른 화면 전환 처리
    /// </summary>
    /// <param name="result">인증 결과</param>
    private void HandleScreenTransition(AutoLoginResult result)
    {
        switch (result)
        {
            case AutoLoginResult.Success:
                // 메인 게임 화면으로 이동
                LoadMainGameScene();
                break;

            case AutoLoginResult.NoStoredCredentials:
            case AutoLoginResult.TokenRefreshFailed:
            case AutoLoginResult.AuthenticationFailed:
                // 로그인 화면으로 이동
                TransitionToLoginScreen();
                break;

            case AutoLoginResult.Disabled:
                // 수동 로그인 화면 표시
                ShowManualLoginScreen();
                break;

            default:
                // 오류 처리 후 로그인 화면 표시
                HandleAuthenticationError(result);
                TransitionToLoginScreen();
                break;
        }
    }

    /// <summary>
    /// 메인 게임 화면 로드
    /// </summary>
    private void LoadMainGameScene()
    {
        Debug.Log("[BackgroundAuth] Loading main game scene");
        
        // 메인 게임 씬이 있는지 확인
        if (Application.CanStreamedLevelBeLoaded("MainGame"))
        {
            SceneManager.LoadScene("MainGame");
        }
        else
        {
            Debug.LogWarning("[BackgroundAuth] MainGame scene not found, staying in current scene");
        }
    }

    /// <summary>
    /// 로그인 화면으로 전환
    /// </summary>
    private void TransitionToLoginScreen()
    {
        Debug.Log("[BackgroundAuth] Transitioning to login screen");
        
        if (LoginFlowStateMachine.Instance != null)
        {
            LoginFlowStateMachine.Instance.TransitionTo(LoginFlowState.ShowingLogin);
        }
        else
        {
            Debug.LogWarning("[BackgroundAuth] LoginFlowStateMachine not available");
        }
    }

    /// <summary>
    /// 수동 로그인 화면 표시
    /// </summary>
    private void ShowManualLoginScreen()
    {
        Debug.Log("[BackgroundAuth] Showing manual login screen");
        
        if (LoginFlowStateMachine.Instance != null)
        {
            LoginFlowStateMachine.Instance.TransitionTo(LoginFlowState.Ready);
        }
    }

    /// <summary>
    /// 인증 오류 처리
    /// </summary>
    /// <param name="result">인증 결과</param>
    private void HandleAuthenticationError(AutoLoginResult result)
    {
        var errorInfo = CreateAuthenticationError(result);
        
        if (GlobalErrorHandler.Instance != null)
        {
            GlobalErrorHandler.Instance.HandleError(errorInfo);
        }
        else
        {
            Debug.LogError($"[BackgroundAuth] Authentication error: {errorInfo.Message}");
        }
    }

    /// <summary>
    /// 인증 오류 정보 생성
    /// </summary>
    /// <param name="result">인증 결과</param>
    /// <returns>오류 정보</returns>
    private ErrorInfo CreateAuthenticationError(AutoLoginResult result)
    {
        return new ErrorInfo
        {
            Type = ErrorType.Authentication,
            Code = $"AUTO_LOGIN_{result}",
            Message = $"Auto-login failed with result: {result}",
            UserMessage = GetUserFriendlyErrorMessage(result),
            Severity = ErrorSeverity.Medium,
            Context = "BackgroundAuth",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 사용자 친화적 오류 메시지 반환
    /// </summary>
    /// <param name="result">인증 결과</param>
    /// <returns>사용자 메시지</returns>
    private string GetUserFriendlyErrorMessage(AutoLoginResult result)
    {
        return result switch
        {
            AutoLoginResult.TokenRefreshFailed => "로그인 정보를 갱신하는 중 문제가 발생했습니다. 다시 로그인해주세요.",
            AutoLoginResult.NetworkError => "네트워크 연결을 확인하고 다시 시도해주세요.",
            AutoLoginResult.Unknown => "알 수 없는 오류가 발생했습니다. 다시 로그인해주세요.",
            _ => "자동 로그인 중 문제가 발생했습니다."
        };
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 수동으로 백그라운드 인증 트리거
    /// </summary>
    public void TriggerBackgroundAuthentication()
    {
        if (!enableBackgroundAuth)
        {
            Debug.LogWarning("[BackgroundAuth] Background authentication is disabled");
            return;
        }

        if (IsAuthenticationInProgress)
        {
            Debug.LogWarning("[BackgroundAuth] Authentication already in progress");
            return;
        }

        Debug.Log("[BackgroundAuth] Manually triggering background authentication");
        StartCoroutine(PerformBackgroundAuthentication());
    }

    /// <summary>
    /// 백그라운드 인증 활성화/비활성화
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetBackgroundAuthEnabled(bool enabled)
    {
        enableBackgroundAuth = enabled;
        Debug.Log($"[BackgroundAuth] Background authentication {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    /// <param name="authenticateOnFocus">포커스 시 인증 여부</param>
    /// <param name="showSplash">스플래시 표시 여부</param>
    /// <param name="maxAuthTime">최대 인증 시간</param>
    public void UpdateSettings(bool authenticateOnFocus, bool showSplash, float maxAuthTime)
    {
        authenticateOnAppFocus = authenticateOnFocus;
        showSplashDuringAuth = showSplash;
        maxAuthenticationTime = Mathf.Clamp(maxAuthTime, 5f, 60f);
        
        Debug.Log("[BackgroundAuth] Settings updated");
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (AutoLoginManager.Instance != null)
        {
            AutoLoginManager.Instance.OnAutoLoginCompleted -= HandleAutoLoginCompleted;
            AutoLoginManager.Instance.OnAutoLoginFailed -= HandleAutoLoginFailed;
        }
    }
    #endregion
}