using System;
using UnityEngine;

/// <summary>
/// 메인 페이지 섹션의 추상 기본 클래스
/// 모든 섹션이 공통으로 구현해야 하는 인터페이스와 기본 기능을 제공합니다.
/// 초기화/해제 라이프사이클과 기존 매니저 참조 접근을 관리합니다.
/// </summary>
public abstract class SectionBase : MonoBehaviour, IMainPageSection
{
    #region Events
    /// <summary>
    /// 섹션이 초기화될 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType> OnSectionInitialized;
    
    /// <summary>
    /// 섹션이 활성화될 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType> OnSectionActivated;
    
    /// <summary>
    /// 섹션이 비활성화될 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType> OnSectionDeactivated;
    
    /// <summary>
    /// 섹션에서 오류가 발생할 때 발생하는 이벤트
    /// </summary>
    public static event Action<MainPageSectionType, string> OnSectionError;
    #endregion

    #region Protected Fields
    protected MainPageManager _mainPageManager;
    protected UserDataManager _userDataManager;
    protected AuthenticationManager _authenticationManager;
    protected SettingsManager _settingsManager;
    protected ScreenTransitionManager _screenTransitionManager;
    
    protected bool _isInitialized = false;
    protected bool _isActive = false;
    protected bool _isRefreshing = false;
    
    // 캐시된 데이터
    protected UserData _cachedUserData;
    protected bool _isOfflineMode = false;
    
    // 설정
    protected readonly float UPDATE_THROTTLE_INTERVAL = 0.1f; // UI 업데이트 스로틀링
    protected DateTime _lastUpdateTime = DateTime.MinValue;
    #endregion

    #region Abstract Properties
    /// <summary>
    /// 섹션 타입 (하위 클래스에서 반드시 구현)
    /// </summary>
    public abstract MainPageSectionType SectionType { get; }
    
    /// <summary>
    /// 섹션 표시 이름 (하위 클래스에서 반드시 구현)
    /// </summary>
    public abstract string SectionDisplayName { get; }
    #endregion

    #region Public Properties
    /// <summary>
    /// 섹션 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 섹션 활성화 상태
    /// </summary>
    public bool IsActive => _isActive;
    
    /// <summary>
    /// 섹션 갱신 진행 중 여부
    /// </summary>
    public bool IsRefreshing => _isRefreshing;
    
    /// <summary>
    /// 캐시된 사용자 데이터
    /// </summary>
    public UserData CachedUserData => _cachedUserData;
    
