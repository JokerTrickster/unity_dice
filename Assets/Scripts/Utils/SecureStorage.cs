using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 보안 스토리지 유틸리티
/// 민감한 데이터(토큰, 자격증명)를 안전하게 저장하고 관리합니다.
/// </summary>
public static class SecureStorage
{
    #region Constants
    private const string AUTH_TOKEN_KEY = "secure_auth_token";
    private const string REFRESH_TOKEN_KEY = "secure_refresh_token";
    private const string USER_CREDENTIALS_KEY = "secure_user_credentials";
    private const string ENCRYPTION_KEY_KEY = "secure_encryption_key";
    #endregion

    #region Properties
    /// <summary>
    /// 암호화 키가 초기화되었는지 여부
    /// </summary>
    public static bool IsInitialized { get; private set; }
    
    /// <summary>
    /// 저장된 인증 토큰이 있는지 여부
    /// </summary>
    public static bool HasAuthToken => !string.IsNullOrEmpty(GetAuthToken());
    
    /// <summary>
    /// 저장된 리프레시 토큰이 있는지 여부
    /// </summary>
    public static bool HasRefreshToken => !string.IsNullOrEmpty(GetRefreshToken());
    #endregion

    #region Initialization
    /// <summary>
    /// 보안 스토리지 초기화
    /// </summary>
    public static void Initialize()
    {
        try
        {
            // 암호화 키가 없으면 생성
            if (!PlayerPrefs.HasKey(ENCRYPTION_KEY_KEY))
            {
                GenerateEncryptionKey();
            }
            
            IsInitialized = true;
            Debug.Log("[SecureStorage] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Initialization failed: {ex.Message}");
            IsInitialized = false;
        }
    }

    /// <summary>
    /// 새로운 암호화 키 생성
    /// </summary>
    private static void GenerateEncryptionKey()
    {
        try
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] keyBytes = new byte[32]; // 256 bits
                rng.GetBytes(keyBytes);
                string encodedKey = Convert.ToBase64String(keyBytes);
                PlayerPrefs.SetString(ENCRYPTION_KEY_KEY, encodedKey);
                PlayerPrefs.Save();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to generate encryption key: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 암호화 키 반환
    /// </summary>
    private static byte[] GetEncryptionKey()
    {
        if (!PlayerPrefs.HasKey(ENCRYPTION_KEY_KEY))
        {
            throw new InvalidOperationException("Encryption key not found. Initialize SecureStorage first.");
        }
        
        string encodedKey = PlayerPrefs.GetString(ENCRYPTION_KEY_KEY);
        return Convert.FromBase64String(encodedKey);
    }
    #endregion

