using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 방 코드 생성 시스템
/// 고유한 4자리 숫자 방 코드 생성, 중복 방지, 브루트포스 보안 기능 제공
/// </summary>
public class RoomCodeGenerator
{
    #region Constants
    private const int MIN_CODE = 1000;
    private const int MAX_CODE = 9999;
    private const int MAX_GENERATION_ATTEMPTS = 100;
    private const int MAX_BRUTE_FORCE_ATTEMPTS = 5;
    private const float BRUTE_FORCE_COOLDOWN = 300f; // 5분
    private const int RESERVED_CODES_COUNT = 50; // 예약된 코드 수
    #endregion

    #region Private Fields
    private static RoomCodeGenerator _instance;
    private HashSet<string> _activeCodes;
    private HashSet<string> _reservedCodes;
    private Dictionary<string, DateTime> _codeExpirations;
    private Dictionary<string, int> _bruteForceAttempts;
    private Dictionary<string, DateTime> _bruteForceCooldowns;
    private System.Random _random;
    #endregion

    #region Singleton
    public static RoomCodeGenerator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new RoomCodeGenerator();
            }
            return _instance;
        }
    }
    #endregion

    #region Constructor
    private RoomCodeGenerator()
    {
        _activeCodes = new HashSet<string>();
        _reservedCodes = new HashSet<string>();
        _codeExpirations = new Dictionary<string, DateTime>();
        _bruteForceAttempts = new Dictionary<string, int>();
        _bruteForceCooldowns = new Dictionary<string, DateTime>();
        _random = new System.Random();
        
        InitializeReservedCodes();
        Debug.Log("[RoomCodeGenerator] Initialized with reserved codes protection");
    }
    #endregion

    #region Code Generation
    /// <summary>
    /// 고유한 방 코드 생성
    /// </summary>
    public string GenerateRoomCode()
    {
        CleanupExpiredCodes();
        
        for (int attempt = 0; attempt < MAX_GENERATION_ATTEMPTS; attempt++)
        {
            string code = GenerateRandomCode();
            
            if (IsCodeAvailable(code))
            {
                RegisterCode(code);
                Debug.Log($"[RoomCodeGenerator] Generated room code: {code} (attempt {attempt + 1})");
                return code;
            }
        }
        
        Debug.LogError($"[RoomCodeGenerator] Failed to generate unique room code after {MAX_GENERATION_ATTEMPTS} attempts");
        throw new InvalidOperationException("Unable to generate unique room code. Too many active rooms.");
    }

    /// <summary>
    /// 방 코드 생성 및 예약 (즉시 활성화하지 않음)
    /// </summary>
    public string GenerateAndReserveCode()
    {
        string code = GenerateRoomCode();
        if (code != null)
        {
            // 생성된 코드를 활성 상태에서 제거하고 예약 상태로 변경
            _activeCodes.Remove(code);
            _reservedCodes.Add(code);
            _codeExpirations[code] = DateTime.Now.AddMinutes(5); // 5분간 예약
            
            Debug.Log($"[RoomCodeGenerator] Code {code} reserved for 5 minutes");
        }
        return code;
    }

    /// <summary>
    /// 예약된 코드 활성화
    /// </summary>
    public bool ActivateReservedCode(string code)
    {
        if (string.IsNullOrEmpty(code) || !_reservedCodes.Contains(code))
        {
            return false;
        }

        // 예약 만료 확인
        if (_codeExpirations.ContainsKey(code) && DateTime.Now > _codeExpirations[code])
        {
            _reservedCodes.Remove(code);
            _codeExpirations.Remove(code);
            Debug.LogWarning($"[RoomCodeGenerator] Reserved code {code} expired");
            return false;
        }

        // 예약에서 활성으로 이동
        _reservedCodes.Remove(code);
        _activeCodes.Add(code);
        _codeExpirations[code] = DateTime.Now.AddMinutes(30); // 30분 활성화
        
        Debug.Log($"[RoomCodeGenerator] Code {code} activated for 30 minutes");
        return true;
    }

    /// <summary>
    /// 랜덤 4자리 코드 생성
    /// </summary>
    private string GenerateRandomCode()
    {
        int code = _random.Next(MIN_CODE, MAX_CODE + 1);
        return code.ToString("D4");
    }

    /// <summary>
    /// 코드 사용 가능 여부 확인
    /// </summary>
    private bool IsCodeAvailable(string code)
    {
        return !_activeCodes.Contains(code) && 
               !_reservedCodes.Contains(code) &&
               !IsReservedSystemCode(code);
    }

    /// <summary>
    /// 시스템 예약 코드 확인
    /// </summary>
    private bool IsReservedSystemCode(string code)
    {
        // 연속된 숫자, 반복되는 숫자 등 예약된 패턴 확인
        if (IsConsecutiveDigits(code) || IsRepeatingDigits(code))
        {
            return true;
        }

        // 특별한 의미가 있는 숫자들 (생일, 기념일 등)
        string[] specialCodes = { "0000", "1111", "2222", "3333", "4444", "5555", 
                                "6666", "7777", "8888", "9999", "1234", "4321" };
        
        return Array.Exists(specialCodes, sc => sc == code);
    }

    /// <summary>
    /// 연속된 숫자인지 확인
    /// </summary>
    private bool IsConsecutiveDigits(string code)
    {
        for (int i = 1; i < code.Length; i++)
        {
            if (code[i] - code[i-1] != 1 && code[i-1] - code[i] != 1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 반복되는 숫자인지 확인
    /// </summary>
    private bool IsRepeatingDigits(string code)
    {
        return code.Distinct().Count() <= 2; // 2가지 이하의 서로 다른 숫자만 포함
    }
    #endregion

    #region Code Management
    /// <summary>
    /// 코드 등록 및 만료 시간 설정
    /// </summary>
    private void RegisterCode(string code)
    {
        _activeCodes.Add(code);
        _codeExpirations[code] = DateTime.Now.AddMinutes(30); // 30분 후 만료
    }

    /// <summary>
    /// 방 코드 해제
    /// </summary>
    public void ReleaseCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;

        bool removed = false;
        
        if (_activeCodes.Remove(code))
        {
            removed = true;
            Debug.Log($"[RoomCodeGenerator] Released active code: {code}");
        }
        
        if (_reservedCodes.Remove(code))
        {
            removed = true;
            Debug.Log($"[RoomCodeGenerator] Released reserved code: {code}");
        }

        _codeExpirations.Remove(code);

        if (!removed)
        {
            Debug.LogWarning($"[RoomCodeGenerator] Attempted to release non-existent code: {code}");
        }
    }

    /// <summary>
    /// 만료된 코드 정리
    /// </summary>
    public void CleanupExpiredCodes()
    {
        var now = DateTime.Now;
        var expiredCodes = new List<string>();

        foreach (var kvp in _codeExpirations)
        {
            if (now > kvp.Value)
            {
                expiredCodes.Add(kvp.Key);
            }
        }

        foreach (var expiredCode in expiredCodes)
        {
            ReleaseCode(expiredCode);
            Debug.Log($"[RoomCodeGenerator] Auto-released expired code: {expiredCode}");
        }

        // 브루트포스 쿨다운 정리
        var expiredCooldowns = _bruteForceCooldowns
            .Where(kvp => now > kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var ip in expiredCooldowns)
        {
            _bruteForceCooldowns.Remove(ip);
            _bruteForceAttempts.Remove(ip);
            Debug.Log($"[RoomCodeGenerator] Brute force cooldown expired for: {ip}");
        }
    }

    /// <summary>
    /// 코드 만료 시간 연장
    /// </summary>
    public bool ExtendCodeExpiration(string code, int additionalMinutes = 15)
    {
        if (string.IsNullOrEmpty(code) || (!_activeCodes.Contains(code) && !_reservedCodes.Contains(code)))
        {
            return false;
        }

        if (_codeExpirations.ContainsKey(code))
        {
            _codeExpirations[code] = _codeExpirations[code].AddMinutes(additionalMinutes);
            Debug.Log($"[RoomCodeGenerator] Extended code {code} expiration by {additionalMinutes} minutes");
            return true;
        }

        return false;
    }
    #endregion

    #region Validation
    /// <summary>
    /// 방 코드 형식 검증
    /// </summary>
    public static bool IsValidRoomCodeFormat(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (code.Length != 4) return false;
        if (!int.TryParse(code, out int numericCode)) return false;
        if (numericCode < MIN_CODE || numericCode > MAX_CODE) return false;
        
        return true;
    }

    /// <summary>
    /// 활성 방 코드인지 확인
    /// </summary>
    public bool IsActiveCode(string code)
    {
        if (!IsValidRoomCodeFormat(code)) return false;
        
        CleanupExpiredCodes();
        return _activeCodes.Contains(code);
    }

    /// <summary>
    /// 예약된 방 코드인지 확인
    /// </summary>
    public bool IsReservedCode(string code)
    {
        if (!IsValidRoomCodeFormat(code)) return false;
        
        CleanupExpiredCodes();
        return _reservedCodes.Contains(code);
    }

    /// <summary>
    /// 코드 만료 시간 조회
    /// </summary>
    public DateTime? GetCodeExpiration(string code)
    {
        if (string.IsNullOrEmpty(code) || !_codeExpirations.ContainsKey(code))
        {
            return null;
        }

        return _codeExpirations[code];
    }
    #endregion

    #region Security
    /// <summary>
    /// 브루트포스 공격 시도 기록
    /// </summary>
    public bool RecordBruteForceAttempt(string clientIp, string attemptedCode)
    {
        if (string.IsNullOrEmpty(clientIp)) return true; // IP를 알 수 없으면 허용

        // 쿨다운 중인지 확인
        if (_bruteForceCooldowns.ContainsKey(clientIp) && 
            DateTime.Now < _bruteForceCooldowns[clientIp])
        {
            Debug.LogWarning($"[RoomCodeGenerator] Brute force attempt from {clientIp} blocked (cooldown active)");
            return false;
        }

        // 시도 횟수 증가
        if (!_bruteForceAttempts.ContainsKey(clientIp))
        {
            _bruteForceAttempts[clientIp] = 0;
        }
        
        _bruteForceAttempts[clientIp]++;

        // 최대 시도 횟수 초과시 쿨다운 적용
        if (_bruteForceAttempts[clientIp] >= MAX_BRUTE_FORCE_ATTEMPTS)
        {
            _bruteForceCooldowns[clientIp] = DateTime.Now.AddSeconds(BRUTE_FORCE_COOLDOWN);
            Debug.LogWarning($"[RoomCodeGenerator] Brute force cooldown applied to {clientIp} for {BRUTE_FORCE_COOLDOWN} seconds");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 성공적인 방 참여시 브루트포스 시도 초기화
    /// </summary>
    public void ResetBruteForceAttempts(string clientIp)
    {
        if (!string.IsNullOrEmpty(clientIp))
        {
            _bruteForceAttempts.Remove(clientIp);
            Debug.Log($"[RoomCodeGenerator] Reset brute force attempts for {clientIp}");
        }
    }
    #endregion

    #region Statistics
    /// <summary>
    /// 시스템 통계 조회
    /// </summary>
    public RoomCodeStatistics GetStatistics()
    {
        CleanupExpiredCodes();
        
        return new RoomCodeStatistics
        {
            ActiveCodes = _activeCodes.Count,
            ReservedCodes = _reservedCodes.Count,
            TotalAvailable = (MAX_CODE - MIN_CODE + 1) - _activeCodes.Count - _reservedCodes.Count - RESERVED_CODES_COUNT,
            BruteForceBlocked = _bruteForceCooldowns.Count,
            SystemCapacity = MAX_CODE - MIN_CODE + 1,
            UsagePercentage = (float)(_activeCodes.Count + _reservedCodes.Count) / (MAX_CODE - MIN_CODE + 1) * 100f
        };
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 시스템 예약 코드 초기화
    /// </summary>
    private void InitializeReservedCodes()
    {
        // 시스템에서 사용하지 않을 특별한 코드들을 미리 예약
        string[] systemReservedCodes = { 
            "0000", "1111", "2222", "3333", "4444", "5555", "6666", "7777", "8888", "9999",
            "1234", "4321", "2468", "8642", "1357", "9753",
            "0001", "0010", "0100", "1000" // 테스트용 코드들
        };

        foreach (var code in systemReservedCodes)
        {
            _reservedCodes.Add(code);
        }

        Debug.Log($"[RoomCodeGenerator] Initialized {systemReservedCodes.Length} system reserved codes");
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 시스템 정리
    /// </summary>
    public void Cleanup()
    {
        _activeCodes?.Clear();
        _reservedCodes?.Clear();
        _codeExpirations?.Clear();
        _bruteForceAttempts?.Clear();
        _bruteForceCooldowns?.Clear();
        
        Debug.Log("[RoomCodeGenerator] System cleaned up");
    }
    #endregion
}

/// <summary>
/// 방 코드 생성기 통계 정보
/// </summary>
public class RoomCodeStatistics
{
    public int ActiveCodes { get; set; }
    public int ReservedCodes { get; set; }
    public int TotalAvailable { get; set; }
    public int BruteForceBlocked { get; set; }
    public int SystemCapacity { get; set; }
    public float UsagePercentage { get; set; }

    public string GetSummary()
    {
        return $"Active: {ActiveCodes}, Reserved: {ReservedCodes}, Available: {TotalAvailable}, " +
               $"Usage: {UsagePercentage:F1}%, Blocked IPs: {BruteForceBlocked}";
    }
}