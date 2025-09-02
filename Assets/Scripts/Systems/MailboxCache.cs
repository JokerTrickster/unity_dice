using System;
using UnityEngine;

/// <summary>
/// 우편함 로컬 캐시 관리자
/// PlayerPrefs와 CryptoHelper를 활용한 보안 캐싱 시스템
/// </summary>
public static class MailboxCache
{
    #region Constants
    private const string MAILBOX_CACHE_KEY = "mailbox_data_encrypted";
    private const string CACHE_TIMESTAMP_KEY = "mailbox_cache_timestamp";
    private const string CACHE_VERSION_KEY = "mailbox_cache_version";
    private const string CACHE_USER_KEY = "mailbox_cache_user_id";
    
    private const int CACHE_EXPIRY_HOURS = 6;
    private const string CACHE_VERSION = "1.0";
    private const string ENCRYPTION_PASSWORD_PREFIX = "mailbox_cache_";
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 우편함 데이터를 캐시에 저장
    /// </summary>
    /// <param name="data">저장할 우편함 데이터</param>
    /// <param name="userId">현재 사용자 ID</param>
    /// <returns>저장 성공 여부</returns>
    public static bool SaveToCache(MailboxData data, string userId)
    {
        if (data == null)
        {
            Debug.LogError("[MailboxCache] Cannot save null data to cache");
            return false;
        }
        
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[MailboxCache] Cannot save cache without user ID");
            return false;
        }
        
        try
        {
            // 데이터 유효성 검증
            if (!data.IsValid())
            {
                Debug.LogWarning("[MailboxCache] Data validation failed, attempting to fix");
                data.RecalculateUnreadCount();
                
                if (!data.IsValid())
                {
                    Debug.LogError("[MailboxCache] Data validation failed after fix attempt");
                    return false;
                }
            }
            
            // 타임스탬프 업데이트
            data.LastSyncTime = DateTime.UtcNow;
            
            // JSON 직렬화
            string jsonData = JsonUtility.ToJson(data, false);
            if (string.IsNullOrEmpty(jsonData))
            {
                Debug.LogError("[MailboxCache] JSON serialization failed");
                return false;
            }
            
            // 암호화
            string encryptionPassword = GenerateEncryptionPassword(userId);
            string encryptedData = CryptoHelper.EncryptAES(jsonData, encryptionPassword);
            
            // PlayerPrefs에 저장
            PlayerPrefs.SetString(MAILBOX_CACHE_KEY, encryptedData);
            PlayerPrefs.SetString(CACHE_TIMESTAMP_KEY, DateTime.UtcNow.ToBinary().ToString());
            PlayerPrefs.SetString(CACHE_VERSION_KEY, CACHE_VERSION);
            PlayerPrefs.SetString(CACHE_USER_KEY, userId);
            PlayerPrefs.Save();
            
            Debug.Log($"[MailboxCache] Saved {data.messages?.Count ?? 0} messages to cache for user {userId}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxCache] Failed to save cache: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 캐시에서 우편함 데이터 로드
    /// </summary>
    /// <param name="userId">현재 사용자 ID</param>
    /// <returns>캐시된 우편함 데이터, 캐시가 없거나 만료된 경우 null</returns>
    public static MailboxData LoadFromCache(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[MailboxCache] Cannot load cache without user ID");
            return null;
        }
        
        try
        {
            // 캐시 존재 확인
            if (!HasValidCache())
            {
                return null;
            }
            
            // 사용자 ID 검증
            string cachedUserId = PlayerPrefs.GetString(CACHE_USER_KEY, "");
            if (cachedUserId != userId)
            {
                Debug.LogWarning($"[MailboxCache] Cache user ID mismatch: expected {userId}, got {cachedUserId}");
                ClearCache();
                return null;
            }
            
            // 캐시 만료 확인
            if (IsCacheExpired())
            {
                Debug.Log("[MailboxCache] Cache expired, clearing");
                ClearCache();
                return null;
            }
            
            // 암호화된 데이터 로드
            string encryptedData = PlayerPrefs.GetString(MAILBOX_CACHE_KEY, "");
            if (string.IsNullOrEmpty(encryptedData))
            {
                Debug.LogWarning("[MailboxCache] No encrypted data found in cache");
                return null;
            }
            
            // 복호화
            string encryptionPassword = GenerateEncryptionPassword(userId);
            string jsonData = CryptoHelper.DecryptAES(encryptedData, encryptionPassword);
            
            if (string.IsNullOrEmpty(jsonData))
            {
                Debug.LogError("[MailboxCache] Failed to decrypt cache data");
                ClearCache();
                return null;
            }
            
            // JSON 역직렬화
            MailboxData data = JsonUtility.FromJson<MailboxData>(jsonData);
            if (data == null)
            {
                Debug.LogError("[MailboxCache] Failed to deserialize cache data");
                ClearCache();
                return null;
            }
            
            // 데이터 유효성 검증
            if (!data.IsValid())
            {
                Debug.LogWarning("[MailboxCache] Cached data validation failed, attempting to fix");
                data.RecalculateUnreadCount();
                
                if (!data.IsValid())
                {
                    Debug.LogError("[MailboxCache] Cached data is corrupted");
                    ClearCache();
                    return null;
                }
            }
            
            Debug.Log($"[MailboxCache] Loaded {data.messages?.Count ?? 0} messages from cache for user {userId}");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxCache] Failed to load cache: {e.Message}");
            ClearCache();
            return null;
        }
    }
    
