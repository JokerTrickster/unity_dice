using System;
using UnityEngine;

/// <summary>
/// 자동 로그인 사용자 선호도 관리
/// PlayerPrefs를 사용하여 사용자의 자동 로그인 설정을 영구 저장합니다.
/// </summary>
public static class AutoLoginPrefs
{
    #region Constants
    private const string ENABLE_AUTO_LOGIN = "auto_login_enabled";
    private const string LAST_AUTO_LOGIN = "last_auto_login_time";
    private const string AUTO_LOGIN_ATTEMPTS = "auto_login_attempts_count";
    private const string BIOMETRIC_AUTH_ENABLED = "biometric_auth_enabled";
    private const string SHOW_SPLASH_DURING_AUTH = "show_splash_during_auth";
    private const string AUTHENTICATE_ON_APP_FOCUS = "authenticate_on_app_focus";
    private const string FIRST_LAUNCH = "is_first_launch";
    private const string USER_CONSENT_GIVEN = "user_consent_auto_login";
    private const string SETTINGS_VERSION = "auto_login_settings_version";
    
    private const int CURRENT_SETTINGS_VERSION = 1;
    #endregion

    #region Properties
    /// <summary>
    /// 자동 로그인 활성화 여부
    /// </summary>
    public static bool IsAutoLoginEnabled
    {
        get => PlayerPrefs.GetInt(ENABLE_AUTO_LOGIN, 1) == 1;
        set 
        { 
            PlayerPrefs.SetInt(ENABLE_AUTO_LOGIN, value ? 1 : 0);
            PlayerPrefs.Save();
            OnPreferencesChanged?.Invoke();
            
            if (AutoLoginSettings.Instance.EnableDebugLogging)
            {
                Debug.Log($"[AutoLoginPrefs] Auto-login {(value ? "enabled" : "disabled")}");
            }
        }
    }

