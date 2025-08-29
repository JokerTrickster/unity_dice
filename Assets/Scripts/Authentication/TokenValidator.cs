using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

/// <summary>
/// JWT 토큰 검증 및 유효성 검사 유틸리티
/// 토큰 형식, 만료 시간, 서명 등을 검증합니다.
/// </summary>
public static class TokenValidator
{
    #region Constants
    private const float TOKEN_VALIDITY_THRESHOLD_SECONDS = 3600f; // 1시간
    private const string JWT_PATTERN = @"^[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+$";
    #endregion

    #region Properties
    /// <summary>
    /// 서버 검증 필요 여부 (디버그 모드에서는 비활성화)
    /// </summary>
    public static bool RequireServerValidation => !Debug.isDebugBuild;
    #endregion

    #region Token Validation
    /// <summary>
    /// 토큰 유효성 검사
    /// </summary>
    /// <param name="token">검증할 JWT 토큰</param>
    /// <returns>토큰이 유효한지 여부</returns>
    public static async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[TokenValidator] Empty token provided");
            return false;
        }

        try
        {
            // 1. JWT 토큰 형식 검사
            if (!IsValidTokenFormat(token))
            {
                Debug.LogWarning("[TokenValidator] Invalid token format");
                return false;
            }

            // 2. 만료 시간 확인
            var expirationTime = GetTokenExpiration(token);
            if (expirationTime.HasValue)
            {
                var timeUntilExpiry = expirationTime.Value - DateTime.UtcNow;
                if (timeUntilExpiry.TotalSeconds <= TOKEN_VALIDITY_THRESHOLD_SECONDS)
                {
                    Debug.LogWarning($"[TokenValidator] Token expires too soon: {timeUntilExpiry.TotalSeconds}s remaining");
                    return false;
                }
            }

            // 3. 서버 유효성 검사 (선택적)
            if (RequireServerValidation)
            {
                var serverValidationResult = await ValidateTokenWithServerAsync(token);
                if (!serverValidationResult)
                {
                    Debug.LogWarning("[TokenValidator] Server validation failed");
                    return false;
                }
            }

            Debug.Log("[TokenValidator] Token validation successful");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Token validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// JWT 토큰 형식 검증
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>형식이 올바른지 여부</returns>
    public static bool IsValidTokenFormat(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        // JWT는 3개의 Base64 인코딩된 부분이 점(.)으로 구분됨
        var regex = new Regex(JWT_PATTERN);
        return regex.IsMatch(token);
    }

    /// <summary>
    /// 토큰에서 만료 시간 추출
    /// </summary>
    /// <param name="token">JWT 토큰</param>
    /// <returns>만료 시간 (UTC) 또는 null</returns>
    public static DateTime? GetTokenExpiration(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                Debug.LogWarning("[TokenValidator] Invalid JWT structure");
                return null;
            }

            // Payload 디코딩
            var payload = parts[1];
            // Base64 패딩 추가 (필요한 경우)
            payload = AddBase64Padding(payload);
            
            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            
            var payloadObject = JsonConvert.DeserializeObject<TokenPayload>(payloadJson);
            
            if (payloadObject?.exp == null)
            {
                Debug.LogWarning("[TokenValidator] No expiration claim found in token");
                return null;
            }

            // Unix timestamp를 DateTime으로 변환
            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(payloadObject.exp.Value);
            return dateTimeOffset.UtcDateTime;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Failed to extract expiration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 토큰에서 사용자 ID 추출
    /// </summary>
    /// <param name="token">JWT 토큰</param>
    /// <returns>사용자 ID 또는 null</returns>
    public static string GetTokenUserId(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[1];
            payload = AddBase64Padding(payload);
            
            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            
            var payloadObject = JsonConvert.DeserializeObject<TokenPayload>(payloadJson);
            
            return payloadObject?.sub ?? payloadObject?.user_id;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Failed to extract user ID: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 토큰이 곧 만료되는지 확인
    /// </summary>
    /// <param name="token">JWT 토큰</param>
    /// <param name="thresholdSeconds">임계 시간 (초)</param>
    /// <returns>곧 만료되는지 여부</returns>
    public static bool IsTokenExpiringSoon(string token, float thresholdSeconds = TOKEN_VALIDITY_THRESHOLD_SECONDS)
    {
        var expirationTime = GetTokenExpiration(token);
        if (!expirationTime.HasValue)
            return true; // 만료 시간을 확인할 수 없으면 만료된 것으로 처리

        var timeUntilExpiry = expirationTime.Value - DateTime.UtcNow;
        return timeUntilExpiry.TotalSeconds <= thresholdSeconds;
    }
    #endregion

    #region Server Validation
    /// <summary>
    /// 서버와 함께 토큰 유효성 검사
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>서버 검증 결과</returns>
    private static async Task<bool> ValidateTokenWithServerAsync(string token)
    {
        try
        {
            var request = new TokenValidationRequest { Token = token };
            var response = await NetworkManager.Instance.PostAsync<TokenValidationResponse>(
                "/api/auth/validate",
                request
            );

            if (response.IsSuccess && response.data != null)
            {
                Debug.Log("[TokenValidator] Server validation successful");
                return response.data.IsValid;
            }

            Debug.LogWarning($"[TokenValidator] Server validation failed: {response.errorMessage}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Server validation error: {ex.Message}");
            // 네트워크 오류 시에는 로컬 검증만으로 진행
            return true;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Base64 패딩 추가
    /// </summary>
    /// <param name="base64">Base64 문자열</param>
    /// <returns>패딩이 추가된 Base64 문자열</returns>
    private static string AddBase64Padding(string base64)
    {
        var paddingLength = 4 - (base64.Length % 4);
        if (paddingLength == 4)
            return base64;

        return base64 + new string('=', paddingLength);
    }

    /// <summary>
    /// 토큰 검증 결과 정보 반환
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>토큰 검증 정보</returns>
    public static TokenValidationInfo GetTokenValidationInfo(string token)
    {
        var info = new TokenValidationInfo
        {
            Token = token,
            IsValidFormat = IsValidTokenFormat(token),
            ExpirationTime = GetTokenExpiration(token),
            UserId = GetTokenUserId(token)
        };

        if (info.ExpirationTime.HasValue)
        {
            info.TimeUntilExpiry = info.ExpirationTime.Value - DateTime.UtcNow;
            info.IsExpired = info.TimeUntilExpiry.TotalSeconds <= 0;
            info.IsExpiringSoon = info.TimeUntilExpiry.TotalSeconds <= TOKEN_VALIDITY_THRESHOLD_SECONDS;
        }
        else
        {
            info.IsExpired = true;
            info.IsExpiringSoon = true;
        }

        return info;
    }
    #endregion
}

#region Data Classes
/// <summary>
/// JWT 토큰 페이로드 구조
/// </summary>
[Serializable]
public class TokenPayload
{
    public string sub;
    public string user_id;
    public long? exp;
    public long? iat;
    public string iss;
    public string aud;
}

/// <summary>
/// 토큰 검증 요청
/// </summary>
[Serializable]
public class TokenValidationRequest
{
    public string Token;
}

/// <summary>
/// 토큰 검증 응답
/// </summary>
[Serializable]
public class TokenValidationResponse
{
    public bool IsValid;
    public string Message;
    public DateTime? ExpirationTime;
}

/// <summary>
/// 토큰 검증 정보
/// </summary>
[Serializable]
public class TokenValidationInfo
{
    public string Token;
    public bool IsValidFormat;
    public DateTime? ExpirationTime;
    public TimeSpan TimeUntilExpiry;
    public bool IsExpired;
    public bool IsExpiringSoon;
    public string UserId;
}
#endregion