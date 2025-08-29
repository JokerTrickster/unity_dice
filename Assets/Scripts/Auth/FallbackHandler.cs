using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 로그인 실패 시 폴백(Fallback) 처리를 담당하는 클래스
/// 다양한 실패 시나리오에 대해 사용자 친화적인 복구 방안을 제공합니다.
/// Stream A/B 컴포넌트와 연동하여 seamless한 사용자 경험을 제공합니다.
/// </summary>
public class FallbackHandler : MonoBehaviour
{
    #region Singleton
    private static FallbackHandler _instance;
    public static FallbackHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FallbackHandler");
                _instance = go.AddComponent<FallbackHandler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Fallback Configuration")]
    [SerializeField] private bool enableFallbackMechanisms = true;
    [SerializeField] private float fallbackTransitionDelay = 1.5f;
    [SerializeField] private bool showFallbackNotifications = true;
    [SerializeField] private bool enableRetryMechanisms = true;
    [SerializeField] private int maxRetryAttempts = 2;
    [SerializeField] private float retryDelay = 3f;

    [Header("UI Integration")]
    [SerializeField] private bool transitionToManualLogin = true;
    [SerializeField] private bool showErrorDialog = true;
    [SerializeField] private bool enableOfflineMode = true;
    [SerializeField] private bool fallbackToGuestMode = false;

    [Header("Recovery Options")]
    [SerializeField] private bool enableTokenRecovery = true;
    [SerializeField] private bool enableNetworkRetry = true;
    [SerializeField] private bool enableCredentialReset = true;
    [SerializeField] private bool enableDiagnosticMode = false;
    #endregion

    #region Events
    /// <summary>
    /// 폴백이 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action<AutoLoginResult, FallbackStrategy> OnFallbackStarted;

    /// <summary>
    /// 폴백이 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action<FallbackResult> OnFallbackCompleted;

    /// <summary>
    /// 폴백 진행 상황 업데이트 이벤트
    /// </summary>
    public static event Action<string, float> OnFallbackProgress;

    /// <summary>
    /// 재시도가 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action<int, int> OnRetryAttemptStarted;

    /// <summary>
    /// 사용자에게 수동 액션이 필요할 때 발생하는 이벤트
    /// </summary>
    public static event Action<UserActionRequired> OnUserActionRequired;
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isFallbackInProgress = false;
    private FallbackStrategy _currentStrategy = FallbackStrategy.None;
    private AutoLoginResult _lastFailureReason = AutoLoginResult.Unknown;
    private int _currentRetryAttempt = 0;
    private Dictionary<AutoLoginResult, FallbackStrategy> _fallbackStrategies;
    private Queue<FallbackAction> _fallbackQueue;
    private Coroutine _fallbackCoroutine;
    #endregion

    #region Properties
    /// <summary>
    /// 폴백 핸들러 초기화 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 폴백 처리 중 여부
    /// </summary>
    public bool IsFallbackInProgress => _isFallbackInProgress;

    /// <summary>
    /// 현재 폴백 전략
    /// </summary>
    public FallbackStrategy CurrentStrategy => _currentStrategy;

    /// <summary>
    /// 마지막 실패 이유
    /// </summary>
    public AutoLoginResult LastFailureReason => _lastFailureReason;

    /// <summary>
    /// 폴백 메커니즘 활성화 여부
    /// </summary>
    public bool EnableFallbackMechanisms
    {
        get => enableFallbackMechanisms;
        set => enableFallbackMechanisms = value;
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
        InitializeFallbackHandler();
    }

