using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 백그라운드 인증 처리기
/// 백그라운드에서 토큰 갱신, 인증 상태 모니터링, 무중단 인증 처리를 담당합니다.
/// </summary>
public class BackgroundAuthProcessor : MonoBehaviour
{
    #region Singleton
    private static BackgroundAuthProcessor _instance;
    public static BackgroundAuthProcessor Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("BackgroundAuthProcessor");
                _instance = go.AddComponent<BackgroundAuthProcessor>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Background Processing Configuration")]
    [SerializeField] private bool enableBackgroundProcessing = true;
    [SerializeField] private float tokenCheckInterval = 60f; // 1분마다 토큰 상태 확인
    [SerializeField] private float networkRetryInterval = 300f; // 5분마다 네트워크 재시도
    [SerializeField] private bool processOnAppFocus = true;
    [SerializeField] private bool silentProcessing = false;

    [Header("Token Management")]
    [SerializeField] private float tokenRefreshThreshold = 300f; // 5분 전 갱신
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryBackoffMultiplier = 1.5f;
    [SerializeField] private bool aggressiveRefresh = false;

    [Header("Network Monitoring")]
    [SerializeField] private bool monitorNetworkChanges = true;
    [SerializeField] private bool validateTokensOnNetworkRestore = true;
    [SerializeField] private float networkTimeoutSeconds = 10f;
    #endregion

    #region Events
    /// <summary>
    /// 백그라운드 처리가 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action<BackgroundProcessType> OnBackgroundProcessStarted;
    
    /// <summary>
    /// 백그라운드 처리가 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action<BackgroundProcessType, bool, string> OnBackgroundProcessCompleted;
    
    /// <summary>
    /// 토큰이 백그라운드에서 갱신될 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnTokenRefreshedInBackground;

    /// <summary>
    /// 백그라운드 인증 실패 시 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnBackgroundAuthenticationFailed;

    /// <summary>
    /// 네트워크 상태 변경 감지 이벤트
    /// </summary>
    public static event Action<bool> OnNetworkStatusChanged;
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isProcessing = false;
    private DateTime _lastTokenCheck = DateTime.MinValue;
    private DateTime _lastNetworkRetry = DateTime.MinValue;
    private NetworkReachability _lastNetworkStatus = NetworkReachability.NotReachable;
    
    private Coroutine _backgroundProcessingCoroutine;
    private Coroutine _networkMonitoringCoroutine;
    
    private int _consecutiveFailures = 0;
    private DateTime _lastProcessingAttempt = DateTime.MinValue;
    #endregion

    #region Properties
    /// <summary>
    /// 백그라운드 처리 활성화 여부
    /// </summary>
    public bool EnableBackgroundProcessing
    {
        get => enableBackgroundProcessing;
        set
        {
            enableBackgroundProcessing = value;
            if (_isInitialized)
            {
                if (value)
                    StartBackgroundProcessing();
                else
                    StopBackgroundProcessing();
            }
        }
    }
    
    /// <summary>
    /// 현재 처리 중 여부
    /// </summary>
    public bool IsProcessing => _isProcessing;
    
    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 마지막 토큰 확인 시간
    /// </summary>
    public DateTime LastTokenCheck => _lastTokenCheck;
    
    /// <summary>
    /// 현재 네트워크 상태
    /// </summary>
    public NetworkReachability CurrentNetworkStatus => Application.internetReachability;
    
