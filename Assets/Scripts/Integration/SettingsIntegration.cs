using System;
using UnityEngine;

/// <summary>
/// SettingsIntegration - 설정 시스템 통합 및 조정 클래스
/// MainPageSettings, LogoutHandler, TermsHandler를 통합하여 
/// 일관된 설정 경험을 제공하고 Stream B UI와 원활한 연동을 지원합니다.
/// </summary>
public class SettingsIntegration : MonoBehaviour
{
    #region Singleton
    private static SettingsIntegration _instance;
    public static SettingsIntegration Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SettingsIntegration>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SettingsIntegration");
                    _instance = go.AddComponent<SettingsIntegration>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Component References
    [Header("Component References")]
    [SerializeField] private MainPageSettings _mainPageSettings;
    [SerializeField] private LogoutHandler _logoutHandler;
    [SerializeField] private TermsHandler _termsHandler;
    #endregion

    #region Events
    /// <summary>
    /// 통합 시스템 초기화 완료 이벤트
    /// </summary>
    public static event Action OnIntegrationInitialized;
    
    /// <summary>
    /// 설정 변경 통합 이벤트 (UI 업데이트용)
    /// </summary>
    public static event Action<string, object> OnSettingChanged;
    
    /// <summary>
    /// 시스템 상태 변경 이벤트
    /// </summary>
    public static event Action<SystemStatus> OnSystemStatusChanged;
    #endregion

    #region Properties
    /// <summary>
    /// MainPageSettings 컴포넌트 참조
    /// </summary>
    public MainPageSettings MainPageSettings => _mainPageSettings;
    
    /// <summary>
    /// LogoutHandler 컴포넌트 참조
    /// </summary>
    public LogoutHandler LogoutHandler => _logoutHandler;
    
    /// <summary>
    /// TermsHandler 컴포넌트 참조
    /// </summary>
    public TermsHandler TermsHandler => _termsHandler;

    /// <summary>
    /// 통합 시스템 초기화 여부
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// 모든 컴포넌트가 사용 가능한지 여부
    /// </summary>
    public bool AllComponentsAvailable =>
        _mainPageSettings != null && _logoutHandler != null && _termsHandler != null;
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

