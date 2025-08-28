using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

/// <summary>
/// Google Play Games Services 연동 관리자
/// Google Play Games Services 인증, 로그인, 로그아웃 기능을 제공합니다.
/// </summary>
public class GooglePlayGamesManager : MonoBehaviour
{
    [Header("Google Play Games Settings")]
    [SerializeField] private readonly bool enableDebugLog = true;
    
    [Header("UI References")]
    [SerializeField] private UnityEngine.UI.Button loginButton;
    [SerializeField] private UnityEngine.UI.Button logoutButton;
    [SerializeField] private UnityEngine.UI.Text statusText;
    [SerializeField] private UnityEngine.UI.Text userInfoText;
    
    private bool isInitialized = false;
    
    public static GooglePlayGamesManager Instance { get; private set; }
    
    /// <summary>
    /// 현재 로그인 상태
    /// </summary>
    public bool IsAuthenticated => Social.localUser.authenticated;
    
    /// <summary>
    /// 현재 사용자 정보
    /// </summary>
    public ILocalUser CurrentUser => Social.localUser;
    
    private void Awake()
    {
        // 싱글톤 패턴 적용
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        InitializeGooglePlayGames();
        SetupUI();
    }
    
    /// <summary>
    /// Google Play Games Services 초기화
    /// </summary>
    private void InitializeGooglePlayGames()
    {
        if (isInitialized)
        {
            Log("Google Play Games already initialized");
            return;
        }
        
        try
        {
            PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
                .RequestServerAuthCode(false) // 서버 인증이 필요한 경우 true로 설정
                .RequestEmail()
                .RequestIdToken()
                .Build();
            
            PlayGamesPlatform.InitializeInstance(config);
            PlayGamesPlatform.Activate();
            
            isInitialized = true;
            Log("Google Play Games Services initialized successfully");
            UpdateStatusText("Ready to authenticate");
        }
        catch (System.Exception e)
        {
            LogError($"Failed to initialize Google Play Games: {e.Message}");
            UpdateStatusText("Initialization failed");
        }
    }
    
    /// <summary>
    /// UI 버튼 이벤트 설정
    /// </summary>
    private void SetupUI()
    {
        loginButton?.onClick.AddListener(AuthenticateUser);
        logoutButton?.onClick.AddListener(SignOut);
        
        UpdateUI();
    }
    
    /// <summary>
    /// Google Play Games 사용자 인증
    /// </summary>
    public void AuthenticateUser()
    {
        if (!isInitialized)
        {
            LogError("Google Play Games not initialized");
            UpdateStatusText("Not initialized");
            return;
        }
        
        UpdateStatusText("Authenticating...");
        Log("Starting authentication...");
        
        Social.localUser.Authenticate((bool success) =>
        {
            if (success)
            {
                Log($"Authentication successful! User: {Social.localUser.userName} (ID: {Social.localUser.id})");
                UpdateStatusText("Authentication successful");
                UpdateUserInfo();
            }
            else
            {
                LogError("Authentication failed");
                UpdateStatusText("Authentication failed");
                ClearUserInfo();
            }
            
            UpdateUI();
        });
    }
    
    /// <summary>
    /// Google Play Games 로그아웃
    /// </summary>
    public void SignOut()
    {
        if (PlayGamesPlatform.Instance != null)
        {
            PlayGamesPlatform.Instance.SignOut();
            Log("User signed out");
            UpdateStatusText("Signed out");
            ClearUserInfo();
            UpdateUI();
        }
    }
    
    /// <summary>
    /// 자동 로그인 시도 (앱 시작 시 사용)
    /// </summary>
    public void TryAutoLogin()
    {
        if (!isInitialized)
        {
            InitializeGooglePlayGames();
        }
        
        if (Social.localUser.authenticated)
        {
            Log("User already authenticated");
            UpdateStatusText("Already authenticated");
            UpdateUserInfo();
            UpdateUI();
        }
        else
        {
            Log("Attempting silent authentication...");
            AuthenticateUser();
        }
    }
    
    /// <summary>
    /// UI 상태 업데이트
    /// </summary>
    private void UpdateUI()
    {
        bool isAuth = IsAuthenticated;
        
        if (loginButton != null) loginButton.interactable = !isAuth;
        if (logoutButton != null) logoutButton.interactable = isAuth;
    }
    
    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    private void UpdateStatusText(string status)
    {
        if (statusText != null) statusText.text = $"Status: {status}";
        Log($"Status: {status}");
    }
    
    /// <summary>
    /// 사용자 정보 UI 업데이트
    /// </summary>
    private void UpdateUserInfo()
    {
        if (userInfoText != null && IsAuthenticated)
        {
            string userInfo = $"User: {CurrentUser.userName}\nID: {CurrentUser.id}";
            userInfoText.text = userInfo;
        }
    }
    
    /// <summary>
    /// 사용자 정보 UI 클리어
    /// </summary>
    private void ClearUserInfo()
    {
        if (userInfoText != null) userInfoText.text = "Not authenticated";
    }
    
    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[GooglePlayGames] {message}");
    }
    
    /// <summary>
    /// 에러 로그 출력
    /// </summary>
    private void LogError(string message)
    {
        Debug.LogError($"[GooglePlayGames] {message}");
    }
    
    /// <summary>
    /// 연결 테스트 수행
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void TestConnection()
    {
        Log("=== Google Play Games Connection Test ===");
        Log($"Is Initialized: {isInitialized}");
        Log($"Is Authenticated: {IsAuthenticated}");
        Log($"Platform: {Application.platform}");
        
        if (IsAuthenticated)
        {
            Log($"User Name: {CurrentUser.userName}");
            Log($"User ID: {CurrentUser.id}");
        }
        
        Log("=== Test Complete ===");
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 제거
        loginButton?.onClick.RemoveListener(AuthenticateUser);
        logoutButton?.onClick.RemoveListener(SignOut);
    }
}