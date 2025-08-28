using System.Collections;
using GooglePlayGames.BasicApi;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 화면 UI 관리자
/// Google 로그인, 로딩 상태, 오류 메시지를 처리합니다.
/// </summary>
public class LoginScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text statusText;
    [SerializeField] private Text errorText;
    [SerializeField] private Image loadingSpinner;
    [SerializeField] private Text gameTitle;
    [SerializeField] private Image gameLogoImage;
    
    [Header("Loading Animation")]
    [SerializeField] private float spinSpeed = 360f;
    [SerializeField] private AnimationCurve loadingCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Google Branding")]
    [SerializeField] private Sprite googleSignInSprite;
    [SerializeField] private Color googleButtonColor = new Color(0.26f, 0.52f, 0.96f); // Google Blue
    
    [Header("Localization")]
    [SerializeField] private readonly bool useKoreanLocalization = true;
    
    // UI States
    private enum UIState
    {
        Ready,      // 로그인 준비 상태
        Loading,    // 로그인 진행 중
        Success,    // 로그인 성공
        Error       // 로그인 실패
    }
    
    private UIState _currentState = UIState.Ready;
    private Coroutine _loadingAnimationCoroutine;
    private Coroutine _statusUpdateCoroutine;
    
    // 로컬라이제이션 텍스트
    private readonly string[] _loginButtonTexts = ["Sign in with Google", "Google 로그인"];
    private readonly string[] _gameTitles = ["Unity Dice Game", "유니티 주사위 게임"];
    private readonly string[] _statusMessages =
    [
        "Ready to sign in", "로그인 준비 완료",
        "Signing in...", "로그인 중...",
        "Sign in successful!", "로그인 성공!",
        "Sign in failed", "로그인 실패"
    ];
    
    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        SetupUI();
    }
    
    private void Start()
    {
        RegisterAuthEvents();
        SetUIState(UIState.Ready);
        ApplyLocalization();
    }
    
    private void OnDestroy()
    {
        UnregisterAuthEvents();
        StopAllAnimations();
    }
    #endregion
    
    #region Initialization
    /// <summary>
    /// UI 컴포넌트 검증
    /// </summary>
    private void ValidateComponents()
    {
        if (googleLoginButton == null)
            Debug.LogError("[LoginScreen] Google Login Button is not assigned!");
        
        if (loadingPanel == null)
            Debug.LogError("[LoginScreen] Loading Panel is not assigned!");
        
        if (statusText == null)
            Debug.LogWarning("[LoginScreen] Status Text is not assigned");
        
        if (errorText == null)
            Debug.LogWarning("[LoginScreen] Error Text is not assigned");
    }
    
    /// <summary>
    /// UI 초기 설정
    /// </summary>
    private void SetupUI()
    {
        // Google 로그인 버튼 설정
        if (googleLoginButton != null)
        {
            googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);
            
            // Google 브랜딩 적용
            if (googleSignInSprite != null)
            {
                var buttonImage = googleLoginButton.GetComponent<Image>();
                if (buttonImage != null) buttonImage.sprite = googleSignInSprite;
            }
            
            // Google 색상 적용
            var colorBlock = googleLoginButton.colors;
            colorBlock.normalColor = googleButtonColor;
            colorBlock.highlightedColor = Color.Lerp(googleButtonColor, Color.white, 0.1f);
            colorBlock.pressedColor = Color.Lerp(googleButtonColor, Color.black, 0.1f);
            googleLoginButton.colors = colorBlock;
        }
        
        // 로딩 패널 초기 상태
        loadingPanel?.SetActive(false);
        
        // 오류 텍스트 초기 상태
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
            errorText.color = Color.red;
        }
    }
    
    /// <summary>
    /// 로컬라이제이션 적용
    /// </summary>
    private void ApplyLocalization()
    {
        int langIndex = useKoreanLocalization ? 1 : 0;
        
        // 로그인 버튼 텍스트
        if (googleLoginButton != null)
        {
            var buttonText = googleLoginButton.GetComponentInChildren<Text>();
            if (buttonText != null) buttonText.text = _loginButtonTexts[langIndex];
        }
        
        // 게임 타이틀
        if (gameTitle != null) gameTitle.text = _gameTitles[langIndex];
        
        // 상태 텍스트 초기화
        UpdateStatusText(_statusMessages[langIndex * 4]); // Ready 상태
    }
    #endregion
    
    #region Event Handling
    /// <summary>
    /// 인증 이벤트 등록
    /// </summary>
    private void RegisterAuthEvents()
    {
        AuthenticationManager.OnAuthenticationStateChanged += OnAuthenticationStateChanged;
        AuthenticationManager.OnLoginSuccess += OnLoginSuccess;
        AuthenticationManager.OnLoginFailed += OnLoginFailed;
        AuthenticationManager.OnLogoutCompleted += OnLogoutCompleted;
    }
    
    /// <summary>
    /// 인증 이벤트 해제
    /// </summary>
    private void UnregisterAuthEvents()
    {
        if (AuthenticationManager.Instance != null)
        {
            AuthenticationManager.OnAuthenticationStateChanged -= OnAuthenticationStateChanged;
            AuthenticationManager.OnLoginSuccess -= OnLoginSuccess;
            AuthenticationManager.OnLoginFailed -= OnLoginFailed;
            AuthenticationManager.OnLogoutCompleted -= OnLogoutCompleted;
        }
    }
    
    /// <summary>
    /// Google 로그인 버튼 클릭 처리
    /// </summary>
    private void OnGoogleLoginClicked()
    {
        Debug.Log("[LoginScreen] Google login button clicked");
        
        if (AuthenticationManager.Instance == null)
        {
            ShowError("Authentication Manager not found");
            return;
        }
        
        if (AuthenticationManager.Instance.IsAuthenticating)
        {
            Debug.LogWarning("[LoginScreen] Already authenticating");
            return;
        }
        
        SetUIState(UIState.Loading);
        AuthenticationManager.Instance.Login();
    }
    
    /// <summary>
    /// 인증 상태 변경 이벤트 처리
    /// </summary>
    private void OnAuthenticationStateChanged(bool isAuthenticated)
    {
        Debug.Log($"[LoginScreen] Authentication state changed: {isAuthenticated}");
        
        if (isAuthenticated)
        {
            SetUIState(UIState.Success);
        }
    }
    
    /// <summary>
    /// 로그인 성공 이벤트 처리
    /// </summary>
    private void OnLoginSuccess(ILocalUser user)
    {
        Debug.Log($"[LoginScreen] Login successful: {user.userName}");
        SetUIState(UIState.Success);
        
        // 성공 후 메인 화면으로 전환 (3초 후)
        StartCoroutine(TransitionToMainScreen(3f));
    }
    
    /// <summary>
    /// 로그인 실패 이벤트 처리
    /// </summary>
    private void OnLoginFailed(string errorMessage)
    {
        Debug.LogError($"[LoginScreen] Login failed: {errorMessage}");
        SetUIState(UIState.Error);
        ShowError(errorMessage);
    }
    
    /// <summary>
    /// 로그아웃 완료 이벤트 처리
    /// </summary>
    private void OnLogoutCompleted()
    {
        Debug.Log("[LoginScreen] Logout completed");
        SetUIState(UIState.Ready);
        HideError();
    }
    #endregion
    
    #region UI State Management
    /// <summary>
    /// UI 상태 설정
    /// </summary>
    private void SetUIState(UIState newState)
    {
        if (_currentState == newState) return;
        
        _currentState = newState;
        UpdateUIForState(newState);
    }
    
    /// <summary>
    /// 상태에 따른 UI 업데이트
    /// </summary>
    private void UpdateUIForState(UIState state)
    {
        int langIndex = useKoreanLocalization ? 1 : 0;
        
        switch (state)
        {
            case UIState.Ready:
                SetLoginButtonEnabled(true);
                ShowLoadingPanel(false);
                UpdateStatusText(_statusMessages[langIndex * 4]); // Ready
                HideError();
                break;
                
            case UIState.Loading:
                SetLoginButtonEnabled(false);
                ShowLoadingPanel(true);
                UpdateStatusText(_statusMessages[langIndex * 4 + 1]); // Loading
                HideError();
                StartLoadingAnimation();
                break;
                
            case UIState.Success:
                SetLoginButtonEnabled(false);
                ShowLoadingPanel(false);
                UpdateStatusText(_statusMessages[langIndex * 4 + 2]); // Success
                HideError();
                StopLoadingAnimation();
                break;
                
            case UIState.Error:
                SetLoginButtonEnabled(true);
                ShowLoadingPanel(false);
                UpdateStatusText(_statusMessages[langIndex * 4 + 3]); // Error
                StopLoadingAnimation();
                break;
        }
    }
    
    /// <summary>
    /// 로그인 버튼 활성화/비활성화
    /// </summary>
    private void SetLoginButtonEnabled(bool enabled)
    {
        if (googleLoginButton != null)
        {
            googleLoginButton.interactable = enabled;
            
            // 시각적 피드백
            var canvasGroup = googleLoginButton.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = enabled ? 1f : 0.6f;
        }
    }
    
    /// <summary>
    /// 로딩 패널 표시/숨김
    /// </summary>
    private void ShowLoadingPanel(bool show)
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(show);
    }
    
    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
    #endregion
    
    #region Error Handling
    /// <summary>
    /// 오류 메시지 표시
    /// </summary>
    private void ShowError(string errorMessage)
    {
        if (errorText != null)
        {
            errorText.text = errorMessage;
            errorText.gameObject.SetActive(true);
            
            // 자동 숨김 (5초 후)
            StartCoroutine(HideErrorAfterDelay(5f));
        }
    }
    
    /// <summary>
    /// 오류 메시지 숨김
    /// </summary>
    private void HideError()
    {
        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 지연 후 오류 메시지 숨김
    /// </summary>
    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideError();
    }
    #endregion
    
    #region Animations
    /// <summary>
    /// 로딩 애니메이션 시작
    /// </summary>
    private void StartLoadingAnimation()
    {
        StopLoadingAnimation();
        
        if (loadingSpinner != null)
            _loadingAnimationCoroutine = StartCoroutine(LoadingSpinAnimation());
    }
    
    /// <summary>
    /// 로딩 애니메이션 중지
    /// </summary>
    private void StopLoadingAnimation()
    {
        if (_loadingAnimationCoroutine != null)
        {
            StopCoroutine(_loadingAnimationCoroutine);
            _loadingAnimationCoroutine = null;
        }
    }
    
    /// <summary>
    /// 로딩 스피너 애니메이션
    /// </summary>
    private IEnumerator LoadingSpinAnimation()
    {
        while (loadingSpinner != null)
        {
            float rotation = (Time.time * spinSpeed) % 360f;
            loadingSpinner.transform.rotation = Quaternion.Euler(0, 0, -rotation);
            yield return null;
        }
    }
    
    /// <summary>
    /// 모든 애니메이션 중지
    /// </summary>
    private void StopAllAnimations()
    {
        StopLoadingAnimation();
        
        if (_statusUpdateCoroutine != null)
        {
            StopCoroutine(_statusUpdateCoroutine);
            _statusUpdateCoroutine = null;
        }
    }
    #endregion
    
    #region Scene Transition
    /// <summary>
    /// 메인 화면으로 전환
    /// </summary>
    private IEnumerator TransitionToMainScreen(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 여기에 메인 화면 전환 로직 추가
        Debug.Log("[LoginScreen] Transitioning to main screen...");
        
        // 예시: 씬 전환
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainGame");
        
        // 현재는 로그인 화면을 비활성화
        gameObject.SetActive(false);
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 강제로 로그인 화면 표시
    /// </summary>
    public void ShowLoginScreen()
    {
        gameObject.SetActive(true);
        SetUIState(UIState.Ready);
    }
    
    /// <summary>
    /// 로그인 화면 숨김
    /// </summary>
    public void HideLoginScreen()
    {
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 로그아웃 및 로그인 화면 표시
    /// </summary>
    public void LogoutAndShowLogin()
    {
        if (AuthenticationManager.Instance != null)
            AuthenticationManager.Instance.Logout();
        
        ShowLoginScreen();
    }
    
    /// <summary>
    /// 로컬라이제이션 언어 변경
    /// </summary>
    public void SetLanguage(bool useKorean)
    {
        useKoreanLocalization = useKorean;
        ApplyLocalization();
        
        // 현재 상태에 맞는 텍스트 재적용
        UpdateUIForState(_currentState);
    }
    #endregion
}