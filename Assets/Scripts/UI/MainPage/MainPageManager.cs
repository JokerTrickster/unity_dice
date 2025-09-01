using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메인 페이지 전체 상태 관리자
/// 4개 섹션(프로필, 피로도, 매칭, 설정) 간 통신 조정 및 기존 매니저들과 연동을 담당하는 Singleton 클래스
/// </summary>
public class MainPageManager : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 메인 페이지가 초기화될 때 발생하는 이벤트
    /// </summary>
    public static event Action OnMainPageInitialized;
    
    /// <summary>
    /// 섹션이 활성화/비활성화될 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType, bool> OnSectionStateChanged;
    
    /// <summary>
    /// 섹션 간 데이터 통신이 필요할 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType, MainPageSectionType, object> OnSectionCommunication;
    
    /// <summary>
    /// 사용자 데이터가 업데이트될 때 발생하는 이벤트
    /// </summary>
    public static event Action<UserData> OnUserDataRefreshed;
    
    /// <summary>
    /// 설정이 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<string, object> OnSettingChanged;
    #endregion

    #region Singleton
    private static MainPageManager _instance;
    public static MainPageManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MainPageManager");
                _instance = go.AddComponent<MainPageManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private readonly Dictionary<MainPageSectionType, SectionBase> _sections = new();
    private readonly Dictionary<MainPageSectionType, bool> _sectionStates = new();
    private bool _isInitialized = false;
    private bool _isRefreshingData = false;
    private Coroutine _dataRefreshCoroutine;
    
    // 외부 매니저 참조 캐싱
    private UserDataManager _userDataManager;
    private AuthenticationManager _authenticationManager;
    private SettingsManager _settingsManager;
    private ScreenTransitionManager _screenTransitionManager;
    
    // 설정 상수
    private const float DATA_REFRESH_INTERVAL = 30f; // 30초마다 데이터 갱신
    private const float SECTION_ACTIVATION_DELAY = 0.1f; // 섹션 활성화 간 지연시간
    #endregion

    #region Public Properties
    /// <summary>
    /// 메인 페이지 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 현재 활성화된 섹션 수
    /// </summary>
    public int ActiveSectionCount 
    { 
        get 
        { 
            int count = 0;
            foreach (var state in _sectionStates.Values)
            {
                if (state) count++;
            }
            return count;
        }
    }
    
    /// <summary>
    /// 현재 사용자 데이터
    /// </summary>
    public UserData CurrentUserData => _userDataManager?.CurrentUser;
    
    /// <summary>
    /// 현재 인증 상태
    /// </summary>
    public bool IsAuthenticated => _authenticationManager?.IsAuthenticated ?? false;
    
    /// <summary>
    /// 데이터 갱신 진행 중 여부
    /// </summary>
    public bool IsRefreshingData => _isRefreshingData;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton 패턴 구현
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAsync());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        UnsubscribeFromEvents();
        
        // 이벤트 구독 해제
        OnMainPageInitialized = null;
        OnSectionStateChanged = null;
        OnSectionCommunication = null;
        OnUserDataRefreshed = null;
        OnSettingChanged = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 비동기 초기화
    /// </summary>
    private IEnumerator InitializeAsync()
    {
        Debug.Log("[MainPageManager] Initializing...");
        
        // 외부 매니저들 초기화 대기
        yield return WaitForManagersInitialization();
        
        // 외부 매니저 참조 설정
        SetupManagerReferences();
        
        // 외부 이벤트 구독
        SubscribeToEvents();
        
        // 섹션 상태 초기화
        InitializeSectionStates();
        
        // 데이터 갱신 시작
        StartDataRefresh();
        
        _isInitialized = true;
        OnMainPageInitialized?.Invoke();
        
        Debug.Log("[MainPageManager] Initialization complete");
    }
    
    /// <summary>
    /// 매니저들 초기화 대기
    /// </summary>
    private IEnumerator WaitForManagersInitialization()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            bool allReady = true;
            
            // UserDataManager 확인
            if (UserDataManager.Instance == null || !UserDataManager.Instance.IsInitialized)
                allReady = false;
            
            // AuthenticationManager 확인
            if (AuthenticationManager.Instance == null || !AuthenticationManager.Instance.IsInitialized)
                allReady = false;
            
            // SettingsManager 확인
            if (SettingsManager.Instance == null || !SettingsManager.Instance.IsInitialized)
                allReady = false;
            
            if (allReady)
            {
                Debug.Log("[MainPageManager] All managers ready");
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Debug.LogWarning("[MainPageManager] Manager initialization timeout, proceeding anyway");
    }
    
    /// <summary>
    /// 매니저 참조 설정
    /// </summary>
    private void SetupManagerReferences()
    {
        _userDataManager = UserDataManager.Instance;
        _authenticationManager = AuthenticationManager.Instance;
        _settingsManager = SettingsManager.Instance;
        _screenTransitionManager = ScreenTransitionManager.Instance;
        
        Debug.Log("[MainPageManager] Manager references established");
    }
    
    /// <summary>
    /// 섹션 상태 초기화
    /// </summary>
    private void InitializeSectionStates()
    {
        _sectionStates[MainPageSectionType.Profile] = true;
        _sectionStates[MainPageSectionType.Energy] = true;
        _sectionStates[MainPageSectionType.Matching] = true;
        _sectionStates[MainPageSectionType.Settings] = true;
        
        Debug.Log("[MainPageManager] Section states initialized");
    }
    #endregion

    #region Event Management
    /// <summary>
    /// 외부 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // UserDataManager 이벤트
        if (_userDataManager != null)
        {
            UserDataManager.OnUserDataLoaded += OnUserDataLoaded;
            UserDataManager.OnUserDataUpdated += OnUserDataUpdated;
            UserDataManager.OnOfflineModeChanged += OnOfflineModeChanged;
        }
        
        // AuthenticationManager 이벤트
        if (_authenticationManager != null)
        {
            AuthenticationManager.OnAuthenticationStateChanged += OnAuthenticationStateChanged;
            AuthenticationManager.OnLogoutCompleted += OnLogoutCompleted;
        }
        
        // SettingsManager 이벤트
        if (_settingsManager != null)
        {
            SettingsManager.OnSettingChanged += OnSettingsChanged;
        }
    }
    
    /// <summary>
    /// 외부 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_userDataManager != null)
        {
            UserDataManager.OnUserDataLoaded -= OnUserDataLoaded;
            UserDataManager.OnUserDataUpdated -= OnUserDataUpdated;
            UserDataManager.OnOfflineModeChanged -= OnOfflineModeChanged;
        }
        
        if (_authenticationManager != null)
        {
            AuthenticationManager.OnAuthenticationStateChanged -= OnAuthenticationStateChanged;
            AuthenticationManager.OnLogoutCompleted -= OnLogoutCompleted;
        }
        
        if (_settingsManager != null)
        {
            SettingsManager.OnSettingChanged -= OnSettingsChanged;
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 사용자 데이터 로드 이벤트 처리
    /// </summary>
    private void OnUserDataLoaded(UserData userData)
    {
        Debug.Log($"[MainPageManager] User data loaded: {userData.DisplayName}");
        OnUserDataRefreshed?.Invoke(userData);
        NotifySectionsOfDataUpdate(userData);
    }
    
    /// <summary>
    /// 사용자 데이터 업데이트 이벤트 처리
    /// </summary>
    private void OnUserDataUpdated(UserData userData)
    {
        Debug.Log($"[MainPageManager] User data updated: {userData.DisplayName}");
        OnUserDataRefreshed?.Invoke(userData);
        NotifySectionsOfDataUpdate(userData);
    }
    
    /// <summary>
    /// 오프라인 모드 변경 이벤트 처리
    /// </summary>
    private void OnOfflineModeChanged(bool isOffline)
    {
        Debug.Log($"[MainPageManager] Offline mode changed: {isOffline}");
        NotifySectionsOfModeChange(isOffline);
    }
    
    /// <summary>
    /// 인증 상태 변경 이벤트 처리
    /// </summary>
    private void OnAuthenticationStateChanged(bool isAuthenticated)
    {
        Debug.Log($"[MainPageManager] Authentication state changed: {isAuthenticated}");
        
        if (!isAuthenticated)
        {
            // 로그아웃 시 로그인 화면으로 전환
            TransitionToLoginScreen();
        }
    }
    
    /// <summary>
    /// 로그아웃 완료 이벤트 처리
    /// </summary>
    private void OnLogoutCompleted()
    {
        Debug.Log("[MainPageManager] Logout completed");
        TransitionToLoginScreen();
    }
    
    /// <summary>
    /// 설정 변경 이벤트 처리
    /// </summary>
    private void OnSettingsChanged(SettingChangeEventArgs args)
    {
        Debug.Log($"[MainPageManager] Setting changed: {args.SettingName} = {args.NewValue}");
        OnSettingChanged?.Invoke(args.SettingName, args.NewValue);
        NotifySectionsOfSettingChange(args.SettingName, args.NewValue);
    }
    #endregion

    #region Section Management
    /// <summary>
    /// 섹션 등록
    /// </summary>
    public void RegisterSection(MainPageSectionType sectionType, SectionBase section)
    {
        if (section == null)
        {
            Debug.LogError($"[MainPageManager] Cannot register null section: {sectionType}");
            return;
        }
        
        if (_sections.ContainsKey(sectionType))
        {
            Debug.LogWarning($"[MainPageManager] Section already registered, replacing: {sectionType}");
        }
        
        _sections[sectionType] = section;
        section.Initialize(this);
        
        Debug.Log($"[MainPageManager] Section registered: {sectionType}");
    }
    
    /// <summary>
    /// 섹션 등록 해제
    /// </summary>
    public void UnregisterSection(MainPageSectionType sectionType)
    {
        if (_sections.TryGetValue(sectionType, out SectionBase section))
        {
            section.Cleanup();
            _sections.Remove(sectionType);
            Debug.Log($"[MainPageManager] Section unregistered: {sectionType}");
        }
    }
    
    /// <summary>
    /// 섹션 가져오기
    /// </summary>
    public T GetSection<T>(MainPageSectionType sectionType) where T : SectionBase
    {
        if (_sections.TryGetValue(sectionType, out SectionBase section))
        {
            return section as T;
        }
        return null;
    }
    
    /// <summary>
    /// 섹션 활성화 상태 설정
    /// </summary>
    public void SetSectionActive(MainPageSectionType sectionType, bool active)
    {
        if (_sectionStates.ContainsKey(sectionType))
        {
            _sectionStates[sectionType] = active;
            
            if (_sections.TryGetValue(sectionType, out SectionBase section))
            {
                if (active)
                    section.Activate();
                else
                    section.Deactivate();
            }
            
            OnSectionStateChanged?.Invoke(sectionType, active);
            Debug.Log($"[MainPageManager] Section {sectionType} {(active ? "activated" : "deactivated")}");
        }
    }
    
    /// <summary>
    /// 섹션 활성화 상태 확인
    /// </summary>
    public bool IsSectionActive(MainPageSectionType sectionType)
    {
        return _sectionStates.TryGetValue(sectionType, out bool active) && active;
    }
    
    /// <summary>
    /// 모든 섹션 활성화
    /// </summary>
    public void ActivateAllSections()
    {
        StartCoroutine(ActivateAllSectionsCoroutine());
    }
    
    /// <summary>
    /// 모든 섹션 비활성화
    /// </summary>
    public void DeactivateAllSections()
    {
        foreach (var sectionType in _sectionStates.Keys)
        {
            SetSectionActive(sectionType, false);
        }
    }
    
    /// <summary>
    /// 순차적 섹션 활성화 코루틴
    /// </summary>
    private IEnumerator ActivateAllSectionsCoroutine()
    {
        var sectionOrder = new[]
        {
            MainPageSectionType.Profile,
            MainPageSectionType.Energy,
            MainPageSectionType.Matching,
            MainPageSectionType.Settings
        };
        
        foreach (var sectionType in sectionOrder)
        {
            SetSectionActive(sectionType, true);
            yield return new WaitForSeconds(SECTION_ACTIVATION_DELAY);
        }
        
        Debug.Log("[MainPageManager] All sections activated");
    }
    #endregion

    #region Data Management
    /// <summary>
    /// 데이터 갱신 시작
    /// </summary>
    private void StartDataRefresh()
    {
        if (_dataRefreshCoroutine != null)
            StopCoroutine(_dataRefreshCoroutine);
        
        _dataRefreshCoroutine = StartCoroutine(DataRefreshCoroutine());
    }
    
    /// <summary>
    /// 데이터 갱신 중지
    /// </summary>
    private void StopDataRefresh()
    {
        if (_dataRefreshCoroutine != null)
        {
            StopCoroutine(_dataRefreshCoroutine);
            _dataRefreshCoroutine = null;
        }
    }
    
    /// <summary>
    /// 주기적 데이터 갱신 코루틴
    /// </summary>
    private IEnumerator DataRefreshCoroutine()
    {
        while (_isInitialized)
        {
            yield return new WaitForSeconds(DATA_REFRESH_INTERVAL);
            
            if (!_isRefreshingData && _userDataManager != null)
            {
                RefreshUserData();
            }
        }
    }
    
    /// <summary>
    /// 사용자 데이터 갱신
    /// </summary>
    public void RefreshUserData()
    {
        if (_isRefreshingData)
        {
            Debug.LogWarning("[MainPageManager] Data refresh already in progress");
            return;
        }
        
        if (_userDataManager?.CurrentUser == null)
        {
            Debug.LogWarning("[MainPageManager] No current user data to refresh");
            return;
        }
        
        _isRefreshingData = true;
        
        try
        {
            // 서버와 동기화 시도
            _userDataManager.SyncWithServer();
            
            var currentUser = _userDataManager.CurrentUser;
            OnUserDataRefreshed?.Invoke(currentUser);
            NotifySectionsOfDataUpdate(currentUser);
            
            Debug.Log($"[MainPageManager] User data refreshed: {currentUser.DisplayName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainPageManager] Failed to refresh user data: {e.Message}");
        }
        finally
        {
            _isRefreshingData = false;
        }
    }
    #endregion

    #region Section Communication
    /// <summary>
    /// 섹션 간 통신 중계
    /// </summary>
    public void SendMessageToSection(MainPageSectionType fromSection, MainPageSectionType toSection, object data)
    {
        if (!_sections.ContainsKey(toSection))
        {
            Debug.LogWarning($"[MainPageManager] Target section not found: {toSection}");
            return;
        }
        
        if (!IsSectionActive(toSection))
        {
            Debug.LogWarning($"[MainPageManager] Target section not active: {toSection}");
            return;
        }
        
        try
        {
            _sections[toSection].ReceiveMessage(fromSection, data);
            OnSectionCommunication?.Invoke(fromSection, toSection, data);
            
            Debug.Log($"[MainPageManager] Message sent: {fromSection} -> {toSection}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainPageManager] Failed to send message to section {toSection}: {e.Message}");
        }
    }
    
    /// <summary>
    /// 모든 섹션에 브로드캐스트
    /// </summary>
    public void BroadcastToAllSections(MainPageSectionType fromSection, object data)
    {
        foreach (var kvp in _sections)
        {
            if (kvp.Key != fromSection && IsSectionActive(kvp.Key))
            {
                try
                {
                    kvp.Value.ReceiveMessage(fromSection, data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MainPageManager] Failed to broadcast to section {kvp.Key}: {e.Message}");
                }
            }
        }
        
        Debug.Log($"[MainPageManager] Broadcast from {fromSection} to all sections");
    }
    
    /// <summary>
    /// 섹션들에게 데이터 업데이트 알림
    /// </summary>
    private void NotifySectionsOfDataUpdate(UserData userData)
    {
        foreach (var kvp in _sections)
        {
            if (IsSectionActive(kvp.Key))
            {
                try
                {
                    kvp.Value.OnUserDataUpdated(userData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MainPageManager] Failed to notify section {kvp.Key} of data update: {e.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 섹션들에게 설정 변경 알림
    /// </summary>
    private void NotifySectionsOfSettingChange(string settingName, object newValue)
    {
        foreach (var kvp in _sections)
        {
            if (IsSectionActive(kvp.Key))
            {
                try
                {
                    kvp.Value.OnSettingChanged(settingName, newValue);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MainPageManager] Failed to notify section {kvp.Key} of setting change: {e.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 섹션들에게 모드 변경 알림
    /// </summary>
    private void NotifySectionsOfModeChange(bool isOfflineMode)
    {
        foreach (var kvp in _sections)
        {
            if (IsSectionActive(kvp.Key))
            {
                try
                {
                    kvp.Value.OnModeChanged(isOfflineMode);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MainPageManager] Failed to notify section {kvp.Key} of mode change: {e.Message}");
                }
            }
        }
    }
    #endregion

    #region Manager Integration
    /// <summary>
    /// 로그아웃 처리
    /// </summary>
    public void Logout()
    {
        if (_authenticationManager == null)
        {
            Debug.LogError("[MainPageManager] AuthenticationManager not available");
            return;
        }
        
        Debug.Log("[MainPageManager] Initiating logout...");
        
        // 모든 섹션 비활성화
        DeactivateAllSections();
        
        // 사용자 데이터 저장
        if (_userDataManager?.CurrentUser != null)
        {
            _userDataManager.LogoutCurrentUser();
        }
        
        // 인증 매니저를 통한 로그아웃
        _authenticationManager.Logout();
    }
    
    /// <summary>
    /// 로그인 화면으로 전환
    /// </summary>
    private void TransitionToLoginScreen()
    {
        try
        {
            // 데이터 갱신 중지
            StopDataRefresh();
            
            // 섹션들 정리
            DeactivateAllSections();
            
            // 화면 전환
            if (_screenTransitionManager != null)
            {
                _screenTransitionManager.ShowScreen(ScreenType.Login);
            }
            
            Debug.Log("[MainPageManager] Transitioned to login screen");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainPageManager] Failed to transition to login screen: {e.Message}");
        }
    }
    
    /// <summary>
    /// 설정 값 가져오기
    /// </summary>
    public T GetSetting<T>(string settingName)
    {
        if (_settingsManager != null)
        {
            return _settingsManager.GetSetting<T>(settingName);
        }
        return default(T);
    }
    
    /// <summary>
    /// 설정 값 설정
    /// </summary>
    public bool SetSetting<T>(string settingName, T value)
    {
        if (_settingsManager != null)
        {
            return _settingsManager.SetSetting(settingName, value);
        }
        return false;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 메인 페이지 활성화
    /// </summary>
    public void ActivateMainPage()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[MainPageManager] Cannot activate - not initialized");
            return;
        }
        
        Debug.Log("[MainPageManager] Activating main page...");
        ActivateAllSections();
        StartDataRefresh();
    }
    
    /// <summary>
    /// 메인 페이지 비활성화
    /// </summary>
    public void DeactivateMainPage()
    {
        Debug.Log("[MainPageManager] Deactivating main page...");
        DeactivateAllSections();
        StopDataRefresh();
    }
    
    /// <summary>
    /// 현재 상태 정보 반환
    /// </summary>
    public MainPageManagerStatus GetStatus()
    {
        return new MainPageManagerStatus
        {
            IsInitialized = _isInitialized,
            IsRefreshingData = _isRefreshingData,
            ActiveSectionCount = ActiveSectionCount,
            RegisteredSectionCount = _sections.Count,
            IsAuthenticated = IsAuthenticated,
            HasCurrentUser = CurrentUserData != null,
            IsOfflineMode = _userDataManager?.IsOfflineMode ?? false
        };
    }
    
    /// <summary>
    /// 강제 새로고침
    /// </summary>
    public void ForceRefresh()
    {
        Debug.Log("[MainPageManager] Force refresh triggered");
        RefreshUserData();
        
        // 모든 섹션에 강제 새로고침 알림
        foreach (var kvp in _sections)
        {
            if (IsSectionActive(kvp.Key))
            {
                try
                {
                    kvp.Value.ForceRefresh();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MainPageManager] Failed to force refresh section {kvp.Key}: {e.Message}");
                }
            }
        }
    }
    #endregion
}

#region Enums
/// <summary>
/// 메인 페이지 섹션 타입
/// </summary>
public enum MainPageSectionType
{
    Profile,    // 프로필 섹션 (25% width)
    Energy,     // 피로도 섹션 (25% width)
    Matching,   // 매칭 섹션 (50% width)
    Settings    // 설정 섹션 (Footer)
}
#endregion

#region Data Classes
/// <summary>
/// 메인 페이지 매니저 상태 정보
/// </summary>
[Serializable]
public class MainPageManagerStatus
{
    public bool IsInitialized;
    public bool IsRefreshingData;
    public int ActiveSectionCount;
    public int RegisteredSectionCount;
    public bool IsAuthenticated;
    public bool HasCurrentUser;
    public bool IsOfflineMode;
}
#endregion