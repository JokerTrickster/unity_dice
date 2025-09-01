using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 메인 페이지 시스템 통합 테스트
/// MainPageManager, SectionBase, 기존 매니저들 간의 통합 기능을 검증하는 테스트 스위트
/// </summary>
public class MainPageIntegrationTests
{
    private MainPageManager _mainPageManager;
    private MainPageScreen _mainPageScreen;
    private UserDataManager _userDataManager;
    private AuthenticationManager _authenticationManager;
    private SettingsManager _settingsManager;
    
    private GameObject _mainPageManagerGO;
    private GameObject _mainPageScreenGO;
    private GameObject _userDataManagerGO;
    private GameObject _authManagerGO;
    private GameObject _settingsManagerGO;

    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 완전 정리
        PlayerPrefs.DeleteAll();
        CleanupExistingInstances();
        
        // 실제 매니저들 생성 (통합 테스트용)
        SetupRealManagers();
        SetupMainPageComponents();
    }

    [TearDown]
    public void TearDown()
    {
        // 테스트 후 완전 정리
        CleanupTestComponents();
        PlayerPrefs.DeleteAll();
        
        // 모든 이벤트 구독 해제
        ClearAllEvents();
    }

    #region Setup Helpers
    /// <summary>
    /// 기존 인스턴스들 완전 정리
    /// </summary>
    private void CleanupExistingInstances()
    {
        var existingManagers = Object.FindObjectsOfType<MonoBehaviour>();
        foreach (var manager in existingManagers)
        {
            if (manager is MainPageManager || manager is UserDataManager || 
                manager is AuthenticationManager || manager is SettingsManager ||
                manager is MainPageScreen)
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }
    }
    
    /// <summary>
    /// 실제 매니저들 설정
    /// </summary>
    private void SetupRealManagers()
    {
        // UserDataManager 생성
        _userDataManagerGO = new GameObject("UserDataManager");
        _userDataManager = _userDataManagerGO.AddComponent<UserDataManager>();
        
        // AuthenticationManager 생성
        _authManagerGO = new GameObject("AuthenticationManager");
        _authenticationManager = _authManagerGO.AddComponent<AuthenticationManager>();
        
        // SettingsManager 생성
        _settingsManagerGO = new GameObject("SettingsManager");
        _settingsManager = _settingsManagerGO.AddComponent<SettingsManager>();
        
        // MainPageManager 생성
        _mainPageManagerGO = new GameObject("MainPageManager");
        _mainPageManager = _mainPageManagerGO.AddComponent<MainPageManager>();
    }
    
    /// <summary>
    /// 메인 페이지 컴포넌트들 설정
    /// </summary>
    private void SetupMainPageComponents()
    {
        // MainPageScreen 생성
        _mainPageScreenGO = new GameObject("MainPageScreen");
        var canvas = _mainPageScreenGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        _mainPageScreen = _mainPageScreenGO.AddComponent<MainPageScreen>();
        
        // 필요한 UI 컴포넌트들 최소한으로 설정
        SetupMinimalUIComponents();
    }
    
    /// <summary>
    /// 최소한의 UI 컴포넌트 설정
    /// </summary>
    private void SetupMinimalUIComponents()
    {
        // Content Area와 Container들 생성
        var contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(_mainPageScreenGO.transform);
        
        var containers = new[]
        {
            "ProfileContainer", "EnergyContainer", 
            "MatchingContainer", "SettingsContainer"
        };
        
        foreach (var containerName in containers)
        {
            var container = new GameObject(containerName);
            container.transform.SetParent(contentArea.transform);
            container.AddComponent<RectTransform>();
        }
    }
    
    /// <summary>
    /// 테스트 컴포넌트들 정리
    /// </summary>
    private void CleanupTestComponents()
    {
        var gameObjects = new[]
        {
            _mainPageManagerGO, _mainPageScreenGO, _userDataManagerGO, 
            _authManagerGO, _settingsManagerGO
        };
        
        foreach (var go in gameObjects)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
    
    /// <summary>
    /// 모든 이벤트 구독 해제
    /// </summary>
    private void ClearAllEvents()
    {
        MainPageManager.OnMainPageInitialized = null;
        MainPageManager.OnSectionStateChanged = null;
        MainPageManager.OnSectionCommunication = null;
        MainPageManager.OnUserDataRefreshed = null;
        MainPageManager.OnSettingChanged = null;
        
        SectionBase.OnSectionInitialized = null;
        SectionBase.OnSectionActivated = null;
        SectionBase.OnSectionDeactivated = null;
        SectionBase.OnSectionError = null;
        
        UserDataManager.OnUserDataLoaded = null;
        UserDataManager.OnUserDataUpdated = null;
        UserDataManager.OnSyncCompleted = null;
        UserDataManager.OnOfflineModeChanged = null;
        
        AuthenticationManager.OnAuthenticationStateChanged = null;
        AuthenticationManager.OnLoginSuccess = null;
        AuthenticationManager.OnLoginFailed = null;
        AuthenticationManager.OnLogoutCompleted = null;
    }
    #endregion

    #region Integration Tests
    [UnityTest]
    public IEnumerator Integration_FullSystemInitialization_ShouldCompleteSuccessfully()
    {
        // Arrange
        float startTime = Time.time;
        const float maxTotalInitTime = 5f; // 전체 시스템 초기화 5초 이내
        
        bool mainPageInitialized = false;
        MainPageManager.OnMainPageInitialized += () => mainPageInitialized = true;
        
        // Act - 모든 컴포넌트 초기화 대기
        yield return new WaitUntil(() => 
            (_userDataManager.IsInitialized && 
             _authenticationManager.IsInitialized && 
             _settingsManager.IsInitialized && 
             _mainPageManager.IsInitialized &&
             _mainPageScreen.IsInitialized) || 
            Time.time > startTime + maxTotalInitTime);
        
        float totalInitTime = Time.time - startTime;
        
        // Assert
        Assert.IsTrue(_userDataManager.IsInitialized, "UserDataManager not initialized");
        Assert.IsTrue(_authenticationManager.IsInitialized, "AuthenticationManager not initialized");
        Assert.IsTrue(_settingsManager.IsInitialized, "SettingsManager not initialized");
        Assert.IsTrue(_mainPageManager.IsInitialized, "MainPageManager not initialized");
        Assert.IsTrue(_mainPageScreen.IsInitialized, "MainPageScreen not initialized");
        Assert.IsTrue(mainPageInitialized, "MainPageInitialized event not triggered");
        Assert.Less(totalInitTime, maxTotalInitTime, $"Total initialization time exceeded: {totalInitTime:F2}s");
        
        Debug.Log($"[INTEGRATION TEST] Full system initialization verified: {totalInitTime:F2}s");
    }
    
    [UnityTest]
    public IEnumerator Integration_UserDataFlow_ShouldPropagateCorrectly()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        bool userDataRefreshed = false;
        UserData refreshedData = null;
        
        MainPageManager.OnUserDataRefreshed += (userData) =>
        {
            userDataRefreshed = true;
            refreshedData = userData;
        };
        
        var testUserData = new UserData
        {
            UserId = "integration_test_user",
            DisplayName = "Integration Test User",
            Email = "test@example.com",
            Level = 5,
            Experience = 500
        };
        
        // Act
        _userDataManager.SetCurrentUser(testUserData.UserId, testUserData.DisplayName, testUserData.Email);
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsNotNull(_userDataManager.CurrentUser);
        Assert.AreEqual(testUserData.UserId, _userDataManager.CurrentUser.UserId);
        Assert.AreEqual(testUserData.DisplayName, _userDataManager.CurrentUser.DisplayName);
        Assert.IsNotNull(_mainPageManager.CurrentUserData);
        Assert.AreEqual(testUserData.UserId, _mainPageManager.CurrentUserData.UserId);
        
        Debug.Log("[INTEGRATION TEST] User data flow propagation verified");
    }
    
    [UnityTest]
    public IEnumerator Integration_LogoutFlow_ShouldCleanupProperly()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        // 사용자 설정
        var testUserData = new UserData
        {
            UserId = "logout_test_user",
            DisplayName = "Logout Test User"
        };
        _userDataManager.SetCurrentUser(testUserData.UserId, testUserData.DisplayName);
        
        bool logoutCompleted = false;
        AuthenticationManager.OnLogoutCompleted += () => logoutCompleted = true;
        
        // Act
        _mainPageManager.Logout();
        yield return new WaitForSeconds(1f);
        
        // Assert
        Assert.IsTrue(logoutCompleted);
        Assert.AreEqual(0, _mainPageManager.ActiveSectionCount);
        Assert.IsNull(_userDataManager.CurrentUser);
        Assert.IsFalse(_mainPageScreen.gameObject.activeInHierarchy);
        
        Debug.Log("[INTEGRATION TEST] Logout flow cleanup verified");
        
        // 테스트 후 화면 재활성화
        _mainPageScreenGO.SetActive(true);
    }
    
    [UnityTest]
    public IEnumerator Integration_SettingsFlow_ShouldPropagateToSections()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        // 테스트용 섹션 등록
        var testSection = CreateTestSection();
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, testSection);
        
        bool settingChangeReceived = false;
        MainPageManager.OnSettingChanged += (name, value) => settingChangeReceived = true;
        
        // Act
        var testSettingName = "TestSetting";
        var testSettingValue = true;
        _mainPageManager.SetSetting(testSettingName, testSettingValue);
        
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(settingChangeReceived);
        
        Debug.Log("[INTEGRATION TEST] Settings flow propagation verified");
        
        // Cleanup
        Object.DestroyImmediate(testSection.gameObject);
    }
    
    [UnityTest]
    public IEnumerator Integration_SectionCommunication_ShouldWorkCorrectly()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        var profileSection = CreateTestSection(MainPageSectionType.Profile);
        var energySection = CreateTestSection(MainPageSectionType.Energy);
        
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        _mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        
        bool communicationReceived = false;
        MainPageManager.OnSectionCommunication += (from, to, data) => communicationReceived = true;
        
        var testMessage = "integration_test_message";
        
        // Act
        _mainPageManager.SendMessageToSection(MainPageSectionType.Profile, MainPageSectionType.Energy, testMessage);
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(communicationReceived);
        Assert.IsTrue(energySection.ReceivedMessage);
        Assert.AreEqual(testMessage, energySection.LastReceivedData);
        
        Debug.Log("[INTEGRATION TEST] Section communication verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
        Object.DestroyImmediate(energySection.gameObject);
    }
    #endregion

    #region Performance Integration Tests
    [UnityTest]
    public IEnumerator Integration_Performance_ShouldMeetRequirements()
    {
        // Arrange
        float startTime = Time.time;
        long initialMemory = GC.GetTotalMemory(true);
        
        const float maxLoadTime = 3f; // 요구사항: 3초 이내 로딩
        const long maxMemoryIncrease = 10 * 1024 * 1024; // 요구사항: 10MB 이하 증가
        
        // Act
        yield return new WaitUntil(() => 
            (_mainPageManager.IsInitialized && _mainPageScreen.IsInitialized) || 
            Time.time > startTime + maxLoadTime);
        
        float loadTime = Time.time - startTime;
        long finalMemory = GC.GetTotalMemory(true);
        long memoryIncrease = finalMemory - initialMemory;
        
        // Assert
        Assert.Less(loadTime, maxLoadTime, $"Loading time exceeded requirement: {loadTime:F2}s > {maxLoadTime}s");
        Assert.Less(memoryIncrease, maxMemoryIncrease, 
            $"Memory increase exceeded requirement: {memoryIncrease / 1024 / 1024}MB > {maxMemoryIncrease / 1024 / 1024}MB");
        
        Debug.Log($"[INTEGRATION TEST] Performance requirements verified - Load: {loadTime:F2}s, Memory: {memoryIncrease / 1024 / 1024}MB");
    }
    
    [UnityTest]
    public IEnumerator Integration_MemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        // 메모리 압박 상황 시뮬레이션 (여러 섹션 등록/해제)
        var sections = new IntegrationTestSection[10];
        
        // Act
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i] = CreateTestSection((MainPageSectionType)(i % 4));
            _mainPageManager.RegisterSection((MainPageSectionType)(i % 4), sections[i]);
            yield return null; // 프레임 분산
        }
        
        // 강제 GC 수행
        System.GC.Collect();
        yield return new WaitForSeconds(0.1f);
        
        // 섹션들 정리
        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i] != null)
            {
                _mainPageManager.UnregisterSection((MainPageSectionType)(i % 4));
                Object.DestroyImmediate(sections[i].gameObject);
            }
        }
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsInitialized); // 여전히 정상 작동해야 함
        
        Debug.Log("[INTEGRATION TEST] Memory pressure handling verified");
    }
    #endregion

    #region Error Recovery Tests
    [UnityTest]
    public IEnumerator Integration_ManagerFailure_ShouldContinueOperation()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        // Act - UserDataManager 강제 제거 (실패 시뮬레이션)
        Object.DestroyImmediate(_userDataManagerGO);
        _userDataManager = null;
        
        yield return new WaitForSeconds(0.5f);
        
        // 섹션 추가 시도
        var testSection = CreateTestSection();
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, testSection);
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsInitialized); // 여전히 작동해야 함
        Assert.IsNotNull(_mainPageManager.GetSection<IntegrationTestSection>(MainPageSectionType.Profile));
        
        Debug.Log("[INTEGRATION TEST] Manager failure recovery verified");
        
        // Cleanup
        Object.DestroyImmediate(testSection.gameObject);
    }
    
    [UnityTest]
    public IEnumerator Integration_AuthenticationFlow_ShouldUpdateSections()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        var testSection = CreateTestSection();
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, testSection);
        
        bool authStateChanged = false;
        var testUserData = new UserData
        {
            UserId = "auth_test_user",
            DisplayName = "Auth Test User",
            Level = 1
        };
        
        // UserDataManager에 사용자 설정
        _userDataManager.SetCurrentUser(testUserData.UserId, testUserData.DisplayName);
        
        // Act - 인증 상태 변경 시뮬레이션
        AuthenticationManager.OnAuthenticationStateChanged?.Invoke(false);
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.AreEqual(0, _mainPageManager.ActiveSectionCount); // 로그아웃 시 섹션들 비활성화
        
        Debug.Log("[INTEGRATION TEST] Authentication flow impact verified");
        
        // Cleanup
        Object.DestroyImmediate(testSection.gameObject);
    }
    #endregion

    #region Data Consistency Tests
    [UnityTest]
    public IEnumerator Integration_DataConsistency_AcrossComponents()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        var testSection = CreateTestSection();
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, testSection);
        
        var testUserData = new UserData
        {
            UserId = "consistency_test_user",
            DisplayName = "Consistency Test User",
            Level = 8,
            Experience = 750
        };
        
        // Act
        _userDataManager.SetCurrentUser(testUserData.UserId, testUserData.DisplayName);
        _userDataManager.UpdateUserData(testUserData);
        
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        // UserDataManager에 저장된 데이터
        Assert.AreEqual(testUserData.UserId, _userDataManager.CurrentUser.UserId);
        
        // MainPageManager를 통해 접근한 데이터
        Assert.AreEqual(testUserData.UserId, _mainPageManager.CurrentUserData.UserId);
        
        // 섹션에 전달된 데이터
        Assert.IsNotNull(testSection.CachedUserData);
        Assert.AreEqual(testUserData.UserId, testSection.CachedUserData.UserId);
        
        Debug.Log("[INTEGRATION TEST] Data consistency across components verified");
        
        // Cleanup
        Object.DestroyImmediate(testSection.gameObject);
    }
    #endregion

    #region Stress Tests
    [UnityTest]
    public IEnumerator Integration_RapidSectionOperations_ShouldHandleGracefully()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 10f);
        
        var testSections = new IntegrationTestSection[4];
        var sectionTypes = new[]
        {
            MainPageSectionType.Profile, MainPageSectionType.Energy,
            MainPageSectionType.Matching, MainPageSectionType.Settings
        };
        
        // Act - 빠른 섹션 등록/해제 반복
        for (int cycle = 0; cycle < 3; cycle++)
        {
            // 등록
            for (int i = 0; i < testSections.Length; i++)
            {
                testSections[i] = CreateTestSection(sectionTypes[i]);
                _mainPageManager.RegisterSection(sectionTypes[i], testSections[i]);
            }
            
            yield return new WaitForSeconds(0.1f);
            
            // 활성화/비활성화 반복
            for (int i = 0; i < 5; i++)
            {
                _mainPageManager.DeactivateAllSections();
                _mainPageManager.ActivateAllSections();
                yield return null;
            }
            
            // 해제
            for (int i = 0; i < testSections.Length; i++)
            {
                _mainPageManager.UnregisterSection(sectionTypes[i]);
                Object.DestroyImmediate(testSections[i].gameObject);
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsInitialized); // 여전히 정상 작동해야 함
        Assert.AreEqual(0, _mainPageManager.GetStatus().RegisteredSectionCount);
        
        Debug.Log("[INTEGRATION TEST] Rapid section operations stress test verified");
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 테스트용 섹션 생성
    /// </summary>
    private IntegrationTestSection CreateTestSection(MainPageSectionType sectionType = MainPageSectionType.Profile)
    {
        var sectionGO = new GameObject($"IntegrationTestSection_{sectionType}");
        var section = sectionGO.AddComponent<IntegrationTestSection>();
        section.SetSectionType(sectionType);
        return section;
    }
    #endregion
}

