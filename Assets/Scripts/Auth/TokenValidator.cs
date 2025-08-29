using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Text;

/// <summary>
/// 토큰 유효성 검증 매니저
/// JWT 토큰의 형식, 만료 시간, 서명 등을 검증하고 토큰 상태를 관리합니다.
/// </summary>
public class TokenValidator
{
    #region Constants
    private const float DEFAULT_VALIDITY_THRESHOLD_SECONDS = 300f; // 5분
    private const string JWT_PATTERN = @"^[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+$";
    private const int MAX_VALIDATION_CACHE_SIZE = 100;
    #endregion

    #region Private Fields
    private readonly float _validityThresholdSeconds;
    private readonly bool _requireServerValidation;
    private readonly ValidationCache _validationCache;
    private readonly string _secretKey;
    #endregion

    #region Properties
    /// <summary>
    /// 서버 검증 필요 여부
    /// </summary>
    public bool RequireServerValidation => _requireServerValidation;

    /// <summary>
    /// 토큰 유효성 임계값 (초)
    /// </summary>
    public float ValidityThresholdSeconds => _validityThresholdSeconds;
    #endregion

    #region Constructor
    /// <summary>
    /// TokenValidator 생성자
    /// </summary>
    /// <param name="validityThresholdSeconds">토큰 유효성 임계값 (초)</param>
    /// <param name="requireServerValidation">서버 검증 필요 여부</param>
    /// <param name="secretKey">JWT 서명 검증을 위한 시크릿 키</param>
    public TokenValidator(float validityThresholdSeconds = DEFAULT_VALIDITY_THRESHOLD_SECONDS, 
                         bool requireServerValidation = false,
                         string secretKey = null)
    {
        _validityThresholdSeconds = validityThresholdSeconds;
        _requireServerValidation = requireServerValidation && !Debug.isDebugBuild;
        _secretKey = secretKey;
        _validationCache = new ValidationCache(MAX_VALIDATION_CACHE_SIZE);

        Debug.Log($"[TokenValidator] Initialized - Threshold: {_validityThresholdSeconds}s, Server validation: {_requireServerValidation}");
    }
    #endregion