    /// <summary>
    /// 캐시 클리어
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            PlayerPrefs.DeleteKey(MAILBOX_CACHE_KEY);
            PlayerPrefs.DeleteKey(CACHE_TIMESTAMP_KEY);
            PlayerPrefs.DeleteKey(CACHE_VERSION_KEY);
            PlayerPrefs.DeleteKey(CACHE_USER_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("[MailboxCache] Cache cleared");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxCache] Failed to clear cache: {e.Message}");
        }
    }
    
    /// <summary>
    /// 캐시 존재 여부 확인
    /// </summary>
    /// <returns>유효한 캐시가 존재하는지 여부</returns>
    public static bool HasCache()
    {
        return HasValidCache() && !IsCacheExpired();
    }
    
    /// <summary>
    /// 캐시 만료 여부 확인
    /// </summary>
    /// <returns>캐시가 만료되었는지 여부</returns>
    public static bool IsCacheExpired()
    {
        try
        {
            string timestampStr = PlayerPrefs.GetString(CACHE_TIMESTAMP_KEY, "");
            if (string.IsNullOrEmpty(timestampStr))
                return true;
            
            if (!long.TryParse(timestampStr, out long timestampBinary))
                return true;
            
            DateTime cacheTime = DateTime.FromBinary(timestampBinary);
            TimeSpan timeSinceCache = DateTime.UtcNow - cacheTime;
            
            return timeSinceCache.TotalHours > CACHE_EXPIRY_HOURS;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxCache] Error checking cache expiry: {e.Message}");
            return true;
        }
    }
    
    /// <summary>
    /// 캐시 통계 정보 가져오기
    /// </summary>
    /// <returns>캐시 통계</returns>
    public static MailboxCacheInfo GetCacheInfo()
    {
        var info = new MailboxCacheInfo();
        
        try
        {
            info.HasCache = HasValidCache();
            info.IsExpired = IsCacheExpired();
            info.CacheVersion = PlayerPrefs.GetString(CACHE_VERSION_KEY, "");
            info.UserId = PlayerPrefs.GetString(CACHE_USER_KEY, "");
            
            string timestampStr = PlayerPrefs.GetString(CACHE_TIMESTAMP_KEY, "");
            if (!string.IsNullOrEmpty(timestampStr) && long.TryParse(timestampStr, out long timestampBinary))
            {
                info.CacheTime = DateTime.FromBinary(timestampBinary);
                info.TimeUntilExpiry = TimeSpan.FromHours(CACHE_EXPIRY_HOURS) - (DateTime.UtcNow - info.CacheTime);
                
                if (info.TimeUntilExpiry.TotalSeconds < 0)
                    info.TimeUntilExpiry = TimeSpan.Zero;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxCache] Error getting cache info: {e.Message}");
        }
        
        return info;
    }
    #endregion
    
    #region Private Methods
    /// <summary>
    /// 유효한 캐시 존재 확인 (기본 검증)
    /// </summary>
    private static bool HasValidCache()
    {
        // 기본 키 존재 확인
        if (!PlayerPrefs.HasKey(MAILBOX_CACHE_KEY) || 
            !PlayerPrefs.HasKey(CACHE_TIMESTAMP_KEY) ||
            !PlayerPrefs.HasKey(CACHE_VERSION_KEY) ||
            !PlayerPrefs.HasKey(CACHE_USER_KEY))
        {
            return false;
        }
        
        // 버전 확인
        string cacheVersion = PlayerPrefs.GetString(CACHE_VERSION_KEY, "");
        if (cacheVersion != CACHE_VERSION)
        {
            Debug.LogWarning($"[MailboxCache] Cache version mismatch: expected {CACHE_VERSION}, got {cacheVersion}");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 사용자별 암호화 패스워드 생성
    /// </summary>
    private static string GenerateEncryptionPassword(string userId)
    {
        // 디바이스 고유 정보와 사용자 ID를 조합해 패스워드 생성
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        string combined = $"{ENCRYPTION_PASSWORD_PREFIX}{userId}_{deviceId}";
        
        // SHA-256 해시로 안전한 패스워드 생성
        return CryptoHelper.ComputeSHA256Hash(combined);
    }
    #endregion
}

/// <summary>
/// 우편함 캐시 정보
/// </summary>
[Serializable]
public class MailboxCacheInfo
{
    public bool HasCache;
    public bool IsExpired;
    public string CacheVersion;
    public string UserId;
    public DateTime CacheTime;
    public TimeSpan TimeUntilExpiry;
    
    public MailboxCacheInfo()
    {
        HasCache = false;
        IsExpired = true;
        CacheVersion = "";
        UserId = "";
        CacheTime = DateTime.MinValue;
        TimeUntilExpiry = TimeSpan.Zero;
    }
}