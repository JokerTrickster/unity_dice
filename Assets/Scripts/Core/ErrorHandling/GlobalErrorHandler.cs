using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 글로벌 오류 처리 시스템
/// 애플리케이션 전체의 오류를 중앙에서 관리하고 처리합니다.
/// </summary>
public class GlobalErrorHandler : MonoBehaviour
{
    #region Singleton
    private static GlobalErrorHandler _instance;
    public static GlobalErrorHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GlobalErrorHandler");
                _instance = go.AddComponent<GlobalErrorHandler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// 오류가 발생했을 때 발생하는 이벤트
    /// </summary>
    public static event Action<ErrorInfo> OnErrorOccurred;
    
    /// <summary>
    /// 오류가 복구되었을 때 발생하는 이벤트
    /// </summary>
    public static event Action<ErrorInfo> OnErrorRecovered;
    
    /// <summary>
    /// 치명적인 오류가 발생했을 때 발생하는 이벤트
    /// </summary>
    public static event Action<ErrorInfo> OnCriticalError;
    
    /// <summary>
    /// 오류 메시지 표시 요청 이벤트
    /// </summary>
    public static event Action<string, ErrorSeverity> OnShowErrorMessage;
    #endregion

    #region Configuration
    [Header("Error Handling Settings")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool enableRetry = true;
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelay = 1f;
    [SerializeField] private bool showUserFriendlyMessages = true;
    
    [Header("Error Thresholds")]
    [SerializeField] private int maxErrorsPerMinute = 10;
    [SerializeField] private float errorResetInterval = 60f;
    #endregion

    #region Private Fields
    private readonly Dictionary<string, int> _errorCounts = new();
    private readonly Dictionary<string, DateTime> _lastErrorTimes = new();
    private readonly Dictionary<ErrorType, ErrorHandler> _errorHandlers = new();
    private readonly ErrorLogger _errorLogger = new();
    private readonly Queue<ErrorInfo> _recentErrors = new();
    private DateTime _lastErrorReset = DateTime.Now;
    private bool _isInitialized = false;
    #endregion

    #region Properties
    /// <summary>
    /// 전체 오류 발생 횟수
    /// </summary>
    public int TotalErrorCount => _recentErrors.Count;
    
    /// <summary>
    /// 오류 처리 시스템 활성화 여부
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
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
        InitializeErrorHandler();
    }

    private void Update()
    {
        // 오류 카운트 리셋
        if (DateTime.Now.Subtract(_lastErrorReset).TotalSeconds >= errorResetInterval)
        {
            ResetErrorCounts();
        }
    }

