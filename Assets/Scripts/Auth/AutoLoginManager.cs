using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 통합 자동 로그인 관리자
/// 앱 시작 시와 백그라운드에서 seamless한 자동 로그인 경험을 제공합니다.
/// Stream A 컴포넌트(TokenManager, TokenStorage, TokenValidator)와 통합되어 작동합니다.
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
    [SerializeField] private float maxAuthenticationTime = 15f;
    [SerializeField] private int maxAutoLoginAttempts = 3;
    [SerializeField] private bool fallbackToManualLogin = true;
    [SerializeField] private bool showProgressFeedback = true;

    [Header("Integration Settings")]
    [SerializeField] private bool integrateWithGooglePlayGames = true;
    [SerializeField] private bool requireNetworkValidation = false;
    [SerializeField] private float retryDelay = 2f;
    #endregion

    #region Events
    /// <summary>
    /// 자동 로그인이 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action OnAutoLoginStarted;
    
    /// <summary>
    /// 자동 로그인이 완료될 때 발생하는 이벤트 (성공 여부)
    /// </summary>
    public static event Action<AutoLoginResult, string> OnAutoLoginCompleted;
    
    /// <summary>
    /// 자동 로그인 진행 상황 업데이트 이벤트
    /// </summary>
    public static event Action<string, float> OnAutoLoginProgress;

    /// <summary>
    /// 자동 로그인이 실패했을 때 수동 로그인으로 전환하는 이벤트
    /// </summary>
    public static event Action<AutoLoginResult> OnFallbackToManualLogin;
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isAutoLoginInProgress = false;
    private DateTime _lastAutoLoginAttempt = DateTime.MinValue;
    private int _currentAttemptCount = 0;
    private AutoLoginResult _lastResult = AutoLoginResult.Unknown;
    private Coroutine _autoLoginCoroutine;
    #endregion

    #region Properties
    /// <summary>
    /// 자동 로그인 활성화 여부
    /// </summary>
    public bool EnableAutoLogin
    {
        get => enableAutoLogin && GetAutoLoginPreference();
        set
        {
            enableAutoLogin = value;
            SetAutoLoginPreference(value);
        }
    }
    
    /// <summary>
    /// 자동 로그인 진행 중 여부
    /// </summary>
    public bool IsAutoLoginInProgress => _isAutoLoginInProgress;
    
    /// <summary>
    /// 마지막 자동 로그인 결과
    /// </summary>
    public AutoLoginResult LastResult => _lastResult;
    
    /// <summary>
    /// 현재 시도 횟수
    /// </summary>
    public int CurrentAttemptCount => _currentAttemptCount;
    
    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 자동 로그인이 가능한 상태인지 여부
    /// </summary>
    public bool CanAttemptAutoLogin
    {
        get
        {
            return _isInitialized &&
                   EnableAutoLogin &&
                   !_isAutoLoginInProgress &&
                   _currentAttemptCount < maxAutoLoginAttempts &&
                   TokenStorage.HasValidAccessToken;
        }
    }
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
        InitializeAutoLoginManager();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _isInitialized && CanAttemptAutoLogin)
        {
            // 마지막 시도로부터 충분한 시간이 지났는지 확인
            if ((DateTime.UtcNow - _lastAutoLoginAttempt).TotalMinutes >= 1)
            {
                StartCoroutine(DelayedAutoLogin(1f));
            }
        }
    }

    private void OnDestroy()
    {
        StopAutoLogin();
        
        // 이벤트 구독 해제
        OnAutoLoginStarted = null;
        OnAutoLoginCompleted = null;
        OnAutoLoginProgress = null;
        OnFallbackToManualLogin = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 자동 로그인 관리자 초기화
    /// </summary>
    private void InitializeAutoLoginManager()
    {
        try
        {
            Debug.Log("[AutoLoginManager] Initializing...");

            // TokenManager 초기화 대기
            if (!TokenManager.Instance.IsInitialized)
            {
                StartCoroutine(WaitForTokenManagerInit());
                return;
            }

            // AuthenticationManager와의 이벤트 통합
            if (AuthenticationManager.Instance != null)
            {
                AuthenticationManager.OnAuthenticationStateChanged += OnAuthStateChanged;
                AuthenticationManager.OnLoginSuccess += OnGooglePlayLoginSuccess;
                AuthenticationManager.OnLoginFailed += OnGooglePlayLoginFailed;
            }

            _isInitialized = true;
            Debug.Log("[AutoLoginManager] Initialized successfully");

            // 앱 시작 시 자동 로그인 시도 (조건이 맞으면)
            if (CanAttemptAutoLogin)
            {
                StartCoroutine(DelayedAutoLogin(2f)); // 2초 후 시도
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginManager] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// TokenManager 초기화 대기
    /// </summary>
    private IEnumerator WaitForTokenManagerInit()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!TokenManager.Instance.IsInitialized && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (TokenManager.Instance.IsInitialized)
        {
            InitializeAutoLoginManager();
        }
        else
        {
            Debug.LogError("[AutoLoginManager] TokenManager initialization timeout");
        }
    }
    #endregion

    #region Auto-Login Flow
    /// <summary>
    /// 자동 로그인 시도 (비동기)
    /// </summary>
    /// <returns>자동 로그인 결과</returns>
    public async Task<AutoLoginResult> TryAutoLoginAsync()
    {
        if (!CanAttemptAutoLogin)
        {
            var reason = GetAutoLoginBlockReason();
            Debug.Log($"[AutoLoginManager] Auto-login blocked: {reason}");
            return reason;
        }

        _isAutoLoginInProgress = true;
        _lastAutoLoginAttempt = DateTime.UtcNow;
        _currentAttemptCount++;

        OnAutoLoginStarted?.Invoke();
        UpdateProgress("자동 로그인을 시작합니다...", 0.1f);

        try
        {
            Debug.Log($"[AutoLoginManager] Starting auto-login attempt {_currentAttemptCount}/{maxAutoLoginAttempts}");

            // 1. 토큰 유효성 검사 및 갱신
            UpdateProgress("저장된 토큰을 확인하고 있습니다...", 0.2f);
            var tokenResult = await ValidateAndRefreshTokens();
            if (!tokenResult.Success)
            {
                Debug.LogWarning($"[AutoLoginManager] Token validation failed: {tokenResult.ErrorMessage}");
                return SetResult(tokenResult.Result);
            }

            // 2. Google Play Games 인증 (활성화된 경우)
            if (integrateWithGooglePlayGames)
            {
                UpdateProgress("Google Play Games 인증 중...", 0.5f);
                var gpgsResult = await AuthenticateWithGooglePlayGames();
                if (!gpgsResult.Success)
                {
                    Debug.LogWarning($"[AutoLoginManager] GPGS authentication failed: {gpgsResult.ErrorMessage}");
                    return SetResult(AutoLoginResult.AuthenticationFailed);
                }
            }

            // 3. 네트워크 검증 (필요한 경우)
            if (requireNetworkValidation)
            {
                UpdateProgress("서버 인증을 확인하고 있습니다...", 0.7f);
                var networkResult = await ValidateWithServer();
                if (!networkResult.Success)
                {
                    Debug.LogWarning($"[AutoLoginManager] Network validation failed: {networkResult.ErrorMessage}");
                    return SetResult(AutoLoginResult.NetworkError);
                }
            }

            // 4. 사용자 데이터 로드
            UpdateProgress("사용자 데이터를 로드하고 있습니다...", 0.9f);
            var userDataResult = await LoadUserData();
            if (!userDataResult.Success)
            {
                Debug.LogWarning($"[AutoLoginManager] User data loading failed: {userDataResult.ErrorMessage}");
                // 사용자 데이터 로드 실패는 치명적이지 않음
            }

            // 5. 완료
            UpdateProgress("자동 로그인이 완료되었습니다", 1.0f);
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
        }
    }

    /// <summary>
    /// 자동 로그인 시도 (코루틴 버전)
    /// </summary>
    public void TryAutoLogin()
    {
        if (_autoLoginCoroutine != null)
        {
            StopCoroutine(_autoLoginCoroutine);
        }
        _autoLoginCoroutine = StartCoroutine(TryAutoLoginCoroutine());
    }

    /// <summary>
    /// 자동 로그인 코루틴
    /// </summary>
    private IEnumerator TryAutoLoginCoroutine()
    {
        var task = TryAutoLoginAsync();
        
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
            Debug.LogError("[AutoLoginManager] Auto-login timed out in coroutine");
            result = AutoLoginResult.Timeout;
            _isAutoLoginInProgress = false;
        }

        // 결과 처리
        HandleAutoLoginResult(result);
    }

    /// <summary>
    /// 지연된 자동 로그인
    /// </summary>
    /// <param name="delay">지연 시간 (초)</param>
    private IEnumerator DelayedAutoLogin(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (CanAttemptAutoLogin)
        {
            TryAutoLogin();
        }
    }

    /// <summary>
    /// 자동 로그인 중단
    /// </summary>
    public void StopAutoLogin()
    {
        if (_autoLoginCoroutine != null)
        {
            StopCoroutine(_autoLoginCoroutine);
            _autoLoginCoroutine = null;
        }
        
        if (_isAutoLoginInProgress)
        {
            _isAutoLoginInProgress = false;
            SetResult(AutoLoginResult.UserCancelled);
        }
    }
    #endregion

    #region Token Management Integration
    /// <summary>
    /// 토큰 유효성 검사 및 갱신
    /// </summary>
    private async Task<AuthOperationResult> ValidateAndRefreshTokens()
    {
        try
        {
            // 현재 토큰 상태 확인
            if (!TokenStorage.HasValidAccessToken)
            {
                return new AuthOperationResult
                {
                    Success = false,
                    Result = AutoLoginResult.NoStoredCredentials,
                    ErrorMessage = "No valid access token found"
                };
            }

            // 토큰 유효성 검사
            var accessToken = await TokenManager.Instance.GetAccessTokenAsync(validateToken: true);
            if (string.IsNullOrEmpty(accessToken))
            {
                // 자동 갱신 시도
                if (TokenStorage.HasValidRefreshToken)
                {
                    var refreshResult = await TokenManager.Instance.RefreshTokenAsync();
                    if (refreshResult.Success)
                    {
                        Debug.Log("[AutoLoginManager] Token refreshed successfully during auto-login");
                        return new AuthOperationResult { Success = true };
                    }
                    else
                    {
                        return new AuthOperationResult
                        {
                            Success = false,
                            Result = AutoLoginResult.TokenRefreshFailed,
                            ErrorMessage = refreshResult.ErrorMessage
                        };
                    }
                }
                else
                {
                    return new AuthOperationResult
                    {
                        Success = false,
                        Result = AutoLoginResult.TokenExpired,
                        ErrorMessage = "No valid refresh token available"
                    };
                }
            }

            return new AuthOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AuthOperationResult
            {
                Success = false,
                Result = AutoLoginResult.Unknown,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Google Play Games Integration
    /// <summary>
    /// Google Play Games 인증
    /// </summary>
    private async Task<AuthOperationResult> AuthenticateWithGooglePlayGames()
    {
        try
        {
            if (AuthenticationManager.Instance == null)
            {
                return new AuthOperationResult
                {
                    Success = false,
                    ErrorMessage = "AuthenticationManager not available"
                };
            }

            // 이미 인증된 상태인지 확인
            if (AuthenticationManager.Instance.IsAuthenticated)
            {
                Debug.Log("[AutoLoginManager] Already authenticated with Google Play Games");
                return new AuthOperationResult { Success = true };
            }

            // 자동 로그인 시도
            bool loginCompleted = false;
            bool loginSuccess = false;
            string errorMessage = "";

            // 콜백 등록
            Action<bool> onAuthStateChanged = (success) =>
            {
                loginCompleted = true;
                loginSuccess = success;
            };

            Action<string> onLoginFailed = (error) =>
            {
                loginCompleted = true;
                loginSuccess = false;
                errorMessage = error;
            };

            AuthenticationManager.OnAuthenticationStateChanged += onAuthStateChanged;
            AuthenticationManager.OnLoginFailed += onLoginFailed;

            try
            {
                // 자동 로그인 트리거
                AuthenticationManager.Instance.TryAutoLogin();

                // 결과 대기 (타임아웃 포함)
                float timeout = 10f;
                float elapsed = 0f;
                
                while (!loginCompleted && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    await Task.Delay(100);
                }

                if (loginCompleted && loginSuccess)
                {
                    return new AuthOperationResult { Success = true };
                }
                else
                {
                    return new AuthOperationResult
                    {
                        Success = false,
                        ErrorMessage = loginCompleted ? errorMessage : "Google Play Games authentication timeout"
                    };
                }
            }
            finally
            {
                AuthenticationManager.OnAuthenticationStateChanged -= onAuthStateChanged;
                AuthenticationManager.OnLoginFailed -= onLoginFailed;
            }
        }
        catch (Exception ex)
        {
            return new AuthOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Network Validation
    /// <summary>
    /// 서버와 함께 토큰 검증
    /// </summary>
    private async Task<AuthOperationResult> ValidateWithServer()
    {
        try
        {
            // NetworkManager를 통한 서버 검증 로직 추가 예정
            // 현재는 로컬 검증만 수행
            await Task.Delay(500); // 네트워크 요청 시뮬레이션
            
            return new AuthOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AuthOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 사용자 데이터 로드
    /// </summary>
    private async Task<AuthOperationResult> LoadUserData()
    {
        try
        {
            // 사용자 데이터 로드 로직 (향후 구현)
            await Task.Delay(200); // 시뮬레이션
            
            return new AuthOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AuthOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 인증 상태 변경 이벤트 핸들러
    /// </summary>
    private void OnAuthStateChanged(bool isAuthenticated)
    {
        Debug.Log($"[AutoLoginManager] Auth state changed: {isAuthenticated}");
    }

    /// <summary>
    /// Google Play Games 로그인 성공 이벤트 핸들러
    /// </summary>
    private void OnGooglePlayLoginSuccess(GooglePlayGames.BasicApi.ILocalUser user)
    {
        Debug.Log($"[AutoLoginManager] Google Play Games login success: {user?.userName}");
    }

    /// <summary>
    /// Google Play Games 로그인 실패 이벤트 핸들러
    /// </summary>
    private void OnGooglePlayLoginFailed(string error)
    {
        Debug.LogWarning($"[AutoLoginManager] Google Play Games login failed: {error}");
    }
    #endregion

    #region Result Handling
    /// <summary>
    /// 자동 로그인 결과 설정
    /// </summary>
    private AutoLoginResult SetResult(AutoLoginResult result)
    {
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// 자동 로그인 결과 처리
    /// </summary>
    private void HandleAutoLoginResult(AutoLoginResult result)
    {
        string message = GetResultMessage(result);
        
        OnAutoLoginCompleted?.Invoke(result, message);

        if (result == AutoLoginResult.Success)
        {
            Debug.Log($"[AutoLoginManager] Auto-login succeeded: {message}");
        }
        else
        {
            Debug.LogWarning($"[AutoLoginManager] Auto-login failed: {message}");
            
            // Fallback 처리
            if (fallbackToManualLogin && ShouldFallbackToManualLogin(result))
            {
                OnFallbackToManualLogin?.Invoke(result);
            }
        }
    }

    /// <summary>
    /// 수동 로그인으로 전환해야 하는지 판단
    /// </summary>
    private bool ShouldFallbackToManualLogin(AutoLoginResult result)
    {
        return result switch
        {
            AutoLoginResult.NoStoredCredentials => true,
            AutoLoginResult.TokenExpired => true,
            AutoLoginResult.TokenRefreshFailed => true,
            AutoLoginResult.MaxAttemptsExceeded => true,
            AutoLoginResult.Disabled => true,
            _ => false
        };
    }

    /// <summary>
    /// 자동 로그인이 차단된 이유 반환
    /// </summary>
    private AutoLoginResult GetAutoLoginBlockReason()
    {
        if (!_isInitialized) return AutoLoginResult.Unknown;
        if (!EnableAutoLogin) return AutoLoginResult.Disabled;
        if (_isAutoLoginInProgress) return AutoLoginResult.Unknown; // 이미 진행 중
        if (_currentAttemptCount >= maxAutoLoginAttempts) return AutoLoginResult.MaxAttemptsExceeded;
        if (!TokenStorage.HasValidAccessToken) return AutoLoginResult.NoStoredCredentials;
        
        return AutoLoginResult.Unknown;
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

    #region Progress Updates
    /// <summary>
    /// 진행 상황 업데이트
    /// </summary>
    private void UpdateProgress(string message, float progress)
    {
        if (showProgressFeedback)
        {
            Debug.Log($"[AutoLoginManager] {message} ({progress * 100:F0}%)");
            OnAutoLoginProgress?.Invoke(message, progress);
        }
    }
    #endregion

    #region Settings Management
    /// <summary>
    /// 자동 로그인 설정 가져오기
    /// </summary>
    private bool GetAutoLoginPreference()
    {
        return PlayerPrefs.GetInt("AutoLoginEnabled", 1) == 1;
    }

    /// <summary>
    /// 자동 로그인 설정 저장
    /// </summary>
    private void SetAutoLoginPreference(bool enabled)
    {
        PlayerPrefs.SetInt("AutoLoginEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    #endregion

    #region Public API
    /// <summary>
    /// 현재 상태 정보 반환
    /// </summary>
    public AutoLoginStatus GetStatus()
    {
        return new AutoLoginStatus
        {
            IsInitialized = _isInitialized,
            IsEnabled = EnableAutoLogin,
            IsInProgress = _isAutoLoginInProgress,
            CanAttempt = CanAttemptAutoLogin,
            LastResult = _lastResult,
            CurrentAttemptCount = _currentAttemptCount,
            MaxAttempts = maxAutoLoginAttempts,
            LastAttemptTime = _lastAutoLoginAttempt,
            HasValidTokens = TokenStorage.HasValidAccessToken,
            TokenManagerReady = TokenManager.Instance.IsInitialized
        };
    }

    /// <summary>
    /// 자동 로그인 시도 횟수 리셋
    /// </summary>
    public void ResetAttemptCount()
    {
        _currentAttemptCount = 0;
        Debug.Log("[AutoLoginManager] Attempt count reset");
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(bool enableAutoLogin, float maxAuthTime, int maxAttempts)
    {
        this.enableAutoLogin = enableAutoLogin;
        this.maxAuthenticationTime = Mathf.Max(5f, maxAuthTime);
        this.maxAutoLoginAttempts = Mathf.Max(1, maxAttempts);
        
        SetAutoLoginPreference(enableAutoLogin);
        
        Debug.Log($"[AutoLoginManager] Settings updated - Enabled: {enableAutoLogin}, MaxTime: {maxAuthTime}s, MaxAttempts: {maxAttempts}");
    }
    #endregion
}

#region Data Classes
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
/// 인증 작업 결과
/// </summary>
public class AuthOperationResult
{
    public bool Success { get; set; }
    public AutoLoginResult Result { get; set; }
    public string ErrorMessage { get; set; }
}

/// <summary>
/// 자동 로그인 상태 정보
/// </summary>
[Serializable]
public class AutoLoginStatus
{
    public bool IsInitialized;
    public bool IsEnabled;
    public bool IsInProgress;
    public bool CanAttempt;
    public AutoLoginResult LastResult;
    public int CurrentAttemptCount;
    public int MaxAttempts;
    public DateTime LastAttemptTime;
    public bool HasValidTokens;
    public bool TokenManagerReady;
}
#endregion