    /// <summary>
    /// 오프라인 모드 상태
    /// </summary>
    public bool IsOfflineMode => _isOfflineMode;
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        ValidateComponents();
    }

    protected virtual void Start()
    {
        // 하위 클래스에서 필요시 오버라이드
    }

    protected virtual void OnDestroy()
    {
        Cleanup();
    }

    protected virtual void OnEnable()
    {
        if (_isInitialized && !_isActive)
        {
            Activate();
        }
    }

    protected virtual void OnDisable()
    {
        if (_isActive)
        {
            Deactivate();
        }
    }
    #endregion

    #region IMainPageSection Implementation
    /// <summary>
    /// 섹션 초기화
    /// </summary>
    public virtual void Initialize(MainPageManager mainPageManager)
    {
        if (_isInitialized)
        {
            Debug.LogWarning($"[{SectionType}Section] Already initialized");
            return;
        }
        
        try
        {
            _mainPageManager = mainPageManager;
            
            // 매니저 참조 설정
            SetupManagerReferences();
            
            // 하위 클래스 초기화
            OnInitialize();
            
            _isInitialized = true;
            OnSectionInitialized?.Invoke(SectionType);
            
            Debug.Log($"[{SectionType}Section] Initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Initialization failed: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"Initialization failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 섹션 활성화
    /// </summary>
    public virtual void Activate()
    {
        if (!_isInitialized)
        {
            Debug.LogError($"[{SectionType}Section] Cannot activate - not initialized");
            return;
        }
        
        if (_isActive)
        {
            Debug.LogWarning($"[{SectionType}Section] Already active");
            return;
        }
        
        try
        {
            _isActive = true;
            gameObject.SetActive(true);
            
            // 하위 클래스 활성화
            OnActivate();
            
            // 현재 데이터로 UI 업데이트
            if (_cachedUserData != null)
            {
                UpdateUI(_cachedUserData);
            }
            
            OnSectionActivated?.Invoke(SectionType);
            Debug.Log($"[{SectionType}Section] Activated");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Activation failed: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"Activation failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 섹션 비활성화
    /// </summary>
    public virtual void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        
        try
        {
            _isActive = false;
            
            // 하위 클래스 비활성화
            OnDeactivate();
            
            gameObject.SetActive(false);
            
            OnSectionDeactivated?.Invoke(SectionType);
            Debug.Log($"[{SectionType}Section] Deactivated");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Deactivation failed: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"Deactivation failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 섹션 정리
    /// </summary>
    public virtual void Cleanup()
    {
        try
        {
            if (_isActive)
            {
                Deactivate();
            }
            
            // 하위 클래스 정리
            OnCleanup();
            
            _isInitialized = false;
            Debug.Log($"[{SectionType}Section] Cleaned up");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Cleanup failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 섹션 간 메시지 수신
    /// </summary>
    public virtual void ReceiveMessage(MainPageSectionType fromSection, object data)
    {
        try
        {
            OnReceiveMessage(fromSection, data);
            Debug.Log($"[{SectionType}Section] Received message from {fromSection}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Failed to process message from {fromSection}: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"Message processing failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 사용자 데이터 업데이트 처리
    /// </summary>
    public virtual void OnUserDataUpdated(UserData userData)
    {
        if (userData == null)
        {
            Debug.LogWarning($"[{SectionType}Section] Received null user data");
            return;
        }
        
        _cachedUserData = userData;
        
        // UI 업데이트 스로틀링
        if (ShouldUpdateUI())
        {
            try
            {
                UpdateUI(userData);
                _lastUpdateTime = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{SectionType}Section] UI update failed: {e.Message}");
                OnSectionError?.Invoke(SectionType, $"UI update failed: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// 설정 변경 처리
    /// </summary>
    public virtual void OnSettingChanged(string settingName, object newValue)
    {
        try
        {
            OnSettingUpdated(settingName, newValue);
            Debug.Log($"[{SectionType}Section] Setting updated: {settingName} = {newValue}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Setting update failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 모드 변경 처리 (온라인/오프라인)
    /// </summary>
    public virtual void OnModeChanged(bool isOfflineMode)
    {
        _isOfflineMode = isOfflineMode;
        
        try
        {
            OnOfflineModeChanged(isOfflineMode);
            Debug.Log($"[{SectionType}Section] Mode changed to {(isOfflineMode ? "offline" : "online")}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Mode change handling failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 강제 새로고침
    /// </summary>
    public virtual void ForceRefresh()
    {
        if (!_isActive)
        {
            Debug.LogWarning($"[{SectionType}Section] Cannot refresh - section not active");
            return;
        }
        
        _isRefreshing = true;
        
        try
        {
            // 현재 데이터로 UI 업데이트
            if (_cachedUserData != null)
            {
                UpdateUI(_cachedUserData);
            }
            
            // 하위 클래스 새로고침
            OnForceRefresh();
            
            Debug.Log($"[{SectionType}Section] Force refresh completed");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Force refresh failed: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"Force refresh failed: {e.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }
    #endregion

    #region Abstract Methods
    /// <summary>
    /// 하위 클래스별 초기화 로직 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void OnInitialize();
    
    /// <summary>
    /// 하위 클래스별 활성화 로직 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void OnActivate();
    
    /// <summary>
    /// 하위 클래스별 비활성화 로직 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void OnDeactivate();
    
    /// <summary>
    /// 하위 클래스별 정리 로직 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void OnCleanup();
    
    /// <summary>
    /// UI 업데이트 로직 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void UpdateUI(UserData userData);
    
    /// <summary>
    /// 컴포넌트 유효성 검사 (하위 클래스에서 구현)
    /// </summary>
    protected abstract void ValidateComponents();
    #endregion

    #region Virtual Methods
    /// <summary>
    /// 섹션 간 메시지 처리 (하위 클래스에서 필요시 오버라이드)
    /// </summary>
    protected virtual void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        // 기본 구현은 아무것도 하지 않음
        Debug.Log($"[{SectionType}Section] Received message from {fromSection}, but no handler implemented");
    }
    
    /// <summary>
    /// 설정 업데이트 처리 (하위 클래스에서 필요시 오버라이드)
    /// </summary>
    protected virtual void OnSettingUpdated(string settingName, object newValue)
    {
        // 기본 구현은 아무것도 하지 않음
        Debug.Log($"[{SectionType}Section] Setting {settingName} updated to {newValue}, but no handler implemented");
    }
    
    /// <summary>
    /// 오프라인 모드 변경 처리 (하위 클래스에서 필요시 오버라이드)
    /// </summary>
    protected virtual void OnOfflineModeChanged(bool isOfflineMode)
    {
        // 기본 구현은 아무것도 하지 않음
        Debug.Log($"[{SectionType}Section] Offline mode changed to {isOfflineMode}, but no handler implemented");
    }
    
    /// <summary>
    /// 강제 새로고침 처리 (하위 클래스에서 필요시 오버라이드)
    /// </summary>
    protected virtual void OnForceRefresh()
    {
        // 기본 구현은 아무것도 하지 않음
        Debug.Log($"[{SectionType}Section] Force refresh requested, but no handler implemented");
    }
    #endregion

    #region Protected Helper Methods
    /// <summary>
    /// 매니저 참조 설정
    /// </summary>
    protected virtual void SetupManagerReferences()
    {
        _userDataManager = UserDataManager.Instance;
        _authenticationManager = AuthenticationManager.Instance;
        _settingsManager = SettingsManager.Instance;
        _screenTransitionManager = ScreenTransitionManager.Instance;
        
        if (_userDataManager == null)
            Debug.LogWarning($"[{SectionType}Section] UserDataManager not available");
        
        if (_authenticationManager == null)
            Debug.LogWarning($"[{SectionType}Section] AuthenticationManager not available");
        
        if (_settingsManager == null)
            Debug.LogWarning($"[{SectionType}Section] SettingsManager not available");
        
        if (_screenTransitionManager == null)
            Debug.LogWarning($"[{SectionType}Section] ScreenTransitionManager not available");
    }
    
    /// <summary>
    /// UI 업데이트 스로틀링 확인
    /// </summary>
    protected bool ShouldUpdateUI()
    {
        var timeSinceLastUpdate = DateTime.Now - _lastUpdateTime;
        return timeSinceLastUpdate.TotalSeconds >= UPDATE_THROTTLE_INTERVAL;
    }
    
    /// <summary>
    /// 다른 섹션에 메시지 전송
    /// </summary>
    protected void SendMessageToSection(MainPageSectionType targetSection, object data)
    {
        if (_mainPageManager != null)
        {
            _mainPageManager.SendMessageToSection(SectionType, targetSection, data);
        }
        else
        {
            Debug.LogError($"[{SectionType}Section] Cannot send message - MainPageManager not available");
        }
    }
    
    /// <summary>
    /// 모든 섹션에 브로드캐스트
    /// </summary>
    protected void BroadcastToAllSections(object data)
    {
        if (_mainPageManager != null)
        {
            _mainPageManager.BroadcastToAllSections(SectionType, data);
        }
        else
        {
            Debug.LogError($"[{SectionType}Section] Cannot broadcast - MainPageManager not available");
        }
    }
    
    /// <summary>
    /// 설정 값 가져오기
    /// </summary>
    protected T GetSetting<T>(string settingName)
    {
        if (_settingsManager != null)
        {
            return _settingsManager.GetSetting<T>(settingName);
        }
        
        if (_mainPageManager != null)
        {
            return _mainPageManager.GetSetting<T>(settingName);
        }
        
        Debug.LogWarning($"[{SectionType}Section] Cannot get setting {settingName} - no settings manager available");
        return default(T);
    }
    
    /// <summary>
    /// 설정 값 설정
    /// </summary>
    protected bool SetSetting<T>(string settingName, T value)
    {
        if (_settingsManager != null)
        {
            return _settingsManager.SetSetting(settingName, value);
        }
        
        if (_mainPageManager != null)
        {
            return _mainPageManager.SetSetting(settingName, value);
        }
        
        Debug.LogWarning($"[{SectionType}Section] Cannot set setting {settingName} - no settings manager available");
        return false;
    }
    
    /// <summary>
    /// 로그아웃 요청
    /// </summary>
    protected void RequestLogout()
    {
        if (_mainPageManager != null)
        {
            _mainPageManager.Logout();
        }
        else if (_authenticationManager != null)
        {
            _authenticationManager.Logout();
        }
        else
        {
            Debug.LogError($"[{SectionType}Section] Cannot logout - no authentication manager available");
        }
    }
    
    /// <summary>
    /// 화면 전환 요청
    /// </summary>
    protected void RequestScreenTransition(ScreenType targetScreen)
    {
        if (_screenTransitionManager != null)
        {
            _screenTransitionManager.ShowScreen(targetScreen);
        }
        else
        {
            Debug.LogError($"[{SectionType}Section] Cannot transition to {targetScreen} - no screen manager available");
        }
    }
    
    /// <summary>
    /// 오류 보고
    /// </summary>
    protected void ReportError(string errorMessage)
    {
        Debug.LogError($"[{SectionType}Section] {errorMessage}");
        OnSectionError?.Invoke(SectionType, errorMessage);
    }
    
    /// <summary>
    /// 안전한 UI 업데이트 실행
    /// </summary>
    protected void SafeUIUpdate(Action updateAction, string operationName = "UI Update")
    {
        if (!_isActive)
        {
            return;
        }
        
        try
        {
            updateAction?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] {operationName} failed: {e.Message}");
            OnSectionError?.Invoke(SectionType, $"{operationName} failed: {e.Message}");
        }
    }
    #endregion

    #region Public Utility Methods
    /// <summary>
    /// 섹션 상태 정보 반환
    /// </summary>
    public virtual SectionStatus GetSectionStatus()
    {
        return new SectionStatus
        {
            SectionType = SectionType,
            DisplayName = SectionDisplayName,
            IsInitialized = _isInitialized,
            IsActive = _isActive,
            IsRefreshing = _isRefreshing,
            IsOfflineMode = _isOfflineMode,
            HasCachedData = _cachedUserData != null,
            LastUpdateTime = _lastUpdateTime
        };
    }
    
    /// <summary>
    /// 섹션 성능 정보 반환
    /// </summary>
    public virtual SectionPerformanceInfo GetPerformanceInfo()
    {
        return new SectionPerformanceInfo
        {
            SectionType = SectionType,
            LastUpdateDuration = 0f, // 하위 클래스에서 측정 구현
            UpdateCount = 0, // 하위 클래스에서 카운팅 구현
            ErrorCount = 0, // 하위 클래스에서 카운팅 구현
            MemoryUsage = GC.GetTotalMemory(false) // 대략적인 메모리 사용량
        };
    }
    #endregion
}

#region Interfaces
/// <summary>
/// 메인 페이지 섹션 인터페이스
/// </summary>
public interface IMainPageSection
{
    MainPageSectionType SectionType { get; }
    string SectionDisplayName { get; }
    bool IsInitialized { get; }
    bool IsActive { get; }
    bool IsRefreshing { get; }
    
    void Initialize(MainPageManager mainPageManager);
    void Activate();
    void Deactivate();
    void Cleanup();
    void ReceiveMessage(MainPageSectionType fromSection, object data);
    void OnUserDataUpdated(UserData userData);
    void OnSettingChanged(string settingName, object newValue);
    void OnModeChanged(bool isOfflineMode);
    void ForceRefresh();
    
    SectionStatus GetSectionStatus();
    SectionPerformanceInfo GetPerformanceInfo();
}
#endregion

#region Data Classes
/// <summary>
/// 섹션 상태 정보
/// </summary>
[Serializable]
public class SectionStatus
{
    public MainPageSectionType SectionType;
    public string DisplayName;
    public bool IsInitialized;
    public bool IsActive;
    public bool IsRefreshing;
    public bool IsOfflineMode;
    public bool HasCachedData;
    public DateTime LastUpdateTime;
}

/// <summary>
/// 섹션 성능 정보
/// </summary>
[Serializable]
public class SectionPerformanceInfo
{
    public MainPageSectionType SectionType;
    public float LastUpdateDuration;
    public int UpdateCount;
    public int ErrorCount;
    public long MemoryUsage;
}
#endregion