        InitializeComponents();
    }

    private void Start()
    {
        InitializeIntegration();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // 이벤트 정리
        OnIntegrationInitialized = null;
        OnSettingChanged = null;
        OnSystemStatusChanged = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 컴포넌트 초기화 및 참조 설정
    /// </summary>
    private void InitializeComponents()
    {
        try
        {
            // 컴포넌트 자동 검색
            if (_mainPageSettings == null)
            {
                _mainPageSettings = GetComponent<MainPageSettings>();
                if (_mainPageSettings == null)
                {
                    _mainPageSettings = FindObjectOfType<MainPageSettings>();
                }
            }

            if (_logoutHandler == null)
            {
                _logoutHandler = GetComponent<LogoutHandler>();
                if (_logoutHandler == null)
                {
                    _logoutHandler = FindObjectOfType<LogoutHandler>();
                }
            }

            if (_termsHandler == null)
            {
                _termsHandler = GetComponent<TermsHandler>();
                if (_termsHandler == null)
                {
                    _termsHandler = FindObjectOfType<TermsHandler>();
                }
            }

            // 누락된 컴포넌트 자동 추가
            CreateMissingComponents();

            Debug.Log("[SettingsIntegration] Components initialized");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsIntegration] Failed to initialize components: {ex.Message}");
        }
    }

    /// <summary>
    /// 누락된 컴포넌트 생성
    /// </summary>
    private void CreateMissingComponents()
    {
        if (_mainPageSettings == null)
        {
            _mainPageSettings = gameObject.AddComponent<MainPageSettings>();
            Debug.Log("[SettingsIntegration] MainPageSettings component created");
        }

        if (_logoutHandler == null)
        {
            _logoutHandler = gameObject.AddComponent<LogoutHandler>();
            Debug.Log("[SettingsIntegration] LogoutHandler component created");
        }

        if (_termsHandler == null)
        {
            _termsHandler = gameObject.AddComponent<TermsHandler>();
            Debug.Log("[SettingsIntegration] TermsHandler component created");
        }
    }

    /// <summary>
    /// 통합 시스템 초기화
    /// </summary>
    private void InitializeIntegration()
    {
        try
        {
            if (!AllComponentsAvailable)
            {
                Debug.LogError("[SettingsIntegration] Not all required components are available");
                return;
            }

            // 이벤트 구독
            SubscribeToEvents();

            // 시스템 상태 알림
            NotifySystemStatus();

            IsInitialized = true;
            OnIntegrationInitialized?.Invoke();

            Debug.Log("[SettingsIntegration] Integration system initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsIntegration] Failed to initialize integration: {ex.Message}");
            IsInitialized = false;
        }
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // MainPageSettings 이벤트
        MainPageSettings.OnMusicSettingChanged += OnMusicSettingChanged;
        MainPageSettings.OnSoundSettingChanged += OnSoundSettingChanged;
        MainPageSettings.OnSettingsInitialized += OnSettingsInitialized;

        // LogoutHandler 이벤트
        LogoutHandler.OnLogoutStarted += OnLogoutStarted;
        LogoutHandler.OnLogoutProgress += OnLogoutProgress;
        LogoutHandler.OnLogoutCompleted += OnLogoutCompleted;

        // TermsHandler 이벤트
        TermsHandler.OnTermsDisplayStarted += OnTermsDisplayStarted;
        TermsHandler.OnTermsDisplayCompleted += OnTermsDisplayCompleted;
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        // MainPageSettings 이벤트
        MainPageSettings.OnMusicSettingChanged -= OnMusicSettingChanged;
        MainPageSettings.OnSoundSettingChanged -= OnSoundSettingChanged;
        MainPageSettings.OnSettingsInitialized -= OnSettingsInitialized;

        // LogoutHandler 이벤트
        LogoutHandler.OnLogoutStarted -= OnLogoutStarted;
        LogoutHandler.OnLogoutProgress -= OnLogoutProgress;
        LogoutHandler.OnLogoutCompleted -= OnLogoutCompleted;

        // TermsHandler 이벤트
        TermsHandler.OnTermsDisplayStarted -= OnTermsDisplayStarted;
        TermsHandler.OnTermsDisplayCompleted -= OnTermsDisplayCompleted;
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 배경음악 설정 변경 이벤트 핸들러
    /// </summary>
    private void OnMusicSettingChanged(bool isEnabled)
    {
        OnSettingChanged?.Invoke("MusicEnabled", isEnabled);
        Debug.Log($"[SettingsIntegration] Music setting changed: {isEnabled}");
    }

    /// <summary>
    /// 효과음 설정 변경 이벤트 핸들러
    /// </summary>
    private void OnSoundSettingChanged(bool isEnabled)
    {
        OnSettingChanged?.Invoke("SoundEnabled", isEnabled);
        Debug.Log($"[SettingsIntegration] Sound setting changed: {isEnabled}");
    }

    /// <summary>
    /// 설정 초기화 완료 이벤트 핸들러
    /// </summary>
    private void OnSettingsInitialized()
    {
        NotifySystemStatus();
        Debug.Log("[SettingsIntegration] Settings initialization completed");
    }

    /// <summary>
    /// 로그아웃 시작 이벤트 핸들러
    /// </summary>
    private void OnLogoutStarted()
    {
        OnSettingChanged?.Invoke("LogoutStatus", "Started");
        Debug.Log("[SettingsIntegration] Logout process started");
    }

    /// <summary>
    /// 로그아웃 진행 상태 이벤트 핸들러
    /// </summary>
    private void OnLogoutProgress(string message, float progress)
    {
        OnSettingChanged?.Invoke("LogoutProgress", new { Message = message, Progress = progress });
        Debug.Log($"[SettingsIntegration] Logout progress: {message} ({progress * 100:F0}%)");
    }

    /// <summary>
    /// 로그아웃 완료 이벤트 핸들러
    /// </summary>
    private void OnLogoutCompleted(bool success, string message)
    {
        OnSettingChanged?.Invoke("LogoutStatus", new { Success = success, Message = message });
        Debug.Log($"[SettingsIntegration] Logout completed: {success} - {message}");
    }

    /// <summary>
    /// 약관 표시 시작 이벤트 핸들러
    /// </summary>
    private void OnTermsDisplayStarted(TermsType termsType)
    {
        OnSettingChanged?.Invoke("TermsDisplayStatus", new { Type = termsType, Status = "Started" });
        Debug.Log($"[SettingsIntegration] Terms display started: {termsType}");
    }

    /// <summary>
    /// 약관 표시 완료 이벤트 핸들러
    /// </summary>
    private void OnTermsDisplayCompleted(TermsType termsType, bool success)
    {
        OnSettingChanged?.Invoke("TermsDisplayStatus", new { Type = termsType, Status = "Completed", Success = success });
        Debug.Log($"[SettingsIntegration] Terms display completed: {termsType} - {success}");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 배경음악 토글 (UI 연동)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    public void ToggleMusic(bool isEnabled)
    {
        if (_mainPageSettings?.IsInitialized == true)
        {
            _mainPageSettings.OnMusicToggleChanged(isEnabled);
        }
        else
        {
            Debug.LogWarning("[SettingsIntegration] MainPageSettings not initialized");
        }
    }

    /// <summary>
    /// 효과음 토글 (UI 연동)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    public void ToggleSound(bool isEnabled)
    {
        if (_mainPageSettings?.IsInitialized == true)
        {
            _mainPageSettings.OnSoundToggleChanged(isEnabled);
        }
        else
        {
            Debug.LogWarning("[SettingsIntegration] MainPageSettings not initialized");
        }
    }

    /// <summary>
    /// 로그아웃 시작 (UI 연동)
    /// </summary>
    public void InitiateLogout()
    {
        if (_logoutHandler != null)
        {
            _logoutHandler.InitiateLogout();
        }
        else
        {
            Debug.LogError("[SettingsIntegration] LogoutHandler not available");
        }
    }

    /// <summary>
    /// 이용약관 표시 (UI 연동)
    /// </summary>
    public void ShowTermsAndConditions()
    {
        if (_termsHandler != null)
        {
            _termsHandler.ShowTermsAndConditions();
        }
        else
        {
            Debug.LogError("[SettingsIntegration] TermsHandler not available");
        }
    }

    /// <summary>
    /// 개인정보 처리방침 표시 (UI 연동)
    /// </summary>
    public void ShowPrivacyPolicy()
    {
        if (_termsHandler != null)
        {
            _termsHandler.ShowPrivacyPolicy();
        }
        else
        {
            Debug.LogError("[SettingsIntegration] TermsHandler not available");
        }
    }

    /// <summary>
    /// 우편함 열기 (MailboxManager 연동)
    /// </summary>
    public void OpenMailbox()
    {
        try
        {
            if (MailboxManager.Instance != null)
            {
                MailboxManager.Instance.ShowMailbox();
                OnSettingChanged?.Invoke("MailboxOpened", true);
                Debug.Log("[SettingsIntegration] Mailbox opened");
            }
            else
            {
                Debug.LogWarning("[SettingsIntegration] MailboxManager not available");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsIntegration] Failed to open mailbox: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 설정 상태 가져오기
    /// </summary>
    public IntegratedSettingsStatus GetCurrentSettings()
    {
        return new IntegratedSettingsStatus
        {
            IsInitialized = IsInitialized,
            MusicEnabled = _mainPageSettings?.IsMusicEnabled ?? true,
            SoundEnabled = _mainPageSettings?.IsSoundEnabled ?? true,
            IsLogoutInProgress = _logoutHandler?.IsLogoutInProgress ?? false,
            IsDisplayingTerms = _termsHandler?.IsDisplayingTerms ?? false,
            SystemStatus = GetCurrentSystemStatus()
        };
    }

    /// <summary>
    /// 모든 설정을 기본값으로 리셋
    /// </summary>
    public void ResetAllSettings()
    {
        try
        {
            if (_mainPageSettings?.IsInitialized == true)
            {
                _mainPageSettings.ResetToDefaults();
            }

            OnSettingChanged?.Invoke("SettingsReset", DateTime.Now);
            Debug.Log("[SettingsIntegration] All settings reset to defaults");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsIntegration] Failed to reset settings: {ex.Message}");
        }
    }

    /// <summary>
    /// 수동 새로고침
    /// </summary>
    public void RefreshIntegration()
    {
        try
        {
            if (IsInitialized)
            {
                NotifySystemStatus();
                OnSettingChanged?.Invoke("IntegrationRefreshed", DateTime.Now);
                Debug.Log("[SettingsIntegration] Integration refreshed");
            }
            else
            {
                InitializeIntegration();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsIntegration] Failed to refresh integration: {ex.Message}");
        }
    }
    #endregion

    #region System Status
    /// <summary>
    /// 시스템 상태 알림
    /// </summary>
    private void NotifySystemStatus()
    {
        var status = GetCurrentSystemStatus();
        OnSystemStatusChanged?.Invoke(status);
    }

    /// <summary>
    /// 현재 시스템 상태 반환
    /// </summary>
    private SystemStatus GetCurrentSystemStatus()
    {
        return new SystemStatus
        {
            IsIntegrationInitialized = IsInitialized,
            IsSettingsReady = _mainPageSettings?.IsInitialized ?? false,
            IsLogoutHandlerReady = _logoutHandler != null,
            IsTermsHandlerReady = _termsHandler != null,
            HasSettingsManager = SettingsManager.Instance?.IsInitialized ?? false,
            HasAuthenticationManager = AuthenticationManager.Instance != null,
            HasMailboxManager = MailboxManager.Instance != null,
            Timestamp = DateTime.Now
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 통합된 설정 상태 정보
/// </summary>
[Serializable]
public class IntegratedSettingsStatus
{
    public bool IsInitialized { get; set; }
    public bool MusicEnabled { get; set; }
    public bool SoundEnabled { get; set; }
    public bool IsLogoutInProgress { get; set; }
    public bool IsDisplayingTerms { get; set; }
    public SystemStatus SystemStatus { get; set; }
}

/// <summary>
/// 시스템 상태 정보
/// </summary>
[Serializable]
public class SystemStatus
{
    public bool IsIntegrationInitialized { get; set; }
    public bool IsSettingsReady { get; set; }
    public bool IsLogoutHandlerReady { get; set; }
    public bool IsTermsHandlerReady { get; set; }
    public bool HasSettingsManager { get; set; }
    public bool HasAuthenticationManager { get; set; }
    public bool HasMailboxManager { get; set; }
    public DateTime Timestamp { get; set; }
}
#endregion