#region Test Helper Classes
/// <summary>
/// 통합 테스트용 섹션 클래스
/// </summary>
public class IntegrationTestSection : SectionBase
{
    public override MainPageSectionType SectionType { get; private set; } = MainPageSectionType.Profile;
    public override string SectionDisplayName => $"Integration Test {SectionType} Section";
    
    public bool ReceivedMessage { get; private set; }
    public object LastReceivedData { get; private set; }
    public bool InitializeCalled { get; private set; }
    public bool ActivateCalled { get; private set; }
    public bool DeactivateCalled { get; private set; }
    public bool UIUpdateCalled { get; private set; }
    
    public void SetSectionType(MainPageSectionType sectionType)
    {
        SectionType = sectionType;
    }
    
    protected override void OnInitialize()
    {
        InitializeCalled = true;
        Debug.Log($"[IntegrationTestSection] {SectionType} initialized");
    }
    
    protected override void OnActivate()
    {
        ActivateCalled = true;
        Debug.Log($"[IntegrationTestSection] {SectionType} activated");
    }
    
    protected override void OnDeactivate()
    {
        DeactivateCalled = true;
        Debug.Log($"[IntegrationTestSection] {SectionType} deactivated");
    }
    
    protected override void OnCleanup()
    {
        Debug.Log($"[IntegrationTestSection] {SectionType} cleaned up");
    }
    
    protected override void UpdateUI(UserData userData)
    {
        UIUpdateCalled = true;
        Debug.Log($"[IntegrationTestSection] {SectionType} UI updated for user: {userData?.DisplayName}");
    }
    
    protected override void ValidateComponents()
    {
        // Mock validation for integration test
    }
    
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        ReceivedMessage = true;
        LastReceivedData = data;
        Debug.Log($"[IntegrationTestSection] {SectionType} received message from {fromSection}: {data}");
    }
}
#endregion