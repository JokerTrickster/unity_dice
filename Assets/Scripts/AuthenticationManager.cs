using System;
using System.Collections;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

/// <summary>
/// Google Play Games Services 인증 관리자
/// Singleton 패턴을 사용하여 전역에서 접근 가능한 인증 시스템을 제공합니다.
/// </summary>
public class AuthenticationManager : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 인증 상태가 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnAuthenticationStateChanged;
    
    /// <summary>
    /// 로그인이 성공했을 때 발생하는 이벤트
    /// </summary>
    public static event Action<ILocalUser> OnLoginSuccess;
    
    /// <summary>
    /// 로그인이 실패했을 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnLoginFailed;
    
    /// <summary>
    /// 로그아웃이 완료되었을 때 발생하는 이벤트
    /// </summary>
    public static event Action OnLogoutCompleted;
    #endregion

    #region Singleton
    private static AuthenticationManager _instance;
    public static AuthenticationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AuthenticationManager");
                _instance = go.AddComponent<AuthenticationManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isAuthenticating = false;
    private bool _autoLoginEnabled = true;
    private Coroutine _tokenRefreshCoroutine;
    
    // 토큰 저장 키
    private const string TOKEN_KEY = "gpg_token_encrypted";
    private const string USER_ID_KEY = "gpg_user_id";
    private const string USER_NAME_KEY = "gpg_user_name";
    private const string AUTO_LOGIN_KEY = "gpg_auto_login";
    private const string TOKEN_EXPIRY_KEY = "gpg_token_expiry";
    
    // 토큰 갱신 설정
    private const int TOKEN_REFRESH_INTERVAL_HOURS = 23; // 23시간마다 갱신
    private const int TOKEN_EXPIRY_BUFFER_HOURS = 1; // 만료 1시간 전 갱신
    #endregion

    #region Public Properties
    /// <summary>
    /// 현재 인증 상태
    /// </summary>
    public bool IsAuthenticated => Social.localUser != null && Social.localUser.authenticated;
    
    /// <summary>
    /// 현재 로그인된 사용자 정보
    /// </summary>
    public ILocalUser CurrentUser => IsAuthenticated ? Social.localUser : null;
    
    /// <summary>
    /// 현재 인증 진행 중 여부
    /// </summary>
    public bool IsAuthenticating => _isAuthenticating;
    
    /// <summary>
    /// Google Play Games 서비스 초기화 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 자동 로그인 활성화 상태
    /// </summary>
    public bool AutoLoginEnabled
    {
        get => _autoLoginEnabled;
        set
        {
            _autoLoginEnabled = value;
            PlayerPrefs.SetInt(AUTO_LOGIN_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton 패턴 구현
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
        
        LoadSettings();
    }

    private void Start()
    {
        InitializeGooglePlayGames();
    }

    private void OnDestroy()
    {
        StopTokenRefresh();
        
        // 이벤트 구독 해제
        OnAuthenticationStateChanged = null;
        OnLoginSuccess = null;
        OnLoginFailed = null;
        OnLogoutCompleted = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Google Play Games Services 초기화
    /// </summary>
    private void InitializeGooglePlayGames()
    {
        if (_isInitialized)
        {
            Debug.Log("[Auth] Already initialized");
            return;
        }

        try
        {
            PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
                .RequestServerAuthCode(false)
                .RequestEmail()
                .RequestIdToken()
                .Build();

            PlayGamesPlatform.InitializeInstance(config);
            PlayGamesPlatform.Activate();

            _isInitialized = true;
            Debug.Log("[Auth] Google Play Games Services initialized");

            // 자동 로그인이 활성화된 경우 시도
            if (_autoLoginEnabled)
            {
                StartCoroutine(TryAutoLoginCoroutine());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Initialization failed: {e.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 설정 로드
    /// </summary>
    private void LoadSettings()
    {
        _autoLoginEnabled = PlayerPrefs.GetInt(AUTO_LOGIN_KEY, 1) == 1;
    }
    #endregion

    #region Authentication
    /// <summary>
    /// Google Play Games 로그인
    /// </summary>
    public void Login()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[Auth] Not initialized");
            OnLoginFailed?.Invoke("Authentication manager not initialized");
            return;
        }

        if (_isAuthenticating)
        {
            Debug.LogWarning("[Auth] Already authenticating");
            return;
        }

        if (IsAuthenticated)
        {
            Debug.Log("[Auth] Already authenticated");
            OnLoginSuccess?.Invoke(CurrentUser);
            return;
        }

        StartCoroutine(LoginCoroutine());
    }

    /// <summary>
    /// 로그인 코루틴
    /// </summary>
    private IEnumerator LoginCoroutine()
    {
        _isAuthenticating = true;
        bool loginCompleted = false;
        bool loginSuccess = false;
        string errorMessage = "";

        Debug.Log("[Auth] Starting authentication...");

        Social.localUser.Authenticate((bool success) =>
        {
            loginCompleted = true;
            loginSuccess = success;

            if (success)
            {
                Debug.Log($"[Auth] Login successful - User: {Social.localUser.userName}, ID: {Social.localUser.id}");
                SaveUserData();
                StartTokenRefresh();
            }
            else
            {
                errorMessage = "Authentication failed";
                Debug.LogError("[Auth] Login failed");
            }
        });

        // 로그인 완료까지 대기 (최대 30초)
        float timeoutTime = 30f;
        float elapsedTime = 0f;

        while (!loginCompleted && elapsedTime < timeoutTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _isAuthenticating = false;

        if (!loginCompleted)
        {
            errorMessage = "Authentication timeout";
            Debug.LogError("[Auth] Login timeout");
        }

        // 결과 처리
        if (loginSuccess && IsAuthenticated)
        {
            OnAuthenticationStateChanged?.Invoke(true);
            OnLoginSuccess?.Invoke(CurrentUser);
        }
        else
        {
            OnAuthenticationStateChanged?.Invoke(false);
            OnLoginFailed?.Invoke(string.IsNullOrEmpty(errorMessage) ? "Unknown error" : errorMessage);
        }
    }

    /// <summary>
    /// 자동 로그인 시도
    /// </summary>
    private IEnumerator TryAutoLoginCoroutine()
    {
        yield return new WaitForSeconds(1f); // 초기화 완료 대기

        if (HasValidToken())
        {
            Debug.Log("[Auth] Valid token found, attempting auto-login");
            Login();
        }
        else
        {
            Debug.Log("[Auth] No valid token found for auto-login");
        }
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    public void Logout()
    {
        if (!IsAuthenticated)
        {
            Debug.LogWarning("[Auth] Not authenticated");
            OnLogoutCompleted?.Invoke();
            return;
        }

        try
        {
            PlayGamesPlatform.Instance?.SignOut();
            ClearUserData();
            StopTokenRefresh();

            Debug.Log("[Auth] Logout successful");
            OnAuthenticationStateChanged?.Invoke(false);
            OnLogoutCompleted?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Logout failed: {e.Message}");
            OnLogoutCompleted?.Invoke();
        }
    }
    #endregion

    #region Token Management
    /// <summary>
    /// 토큰 갱신 시작
    /// </summary>
    private void StartTokenRefresh()
    {
        StopTokenRefresh();
        _tokenRefreshCoroutine = StartCoroutine(TokenRefreshCoroutine());
    }

    /// <summary>
    /// 토큰 갱신 중지
    /// </summary>
    private void StopTokenRefresh()
    {
        if (_tokenRefreshCoroutine != null)
        {
            StopCoroutine(_tokenRefreshCoroutine);
            _tokenRefreshCoroutine = null;
        }
    }

    /// <summary>
    /// 토큰 갱신 코루틴
    /// </summary>
    private IEnumerator TokenRefreshCoroutine()
    {
        while (IsAuthenticated)
        {
            yield return new WaitForSeconds(TOKEN_REFRESH_INTERVAL_HOURS * 3600f);

            if (IsAuthenticated)
            {
                Debug.Log("[Auth] Refreshing token...");
                RefreshToken();
            }
        }
    }

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    private void RefreshToken()
    {
        if (!IsAuthenticated) return;

        try
        {
            // 현재 사용자 정보 다시 저장 (토큰 갱신 효과)
            SaveUserData();
            Debug.Log("[Auth] Token refreshed successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Token refresh failed: {e.Message}");
        }
    }

    /// <summary>
    /// 유효한 토큰이 있는지 확인
    /// </summary>
    private bool HasValidToken()
    {
        if (!PlayerPrefs.HasKey(TOKEN_KEY) || !PlayerPrefs.HasKey(TOKEN_EXPIRY_KEY))
            return false;

        long expiryTicks = Convert.ToInt64(PlayerPrefs.GetString(TOKEN_EXPIRY_KEY, "0"));
        DateTime expiryTime = new DateTime(expiryTicks);
        DateTime currentTime = DateTime.Now;

        // 만료 1시간 전까지를 유효로 간주
        return currentTime < expiryTime.AddHours(-TOKEN_EXPIRY_BUFFER_HOURS);
    }
    #endregion

    #region Data Management
    /// <summary>
    /// 사용자 데이터 저장 (암호화)
    /// </summary>
    private void SaveUserData()
    {
        if (!IsAuthenticated) return;

        try
        {
            // 사용자 정보 저장
            PlayerPrefs.SetString(USER_ID_KEY, CurrentUser.id);
            PlayerPrefs.SetString(USER_NAME_KEY, CurrentUser.userName);

            // 토큰 만료 시간 설정 (24시간 후)
            DateTime expiryTime = DateTime.Now.AddHours(24);
            PlayerPrefs.SetString(TOKEN_EXPIRY_KEY, expiryTime.Ticks.ToString());

            // 더미 토큰 저장 (실제 토큰은 Google Play Games에서 내부 관리)
            string encryptedToken = EncryptData($"gpg_token_{CurrentUser.id}_{DateTime.Now.Ticks}");
            PlayerPrefs.SetString(TOKEN_KEY, encryptedToken);

            PlayerPrefs.Save();
            Debug.Log("[Auth] User data saved");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Failed to save user data: {e.Message}");
        }
    }

    /// <summary>
    /// 사용자 데이터 삭제
    /// </summary>
    private void ClearUserData()
    {
        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.DeleteKey(USER_ID_KEY);
        PlayerPrefs.DeleteKey(USER_NAME_KEY);
        PlayerPrefs.DeleteKey(TOKEN_EXPIRY_KEY);
        PlayerPrefs.Save();
        
        Debug.Log("[Auth] User data cleared");
    }

    /// <summary>
    /// 데이터 암호화 (간단한 XOR 암호화)
    /// </summary>
    private string EncryptData(string data)
    {
        if (string.IsNullOrEmpty(data)) return data;

        char[] chars = data.ToCharArray();
        const int key = 129;

        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(chars[i] ^ key);
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string(chars)));
    }

    /// <summary>
    /// 데이터 복호화
    /// </summary>
    private string DecryptData(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData)) return encryptedData;

        try
        {
            byte[] bytes = Convert.FromBase64String(encryptedData);
            char[] chars = System.Text.Encoding.UTF8.GetString(bytes).ToCharArray();
            const int key = 129;

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)(chars[i] ^ key);
            }

            return new string(chars);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Decryption failed: {e.Message}");
            return "";
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 인증 상태 강제 갱신
    /// </summary>
    public void RefreshAuthenticationState()
    {
        bool currentState = IsAuthenticated;
        OnAuthenticationStateChanged?.Invoke(currentState);
        
        if (currentState)
        {
            OnLoginSuccess?.Invoke(CurrentUser);
        }
    }

    /// <summary>
    /// 저장된 사용자 정보 가져오기
    /// </summary>
    public (string userId, string userName) GetSavedUserInfo()
    {
        string userId = PlayerPrefs.GetString(USER_ID_KEY, "");
        string userName = PlayerPrefs.GetString(USER_NAME_KEY, "");
        return (userId, userName);
    }

    /// <summary>
    /// 강제로 자동 로그인 시도
    /// </summary>
    public void TryAutoLogin()
    {
        if (!_autoLoginEnabled)
        {
            Debug.Log("[Auth] Auto-login disabled");
            return;
        }

        StartCoroutine(TryAutoLoginCoroutine());
    }
    #endregion
}