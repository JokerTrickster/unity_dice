using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 토큰 관리자
/// 액세스 토큰과 리프레시 토큰의 생명주기를 관리하고 자동 갱신을 처리합니다.
/// </summary>
public class TokenManager : MonoBehaviour
{
    #region Singleton
    private static TokenManager _instance;
    public static TokenManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TokenManager");
                _instance = go.AddComponent<TokenManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Token Management Settings")]
    [SerializeField] private float tokenRefreshThreshold = 300f; // 5분
    [SerializeField] private int maxRefreshAttempts = 3;
    [SerializeField] private float refreshRetryDelay = 5f;
    [SerializeField] private bool autoRefreshEnabled = true;
    [SerializeField] private bool requireServerValidation = false;

    [Header("Security Settings")]
    [SerializeField] private bool deviceBindingEnabled = true;
    [SerializeField] private string jwtSecretKey = "";
    #endregion

    #region Events
    /// <summary>
    /// 토큰이 갱신되었을 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnTokenRefreshed;
    
    /// <summary>
    /// 토큰 갱신이 실패했을 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnTokenRefreshFailed;
    
    /// <summary>
    /// 토큰이 만료되었을 때 발생하는 이벤트
    /// </summary>
    public static event Action OnTokenExpired;
    
    /// <summary>
    /// 토큰이 저장되었을 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnTokenStored;
    #endregion

    #region Private Fields
    private TokenValidator _tokenValidator;
    private bool _isInitialized = false;
    private bool _isRefreshing = false;
    private DateTime _lastRefreshAttempt = DateTime.MinValue;
    private int _currentRefreshAttempts = 0;
    private System.Collections.Coroutine _autoRefreshCoroutine;
    #endregion

    #region Properties
    /// <summary>
    /// 토큰 관리자 초기화 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 현재 토큰 갱신 중 여부
    /// </summary>
    public bool IsRefreshing => _isRefreshing;

    /// <summary>
    /// 유효한 액세스 토큰이 있는지 여부
    /// </summary>
    public bool HasValidToken => TokenStorage.HasValidAccessToken;

    /// <summary>
    /// 현재 액세스 토큰
    /// </summary>
    public string CurrentAccessToken
    {
        get
        {
            var tokenInfo = TokenStorage.GetAccessToken();
            return tokenInfo?.Token;
        }
    }

    /// <summary>
    /// 토큰 만료 시간
    /// </summary>
    public DateTime? TokenExpirationTime
    {
        get
        {
            var tokenInfo = TokenStorage.GetAccessToken();
            return tokenInfo?.ExpirationTime;
        }
    }