    private void OnDestroy()
    {
        StopFallback();
        
        // 이벤트 구독 해제
        OnFallbackStarted = null;
        OnFallbackCompleted = null;
        OnFallbackProgress = null;
        OnRetryAttemptStarted = null;
        OnUserActionRequired = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 폴백 핸들러 초기화
    /// </summary>
    private void InitializeFallbackHandler()
    {
        try
        {
            Debug.Log("[FallbackHandler] Initializing...");

            // 폴백 전략 맵 초기화
            InitializeFallbackStrategies();

            // 폴백 큐 초기화
            _fallbackQueue = new Queue<FallbackAction>();

            // AutoLoginManager 이벤트 구독
            if (AutoLoginManager.Instance != null)
            {
                AutoLoginManager.OnAutoLoginCompleted += OnAutoLoginCompleted;
                AutoLoginManager.OnFallbackToManualLogin += OnAutoLoginFallbackRequested;
            }

            // AuthenticationManager 이벤트 구독
            if (AuthenticationManager.Instance != null)
            {
                AuthenticationManager.OnLoginFailed += OnAuthenticationFailed;
            }

            _isInitialized = true;
            Debug.Log("[FallbackHandler] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FallbackHandler] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 폴백 전략 맵 초기화
    /// </summary>
    private void InitializeFallbackStrategies()
    {
        _fallbackStrategies = new Dictionary<AutoLoginResult, FallbackStrategy>
        {
            { AutoLoginResult.NoStoredCredentials, FallbackStrategy.TransitionToManualLogin },
            { AutoLoginResult.TokenExpired, FallbackStrategy.AttemptTokenRecovery },
            { AutoLoginResult.TokenRefreshFailed, FallbackStrategy.ClearCredentialsAndRetry },
            { AutoLoginResult.AuthenticationFailed, FallbackStrategy.RetryWithDelay },
            { AutoLoginResult.NetworkError, FallbackStrategy.EnableOfflineMode },
            { AutoLoginResult.Timeout, FallbackStrategy.RetryWithDelay },
            { AutoLoginResult.MaxAttemptsExceeded, FallbackStrategy.TransitionToManualLogin },
            { AutoLoginResult.Disabled, FallbackStrategy.TransitionToManualLogin },
            { AutoLoginResult.UserCancelled, FallbackStrategy.TransitionToManualLogin },
            { AutoLoginResult.Unknown, FallbackStrategy.ShowErrorAndRetry }
        };
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 자동 로그인 완료 이벤트 핸들러
    /// </summary>
    private void OnAutoLoginCompleted(AutoLoginResult result, string message)
    {
        if (result != AutoLoginResult.Success && enableFallbackMechanisms)
        {
            Debug.Log($"[FallbackHandler] Auto-login failed: {result}, initiating fallback");
            StartFallback(result);
        }
    }

    /// <summary>
    /// 자동 로그인에서 수동 로그인 폴백 요청 이벤트 핸들러
    /// </summary>
    private void OnAutoLoginFallbackRequested(AutoLoginResult result)
    {
        if (enableFallbackMechanisms)
        {
            Debug.Log($"[FallbackHandler] Manual login fallback requested: {result}");
            StartFallback(result, FallbackStrategy.TransitionToManualLogin);
        }
    }

    /// <summary>
    /// 인증 실패 이벤트 핸들러
    /// </summary>
    private void OnAuthenticationFailed(string error)
    {
        if (enableFallbackMechanisms && !_isFallbackInProgress)
        {
            Debug.Log($"[FallbackHandler] Authentication failed: {error}, checking for fallback");
            StartFallback(AutoLoginResult.AuthenticationFailed);
        }
    }
    #endregion

    #region Fallback Main Flow
    /// <summary>
    /// 폴백 처리 시작
    /// </summary>
    /// <param name="failureReason">실패 이유</param>
    /// <param name="overrideStrategy">강제 전략 (선택적)</param>
    public void StartFallback(AutoLoginResult failureReason, FallbackStrategy overrideStrategy = FallbackStrategy.None)
    {
        if (!enableFallbackMechanisms)
        {
            Debug.Log("[FallbackHandler] Fallback mechanisms are disabled");
            return;
        }

        if (_isFallbackInProgress)
        {
            Debug.LogWarning("[FallbackHandler] Fallback already in progress, ignoring new request");
            return;
        }

        _lastFailureReason = failureReason;
        _isFallbackInProgress = true;
        _currentRetryAttempt = 0;

        // 전략 결정
        if (overrideStrategy != FallbackStrategy.None)
        {
            _currentStrategy = overrideStrategy;
        }
        else if (_fallbackStrategies.TryGetValue(failureReason, out var strategy))
        {
            _currentStrategy = strategy;
        }
        else
        {
            _currentStrategy = FallbackStrategy.ShowErrorAndRetry;
        }

        Debug.Log($"[FallbackHandler] Starting fallback for {failureReason} with strategy {_currentStrategy}");
        OnFallbackStarted?.Invoke(failureReason, _currentStrategy);

        // 폴백 실행
        if (_fallbackCoroutine != null)
        {
            StopCoroutine(_fallbackCoroutine);
        }
        _fallbackCoroutine = StartCoroutine(ExecuteFallbackCoroutine());
    }

    /// <summary>
    /// 폴백 실행 코루틴
    /// </summary>
    private IEnumerator ExecuteFallbackCoroutine()
    {
        UpdateProgress("폴백 처리를 시작합니다...", 0.1f);

        try
        {
            // 지연 시간 적용
            if (fallbackTransitionDelay > 0)
            {
                yield return new WaitForSeconds(fallbackTransitionDelay);
            }

            // 전략별 처리 실행
            var result = yield return StartCoroutine(ExecuteFallbackStrategy());

            // 결과 처리
            HandleFallbackResult(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FallbackHandler] Fallback execution failed: {ex.Message}");
            var errorResult = new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = ex.Message,
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
            HandleFallbackResult(errorResult);
        }
        finally
        {
            _isFallbackInProgress = false;
            UpdateProgress("폴백 처리가 완료되었습니다.", 1.0f);
        }
    }

    /// <summary>
    /// 폴백 전략 실행
    /// </summary>
    private IEnumerator ExecuteFallbackStrategy()
    {
        switch (_currentStrategy)
        {
            case FallbackStrategy.RetryWithDelay:
                yield return StartCoroutine(ExecuteRetryWithDelay());
                break;

            case FallbackStrategy.AttemptTokenRecovery:
                yield return StartCoroutine(ExecuteTokenRecovery());
                break;

            case FallbackStrategy.ClearCredentialsAndRetry:
                yield return StartCoroutine(ExecuteClearCredentialsAndRetry());
                break;

            case FallbackStrategy.TransitionToManualLogin:
                yield return StartCoroutine(ExecuteTransitionToManualLogin());
                break;

            case FallbackStrategy.EnableOfflineMode:
                yield return StartCoroutine(ExecuteOfflineMode());
                break;

            case FallbackStrategy.FallbackToGuestMode:
                yield return StartCoroutine(ExecuteGuestMode());
                break;

            case FallbackStrategy.ShowErrorAndRetry:
                yield return StartCoroutine(ExecuteShowErrorAndRetry());
                break;

            default:
                yield return StartCoroutine(ExecuteDefaultFallback());
                break;
        }
    }

    /// <summary>
    /// 폴백 중단
    /// </summary>
    public void StopFallback()
    {
        if (_fallbackCoroutine != null)
        {
            StopCoroutine(_fallbackCoroutine);
            _fallbackCoroutine = null;
        }

        if (_isFallbackInProgress)
        {
            _isFallbackInProgress = false;
            _currentStrategy = FallbackStrategy.None;
            
            var result = new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = "Fallback cancelled by user",
                RequiresUserAction = false
            };
            
            OnFallbackCompleted?.Invoke(result);
        }
    }
    #endregion

    #region Fallback Strategy Implementations

    /// <summary>
    /// 지연 후 재시도 전략 실행
    /// </summary>
    private IEnumerator ExecuteRetryWithDelay()
    {
        if (!enableRetryMechanisms)
        {
            yield return new FallbackResult 
            { 
                Success = false, 
                Strategy = _currentStrategy,
                ErrorMessage = "Retry mechanisms are disabled",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ManualLogin
            };
            yield break;
        }

        var maxAttempts = Mathf.Min(maxRetryAttempts, AutoLoginSettings.Instance.MaxRetryAttempts);
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _currentRetryAttempt = attempt;
            UpdateProgress($"재시도 중... ({attempt}/{maxAttempts})", 0.3f + (0.4f * attempt / maxAttempts));
            OnRetryAttemptStarted?.Invoke(attempt, maxAttempts);

            yield return new WaitForSeconds(retryDelay);

            // AutoLoginManager를 통해 재시도
            if (AutoLoginManager.Instance.CanAttemptAutoLogin)
            {
                var retryTask = AutoLoginManager.Instance.TryAutoLoginAsync();
                yield return new WaitUntil(() => retryTask.IsCompleted);

                if (retryTask.Result == AutoLoginResult.Success)
                {
                    yield return new FallbackResult 
                    { 
                        Success = true, 
                        Strategy = _currentStrategy,
                        Message = $"자동 로그인이 재시도 {attempt}번째에서 성공했습니다."
                    };
                    yield break;
                }
            }
        }

        // 모든 재시도 실패
        yield return new FallbackResult
        {
            Success = false,
            Strategy = _currentStrategy,
            ErrorMessage = $"All {maxAttempts} retry attempts failed",
            RequiresUserAction = true,
            UserActionType = UserActionRequired.ManualLogin
        };
    }

    /// <summary>
    /// 토큰 복구 전략 실행
    /// </summary>
    private IEnumerator ExecuteTokenRecovery()
    {
        if (!enableTokenRecovery)
        {
            yield return new FallbackResult 
            { 
                Success = false, 
                Strategy = _currentStrategy,
                ErrorMessage = "Token recovery is disabled"
            };
            yield break;
        }

        UpdateProgress("토큰 복구를 시도합니다...", 0.4f);

        try
        {
            // 리프레시 토큰을 이용한 복구 시도
            if (TokenStorage.HasValidRefreshToken)
            {
                var refreshTask = TokenManager.Instance.RefreshTokenAsync();
                yield return new WaitUntil(() => refreshTask.IsCompleted);

                if (refreshTask.Result.Success)
                {
                    UpdateProgress("토큰이 성공적으로 복구되었습니다.", 0.8f);
                    
                    // 복구된 토큰으로 자동 로그인 재시도
                    var loginTask = AutoLoginManager.Instance.TryAutoLoginAsync();
                    yield return new WaitUntil(() => loginTask.IsCompleted);

                    if (loginTask.Result == AutoLoginResult.Success)
                    {
                        yield return new FallbackResult 
                        { 
                            Success = true, 
                            Strategy = _currentStrategy,
                            Message = "토큰 복구 후 자동 로그인이 성공했습니다."
                        };
                        yield break;
                    }
                }
            }

            // 토큰 복구 실패 - 수동 로그인으로 전환
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = "Token recovery failed",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ManualLogin
            };
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Token recovery error: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ManualLogin
            };
        }
    }

    /// <summary>
    /// 자격 증명 정리 후 재시도 전략 실행
    /// </summary>
    private IEnumerator ExecuteClearCredentialsAndRetry()
    {
        UpdateProgress("저장된 자격 증명을 정리합니다...", 0.3f);

        try
        {
            // 토큰 정리
            TokenManager.Instance.ClearTokens();
            AutoLoginPrefs.ResetAllPreferences();

            yield return new WaitForSeconds(1f);
            UpdateProgress("자격 증명이 정리되었습니다. 수동 로그인으로 이동합니다.", 0.7f);

            // 수동 로그인으로 전환
            yield return new FallbackResult
            {
                Success = true,
                Strategy = _currentStrategy,
                Message = "자격 증명을 정리했습니다. 수동 로그인을 진행해주세요.",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ManualLogin
            };
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Credential clearing failed: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
        }
    }

    /// <summary>
    /// 수동 로그인으로 전환 전략 실행
    /// </summary>
    private IEnumerator ExecuteTransitionToManualLogin()
    {
        UpdateProgress("수동 로그인 화면으로 이동합니다...", 0.5f);

        try
        {
            yield return new WaitForSeconds(0.5f);

            var result = new FallbackResult
            {
                Success = true,
                Strategy = _currentStrategy,
                Message = GetManualLoginMessage(),
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ManualLogin
            };

            yield return result;
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Transition to manual login failed: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
        }
    }

    /// <summary>
    /// 오프라인 모드 전략 실행
    /// </summary>
    private IEnumerator ExecuteOfflineMode()
    {
        if (!enableOfflineMode)
        {
            yield return new FallbackResult 
            { 
                Success = false, 
                Strategy = _currentStrategy,
                ErrorMessage = "Offline mode is disabled"
            };
            yield break;
        }

        UpdateProgress("오프라인 모드로 전환합니다...", 0.6f);

        try
        {
            yield return new WaitForSeconds(1f);

            // 오프라인 모드 설정
            var result = new FallbackResult
            {
                Success = true,
                Strategy = _currentStrategy,
                Message = "네트워크 연결 없이 제한된 기능으로 사용할 수 있습니다.",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.EnableOfflineMode
            };

            yield return result;
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Offline mode setup failed: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
        }
    }

    /// <summary>
    /// 게스트 모드 전략 실행
    /// </summary>
    private IEnumerator ExecuteGuestMode()
    {
        if (!fallbackToGuestMode)
        {
            yield return new FallbackResult 
            { 
                Success = false, 
                Strategy = _currentStrategy,
                ErrorMessage = "Guest mode is disabled"
            };
            yield break;
        }

        UpdateProgress("게스트 모드로 진입합니다...", 0.6f);

        try
        {
            yield return new WaitForSeconds(1f);

            var result = new FallbackResult
            {
                Success = true,
                Strategy = _currentStrategy,
                Message = "게스트로 제한된 기능을 사용할 수 있습니다. 나중에 로그인할 수 있습니다.",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.EnableGuestMode
            };

            yield return result;
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Guest mode setup failed: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
        }
    }

    /// <summary>
    /// 오류 표시 후 재시도 전략 실행
    /// </summary>
    private IEnumerator ExecuteShowErrorAndRetry()
    {
        UpdateProgress("오류 정보를 표시합니다...", 0.4f);

        try
        {
            var errorMessage = GetDetailedErrorMessage(_lastFailureReason);
            
            yield return new WaitForSeconds(1f);

            var result = new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = errorMessage,
                RequiresUserAction = true,
                UserActionType = showErrorDialog ? UserActionRequired.ShowError : UserActionRequired.ManualLogin,
                AdditionalData = new Dictionary<string, object>
                {
                    ["OriginalError"] = _lastFailureReason,
                    ["CanRetry"] = enableRetryMechanisms && _currentRetryAttempt < maxRetryAttempts
                }
            };

            yield return result;
        }
        catch (Exception ex)
        {
            yield return new FallbackResult
            {
                Success = false,
                Strategy = _currentStrategy,
                ErrorMessage = $"Error display failed: {ex.Message}",
                RequiresUserAction = true,
                UserActionType = UserActionRequired.ShowError
            };
        }
    }

