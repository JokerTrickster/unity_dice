using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 자동 로그인 관리자
/// 앱 시작 시 seamless한 자동 로그인 경험을 제공합니다.
/// </summary>
public class AutoLoginManager : MonoBehaviour
{
    #region Singleton
    private static AutoLoginManager _instance;
    public static AutoLoginManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AutoLoginManager");
                _instance = go.AddComponent<AutoLoginManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Auto-Login Configuration")]
    [SerializeField] private bool enableAutoLogin = true;
    [SerializeField] private float tokenValidityThreshold = 3600f; // 1시간
    [SerializeField] private int maxAutoLoginAttempts = 2;
    [SerializeField] private float maxAuthenticationTime = 10f;
    [SerializeField] private bool requireServerValidation = false;

    [Header("Background Behavior")]
    [SerializeField] private bool authenticateOnAppFocus = true;
    [SerializeField] private bool showSplashDuringAuth = true;
    #endregion

    #region Events
    /// <summary>
    /// 자동 로그인 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action<AutoLoginResult> OnAutoLoginCompleted;
    
    /// <summary>
    /// 자동 로그인 실패 시 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnAutoLoginFailed;
    
    /// <summary>
    /// 자동 로그인 진행 상태 변경 이벤트
    /// </summary>
    public static event Action<string> OnAutoLoginProgressChanged;
    #endregion

    #region Private Fields
    private TokenValidator _tokenValidator;
    private TokenRefreshManager _tokenRefreshManager;
    private bool _isAutoLoginInProgress = false;
    private DateTime _lastAutoLoginAttempt = DateTime.MinValue;
    private int _currentAttemptCount = 0;
    private AutoLoginResult _lastResult = AutoLoginResult.Unknown;
    private bool _isInitialized = false;
    #endregion

    #region Properties
    /// <summary>
    /// 자동 로그인 활성화 여부
    /// </summary>
    public bool EnableAutoLogin
    {
        get => enableAutoLogin && AutoLoginPrefs.IsAutoLoginEnabled;
        set
        {
            enableAutoLogin = value;
            AutoLoginPrefs.IsAutoLoginEnabled = value;
        }
    }
    
    /// <summary>
    /// 자동 로그인 진행 중 여부
    /// </summary>
    public bool IsAutoLoginInProgress => _isAutoLoginInProgress;
    
    /// <summary>
    /// 마지막 자동 로그인 시도 시간
    /// </summary>
    public DateTime LastAutoLoginAttempt => _lastAutoLoginAttempt;
    
