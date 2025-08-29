using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자동 로그인 관련 UI 설정을 관리하는 매니저 클래스
/// AutoLoginSettings와 AutoLoginPrefs를 통합하여 일관된 사용자 설정 경험을 제공합니다.
/// Stream A/B 컴포넌트들과 연동하여 실시간 설정 변경을 지원합니다.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    #region Singleton
    private static SettingsManager _instance;
    public static SettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SettingsManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SettingsManager");
                    _instance = go.AddComponent<SettingsManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    #endregion

    #region UI References
    [Header("Auto-Login Settings UI")]
    [SerializeField] private Toggle autoLoginToggle;
    [SerializeField] private Toggle biometricAuthToggle;
    [SerializeField] private Toggle showSplashToggle;
    [SerializeField] private Toggle authOnFocusToggle;
    [SerializeField] private Slider maxRetrySlider;
    [SerializeField] private Slider timeoutSlider;
    
    [Header("Token Management UI")]
    [SerializeField] private Toggle enableTokenRefreshToggle;
    [SerializeField] private Slider tokenValiditySlider;
    [SerializeField] private Slider refreshThresholdSlider;
    
    [Header("Security Settings UI")]
    [SerializeField] private Toggle enableSecureStorageToggle;
    [SerializeField] private Toggle deviceBindingToggle;
    [SerializeField] private Button clearCredentialsButton;
    [SerializeField] private Button resetSettingsButton;
    
    [Header("Fallback Settings UI")]
    [SerializeField] private Toggle enableFallbackToggle;
    [SerializeField] private Toggle enableRetryToggle;
    [SerializeField] private Toggle enableOfflineModeToggle;
    [SerializeField] private Toggle enableGuestModeToggle;
    
    [Header("Display UI")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text tokenStatusText;
    [SerializeField] private Text lastLoginText;
    [SerializeField] private Text attemptsCountText;
    [SerializeField] private Button exportSettingsButton;
    [SerializeField] private Button importSettingsButton;
    
    [Header("Debug UI")]
    [SerializeField] private Toggle enableDebugLoggingToggle;
    [SerializeField] private Button diagnosticsButton;
    [SerializeField] private Button testAutoLoginButton;
    [SerializeField] private Text debugInfoText;
    #endregion

    #region Configuration
    [Header("UI Configuration")]
    [SerializeField] private bool enableRealTimeUpdates = true;
    [SerializeField] private float statusUpdateInterval = 2f;
    [SerializeField] private bool enableAdvancedSettings = false;
    [SerializeField] private bool enableDeveloperMode = false;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color warningColor = Color.yellow;
    #endregion

    #region Events
    /// <summary>
    /// 설정이 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<SettingChangeEventArgs> OnSettingChanged;
    
    /// <summary>
    /// 설정 UI가 업데이트될 때 발생하는 이벤트
    /// </summary>
    public static event Action OnSettingsUIUpdated;
    
    /// <summary>
    /// 설정 내보내기/가져오기 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action<bool, string> OnSettingsImportExport;
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isUpdatingUI = false;
    private Coroutine _statusUpdateCoroutine;
    private AutoLoginSettings _settings;
    private Dictionary<string, object> _pendingChanges;
    private bool _hasUnsavedChanges = false;
    #endregion

    #region Properties
    /// <summary>
    /// 설정 매니저 초기화 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 실시간 업데이트 활성화 여부
    /// </summary>
    public bool EnableRealTimeUpdates
    {
        get => enableRealTimeUpdates;
        set
        {
            enableRealTimeUpdates = value;
            if (value && _isInitialized)
            {
                StartStatusUpdates();
            }
            else
            {
                StopStatusUpdates();
            }
        }
    }

    /// <summary>
    /// 고급 설정 표시 여부
    /// </summary>
    public bool EnableAdvancedSettings
    {
        get => enableAdvancedSettings;
        set
        {
            enableAdvancedSettings = value;
            UpdateAdvancedSettingsVisibility();
        }
    }

    /// <summary>
    /// 저장되지 않은 변경사항 존재 여부
    /// </summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeSettingsManager();
    }

    private void OnDestroy()
    {
        StopStatusUpdates();
        UnsubscribeFromEvents();
        
        // 이벤트 구독 해제
        OnSettingChanged = null;
        OnSettingsUIUpdated = null;
        OnSettingsImportExport = null;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _isInitialized)
        {
            // 앱이 다시 활성화될 때 설정 상태 업데이트
            StartCoroutine(DelayedUpdateUI());
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 설정 매니저 초기화
    /// </summary>
    private void InitializeSettingsManager()
    {
        try
        {
            Debug.Log("[SettingsManager] Initializing...");

            // 설정 인스턴스 가져오기
            _settings = AutoLoginSettings.Instance;
            if (_settings == null)
            {
                Debug.LogError("[SettingsManager] Failed to get AutoLoginSettings instance");
                return;
            }

            // 변경사항 추적 딕셔너리 초기화
            _pendingChanges = new Dictionary<string, object>();

            // UI 컴포넌트 설정
            SetupUIComponents();

            // 이벤트 구독
            SubscribeToEvents();

            // UI 초기 업데이트
            UpdateUIFromSettings();

            // 상태 업데이트 시작
            if (enableRealTimeUpdates)
            {
                StartStatusUpdates();
            }

            _isInitialized = true;
            Debug.Log("[SettingsManager] Initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsManager] Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// UI 컴포넌트 설정
    /// </summary>
    private void SetupUIComponents()
    {
        // 토글 이벤트 연결
        if (autoLoginToggle != null)
            autoLoginToggle.onValueChanged.AddListener(OnAutoLoginToggleChanged);
        
        if (biometricAuthToggle != null)
            biometricAuthToggle.onValueChanged.AddListener(OnBiometricAuthToggleChanged);
        
        if (showSplashToggle != null)
            showSplashToggle.onValueChanged.AddListener(OnShowSplashToggleChanged);
        
        if (authOnFocusToggle != null)
            authOnFocusToggle.onValueChanged.AddListener(OnAuthOnFocusToggleChanged);
        
        if (enableTokenRefreshToggle != null)
            enableTokenRefreshToggle.onValueChanged.AddListener(OnTokenRefreshToggleChanged);
        
        if (enableSecureStorageToggle != null)
            enableSecureStorageToggle.onValueChanged.AddListener(OnSecureStorageToggleChanged);
        
        if (enableFallbackToggle != null)
            enableFallbackToggle.onValueChanged.AddListener(OnFallbackToggleChanged);
        
        if (enableRetryToggle != null)
            enableRetryToggle.onValueChanged.AddListener(OnRetryToggleChanged);
        
        if (enableOfflineModeToggle != null)
            enableOfflineModeToggle.onValueChanged.AddListener(OnOfflineModeToggleChanged);
        
        if (enableDebugLoggingToggle != null)
            enableDebugLoggingToggle.onValueChanged.AddListener(OnDebugLoggingToggleChanged);

        // 슬라이더 이벤트 연결
        if (maxRetrySlider != null)
            maxRetrySlider.onValueChanged.AddListener(OnMaxRetrySliderChanged);
        
        if (timeoutSlider != null)
            timeoutSlider.onValueChanged.AddListener(OnTimeoutSliderChanged);
        
        if (tokenValiditySlider != null)
            tokenValiditySlider.onValueChanged.AddListener(OnTokenValiditySliderChanged);
        
        if (refreshThresholdSlider != null)
            refreshThresholdSlider.onValueChanged.AddListener(OnRefreshThresholdSliderChanged);

        // 버튼 이벤트 연결
        if (clearCredentialsButton != null)
            clearCredentialsButton.onClick.AddListener(OnClearCredentialsClicked);
        
        if (resetSettingsButton != null)
            resetSettingsButton.onClick.AddListener(OnResetSettingsClicked);
        
        if (exportSettingsButton != null)
            exportSettingsButton.onClick.AddListener(OnExportSettingsClicked);
        
        if (importSettingsButton != null)
            importSettingsButton.onClick.AddListener(OnImportSettingsClicked);
        
        if (diagnosticsButton != null)
            diagnosticsButton.onClick.AddListener(OnDiagnosticsClicked);
        
        if (testAutoLoginButton != null)
            testAutoLoginButton.onClick.AddListener(OnTestAutoLoginClicked);
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // AutoLoginSettings 이벤트
        AutoLoginSettings.OnSettingsChanged += OnSettingsChanged;
        
        // AutoLoginPrefs 이벤트
        AutoLoginPrefs.OnPreferencesChanged += OnPreferencesChanged;
        
        // AutoLoginManager 이벤트
        if (AutoLoginManager.Instance != null)
        {
            AutoLoginManager.OnAutoLoginStarted += OnAutoLoginStarted;
            AutoLoginManager.OnAutoLoginCompleted += OnAutoLoginCompleted;
            AutoLoginManager.OnAutoLoginProgress += OnAutoLoginProgress;
        }
        
        // TokenManager 이벤트
        if (TokenManager.Instance != null)
        {
            TokenManager.OnTokenRefreshed += OnTokenRefreshed;
            TokenManager.OnTokenRefreshFailed += OnTokenRefreshFailed;
            TokenManager.OnTokenExpired += OnTokenExpired;
        }
        
        // FallbackHandler 이벤트
        if (FallbackHandler.Instance != null)
        {
            FallbackHandler.OnFallbackStarted += OnFallbackStarted;
            FallbackHandler.OnFallbackCompleted += OnFallbackCompleted;
        }
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        AutoLoginSettings.OnSettingsChanged -= OnSettingsChanged;
        AutoLoginPrefs.OnPreferencesChanged -= OnPreferencesChanged;
        
        if (AutoLoginManager.Instance != null)
        {
            AutoLoginManager.OnAutoLoginStarted -= OnAutoLoginStarted;
            AutoLoginManager.OnAutoLoginCompleted -= OnAutoLoginCompleted;
            AutoLoginManager.OnAutoLoginProgress -= OnAutoLoginProgress;
        }
        
        if (TokenManager.Instance != null)
        {
            TokenManager.OnTokenRefreshed -= OnTokenRefreshed;
            TokenManager.OnTokenRefreshFailed -= OnTokenRefreshFailed;
            TokenManager.OnTokenExpired -= OnTokenExpired;
        }
        
        if (FallbackHandler.Instance != null)
        {
            FallbackHandler.OnFallbackStarted -= OnFallbackStarted;
            FallbackHandler.OnFallbackCompleted -= OnFallbackCompleted;
        }
    }
    #endregion

    #region UI Updates
    /// <summary>
    /// 설정에서 UI 업데이트
    /// </summary>
    private void UpdateUIFromSettings()
    {
        if (_isUpdatingUI || _settings == null) return;

        _isUpdatingUI = true;
        
        try
        {
            // 자동 로그인 설정
            if (autoLoginToggle != null)
                autoLoginToggle.isOn = _settings.EnableAutoLogin;
            
            if (biometricAuthToggle != null)
                biometricAuthToggle.isOn = _settings.RequireBiometricAuth;
            
            if (showSplashToggle != null)
                showSplashToggle.isOn = _settings.ShowSplashDuringAuth;
            
            if (authOnFocusToggle != null)
                authOnFocusToggle.isOn = _settings.AuthenticateOnAppFocus;

            // 토큰 관리 설정
            if (enableTokenRefreshToggle != null)
                enableTokenRefreshToggle.isOn = _settings.EnableTokenAutoRefresh;
            
            if (enableSecureStorageToggle != null)
                enableSecureStorageToggle.isOn = _settings.EnableSecureStorage;

            // 슬라이더 값 설정
            if (maxRetrySlider != null)
                maxRetrySlider.value = _settings.MaxRetryAttempts;
            
            if (timeoutSlider != null)
                timeoutSlider.value = _settings.AutoLoginTimeoutSeconds;
            
            if (tokenValiditySlider != null)
                tokenValiditySlider.value = _settings.TokenValidityThresholdSeconds / 3600f; // 시간 단위로 표시
            
            if (refreshThresholdSlider != null)
                refreshThresholdSlider.value = _settings.TokenRefreshThresholdHours;

            // 폴백 설정
            UpdateFallbackSettings();

            // 디버그 설정
            if (enableDebugLoggingToggle != null)
                enableDebugLoggingToggle.isOn = _settings.EnableDebugLogging;

            // 상태 텍스트 업데이트
            UpdateStatusTexts();
            
            OnSettingsUIUpdated?.Invoke();
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    /// <summary>
    /// 폴백 설정 업데이트
    /// </summary>
    private void UpdateFallbackSettings()
    {
        if (FallbackHandler.Instance != null)
        {
            var fallbackStatus = FallbackHandler.Instance.GetStatus();
            
            if (enableFallbackToggle != null)
                enableFallbackToggle.isOn = fallbackStatus.EnableFallbackMechanisms;
            
            if (enableRetryToggle != null)
                enableRetryToggle.isOn = fallbackStatus.EnableRetryMechanisms;
        }
    }

    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    private void UpdateStatusTexts()
    {
        // 전체 상태
        if (statusText != null)
        {
            var autoLoginStatus = AutoLoginManager.Instance?.GetStatus();
            if (autoLoginStatus != null)
            {
                var statusColor = autoLoginStatus.CanAttempt ? successColor : warningColor;
                statusText.text = GetStatusMessage(autoLoginStatus);
                statusText.color = statusColor;
            }
        }

        // 토큰 상태
        if (tokenStatusText != null)
        {
            var tokenStatus = TokenManager.Instance?.GetStatus();
            if (tokenStatus != null)
            {
                var tokenColor = tokenStatus.HasValidToken ? successColor : errorColor;
                tokenStatusText.text = GetTokenStatusMessage(tokenStatus);
                tokenStatusText.color = tokenColor;
            }
        }

        // 마지막 로그인 시간
        if (lastLoginText != null)
        {
            var lastLoginTime = AutoLoginPrefs.LastAutoLoginTime;
            if (lastLoginTime != DateTime.MinValue)
            {
                lastLoginText.text = $"마지막 로그인: {lastLoginTime:yyyy-MM-dd HH:mm}";
            }
            else
            {
                lastLoginText.text = "마지막 로그인: 없음";
            }
        }

        // 시도 횟수
        if (attemptsCountText != null)
        {
            var attempts = AutoLoginPrefs.TodayAutoLoginAttempts;
            var maxAttempts = _settings.MaxRetryAttempts;
            attemptsCountText.text = $"오늘 시도 횟수: {attempts}/{maxAttempts}";
            attemptsCountText.color = attempts >= maxAttempts ? errorColor : Color.white;
        }

        // 디버그 정보
        UpdateDebugInfo();
    }

    /// <summary>
    /// 디버그 정보 업데이트
    /// </summary>
    private void UpdateDebugInfo()
    {
        if (debugInfoText != null && enableDeveloperMode)
        {
            var debugInfo = GetDebugInformation();
            debugInfoText.text = debugInfo;
        }
    }

    /// <summary>
    /// 지연된 UI 업데이트
    /// </summary>
    private IEnumerator DelayedUpdateUI()
    {
        yield return new WaitForSeconds(0.5f);
        UpdateUIFromSettings();
    }

    /// <summary>
    /// 고급 설정 표시/숨김
    /// </summary>
    private void UpdateAdvancedSettingsVisibility()
    {
        // 고급 설정 UI 요소들의 표시/숨김 처리
        var advancedElements = new GameObject[]
        {
            tokenValiditySlider?.gameObject,
            refreshThresholdSlider?.gameObject,
            deviceBindingToggle?.gameObject,
            enableDebugLoggingToggle?.gameObject
        };

        foreach (var element in advancedElements)
        {
            if (element != null)
            {
                element.SetActive(enableAdvancedSettings);
            }
        }
    }
    #endregion

    #region Event Handlers - UI Events

    private void OnAutoLoginToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.EnableAutoLogin = value;
        AutoLoginPrefs.IsAutoLoginEnabled = value;
        NotifySettingChanged("EnableAutoLogin", value);
        
        Debug.Log($"[SettingsManager] Auto-login {(value ? "enabled" : "disabled")}");
    }

    private void OnBiometricAuthToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.RequireBiometricAuth = value;
        AutoLoginPrefs.IsBiometricAuthEnabled = value;
        NotifySettingChanged("RequireBiometricAuth", value);
    }

    private void OnShowSplashToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.ShowSplashDuringAuth = value;
        AutoLoginPrefs.ShowSplashDuringAuth = value;
        NotifySettingChanged("ShowSplashDuringAuth", value);
    }

    private void OnAuthOnFocusToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.AuthenticateOnAppFocus = value;
        AutoLoginPrefs.AuthenticateOnAppFocus = value;
        NotifySettingChanged("AuthenticateOnAppFocus", value);
    }

    private void OnTokenRefreshToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.EnableTokenAutoRefresh = value;
        if (TokenManager.Instance != null)
        {
            TokenManager.Instance.AutoRefreshEnabled = value;
        }
        NotifySettingChanged("EnableTokenAutoRefresh", value);
    }

    private void OnSecureStorageToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.EnableSecureStorage = value;
        NotifySettingChanged("EnableSecureStorage", value);
    }

    private void OnFallbackToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        if (FallbackHandler.Instance != null)
        {
            FallbackHandler.Instance.EnableFallbackMechanisms = value;
        }
        NotifySettingChanged("EnableFallbackMechanisms", value);
    }

    private void OnRetryToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        var currentSettings = FallbackHandler.Instance?.GetStatus();
        if (currentSettings != null)
        {
            FallbackHandler.Instance.UpdateSettings(
                currentSettings.EnableFallbackMechanisms,
                value,
                currentSettings.MaxRetryAttempts,
                2f // 기본 지연 시간
            );
        }
        NotifySettingChanged("EnableRetryMechanisms", value);
    }

    private void OnOfflineModeToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        // 오프라인 모드 설정 처리
        NotifySettingChanged("EnableOfflineMode", value);
    }

    private void OnDebugLoggingToggleChanged(bool value)
    {
        if (_isUpdatingUI) return;
        
        _settings.EnableDebugLogging = value;
        NotifySettingChanged("EnableDebugLogging", value);
    }

    private void OnMaxRetrySliderChanged(float value)
    {
        if (_isUpdatingUI) return;
        
        int intValue = Mathf.RoundToInt(value);
        _settings.MaxRetryAttempts = intValue;
        
        // FallbackHandler 설정도 업데이트
        var fallbackStatus = FallbackHandler.Instance?.GetStatus();
        if (fallbackStatus != null)
        {
            FallbackHandler.Instance.UpdateSettings(
                fallbackStatus.EnableFallbackMechanisms,
                fallbackStatus.EnableRetryMechanisms,
                intValue,
                2f
            );
        }
        
        NotifySettingChanged("MaxRetryAttempts", intValue);
    }

    private void OnTimeoutSliderChanged(float value)
    {
        if (_isUpdatingUI) return;
        
        _settings.AutoLoginTimeoutSeconds = value;
        
        // AutoLoginManager 설정 업데이트
        if (AutoLoginManager.Instance != null)
        {
            AutoLoginManager.Instance.UpdateSettings(_settings.EnableAutoLogin, value, _settings.MaxRetryAttempts);
        }
        
        NotifySettingChanged("AutoLoginTimeoutSeconds", value);
    }

    private void OnTokenValiditySliderChanged(float value)
    {
        if (_isUpdatingUI) return;
        
        float seconds = value * 3600f; // 시간을 초로 변환
        _settings.TokenValidityThresholdSeconds = seconds;
        NotifySettingChanged("TokenValidityThresholdSeconds", seconds);
    }

    private void OnRefreshThresholdSliderChanged(float value)
    {
        if (_isUpdatingUI) return;
        
        int intValue = Mathf.RoundToInt(value);
        _settings.TokenRefreshThresholdHours = intValue;
        
        // TokenManager 설정 업데이트
        if (TokenManager.Instance != null)
        {
            TokenManager.Instance.UpdateSettings(intValue * 3600f, _settings.maxRefreshAttempts, _settings.refreshRetryDelaySeconds);
        }
        
        NotifySettingChanged("TokenRefreshThresholdHours", intValue);
    }

    // 버튼 클릭 이벤트들
    private void OnClearCredentialsClicked()
    {
        StartCoroutine(ClearCredentialsWithConfirmation());
    }

    private void OnResetSettingsClicked()
    {
        StartCoroutine(ResetSettingsWithConfirmation());
    }

    private void OnExportSettingsClicked()
    {
        ExportSettings();
    }

    private void OnImportSettingsClicked()
    {
        // UI에서 파일 선택 다이얼로그 표시 (플랫폼별 구현 필요)
        Debug.Log("[SettingsManager] Import settings clicked - UI implementation needed");
    }

    private void OnDiagnosticsClicked()
    {
        RunDiagnostics();
    }

    private void OnTestAutoLoginClicked()
    {
        TestAutoLogin();
    }

    #endregion

    #region Event Handlers - System Events

    private void OnSettingsChanged()
    {
        if (!_isUpdatingUI)
        {
            UpdateUIFromSettings();
        }
    }

    private void OnPreferencesChanged()
    {
        if (!_isUpdatingUI)
        {
            UpdateUIFromSettings();
        }
    }

    private void OnAutoLoginStarted()
    {
        UpdateStatusMessage("자동 로그인을 시도 중...", Color.yellow);
    }

    private void OnAutoLoginCompleted(AutoLoginResult result, string message)
    {
        var color = result == AutoLoginResult.Success ? successColor : errorColor;
        UpdateStatusMessage(message, color);
        UpdateUIFromSettings();
    }

    private void OnAutoLoginProgress(string message, float progress)
    {
        UpdateStatusMessage($"{message} ({progress * 100:F0}%)", Color.yellow);
    }

    private void OnTokenRefreshed(string token)
    {
        UpdateTokenStatusMessage("토큰이 성공적으로 갱신되었습니다.", successColor);
    }

    private void OnTokenRefreshFailed(string error)
    {
        UpdateTokenStatusMessage($"토큰 갱신 실패: {error}", errorColor);
    }

    private void OnTokenExpired()
    {
        UpdateTokenStatusMessage("토큰이 만료되었습니다.", errorColor);
    }

    private void OnFallbackStarted(AutoLoginResult reason, FallbackStrategy strategy)
    {
        UpdateStatusMessage($"폴백 처리 시작: {strategy}", warningColor);
    }

    private void OnFallbackCompleted(FallbackResult result)
    {
        var color = result.Success ? successColor : errorColor;
        var message = result.Success ? result.Message : result.ErrorMessage;
        UpdateStatusMessage($"폴백 완료: {message}", color);
    }

    #endregion

    #region Button Actions

    /// <summary>
    /// 확인 후 자격 증명 정리
    /// </summary>
    private IEnumerator ClearCredentialsWithConfirmation()
    {
        // 실제 구현에서는 확인 다이얼로그 표시
        Debug.Log("[SettingsManager] Clearing credentials...");
        
        try
        {
            TokenManager.Instance.ClearTokens();
            AutoLoginPrefs.ResetAllPreferences();
            
            UpdateStatusMessage("저장된 로그인 정보가 정리되었습니다.", successColor);
            UpdateUIFromSettings();
            
            yield return new WaitForSeconds(1f);
            UpdateStatusMessage("정리 완료", successColor);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage($"정리 실패: {ex.Message}", errorColor);
        }
    }

    /// <summary>
    /// 확인 후 설정 초기화
    /// </summary>
    private IEnumerator ResetSettingsWithConfirmation()
    {
        // 실제 구현에서는 확인 다이얼로그 표시
        Debug.Log("[SettingsManager] Resetting settings...");
        
        try
        {
            _settings.ResetToDefaults();
            AutoLoginPrefs.ResetAllPreferences();
            
            UpdateUIFromSettings();
            UpdateStatusMessage("설정이 초기화되었습니다.", successColor);
            
            yield return new WaitForSeconds(1f);
            UpdateStatusMessage("초기화 완료", successColor);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage($"초기화 실패: {ex.Message}", errorColor);
        }
    }

    /// <summary>
    /// 설정 내보내기
    /// </summary>
    private void ExportSettings()
    {
        try
        {
            string json = _settings.ExportToJson();
            if (!string.IsNullOrEmpty(json))
            {
                // 실제 구현에서는 파일 저장 다이얼로그 표시
                Debug.Log($"[SettingsManager] Settings exported:\n{json}");
                UpdateStatusMessage("설정이 내보내기되었습니다.", successColor);
                OnSettingsImportExport?.Invoke(true, "Settings exported successfully");
            }
            else
            {
                UpdateStatusMessage("설정 내보내기에 실패했습니다.", errorColor);
                OnSettingsImportExport?.Invoke(false, "Export failed");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage($"내보내기 실패: {ex.Message}", errorColor);
            OnSettingsImportExport?.Invoke(false, ex.Message);
        }
    }

    /// <summary>
    /// 진단 실행
    /// </summary>
    private void RunDiagnostics()
    {
        Debug.Log("[SettingsManager] Running diagnostics...");
        
        var diagnostics = GetDiagnosticInformation();
        Debug.Log($"[SettingsManager] Diagnostics:\n{diagnostics}");
        
        if (debugInfoText != null)
        {
            debugInfoText.text = diagnostics;
        }
        
        UpdateStatusMessage("진단 완료", successColor);
    }

    /// <summary>
    /// 자동 로그인 테스트
    /// </summary>
    private void TestAutoLogin()
    {
        if (AutoLoginManager.Instance != null && AutoLoginManager.Instance.CanAttemptAutoLogin)
        {
            UpdateStatusMessage("자동 로그인 테스트를 시작합니다...", Color.yellow);
            AutoLoginManager.Instance.TryAutoLogin();
        }
        else
        {
            UpdateStatusMessage("자동 로그인을 테스트할 수 없습니다.", errorColor);
        }
    }

    #endregion

    #region Status Updates

    /// <summary>
    /// 상태 업데이트 시작
    /// </summary>
    private void StartStatusUpdates()
    {
        StopStatusUpdates();
        _statusUpdateCoroutine = StartCoroutine(StatusUpdateCoroutine());
    }

    /// <summary>
    /// 상태 업데이트 중지
    /// </summary>
    private void StopStatusUpdates()
    {
        if (_statusUpdateCoroutine != null)
        {
            StopCoroutine(_statusUpdateCoroutine);
            _statusUpdateCoroutine = null;
        }
    }

    /// <summary>
    /// 상태 업데이트 코루틴
    /// </summary>
    private IEnumerator StatusUpdateCoroutine()
    {
        while (enableRealTimeUpdates && _isInitialized)
        {
            yield return new WaitForSeconds(statusUpdateInterval);
            
            if (!_isUpdatingUI)
            {
                UpdateStatusTexts();
            }
        }
    }

    /// <summary>
    /// 상태 메시지 업데이트
    /// </summary>
    private void UpdateStatusMessage(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    /// <summary>
    /// 토큰 상태 메시지 업데이트
    /// </summary>
    private void UpdateTokenStatusMessage(string message, Color color)
    {
        if (tokenStatusText != null)
        {
            tokenStatusText.text = message;
            tokenStatusText.color = color;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 설정 변경 알림
    /// </summary>
    private void NotifySettingChanged(string settingName, object newValue)
    {
        _hasUnsavedChanges = true;
        
        var eventArgs = new SettingChangeEventArgs
        {
            SettingName = settingName,
            NewValue = newValue,
            Timestamp = DateTime.Now
        };
        
        OnSettingChanged?.Invoke(eventArgs);
    }

    /// <summary>
    /// 상태 메시지 생성
    /// </summary>
    private string GetStatusMessage(AutoLoginStatus status)
    {
        if (!status.IsInitialized)
            return "초기화 중...";
        
        if (status.IsInProgress)
            return "자동 로그인 진행 중...";
        
        if (!status.IsEnabled)
            return "자동 로그인 비활성화됨";
        
        if (!status.CanAttempt)
            return "자동 로그인 불가";
        
        if (!status.HasValidTokens)
            return "저장된 토큰 없음";
        
        return status.LastResult switch
        {
            AutoLoginResult.Success => "자동 로그인 준비됨",
            AutoLoginResult.Unknown => "상태 확인 중...",
            _ => $"마지막 결과: {status.LastResult}"
        };
    }

    /// <summary>
    /// 토큰 상태 메시지 생성
    /// </summary>
    private string GetTokenStatusMessage(TokenManagerStatus status)
    {
        if (!status.IsInitialized)
            return "토큰 관리자 초기화 중...";
        
        if (status.IsRefreshing)
            return "토큰 갱신 중...";
        
        if (!status.HasValidToken)
            return "유효한 토큰 없음";
        
        if (status.TokenExpirationTime.HasValue)
        {
            var remaining = status.TokenExpirationTime.Value - DateTime.UtcNow;
            if (remaining.TotalMinutes < 30)
                return $"토큰 만료 임박 ({remaining.TotalMinutes:F0}분 남음)";
            else if (remaining.TotalHours < 24)
                return $"토큰 유효 ({remaining.TotalHours:F0}시간 남음)";
            else
                return "토큰 유효";
        }
        
        return "토큰 상태 불명";
    }

    /// <summary>
    /// 디버그 정보 생성
    /// </summary>
    private string GetDebugInformation()
    {
        var autoLoginStatus = AutoLoginManager.Instance?.GetStatus();
        var tokenStatus = TokenManager.Instance?.GetStatus();
        var prefsStatus = AutoLoginPrefs.GetPreferencesStatus();
        var fallbackStatus = FallbackHandler.Instance?.GetStatus();
        
        return $"=== AUTO-LOGIN DEBUG INFO ===\n" +
               $"Manager: {(autoLoginStatus?.IsInitialized == true ? "OK" : "NG")}\n" +
               $"Token: {(tokenStatus?.HasValidToken == true ? "OK" : "NG")}\n" +
               $"Prefs: {(prefsStatus?.CanPerformAutoLogin == true ? "OK" : "NG")}\n" +
               $"Fallback: {(fallbackStatus?.IsInitialized == true ? "OK" : "NG")}\n" +
               $"Last Attempt: {prefsStatus?.LastAutoLoginTime:HH:mm:ss}\n" +
               $"Today Attempts: {prefsStatus?.TodayAttempts}\n" +
               $"Settings Version: {prefsStatus?.SettingsVersion}";
    }

    /// <summary>
    /// 진단 정보 생성
    /// </summary>
    private string GetDiagnosticInformation()
    {
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine("=== SETTINGS DIAGNOSTICS ===");
        
        // 설정 상태
        diagnostics.AppendLine($"Settings Manager: {(_isInitialized ? "Initialized" : "Not Initialized")}");
        diagnostics.AppendLine($"Real-time Updates: {enableRealTimeUpdates}");
        diagnostics.AppendLine($"Has Unsaved Changes: {_hasUnsavedChanges}");
        
        // 핵심 컴포넌트 상태
        diagnostics.AppendLine($"AutoLoginManager: {(AutoLoginManager.Instance?.IsInitialized == true ? "OK" : "NG")}");
        diagnostics.AppendLine($"TokenManager: {(TokenManager.Instance?.IsInitialized == true ? "OK" : "NG")}");
        diagnostics.AppendLine($"FallbackHandler: {(FallbackHandler.Instance?.IsInitialized == true ? "OK" : "NG")}");
        
        // 설정 값
        if (_settings != null)
        {
            var summary = _settings.GetSettingsSummary();
            diagnostics.AppendLine($"Auto-Login Enabled: {summary.IsAutoLoginEnabled}");
            diagnostics.AppendLine($"Biometric Required: {summary.RequiresBiometric}");
            diagnostics.AppendLine($"Token Auto-Refresh: {_settings.EnableTokenAutoRefresh}");
            diagnostics.AppendLine($"Secure Storage: {summary.UseSecureStorage}");
        }
        
        return diagnostics.ToString();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 설정 강제 새로고침
    /// </summary>
    public void RefreshSettings()
    {
        if (_isInitialized)
        {
            UpdateUIFromSettings();
            Debug.Log("[SettingsManager] Settings refreshed manually");
        }
    }

    /// <summary>
    /// 특정 설정 값 가져오기
    /// </summary>
    public T GetSetting<T>(string settingName)
    {
        if (_settings == null) return default(T);
        
        // Reflection을 사용하여 설정 값 가져오기
        var property = _settings.GetType().GetProperty(settingName);
        if (property != null && property.PropertyType == typeof(T))
        {
            return (T)property.GetValue(_settings);
        }
        
        return default(T);
    }

    /// <summary>
    /// 특정 설정 값 설정
    /// </summary>
    public bool SetSetting<T>(string settingName, T value)
    {
        if (_settings == null) return false;
        
        try
        {
            var property = _settings.GetType().GetProperty(settingName);
            if (property != null && property.CanWrite && property.PropertyType == typeof(T))
            {
                property.SetValue(_settings, value);
                NotifySettingChanged(settingName, value);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsManager] Failed to set setting {settingName}: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// 현재 설정 상태 정보 반환
    /// </summary>
    public SettingsManagerStatus GetStatus()
    {
        return new SettingsManagerStatus
        {
            IsInitialized = _isInitialized,
            IsUpdatingUI = _isUpdatingUI,
            EnableRealTimeUpdates = enableRealTimeUpdates,
            HasUnsavedChanges = _hasUnsavedChanges,
            EnableAdvancedSettings = enableAdvancedSettings,
            EnableDeveloperMode = enableDeveloperMode
        };
    }

    #endregion
}

#region Data Classes

/// <summary>
/// 설정 변경 이벤트 인자
/// </summary>
[Serializable]
public class SettingChangeEventArgs
{
    public string SettingName { get; set; }
    public object NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 설정 매니저 상태 정보
/// </summary>
[Serializable]
public class SettingsManagerStatus
{
    public bool IsInitialized { get; set; }
    public bool IsUpdatingUI { get; set; }
    public bool EnableRealTimeUpdates { get; set; }
    public bool HasUnsavedChanges { get; set; }
    public bool EnableAdvancedSettings { get; set; }
    public bool EnableDeveloperMode { get; set; }
}

#endregion