    #region Token Management
    /// <summary>
    /// 인증 토큰 저장
    /// </summary>
    public static void SaveAuthToken(string token)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(token))
            {
                PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
                return;
            }

            string encryptedToken = EncryptString(token);
            PlayerPrefs.SetString(AUTH_TOKEN_KEY, encryptedToken);
            PlayerPrefs.Save();
            
            Debug.Log("[SecureStorage] Auth token saved successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to save auth token: {ex.Message}");
        }
    }

    /// <summary>
    /// 인증 토큰 반환
    /// </summary>
    public static string GetAuthToken()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return null;
        }

        try
        {
            if (!PlayerPrefs.HasKey(AUTH_TOKEN_KEY))
                return null;

            string encryptedToken = PlayerPrefs.GetString(AUTH_TOKEN_KEY);
            if (string.IsNullOrEmpty(encryptedToken))
                return null;

            return DecryptString(encryptedToken);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to get auth token: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 리프레시 토큰 저장
    /// </summary>
    public static void SaveRefreshToken(string token)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(token))
            {
                PlayerPrefs.DeleteKey(REFRESH_TOKEN_KEY);
                return;
            }

            string encryptedToken = EncryptString(token);
            PlayerPrefs.SetString(REFRESH_TOKEN_KEY, encryptedToken);
            PlayerPrefs.Save();
            
            Debug.Log("[SecureStorage] Refresh token saved successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to save refresh token: {ex.Message}");
        }
    }

    /// <summary>
    /// 리프레시 토큰 반환
    /// </summary>
    public static string GetRefreshToken()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return null;
        }

        try
        {
            if (!PlayerPrefs.HasKey(REFRESH_TOKEN_KEY))
                return null;

            string encryptedToken = PlayerPrefs.GetString(REFRESH_TOKEN_KEY);
            if (string.IsNullOrEmpty(encryptedToken))
                return null;

            return DecryptString(encryptedToken);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to get refresh token: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region User Credentials
    /// <summary>
    /// 사용자 자격증명 저장
    /// </summary>
    public static void SaveUserCredentials(string userId, string userInfo)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return;
        }

        try
        {
            var credentials = new UserCredentials
            {
                UserId = userId,
                UserInfo = userInfo,
                SavedAt = DateTime.UtcNow
            };

            string json = JsonUtility.ToJson(credentials);
            string encryptedCredentials = EncryptString(json);
            PlayerPrefs.SetString(USER_CREDENTIALS_KEY, encryptedCredentials);
            PlayerPrefs.Save();
            
            Debug.Log("[SecureStorage] User credentials saved successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to save user credentials: {ex.Message}");
        }
    }

    /// <summary>
    /// 사용자 자격증명 반환
    /// </summary>
    public static UserCredentials GetUserCredentials()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SecureStorage] Not initialized. Call Initialize() first.");
            return null;
        }

        try
        {
            if (!PlayerPrefs.HasKey(USER_CREDENTIALS_KEY))
                return null;

            string encryptedCredentials = PlayerPrefs.GetString(USER_CREDENTIALS_KEY);
            if (string.IsNullOrEmpty(encryptedCredentials))
                return null;

            string json = DecryptString(encryptedCredentials);
            return JsonUtility.FromJson<UserCredentials>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to get user credentials: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 모든 보안 데이터 삭제
    /// </summary>
    public static void ClearAllSecureData()
    {
        try
        {
            PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(REFRESH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(USER_CREDENTIALS_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("[SecureStorage] All secure data cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to clear secure data: {ex.Message}");
        }
    }

    /// <summary>
    /// 토큰만 삭제 (로그아웃 시)
    /// </summary>
    public static void ClearTokens()
    {
        try
        {
            PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(REFRESH_TOKEN_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("[SecureStorage] Tokens cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to clear tokens: {ex.Message}");
        }
    }

    /// <summary>
    /// 암호화 키 재생성 (보안 강화)
    /// </summary>
    public static void RegenerateEncryptionKey()
    {
        try
        {
            // 기존 데이터 백업
            var authToken = GetAuthToken();
            var refreshToken = GetRefreshToken();
            var credentials = GetUserCredentials();

            // 새 키 생성
            GenerateEncryptionKey();

            // 데이터 재암호화
            if (!string.IsNullOrEmpty(authToken))
                SaveAuthToken(authToken);
            if (!string.IsNullOrEmpty(refreshToken))
                SaveRefreshToken(refreshToken);
            if (credentials != null)
                SaveUserCredentials(credentials.UserId, credentials.UserInfo);

            Debug.Log("[SecureStorage] Encryption key regenerated successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Failed to regenerate encryption key: {ex.Message}");
        }
    }
    #endregion

    #region Encryption/Decryption
    /// <summary>
    /// 문자열 암호화
    /// </summary>
    private static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] key = GetEncryptionKey();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    
                    // IV와 암호화된 데이터를 합쳐서 저장
                    byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
                    Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                    Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
                    
                    return Convert.ToBase64String(result);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Encryption failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 문자열 복호화
    /// </summary>
    private static string DecryptString(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            byte[] key = GetEncryptionKey();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                
                // IV 추출
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;
                
                // 암호화된 데이터 추출
                byte[] encryptedBytes = new byte[cipherBytes.Length - iv.Length];
                Array.Copy(cipherBytes, iv.Length, encryptedBytes, 0, encryptedBytes.Length);
                
                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SecureStorage] Decryption failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 저장된 데이터 유효성 검사
    /// </summary>
    public static bool ValidateStoredData()
    {
        try
        {
            // 암호화/복호화 테스트
            const string testData = "test_validation_data";
            string encrypted = EncryptString(testData);
            string decrypted = DecryptString(encrypted);
            
            return testData == decrypted;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 스토리지 상태 정보 반환
    /// </summary>
    public static SecureStorageInfo GetStorageInfo()
    {
        return new SecureStorageInfo
        {
            IsInitialized = IsInitialized,
            HasAuthToken = HasAuthToken,
            HasRefreshToken = HasRefreshToken,
            HasUserCredentials = PlayerPrefs.HasKey(USER_CREDENTIALS_KEY),
            IsDataValid = ValidateStoredData()
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 사용자 자격증명 데이터
/// </summary>
[Serializable]
public class UserCredentials
{
    public string UserId;
    public string UserInfo;
    public DateTime SavedAt;
}

/// <summary>
/// 보안 스토리지 상태 정보
/// </summary>
[Serializable]
public class SecureStorageInfo
{
    public bool IsInitialized;
    public bool HasAuthToken;
    public bool HasRefreshToken;
    public bool HasUserCredentials;
    public bool IsDataValid;
}
#endregion