using System;
using UnityEngine;

/// <summary>
/// 자동 로그인 설정 관리 클래스
/// 자동 로그인 관련 모든 설정과 사용자 선호도를 관리합니다.
/// </summary>
[Serializable]
[CreateAssetMenu(fileName = "AutoLoginSettings", menuName = "Authentication/Auto Login Settings")]
public class AutoLoginSettings : ScriptableObject
{
    #region Auto-Login Configuration
    [Header("Auto-Login Configuration")]
    [SerializeField] private bool enableAutoLogin = true;
    [SerializeField] private bool requireBiometricAuth = false;
    [SerializeField] private int tokenRefreshThresholdHours = 1;
    [SerializeField] private int maxRetryAttempts = 2;
    [SerializeField] private float autoLoginTimeoutSeconds = 10f;
    
    [Header("Token Management")]
    [SerializeField] private float tokenValidityThresholdSeconds = 3600f; // 1시간
    [SerializeField] private bool enableTokenAutoRefresh = true;
    [SerializeField] private int maxRefreshAttempts = 3;
    [SerializeField] private float refreshRetryDelaySeconds = 2f;
    
    [Header("Background Behavior")]
    [SerializeField] private bool authenticateOnAppFocus = true;
    [SerializeField] private bool showSplashDuringAuth = true;
    [SerializeField] private float maxAuthenticationTime = 10f;
    [SerializeField] private float appFocusDelaySeconds = 0.5f;
    [SerializeField] private float minTimeBetweenAuthAttempts = 60f; // 1분
    
    [Header("Security Settings")]
    [SerializeField] private bool enableSecureStorage = true;
    [SerializeField] private bool rotateEncryptionKeys = false;
    [SerializeField] private int keyRotationIntervalDays = 30;
    [SerializeField] private bool clearTokensOnLogout = true;
    