    /// <summary>
    /// 마지막 자동 로그인 시도 시간
    /// </summary>
    public static DateTime LastAutoLoginTime
    {
        get 
        { 
            var binaryString = PlayerPrefs.GetString(LAST_AUTO_LOGIN, "0");
            if (long.TryParse(binaryString, out var binary) && binary != 0)
            {
                try
                {
                    return DateTime.FromBinary(binary);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AutoLoginPrefs] Failed to parse last login time: {ex.Message}");
                    return DateTime.MinValue;
                }
            }
            return DateTime.MinValue;
        }
        set 
        { 
            PlayerPrefs.SetString(LAST_AUTO_LOGIN, value.ToBinary().ToString());
            PlayerPrefs.Save();
            
            if (AutoLoginSettings.Instance.EnableDebugLogging)
            {
                Debug.Log($"[AutoLoginPrefs] Last auto-login time updated: {value:yyyy-MM-dd HH:mm:ss}");
            }
        }
    }

    /// <summary>
    /// 자동 로그인 시도 횟수 (오늘 기준)
    /// </summary>
    public static int TodayAutoLoginAttempts
    {
        get
        {
            var today = DateTime.Today;
            var lastAttemptDate = LastAutoLoginTime.Date;
            
            if (lastAttemptDate != today)
            {
                // 날짜가 바뀌었으면 시도 횟수 리셋
                return 0;
            }
            
            return PlayerPrefs.GetInt(AUTO_LOGIN_ATTEMPTS, 0);
        }
        private set
        {
            PlayerPrefs.SetInt(AUTO_LOGIN_ATTEMPTS, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 생체 인증 활성화 여부
    /// </summary>
    public static bool IsBiometricAuthEnabled
    {
        get => PlayerPrefs.GetInt(BIOMETRIC_AUTH_ENABLED, 0) == 1;
        set 
        { 
            PlayerPrefs.SetInt(BIOMETRIC_AUTH_ENABLED, value ? 1 : 0);
            PlayerPrefs.Save();
            OnPreferencesChanged?.Invoke();
            
            if (AutoLoginSettings.Instance.EnableDebugLogging)
            {
                Debug.Log($"[AutoLoginPrefs] Biometric auth {(value ? "enabled" : "disabled")}");
            }
        }
    }

    /// <summary>
    /// 인증 중 스플래시 화면 표시 여부
    /// </summary>
    public static bool ShowSplashDuringAuth
    {
        get => PlayerPrefs.GetInt(SHOW_SPLASH_DURING_AUTH, 1) == 1;
        set 
        { 
            PlayerPrefs.SetInt(SHOW_SPLASH_DURING_AUTH, value ? 1 : 0);
            PlayerPrefs.Save();
            OnPreferencesChanged?.Invoke();
        }
    }

    /// <summary>
    /// 앱 포커스 시 인증 여부
    /// </summary>
    public static bool AuthenticateOnAppFocus
    {
        get => PlayerPrefs.GetInt(AUTHENTICATE_ON_APP_FOCUS, 1) == 1;
        set 
        { 
            PlayerPrefs.SetInt(AUTHENTICATE_ON_APP_FOCUS, value ? 1 : 0);
            PlayerPrefs.Save();
            OnPreferencesChanged?.Invoke();
        }
    }

    /// <summary>
    /// 첫 실행 여부
    /// </summary>
    public static bool IsFirstLaunch
    {
        get => PlayerPrefs.GetInt(FIRST_LAUNCH, 1) == 1;
        private set 
        { 
            PlayerPrefs.SetInt(FIRST_LAUNCH, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 사용자가 자동 로그인에 동의했는지 여부
    /// </summary>
    public static bool UserConsentGiven
    {
        get => PlayerPrefs.GetInt(USER_CONSENT_GIVEN, 0) == 1;
        set 
        { 
            PlayerPrefs.SetInt(USER_CONSENT_GIVEN, value ? 1 : 0);
            PlayerPrefs.Save();
            
            if (AutoLoginSettings.Instance.EnableDebugLogging)
            {
                Debug.Log($"[AutoLoginPrefs] User consent {(value ? "given" : "revoked")}");
            }
        }
    }

    /// <summary>
    /// 설정 버전
    /// </summary>
    public static int SettingsVersion
    {
        get => PlayerPrefs.GetInt(SETTINGS_VERSION, 0);
        private set 
        { 
            PlayerPrefs.SetInt(SETTINGS_VERSION, value);
            PlayerPrefs.Save();
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// 선호도 변경 이벤트
    /// </summary>
    public static event Action OnPreferencesChanged;
    #endregion

    #region Methods
    /// <summary>
    /// 자동 로그인 시도 기록
    /// </summary>
    /// <param name="success">성공 여부</param>
    public static void RecordAutoLoginAttempt(bool success)
    {
        LastAutoLoginTime = DateTime.Now;
        
        if (!success)
        {
            TodayAutoLoginAttempts++;
        }
        else
        {
            // 성공 시 시도 횟수 리셋
            TodayAutoLoginAttempts = 0;
        }
        
        if (AutoLoginSettings.Instance.EnableDebugLogging)
        {
            Debug.Log($"[AutoLoginPrefs] Auto-login attempt recorded: {(success ? "success" : "failure")}, attempts today: {TodayAutoLoginAttempts}");
        }
    }

    /// <summary>
    /// 오늘 최대 시도 횟수에 도달했는지 확인
    /// </summary>
    /// <param name="maxAttempts">최대 시도 횟수</param>
    /// <returns>최대 시도 횟수 도달 여부</returns>
    public static bool HasReachedMaxAttemptsToday(int maxAttempts)
    {
        return TodayAutoLoginAttempts >= maxAttempts;
    }

    /// <summary>
    /// 자동 로그인 가능 여부 확인
    /// </summary>
    /// <returns>자동 로그인 가능 여부와 불가능한 이유</returns>
    public static (bool canAutoLogin, string reason) CanPerformAutoLogin()
    {
        // 사용자 동의 확인
        if (!UserConsentGiven)
        {
            return (false, "User consent not given");
        }

        // 자동 로그인 활성화 확인
        if (!IsAutoLoginEnabled)
        {
            return (false, "Auto-login is disabled");
        }

        // 최대 시도 횟수 확인
        var settings = AutoLoginSettings.Instance;
        if (HasReachedMaxAttemptsToday(settings.MaxRetryAttempts))
        {
            return (false, $"Maximum attempts reached today ({TodayAutoLoginAttempts}/{settings.MaxRetryAttempts})");
        }

        // 시간 간격 확인
        var timeSinceLastAttempt = DateTime.Now - LastAutoLoginTime;
        var minInterval = TimeSpan.FromSeconds(settings.minTimeBetweenAuthAttempts);
        
        if (timeSinceLastAttempt < minInterval && LastAutoLoginTime != DateTime.MinValue)
        {
            var remainingTime = minInterval - timeSinceLastAttempt;
            return (false, $"Too soon since last attempt, wait {remainingTime.TotalSeconds:F0}s more");
        }

        return (true, "Auto-login allowed");
    }

    /// <summary>
    /// 첫 실행 설정 완료 처리
    /// </summary>
    public static void CompleteFirstLaunchSetup()
    {
        IsFirstLaunch = false;
        SettingsVersion = CURRENT_SETTINGS_VERSION;
        
        // 기본 설정 적용
        if (!PlayerPrefs.HasKey(ENABLE_AUTO_LOGIN))
        {
            IsAutoLoginEnabled = true;
        }
        
        if (!PlayerPrefs.HasKey(SHOW_SPLASH_DURING_AUTH))
        {
            ShowSplashDuringAuth = true;
        }
        
        if (!PlayerPrefs.HasKey(AUTHENTICATE_ON_APP_FOCUS))
        {
            AuthenticateOnAppFocus = true;
        }
        
        Debug.Log("[AutoLoginPrefs] First launch setup completed");
        OnPreferencesChanged?.Invoke();
    }

    /// <summary>
    /// 설정 마이그레이션 수행
    /// </summary>
    public static void MigrateSettings()
    {
        int currentVersion = SettingsVersion;
        
        if (currentVersion < CURRENT_SETTINGS_VERSION)
        {
            Debug.Log($"[AutoLoginPrefs] Migrating settings from version {currentVersion} to {CURRENT_SETTINGS_VERSION}");
            
            // 버전별 마이그레이션 로직 추가 가능
            switch (currentVersion)
            {
                case 0:
                    // 초기 설정
                    MigrateFromVersion0();
                    break;
            }
            
            SettingsVersion = CURRENT_SETTINGS_VERSION;
            Debug.Log("[AutoLoginPrefs] Settings migration completed");
        }
    }

    /// <summary>
    /// 버전 0에서 마이그레이션
    /// </summary>
    private static void MigrateFromVersion0()
    {
        // 기존 레거시 키들을 새로운 키로 마이그레이션
        if (PlayerPrefs.HasKey("AutoLoginEnabled"))
        {
            IsAutoLoginEnabled = PlayerPrefs.GetInt("AutoLoginEnabled", 1) == 1;
            PlayerPrefs.DeleteKey("AutoLoginEnabled");
        }
        
        // 다른 레거시 설정들도 필요에 따라 마이그레이션
    }

    /// <summary>
    /// 모든 자동 로그인 설정 초기화
    /// </summary>
    public static void ResetAllPreferences()
    {
        PlayerPrefs.DeleteKey(ENABLE_AUTO_LOGIN);
        PlayerPrefs.DeleteKey(LAST_AUTO_LOGIN);
        PlayerPrefs.DeleteKey(AUTO_LOGIN_ATTEMPTS);
        PlayerPrefs.DeleteKey(BIOMETRIC_AUTH_ENABLED);
        PlayerPrefs.DeleteKey(SHOW_SPLASH_DURING_AUTH);
        PlayerPrefs.DeleteKey(AUTHENTICATE_ON_APP_FOCUS);
        PlayerPrefs.DeleteKey(USER_CONSENT_GIVEN);
        // FIRST_LAUNCH와 SETTINGS_VERSION은 유지
        
        PlayerPrefs.Save();
        
        Debug.Log("[AutoLoginPrefs] All preferences reset to defaults");
        OnPreferencesChanged?.Invoke();
    }

    /// <summary>
    /// 개발자용 설정 초기화 (모든 키 삭제)
    /// </summary>
    public static void ResetAllPreferencesIncludingSystem()
    {
        PlayerPrefs.DeleteKey(ENABLE_AUTO_LOGIN);
        PlayerPrefs.DeleteKey(LAST_AUTO_LOGIN);
        PlayerPrefs.DeleteKey(AUTO_LOGIN_ATTEMPTS);
        PlayerPrefs.DeleteKey(BIOMETRIC_AUTH_ENABLED);
        PlayerPrefs.DeleteKey(SHOW_SPLASH_DURING_AUTH);
        PlayerPrefs.DeleteKey(AUTHENTICATE_ON_APP_FOCUS);
        PlayerPrefs.DeleteKey(FIRST_LAUNCH);
        PlayerPrefs.DeleteKey(USER_CONSENT_GIVEN);
        PlayerPrefs.DeleteKey(SETTINGS_VERSION);
        
        PlayerPrefs.Save();
        
        Debug.Log("[AutoLoginPrefs] All preferences including system settings reset");
        OnPreferencesChanged?.Invoke();
    }

    /// <summary>
    /// 현재 설정 상태 정보 반환
    /// </summary>
    /// <returns>설정 상태 정보</returns>
    public static AutoLoginPrefsStatus GetPreferencesStatus()
    {
        return new AutoLoginPrefsStatus
        {
            IsAutoLoginEnabled = IsAutoLoginEnabled,
            LastAutoLoginTime = LastAutoLoginTime,
            TodayAttempts = TodayAutoLoginAttempts,
            IsBiometricEnabled = IsBiometricAuthEnabled,
            ShowSplashDuringAuth = ShowSplashDuringAuth,
            AuthenticateOnAppFocus = AuthenticateOnAppFocus,
            IsFirstLaunch = IsFirstLaunch,
            UserConsentGiven = UserConsentGiven,
            SettingsVersion = SettingsVersion,
            CanPerformAutoLogin = CanPerformAutoLogin().canAutoLogin
        };
    }

    /// <summary>
    /// 사용자 동의 요청 및 처리
    /// </summary>
    /// <returns>동의 여부</returns>
    public static bool RequestUserConsent()
    {
        if (UserConsentGiven)
        {
            return true;
        }

        // 실제 구현에서는 UI를 통해 사용자 동의를 받아야 함
        // 여기서는 기본적으로 동의로 처리 (개발 편의성)
        if (Debug.isDebugBuild)
        {
            Debug.Log("[AutoLoginPrefs] Auto-granting user consent in debug build");
            UserConsentGiven = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    public static void LogDebugInfo()
    {
        if (!AutoLoginSettings.Instance.EnableDebugLogging)
            return;

        var status = GetPreferencesStatus();
        Debug.Log($"[AutoLoginPrefs] Debug Info:\n" +
                  $"- Auto Login Enabled: {status.IsAutoLoginEnabled}\n" +
                  $"- Last Login Time: {status.LastAutoLoginTime:yyyy-MM-dd HH:mm:ss}\n" +
                  $"- Today Attempts: {status.TodayAttempts}\n" +
                  $"- Biometric Enabled: {status.IsBiometricEnabled}\n" +
                  $"- Show Splash: {status.ShowSplashDuringAuth}\n" +
                  $"- Auth on Focus: {status.AuthenticateOnAppFocus}\n" +
                  $"- First Launch: {status.IsFirstLaunch}\n" +
                  $"- User Consent: {status.UserConsentGiven}\n" +
                  $"- Settings Version: {status.SettingsVersion}\n" +
                  $"- Can Auto Login: {status.CanPerformAutoLogin}");
    }
    #endregion

    #region Static Constructor
    static AutoLoginPrefs()
    {
        // 설정 마이그레이션 수행
        MigrateSettings();
        
        // 첫 실행 처리
        if (IsFirstLaunch)
        {
            CompleteFirstLaunchSetup();
        }
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 자동 로그인 선호도 상태 정보
/// </summary>
[Serializable]
public class AutoLoginPrefsStatus
{
    public bool IsAutoLoginEnabled;
    public DateTime LastAutoLoginTime;
    public int TodayAttempts;
    public bool IsBiometricEnabled;
    public bool ShowSplashDuringAuth;
    public bool AuthenticateOnAppFocus;
    public bool IsFirstLaunch;
    public bool UserConsentGiven;
    public int SettingsVersion;
    public bool CanPerformAutoLogin;
}
#endregion