using UnityEngine;
using System.Collections;

/// <summary>
/// 메인 페이지 설정 및 섹션 등록을 담당하는 헬퍼 클래스
/// MainPageManager와 각 섹션들을 연결하고 초기화를 조정합니다.
/// </summary>
public class MainPageSetup : MonoBehaviour
{
    #region Section References
    [Header("Section References")]
    [SerializeField] private ProfileSection profileSection;
    [SerializeField] private EnergySection energySection;
    [SerializeField] private MatchingSection matchingSection;
    [SerializeField] private SettingsSection settingsSection;
    
    [Header("Setup Configuration")]
    [SerializeField] private bool autoRegisterSections = true;
    [SerializeField] private bool activateAllSectionsOnStart = true;
    [SerializeField] private float sectionRegistrationDelay = 0.1f;
    #endregion

    #region Private Fields
    private MainPageManager _mainPageManager;
    private bool _isSetupComplete = false;
    #endregion

    #region Properties
    /// <summary>
    /// 메인 페이지 설정 완료 여부
    /// </summary>
    public bool IsSetupComplete => _isSetupComplete;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        StartCoroutine(SetupMainPage());
    }

    private void OnDestroy()
    {
        UnregisterAllSections();
    }
    #endregion

    #region Setup Process
    /// <summary>
    /// 메인 페이지 설정 코루틴
    /// </summary>
    private IEnumerator SetupMainPage()
    {
        Debug.Log("[MainPageSetup] Starting main page setup...");
        
        // 1. MainPageManager 확인 및 대기
        yield return StartCoroutine(WaitForMainPageManager());
        
        // 2. 섹션 유효성 검증
        ValidateSectionReferences();
        
        // 3. 섹션 등록
        if (autoRegisterSections)
        {
            yield return StartCoroutine(RegisterAllSections());
        }
        
        // 4. 초기 활성화
        if (activateAllSectionsOnStart)
        {
            yield return StartCoroutine(ActivateAllSections());
        }
        
        _isSetupComplete = true;
        Debug.Log("[MainPageSetup] Main page setup complete!");
    }
    
    /// <summary>
    /// MainPageManager 준비 대기
    /// </summary>
    private IEnumerator WaitForMainPageManager()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            _mainPageManager = MainPageManager.Instance;
            
            if (_mainPageManager != null && _mainPageManager.IsInitialized)
            {
                Debug.Log("[MainPageSetup] MainPageManager is ready");
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Debug.LogError("[MainPageSetup] MainPageManager initialization timeout!");
    }
    
    /// <summary>
    /// 섹션 참조 유효성 검증
    /// </summary>
    private void ValidateSectionReferences()
    {
        int validSections = 0;
        
        if (profileSection != null) validSections++;
        else Debug.LogWarning("[MainPageSetup] ProfileSection reference is missing");
        
        if (energySection != null) validSections++;
        else Debug.LogWarning("[MainPageSetup] EnergySection reference is missing");
        
        if (matchingSection != null) validSections++;
        else Debug.LogWarning("[MainPageSetup] MatchingSection reference is missing");
        
        if (settingsSection != null) validSections++;
        else Debug.LogError("[MainPageSetup] SettingsSection reference is missing!");
        
        Debug.Log($"[MainPageSetup] Validated {validSections}/4 section references");
    }
    #endregion

    #region Section Registration
    /// <summary>
    /// 모든 섹션 등록
    /// </summary>
    private IEnumerator RegisterAllSections()
    {
        Debug.Log("[MainPageSetup] Registering all sections...");
        
        // 섹션별 순차 등록 (종속성 고려)
        var registrationTasks = new[]
        {
            RegisterSection(MainPageSectionType.Profile, profileSection),
            RegisterSection(MainPageSectionType.Energy, energySection),
            RegisterSection(MainPageSectionType.Matching, matchingSection),
            RegisterSection(MainPageSectionType.Settings, settingsSection)
        };
        
        foreach (var task in registrationTasks)
        {
            yield return StartCoroutine(task);
            yield return new WaitForSeconds(sectionRegistrationDelay);
        }
        
        Debug.Log("[MainPageSetup] All sections registered successfully");
    }
    
    /// <summary>
    /// 개별 섹션 등록
    /// </summary>
    private IEnumerator RegisterSection(MainPageSectionType sectionType, SectionBase section)
    {
        if (section == null)
        {
            Debug.LogWarning($"[MainPageSetup] Cannot register null section: {sectionType}");
            yield break;
        }
        
        if (_mainPageManager == null)
        {
            Debug.LogError($"[MainPageSetup] MainPageManager not available for section: {sectionType}");
            yield break;
        }
        
        try
        {
            Debug.Log($"[MainPageSetup] Registering section: {sectionType}");
            _mainPageManager.RegisterSection(sectionType, section);
            
            // 등록 완료 대기
            yield return new WaitForSeconds(0.1f);
            
            // 등록 확인
            var registeredSection = _mainPageManager.GetSection<SectionBase>(sectionType);
            if (registeredSection != null)
            {
                Debug.Log($"[MainPageSetup] Section registered successfully: {sectionType}");
            }
            else
            {
                Debug.LogError($"[MainPageSetup] Section registration failed: {sectionType}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainPageSetup] Section registration error for {sectionType}: {e.Message}");
        }
    }
    
    /// <summary>
    /// 모든 섹션 등록 해제
    /// </summary>
    private void UnregisterAllSections()
    {
        if (_mainPageManager == null) return;
        
        var sectionTypes = new[]
        {
            MainPageSectionType.Settings,
            MainPageSectionType.Matching, 
            MainPageSectionType.Energy,
            MainPageSectionType.Profile
        };
        
        foreach (var sectionType in sectionTypes)
        {
            try
            {
                _mainPageManager.UnregisterSection(sectionType);
                Debug.Log($"[MainPageSetup] Section unregistered: {sectionType}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainPageSetup] Section unregistration error for {sectionType}: {e.Message}");
            }
        }
    }
    #endregion

    #region Section Activation
    /// <summary>
    /// 모든 섹션 활성화
    /// </summary>
    private IEnumerator ActivateAllSections()
    {
        if (_mainPageManager == null) yield break;
        
        Debug.Log("[MainPageSetup] Activating all sections...");
        
        // MainPageManager의 순차 활성화 사용
        _mainPageManager.ActivateAllSections();
        
        // 활성화 완료 대기
        yield return new WaitForSeconds(2f);
        
        // 활성화 상태 확인
        var activeCount = _mainPageManager.ActiveSectionCount;
        Debug.Log($"[MainPageSetup] {activeCount} sections are now active");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 수동 섹션 등록 (런타임)
    /// </summary>
    public void RegisterSectionManually(MainPageSectionType sectionType, SectionBase section)
    {
        if (_mainPageManager != null && section != null)
        {
            _mainPageManager.RegisterSection(sectionType, section);
            Debug.Log($"[MainPageSetup] Manually registered section: {sectionType}");
        }
    }
    
    /// <summary>
    /// 특정 섹션 활성화/비활성화
    /// </summary>
    public void SetSectionActive(MainPageSectionType sectionType, bool active)
    {
        if (_mainPageManager != null)
        {
            _mainPageManager.SetSectionActive(sectionType, active);
            Debug.Log($"[MainPageSetup] Section {sectionType} set to {(active ? "active" : "inactive")}");
        }
    }
    
    /// <summary>
    /// 설정 섹션 강제 새로고침
    /// </summary>
    public void RefreshSettingsSection()
    {
        if (settingsSection != null && settingsSection.IsActive)
        {
            settingsSection.ForceRefresh();
            Debug.Log("[MainPageSetup] Settings section refreshed");
        }
    }
    
    /// <summary>
    /// 메인 페이지 상태 정보 가져오기
    /// </summary>
    public MainPageManagerStatus GetMainPageStatus()
    {
        return _mainPageManager?.GetStatus();
    }
    
    /// <summary>
    /// 설정 섹션에 알림 추가 (외부 API)
    /// </summary>
    public void AddSettingsNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        if (settingsSection != null)
        {
            var notification = new SettingsNotification
            {
                Type = type,
                Title = title,
                Message = message,
                Priority = NotificationPriority.Normal,
                AutoDismiss = true,
                DismissAfter = 5f
            };
            
            settingsSection.AddNotification(notification);
            Debug.Log($"[MainPageSetup] Added notification to settings: {title}");
        }
    }
    #endregion

    #region Debug & Diagnostics
    /// <summary>
    /// 현재 설정 상태 로그 출력
    /// </summary>
    [ContextMenu("Log Current Status")]
    public void LogCurrentStatus()
    {
        Debug.Log("=== MAIN PAGE SETUP STATUS ===");
        Debug.Log($"Setup Complete: {_isSetupComplete}");
        Debug.Log($"MainPageManager Available: {_mainPageManager != null}");
        
        if (_mainPageManager != null)
        {
            var status = _mainPageManager.GetStatus();
            Debug.Log($"Active Sections: {status.ActiveSectionCount}/{status.RegisteredSectionCount}");
            Debug.Log($"Authenticated: {status.IsAuthenticated}");
            Debug.Log($"Has User: {status.HasCurrentUser}");
            Debug.Log($"Offline Mode: {status.IsOfflineMode}");
        }
        
        Debug.Log($"Profile Section: {(profileSection != null ? "OK" : "Missing")}");
        Debug.Log($"Energy Section: {(energySection != null ? "OK" : "Missing")}");
        Debug.Log($"Matching Section: {(matchingSection != null ? "OK" : "Missing")}");
        Debug.Log($"Settings Section: {(settingsSection != null ? "OK" : "Missing")}");
    }
    
    /// <summary>
    /// 설정 섹션 디버그 정보 출력
    /// </summary>
    [ContextMenu("Log Settings Section Debug")]
    public void LogSettingsDebug()
    {
        if (settingsSection == null)
        {
            Debug.LogWarning("[MainPageSetup] Settings section not available");
            return;
        }
        
        Debug.Log("=== SETTINGS SECTION DEBUG ===");
        Debug.Log($"Initialized: {settingsSection.IsInitialized}");
        Debug.Log($"Active: {settingsSection.IsActive}");
        Debug.Log($"Refreshing: {settingsSection.IsRefreshing}");
        Debug.Log($"Pending Notifications: {settingsSection.PendingNotificationsCount}");
        
        // 주요 설정값들 출력
        Debug.Log($"Master Volume: {settingsSection.GetCachedSetting<float>("MasterVolume")}");
        Debug.Log($"Music Volume: {settingsSection.GetCachedSetting<float>("MusicVolume")}");
        Debug.Log($"Is Muted: {settingsSection.GetCachedSetting<bool>("IsMuted")}");
        Debug.Log($"Quality Level: {settingsSection.GetCachedSetting<int>("QualityLevel")}");
        Debug.Log($"Fullscreen: {settingsSection.GetCachedSetting<bool>("IsFullscreen")}");
    }
    #endregion
}