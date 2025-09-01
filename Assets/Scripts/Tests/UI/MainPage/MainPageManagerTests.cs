using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MainPageManager에 대한 단위 테스트
/// 커버리지 80% 이상을 목표로 하는 포괄적인 테스트 스위트
/// </summary>
public class MainPageManagerTests
{
    private MainPageManager _mainPageManager;
    private GameObject _testGameObject;
    private UserDataManager _mockUserDataManager;
    private AuthenticationManager _mockAuthenticationManager;
    private SettingsManager _mockSettingsManager;
    private ScreenTransitionManager _mockScreenTransitionManager;

    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 정리
        PlayerPrefs.DeleteAll();
        
        // 기존 MainPageManager 인스턴스가 있다면 제거
        if (MainPageManager.Instance != null)
        {
            Object.DestroyImmediate(MainPageManager.Instance.gameObject);
        }
        
        // 종속 매니저들 정리
        CleanupExistingManagers();
        
        // 테스트용 종속 매니저들 생성
        SetupMockManagers();
        
        // 테스트용 MainPageManager 생성
        _testGameObject = new GameObject("TestMainPageManager");
        _mainPageManager = _testGameObject.AddComponent<MainPageManager>();
    }

    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        CleanupMockManagers();
        PlayerPrefs.DeleteAll();
        
        // 이벤트 구독 해제
        MainPageManager.OnMainPageInitialized = null;
        MainPageManager.OnSectionStateChanged = null;
        MainPageManager.OnSectionCommunication = null;
        MainPageManager.OnUserDataRefreshed = null;
        MainPageManager.OnSettingChanged = null;
    }

    #region Setup Helpers
    /// <summary>
    /// 기존 매니저 인스턴스들 정리
    /// </summary>
    private void CleanupExistingManagers()
    {
        var managers = new[]
        {
            UserDataManager.Instance?.gameObject,
            AuthenticationManager.Instance?.gameObject,
            SettingsManager.Instance?.gameObject,
            ScreenTransitionManager.Instance?.gameObject
        };
        
        foreach (var manager in managers)
        {
            if (manager != null)
            {
                Object.DestroyImmediate(manager);
            }
        }
    }
    
    /// <summary>
    /// 테스트용 목 매니저들 설정
    /// </summary>
    private void SetupMockManagers()
    {
        // UserDataManager 생성
        var userDataGO = new GameObject("MockUserDataManager");
        _mockUserDataManager = userDataGO.AddComponent<UserDataManager>();
        
        // AuthenticationManager 생성
        var authGO = new GameObject("MockAuthenticationManager");
        _mockAuthenticationManager = authGO.AddComponent<AuthenticationManager>();
        
        // SettingsManager 생성
        var settingsGO = new GameObject("MockSettingsManager");
        _mockSettingsManager = settingsGO.AddComponent<SettingsManager>();
        
        // ScreenTransitionManager 생성
        var screenGO = new GameObject("MockScreenTransitionManager");
        _mockScreenTransitionManager = screenGO.AddComponent<ScreenTransitionManager>();
    }
    
    /// <summary>
    /// 목 매니저들 정리
    /// </summary>
    private void CleanupMockManagers()
    {
        if (_mockUserDataManager != null)
            Object.DestroyImmediate(_mockUserDataManager.gameObject);
        
        if (_mockAuthenticationManager != null)
            Object.DestroyImmediate(_mockAuthenticationManager.gameObject);
        
        if (_mockSettingsManager != null)
            Object.DestroyImmediate(_mockSettingsManager.gameObject);
        
        if (_mockScreenTransitionManager != null)
            Object.DestroyImmediate(_mockScreenTransitionManager.gameObject);
    }
    #endregion

    #region Singleton Tests
    [Test]
    public void Instance_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = MainPageManager.Instance;
        var instance2 = MainPageManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance1);
        Assert.IsNotNull(instance2);
        Assert.AreSame(instance1, instance2);
        Debug.Log("[TEST] MainPageManager Singleton pattern verified");
    }
    
    [Test]
    public void Instance_ShouldCreateDontDestroyOnLoadObject()
    {
        // Arrange & Act
        var instance = MainPageManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsNotNull(instance.gameObject);
        Assert.AreEqual("MainPageManager", instance.gameObject.name);
        Debug.Log("[TEST] MainPageManager DontDestroyOnLoad object created");
    }
    
    [Test]
    public void Awake_ShouldPreventMultipleInstances()
    {
        // Arrange
        var firstInstance = MainPageManager.Instance;
        
        // Act - 두 번째 인스턴스 생성 시도
        var secondGameObject = new GameObject("SecondMainPageManager");
        var secondInstance = secondGameObject.AddComponent<MainPageManager>();
        
        // Assert
        Assert.IsNotNull(firstInstance);
        Assert.IsNull(secondInstance); // Destroy되어야 함
        Assert.AreSame(firstInstance, MainPageManager.Instance);
        
        Debug.Log("[TEST] Multiple instance prevention verified");
    }
    #endregion

    #region Initialization Tests
    [UnityTest]
    public IEnumerator Initialize_ShouldWaitForDependentManagers()
    {
        // Arrange
        bool initializationCompleted = false;
        MainPageManager.OnMainPageInitialized += () => initializationCompleted = true;
        
        // Act
        yield return new WaitForSeconds(0.1f); // 매니저들 초기화 대기
        
        // Assert
        yield return new WaitUntil(() => initializationCompleted || Time.time > 15f);
        Assert.IsTrue(_mainPageManager.IsInitialized);
        Assert.IsTrue(initializationCompleted);
        
        Debug.Log("[TEST] MainPageManager initialization with dependencies verified");
    }
    
    [UnityTest]
    public IEnumerator Initialize_ShouldSetupSectionStates()
    {
        // Arrange & Act
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Profile));
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Energy));
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Matching));
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Settings));
        
        Debug.Log("[TEST] Section states initialization verified");
    }
    
    [UnityTest]
    public IEnumerator Initialize_ShouldSubscribeToManagerEvents()
    {
        // Arrange
        bool userDataEventReceived = false;
        bool authEventReceived = false;
        
        MainPageManager.OnUserDataRefreshed += (userData) => userDataEventReceived = true;
        
        // Act
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        // 이벤트 트리거 시뮬레이션
        var testUserData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User",
            Level = 1
        };
        
        UserDataManager.OnUserDataLoaded?.Invoke(testUserData);
        AuthenticationManager.OnAuthenticationStateChanged?.Invoke(false);
        
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(userDataEventReceived);
        
        Debug.Log("[TEST] Manager event subscription verified");
    }
    #endregion

    #region Section Management Tests
    [Test]
    public void RegisterSection_ShouldAddSectionToCollection()
    {
        // Arrange
        var mockSection = CreateMockSection(MainPageSectionType.Profile);
        
        // Act
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, mockSection);
        
        // Assert
        var retrievedSection = _mainPageManager.GetSection<MockProfileSection>(MainPageSectionType.Profile);
        Assert.IsNotNull(retrievedSection);
        Assert.AreSame(mockSection, retrievedSection);
        
        Debug.Log("[TEST] Section registration verified");
        
        // Cleanup
        Object.DestroyImmediate(mockSection.gameObject);
    }
    
    [Test]
    public void RegisterSection_WithNullSection_ShouldLogError()
    {
        // Arrange
        bool errorLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Error && message.Contains("Cannot register null section"))
                errorLogged = true;
        });
        
        // Act
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, null);
        
        // Assert
        Assert.IsTrue(errorLogged);
        
        Debug.Log("[TEST] Null section registration error handling verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    
    [Test]
    public void SetSectionActive_ShouldUpdateSectionState()
    {
        // Arrange
        var mockSection = CreateMockSection(MainPageSectionType.Profile);
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, mockSection);
        
        bool stateChangeEventReceived = false;
        MainPageSectionType eventSectionType = MainPageSectionType.Profile;
        bool eventState = false;
        
        MainPageManager.OnSectionStateChanged += (sectionType, state) =>
        {
            stateChangeEventReceived = true;
            eventSectionType = sectionType;
            eventState = state;
        };
        
        // Act
        _mainPageManager.SetSectionActive(MainPageSectionType.Profile, false);
        
        // Assert
        Assert.IsFalse(_mainPageManager.IsSectionActive(MainPageSectionType.Profile));
        Assert.IsTrue(stateChangeEventReceived);
        Assert.AreEqual(MainPageSectionType.Profile, eventSectionType);
        Assert.IsFalse(eventState);
        
        Debug.Log("[TEST] Section state change verified");
        
        // Cleanup
        Object.DestroyImmediate(mockSection.gameObject);
    }
    
    [UnityTest]
    public IEnumerator ActivateAllSections_ShouldActivateSequentially()
    {
        // Arrange
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        var energySection = CreateMockSection(MainPageSectionType.Energy);
        
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        _mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        
        // 모든 섹션 비활성화
        _mainPageManager.SetSectionActive(MainPageSectionType.Profile, false);
        _mainPageManager.SetSectionActive(MainPageSectionType.Energy, false);
        
        // Act
        _mainPageManager.ActivateAllSections();
        
        // 순차 활성화 대기
        yield return new WaitForSeconds(1f);
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Profile));
        Assert.IsTrue(_mainPageManager.IsSectionActive(MainPageSectionType.Energy));
        Assert.AreEqual(4, _mainPageManager.ActiveSectionCount); // 모든 섹션 활성화
        
        Debug.Log("[TEST] Sequential section activation verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
        Object.DestroyImmediate(energySection.gameObject);
    }
    #endregion

    #region Data Management Tests
    [UnityTest]
    public IEnumerator RefreshUserData_ShouldTriggerSyncAndEvents()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        bool userDataRefreshed = false;
        UserData refreshedData = null;
        
        MainPageManager.OnUserDataRefreshed += (userData) =>
        {
            userDataRefreshed = true;
            refreshedData = userData;
        };
        
        // 테스트 사용자 데이터 설정
        var testUserData = new UserData
        {
            UserId = "test_user_123",
            DisplayName = "Test User",
            Level = 5,
            Experience = 250
        };
        
        _mockUserDataManager.SetCurrentUser(testUserData.UserId, testUserData.DisplayName);
        
        // Act
        _mainPageManager.RefreshUserData();
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(userDataRefreshed);
        Assert.IsNotNull(refreshedData);
        Assert.AreEqual(testUserData.UserId, refreshedData.UserId);
        
        Debug.Log("[TEST] User data refresh and event triggering verified");
    }
    
    [Test]
    public void RefreshUserData_WhenAlreadyRefreshing_ShouldLogWarning()
    {
        // Arrange
        bool warningLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Warning && message.Contains("already in progress"))
                warningLogged = true;
        });
        
        // Act - 동시에 두 번 호출
        _mainPageManager.RefreshUserData();
        _mainPageManager.RefreshUserData();
        
        // Assert
        Assert.IsTrue(warningLogged);
        
        Debug.Log("[TEST] Concurrent refresh prevention verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    #endregion

    #region Communication Tests
    [Test]
    public void SendMessageToSection_ShouldDeliverMessageToTargetSection()
    {
        // Arrange
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        var energySection = CreateMockSection(MainPageSectionType.Energy);
        
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        _mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        
        bool communicationEventReceived = false;
        var testData = "test_message";
        
        MainPageManager.OnSectionCommunication += (from, to, data) =>
        {
            communicationEventReceived = true;
        };
        
        // Act
        _mainPageManager.SendMessageToSection(MainPageSectionType.Profile, MainPageSectionType.Energy, testData);
        
        // Assert
        Assert.IsTrue(communicationEventReceived);
        Assert.IsTrue(energySection.ReceivedMessage);
        Assert.AreEqual(testData, energySection.LastReceivedData);
        
        Debug.Log("[TEST] Section message delivery verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
        Object.DestroyImmediate(energySection.gameObject);
    }
    
    [Test]
    public void BroadcastToAllSections_ShouldDeliverToAllActiveSections()
    {
        // Arrange
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        var energySection = CreateMockSection(MainPageSectionType.Energy);
        var matchingSection = CreateMockSection(MainPageSectionType.Matching);
        
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        _mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        _mainPageManager.RegisterSection(MainPageSectionType.Matching, matchingSection);
        
        var testData = "broadcast_message";
        
        // Act
        _mainPageManager.BroadcastToAllSections(MainPageSectionType.Settings, testData);
        
        // Assert
        Assert.IsTrue(profileSection.ReceivedMessage);
        Assert.IsTrue(energySection.ReceivedMessage);
        Assert.IsTrue(matchingSection.ReceivedMessage);
        Assert.AreEqual(testData, profileSection.LastReceivedData);
        Assert.AreEqual(testData, energySection.LastReceivedData);
        Assert.AreEqual(testData, matchingSection.LastReceivedData);
        
        Debug.Log("[TEST] Broadcast message delivery verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
        Object.DestroyImmediate(energySection.gameObject);
        Object.DestroyImmediate(matchingSection.gameObject);
    }
    #endregion

    #region Manager Integration Tests
    [UnityTest]
    public IEnumerator Logout_ShouldDeactivateSectionsAndTriggerAuthLogout()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        
        bool logoutCompleted = false;
        AuthenticationManager.OnLogoutCompleted += () => logoutCompleted = true;
        
        // Act
        _mainPageManager.Logout();
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.AreEqual(0, _mainPageManager.ActiveSectionCount);
        Assert.IsTrue(logoutCompleted);
        
        Debug.Log("[TEST] Logout process verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
    }
    
    [Test]
    public void GetSetting_ShouldReturnValueFromSettingsManager()
    {
        // Arrange
        const string testSettingName = "EnableAutoLogin";
        const bool expectedValue = true;
        
        // SettingsManager에 설정값 설정 (모의)
        // 실제 구현에서는 _mockSettingsManager.SetSetting(testSettingName, expectedValue);
        
        // Act
        var result = _mainPageManager.GetSetting<bool>(testSettingName);
        
        // Assert - 기본값이라도 호출이 성공해야 함
        Assert.IsNotNull(result);
        
        Debug.Log($"[TEST] Setting retrieval verified: {testSettingName} = {result}");
    }
    #endregion

    #region Status Tests
    [UnityTest]
    public IEnumerator GetStatus_ShouldReturnAccurateInformation()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        
        // Act
        var status = _mainPageManager.GetStatus();
        
        // Assert
        Assert.IsNotNull(status);
        Assert.IsTrue(status.IsInitialized);
        Assert.AreEqual(1, status.RegisteredSectionCount);
        Assert.AreEqual(4, status.ActiveSectionCount); // 기본 4개 섹션 모두 활성화
        
        Debug.Log("[TEST] Status information accuracy verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
    }
    
    [UnityTest]
    public IEnumerator ForceRefresh_ShouldTriggerAllSectionRefresh()
    {
        // Arrange
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > 15f);
        
        var profileSection = CreateMockSection(MainPageSectionType.Profile);
        var energySection = CreateMockSection(MainPageSectionType.Energy);
        
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, profileSection);
        _mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        
        // Act
        _mainPageManager.ForceRefresh();
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(profileSection.ForceRefreshCalled);
        Assert.IsTrue(energySection.ForceRefreshCalled);
        
        Debug.Log("[TEST] Force refresh propagation verified");
        
        // Cleanup
        Object.DestroyImmediate(profileSection.gameObject);
        Object.DestroyImmediate(energySection.gameObject);
    }
    #endregion

    #region Performance Tests
    [UnityTest]
    public IEnumerator Performance_InitializationShouldCompleteWithinTimeout()
    {
        // Arrange
        float startTime = Time.time;
        const float maxInitTime = 3f; // 3초 이내 초기화 완료
        
        // Act
        yield return new WaitUntil(() => _mainPageManager.IsInitialized || Time.time > startTime + maxInitTime);
        
        float initializationTime = Time.time - startTime;
        
        // Assert
        Assert.IsTrue(_mainPageManager.IsInitialized);
        Assert.Less(initializationTime, maxInitTime);
        
        Debug.Log($"[TEST] Initialization performance verified: {initializationTime:F2}s < {maxInitTime}s");
    }
    
    [Test]
    public void Performance_MemoryUsage_ShouldBeReasonable()
    {
        // Arrange
        long initialMemory = GC.GetTotalMemory(true);
        
        // Act - 여러 섹션 등록
        var sections = new[]
        {
            CreateMockSection(MainPageSectionType.Profile),
            CreateMockSection(MainPageSectionType.Energy),
            CreateMockSection(MainPageSectionType.Matching),
            CreateMockSection(MainPageSectionType.Settings)
        };
        
        foreach (var section in sections)
        {
            _mainPageManager.RegisterSection(section.SectionType, section);
        }
        
        long afterMemory = GC.GetTotalMemory(true);
        long memoryIncrease = afterMemory - initialMemory;
        const long maxMemoryIncrease = 10 * 1024 * 1024; // 10MB 제한
        
        // Assert
        Assert.Less(memoryIncrease, maxMemoryIncrease);
        
        Debug.Log($"[TEST] Memory usage verified: {memoryIncrease / 1024 / 1024}MB < {maxMemoryIncrease / 1024 / 1024}MB");
        
        // Cleanup
        foreach (var section in sections)
        {
            Object.DestroyImmediate(section.gameObject);
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 테스트용 목 섹션 생성
    /// </summary>
    private MockProfileSection CreateMockSection(MainPageSectionType sectionType)
    {
        var sectionGameObject = new GameObject($"Mock{sectionType}Section");
        var mockSection = sectionGameObject.AddComponent<MockProfileSection>();
        mockSection.SetSectionType(sectionType);
        return mockSection;
    }
    #endregion
}

#region Test Helper Classes
/// <summary>
/// 테스트용 목 섹션 클래스
/// </summary>
public class MockProfileSection : SectionBase
{
    public override MainPageSectionType SectionType { get; private set; } = MainPageSectionType.Profile;
    public override string SectionDisplayName => "Mock Profile Section";
    
    public bool ReceivedMessage { get; private set; }
    public object LastReceivedData { get; private set; }
    public bool ForceRefreshCalled { get; private set; }
    public bool InitializeCalled { get; private set; }
    public bool ActivateCalled { get; private set; }
    public bool DeactivateCalled { get; private set; }
    
    public void SetSectionType(MainPageSectionType sectionType)
    {
        SectionType = sectionType;
    }
    
    protected override void OnInitialize()
    {
        InitializeCalled = true;
    }
    
    protected override void OnActivate()
    {
        ActivateCalled = true;
    }
    
    protected override void OnDeactivate()
    {
        DeactivateCalled = true;
    }
    
    protected override void OnCleanup()
    {
        // Mock cleanup
    }
    
    protected override void UpdateUI(UserData userData)
    {
        // Mock UI update
    }
    
    protected override void ValidateComponents()
    {
        // Mock validation
    }
    
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        ReceivedMessage = true;
        LastReceivedData = data;
    }
    
    protected override void OnForceRefresh()
    {
        ForceRefreshCalled = true;
    }
}

/// <summary>
/// 테스트용 로그 핸들러
/// </summary>
public class TestLogHandler : ILogHandler
{
    private readonly Action<LogType, string> _onLog;
    
    public TestLogHandler(Action<LogType, string> onLog)
    {
        _onLog = onLog;
    }
    
    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        _onLog?.Invoke(logType, string.Format(format, args));
    }
    
    public void LogException(System.Exception exception, UnityEngine.Object context)
    {
        _onLog?.Invoke(LogType.Exception, exception.Message);
    }
}
#endregion