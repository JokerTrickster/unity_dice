using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토큰 저장소 관리자
/// SecureStorage를 기반으로 토큰의 안전한 저장, 검색, 관리 기능을 제공합니다.
/// </summary>
public static class TokenStorage
{
    #region Constants
    private const string TOKEN_METADATA_KEY = "token_metadata";
    private const string DEVICE_BINDING_KEY = "device_binding_token";
    private const string TOKEN_HISTORY_KEY = "token_history";
    private const int MAX_TOKEN_HISTORY_COUNT = 5;
    #endregion

    #region Properties
    /// <summary>
    /// 토큰 저장소 초기화 여부
    /// </summary>
    public static bool IsInitialized => SecureStorage.IsInitialized;

    /// <summary>
    /// 유효한 액세스 토큰이 존재하는지 여부
    /// </summary>
    public static bool HasValidAccessToken
    {
        get
        {
            var metadata = GetTokenMetadata();
            if (metadata?.AccessToken == null) return false;
            
            return !IsTokenExpired(metadata.AccessToken.ExpirationTime);
        }
    }

    /// <summary>
    /// 유효한 리프레시 토큰이 존재하는지 여부
    /// </summary>
    public static bool HasValidRefreshToken
    {
        get
        {
            var metadata = GetTokenMetadata();
            if (metadata?.RefreshToken == null) return false;
            
            return !IsTokenExpired(metadata.RefreshToken.ExpirationTime);
        }
    }