    /// <summary>
    /// 마지막 자동 로그인 결과
    /// </summary>
    public AutoLoginResult LastResult => _lastResult;
    
    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;
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
        InitializeAutoLogin();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && authenticateOnAppFocus && _isInitialized)
        {
            // 앱이 포커스를 얻었을 때 자동 로그인 시도
            StartCoroutine(DelayedAutoLogin(1f));
        }
    }

    private void OnDestroy()
    {
        OnAutoLoginCompleted = null;
        OnAutoLoginFailed = null;
        OnAutoLoginProgressChanged = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 자동 로그인 시스템 초기화
    /// </summary>
    private void InitializeAutoLogin()
    {
        try
        {
            // SecureStorage 초기화
            if (!SecureStorage.IsInitialized)
            {
                SecureStorage.Initialize();
            }

            // 토큰 관리 컴포넌트 초기화
            _tokenValidator = new TokenValidator(tokenValidityThreshold, requireServerValidation);
            _tokenRefreshManager = new TokenRefreshManager();

            // 설정 로드
            LoadSettings();

            _isInitialized = true;
            Debug.Log("[AutoLoginManager] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginManager] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 설정 로드
    /// </summary>
    private void LoadSettings()
    {
        // PlayerPrefs에서 사용자 설정 로드
        enableAutoLogin = AutoLoginPrefs.IsAutoLoginEnabled;
        
        Debug.Log($"[AutoLoginManager] Settings loaded - Auto-login: {enableAutoLogin}");
    }
    #endregion

    #region Auto-Login Flow
    /// <summary>
    /// 자동 로그인 시도 (비동기)
    /// </summary>
    public async Task<AutoLoginResult> TryAutoLoginAsync()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[AutoLoginManager] Not initialized");
            return AutoLoginResult.Unknown;
        }

        if (_isAutoLoginInProgress)
        {
            Debug.LogWarning("[AutoLoginManager] Auto-login already in progress");
            return _lastResult;
        }

        if (!EnableAutoLogin)
        {
            Debug.Log("[AutoLoginManager] Auto-login is disabled");
            return AutoLoginResult.Disabled;
        }

        // 최대 시도 횟수 체크
        if (_currentAttemptCount >= maxAutoLoginAttempts)
        {
            Debug.LogWarning("[AutoLoginManager] Max auto-login attempts exceeded");
            return AutoLoginResult.MaxAttemptsExceeded;
        }

        _isAutoLoginInProgress = true;
        _lastAutoLoginAttempt = DateTime.Now;
        _currentAttemptCount++;

        try
        {
            UpdateProgress("자동 로그인을 시도합니다...");

            // 1. 저장된 자격증명 확인
            UpdateProgress("저장된 자격증명을 확인합니다...");
            var storedToken = SecureStorage.GetAuthToken();
            if (string.IsNullOrEmpty(storedToken))
            {
                Debug.Log("[AutoLoginManager] No stored credentials found");
                return SetResult(AutoLoginResult.NoStoredCredentials);
            }

            // 2. 토큰 유효성 검사
            UpdateProgress("토큰 유효성을 검사합니다...");
            bool isTokenValid = await _tokenValidator.ValidateTokenAsync(storedToken);
            
            if (!isTokenValid)
            {
                // 3. 토큰 갱신 시도
                UpdateProgress("토큰을 갱신합니다...");
                var refreshResult = await _tokenRefreshManager.RefreshTokenAsync();
                
                if (!refreshResult.Success)
                {
                    Debug.LogError($"[AutoLoginManager] Token refresh failed: {refreshResult.ErrorMessage}");
                    return SetResult(AutoLoginResult.TokenRefreshFailed);
                }
                
                // 갱신된 토큰으로 다시 검증
                storedToken = SecureStorage.GetAuthToken();
                if (string.IsNullOrEmpty(storedToken))
                {
                    return SetResult(AutoLoginResult.TokenRefreshFailed);
                }
            }

            // 4. Google Play Games 인증
            UpdateProgress("Google Play Games 인증을 시도합니다...");
            var authResult = await AuthenticateWithStoredCredentialsAsync();
            if (!authResult.Success)
            {
                Debug.LogError($"[AutoLoginManager] Authentication failed: {authResult.ErrorMessage}");
                return SetResult(AutoLoginResult.AuthenticationFailed);
            }

            // 5. 사용자 데이터 로드
            UpdateProgress("사용자 데이터를 로드합니다...");
            bool userDataLoaded = await LoadUserDataAsync(authResult.UserId);
            if (!userDataLoaded)
            {
                Debug.LogWarning("[AutoLoginManager] Failed to load user data, but proceeding");
            }

            // 6. 상태 머신 업데이트
            UpdateProgress("로그인을 완료합니다...");
            if (LoginFlowStateMachine.Instance != null)
            {
                LoginFlowStateMachine.Instance.ChangeState(LoginState.Complete, "Auto-login successful");
            }

            Debug.Log("[AutoLoginManager] Auto-login completed successfully");
            _currentAttemptCount = 0; // 성공 시 카운터 리셋
            return SetResult(AutoLoginResult.Success);
        }
        catch (TimeoutException)
        {
            Debug.LogError("[AutoLoginManager] Auto-login timeout");
            return SetResult(AutoLoginResult.Timeout);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginManager] Auto-login failed with exception: {ex.Message}");
            return SetResult(AutoLoginResult.Unknown);
        }
        finally
        {
            _isAutoLoginInProgress = false;
            AutoLoginPrefs.LastAutoLoginTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 코루틴을 통한 자동 로그인 시도
    /// </summary>
    public void TryAutoLogin()
    {
        StartCoroutine(TryAutoLoginCoroutine());
    }

    /// <summary>
    /// 자동 로그인 코루틴
    /// </summary>
    private IEnumerator TryAutoLoginCoroutine()
    {
        var task = TryAutoLoginAsync();
        
        // 최대 시간 제한
        float elapsed = 0f;
        while (!task.IsCompleted && elapsed < maxAuthenticationTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        AutoLoginResult result;
        if (task.IsCompleted)
        {
            result = task.Result;
        }
        else
        {
            Debug.LogError("[AutoLoginManager] Auto-login timed out");
            result = AutoLoginResult.Timeout;
        }

        // 이벤트 발생
        OnAutoLoginCompleted?.Invoke(result);
        
        if (result != AutoLoginResult.Success)
        {
            string errorMessage = GetResultMessage(result);
            OnAutoLoginFailed?.Invoke(errorMessage);
        }
    }

    /// <summary>
    /// 지연된 자동 로그인 (앱 포커스 시)
    /// </summary>
    private IEnumerator DelayedAutoLogin(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 마지막 시도로부터 충분한 시간이 지났는지 확인
        if ((DateTime.Now - _lastAutoLoginAttempt).TotalMinutes > 5)
        {
            TryAutoLogin();
        }
    }
    #endregion

    #region Authentication Methods
    /// <summary>
    /// 저장된 자격증명으로 인증
    /// </summary>
    private async Task<AuthenticationResult> AuthenticateWithStoredCredentialsAsync()
    {
        try
        {
            var credentials = SecureStorage.GetUserCredentials();
            if (credentials == null)
            {
                return new AuthenticationResult 
                { 
                    Success = false, 
                    ErrorMessage = "No stored user credentials" 
                };
            }

            // Google Play Games 인증 시도
            if (GooglePlayGamesManager.Instance != null)
            {
                bool gpgsResult = await GooglePlayGamesManager.Instance.SilentSignInAsync();
                if (gpgsResult)
                {
                    var userInfo = GooglePlayGamesManager.Instance.GetCurrentPlayerInfo();
                    return new AuthenticationResult
                    {
                        Success = true,
                        UserId = userInfo?.playerId ?? credentials.UserId
                    };
                }
            }

            // GPGS 실패 시 저장된 자격증명 사용
            return new AuthenticationResult
            {
                Success = true,
                UserId = credentials.UserId
            };
        }
        catch (Exception ex)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 사용자 데이터 로드
    /// </summary>
    private async Task<bool> LoadUserDataAsync(string userId)
    {
        try
        {
            if (UserDataManager.Instance != null)
            {
                await UserDataManager.Instance.LoadUserDataAsync(userId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginManager] Failed to load user data: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 결과 설정 및 반환
    /// </summary>
    private AutoLoginResult SetResult(AutoLoginResult result)
    {
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// 진행 상황 업데이트
    /// </summary>
    private void UpdateProgress(string message)
    {
        Debug.Log($"[AutoLoginManager] {message}");
        OnAutoLoginProgressChanged?.Invoke(message);
    }

    /// <summary>
    /// 결과 메시지 반환
    /// </summary>
    private string GetResultMessage(AutoLoginResult result)
    {
        return result switch
        {
            AutoLoginResult.Success => "자동 로그인에 성공했습니다.",
            AutoLoginResult.Disabled => "자동 로그인이 비활성화되어 있습니다.",
            AutoLoginResult.NoStoredCredentials => "저장된 로그인 정보가 없습니다.",
            AutoLoginResult.TokenExpired => "로그인 토큰이 만료되었습니다.",
            AutoLoginResult.TokenRefreshFailed => "토큰 갱신에 실패했습니다.",
            AutoLoginResult.AuthenticationFailed => "인증에 실패했습니다.",
            AutoLoginResult.NetworkError => "네트워크 오류가 발생했습니다.",
            AutoLoginResult.Timeout => "로그인 시간이 초과되었습니다.",
            AutoLoginResult.MaxAttemptsExceeded => "최대 시도 횟수를 초과했습니다.",
            AutoLoginResult.UserCancelled => "사용자가 취소했습니다.",
            _ => "알 수 없는 오류가 발생했습니다."
        };
    }
    #endregion

    #region Public API
    /// <summary>
    /// 자동 로그인 강제 중단
    /// </summary>
    public void CancelAutoLogin()
    {
        if (_isAutoLoginInProgress)
        {
            _isAutoLoginInProgress = false;
            SetResult(AutoLoginResult.UserCancelled);
            OnAutoLoginCompleted?.Invoke(AutoLoginResult.UserCancelled);
            Debug.Log("[AutoLoginManager] Auto-login cancelled by user");
        }
    }

    /// <summary>
    /// 자동 로그인 카운터 리셋
    /// </summary>
    public void ResetAttemptCount()
    {
        _currentAttemptCount = 0;
        Debug.Log("[AutoLoginManager] Attempt count reset");
    }

    /// <summary>
    /// 자동 로그인 가능 여부 확인
    /// </summary>
    public bool CanAttemptAutoLogin()
    {
        return _isInitialized && 
               EnableAutoLogin && 
               !_isAutoLoginInProgress && 
               _currentAttemptCount < maxAutoLoginAttempts &&
               SecureStorage.HasAuthToken;
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(bool enableAutoLogin, float tokenThreshold = 3600f, int maxAttempts = 2)
    {
        this.enableAutoLogin = enableAutoLogin;
        this.tokenValidityThreshold = tokenThreshold;
        this.maxAutoLoginAttempts = maxAttempts;
        
        AutoLoginPrefs.IsAutoLoginEnabled = enableAutoLogin;
        
        if (_tokenValidator != null)
        {
            _tokenValidator.UpdateTokenValidityThreshold(tokenThreshold);
        }
        
        Debug.Log($"[AutoLoginManager] Settings updated - Auto-login: {enableAutoLogin}");
    }

    /// <summary>
    /// 통계 정보 반환
    /// </summary>
    public AutoLoginStats GetStats()
    {
        return new AutoLoginStats
        {
            IsEnabled = EnableAutoLogin,
            IsInProgress = IsAutoLoginInProgress,
            LastAttemptTime = LastAutoLoginAttempt,
            LastResult = LastResult,
            CurrentAttemptCount = _currentAttemptCount,
            MaxAttempts = maxAutoLoginAttempts,
            CanAttempt = CanAttemptAutoLogin()
        };
    }
    #endregion
}

#region Result Types and Data Classes
/// <summary>
/// 자동 로그인 결과
/// </summary>
public enum AutoLoginResult
{
    Success,
    Disabled,
    NoStoredCredentials,
    TokenExpired,
    TokenRefreshFailed,
    AuthenticationFailed,
    NetworkError,
    Timeout,
    MaxAttemptsExceeded,
    UserCancelled,
    Unknown
}

/// <summary>
/// 인증 결과
/// </summary>
public class AuthenticationResult
{
    public bool Success { get; set; }
    public string UserId { get; set; }
    public string ErrorMessage { get; set; }
}

/// <summary>
/// 자동 로그인 통계
/// </summary>
[Serializable]
public class AutoLoginStats
{
    public bool IsEnabled;
    public bool IsInProgress;
    public DateTime LastAttemptTime;
    public AutoLoginResult LastResult;
    public int CurrentAttemptCount;
    public int MaxAttempts;
    public bool CanAttempt;
}
#endregion