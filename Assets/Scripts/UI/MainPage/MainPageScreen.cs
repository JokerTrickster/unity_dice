using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 페이지 화면 컨트롤러
/// UI 컨트롤러 역할을 수행하며 섹션별 UI 참조 관리와 화면 전환 로직을 담당합니다.
/// MainPageManager와 연동하여 전체 화면의 라이프사이클을 관리합니다.
/// </summary>
public class MainPageScreen : MonoBehaviour
{
    #region UI References
    [Header("Header Section")]
    [SerializeField] private GameObject headerSection;
    [SerializeField] private Image gameLogoImage;
    [SerializeField] private Text userProfileText;
    [SerializeField] private Button userProfileButton;
    [SerializeField] private Button logoutButton;
    
    [Header("Content Area")]
    [SerializeField] private GameObject contentArea;
    [SerializeField] private RectTransform profileSectionContainer;
    [SerializeField] private RectTransform energySectionContainer;
    [SerializeField] private RectTransform matchingSectionContainer;
    [SerializeField] private RectTransform settingsSectionContainer;
    
    [Header("Section Prefabs")]
    [SerializeField] private GameObject profileSectionPrefab;
    [SerializeField] private GameObject energySectionPrefab;
    [SerializeField] private GameObject matchingSectionPrefab;
    [SerializeField] private GameObject settingsSectionPrefab;
    
    [Header("Navigation")]
    [SerializeField] private GameObject navigationBar;
    [SerializeField] private Button[] navigationButtons;
    
    [Header("Layout Settings")]
    [SerializeField] private bool enableResponsiveLayout = true;
    [SerializeField] private float minButtonSize = 44f; // 터치 친화적 최소 크기
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableSectionAnimations = true;
    [SerializeField] private float sectionTransitionDuration = 0.3f;
    [SerializeField] private AnimationCurve sectionTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Private Fields
    private MainPageManager _mainPageManager;
    private readonly Dictionary<MainPageSectionType, GameObject> _sectionInstances = new();
    private readonly Dictionary<MainPageSectionType, SectionBase> _sectionComponents = new();
    private readonly Dictionary<MainPageSectionType, RectTransform> _sectionContainers = new();
    
    private bool _isInitialized = false;
    private bool _isLayoutSetup = false;
    private Coroutine _initializationCoroutine;
    private CanvasScaler _canvasScaler;
    
    // 반응형 레이아웃 상태
    private Vector2 _currentScreenSize;
    private bool _isPortraitMode = false;
    #endregion

    #region Properties
    /// <summary>
    /// 화면 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 레이아웃 설정 완료 상태
    /// </summary>
    public bool IsLayoutSetup => _isLayoutSetup;
    
    /// <summary>
    /// 현재 화면 방향
    /// </summary>
    public bool IsPortraitMode => _isPortraitMode;
    