    /// <summary>
    /// 디바이스 바인딩이 유효한지 여부
    /// </summary>
    public static bool IsDeviceBound
    {
        get
        {
            var metadata = GetTokenMetadata();
            if (metadata?.DeviceBinding == null) return false;

            string currentFingerprint = CryptoHelper.GenerateDeviceFingerprint();
            return metadata.DeviceBinding.DeviceFingerprint == currentFingerprint;
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 토큰 저장소 초기화
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (!SecureStorage.IsInitialized)
            {
                SecureStorage.Initialize();
            }

            // 기존 토큰 유효성 검사 및 정리
            ValidateAndCleanupStoredTokens();

            Debug.Log("[TokenStorage] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Initialization failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region Token Storage Operations
    /// <summary>
    /// 토큰 세트 저장 (액세스 토큰 + 리프레시 토큰)
    /// </summary>
    /// <param name="accessToken">액세스 토큰</param>
    /// <param name="refreshToken">리프레시 토큰 (선택적)</param>
    /// <param name="userId">사용자 ID</param>
    /// <returns>저장 성공 여부</returns>
    public static bool StoreTokens(string accessToken, string refreshToken = null, string userId = null)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("[TokenStorage] Access token cannot be null or empty");
            return false;
        }

        try
        {
            // 기존 메타데이터 로드 또는 새로 생성
            var metadata = GetTokenMetadata() ?? new TokenMetadata();

            // 액세스 토큰 저장
            var accessTokenInfo = new TokenInfo
            {
                Token = accessToken,
                TokenType = TokenType.AccessToken,
                StoredAt = DateTime.UtcNow,
                ExpirationTime = ExtractTokenExpiration(accessToken) ?? DateTime.UtcNow.AddHours(1),
                UserId = userId
            };

            metadata.AccessToken = accessTokenInfo;
            SecureStorage.SaveAuthToken(accessToken);

            // 리프레시 토큰이 있으면 저장
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshTokenInfo = new TokenInfo
                {
                    Token = refreshToken,
                    TokenType = TokenType.RefreshToken,
                    StoredAt = DateTime.UtcNow,
                    ExpirationTime = ExtractTokenExpiration(refreshToken) ?? DateTime.UtcNow.AddDays(30),
                    UserId = userId
                };

                metadata.RefreshToken = refreshTokenInfo;
                SecureStorage.SaveRefreshToken(refreshToken);
            }

            // 디바이스 바인딩 설정
            if (!string.IsNullOrEmpty(userId))
            {
                SetupDeviceBinding(metadata, userId);
            }

            // 메타데이터 저장
            SaveTokenMetadata(metadata);

            // 토큰 히스토리에 추가
            AddToTokenHistory(accessTokenInfo);

            Debug.Log("[TokenStorage] Tokens stored successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to store tokens: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 액세스 토큰 업데이트
    /// </summary>
    /// <param name="newAccessToken">새로운 액세스 토큰</param>
    /// <returns>업데이트 성공 여부</returns>
    public static bool UpdateAccessToken(string newAccessToken)
    {
        if (string.IsNullOrEmpty(newAccessToken))
        {
            Debug.LogError("[TokenStorage] New access token cannot be null or empty");
            return false;
        }

        try
        {
            var metadata = GetTokenMetadata();
            if (metadata == null)
            {
                Debug.LogWarning("[TokenStorage] No existing token metadata found");
                return StoreTokens(newAccessToken);
            }

            // 액세스 토큰 정보 업데이트
            metadata.AccessToken = new TokenInfo
            {
                Token = newAccessToken,
                TokenType = TokenType.AccessToken,
                StoredAt = DateTime.UtcNow,
                ExpirationTime = ExtractTokenExpiration(newAccessToken) ?? DateTime.UtcNow.AddHours(1),
                UserId = metadata.AccessToken?.UserId
            };

            // SecureStorage에 저장
            SecureStorage.SaveAuthToken(newAccessToken);
            
            // 메타데이터 저장
            SaveTokenMetadata(metadata);

            // 히스토리에 추가
            AddToTokenHistory(metadata.AccessToken);

            Debug.Log("[TokenStorage] Access token updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to update access token: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 저장된 액세스 토큰 반환
    /// </summary>
    /// <returns>액세스 토큰 정보</returns>
    public static TokenInfo GetAccessToken()
    {
        try
        {
            var metadata = GetTokenMetadata();
            return metadata?.AccessToken;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to get access token: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 저장된 리프레시 토큰 반환
    /// </summary>
    /// <returns>리프레시 토큰 정보</returns>
    public static TokenInfo GetRefreshToken()
    {
        try
        {
            var metadata = GetTokenMetadata();
            return metadata?.RefreshToken;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to get refresh token: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Token Validation & Cleanup
    /// <summary>
    /// 토큰 만료 여부 확인
    /// </summary>
    /// <param name="expirationTime">만료 시간</param>
    /// <param name="bufferMinutes">여유 시간 (분)</param>
    /// <returns>만료 여부</returns>
    public static bool IsTokenExpired(DateTime expirationTime, int bufferMinutes = 5)
    {
        return DateTime.UtcNow.AddMinutes(bufferMinutes) >= expirationTime;
    }

    /// <summary>
    /// 저장된 모든 토큰의 유효성 검사
    /// </summary>
    /// <returns>유효성 검사 결과</returns>
    public static TokenValidationResult ValidateStoredTokens()
    {
        var result = new TokenValidationResult();

        try
        {
            var metadata = GetTokenMetadata();
            if (metadata == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "No token metadata found";
                return result;
            }

            // 액세스 토큰 검증
            if (metadata.AccessToken != null)
            {
                result.AccessTokenValid = !IsTokenExpired(metadata.AccessToken.ExpirationTime);
                if (!result.AccessTokenValid)
                {
                    result.Issues.Add("Access token is expired");
                }
            }
            else
            {
                result.Issues.Add("No access token found");
            }

            // 리프레시 토큰 검증
            if (metadata.RefreshToken != null)
            {
                result.RefreshTokenValid = !IsTokenExpired(metadata.RefreshToken.ExpirationTime);
                if (!result.RefreshTokenValid)
                {
                    result.Issues.Add("Refresh token is expired");
                }
            }
            else
            {
                result.Issues.Add("No refresh token found");
            }

            // 디바이스 바인딩 검증
            if (metadata.DeviceBinding != null)
            {
                string currentFingerprint = CryptoHelper.GenerateDeviceFingerprint();
                result.DeviceBindingValid = metadata.DeviceBinding.DeviceFingerprint == currentFingerprint;
                if (!result.DeviceBindingValid)
                {
                    result.Issues.Add("Device binding mismatch");
                }
            }

            result.IsValid = result.AccessTokenValid || result.RefreshTokenValid;
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Token validation failed: {ex.Message}");
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// 만료된 토큰 정리
    /// </summary>
    /// <returns>정리된 토큰 수</returns>
    public static int CleanupExpiredTokens()
    {
        int cleanedCount = 0;

        try
        {
            var metadata = GetTokenMetadata();
            if (metadata == null) return 0;

            // 액세스 토큰 정리
            if (metadata.AccessToken != null && IsTokenExpired(metadata.AccessToken.ExpirationTime))
            {
                metadata.AccessToken = null;
                SecureStorage.SaveAuthToken(null);
                cleanedCount++;
                Debug.Log("[TokenStorage] Expired access token cleaned up");
            }

            // 리프레시 토큰 정리
            if (metadata.RefreshToken != null && IsTokenExpired(metadata.RefreshToken.ExpirationTime))
            {
                metadata.RefreshToken = null;
                SecureStorage.SaveRefreshToken(null);
                cleanedCount++;
                Debug.Log("[TokenStorage] Expired refresh token cleaned up");
            }

            // 변경사항이 있으면 메타데이터 저장
            if (cleanedCount > 0)
            {
                SaveTokenMetadata(metadata);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to cleanup expired tokens: {ex.Message}");
            return 0;
        }
    }
    #endregion

    #region Device Binding
    /// <summary>
    /// 디바이스 바인딩 설정
    /// </summary>
    /// <param name="metadata">토큰 메타데이터</param>
    /// <param name="userId">사용자 ID</param>
    private static void SetupDeviceBinding(TokenMetadata metadata, string userId)
    {
        try
        {
            string deviceFingerprint = CryptoHelper.GenerateDeviceFingerprint();
            string bindingToken = CryptoHelper.GenerateDeviceBindingToken(userId, deviceFingerprint);

            metadata.DeviceBinding = new DeviceBindingInfo
            {
                DeviceFingerprint = deviceFingerprint,
                BindingToken = bindingToken,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            Debug.Log("[TokenStorage] Device binding setup completed");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to setup device binding: {ex.Message}");
        }
    }

    /// <summary>
    /// 디바이스 바인딩 검증
    /// </summary>
    /// <param name="userId">검증할 사용자 ID</param>
    /// <returns>바인딩 유효 여부</returns>
    public static bool ValidateDeviceBinding(string userId)
    {
        try
        {
            var metadata = GetTokenMetadata();
            if (metadata?.DeviceBinding == null) return false;

            // 사용자 ID 일치 확인
            if (metadata.DeviceBinding.UserId != userId) return false;

            // 디바이스 지문 일치 확인
            string currentFingerprint = CryptoHelper.GenerateDeviceFingerprint();
            return metadata.DeviceBinding.DeviceFingerprint == currentFingerprint;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Device binding validation failed: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Token History
    /// <summary>
    /// 토큰 히스토리에 추가
    /// </summary>
    /// <param name="tokenInfo">토큰 정보</param>
    private static void AddToTokenHistory(TokenInfo tokenInfo)
    {
        try
        {
            var history = GetTokenHistory();
            
            // 액세스 토큰만 히스토리에 추가
            if (tokenInfo.TokenType == TokenType.AccessToken)
            {
                history.Add(new TokenHistoryEntry
                {
                    TokenHash = CryptoHelper.ComputeSHA256Hash(tokenInfo.Token),
                    StoredAt = tokenInfo.StoredAt,
                    ExpirationTime = tokenInfo.ExpirationTime,
                    UserId = tokenInfo.UserId
                });

                // 최대 개수 초과 시 오래된 항목 제거
                while (history.Count > MAX_TOKEN_HISTORY_COUNT)
                {
                    history.RemoveAt(0);
                }

                SaveTokenHistory(history);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to add to token history: {ex.Message}");
        }
    }

    /// <summary>
    /// 토큰 히스토리 반환
    /// </summary>
    /// <returns>토큰 히스토리 목록</returns>
    public static List<TokenHistoryEntry> GetTokenHistory()
    {
        try
        {
            string historyData = PlayerPrefs.GetString(TOKEN_HISTORY_KEY, "");
            if (string.IsNullOrEmpty(historyData))
                return new List<TokenHistoryEntry>();

            var wrapper = JsonUtility.FromJson<TokenHistoryWrapper>(historyData);
            return wrapper?.Entries ?? new List<TokenHistoryEntry>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to get token history: {ex.Message}");
            return new List<TokenHistoryEntry>();
        }
    }
    #endregion

    #region Storage Management
    /// <summary>
    /// 모든 토큰 데이터 삭제
    /// </summary>
    public static void ClearAllTokens()
    {
        try
        {
            SecureStorage.ClearTokens();
            PlayerPrefs.DeleteKey(TOKEN_METADATA_KEY);
            PlayerPrefs.DeleteKey(TOKEN_HISTORY_KEY);
            PlayerPrefs.Save();

            Debug.Log("[TokenStorage] All tokens cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to clear all tokens: {ex.Message}");
        }
    }

    /// <summary>
    /// 토큰 저장소 통계 반환
    /// </summary>
    /// <returns>저장소 통계 정보</returns>
    public static TokenStorageStats GetStorageStats()
    {
        try
        {
            var metadata = GetTokenMetadata();
            var history = GetTokenHistory();
            var validation = ValidateStoredTokens();

            return new TokenStorageStats
            {
                IsInitialized = IsInitialized,
                HasAccessToken = metadata?.AccessToken != null,
                HasRefreshToken = metadata?.RefreshToken != null,
                HasDeviceBinding = metadata?.DeviceBinding != null,
                AccessTokenExpiry = metadata?.AccessToken?.ExpirationTime,
                RefreshTokenExpiry = metadata?.RefreshToken?.ExpirationTime,
                HistoryCount = history.Count,
                IsDeviceBound = IsDeviceBound,
                ValidationResult = validation
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to get storage stats: {ex.Message}");
            return new TokenStorageStats { IsInitialized = false };
        }
    }
    #endregion

    #region Private Helpers
    /// <summary>
    /// 토큰 메타데이터 로드
    /// </summary>
    private static TokenMetadata GetTokenMetadata()
    {
        try
        {
            string metadataJson = PlayerPrefs.GetString(TOKEN_METADATA_KEY, "");
            if (string.IsNullOrEmpty(metadataJson))
                return null;

            return JsonUtility.FromJson<TokenMetadata>(metadataJson);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to get token metadata: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 토큰 메타데이터 저장
    /// </summary>
    private static void SaveTokenMetadata(TokenMetadata metadata)
    {
        try
        {
            string metadataJson = JsonUtility.ToJson(metadata);
            PlayerPrefs.SetString(TOKEN_METADATA_KEY, metadataJson);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to save token metadata: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 토큰 히스토리 저장
    /// </summary>
    private static void SaveTokenHistory(List<TokenHistoryEntry> history)
    {
        try
        {
            var wrapper = new TokenHistoryWrapper { Entries = history };
            string historyJson = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(TOKEN_HISTORY_KEY, historyJson);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to save token history: {ex.Message}");
        }
    }

    /// <summary>
    /// JWT 토큰에서 만료 시간 추출
    /// </summary>
    private static DateTime? ExtractTokenExpiration(string token)
    {
        try
        {
            return TokenValidator.GetTokenExpiration(token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to extract token expiration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 저장된 토큰 유효성 검사 및 정리
    /// </summary>
    private static void ValidateAndCleanupStoredTokens()
    {
        try
        {
            int cleanedCount = CleanupExpiredTokens();
            if (cleanedCount > 0)
            {
                Debug.Log($"[TokenStorage] Cleaned up {cleanedCount} expired tokens during initialization");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TokenStorage] Failed to validate and cleanup stored tokens: {ex.Message}");
        }
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 토큰 유형
/// </summary>
public enum TokenType
{
    AccessToken,
    RefreshToken
}

/// <summary>
/// 토큰 정보
/// </summary>
[Serializable]
public class TokenInfo
{
    public string Token;
    public TokenType TokenType;
    public DateTime StoredAt;
    public DateTime ExpirationTime;
    public string UserId;
}

/// <summary>
/// 디바이스 바인딩 정보
/// </summary>
[Serializable]
public class DeviceBindingInfo
{
    public string DeviceFingerprint;
    public string BindingToken;
    public string UserId;
    public DateTime CreatedAt;
}

/// <summary>
/// 토큰 메타데이터
/// </summary>
[Serializable]
public class TokenMetadata
{
    public TokenInfo AccessToken;
    public TokenInfo RefreshToken;
    public DeviceBindingInfo DeviceBinding;
}

/// <summary>
/// 토큰 검증 결과
/// </summary>
[Serializable]
public class TokenValidationResult
{
    public bool IsValid;
    public bool AccessTokenValid;
    public bool RefreshTokenValid;
    public bool DeviceBindingValid;
    public List<string> Issues = new List<string>();
    public string ErrorMessage;
}

/// <summary>
/// 토큰 히스토리 항목
/// </summary>
[Serializable]
public class TokenHistoryEntry
{
    public string TokenHash;
    public DateTime StoredAt;
    public DateTime ExpirationTime;
    public string UserId;
}

/// <summary>
/// 토큰 히스토리 래퍼 (JsonUtility용)
/// </summary>
[Serializable]
public class TokenHistoryWrapper
{
    public List<TokenHistoryEntry> Entries = new List<TokenHistoryEntry>();
}

/// <summary>
/// 토큰 저장소 통계
/// </summary>
[Serializable]
public class TokenStorageStats
{
    public bool IsInitialized;
    public bool HasAccessToken;
    public bool HasRefreshToken;
    public bool HasDeviceBinding;
    public DateTime? AccessTokenExpiry;
    public DateTime? RefreshTokenExpiry;
    public int HistoryCount;
    public bool IsDeviceBound;
    public TokenValidationResult ValidationResult;
}
#endregion