    private void OnDestroy()
    {
        OnErrorOccurred = null;
        OnErrorRecovered = null;
        OnCriticalError = null;
        OnShowErrorMessage = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 오류 처리 시스템 초기화
    /// </summary>
    private void InitializeErrorHandler()
    {
        SetupErrorHandlers();
        _errorLogger.Initialize(enableLogging);
        
        // Unity 로그 캡처 설정
        Application.logMessageReceived += HandleUnityLogMessage;
        
        _isInitialized = true;
        Debug.Log("[GlobalErrorHandler] Error handling system initialized");
    }

    /// <summary>
    /// 오류 타입별 핸들러 설정
    /// </summary>
    private void SetupErrorHandlers()
    {
        _errorHandlers[ErrorType.Network] = new NetworkErrorHandler();
        _errorHandlers[ErrorType.Authentication] = new AuthErrorHandler();
        _errorHandlers[ErrorType.Validation] = new ValidationErrorHandler();
        _errorHandlers[ErrorType.System] = new SystemErrorHandler();
        _errorHandlers[ErrorType.UserInput] = new UserInputErrorHandler();
        _errorHandlers[ErrorType.GooglePlayGames] = new GooglePlayGamesErrorHandler();
        
        Debug.Log($"[GlobalErrorHandler] Setup {_errorHandlers.Count} error handlers");
    }
    #endregion

    #region Error Handling
    /// <summary>
    /// 오류 처리 메인 메서드
    /// </summary>
    public void HandleError(ErrorInfo errorInfo)
    {
        if (!IsEnabled || !_isInitialized) return;

        try
        {
            // 오류 정보 검증
            if (errorInfo == null)
            {
                Debug.LogError("[GlobalErrorHandler] ErrorInfo is null");
                return;
            }

            // 오류 빈도 체크
            if (IsErrorRateLimited(errorInfo))
            {
                Debug.LogWarning($"[GlobalErrorHandler] Error rate limited: {errorInfo.Type}");
                return;
            }

            // 오류 기록
            RecordError(errorInfo);

            // 오류 로깅
            _errorLogger.LogError(errorInfo);

            // 타입별 처리
            ProcessErrorByType(errorInfo);

            // 이벤트 발생
            OnErrorOccurred?.Invoke(errorInfo);

            // 치명적 오류 체크
            if (errorInfo.Severity == ErrorSeverity.Critical)
            {
                OnCriticalError?.Invoke(errorInfo);
            }

            // 사용자 메시지 표시
            if (showUserFriendlyMessages && !string.IsNullOrEmpty(errorInfo.UserMessage))
            {
                OnShowErrorMessage?.Invoke(errorInfo.UserMessage, errorInfo.Severity);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GlobalErrorHandler] Error in HandleError: {ex.Message}");
        }
    }

    /// <summary>
    /// 타입별 오류 처리
    /// </summary>
    private void ProcessErrorByType(ErrorInfo errorInfo)
    {
        if (_errorHandlers.ContainsKey(errorInfo.Type))
        {
            var handler = _errorHandlers[errorInfo.Type];
            var result = handler.HandleError(errorInfo);
            
            if (result.ShouldRetry && enableRetry)
            {
                ScheduleRetry(errorInfo, result);
            }
        }
        else
        {
            Debug.LogWarning($"[GlobalErrorHandler] No handler for error type: {errorInfo.Type}");
        }
    }

    /// <summary>
    /// 오류 재시도 스케줄링
    /// </summary>
    private void ScheduleRetry(ErrorInfo errorInfo, ErrorHandlingResult result)
    {
        if (errorInfo.RetryCount >= maxRetryAttempts)
        {
            Debug.LogError($"[GlobalErrorHandler] Max retry attempts exceeded for: {errorInfo.Code}");
            return;
        }

        float delay = result.RetryDelay > 0 ? result.RetryDelay : retryDelay;
        StartCoroutine(RetryAfterDelay(errorInfo, delay));
    }

    /// <summary>
    /// 지연 후 재시도 코루틴
    /// </summary>
    private System.Collections.IEnumerator RetryAfterDelay(ErrorInfo errorInfo, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        errorInfo.RetryCount++;
        Debug.Log($"[GlobalErrorHandler] Retrying error {errorInfo.Code} (attempt {errorInfo.RetryCount})");
        
        // 재시도 로직은 각 핸들러에서 구현
        if (_errorHandlers.ContainsKey(errorInfo.Type))
        {
            _errorHandlers[errorInfo.Type].RetryOperation(errorInfo);
        }
    }
    #endregion

    #region Error Recording and Analysis
    /// <summary>
    /// 오류 기록
    /// </summary>
    private void RecordError(ErrorInfo errorInfo)
    {
        _recentErrors.Enqueue(errorInfo);
        
        // 최대 100개까지만 보관
        while (_recentErrors.Count > 100)
        {
            _recentErrors.Dequeue();
        }

        // 오류 카운트 증가
        string errorKey = $"{errorInfo.Type}_{errorInfo.Code}";
        _errorCounts[errorKey] = _errorCounts.GetValueOrDefault(errorKey, 0) + 1;
        _lastErrorTimes[errorKey] = DateTime.Now;
    }

    /// <summary>
    /// 오류 빈도 제한 체크
    /// </summary>
    private bool IsErrorRateLimited(ErrorInfo errorInfo)
    {
        string errorKey = $"{errorInfo.Type}_{errorInfo.Code}";
        
        if (!_errorCounts.ContainsKey(errorKey)) return false;
        
        var timeSinceLastError = DateTime.Now.Subtract(_lastErrorTimes[errorKey]).TotalSeconds;
        if (timeSinceLastError < 1.0 && _errorCounts[errorKey] > maxErrorsPerMinute)
        {
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 오류 카운트 리셋
    /// </summary>
    private void ResetErrorCounts()
    {
        _errorCounts.Clear();
        _lastErrorReset = DateTime.Now;
        Debug.Log("[GlobalErrorHandler] Error counts reset");
    }

    /// <summary>
    /// Unity 로그 메시지 처리
    /// </summary>
    private void HandleUnityLogMessage(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            var errorInfo = new ErrorInfo
            {
                Type = ErrorType.System,
                Code = "UNITY_ERROR",
                Message = logString,
                StackTrace = stackTrace,
                Severity = type == LogType.Exception ? ErrorSeverity.Critical : ErrorSeverity.High,
                Timestamp = DateTime.Now,
                Context = "Unity Engine"
            };
            
            HandleError(errorInfo);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 네트워크 오류 처리
    /// </summary>
    public void HandleNetworkError(string errorMessage, int statusCode = 0, string context = "")
    {
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = $"NETWORK_{statusCode}",
            Message = errorMessage,
            Severity = ClassifyNetworkErrorSeverity(statusCode),
            Context = context,
            Timestamp = DateTime.Now,
            UserMessage = GetUserFriendlyNetworkMessage(statusCode)
        };
        
        HandleError(errorInfo);
    }

    /// <summary>
    /// 인증 오류 처리
    /// </summary>
    public void HandleAuthenticationError(string errorMessage, string context = "")
    {
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Authentication,
            Code = "AUTH_FAILED",
            Message = errorMessage,
            Severity = ErrorSeverity.High,
            Context = context,
            Timestamp = DateTime.Now,
            UserMessage = "로그인에 실패했습니다. 다시 시도해주세요."
        };
        
        HandleError(errorInfo);
    }

    /// <summary>
    /// 유효성 검증 오류 처리
    /// </summary>
    public void HandleValidationError(string field, string errorMessage, string context = "")
    {
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Validation,
            Code = $"VALIDATION_{field.ToUpper()}",
            Message = errorMessage,
            Severity = ErrorSeverity.Medium,
            Context = context,
            Timestamp = DateTime.Now,
            UserMessage = errorMessage
        };
        
        HandleError(errorInfo);
    }

    /// <summary>
    /// 시스템 오류 처리
    /// </summary>
    public void HandleSystemError(Exception exception, string context = "")
    {
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.System,
            Code = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Severity = ErrorSeverity.High,
            Context = context,
            Timestamp = DateTime.Now,
            UserMessage = "시스템 오류가 발생했습니다. 잠시 후 다시 시도해주세요."
        };
        
        HandleError(errorInfo);
    }

