using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 토큰 자동 갱신 관리자
/// 만료된 토큰을 자동으로 갱신하고 새로운 토큰을 안전하게 저장합니다.
/// </summary>
public static class TokenRefreshManager
{
    #region Constants
    private const int MAX_REFRESH_ATTEMPTS = 3;
    private const float REFRESH_RETRY_DELAY_SECONDS = 2f;
    private const float REFRESH_TIMEOUT_SECONDS = 30f;
    #endregion

    #region Events
    /// <summary>
    /// 토큰 갱신 시작 이벤트
    /// </summary>
    public static event Action OnTokenRefreshStarted;
    
    /// <summary>
    /// 토큰 갱신 성공 이벤트
    /// </summary>
    public static event Action<string> OnTokenRefreshSuccess;
    
    /// <summary>
    /// 토큰 갱신 실패 이벤트
    /// </summary>
    public static event Action<RefreshResult> OnTokenRefreshFailed;
    #endregion

    #region Properties
    /// <summary>
    /// 토큰 갱신 진행 중 여부
    /// </summary>
    public static bool IsRefreshInProgress { get; private set; }
    
    /// <summary>
    /// 마지막 갱신 시도 시간
    /// </summary>
    public static DateTime LastRefreshAttempt { get; private set; }
    
    /// <summary>
    /// 갱신 시도 횟수
    /// </summary>
    public static int RefreshAttemptCount { get; private set; }
    #endregion

    #region Token Refresh
    /// <summary>
    /// 토큰 갱신 시도
    /// </summary>
    /// <returns>갱신 결과</returns>
    public static async Task<RefreshResult> RefreshTokenAsync()
    {
        if (IsRefreshInProgress)
        {
            Debug.LogWarning("[TokenRefreshManager] Refresh already in progress");
            return RefreshResult.AlreadyInProgress;
        }

        var refreshToken = SecureStorage.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("[TokenRefreshManager] No refresh token available");
            return RefreshResult.NoRefreshToken;
        }

        IsRefreshInProgress = true;
        LastRefreshAttempt = DateTime.UtcNow;
        RefreshAttemptCount = 0;

        OnTokenRefreshStarted?.Invoke();