    /// <summary>
    /// 인스턴스화된 섹션 수
    /// </summary>
    public int InstantiatedSectionCount => _sectionInstances.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        SetupContainerReferences();
        SetupCanvasScaler();
    }
    
    private void Start()
    {
        _initializationCoroutine = StartCoroutine(InitializeScreen());
    }
    
    private void OnDestroy()
    {
        if (_initializationCoroutine != null)
        {
            StopCoroutine(_initializationCoroutine);
        }
        
        UnsubscribeFromEvents();
        CleanupSections();
    }
    
    private void Update()
    {
        if (_isInitialized && enableResponsiveLayout)
        {
            CheckScreenSizeChanges();
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 화면 초기화 코루틴
    /// </summary>
    private IEnumerator InitializeScreen()
    {
        Debug.Log("[MainPageScreen] Initializing screen...");
        
        // MainPageManager 초기화 대기
        yield return WaitForMainPageManager();
        
        // UI 이벤트 설정
        SetupUIEvents();
        
        // 외부 이벤트 구독
        SubscribeToEvents();
        
        // 레이아웃 설정
        SetupLayout();
        
        // 섹션 인스턴스화
        yield return InstantiateSections();
        
        // 초기 사용자 데이터 로드
        LoadInitialUserData();
        
        _isInitialized = true;
        Debug.Log("[MainPageScreen] Screen initialization complete");
    }
    
    /// <summary>
    /// MainPageManager 초기화 대기
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
                Debug.Log("[MainPageScreen] MainPageManager ready");
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Debug.LogError("[MainPageScreen] MainPageManager initialization timeout");
    }
    
    /// <summary>
    /// 컨테이너 참조 설정
    /// </summary>
    private void SetupContainerReferences()
    {
        _sectionContainers[MainPageSectionType.Profile] = profileSectionContainer;
        _sectionContainers[MainPageSectionType.Energy] = energySectionContainer;
        _sectionContainers[MainPageSectionType.Matching] = matchingSectionContainer;
        _sectionContainers[MainPageSectionType.Settings] = settingsSectionContainer;
    }
    
    /// <summary>
    /// CanvasScaler 설정
    /// </summary>
    private void SetupCanvasScaler()
    {
        _canvasScaler = GetComponentInParent<CanvasScaler>();
        if (_canvasScaler != null)
        {
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = referenceResolution;
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _canvasScaler.matchWidthOrHeight = 0.5f;
        }
    }
    
    /// <summary>
    /// UI 이벤트 설정
    /// </summary>
    private void SetupUIEvents()
    {
        if (userProfileButton != null)
            userProfileButton.onClick.AddListener(OnUserProfileClicked);
        
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);
        
        // 네비게이션 버튼 이벤트 설정
        for (int i = 0; i < navigationButtons.Length; i++)
        {
            if (navigationButtons[i] != null)
            {
                int index = i; // 클로저 캡처 방지
                navigationButtons[i].onClick.AddListener(() => OnNavigationClicked(index));
            }
        }
    }
    #endregion

    #region Layout Management
    /// <summary>
    /// 레이아웃 설정
    /// </summary>
    private void SetupLayout()
    {
        ApplyResponsiveLayout();
        ApplyTouchFriendlySettings();
        _isLayoutSetup = true;
        
        Debug.Log("[MainPageScreen] Layout setup complete");
    }
    
    /// <summary>
    /// 반응형 레이아웃 적용
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        UpdateScreenOrientationInfo();
        
        if (_isPortraitMode)
        {
            ApplyPortraitLayout();
        }
        else
        {
            ApplyLandscapeLayout();
        }
    }
    
    /// <summary>
    /// 화면 방향 정보 업데이트
    /// </summary>
    private void UpdateScreenOrientationInfo()
    {
        _currentScreenSize = new Vector2(Screen.width, Screen.height);
        _isPortraitMode = Screen.height > Screen.width;
    }
    
    /// <summary>
    /// 세로 모드 레이아웃 적용
    /// </summary>
    private void ApplyPortraitLayout()
    {
        // 세로 모드에서는 섹션을 세로로 배치
        if (profileSectionContainer != null)
        {
            profileSectionContainer.anchorMin = new Vector2(0, 0.75f);
            profileSectionContainer.anchorMax = new Vector2(1, 1);
        }
        
        if (energySectionContainer != null)
        {
            energySectionContainer.anchorMin = new Vector2(0, 0.5f);
            energySectionContainer.anchorMax = new Vector2(1, 0.75f);
        }
        
        if (matchingSectionContainer != null)
        {
            matchingSectionContainer.anchorMin = new Vector2(0, 0.2f);
            matchingSectionContainer.anchorMax = new Vector2(1, 0.5f);
        }
        
        if (settingsSectionContainer != null)
        {
            settingsSectionContainer.anchorMin = new Vector2(0, 0);
            settingsSectionContainer.anchorMax = new Vector2(1, 0.2f);
        }
        
        Debug.Log("[MainPageScreen] Portrait layout applied");
    }
    
    /// <summary>
    /// 가로 모드 레이아웃 적용 (태스크 요구사항에 따른 기본 레이아웃)
    /// </summary>
    private void ApplyLandscapeLayout()
    {
        // 가로 모드에서는 프로필(25%) | 피로도(25%) | 매칭(50%) 배치
        if (profileSectionContainer != null)
        {
            profileSectionContainer.anchorMin = new Vector2(0, 0.2f);
            profileSectionContainer.anchorMax = new Vector2(0.25f, 1);
        }
        
        if (energySectionContainer != null)
        {
            energySectionContainer.anchorMin = new Vector2(0.25f, 0.2f);
            energySectionContainer.anchorMax = new Vector2(0.5f, 1);
        }
        
        if (matchingSectionContainer != null)
        {
            matchingSectionContainer.anchorMin = new Vector2(0.5f, 0.2f);
            matchingSectionContainer.anchorMax = new Vector2(1, 1);
        }
        
        if (settingsSectionContainer != null)
        {
            settingsSectionContainer.anchorMin = new Vector2(0, 0);
            settingsSectionContainer.anchorMax = new Vector2(1, 0.2f);
        }
        
        Debug.Log("[MainPageScreen] Landscape layout applied");
    }
    
    /// <summary>
    /// 터치 친화적 설정 적용
    /// </summary>
    private void ApplyTouchFriendlySettings()
    {
        // 모든 버튼에 최소 크기 적용
        ApplyMinButtonSize(userProfileButton);
        ApplyMinButtonSize(logoutButton);
        
        foreach (var button in navigationButtons)
        {
            ApplyMinButtonSize(button);
        }
        
        Debug.Log("[MainPageScreen] Touch-friendly settings applied");
    }
    
    /// <summary>
    /// 버튼 최소 크기 적용
    /// </summary>
    private void ApplyMinButtonSize(Button button)
    {
        if (button == null) return;
        
        var rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            var sizeDelta = rectTransform.sizeDelta;
            sizeDelta.x = Mathf.Max(sizeDelta.x, minButtonSize);
            sizeDelta.y = Mathf.Max(sizeDelta.y, minButtonSize);
            rectTransform.sizeDelta = sizeDelta;
        }
    }
    
    /// <summary>
    /// 화면 크기 변경 감지
    /// </summary>
    private void CheckScreenSizeChanges()
    {
        var newScreenSize = new Vector2(Screen.width, Screen.height);
        bool newOrientationMode = Screen.height > Screen.width;
        
        if (newScreenSize != _currentScreenSize || newOrientationMode != _isPortraitMode)
        {
            _currentScreenSize = newScreenSize;
            _isPortraitMode = newOrientationMode;
            
            Debug.Log($"[MainPageScreen] Screen size changed: {_currentScreenSize}, Portrait: {_isPortraitMode}");
            ApplyResponsiveLayout();
        }
    }
    #endregion

    #region Section Management
    /// <summary>
    /// 섹션 인스턴스화
    /// </summary>
    private IEnumerator InstantiateSections()
    {
        Debug.Log("[MainPageScreen] Instantiating sections...");
        
        // 섹션별 프리팹과 컨테이너 매핑
        var sectionMappings = new Dictionary<MainPageSectionType, (GameObject prefab, RectTransform container)>
        {
            { MainPageSectionType.Profile, (profileSectionPrefab, profileSectionContainer) },
            { MainPageSectionType.Energy, (energySectionPrefab, energySectionContainer) },
            { MainPageSectionType.Matching, (matchingSectionPrefab, matchingSectionContainer) },
            { MainPageSectionType.Settings, (settingsSectionPrefab, settingsSectionContainer) }
        };
        
        foreach (var kvp in sectionMappings)
        {
            yield return InstantiateSection(kvp.Key, kvp.Value.prefab, kvp.Value.container);
        }
        
        Debug.Log("[MainPageScreen] Section instantiation complete");
    }
    
    /// <summary>
    /// 개별 섹션 인스턴스화
    /// </summary>
    private IEnumerator InstantiateSection(MainPageSectionType sectionType, GameObject prefab, RectTransform container)
    {
        if (prefab == null || container == null)
        {
            Debug.LogWarning($"[MainPageScreen] Cannot instantiate section {sectionType} - missing prefab or container");
            yield break;
        }
        
        try
        {
            // 프리팹 인스턴스화
            GameObject sectionInstance = Instantiate(prefab, container);
            sectionInstance.name = $"{sectionType}Section";
            
            // 컨테이너에 맞춰 크기 조정
            var rectTransform = sectionInstance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
            
            // 섹션 컴포넌트 가져오기
            var sectionComponent = sectionInstance.GetComponent<SectionBase>();
            if (sectionComponent != null)
            {
                _sectionComponents[sectionType] = sectionComponent;
                
                // MainPageManager에 등록
                if (_mainPageManager != null)
                {
                    _mainPageManager.RegisterSection(sectionType, sectionComponent);
                }
            }
            else
            {
                Debug.LogError($"[MainPageScreen] Section {sectionType} prefab does not have SectionBase component");
            }
            
            _sectionInstances[sectionType] = sectionInstance;
            
            Debug.Log($"[MainPageScreen] Section {sectionType} instantiated successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainPageScreen] Failed to instantiate section {sectionType}: {e.Message}");
        }
        
        yield return null; // 프레임 분산
    }
    
    /// <summary>
    /// 섹션 정리
    /// </summary>
    private void CleanupSections()
    {
        foreach (var kvp in _sectionInstances)
        {
            if (kvp.Value != null)
            {
                // MainPageManager에서 등록 해제
                if (_mainPageManager != null)
                {
                    _mainPageManager.UnregisterSection(kvp.Key);
                }
                
                Destroy(kvp.Value);
            }
        }
        
        _sectionInstances.Clear();
        _sectionComponents.Clear();
        
        Debug.Log("[MainPageScreen] Sections cleaned up");
    }
    #endregion

    #region Event Management
    /// <summary>
    /// 외부 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        if (_mainPageManager != null)
        {
            MainPageManager.OnUserDataRefreshed += OnUserDataRefreshed;
            MainPageManager.OnSectionStateChanged += OnSectionStateChanged;
        }
        
        // AuthenticationManager 이벤트
        AuthenticationManager.OnLogoutCompleted += OnLogoutCompleted;
        
        // UserDataManager 이벤트
        UserDataManager.OnUserDataLoaded += OnUserDataLoaded;
        UserDataManager.OnUserDataUpdated += OnUserDataUpdated;
    }
    
    /// <summary>
    /// 외부 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_mainPageManager != null)
        {
            MainPageManager.OnUserDataRefreshed -= OnUserDataRefreshed;
            MainPageManager.OnSectionStateChanged -= OnSectionStateChanged;
        }
        
        AuthenticationManager.OnLogoutCompleted -= OnLogoutCompleted;
        UserDataManager.OnUserDataLoaded -= OnUserDataLoaded;
        UserDataManager.OnUserDataUpdated -= OnUserDataUpdated;
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 사용자 데이터 갱신 이벤트 처리
    /// </summary>
    private void OnUserDataRefreshed(UserData userData)
    {
        UpdateUserProfileDisplay(userData);
    }
    
    /// <summary>
    /// 섹션 상태 변경 이벤트 처리
    /// </summary>
    private void OnSectionStateChanged(MainPageSectionType sectionType, bool isActive)
    {
        if (_sectionInstances.TryGetValue(sectionType, out GameObject sectionInstance))
        {
            sectionInstance.SetActive(isActive);
        }
    }
    
    /// <summary>
    /// 로그아웃 완료 이벤트 처리
    /// </summary>
    private void OnLogoutCompleted()
    {
        Debug.Log("[MainPageScreen] Logout completed, cleaning up screen");
        CleanupSections();
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 사용자 데이터 로드 이벤트 처리
    /// </summary>
    private void OnUserDataLoaded(UserData userData)
    {
        UpdateUserProfileDisplay(userData);
    }
    
    /// <summary>
    /// 사용자 데이터 업데이트 이벤트 처리
    /// </summary>
    private void OnUserDataUpdated(UserData userData)
    {
        UpdateUserProfileDisplay(userData);
    }
    
    /// <summary>
    /// 사용자 프로필 버튼 클릭 처리
    /// </summary>
    private void OnUserProfileClicked()
    {
        Debug.Log("[MainPageScreen] User profile clicked");
        
        // 프로필 섹션으로 포커스 이동 또는 프로필 상세 화면 표시
        if (_mainPageManager != null)
        {
            _mainPageManager.SendMessageToSection(MainPageSectionType.Settings, MainPageSectionType.Profile, "focus_requested");
        }
    }
    
    /// <summary>
    /// 로그아웃 버튼 클릭 처리
    /// </summary>
    private void OnLogoutClicked()
    {
        Debug.Log("[MainPageScreen] Logout button clicked");
        
        if (_mainPageManager != null)
        {
            _mainPageManager.Logout();
        }
    }
    
    /// <summary>
    /// 네비게이션 버튼 클릭 처리
    /// </summary>
    private void OnNavigationClicked(int index)
    {
        Debug.Log($"[MainPageScreen] Navigation button {index} clicked");
        
        // 네비게이션 인덱스에 따른 섹션 포커스
        var sectionTypes = new[] 
        { 
            MainPageSectionType.Profile, 
            MainPageSectionType.Energy, 
            MainPageSectionType.Matching, 
            MainPageSectionType.Settings 
        };
        
        if (index >= 0 && index < sectionTypes.Length)
        {
            var targetSection = sectionTypes[index];
            FocusOnSection(targetSection);
        }
    }
    #endregion

    #region UI Updates
    /// <summary>
    /// 사용자 프로필 표시 업데이트
    /// </summary>
    private void UpdateUserProfileDisplay(UserData userData)
    {
        if (userData == null)
        {
            Debug.LogWarning("[MainPageScreen] Cannot update profile display - null user data");
            return;
        }
        
        try
        {
            if (userProfileText != null)
            {
                userProfileText.text = $"{userData.DisplayName} (Lv.{userData.Level})";
            }
            
            // 프로필 이미지 업데이트 (향후 구현)
            // UpdateUserProfileImage(userData);
            
            Debug.Log($"[MainPageScreen] Profile display updated: {userData.DisplayName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainPageScreen] Failed to update profile display: {e.Message}");
        }
    }
    
    /// <summary>
    /// 초기 사용자 데이터 로드
    /// </summary>
    private void LoadInitialUserData()
    {
        var userDataManager = UserDataManager.Instance;
        if (userDataManager?.CurrentUser != null)
        {
            UpdateUserProfileDisplay(userDataManager.CurrentUser);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 특정 섹션에 포커스
    /// </summary>
    public void FocusOnSection(MainPageSectionType sectionType)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[MainPageScreen] Cannot focus on section - screen not initialized");
            return;
        }
        
        Debug.Log($"[MainPageScreen] Focusing on section: {sectionType}");
        
        // 모든 섹션 비활성화 후 타겟 섹션만 활성화
        if (_mainPageManager != null)
        {
            _mainPageManager.DeactivateAllSections();
            _mainPageManager.SetSectionActive(sectionType, true);
        }
    }
    
    /// <summary>
    /// 모든 섹션 표시
    /// </summary>
    public void ShowAllSections()
    {
        if (_mainPageManager != null)
        {
            _mainPageManager.ActivateAllSections();
        }
    }
    
    /// <summary>
    /// 컴포넌트 유효성 검사
    /// </summary>
    private void ValidateComponents()
    {
        if (headerSection == null)
            Debug.LogError("[MainPageScreen] Header Section is not assigned!");
        
        if (contentArea == null)
            Debug.LogError("[MainPageScreen] Content Area is not assigned!");
        
        if (profileSectionContainer == null)
            Debug.LogError("[MainPageScreen] Profile Section Container is not assigned!");
        
        if (energySectionContainer == null)
            Debug.LogError("[MainPageScreen] Energy Section Container is not assigned!");
        
        if (matchingSectionContainer == null)
            Debug.LogError("[MainPageScreen] Matching Section Container is not assigned!");
        
        if (settingsSectionContainer == null)
            Debug.LogError("[MainPageScreen] Settings Section Container is not assigned!");
        
        if (userProfileButton == null)
            Debug.LogWarning("[MainPageScreen] User Profile Button is not assigned");
        
        if (logoutButton == null)
            Debug.LogWarning("[MainPageScreen] Logout Button is not assigned");
    }
    
    /// <summary>
    /// 화면 상태 정보 반환
    /// </summary>
    public MainPageScreenStatus GetScreenStatus()
    {
        return new MainPageScreenStatus
        {
            IsInitialized = _isInitialized,
            IsLayoutSetup = _isLayoutSetup,
            IsPortraitMode = _isPortraitMode,
            CurrentScreenSize = _currentScreenSize,
            InstantiatedSectionCount = _sectionInstances.Count,
            EnableResponsiveLayout = enableResponsiveLayout,
            EnableSectionAnimations = enableSectionAnimations
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 메인 페이지 화면 상태 정보
/// </summary>
[Serializable]
public class MainPageScreenStatus
{
    public bool IsInitialized;
    public bool IsLayoutSetup;
    public bool IsPortraitMode;
    public Vector2 CurrentScreenSize;
    public int InstantiatedSectionCount;
    public bool EnableResponsiveLayout;
    public bool EnableSectionAnimations;
}
#endregion