    /// <summary>
    /// 연속 실패 횟수
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;
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
        InitializeBackgroundProcessor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && processOnAppFocus && _isInitialized)
        {
            StartCoroutine(DelayedFocusProcessing());
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && processOnAppFocus && _isInitialized)
        {
            StartCoroutine(DelayedFocusProcessing());
        }
    }

    private void OnDestroy()
    {
        StopAllProcessing();
        
        // 이벤트 구독 해제
        OnBackgroundProcessStarted = null;
        OnBackgroundProcessCompleted = null;
        OnTokenRefreshedInBackground = null;
        OnBackgroundAuthenticationFailed = null;
        OnNetworkStatusChanged = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 백그라운드 프로세서 초기화
    /// </summary>
    private void InitializeBackgroundProcessor()
    {
        try
        {
            Debug.Log("[BackgroundAuthProcessor] Initializing...");

            // 초기 네트워크 상태 저장
            _lastNetworkStatus = Application.internetReachability;
            
            // TokenManager와의 이벤트 연결
            if (TokenManager.Instance != null)
            {
                TokenManager.OnTokenRefreshed += OnTokenManagerRefreshed;
                TokenManager.OnTokenRefreshFailed += OnTokenManagerRefreshFailed;
                TokenManager.OnTokenExpired += OnTokenManagerExpired;
            }

            _isInitialized = true;
            Debug.Log("[BackgroundAuthProcessor] Initialized successfully");

            // 백그라운드 처리 시작
            if (enableBackgroundProcessing)
            {
                StartBackgroundProcessing();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BackgroundAuthProcessor] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }
    #endregion

    #region Background Processing
    /// <summary>
    /// 백그라운드 처리 시작
    /// </summary>
    private void StartBackgroundProcessing()
    {
        if (!_isInitialized || !enableBackgroundProcessing)
            return;

        StopBackgroundProcessing(); // 기존 코루틴 중지

        _backgroundProcessingCoroutine = StartCoroutine(BackgroundProcessingLoop());
        
        if (monitorNetworkChanges)
        {
            _networkMonitoringCoroutine = StartCoroutine(NetworkMonitoringLoop());
        }

        Debug.Log("[BackgroundAuthProcessor] Background processing started");
    }

    /// <summary>
    /// 백그라운드 처리 중지
    /// </summary>
    private void StopBackgroundProcessing()
    {
        if (_backgroundProcessingCoroutine != null)
        {
            StopCoroutine(_backgroundProcessingCoroutine);
            _backgroundProcessingCoroutine = null;
        }

        if (_networkMonitoringCoroutine != null)
        {
            StopCoroutine(_networkMonitoringCoroutine);
            _networkMonitoringCoroutine = null;
        }

        Debug.Log("[BackgroundAuthProcessor] Background processing stopped");
    }

    /// <summary>
    /// 모든 처리 중지
    /// </summary>
    private void StopAllProcessing()
    {
        StopBackgroundProcessing();
        _isProcessing = false;
    }

    /// <summary>
    /// 백그라운드 처리 루프
    /// </summary>
    private IEnumerator BackgroundProcessingLoop()
    {
        while (enableBackgroundProcessing && _isInitialized)
        {
            try
            {
                // 토큰 상태 확인 간격
                if ((DateTime.UtcNow - _lastTokenCheck).TotalSeconds >= tokenCheckInterval)
                {
                    yield return StartCoroutine(ProcessTokenValidationCoroutine());
                }

                // 네트워크 재시도 간격
                if ((DateTime.UtcNow - _lastNetworkRetry).TotalSeconds >= networkRetryInterval)
                {
                    yield return StartCoroutine(ProcessNetworkRetryCoroutine());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackgroundAuthProcessor] Background processing error: {ex.Message}");
            }

            // 다음 체크까지 대기
            yield return new WaitForSeconds(Math.Min(tokenCheckInterval, networkRetryInterval) / 4);
        }

        Debug.Log("[BackgroundAuthProcessor] Background processing loop ended");
    }

    /// <summary>
    /// 네트워크 모니터링 루프
    /// </summary>
    private IEnumerator NetworkMonitoringLoop()
    {
        while (monitorNetworkChanges && _isInitialized)
        {
            try
            {
                NetworkReachability currentStatus = Application.internetReachability;
                
                if (currentStatus != _lastNetworkStatus)
                {
                    Debug.Log($"[BackgroundAuthProcessor] Network status changed: {_lastNetworkStatus} -> {currentStatus}");
                    
                    OnNetworkStatusChanged?.Invoke(currentStatus != NetworkReachability.NotReachable);
                    
                    // 네트워크가 복구되면 토큰 검증
                    if (currentStatus != NetworkReachability.NotReachable && 
                        _lastNetworkStatus == NetworkReachability.NotReachable &&
                        validateTokensOnNetworkRestore)
                    {
                        yield return StartCoroutine(ProcessNetworkRestoreCoroutine());
                    }
                    
                    _lastNetworkStatus = currentStatus;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackgroundAuthProcessor] Network monitoring error: {ex.Message}");
            }

            yield return new WaitForSeconds(5f); // 5초마다 네트워크 상태 확인
        }
    }
    #endregion

    #region Processing Coroutines
    /// <summary>
    /// 토큰 검증 처리 코루틴
    /// </summary>
    private IEnumerator ProcessTokenValidationCoroutine()
    {
        if (_isProcessing)
            yield break;

        _isProcessing = true;
        _lastTokenCheck = DateTime.UtcNow;

        OnBackgroundProcessStarted?.Invoke(BackgroundProcessType.TokenValidation);
        
        if (!silentProcessing)
        {
            Debug.Log("[BackgroundAuthProcessor] Starting background token validation");
        }

        var task = ProcessTokenValidationAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        var result = task.Result;
        OnBackgroundProcessCompleted?.Invoke(BackgroundProcessType.TokenValidation, result.Success, result.ErrorMessage);

        if (result.Success)
        {
            _consecutiveFailures = 0;
        }
        else
        {
            _consecutiveFailures++;
            Debug.LogWarning($"[BackgroundAuthProcessor] Token validation failed: {result.ErrorMessage}");
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 네트워크 재시도 처리 코루틴
    /// </summary>
    private IEnumerator ProcessNetworkRetryCoroutine()
    {
        if (_isProcessing || Application.internetReachability == NetworkReachability.NotReachable)
            yield break;

        _isProcessing = true;
        _lastNetworkRetry = DateTime.UtcNow;

        OnBackgroundProcessStarted?.Invoke(BackgroundProcessType.NetworkRetry);
        
        if (!silentProcessing)
        {
            Debug.Log("[BackgroundAuthProcessor] Starting background network retry");
        }

        var task = ProcessNetworkRetryAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        var result = task.Result;
        OnBackgroundProcessCompleted?.Invoke(BackgroundProcessType.NetworkRetry, result.Success, result.ErrorMessage);

        _isProcessing = false;
    }

    /// <summary>
    /// 네트워크 복구 처리 코루틴
    /// </summary>
    private IEnumerator ProcessNetworkRestoreCoroutine()
    {
        if (_isProcessing)
            yield break;

        _isProcessing = true;

        OnBackgroundProcessStarted?.Invoke(BackgroundProcessType.NetworkRestore);
        
        Debug.Log("[BackgroundAuthProcessor] Processing network restore");

        // 잠시 대기 (네트워크 완전 복구 대기)
        yield return new WaitForSeconds(2f);

        var task = ProcessNetworkRestoreAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        var result = task.Result;
        OnBackgroundProcessCompleted?.Invoke(BackgroundProcessType.NetworkRestore, result.Success, result.ErrorMessage);

        _isProcessing = false;
    }

    /// <summary>
    /// 앱 포커스 시 지연 처리
    /// </summary>
    private IEnumerator DelayedFocusProcessing()
    {
        yield return new WaitForSeconds(1f);
        
        if (!_isProcessing)
        {
            yield return StartCoroutine(ProcessTokenValidationCoroutine());
        }
    }
    #endregion

    #region Async Processing Methods
    /// <summary>
    /// 토큰 검증 처리 (비동기)
    /// </summary>
    private async Task<BackgroundProcessResult> ProcessTokenValidationAsync()
    {
        try
        {
            if (!TokenManager.Instance.IsInitialized)
            {
                return new BackgroundProcessResult
                {
                    Success = false,
                    ErrorMessage = "TokenManager not initialized"
                };
            }

            // 현재 토큰 상태 확인
            var status = TokenManager.Instance.GetStatus();
            if (!status.HasValidToken)
            {
                if (!silentProcessing)
                {
                    Debug.Log("[BackgroundAuthProcessor] No valid token found during background check");
                }
                return new BackgroundProcessResult { Success = true };
            }

            // 토큰 만료 임박 확인
            if (status.TokenExpirationTime.HasValue)
            {
                var timeUntilExpiry = status.TokenExpirationTime.Value - DateTime.UtcNow;
                if (timeUntilExpiry.TotalSeconds <= tokenRefreshThreshold)
                {
                    if (!silentProcessing)
                    {
                        Debug.Log($"[BackgroundAuthProcessor] Token expires in {timeUntilExpiry.TotalSeconds:F0} seconds, refreshing...");
                    }

                    // 백그라운드에서 토큰 갱신
                    var refreshResult = await TokenManager.Instance.RefreshTokenAsync();
                    if (refreshResult.Success)
                    {
                        OnTokenRefreshedInBackground?.Invoke(refreshResult.AccessToken);
                        return new BackgroundProcessResult { Success = true };
                    }
                    else
                    {
                        OnBackgroundAuthenticationFailed?.Invoke(refreshResult.ErrorMessage);
                        return new BackgroundProcessResult
                        {
                            Success = false,
                            ErrorMessage = refreshResult.ErrorMessage
                        };
                    }
                }
            }

            return new BackgroundProcessResult { Success = true };
        }
        catch (Exception ex)
        {
            return new BackgroundProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 네트워크 재시도 처리 (비동기)
    /// </summary>
    private async Task<BackgroundProcessResult> ProcessNetworkRetryAsync()
    {
        try
        {
            // 연속 실패가 있었다면 재시도
            if (_consecutiveFailures > 0)
            {
                if (!silentProcessing)
                {
                    Debug.Log($"[BackgroundAuthProcessor] Retrying after {_consecutiveFailures} failures");
                }

                return await ProcessTokenValidationAsync();
            }

            return new BackgroundProcessResult { Success = true };
        }
        catch (Exception ex)
        {
            return new BackgroundProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 네트워크 복구 처리 (비동기)
    /// </summary>
    private async Task<BackgroundProcessResult> ProcessNetworkRestoreAsync()
    {
        try
        {
            // 네트워크 복구 시 토큰 상태 재검증
            var validationResult = await ProcessTokenValidationAsync();
            
            if (validationResult.Success)
            {
                Debug.Log("[BackgroundAuthProcessor] Network restore validation completed successfully");
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            return new BackgroundProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// TokenManager 토큰 갱신 이벤트 핸들러
    /// </summary>
    private void OnTokenManagerRefreshed(string accessToken)
    {
        if (!silentProcessing)
        {
            Debug.Log("[BackgroundAuthProcessor] Token refreshed by TokenManager");
        }
        _consecutiveFailures = 0;
    }

    /// <summary>
    /// TokenManager 토큰 갱신 실패 이벤트 핸들러
    /// </summary>
    private void OnTokenManagerRefreshFailed(string error)
    {
        Debug.LogWarning($"[BackgroundAuthProcessor] Token refresh failed in TokenManager: {error}");
        _consecutiveFailures++;
        OnBackgroundAuthenticationFailed?.Invoke(error);
    }

    /// <summary>
    /// TokenManager 토큰 만료 이벤트 핸들러
    /// </summary>
    private void OnTokenManagerExpired()
    {
        Debug.LogWarning("[BackgroundAuthProcessor] Token expired in TokenManager");
        
        // 즉시 갱신 시도
        if (enableBackgroundProcessing && !_isProcessing)
        {
            StartCoroutine(ProcessTokenValidationCoroutine());
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 즉시 토큰 검증 실행
    /// </summary>
    public void TriggerTokenValidation()
    {
        if (!_isInitialized || _isProcessing)
        {
            Debug.LogWarning("[BackgroundAuthProcessor] Cannot trigger validation - not ready or already processing");
            return;
        }

        StartCoroutine(ProcessTokenValidationCoroutine());
    }

    /// <summary>
    /// 백그라운드 처리 상태 정보 반환
    /// </summary>
    public BackgroundProcessorStatus GetStatus()
    {
        return new BackgroundProcessorStatus
        {
            IsInitialized = _isInitialized,
            IsEnabled = enableBackgroundProcessing,
            IsProcessing = _isProcessing,
            LastTokenCheck = _lastTokenCheck,
            LastNetworkRetry = _lastNetworkRetry,
            ConsecutiveFailures = _consecutiveFailures,
            CurrentNetworkStatus = CurrentNetworkStatus,
            TokenCheckInterval = tokenCheckInterval,
            NetworkRetryInterval = networkRetryInterval
        };
    }

    /// <summary>
    /// 연속 실패 카운터 리셋
    /// </summary>
    public void ResetFailureCount()
    {
        _consecutiveFailures = 0;
        Debug.Log("[BackgroundAuthProcessor] Failure count reset");
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(float tokenCheckInterval, float networkRetryInterval, float tokenRefreshThreshold)
    {
        this.tokenCheckInterval = Mathf.Max(10f, tokenCheckInterval);
        this.networkRetryInterval = Mathf.Max(60f, networkRetryInterval);
        this.tokenRefreshThreshold = Mathf.Max(60f, tokenRefreshThreshold);
        
        Debug.Log($"[BackgroundAuthProcessor] Settings updated - TokenCheck: {this.tokenCheckInterval}s, NetworkRetry: {this.networkRetryInterval}s, RefreshThreshold: {this.tokenRefreshThreshold}s");
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 백그라운드 처리 타입
/// </summary>
public enum BackgroundProcessType
{
    TokenValidation,
    TokenRefresh,
    NetworkRetry,
    NetworkRestore,
    PeriodicCheck
}

/// <summary>
/// 백그라운드 처리 결과
/// </summary>
public class BackgroundProcessResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 백그라운드 프로세서 상태 정보
/// </summary>
[Serializable]
public class BackgroundProcessorStatus
{
    public bool IsInitialized;
    public bool IsEnabled;
    public bool IsProcessing;
    public DateTime LastTokenCheck;
    public DateTime LastNetworkRetry;
    public int ConsecutiveFailures;
    public NetworkReachability CurrentNetworkStatus;
    public float TokenCheckInterval;
    public float NetworkRetryInterval;
}
#endregion