        try
        {
            Debug.Log("[TokenRefreshManager] Starting token refresh process");
            
            var result = await RefreshTokenWithRetriesAsync(refreshToken);
            
            if (result.Success)
            {
                Debug.Log("[TokenRefreshManager] Token refresh completed successfully");
                OnTokenRefreshSuccess?.Invoke(result.NewAccessToken);
            }
            else
            {
                Debug.LogError($"[TokenRefreshManager] Token refresh failed: {result.ErrorMessage}");
                OnTokenRefreshFailed?.Invoke(result);
            }

            return result;
        }
        finally
        {
            IsRefreshInProgress = false;
        }
    }

    /// <summary>
    /// 재시도 로직을 포함한 토큰 갱신
    /// </summary>
    /// <param name="refreshToken">리프레시 토큰</param>
    /// <returns>갱신 결과</returns>
    private static async Task<RefreshResult> RefreshTokenWithRetriesAsync(string refreshToken)
    {
        RefreshResult lastResult = null;

        for (RefreshAttemptCount = 1; RefreshAttemptCount <= MAX_REFRESH_ATTEMPTS; RefreshAttemptCount++)
        {
            Debug.Log($"[TokenRefreshManager] Refresh attempt {RefreshAttemptCount}/{MAX_REFRESH_ATTEMPTS}");

            try
            {
                var result = await PerformTokenRefreshAsync(refreshToken);
                
                if (result.Success)
                {
                    return result;
                }

                lastResult = result;

                // 재시도하지 않는 오류 조건들
                if (result.ErrorType == RefreshErrorType.InvalidRefreshToken ||
                    result.ErrorType == RefreshErrorType.RefreshTokenExpired ||
                    result.ErrorType == RefreshErrorType.UserNotFound)
                {
                    Debug.LogWarning($"[TokenRefreshManager] Non-retryable error: {result.ErrorType}");
                    break;
                }

                // 재시도 전 대기
                if (RefreshAttemptCount < MAX_REFRESH_ATTEMPTS)
                {
                    var delaySeconds = REFRESH_RETRY_DELAY_SECONDS * RefreshAttemptCount; // 지수 백오프
                    Debug.Log($"[TokenRefreshManager] Waiting {delaySeconds}s before retry");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
            catch (Exception ex)
            {
                lastResult = new RefreshResult
                {
                    Success = false,
                    ErrorType = RefreshErrorType.NetworkError,
                    ErrorMessage = $"Exception during refresh: {ex.Message}"
                };
                
                Debug.LogError($"[TokenRefreshManager] Refresh attempt {RefreshAttemptCount} failed: {ex.Message}");
            }
        }

        return lastResult ?? RefreshResult.Unknown;
    }

    /// <summary>
    /// 실제 토큰 갱신 요청 수행
    /// </summary>
    /// <param name="refreshToken">리프레시 토큰</param>
    /// <returns>갱신 결과</returns>
    private static async Task<RefreshResult> PerformTokenRefreshAsync(string refreshToken)
    {
        try
        {
            var request = new TokenRefreshRequest
            {
                RefreshToken = refreshToken,
                ClientId = Application.identifier,
                DeviceId = SystemInfo.deviceUniqueIdentifier
            };

            var response = await NetworkManager.Instance.PostAsync<TokenRefreshResponse>(
                "/api/auth/refresh",
                request,
                timeoutSeconds: REFRESH_TIMEOUT_SECONDS
            );

            if (!response.IsSuccess)
            {
                return CreateErrorResult(response.statusCode, response.errorMessage);
            }

            var tokenResponse = response.data;
            if (tokenResponse == null)
            {
                return new RefreshResult
                {
                    Success = false,
                    ErrorType = RefreshErrorType.InvalidResponse,
                    ErrorMessage = "Empty response from server"
                };
            }

            // 새로운 토큰들을 안전하게 저장
            await SaveNewTokensAsync(tokenResponse);

            return new RefreshResult
            {
                Success = true,
                NewAccessToken = tokenResponse.AccessToken,
                NewRefreshToken = tokenResponse.RefreshToken,
                ExpirationTime = tokenResponse.ExpiresAt
            };
        }
        catch (TaskCanceledException)
        {
            return new RefreshResult
            {
                Success = false,
                ErrorType = RefreshErrorType.Timeout,
                ErrorMessage = "Token refresh request timed out"
            };
        }
        catch (Exception ex)
        {
            return new RefreshResult
            {
                Success = false,
                ErrorType = RefreshErrorType.NetworkError,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 새로운 토큰들을 안전하게 저장
    /// </summary>
    /// <param name="tokenResponse">토큰 응답</param>
    private static async Task SaveNewTokensAsync(TokenRefreshResponse tokenResponse)
    {
        try
        {
            // 기존 토큰들 백업 (롤백 용도)
            var oldAccessToken = SecureStorage.GetAuthToken();
            var oldRefreshToken = SecureStorage.GetRefreshToken();

            // 새로운 토큰들 저장
            SecureStorage.SaveAuthToken(tokenResponse.AccessToken);
            
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                SecureStorage.SaveRefreshToken(tokenResponse.RefreshToken);
            }

            // 저장된 토큰 검증
            var savedAccessToken = SecureStorage.GetAuthToken();
            if (savedAccessToken != tokenResponse.AccessToken)
            {
                // 롤백
                Debug.LogError("[TokenRefreshManager] Token save verification failed, rolling back");
                SecureStorage.SaveAuthToken(oldAccessToken);
                SecureStorage.SaveRefreshToken(oldRefreshToken);
                throw new Exception("Token save verification failed");
            }

            Debug.Log("[TokenRefreshManager] New tokens saved successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenRefreshManager] Failed to save new tokens: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// HTTP 상태 코드와 메시지로부터 오류 결과 생성
    /// </summary>
    /// <param name="statusCode">HTTP 상태 코드</param>
    /// <param name="errorMessage">오류 메시지</param>
    /// <returns>갱신 결과</returns>
    private static RefreshResult CreateErrorResult(int statusCode, string errorMessage)
    {
        RefreshErrorType errorType;
        string message = errorMessage ?? "Unknown error";

        switch (statusCode)
        {
            case 400:
                errorType = RefreshErrorType.InvalidRefreshToken;
                message = "Invalid refresh token format";
                break;
            case 401:
                errorType = RefreshErrorType.RefreshTokenExpired;
                message = "Refresh token has expired";
                break;
            case 404:
                errorType = RefreshErrorType.UserNotFound;
                message = "User account not found";
                break;
            case 429:
                errorType = RefreshErrorType.RateLimited;
                message = "Too many refresh requests";
                break;
            case 500:
            case 502:
            case 503:
                errorType = RefreshErrorType.ServerError;
                message = "Server error occurred";
                break;
            default:
                errorType = RefreshErrorType.NetworkError;
                break;
        }

        return new RefreshResult
        {
            Success = false,
            ErrorType = errorType,
            ErrorMessage = message,
            StatusCode = statusCode
        };
    }
    #endregion

    #region Auto Refresh
    /// <summary>
    /// 토큰이 곧 만료되는 경우 자동 갱신
    /// </summary>
    /// <param name="token">현재 액세스 토큰</param>
    /// <param name="thresholdSeconds">갱신 임계 시간 (초)</param>
    /// <returns>갱신이 필요한지 여부와 결과</returns>
    public static async Task<(bool refreshNeeded, RefreshResult result)> RefreshIfNeededAsync(
        string token, 
        float thresholdSeconds = 3600f)
    {
        if (string.IsNullOrEmpty(token))
        {
            Debug.Log("[TokenRefreshManager] No token provided for refresh check");
            return (false, null);
        }

        try
        {
            var validationInfo = TokenValidator.GetTokenValidationInfo(token);
            
            if (validationInfo.IsExpired)
            {
                Debug.Log("[TokenRefreshManager] Token is expired, refreshing");
                var result = await RefreshTokenAsync();
                return (true, result);
            }

            if (validationInfo.IsExpiringSoon || 
                validationInfo.TimeUntilExpiry.TotalSeconds <= thresholdSeconds)
            {
                Debug.Log($"[TokenRefreshManager] Token expires in {validationInfo.TimeUntilExpiry.TotalSeconds}s, refreshing");
                var result = await RefreshTokenAsync();
                return (true, result);
            }

            Debug.Log($"[TokenRefreshManager] Token valid for {validationInfo.TimeUntilExpiry.TotalMinutes:F1} minutes, no refresh needed");
            return (false, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenRefreshManager] Error during refresh check: {ex.Message}");
            return (false, new RefreshResult
            {
                Success = false,
                ErrorType = RefreshErrorType.ValidationError,
                ErrorMessage = ex.Message
            });
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 마지막 갱신 시도로부터 경과된 시간
    /// </summary>
    public static TimeSpan TimeSinceLastRefreshAttempt => DateTime.UtcNow - LastRefreshAttempt;

    /// <summary>
    /// 갱신 상태 정보 반환
    /// </summary>
    /// <returns>갱신 상태 정보</returns>
    public static TokenRefreshStatus GetRefreshStatus()
    {
        return new TokenRefreshStatus
        {
            IsRefreshInProgress = IsRefreshInProgress,
            LastRefreshAttempt = LastRefreshAttempt,
            RefreshAttemptCount = RefreshAttemptCount,
            TimeSinceLastAttempt = TimeSinceLastRefreshAttempt,
            HasRefreshToken = SecureStorage.HasRefreshToken
        };
    }

    /// <summary>
    /// 갱신 상태 초기화
    /// </summary>
    public static void ResetRefreshState()
    {
        IsRefreshInProgress = false;
        LastRefreshAttempt = DateTime.MinValue;
        RefreshAttemptCount = 0;
        
        Debug.Log("[TokenRefreshManager] Refresh state reset");
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 토큰 갱신 결과
/// </summary>
[Serializable]
public class RefreshResult
{
    public bool Success { get; set; }
    public RefreshErrorType ErrorType { get; set; }
    public string ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public string NewAccessToken { get; set; }
    public string NewRefreshToken { get; set; }
    public DateTime? ExpirationTime { get; set; }

    // 정적 헬퍼 메서드들
    public static RefreshResult NoRefreshToken => new RefreshResult 
    { 
        Success = false, 
        ErrorType = RefreshErrorType.NoRefreshToken,
        ErrorMessage = "No refresh token available" 
    };
    
    public static RefreshResult AlreadyInProgress => new RefreshResult 
    { 
        Success = false, 
        ErrorType = RefreshErrorType.AlreadyInProgress,
        ErrorMessage = "Refresh already in progress" 
    };
    
    public static RefreshResult Unknown => new RefreshResult 
    { 
        Success = false, 
        ErrorType = RefreshErrorType.Unknown,
        ErrorMessage = "Unknown error occurred during token refresh" 
    };
}

/// <summary>
/// 토큰 갱신 오류 유형
/// </summary>
public enum RefreshErrorType
{
    Unknown,
    NoRefreshToken,
    InvalidRefreshToken,
    RefreshTokenExpired,
    NetworkError,
    ServerError,
    UserNotFound,
    RateLimited,
    Timeout,
    InvalidResponse,
    ValidationError,
    AlreadyInProgress
}

/// <summary>
/// 토큰 갱신 요청
/// </summary>
[Serializable]
public class TokenRefreshRequest
{
    public string RefreshToken;
    public string ClientId;
    public string DeviceId;
}

/// <summary>
/// 토큰 갱신 응답
/// </summary>
[Serializable]
public class TokenRefreshResponse
{
    public string AccessToken;
    public string RefreshToken;
    public DateTime ExpiresAt;
    public string TokenType;
}

/// <summary>
/// 토큰 갱신 상태 정보
/// </summary>
[Serializable]
public class TokenRefreshStatus
{
    public bool IsRefreshInProgress;
    public DateTime LastRefreshAttempt;
    public int RefreshAttemptCount;
    public TimeSpan TimeSinceLastAttempt;
    public bool HasRefreshToken;
}
#endregion