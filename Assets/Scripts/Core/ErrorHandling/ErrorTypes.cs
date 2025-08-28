using System;
using System.Collections.Generic;

#region Error Data Structures
/// <summary>
/// 오류 정보 클래스
/// </summary>
[Serializable]
public class ErrorInfo
{
    public ErrorType Type { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
    public ErrorSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
    public string Context { get; set; }
    public string UserMessage { get; set; }
    public int RetryCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 오류 처리 결과
/// </summary>
public class ErrorHandlingResult
{
    public bool IsHandled { get; set; }
    public bool ShouldRetry { get; set; }
    public float RetryDelay { get; set; } = 1f;
    public string Message { get; set; }
    public ErrorRecoveryAction RecoveryAction { get; set; }
}

/// <summary>
/// 오류 타입
/// </summary>
public enum ErrorType
{
    Network,
    Authentication,
    Validation,
    System,
    UserInput,
    GooglePlayGames,
    Database,
    Configuration,
    Unknown
}

/// <summary>
/// 오류 심각도
/// </summary>
public enum ErrorSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// 오류 복구 액션
/// </summary>
public enum ErrorRecoveryAction
{
    None,
    Retry,
    Refresh,
    Logout,
    Restart,
    ShowErrorScreen,
    ReturnToMainMenu
}
#endregion

#region Error Handlers
/// <summary>
/// 오류 처리 기본 인터페이스
/// </summary>
public interface IErrorHandler
{
    ErrorHandlingResult HandleError(ErrorInfo errorInfo);
    void RetryOperation(ErrorInfo errorInfo);
    bool CanHandle(ErrorType errorType);
}

/// <summary>
/// 기본 오류 핸들러
/// </summary>
public abstract class ErrorHandler : IErrorHandler
{
    public abstract ErrorHandlingResult HandleError(ErrorInfo errorInfo);
    public abstract void RetryOperation(ErrorInfo errorInfo);
    public abstract bool CanHandle(ErrorType errorType);
    
    protected virtual void LogError(ErrorInfo errorInfo)
    {
        UnityEngine.Debug.LogError($"[{GetType().Name}] {errorInfo.Type}: {errorInfo.Message}");
    }
}

/// <summary>
/// 네트워크 오류 핸들러
/// </summary>
public class NetworkErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.Network;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        var result = new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = ShouldRetryNetworkError(errorInfo),
            RetryDelay = CalculateRetryDelay(errorInfo)
        };

        // 특정 네트워크 오류에 대한 복구 액션 설정
        if (errorInfo.Code.Contains("401"))
        {
            result.RecoveryAction = ErrorRecoveryAction.Logout;
            result.ShouldRetry = false;
        }
        else if (errorInfo.Code.Contains("0"))
        {
            result.RecoveryAction = ErrorRecoveryAction.Retry;
        }

        return result;
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        // NetworkManager를 통한 재시도
        if (NetworkManager.Instance != null)
        {
            UnityEngine.Debug.Log($"[NetworkErrorHandler] Retrying network operation: {errorInfo.Context}");
            // 실제 재시도 로직은 NetworkManager에서 처리
        }
    }

    private bool ShouldRetryNetworkError(ErrorInfo errorInfo)
    {
        // 401 (Unauthorized), 403 (Forbidden)은 재시도하지 않음
        if (errorInfo.Code.Contains("401") || errorInfo.Code.Contains("403"))
            return false;
            
        // 5xx 서버 오류나 연결 실패는 재시도
        if (errorInfo.Code.Contains("5") || errorInfo.Code.Contains("0"))
            return true;
            
        return false;
    }

    private float CalculateRetryDelay(ErrorInfo errorInfo)
    {
        // 지수 백오프: 1s, 2s, 4s, 8s
        return Math.Min(16f, (float)Math.Pow(2, errorInfo.RetryCount));
    }
}