    #region Public Validation Methods
    /// <summary>
    /// 토큰 유효성 검사 (비동기)
    /// </summary>
    /// <param name="token">검증할 JWT 토큰</param>
    /// <returns>토큰 유효성 여부</returns>
    public async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[TokenValidator] Empty token provided");
            return false;
        }

        try
        {
            // 캐시에서 검증 결과 확인
            var cachedResult = _validationCache.GetValidationResult(token);
            if (cachedResult != null && !cachedResult.IsExpired)
            {
                Debug.Log("[TokenValidator] Using cached validation result");
                return cachedResult.IsValid;
            }

            // 새로운 검증 수행
            var validationResult = await PerformFullValidationAsync(token);
            
            // 결과를 캐시에 저장
            _validationCache.CacheValidationResult(token, validationResult);

            return validationResult.IsValid;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Token validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 토큰 유효성 검사 (동기)
    /// </summary>
    /// <param name="token">검증할 JWT 토큰</param>
    /// <returns>토큰 유효성 여부</returns>
    public bool ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[TokenValidator] Empty token provided");
            return false;
        }

        try
        {
            // 기본적인 형식 및 만료 검사만 수행 (서버 검증 제외)
            var basicValidation = PerformBasicValidation(token);
            return basicValidation.IsValid;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Synchronous token validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 토큰이 곧 만료되는지 확인
    /// </summary>
    /// <param name="token">확인할 JWT 토큰</param>
    /// <param name="customThreshold">사용자 정의 임계값 (초)</param>
    /// <returns>곧 만료되는지 여부</returns>
    public bool IsTokenExpiringSoon(string token, float? customThreshold = null)
    {
        try
        {
            var expirationTime = GetTokenExpiration(token);
            if (!expirationTime.HasValue)
            {
                Debug.LogWarning("[TokenValidator] Cannot determine token expiration");
                return true; // 만료 시간을 알 수 없으면 만료된 것으로 처리
            }

            float threshold = customThreshold ?? _validityThresholdSeconds;
            var timeUntilExpiry = expirationTime.Value - DateTime.UtcNow;
            
            bool expiringSoon = timeUntilExpiry.TotalSeconds <= threshold;
            
            if (expiringSoon)
            {
                Debug.Log($"[TokenValidator] Token expires in {timeUntilExpiry.TotalSeconds:F0} seconds");
            }

            return expiringSoon;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Failed to check token expiration: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 토큰 상세 검증 정보 반환
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>상세 검증 정보</returns>
    public DetailedTokenValidation GetDetailedValidation(string token)
    {
        var result = new DetailedTokenValidation
        {
            Token = token,
            ValidatedAt = DateTime.UtcNow
        };

        try
        {
            if (string.IsNullOrEmpty(token))
            {
                result.AddIssue("Token is null or empty");
                return result;
            }

            // 1. 형식 검사
            result.IsValidFormat = IsValidTokenFormat(token);
            if (!result.IsValidFormat)
            {
                result.AddIssue("Invalid JWT format");
            }

            // 2. 만료 시간 추출 및 검사
            result.ExpirationTime = GetTokenExpiration(token);
            if (result.ExpirationTime.HasValue)
            {
                result.TimeUntilExpiry = result.ExpirationTime.Value - DateTime.UtcNow;
                result.IsExpired = result.TimeUntilExpiry.TotalSeconds <= 0;
                result.IsExpiringSoon = result.TimeUntilExpiry.TotalSeconds <= _validityThresholdSeconds;

                if (result.IsExpired)
                {
                    result.AddIssue($"Token expired {Math.Abs(result.TimeUntilExpiry.TotalSeconds):F0} seconds ago");
                }
                else if (result.IsExpiringSoon)
                {
                    result.AddIssue($"Token expires in {result.TimeUntilExpiry.TotalSeconds:F0} seconds");
                }
            }
            else
            {
                result.AddIssue("Cannot determine token expiration");
                result.IsExpired = true;
                result.IsExpiringSoon = true;
            }

            // 3. 사용자 ID 추출
            result.UserId = GetTokenUserId(token);
            if (string.IsNullOrEmpty(result.UserId))
            {
                result.AddIssue("Cannot extract user ID from token");
            }

            // 4. 서명 검증 (시크릿 키가 있는 경우)
            if (!string.IsNullOrEmpty(_secretKey))
            {
                result.IsSignatureValid = CryptoHelper.VerifyJWTSignature(token, _secretKey);
                if (!result.IsSignatureValid)
                {
                    result.AddIssue("Invalid JWT signature");
                }
            }

            // 종합 유효성 판단
            result.IsValid = result.IsValidFormat && 
                           !result.IsExpired && 
                           (string.IsNullOrEmpty(_secretKey) || result.IsSignatureValid);

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Detailed validation failed: {ex.Message}");
            result.AddIssue($"Validation error: {ex.Message}");
            return result;
        }
    }
    #endregion

    #region Token Information Extraction
    /// <summary>
    /// JWT 토큰 형식 검증
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>형식이 올바른지 여부</returns>
    public bool IsValidTokenFormat(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        // JWT는 3개의 Base64 URL 인코딩된 부분이 점(.)으로 구분됨
        var regex = new Regex(JWT_PATTERN);
        return regex.IsMatch(token);
    }

    /// <summary>
    /// 토큰에서 만료 시간 추출
    /// </summary>
    /// <param name="token">JWT 토큰</param>
    /// <returns>만료 시간 (UTC) 또는 null</returns>
    public DateTime? GetTokenExpiration(string token)
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
            var payloadBytes = CryptoHelper.Base64UrlDecode(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            
            var payloadObject = JsonUtility.FromJson<JWTPayload>(payloadJson);
            
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
    public string GetTokenUserId(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[1];
            var payloadBytes = CryptoHelper.Base64UrlDecode(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            
            var payloadObject = JsonUtility.FromJson<JWTPayload>(payloadJson);
            
            return payloadObject?.sub ?? payloadObject?.user_id;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Failed to extract user ID: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 토큰에서 발급 시간 추출
    /// </summary>
    /// <param name="token">JWT 토큰</param>
    /// <returns>발급 시간 (UTC) 또는 null</returns>
    public DateTime? GetTokenIssuedAt(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[1];
            var payloadBytes = CryptoHelper.Base64UrlDecode(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            
            var payloadObject = JsonUtility.FromJson<JWTPayload>(payloadJson);
            
            if (payloadObject?.iat == null)
                return null;

            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(payloadObject.iat.Value);
            return dateTimeOffset.UtcDateTime;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Failed to extract issued time: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Configuration
    /// <summary>
    /// 토큰 유효성 임계값 업데이트
    /// </summary>
    /// <param name="newThreshold">새로운 임계값 (초)</param>
    public void UpdateTokenValidityThreshold(float newThreshold)
    {
        if (newThreshold < 0)
        {
            Debug.LogWarning("[TokenValidator] Invalid threshold value, keeping current setting");
            return;
        }

        Debug.Log($"[TokenValidator] Threshold updated from {_validityThresholdSeconds} to {newThreshold} seconds");
        // readonly 필드이므로 실제로는 새 인스턴스를 생성해야 함
        // 이 메소드는 호환성을 위해 유지하되 경고 로그 출력
        Debug.LogWarning("[TokenValidator] Threshold cannot be changed after initialization. Create a new instance instead.");
    }

    /// <summary>
    /// 검증 캐시 정리
    /// </summary>
    public void ClearValidationCache()
    {
        _validationCache.Clear();
        Debug.Log("[TokenValidator] Validation cache cleared");
    }

    /// <summary>
    /// 검증 통계 반환
    /// </summary>
    /// <returns>검증 통계 정보</returns>
    public TokenValidationStats GetValidationStats()
    {
        return new TokenValidationStats
        {
            CacheSize = _validationCache.Size,
            CacheHitRate = _validationCache.HitRate,
            ValidityThreshold = _validityThresholdSeconds,
            RequireServerValidation = _requireServerValidation,
            HasSecretKey = !string.IsNullOrEmpty(_secretKey)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 완전한 토큰 검증 수행
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>검증 결과</returns>
    private async Task<ValidationResult> PerformFullValidationAsync(string token)
    {
        var result = new ValidationResult { Token = token };

        try
        {
            // 1. 기본 검증 (형식, 만료 등)
            var basicResult = PerformBasicValidation(token);
            result.IsValid = basicResult.IsValid;
            result.Issues.AddRange(basicResult.Issues);

            // 기본 검증 실패 시 서버 검증하지 않음
            if (!result.IsValid)
            {
                return result;
            }

            // 2. 서명 검증 (로컬)
            if (!string.IsNullOrEmpty(_secretKey))
            {
                bool signatureValid = CryptoHelper.VerifyJWTSignature(token, _secretKey);
                if (!signatureValid)
                {
                    result.IsValid = false;
                    result.Issues.Add("Invalid JWT signature");
                    return result;
                }
            }

            // 3. 서버 검증 (필요한 경우)
            if (_requireServerValidation)
            {
                bool serverValid = await ValidateTokenWithServerAsync(token);
                if (!serverValid)
                {
                    result.IsValid = false;
                    result.Issues.Add("Server validation failed");
                    return result;
                }
            }

            result.ValidatedAt = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Full validation failed: {ex.Message}");
            result.IsValid = false;
            result.Issues.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// 기본 토큰 검증 수행
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>기본 검증 결과</returns>
    private ValidationResult PerformBasicValidation(string token)
    {
        var result = new ValidationResult { Token = token };

        try
        {
            // 1. 형식 검사
            if (!IsValidTokenFormat(token))
            {
                result.IsValid = false;
                result.Issues.Add("Invalid JWT format");
                return result;
            }

            // 2. 만료 시간 확인
            var expirationTime = GetTokenExpiration(token);
            if (expirationTime.HasValue)
            {
                if (DateTime.UtcNow >= expirationTime.Value)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Token expired at {expirationTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
                    return result;
                }

                var timeUntilExpiry = expirationTime.Value - DateTime.UtcNow;
                if (timeUntilExpiry.TotalSeconds <= _validityThresholdSeconds)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Token expires too soon: {timeUntilExpiry.TotalSeconds:F0} seconds remaining");
                    return result;
                }
            }
            else
            {
                result.IsValid = false;
                result.Issues.Add("Cannot determine token expiration");
                return result;
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Basic validation failed: {ex.Message}");
            result.IsValid = false;
            result.Issues.Add($"Basic validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// 서버와 함께 토큰 유효성 검사
    /// </summary>
    /// <param name="token">검증할 토큰</param>
    /// <returns>서버 검증 결과</returns>
    private async Task<bool> ValidateTokenWithServerAsync(string token)
    {
        try
        {
            var request = new TokenValidationRequest { Token = token };
            
            // NetworkManager의 새로운 API 형식에 맞게 수정
            bool validationComplete = false;
            bool validationResult = false;

            NetworkManager.Instance.Post("/api/auth/validate", request, response =>
            {
                if (response.IsSuccess)
                {
                    var validationResponse = response.GetData<TokenValidationResponse>();
                    if (validationResponse != null)
                    {
                        validationResult = validationResponse.IsValid;
                        Debug.Log("[TokenValidator] Server validation successful");
                    }
                    else
                    {
                        Debug.LogWarning("[TokenValidator] Invalid server validation response format");
                    }
                }
                else
                {
                    Debug.LogWarning($"[TokenValidator] Server validation failed: {response.Error}");
                }
                
                validationComplete = true;
            });

            // 서버 응답 대기 (최대 10초)
            float elapsed = 0f;
            while (!validationComplete && elapsed < 10f)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }

            if (!validationComplete)
            {
                Debug.LogWarning("[TokenValidator] Server validation timeout");
                // 네트워크 오류 시에는 로컬 검증만으로 진행
                return true;
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenValidator] Server validation error: {ex.Message}");
            // 네트워크 오류 시에는 로컬 검증만으로 진행
            return true;
        }
    }
    #endregion
}

#region Data Classes
/// <summary>
/// JWT 페이로드 구조
/// </summary>
[Serializable]
public class JWTPayload
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
    public long? ExpirationTime;
}

/// <summary>
/// 검증 결과
/// </summary>
[Serializable]
public class ValidationResult
{
    public string Token;
    public bool IsValid;
    public DateTime ValidatedAt;
    public System.Collections.Generic.List<string> Issues = new System.Collections.Generic.List<string>();
    public bool IsExpired => ValidatedAt.AddMinutes(5) < DateTime.UtcNow; // 5분 후 캐시 만료
}

/// <summary>
/// 상세 토큰 검증 정보
/// </summary>
[Serializable]
public class DetailedTokenValidation
{
    public string Token;
    public bool IsValid;
    public bool IsValidFormat;
    public DateTime? ExpirationTime;
    public TimeSpan TimeUntilExpiry;
    public bool IsExpired;
    public bool IsExpiringSoon;
    public string UserId;
    public bool IsSignatureValid;
    public DateTime ValidatedAt;
    public System.Collections.Generic.List<string> Issues = new System.Collections.Generic.List<string>();
    
    public void AddIssue(string issue)
    {
        Issues.Add(issue);
    }
}

/// <summary>
/// 토큰 검증 통계
/// </summary>
[Serializable]
public class TokenValidationStats
{
    public int CacheSize;
    public float CacheHitRate;
    public float ValidityThreshold;
    public bool RequireServerValidation;
    public bool HasSecretKey;
}

/// <summary>
/// 검증 캐시
/// </summary>
public class ValidationCache
{
    private readonly System.Collections.Generic.Dictionary<string, ValidationResult> _cache;
    private readonly System.Collections.Generic.Queue<string> _cacheOrder;
    private readonly int _maxSize;
    private int _totalRequests;
    private int _cacheHits;

    public ValidationCache(int maxSize)
    {
        _cache = new System.Collections.Generic.Dictionary<string, ValidationResult>();
        _cacheOrder = new System.Collections.Generic.Queue<string>();
        _maxSize = maxSize;
    }

    public ValidationResult GetValidationResult(string token)
    {
        _totalRequests++;
        
        string tokenHash = CryptoHelper.ComputeSHA256Hash(token);
        if (_cache.TryGetValue(tokenHash, out ValidationResult result))
        {
            _cacheHits++;
            return result;
        }
        
        return null;
    }

    public void CacheValidationResult(string token, ValidationResult result)
    {
        string tokenHash = CryptoHelper.ComputeSHA256Hash(token);
        
        if (_cache.Count >= _maxSize)
        {
            // 오래된 항목 제거
            string oldestHash = _cacheOrder.Dequeue();
            _cache.Remove(oldestHash);
        }
        
        _cache[tokenHash] = result;
        _cacheOrder.Enqueue(tokenHash);
    }

    public void Clear()
    {
        _cache.Clear();
        _cacheOrder.Clear();
        _totalRequests = 0;
        _cacheHits = 0;
    }

    public int Size => _cache.Count;
    public float HitRate => _totalRequests > 0 ? (float)_cacheHits / _totalRequests : 0f;
}
#endregion