    /// <summary>
    /// 기본 폴백 전략 실행
    /// </summary>
    private IEnumerator ExecuteDefaultFallback()
    {
        UpdateProgress("기본 복구 절차를 수행합니다...", 0.5f);

        yield return new WaitForSeconds(1f);

        yield return new FallbackResult
        {
            Success = false,
            Strategy = FallbackStrategy.TransitionToManualLogin,
            Message = "알 수 없는 오류가 발생했습니다. 수동 로그인으로 진행해주세요.",
            RequiresUserAction = true,
            UserActionType = UserActionRequired.ManualLogin
        };
    }
    #endregion

    #region Result Handling
    /// <summary>
    /// 폴백 결과 처리
    /// </summary>
    private void HandleFallbackResult(FallbackResult result)
    {
        if (result.Success)
        {
            Debug.Log($"[FallbackHandler] Fallback succeeded: {result.Message}");
        }
        else
        {
            Debug.LogWarning($"[FallbackHandler] Fallback failed: {result.ErrorMessage}");
        }

        // 사용자 액션 필요 시 이벤트 발생
        if (result.RequiresUserAction)
        {
            OnUserActionRequired?.Invoke(result.UserActionType);
        }

        OnFallbackCompleted?.Invoke(result);
    }

    /// <summary>
    /// 수동 로그인 메시지 생성
    /// </summary>
    private string GetManualLoginMessage()
    {
        return _lastFailureReason switch
        {
            AutoLoginResult.NoStoredCredentials => "저장된 로그인 정보가 없습니다. 로그인해주세요.",
            AutoLoginResult.TokenExpired => "로그인 세션이 만료되었습니다. 다시 로그인해주세요.",
            AutoLoginResult.TokenRefreshFailed => "세션 갱신에 실패했습니다. 로그인해주세요.",
            AutoLoginResult.MaxAttemptsExceeded => "자동 로그인 시도 횟수를 초과했습니다. 수동으로 로그인해주세요.",
            AutoLoginResult.Disabled => "자동 로그인이 비활성화되어 있습니다.",
            _ => "자동 로그인에 실패했습니다. 수동으로 로그인해주세요."
        };
    }