    /// <summary>
    /// 자동 갱신 활성화 여부
    /// </summary>
    public bool AutoRefreshEnabled
    {
        get => autoRefreshEnabled;
        set
        {
            autoRefreshEnabled = value;
            if (_isInitialized)
            {
                if (value)
                    StartAutoRefresh();
                else
                    StopAutoRefresh();
            }
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
        InitializeTokenManager();
    }

    private void OnDestroy()
    {
        StopAutoRefresh();
        
        // 이벤트 구독 해제
        OnTokenRefreshed = null;
        OnTokenRefreshFailed = null;
        OnTokenExpired = null;
        OnTokenStored = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 토큰 관리자 초기화
    /// </summary>
    private void InitializeTokenManager()
    {
        try
        {
            // TokenStorage 초기화
            TokenStorage.Initialize();

            // TokenValidator 초기화
            _tokenValidator = new TokenValidator(
                tokenRefreshThreshold,
                requireServerValidation,
                jwtSecretKey
            );

            // 기존 토큰 유효성 검사 및 정리
            ValidateExistingTokens();

            // 자동 갱신 시작 (활성화된 경우)
            if (autoRefreshEnabled && HasValidToken)
            {
                StartAutoRefresh();
            }

            _isInitialized = true;
            Debug.Log("[TokenManager] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 기존 저장된 토큰들의 유효성 검사
    /// </summary>
    private void ValidateExistingTokens()
    {
        try
        {
            var validationResult = TokenStorage.ValidateStoredTokens();
            
            if (!validationResult.IsValid)
            {
                Debug.LogWarning($"[TokenManager] Stored tokens validation failed: {string.Join(", ", validationResult.Issues)}");
                
                // 액세스 토큰이 만료되었지만 리프레시 토큰이 유효한 경우 갱신 시도
                if (!validationResult.AccessTokenValid && validationResult.RefreshTokenValid)
                {
                    Debug.Log("[TokenManager] Access token expired but refresh token is valid, scheduling refresh");
                    _ = RefreshTokenAsync();
                }
            }
            else
            {
                Debug.Log("[TokenManager] Stored tokens are valid");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Failed to validate existing tokens: {ex.Message}");
        }
    }
    #endregion

    #region Token Storage Operations
    /// <summary>
    /// 토큰 저장
    /// </summary>
    /// <param name="accessToken">액세스 토큰</param>
    /// <param name="refreshToken">리프레시 토큰 (선택적)</param>
    /// <param name="userId">사용자 ID</param>
    /// <returns>저장 성공 여부</returns>
    public async Task<bool> StoreTokensAsync(string accessToken, string refreshToken = null, string userId = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[TokenManager] Not initialized");
            return false;
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("[TokenManager] Access token cannot be null or empty");
            return false;
        }

        try
        {
            // 토큰 유효성 사전 검증
            bool isValidToken = await _tokenValidator.ValidateTokenAsync(accessToken);
            if (!isValidToken)
            {
                Debug.LogError("[TokenManager] Cannot store invalid access token");
                return false;
            }

            // 디바이스 바인딩 검증 (활성화된 경우)
            if (deviceBindingEnabled && !string.IsNullOrEmpty(userId))
            {
                if (!ValidateDeviceBinding(userId))
                {
                    Debug.LogWarning("[TokenManager] Device binding validation failed, but proceeding with token storage");
                }
            }

            // TokenStorage를 통해 저장
            bool storeResult = TokenStorage.StoreTokens(accessToken, refreshToken, userId);
            
            if (storeResult)
            {
                Debug.Log("[TokenManager] Tokens stored successfully");
                OnTokenStored?.Invoke(accessToken);

                // 자동 갱신 시작 (활성화된 경우)
                if (autoRefreshEnabled)
                {
                    StartAutoRefresh();
                }

                return true;
            }
            else
            {
                Debug.LogError("[TokenManager] Failed to store tokens");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Failed to store tokens: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 현재 액세스 토큰 반환
    /// </summary>
    /// <param name="validateToken">토큰 유효성 검사 여부</param>
    /// <returns>액세스 토큰 (유효하지 않으면 null)</returns>
    public async Task<string> GetAccessTokenAsync(bool validateToken = true)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[TokenManager] Not initialized");
            return null;
        }

        try
        {
            var tokenInfo = TokenStorage.GetAccessToken();
            if (tokenInfo == null)
            {
                Debug.Log("[TokenManager] No access token found");
                return null;
            }

            // 토큰 유효성 검증 (요청된 경우)
            if (validateToken)
            {
                bool isValid = await _tokenValidator.ValidateTokenAsync(tokenInfo.Token);
                if (!isValid)
                {
                    Debug.LogWarning("[TokenManager] Stored access token is invalid");
                    
                    // 자동 갱신 시도
                    if (autoRefreshEnabled && TokenStorage.HasValidRefreshToken)
                    {
                        var refreshResult = await RefreshTokenAsync();
                        if (refreshResult.Success)
                        {
                            return refreshResult.AccessToken;
                        }
                    }
                    
                    return null;
                }
            }

            return tokenInfo.Token;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Failed to get access token: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 토큰 삭제
    /// </summary>
    public void ClearTokens()
    {
        try
        {
            StopAutoRefresh();
            TokenStorage.ClearAllTokens();
            
            Debug.Log("[TokenManager] All tokens cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Failed to clear tokens: {ex.Message}");
        }
    }
    #endregion

    #region Token Refresh
    /// <summary>
    /// 토큰 갱신 (비동기)
    /// </summary>
    /// <returns>갱신 결과</returns>
    public async Task<TokenRefreshResult> RefreshTokenAsync()
    {
        if (!_isInitialized)
        {
            return new TokenRefreshResult
            {
                Success = false,
                ErrorMessage = "TokenManager not initialized"
            };
        }

        if (_isRefreshing)
        {
            Debug.LogWarning("[TokenManager] Token refresh already in progress");
            return new TokenRefreshResult
            {
                Success = false,
                ErrorMessage = "Token refresh already in progress"
            };
        }

        // 최대 재시도 횟수 확인
        if (_currentRefreshAttempts >= maxRefreshAttempts)
        {
            Debug.LogError("[TokenManager] Maximum refresh attempts exceeded");
            return new TokenRefreshResult
            {
                Success = false,
                ErrorMessage = "Maximum refresh attempts exceeded"
            };
        }

        _isRefreshing = true;
        _lastRefreshAttempt = DateTime.UtcNow;
        _currentRefreshAttempts++;

        try
        {
            Debug.Log($"[TokenManager] Starting token refresh (attempt {_currentRefreshAttempts}/{maxRefreshAttempts})");

            // 리프레시 토큰 확인
            var refreshTokenInfo = TokenStorage.GetRefreshToken();
            if (refreshTokenInfo == null || string.IsNullOrEmpty(refreshTokenInfo.Token))
            {
                return new TokenRefreshResult
                {
                    Success = false,
                    ErrorMessage = "No refresh token available"
                };
            }

            // 리프레시 토큰 유효성 검사
            bool isRefreshTokenValid = await _tokenValidator.ValidateTokenAsync(refreshTokenInfo.Token);
            if (!isRefreshTokenValid)
            {
                return new TokenRefreshResult
                {
                    Success = false,
                    ErrorMessage = "Refresh token is invalid or expired"
                };
            }

            // 서버에 토큰 갱신 요청
            var refreshResult = await RequestTokenRefreshFromServer(refreshTokenInfo.Token);
            
            if (refreshResult.Success)
            {
                // 새 토큰 저장
                bool storeResult = TokenStorage.UpdateAccessToken(refreshResult.AccessToken);
                
                if (storeResult)
                {
                    Debug.Log("[TokenManager] Token refresh completed successfully");
                    _currentRefreshAttempts = 0; // 성공 시 카운터 리셋
                    
                    OnTokenRefreshed?.Invoke(refreshResult.AccessToken);
                    return refreshResult;
                }
                else
                {
                    return new TokenRefreshResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to store refreshed token"
                    };
                }
            }
            else
            {
                Debug.LogError($"[TokenManager] Token refresh failed: {refreshResult.ErrorMessage}");
                OnTokenRefreshFailed?.Invoke(refreshResult.ErrorMessage);
                return refreshResult;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Token refresh failed with exception: {ex.Message}");
            
            var result = new TokenRefreshResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
            
            OnTokenRefreshFailed?.Invoke(ex.Message);
            return result;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 서버에서 토큰 갱신 요청
    /// </summary>
    /// <param name="refreshToken">리프레시 토큰</param>
    /// <returns>갱신 결과</returns>
    private async Task<TokenRefreshResult> RequestTokenRefreshFromServer(string refreshToken)
    {
        try
        {
            var request = new TokenRefreshRequest
            {
                RefreshToken = refreshToken,
                DeviceFingerprint = deviceBindingEnabled ? CryptoHelper.GenerateDeviceFingerprint() : null
            };

            bool requestComplete = false;
            TokenRefreshResult result = null;

            // NetworkManager를 통해 토큰 갱신 요청
            NetworkManager.Instance.Post("/api/auth/refresh", request, response =>
            {
                if (response.IsSuccess)
                {
                    var refreshResponse = response.GetData<TokenRefreshResponse>();
                    if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                    {
                        result = new TokenRefreshResult
                        {
                            Success = true,
                            AccessToken = refreshResponse.AccessToken,
                            RefreshToken = refreshResponse.RefreshToken,
                            ExpiresIn = refreshResponse.ExpiresIn
                        };
                        
                        Debug.Log("[TokenManager] Token refresh server request successful");
                    }
                    else
                    {
                        result = new TokenRefreshResult
                        {
                            Success = false,
                            ErrorMessage = "Invalid server response format"
                        };
                    }
                }
                else
                {
                    result = new TokenRefreshResult
                    {
                        Success = false,
                        ErrorMessage = response.Error
                    };
                }
                
                requestComplete = true;
            });

            // 서버 응답 대기 (최대 30초)
            float elapsed = 0f;
            while (!requestComplete && elapsed < 30f)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }

            if (!requestComplete)
            {
                return new TokenRefreshResult
                {
                    Success = false,
                    ErrorMessage = "Token refresh request timeout"
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Server token refresh failed: {ex.Message}");
            return new TokenRefreshResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Auto Refresh
    /// <summary>
    /// 자동 갱신 시작
    /// </summary>
    private void StartAutoRefresh()
    {
        if (!autoRefreshEnabled || !HasValidToken)
            return;

        StopAutoRefresh(); // 기존 코루틴 중지
        _autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
        
        Debug.Log("[TokenManager] Auto refresh started");
    }

    /// <summary>
    /// 자동 갱신 중지
    /// </summary>
    private void StopAutoRefresh()
    {
        if (_autoRefreshCoroutine != null)
        {
            StopCoroutine(_autoRefreshCoroutine);
            _autoRefreshCoroutine = null;
            Debug.Log("[TokenManager] Auto refresh stopped");
        }
    }

    /// <summary>
    /// 자동 갱신 코루틴
    /// </summary>
    private System.Collections.IEnumerator AutoRefreshCoroutine()
    {
        while (autoRefreshEnabled && HasValidToken)
        {
            yield return new WaitForSeconds(30f); // 30초마다 확인

            try
            {
                var tokenInfo = TokenStorage.GetAccessToken();
                if (tokenInfo != null)
                {
                    // 토큰 만료 임박 확인
                    if (TokenStorage.IsTokenExpired(tokenInfo.ExpirationTime, (int)(tokenRefreshThreshold / 60)))
                    {
                        Debug.Log("[TokenManager] Token expires soon, starting auto refresh");
                        
                        var refreshTask = RefreshTokenAsync();
                        yield return new WaitUntil(() => refreshTask.IsCompleted);
                        
                        var refreshResult = refreshTask.Result;
                        if (!refreshResult.Success)
                        {
                            Debug.LogError($"[TokenManager] Auto refresh failed: {refreshResult.ErrorMessage}");
                            OnTokenExpired?.Invoke();
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[TokenManager] No access token found during auto refresh check");
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TokenManager] Auto refresh error: {ex.Message}");
                yield return new WaitForSeconds(refreshRetryDelay);
            }
        }
        
        Debug.Log("[TokenManager] Auto refresh coroutine ended");
    }
    #endregion

    #region Device Binding
    /// <summary>
    /// 디바이스 바인딩 검증
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>바인딩 유효 여부</returns>
    private bool ValidateDeviceBinding(string userId)
    {
        if (!deviceBindingEnabled)
            return true;

        try
        {
            return TokenStorage.ValidateDeviceBinding(userId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Device binding validation failed: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 토큰 상태 정보 반환
    /// </summary>
    /// <returns>토큰 상태 정보</returns>
    public TokenManagerStatus GetStatus()
    {
        try
        {
            var tokenInfo = TokenStorage.GetAccessToken();
            var refreshTokenInfo = TokenStorage.GetRefreshToken();
            var storageStats = TokenStorage.GetStorageStats();

            return new TokenManagerStatus
            {
                IsInitialized = _isInitialized,
                HasValidToken = HasValidToken,
                IsRefreshing = _isRefreshing,
                AutoRefreshEnabled = autoRefreshEnabled,
                TokenExpirationTime = tokenInfo?.ExpirationTime,
                RefreshTokenExpirationTime = refreshTokenInfo?.ExpirationTime,
                LastRefreshAttempt = _lastRefreshAttempt,
                RefreshAttemptCount = _currentRefreshAttempts,
                MaxRefreshAttempts = maxRefreshAttempts,
                StorageStats = storageStats
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenManager] Failed to get status: {ex.Message}");
            return new TokenManagerStatus { IsInitialized = false };
        }
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    /// <param name="refreshThreshold">갱신 임계값 (초)</param>
    /// <param name="maxAttempts">최대 재시도 횟수</param>
    /// <param name="retryDelay">재시도 지연 시간 (초)</param>
    public void UpdateSettings(float refreshThreshold, int maxAttempts, float retryDelay)
    {
        tokenRefreshThreshold = refreshThreshold;
        maxRefreshAttempts = maxAttempts;
        refreshRetryDelay = retryDelay;

        Debug.Log($"[TokenManager] Settings updated - Threshold: {refreshThreshold}s, Max attempts: {maxAttempts}, Retry delay: {retryDelay}s");
    }

    /// <summary>
    /// 갱신 시도 카운터 리셋
    /// </summary>
    public void ResetRefreshAttempts()
    {
        _currentRefreshAttempts = 0;
        Debug.Log("[TokenManager] Refresh attempt counter reset");
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 토큰 갱신 요청
/// </summary>
[Serializable]
public class TokenRefreshRequest
{
    public string RefreshToken;
    public string DeviceFingerprint;
}

/// <summary>
/// 토큰 갱신 응답
/// </summary>
[Serializable]
public class TokenRefreshResponse
{
    public string AccessToken;
    public string RefreshToken;
    public int ExpiresIn;
}

/// <summary>
/// 토큰 갱신 결과
/// </summary>
[Serializable]
public class TokenRefreshResult
{
    public bool Success;
    public string AccessToken;
    public string RefreshToken;
    public int ExpiresIn;
    public string ErrorMessage;
}

/// <summary>
/// 토큰 관리자 상태
/// </summary>
[Serializable]
public class TokenManagerStatus
{
    public bool IsInitialized;
    public bool HasValidToken;
    public bool IsRefreshing;
    public bool AutoRefreshEnabled;
    public DateTime? TokenExpirationTime;
    public DateTime? RefreshTokenExpirationTime;
    public DateTime LastRefreshAttempt;
    public int RefreshAttemptCount;
    public int MaxRefreshAttempts;
    public TokenStorageStats StorageStats;
}
#endregion