/// <summary>
/// 인증 오류 핸들러
/// </summary>
public class AuthErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.Authentication;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        var result = new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = false, // 인증 오류는 일반적으로 재시도하지 않음
            RecoveryAction = ErrorRecoveryAction.Logout
        };

        // 특정 인증 오류 처리
        if (errorInfo.Code.Contains("TOKEN_EXPIRED"))
        {
            result.ShouldRetry = true;
            result.RecoveryAction = ErrorRecoveryAction.Refresh;
        }

        // 상태 머신을 오류 상태로 전환
        if (LoginFlowStateMachine.Instance != null)
        {
            LoginFlowStateMachine.Instance.TransitionToError(errorInfo.Message);
        }

        return result;
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        // AuthenticationManager를 통한 토큰 갱신 재시도
        if (AuthenticationManager.Instance != null)
        {
            UnityEngine.Debug.Log($"[AuthErrorHandler] Retrying authentication operation");
            AuthenticationManager.Instance.RefreshAuthToken();
        }
    }
}

/// <summary>
/// 유효성 검증 오류 핸들러
/// </summary>
public class ValidationErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.Validation;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        return new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = false, // 유효성 오류는 사용자 수정 필요
            RecoveryAction = ErrorRecoveryAction.None,
            Message = errorInfo.UserMessage ?? errorInfo.Message
        };
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        // 유효성 오류는 재시도하지 않음
        UnityEngine.Debug.LogWarning($"[ValidationErrorHandler] Validation errors should not be retried: {errorInfo.Code}");
    }
}

/// <summary>
/// 시스템 오류 핸들러
/// </summary>
public class SystemErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.System;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        var result = new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = false
        };

        // 심각도에 따른 복구 액션 결정
        result.RecoveryAction = errorInfo.Severity switch
        {
            ErrorSeverity.Critical => ErrorRecoveryAction.Restart,
            ErrorSeverity.High => ErrorRecoveryAction.ShowErrorScreen,
            _ => ErrorRecoveryAction.None
        };

        return result;
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        UnityEngine.Debug.LogWarning($"[SystemErrorHandler] System errors typically should not be retried: {errorInfo.Code}");
    }
}

/// <summary>
/// 사용자 입력 오류 핸들러
/// </summary>
public class UserInputErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.UserInput;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        return new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = false, // 사용자 입력 오류는 재시도하지 않음
            RecoveryAction = ErrorRecoveryAction.None,
            Message = errorInfo.UserMessage ?? "입력 내용을 확인해주세요."
        };
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        // 사용자 입력 오류는 재시도하지 않음
    }
}

/// <summary>
/// Google Play Games 오류 핸들러
/// </summary>
public class GooglePlayGamesErrorHandler : ErrorHandler
{
    public override bool CanHandle(ErrorType errorType)
    {
        return errorType == ErrorType.GooglePlayGames;
    }

    public override ErrorHandlingResult HandleError(ErrorInfo errorInfo)
    {
        LogError(errorInfo);
        
        var result = new ErrorHandlingResult
        {
            IsHandled = true,
            ShouldRetry = ShouldRetryGPGSError(errorInfo),
            RetryDelay = 2f
        };

        // Google Play Games 특정 오류 처리
        if (errorInfo.Code.Contains("SIGN_IN_REQUIRED"))
        {
            result.RecoveryAction = ErrorRecoveryAction.Retry;
        }
        else if (errorInfo.Code.Contains("SERVICE_UNAVAILABLE"))
        {
            result.RecoveryAction = ErrorRecoveryAction.ShowErrorScreen;
            result.ShouldRetry = true;
        }

        return result;
    }

    public override void RetryOperation(ErrorInfo errorInfo)
    {
        // GooglePlayGamesManager를 통한 재시도
        if (GooglePlayGamesManager.Instance != null)
        {
            UnityEngine.Debug.Log($"[GooglePlayGamesErrorHandler] Retrying GPGS operation");
            GooglePlayGamesManager.Instance.RetryConnection();
        }
    }

    private bool ShouldRetryGPGSError(ErrorInfo errorInfo)
    {
        // 서비스 사용 불가나 네트워크 관련 오류는 재시도
        return errorInfo.Code.Contains("SERVICE_UNAVAILABLE") || 
               errorInfo.Code.Contains("NETWORK_ERROR") ||
               errorInfo.Code.Contains("TIMEOUT");
    }
}
#endregion