    /// <summary>
    /// 상세한 오류 메시지 생성
    /// </summary>
    private string GetDetailedErrorMessage(AutoLoginResult failureReason)
    {
        return failureReason switch
        {
            AutoLoginResult.NetworkError => "네트워크 연결을 확인하고 다시 시도해주세요. 계속해서 문제가 발생하면 Wi-Fi 또는 모바일 데이터 연결을 확인해주세요.",
            AutoLoginResult.Timeout => "로그인 시간이 초과되었습니다. 네트워크 상태를 확인하고 다시 시도해주세요.",
            AutoLoginResult.AuthenticationFailed => "인증에 실패했습니다. 계정 정보를 확인하고 다시 로그인해주세요.",
            AutoLoginResult.TokenRefreshFailed => "로그인 세션 갱신에 실패했습니다. 다시 로그인해주세요.",
            AutoLoginResult.Unknown => "알 수 없는 오류가 발생했습니다. 앱을 재시작하거나 다시 로그인해주세요.",
            _ => $"로그인 중 문제가 발생했습니다: {failureReason}"
        };
    }
    #endregion

    #region Progress Updates
    /// <summary>
    /// 진행 상황 업데이트
    /// </summary>
    private void UpdateProgress(string message, float progress)
    {
        if (showFallbackNotifications)
        {
            Debug.Log($"[FallbackHandler] {message} ({progress * 100:F0}%)");
            OnFallbackProgress?.Invoke(message, progress);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 현재 폴백 상태 정보 반환
    /// </summary>
    public FallbackStatus GetStatus()
    {
        return new FallbackStatus
        {
            IsInitialized = _isInitialized,
            IsFallbackInProgress = _isFallbackInProgress,
            CurrentStrategy = _currentStrategy,
            LastFailureReason = _lastFailureReason,
            CurrentRetryAttempt = _currentRetryAttempt,
            MaxRetryAttempts = maxRetryAttempts,
            EnableFallbackMechanisms = enableFallbackMechanisms,
            EnableRetryMechanisms = enableRetryMechanisms
        };
    }

    /// <summary>
    /// 폴백 설정 업데이트
    /// </summary>
    public void UpdateSettings(bool enableFallback, bool enableRetry, int maxRetries, float retryDelaySeconds)
    {
        enableFallbackMechanisms = enableFallback;
        enableRetryMechanisms = enableRetry;
        maxRetryAttempts = Mathf.Max(1, maxRetries);
        retryDelay = Mathf.Max(1f, retryDelaySeconds);

        Debug.Log($"[FallbackHandler] Settings updated - Fallback: {enableFallback}, Retry: {enableRetry}, Max retries: {maxRetries}, Delay: {retryDelaySeconds}s");
    }

    /// <summary>
    /// 재시도 횟수 리셋
    /// </summary>
    public void ResetRetryCount()
    {
        _currentRetryAttempt = 0;
        Debug.Log("[FallbackHandler] Retry count reset");
    }

    /// <summary>
    /// 특정 전략으로 수동 폴백 트리거
    /// </summary>
    public void TriggerManualFallback(FallbackStrategy strategy)
    {
        if (!enableFallbackMechanisms)
        {
            Debug.LogWarning("[FallbackHandler] Fallback mechanisms are disabled");
            return;
        }

        Debug.Log($"[FallbackHandler] Manual fallback triggered with strategy: {strategy}");
        StartFallback(AutoLoginResult.Unknown, strategy);
    }
    #endregion
}

#region Data Classes and Enums

/// <summary>
/// 폴백 전략 열거형
/// </summary>
public enum FallbackStrategy
{
    None,
    RetryWithDelay,
    AttemptTokenRecovery,
    ClearCredentialsAndRetry,
    TransitionToManualLogin,
    EnableOfflineMode,
    FallbackToGuestMode,
    ShowErrorAndRetry
}

/// <summary>
/// 사용자 액션 요구사항 열거형
/// </summary>
public enum UserActionRequired
{
    None,
    ManualLogin,
    ShowError,
    EnableOfflineMode,
    EnableGuestMode,
    RetryAutoLogin,
    CheckNetworkConnection,
    ContactSupport
}

/// <summary>
/// 폴백 결과 클래스
/// </summary>
[Serializable]
public class FallbackResult
{
    public bool Success { get; set; }
    public FallbackStrategy Strategy { get; set; }
    public string Message { get; set; }
    public string ErrorMessage { get; set; }
    public bool RequiresUserAction { get; set; }
    public UserActionRequired UserActionType { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }

    public FallbackResult()
    {
        AdditionalData = new Dictionary<string, object>();
    }
}

/// <summary>
/// 폴백 액션 클래스
/// </summary>
[Serializable]
public class FallbackAction
{
    public FallbackStrategy Strategy { get; set; }
    public AutoLoginResult TriggerReason { get; set; }
    public float Delay { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Parameters { get; set; }

    public FallbackAction()
    {
        Parameters = new Dictionary<string, object>();
    }
}

/// <summary>
/// 폴백 상태 정보 클래스
/// </summary>
[Serializable]
public class FallbackStatus
{
    public bool IsInitialized { get; set; }
    public bool IsFallbackInProgress { get; set; }
    public FallbackStrategy CurrentStrategy { get; set; }
    public AutoLoginResult LastFailureReason { get; set; }
    public int CurrentRetryAttempt { get; set; }
    public int MaxRetryAttempts { get; set; }
    public bool EnableFallbackMechanisms { get; set; }
    public bool EnableRetryMechanisms { get; set; }
}

#endregion