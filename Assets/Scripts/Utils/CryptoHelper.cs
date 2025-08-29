using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 암호화 관련 유틸리티 클래스
/// 토큰 및 민감한 데이터의 암호화/복호화, 해싱, 서명 검증을 담당합니다.
/// </summary>
public static class CryptoHelper
{
    #region Constants
    private const int AES_KEY_SIZE = 256;
    private const int AES_BLOCK_SIZE = 128;
    private const int PBKDF2_ITERATIONS = 10000;
    private const int SALT_SIZE = 16;
    private const string DEVICE_KEY_PREFIX = "unity_dice_device_";
    #endregion

    #region AES Encryption/Decryption
    /// <summary>
    /// AES 암호화 (CBC 모드)
    /// </summary>
    /// <param name="plainText">암호화할 평문</param>
    /// <param name="password">암호화 키로 사용할 패스워드</param>
    /// <returns>암호화된 데이터 (Base64)</returns>
    public static string EncryptAES(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            
            using (var aes = Aes.Create())
            {
                aes.KeySize = AES_KEY_SIZE;
                aes.BlockSize = AES_BLOCK_SIZE;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Salt 생성
                byte[] salt = GenerateRandomBytes(SALT_SIZE);
                
                // PBKDF2를 사용해 키와 IV 생성
                var keyData = GenerateKeyAndIV(password, salt, aes.KeySize / 8, aes.BlockSize / 8);
                aes.Key = keyData.Key;
                aes.IV = keyData.IV;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    
                    // Salt + IV + 암호화된 데이터를 결합
                    byte[] result = new byte[salt.Length + aes.IV.Length + encryptedBytes.Length];
                    Array.Copy(salt, 0, result, 0, salt.Length);
                    Array.Copy(aes.IV, 0, result, salt.Length, aes.IV.Length);
                    Array.Copy(encryptedBytes, 0, result, salt.Length + aes.IV.Length, encryptedBytes.Length);
                    
                    return Convert.ToBase64String(result);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] AES encryption failed: {ex.Message}");
            throw new CryptographicException("AES encryption failed", ex);
        }
    }

    /// <summary>
    /// AES 복호화 (CBC 모드)
    /// </summary>
    /// <param name="cipherText">복호화할 암호문 (Base64)</param>
    /// <param name="password">복호화 키로 사용할 패스워드</param>
    /// <returns>복호화된 평문</returns>
    public static string DecryptAES(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            
            using (var aes = Aes.Create())
            {
                aes.KeySize = AES_KEY_SIZE;
                aes.BlockSize = AES_BLOCK_SIZE;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Salt 추출
                byte[] salt = new byte[SALT_SIZE];
                Array.Copy(cipherBytes, 0, salt, 0, SALT_SIZE);

                // IV 추출
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherBytes, SALT_SIZE, iv, 0, iv.Length);
                
                // 암호화된 데이터 추출
                byte[] encryptedBytes = new byte[cipherBytes.Length - SALT_SIZE - iv.Length];
                Array.Copy(cipherBytes, SALT_SIZE + iv.Length, encryptedBytes, 0, encryptedBytes.Length);

                // PBKDF2를 사용해 키 생성
                var keyData = GenerateKeyAndIV(password, salt, aes.KeySize / 8, iv.Length);
                aes.Key = keyData.Key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] AES decryption failed: {ex.Message}");
            throw new CryptographicException("AES decryption failed", ex);
        }
    }
    #endregion

    #region Hash Functions
    /// <summary>
    /// SHA-256 해시 생성
    /// </summary>
    /// <param name="input">해시할 입력 문자열</param>
    /// <returns>SHA-256 해시 (Base64)</returns>
    public static string ComputeSHA256Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] SHA256 hashing failed: {ex.Message}");
            throw new CryptographicException("SHA256 hashing failed", ex);
        }
    }

    /// <summary>
    /// HMAC-SHA256 서명 생성
    /// </summary>
    /// <param name="message">서명할 메시지</param>
    /// <param name="secretKey">서명 키</param>
    /// <returns>HMAC-SHA256 서명 (Base64)</returns>
    public static string ComputeHMACSHA256(string message, string secretKey)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(secretKey))
            throw new ArgumentException("Message and secret key cannot be null or empty");

        try
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] HMAC-SHA256 computation failed: {ex.Message}");
            throw new CryptographicException("HMAC-SHA256 computation failed", ex);
        }
    }

    /// <summary>
    /// PBKDF2를 사용한 패스워드 해시 생성
    /// </summary>
    /// <param name="password">패스워드</param>
    /// <param name="salt">솔트 (없으면 자동 생성)</param>
    /// <param name="iterations">반복 횟수</param>
    /// <returns>솔트와 해시가 포함된 결과</returns>
    public static PasswordHashResult ComputePBKDF2Hash(string password, byte[] salt = null, int iterations = PBKDF2_ITERATIONS)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        try
        {
            salt ??= GenerateRandomBytes(SALT_SIZE);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32); // 256 bits
                
                return new PasswordHashResult
                {
                    Hash = Convert.ToBase64String(hash),
                    Salt = Convert.ToBase64String(salt),
                    Iterations = iterations
                };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] PBKDF2 hashing failed: {ex.Message}");
            throw new CryptographicException("PBKDF2 hashing failed", ex);
        }
    }

    /// <summary>
    /// PBKDF2 패스워드 검증
    /// </summary>
    /// <param name="password">검증할 패스워드</param>
    /// <param name="hashResult">저장된 해시 결과</param>
    /// <returns>패스워드가 일치하는지 여부</returns>
    public static bool VerifyPBKDF2Hash(string password, PasswordHashResult hashResult)
    {
        if (string.IsNullOrEmpty(password) || hashResult == null)
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(hashResult.Salt);
            var newHashResult = ComputePBKDF2Hash(password, salt, hashResult.Iterations);
            return newHashResult.Hash == hashResult.Hash;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] PBKDF2 verification failed: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region JWT Token Helpers
    /// <summary>
    /// JWT 토큰 서명 검증 (HMAC-SHA256)
    /// </summary>
    /// <param name="token">검증할 JWT 토큰</param>
    /// <param name="secretKey">서명 검증 키</param>
    /// <returns>서명이 유효한지 여부</returns>
    public static bool VerifyJWTSignature(string token, string secretKey)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(secretKey))
            return false;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            // 헤더와 페이로드를 결합
            string headerAndPayload = $"{parts[0]}.{parts[1]}";
            
            // 서명 계산
            string expectedSignature = ComputeHMACSHA256(headerAndPayload, secretKey);
            string actualSignature = parts[2];

            // Base64 URL 디코딩을 위한 변환
            actualSignature = actualSignature.Replace('-', '+').Replace('_', '/');
            switch (actualSignature.Length % 4)
            {
                case 2: actualSignature += "=="; break;
                case 3: actualSignature += "="; break;
            }

            return expectedSignature == actualSignature;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] JWT signature verification failed: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Device Fingerprinting
    /// <summary>
    /// 디바이스 고유 식별자 생성
    /// </summary>
    /// <returns>디바이스 고유 해시값</returns>
    public static string GenerateDeviceFingerprint()
    {
        try
        {
            var deviceInfo = new StringBuilder();
            
            // Unity에서 제공하는 디바이스 정보 수집
            deviceInfo.Append(SystemInfo.deviceUniqueIdentifier);
            deviceInfo.Append(SystemInfo.deviceModel);
            deviceInfo.Append(SystemInfo.processorType);
            deviceInfo.Append(SystemInfo.graphicsDeviceID);
            deviceInfo.Append(Application.version);
            
            return ComputeSHA256Hash(deviceInfo.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] Device fingerprint generation failed: {ex.Message}");
            // 실패 시 기본값 반환
            return ComputeSHA256Hash($"{DEVICE_KEY_PREFIX}{DateTime.UtcNow.Ticks}");
        }
    }

    /// <summary>
    /// 디바이스 바인딩 토큰 생성
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <param name="deviceFingerprint">디바이스 지문</param>
    /// <returns>디바이스 바인딩 토큰</returns>
    public static string GenerateDeviceBindingToken(string userId, string deviceFingerprint)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(deviceFingerprint))
            throw new ArgumentException("UserId and device fingerprint cannot be null or empty");

        try
        {
            string combinedData = $"{userId}:{deviceFingerprint}:{DateTime.UtcNow.Ticks}";
            return ComputeSHA256Hash(combinedData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] Device binding token generation failed: {ex.Message}");
            throw new CryptographicException("Device binding token generation failed", ex);
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 암호학적으로 안전한 랜덤 바이트 배열 생성
    /// </summary>
    /// <param name="size">바이트 배열 크기</param>
    /// <returns>랜덤 바이트 배열</returns>
    public static byte[] GenerateRandomBytes(int size)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be greater than 0", nameof(size));

        try
        {
            byte[] bytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] Random bytes generation failed: {ex.Message}");
            throw new CryptographicException("Random bytes generation failed", ex);
        }
    }

    /// <summary>
    /// Base64 URL 안전 인코딩
    /// </summary>
    /// <param name="input">인코딩할 바이트 배열</param>
    /// <returns>Base64 URL 안전 문자열</returns>
    public static string Base64UrlEncode(byte[] input)
    {
        if (input == null || input.Length == 0)
            return string.Empty;

        string base64 = Convert.ToBase64String(input);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Base64 URL 안전 디코딩
    /// </summary>
    /// <param name="input">디코딩할 Base64 URL 안전 문자열</param>
    /// <returns>디코딩된 바이트 배열</returns>
    public static byte[] Base64UrlDecode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new byte[0];

        try
        {
            // Base64 URL을 일반 Base64로 변환
            string base64 = input.Replace('-', '+').Replace('_', '/');
            
            // 패딩 추가
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            return Convert.FromBase64String(base64);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CryptoHelper] Base64 URL decoding failed: {ex.Message}");
            throw new FormatException("Invalid Base64 URL format", ex);
        }
    }

    /// <summary>
    /// PBKDF2를 사용해 키와 IV 생성
    /// </summary>
    /// <param name="password">패스워드</param>
    /// <param name="salt">솔트</param>
    /// <param name="keySize">키 크기 (바이트)</param>
    /// <param name="ivSize">IV 크기 (바이트)</param>
    /// <returns>키와 IV</returns>
    private static (byte[] Key, byte[] IV) GenerateKeyAndIV(string password, byte[] salt, int keySize, int ivSize)
    {
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, PBKDF2_ITERATIONS, HashAlgorithmName.SHA256))
        {
            byte[] key = pbkdf2.GetBytes(keySize);
            byte[] iv = pbkdf2.GetBytes(ivSize);
            return (key, iv);
        }
    }

    /// <summary>
    /// 보안 문자열 비교 (타이밍 공격 방지)
    /// </summary>
    /// <param name="a">첫 번째 문자열</param>
    /// <param name="b">두 번째 문자열</param>
    /// <returns>문자열이 같은지 여부</returns>
    public static bool SecureStringCompare(string a, string b)
    {
        if (a == null || b == null)
            return a == b;

        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        
        return result == 0;
    }
    #endregion
}

#region Data Classes
/// <summary>
/// PBKDF2 해시 결과
/// </summary>
[Serializable]
public class PasswordHashResult
{
    public string Hash;
    public string Salt;
    public int Iterations;
}
#endregion