    [Header("Debug & Development")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool skipServerValidationInDebug = true;
    [SerializeField] private bool simulateSlowNetwork = false;
    [SerializeField] private float networkSimulationDelay = 2f;
    #endregion

    #region Properties
    /// <summary>
    /// 자동 로그인 활성화 여부
    /// </summary>
    public bool EnableAutoLogin 
    { 
        get => enableAutoLogin && AutoLoginPrefs.IsAutoLoginEnabled; 
        set 
        { 
            enableAutoLogin = value;
            AutoLoginPrefs.IsAutoLoginEnabled = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 생체 인증 필요 여부
    /// </summary>
    public bool RequireBiometricAuth 
    { 
        get => requireBiometricAuth; 
        set 
        { 
            requireBiometricAuth = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 토큰 갱신 임계 시간 (시간)
    /// </summary>
    public int TokenRefreshThresholdHours 
    { 
        get => tokenRefreshThresholdHours; 
        set 
        { 
            tokenRefreshThresholdHours = Mathf.Clamp(value, 1, 24);
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 최대 재시도 횟수
    /// </summary>
    public int MaxRetryAttempts 
    { 
        get => maxRetryAttempts; 
        set 
        { 
            maxRetryAttempts = Mathf.Clamp(value, 1, 5);
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 자동 로그인 타임아웃 (초)
    /// </summary>
    public float AutoLoginTimeoutSeconds 
    { 
        get => autoLoginTimeoutSeconds; 
        set 
        { 
            autoLoginTimeoutSeconds = Mathf.Clamp(value, 5f, 60f);
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 토큰 유효성 임계값 (초)
    /// </summary>
    public float TokenValidityThresholdSeconds 
    { 
        get => tokenValidityThresholdSeconds; 
        set 
        { 
            tokenValidityThresholdSeconds = Mathf.Clamp(value, 300f, 7200f); // 5분~2시간
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 토큰 자동 갱신 활성화 여부
    /// </summary>
    public bool EnableTokenAutoRefresh 
    { 
        get => enableTokenAutoRefresh; 
        set 
        { 
            enableTokenAutoRefresh = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 앱 포커스 시 인증 여부
    /// </summary>
    public bool AuthenticateOnAppFocus 
    { 
        get => authenticateOnAppFocus; 
        set 
        { 
            authenticateOnAppFocus = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 인증 중 스플래시 표시 여부
    /// </summary>
    public bool ShowSplashDuringAuth 
    { 
        get => showSplashDuringAuth; 
        set 
        { 
            showSplashDuringAuth = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 보안 스토리지 활성화 여부
    /// </summary>
    public bool EnableSecureStorage 
    { 
        get => enableSecureStorage; 
        set 
        { 
            enableSecureStorage = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    
    /// <summary>
    /// 디버그 로깅 활성화 여부
    /// </summary>
    public bool EnableDebugLogging 
    { 
        get => enableDebugLogging && Debug.isDebugBuild; 
        set 
        { 
            enableDebugLogging = value;
            OnSettingsChanged?.Invoke();
        } 
    }
    #endregion

    #region Events
    /// <summary>
    /// 설정 변경 이벤트
    /// </summary>
    public static event Action OnSettingsChanged;
    #endregion

    #region Singleton Access
    private static AutoLoginSettings _instance;
    
    /// <summary>
    /// 설정 인스턴스 (런타임에서 리소스 로드)
    /// </summary>
    public static AutoLoginSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AutoLoginSettings>("AutoLoginSettings");
                if (_instance == null)
                {
                    Debug.LogWarning("[AutoLoginSettings] Settings asset not found, creating default instance");
                    _instance = CreateInstance<AutoLoginSettings>();
                    _instance.InitializeDefaults();
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 기본값으로 초기화
    /// </summary>
    public void InitializeDefaults()
    {
        enableAutoLogin = true;
        requireBiometricAuth = false;
        tokenRefreshThresholdHours = 1;
        maxRetryAttempts = 2;
        autoLoginTimeoutSeconds = 10f;
        tokenValidityThresholdSeconds = 3600f;
        enableTokenAutoRefresh = true;
        maxRefreshAttempts = 3;
        refreshRetryDelaySeconds = 2f;
        authenticateOnAppFocus = true;
        showSplashDuringAuth = true;
        maxAuthenticationTime = 10f;
        appFocusDelaySeconds = 0.5f;
        minTimeBetweenAuthAttempts = 60f;
        enableSecureStorage = true;
        rotateEncryptionKeys = false;
        keyRotationIntervalDays = 30;
        clearTokensOnLogout = true;
        enableDebugLogging = true;
        skipServerValidationInDebug = true;
        simulateSlowNetwork = false;
        networkSimulationDelay = 2f;
        
        Debug.Log("[AutoLoginSettings] Initialized with default values");
    }

    /// <summary>
    /// 설정 유효성 검사 및 수정
    /// </summary>
    public void ValidateAndFixSettings()
    {
        // 값 범위 검증 및 수정
        tokenRefreshThresholdHours = Mathf.Clamp(tokenRefreshThresholdHours, 1, 24);
        maxRetryAttempts = Mathf.Clamp(maxRetryAttempts, 1, 5);
        autoLoginTimeoutSeconds = Mathf.Clamp(autoLoginTimeoutSeconds, 5f, 60f);
        tokenValidityThresholdSeconds = Mathf.Clamp(tokenValidityThresholdSeconds, 300f, 7200f);
        maxRefreshAttempts = Mathf.Clamp(maxRefreshAttempts, 1, 5);
        refreshRetryDelaySeconds = Mathf.Clamp(refreshRetryDelaySeconds, 1f, 10f);
        maxAuthenticationTime = Mathf.Clamp(maxAuthenticationTime, 5f, 60f);
        appFocusDelaySeconds = Mathf.Clamp(appFocusDelaySeconds, 0.1f, 5f);
        minTimeBetweenAuthAttempts = Mathf.Clamp(minTimeBetweenAuthAttempts, 30f, 300f);
        keyRotationIntervalDays = Mathf.Clamp(keyRotationIntervalDays, 7, 90);
        networkSimulationDelay = Mathf.Clamp(networkSimulationDelay, 0.5f, 10f);
        
        Debug.Log("[AutoLoginSettings] Settings validated and corrected");
    }
    #endregion

    #region Configuration Methods
    /// <summary>
    /// 프로덕션 환경용 설정 적용
    /// </summary>
    public void ApplyProductionSettings()
    {
        enableAutoLogin = true;
        requireBiometricAuth = false;
        tokenRefreshThresholdHours = 1;
        maxRetryAttempts = 2;
        enableTokenAutoRefresh = true;
        authenticateOnAppFocus = true;
        enableSecureStorage = true;
        rotateEncryptionKeys = true;
        clearTokensOnLogout = true;
        enableDebugLogging = false;
        skipServerValidationInDebug = false;
        simulateSlowNetwork = false;
        
        Debug.Log("[AutoLoginSettings] Production settings applied");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>
    /// 개발 환경용 설정 적용
    /// </summary>
    public void ApplyDevelopmentSettings()
    {
        enableAutoLogin = true;
        requireBiometricAuth = false;
        tokenRefreshThresholdHours = 1;
        maxRetryAttempts = 3;
        autoLoginTimeoutSeconds = 15f;
        enableTokenAutoRefresh = true;
        authenticateOnAppFocus = true;
        enableSecureStorage = true;
        rotateEncryptionKeys = false;
        clearTokensOnLogout = false; // 개발 편의성을 위해
        enableDebugLogging = true;
        skipServerValidationInDebug = true;
        simulateSlowNetwork = false;
        
        Debug.Log("[AutoLoginSettings] Development settings applied");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>
    /// 보안 강화 설정 적용
    /// </summary>
    public void ApplyHighSecuritySettings()
    {
        enableAutoLogin = true;
        requireBiometricAuth = true;
        tokenRefreshThresholdHours = 1;
        maxRetryAttempts = 1;
        autoLoginTimeoutSeconds = 8f;
        enableTokenAutoRefresh = true;
        maxRefreshAttempts = 2;
        authenticateOnAppFocus = true;
        minTimeBetweenAuthAttempts = 120f; // 2분
        enableSecureStorage = true;
        rotateEncryptionKeys = true;
        keyRotationIntervalDays = 7;
        clearTokensOnLogout = true;
        skipServerValidationInDebug = false;
        
        Debug.Log("[AutoLoginSettings] High security settings applied");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>
    /// 성능 우선 설정 적용
    /// </summary>
    public void ApplyPerformanceOptimizedSettings()
    {
        enableAutoLogin = true;
        requireBiometricAuth = false;
        tokenRefreshThresholdHours = 2;
        maxRetryAttempts = 1;
        autoLoginTimeoutSeconds = 5f;
        enableTokenAutoRefresh = true;
        maxRefreshAttempts = 2;
        refreshRetryDelaySeconds = 1f;
        authenticateOnAppFocus = false; // 성능을 위해 비활성화
        maxAuthenticationTime = 5f;
        showSplashDuringAuth = false;
        minTimeBetweenAuthAttempts = 300f; // 5분
        rotateEncryptionKeys = false;
        enableDebugLogging = false;
        
        Debug.Log("[AutoLoginSettings] Performance optimized settings applied");
        OnSettingsChanged?.Invoke();
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 설정을 JSON으로 내보내기
    /// </summary>
    /// <returns>JSON 문자열</returns>
    public string ExportToJson()
    {
        try
        {
            var settingsData = new AutoLoginSettingsData(this);
            return JsonUtility.ToJson(settingsData, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginSettings] Failed to export to JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON에서 설정 가져오기
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>가져오기 성공 여부</returns>
    public bool ImportFromJson(string json)
    {
        try
        {
            var settingsData = JsonUtility.FromJson<AutoLoginSettingsData>(json);
            ApplySettingsData(settingsData);
            ValidateAndFixSettings();
            OnSettingsChanged?.Invoke();
            
            Debug.Log("[AutoLoginSettings] Settings imported from JSON successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoLoginSettings] Failed to import from JSON: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 설정 데이터 적용
    /// </summary>
    /// <param name="data">설정 데이터</param>
    private void ApplySettingsData(AutoLoginSettingsData data)
    {
        enableAutoLogin = data.enableAutoLogin;
        requireBiometricAuth = data.requireBiometricAuth;
        tokenRefreshThresholdHours = data.tokenRefreshThresholdHours;
        maxRetryAttempts = data.maxRetryAttempts;
        autoLoginTimeoutSeconds = data.autoLoginTimeoutSeconds;
        tokenValidityThresholdSeconds = data.tokenValidityThresholdSeconds;
        enableTokenAutoRefresh = data.enableTokenAutoRefresh;
        maxRefreshAttempts = data.maxRefreshAttempts;
        refreshRetryDelaySeconds = data.refreshRetryDelaySeconds;
        authenticateOnAppFocus = data.authenticateOnAppFocus;
        showSplashDuringAuth = data.showSplashDuringAuth;
        maxAuthenticationTime = data.maxAuthenticationTime;
        appFocusDelaySeconds = data.appFocusDelaySeconds;
        minTimeBetweenAuthAttempts = data.minTimeBetweenAuthAttempts;
        enableSecureStorage = data.enableSecureStorage;
        rotateEncryptionKeys = data.rotateEncryptionKeys;
        keyRotationIntervalDays = data.keyRotationIntervalDays;
        clearTokensOnLogout = data.clearTokensOnLogout;
        enableDebugLogging = data.enableDebugLogging;
        skipServerValidationInDebug = data.skipServerValidationInDebug;
        simulateSlowNetwork = data.simulateSlowNetwork;
        networkSimulationDelay = data.networkSimulationDelay;
    }

    /// <summary>
    /// 현재 설정 요약 정보 반환
    /// </summary>
    /// <returns>설정 요약</returns>
    public AutoLoginSettingsSummary GetSettingsSummary()
    {
        return new AutoLoginSettingsSummary
        {
            IsAutoLoginEnabled = EnableAutoLogin,
            RequiresBiometric = RequireBiometricAuth,
            TokenRefreshThreshold = TokenRefreshThresholdHours,
            MaxRetries = MaxRetryAttempts,
            AuthenticationTimeout = AutoLoginTimeoutSeconds,
            AuthenticatesOnFocus = AuthenticateOnAppFocus,
            UseSecureStorage = EnableSecureStorage,
            DebugModeEnabled = EnableDebugLogging
        };
    }

    /// <summary>
    /// 설정 리셋
    /// </summary>
    public void ResetToDefaults()
    {
        InitializeDefaults();
        OnSettingsChanged?.Invoke();
        Debug.Log("[AutoLoginSettings] Settings reset to defaults");
    }
    #endregion

    #region Editor Support
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Authentication/Create Auto Login Settings")]
    private static void CreateAutoLoginSettingsAsset()
    {
        var settings = CreateInstance<AutoLoginSettings>();
        settings.InitializeDefaults();
        
        string assetPath = "Assets/Resources/AutoLoginSettings.asset";
        UnityEditor.AssetDatabase.CreateAsset(settings, assetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        
        UnityEditor.Selection.activeObject = settings;
        Debug.Log($"[AutoLoginSettings] Settings asset created at {assetPath}");
    }

    private void OnValidate()
    {
        ValidateAndFixSettings();
    }
#endif
    #endregion
}

#region Data Classes
/// <summary>
/// 설정 데이터 직렬화용 클래스
/// </summary>
[Serializable]
public class AutoLoginSettingsData
{
    public bool enableAutoLogin;
    public bool requireBiometricAuth;
    public int tokenRefreshThresholdHours;
    public int maxRetryAttempts;
    public float autoLoginTimeoutSeconds;
    public float tokenValidityThresholdSeconds;
    public bool enableTokenAutoRefresh;
    public int maxRefreshAttempts;
    public float refreshRetryDelaySeconds;
    public bool authenticateOnAppFocus;
    public bool showSplashDuringAuth;
    public float maxAuthenticationTime;
    public float appFocusDelaySeconds;
    public float minTimeBetweenAuthAttempts;
    public bool enableSecureStorage;
    public bool rotateEncryptionKeys;
    public int keyRotationIntervalDays;
    public bool clearTokensOnLogout;
    public bool enableDebugLogging;
    public bool skipServerValidationInDebug;
    public bool simulateSlowNetwork;
    public float networkSimulationDelay;

    public AutoLoginSettingsData() { }

    public AutoLoginSettingsData(AutoLoginSettings settings)
    {
        enableAutoLogin = settings.EnableAutoLogin;
        requireBiometricAuth = settings.RequireBiometricAuth;
        tokenRefreshThresholdHours = settings.TokenRefreshThresholdHours;
        maxRetryAttempts = settings.MaxRetryAttempts;
        autoLoginTimeoutSeconds = settings.AutoLoginTimeoutSeconds;
        tokenValidityThresholdSeconds = settings.TokenValidityThresholdSeconds;
        enableTokenAutoRefresh = settings.EnableTokenAutoRefresh;
        maxRefreshAttempts = settings.maxRefreshAttempts;
        refreshRetryDelaySeconds = settings.refreshRetryDelaySeconds;
        authenticateOnAppFocus = settings.AuthenticateOnAppFocus;
        showSplashDuringAuth = settings.ShowSplashDuringAuth;
        maxAuthenticationTime = settings.maxAuthenticationTime;
        appFocusDelaySeconds = settings.appFocusDelaySeconds;
        minTimeBetweenAuthAttempts = settings.minTimeBetweenAuthAttempts;
        enableSecureStorage = settings.EnableSecureStorage;
        rotateEncryptionKeys = settings.rotateEncryptionKeys;
        keyRotationIntervalDays = settings.keyRotationIntervalDays;
        clearTokensOnLogout = settings.clearTokensOnLogout;
        enableDebugLogging = settings.EnableDebugLogging;
        skipServerValidationInDebug = settings.skipServerValidationInDebug;
        simulateSlowNetwork = settings.simulateSlowNetwork;
        networkSimulationDelay = settings.networkSimulationDelay;
    }
}

/// <summary>
/// 설정 요약 정보
/// </summary>
[Serializable]
public class AutoLoginSettingsSummary
{
    public bool IsAutoLoginEnabled;
    public bool RequiresBiometric;
    public int TokenRefreshThreshold;
    public int MaxRetries;
    public float AuthenticationTimeout;
    public bool AuthenticatesOnFocus;
    public bool UseSecureStorage;
    public bool DebugModeEnabled;
}
#endregion