    /// <summary>
    /// 오류 복구 알림
    /// </summary>
    public void NotifyErrorRecovered(ErrorInfo errorInfo)
    {
        _errorLogger.LogRecovery(errorInfo);
        OnErrorRecovered?.Invoke(errorInfo);
        Debug.Log($"[GlobalErrorHandler] Error recovered: {errorInfo.Code}");
    }

    /// <summary>
    /// 최근 오류 목록 반환
    /// </summary>
    public List<ErrorInfo> GetRecentErrors(int count = 10)
    {
        var errors = new List<ErrorInfo>();
        var array = _recentErrors.ToArray();
        
        int start = Math.Max(0, array.Length - count);
        for (int i = start; i < array.Length; i++)
        {
            errors.Add(array[i]);
        }
        
        return errors;
    }

    /// <summary>
    /// 오류 통계 반환
    /// </summary>
    public Dictionary<string, int> GetErrorStatistics()
    {
        return new Dictionary<string, int>(_errorCounts);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 네트워크 오류 심각도 분류
    /// </summary>
    private ErrorSeverity ClassifyNetworkErrorSeverity(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => ErrorSeverity.High,
            >= 400 => ErrorSeverity.Medium,
            0 => ErrorSeverity.High, // 연결 실패
            _ => ErrorSeverity.Low
        };
    }

    /// <summary>
    /// 사용자 친화적 네트워크 메시지 생성
    /// </summary>
    private string GetUserFriendlyNetworkMessage(int statusCode)
    {
        return statusCode switch
        {
            0 => "인터넷 연결을 확인해주세요.",
            401 => "인증이 필요합니다. 다시 로그인해주세요.",
            403 => "접근 권한이 없습니다.",
            404 => "요청한 정보를 찾을 수 없습니다.",
            >= 500 => "서버에 일시적인 문제가 발생했습니다. 잠시 후 다시 시도해주세요.",
            >= 400 => "요청 처리 중 오류가 발생했습니다.",
            _ => "네트워크 오류가 발생했습니다."
        };
    }
    #endregion

    #region Configuration
    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(bool enableLogging, bool enableRetry, int maxRetries = 3)
    {
        this.enableLogging = enableLogging;
        this.enableRetry = enableRetry;
        this.maxRetryAttempts = maxRetries;
        
        _errorLogger.Initialize(enableLogging);
        
        Debug.Log($"[GlobalErrorHandler] Settings updated - Logging: {enableLogging}, Retry: {enableRetry}, MaxRetries: {maxRetries}");
    }
    #endregion
}