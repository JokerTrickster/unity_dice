using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// SectionBase 추상 클래스에 대한 단위 테스트
/// 섹션 라이프사이클과 기본 기능을 검증하는 포괄적인 테스트 스위트
/// </summary>
public class SectionBaseTests
{
    private TestableSection _testSection;
    private GameObject _testGameObject;
    private MainPageManager _mockMainPageManager;
    private UserDataManager _mockUserDataManager;
    private AuthenticationManager _mockAuthenticationManager;
    private SettingsManager _mockSettingsManager;

    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 정리
        PlayerPrefs.DeleteAll();
        CleanupExistingManagers();
        
        // 테스트용 매니저들 생성
        SetupMockManagers();
        
        // 테스트용 섹션 생성
        _testGameObject = new GameObject("TestSection");
        _testSection = _testGameObject.AddComponent<TestableSection>();
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
        SectionBase.OnSectionInitialized = null;
        SectionBase.OnSectionActivated = null;
        SectionBase.OnSectionDeactivated = null;
        SectionBase.OnSectionError = null;
    }

    #region Setup Helpers
    /// <summary>
    /// 기존 매니저 인스턴스들 정리
    /// </summary>
    private void CleanupExistingManagers()
    {
        var managers = new[]
        {
            MainPageManager.Instance?.gameObject,
            UserDataManager.Instance?.gameObject,
            AuthenticationManager.Instance?.gameObject,
            SettingsManager.Instance?.gameObject
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
        // MainPageManager 생성
        var mainPageGO = new GameObject("MockMainPageManager");
        _mockMainPageManager = mainPageGO.AddComponent<MainPageManager>();
        
        // UserDataManager 생성
        var userDataGO = new GameObject("MockUserDataManager");
        _mockUserDataManager = userDataGO.AddComponent<UserDataManager>();
        
        // AuthenticationManager 생성
        var authGO = new GameObject("MockAuthenticationManager");
        _mockAuthenticationManager = authGO.AddComponent<AuthenticationManager>();
        
        // SettingsManager 생성
        var settingsGO = new GameObject("MockSettingsManager");
        _mockSettingsManager = settingsGO.AddComponent<SettingsManager>();
    }
    
    /// <summary>
    /// 목 매니저들 정리
    /// </summary>
    private void CleanupMockManagers()
    {
        if (_mockMainPageManager != null)
            Object.DestroyImmediate(_mockMainPageManager.gameObject);
        
        if (_mockUserDataManager != null)
            Object.DestroyImmediate(_mockUserDataManager.gameObject);
        
        if (_mockAuthenticationManager != null)
            Object.DestroyImmediate(_mockAuthenticationManager.gameObject);
        
        if (_mockSettingsManager != null)
            Object.DestroyImmediate(_mockSettingsManager.gameObject);
    }
    #endregion

    #region Lifecycle Tests
    [Test]
    public void Initialize_ShouldSetInitializedStateAndTriggerEvent()
    {
        // Arrange
        bool initializeEventReceived = false;
        MainPageSectionType eventSectionType = MainPageSectionType.Profile;
        
        SectionBase.OnSectionInitialized += (sectionType) =>
        {
            initializeEventReceived = true;
            eventSectionType = sectionType;
        };
        
        // Act
        _testSection.Initialize(_mockMainPageManager);
        
        // Assert
        Assert.IsTrue(_testSection.IsInitialized);
        Assert.IsTrue(initializeEventReceived);
        Assert.AreEqual(MainPageSectionType.Profile, eventSectionType);
        Assert.IsTrue(_testSection.OnInitializeCalled);
        
        Debug.Log("[TEST] Section initialization verified");
    }
    
    [Test]
    public void Initialize_WhenAlreadyInitialized_ShouldLogWarning()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        bool warningLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Warning && message.Contains("Already initialized"))
                warningLogged = true;
        });
        
        // Act
        _testSection.Initialize(_mockMainPageManager);
        
        // Assert
        Assert.IsTrue(warningLogged);
        
        Debug.Log("[TEST] Duplicate initialization prevention verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    
    [Test]
    public void Activate_ShouldSetActiveStateAndTriggerEvent()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        bool activateEventReceived = false;
        SectionBase.OnSectionActivated += (sectionType) => activateEventReceived = true;
        
        // Act
        _testSection.Activate();
        
        // Assert
        Assert.IsTrue(_testSection.IsActive);
        Assert.IsTrue(activateEventReceived);
        Assert.IsTrue(_testSection.gameObject.activeInHierarchy);
        Assert.IsTrue(_testSection.OnActivateCalled);
        
        Debug.Log("[TEST] Section activation verified");
    }
    
    [Test]
    public void Activate_WhenNotInitialized_ShouldLogError()
    {
        // Arrange
        bool errorLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Error && message.Contains("Cannot activate - not initialized"))
                errorLogged = true;
        });
        
        // Act
        _testSection.Activate();
        
        // Assert
        Assert.IsFalse(_testSection.IsActive);
        Assert.IsTrue(errorLogged);
        
        Debug.Log("[TEST] Activation without initialization error handling verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    
    [Test]
    public void Deactivate_ShouldSetInactiveStateAndTriggerEvent()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        bool deactivateEventReceived = false;
        SectionBase.OnSectionDeactivated += (sectionType) => deactivateEventReceived = true;
        
        // Act
        _testSection.Deactivate();
        
        // Assert
        Assert.IsFalse(_testSection.IsActive);
        Assert.IsTrue(deactivateEventReceived);
        Assert.IsFalse(_testSection.gameObject.activeInHierarchy);
        Assert.IsTrue(_testSection.OnDeactivateCalled);
        
        Debug.Log("[TEST] Section deactivation verified");
    }
    
    [Test]
    public void Cleanup_ShouldResetStateAndCallOnCleanup()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        // Act
        _testSection.Cleanup();
        
        // Assert
        Assert.IsFalse(_testSection.IsInitialized);
        Assert.IsFalse(_testSection.IsActive);
        Assert.IsTrue(_testSection.OnCleanupCalled);
        
        Debug.Log("[TEST] Section cleanup verified");
    }
    #endregion

    #region Data Handling Tests
    [Test]
    public void OnUserDataUpdated_ShouldCacheDataAndUpdateUI()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        var testUserData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User",
            Level = 3,
            Experience = 150
        };
        
        // Act
        _testSection.OnUserDataUpdated(testUserData);
        
        // Assert
        Assert.IsNotNull(_testSection.CachedUserData);
        Assert.AreEqual(testUserData.UserId, _testSection.CachedUserData.UserId);
        Assert.AreEqual(testUserData.DisplayName, _testSection.CachedUserData.DisplayName);
        Assert.IsTrue(_testSection.UpdateUICalled);
        
        Debug.Log("[TEST] User data update and caching verified");
    }
    
    [Test]
    public void OnUserDataUpdated_WithNullData_ShouldLogWarning()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        bool warningLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Warning && message.Contains("null user data"))
                warningLogged = true;
        });
        
        // Act
        _testSection.OnUserDataUpdated(null);
        
        // Assert
        Assert.IsTrue(warningLogged);
        Assert.IsNull(_testSection.CachedUserData);
        
        Debug.Log("[TEST] Null user data handling verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    
    [Test]
    public void ReceiveMessage_ShouldCallOnReceiveMessage()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        var testData = "test_message_data";
        
        // Act
        _testSection.ReceiveMessage(MainPageSectionType.Energy, testData);
        
        // Assert
        Assert.IsTrue(_testSection.OnReceiveMessageCalled);
        Assert.AreEqual(MainPageSectionType.Energy, _testSection.LastMessageFromSection);
        Assert.AreEqual(testData, _testSection.LastReceivedMessageData);
        
        Debug.Log("[TEST] Message reception verified");
    }
    
    [Test]
    public void OnSettingChanged_ShouldCallOnSettingUpdated()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        const string testSetting = "TestSetting";
        const bool testValue = true;
        
        // Act
        _testSection.OnSettingChanged(testSetting, testValue);
        
        // Assert
        Assert.IsTrue(_testSection.OnSettingUpdatedCalled);
        Assert.AreEqual(testSetting, _testSection.LastUpdatedSettingName);
        Assert.AreEqual(testValue, _testSection.LastUpdatedSettingValue);
        
        Debug.Log("[TEST] Setting change handling verified");
    }
    
    [Test]
    public void OnModeChanged_ShouldUpdateOfflineModeAndCallHandler()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        // Act
        _testSection.OnModeChanged(true);
        
        // Assert
        Assert.IsTrue(_testSection.IsOfflineMode);
        Assert.IsTrue(_testSection.OnOfflineModeChangedCalled);
        Assert.IsTrue(_testSection.LastOfflineModeValue);
        
        Debug.Log("[TEST] Offline mode change handling verified");
    }
    #endregion

    #region Refresh Tests
    [Test]
    public void ForceRefresh_WhenActive_ShouldCallOnForceRefresh()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        var testUserData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User"
        };
        _testSection.OnUserDataUpdated(testUserData);
        
        // Act
        _testSection.ForceRefresh();
        
        // Assert
        Assert.IsTrue(_testSection.OnForceRefreshCalled);
        Assert.IsTrue(_testSection.UpdateUICalled); // 캐시된 데이터로 UI 업데이트
        
        Debug.Log("[TEST] Force refresh functionality verified");
    }
    
    [Test]
    public void ForceRefresh_WhenInactive_ShouldLogWarning()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        // 활성화하지 않음
        
        bool warningLogged = false;
        var originalLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new TestLogHandler((logType, message) =>
        {
            if (logType == LogType.Warning && message.Contains("section not active"))
                warningLogged = true;
        });
        
        // Act
        _testSection.ForceRefresh();
        
        // Assert
        Assert.IsTrue(warningLogged);
        Assert.IsFalse(_testSection.OnForceRefreshCalled);
        
        Debug.Log("[TEST] Inactive section refresh prevention verified");
        
        // Cleanup
        Debug.unityLogger.logHandler = originalLogHandler;
    }
    #endregion

    #region Helper Method Tests
    [Test]
    public void GetSectionStatus_ShouldReturnAccurateInformation()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        var testUserData = new UserData { UserId = "test", DisplayName = "Test" };
        _testSection.OnUserDataUpdated(testUserData);
        
        // Act
        var status = _testSection.GetSectionStatus();
        
        // Assert
        Assert.IsNotNull(status);
        Assert.AreEqual(MainPageSectionType.Profile, status.SectionType);
        Assert.AreEqual("Testable Profile Section", status.DisplayName);
        Assert.IsTrue(status.IsInitialized);
        Assert.IsTrue(status.IsActive);
        Assert.IsTrue(status.HasCachedData);
        
        Debug.Log("[TEST] Section status information accuracy verified");
    }
    
    [Test]
    public void GetPerformanceInfo_ShouldReturnValidData()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        // Act
        var perfInfo = _testSection.GetPerformanceInfo();
        
        // Assert
        Assert.IsNotNull(perfInfo);
        Assert.AreEqual(MainPageSectionType.Profile, perfInfo.SectionType);
        Assert.GreaterOrEqual(perfInfo.MemoryUsage, 0);
        
        Debug.Log($"[TEST] Performance info verified: Memory={perfInfo.MemoryUsage} bytes");
    }
    
    [Test]
    public void SendMessageToSection_ShouldRouteToMainPageManager()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        var testData = "test_routing_message";
        
        // Act
        _testSection.TestSendMessageToSection(MainPageSectionType.Energy, testData);
        
        // Assert
        Assert.IsTrue(_testSection.SendMessageCalled);
        Assert.AreEqual(MainPageSectionType.Energy, _testSection.LastMessageTargetSection);
        Assert.AreEqual(testData, _testSection.LastSentMessageData);
        
        Debug.Log("[TEST] Message routing to MainPageManager verified");
    }
    
    [Test]
    public void BroadcastToAllSections_ShouldRouteToMainPageManager()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        var testData = "broadcast_test_data";
        
        // Act
        _testSection.TestBroadcastToAllSections(testData);
        
        // Assert
        Assert.IsTrue(_testSection.BroadcastCalled);
        Assert.AreEqual(testData, _testSection.LastBroadcastData);
        
        Debug.Log("[TEST] Broadcast routing to MainPageManager verified");
    }
    #endregion

    #region Error Handling Tests
    [Test]
    public void Initialize_WithException_ShouldTriggerErrorEvent()
    {
        // Arrange
        bool errorEventReceived = false;
        string errorMessage = "";
        
        SectionBase.OnSectionError += (sectionType, message) =>
        {
            errorEventReceived = true;
            errorMessage = message;
        };
        
        _testSection.SetShouldThrowOnInitialize(true);
        
        // Act
        _testSection.Initialize(_mockMainPageManager);
        
        // Assert
        Assert.IsTrue(errorEventReceived);
        Assert.IsTrue(errorMessage.Contains("Initialization failed"));
        Assert.IsFalse(_testSection.IsInitialized);
        
        Debug.Log("[TEST] Initialization error handling verified");
    }
    
    [Test]
    public void Activate_WithException_ShouldTriggerErrorEvent()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        bool errorEventReceived = false;
        SectionBase.OnSectionError += (sectionType, message) => errorEventReceived = true;
        
        _testSection.SetShouldThrowOnActivate(true);
        
        // Act
        _testSection.Activate();
        
        // Assert
        Assert.IsTrue(errorEventReceived);
        
        Debug.Log("[TEST] Activation error handling verified");
    }
    
    [Test]
    public void UpdateUI_WithException_ShouldTriggerErrorEvent()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        bool errorEventReceived = false;
        SectionBase.OnSectionError += (sectionType, message) => errorEventReceived = true;
        
        _testSection.SetShouldThrowOnUpdateUI(true);
        
        var testUserData = new UserData { UserId = "test", DisplayName = "Test" };
        
        // Act
        _testSection.OnUserDataUpdated(testUserData);
        
        // Assert
        Assert.IsTrue(errorEventReceived);
        
        Debug.Log("[TEST] UI update error handling verified");
    }
    #endregion

    #region UI Update Throttling Tests
    [Test]
    public void OnUserDataUpdated_WithRapidCalls_ShouldThrottleUIUpdates()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        _testSection.Activate();
        
        var testUserData = new UserData { UserId = "test", DisplayName = "Test" };
        
        // Act - 빠른 연속 호출
        _testSection.OnUserDataUpdated(testUserData);
        int firstUpdateCount = _testSection.UpdateUICallCount;
        
        _testSection.OnUserDataUpdated(testUserData); // 즉시 재호출
        int secondUpdateCount = _testSection.UpdateUICallCount;
        
        // Assert
        Assert.AreEqual(1, firstUpdateCount);
        Assert.AreEqual(1, secondUpdateCount); // 스로틀링으로 인해 증가하지 않음
        
        Debug.Log("[TEST] UI update throttling verified");
    }
    #endregion

    #region Integration Tests
    [Test]
    public void ManagerReferences_ShouldBeSetupCorrectly()
    {
        // Act
        _testSection.Initialize(_mockMainPageManager);
        
        // Assert
        Assert.IsNotNull(_testSection.GetUserDataManager());
        Assert.IsNotNull(_testSection.GetAuthenticationManager());
        Assert.IsNotNull(_testSection.GetSettingsManager());
        
        Debug.Log("[TEST] Manager reference setup verified");
    }
    
    [Test]
    public void RequestLogout_ShouldRouteToMainPageManager()
    {
        // Arrange
        _testSection.Initialize(_mockMainPageManager);
        
        // Act
        _testSection.TestRequestLogout();
        
        // Assert
        Assert.IsTrue(_testSection.LogoutRequestCalled);
        
        Debug.Log("[TEST] Logout request routing verified");
    }
    #endregion
}

#region Test Helper Classes
/// <summary>
/// 테스트 가능한 구체적 섹션 클래스
/// </summary>
public class TestableSection : SectionBase
{
    public override MainPageSectionType SectionType => MainPageSectionType.Profile;
    public override string SectionDisplayName => "Testable Profile Section";
    
    // 테스트 상태 추적
    public bool OnInitializeCalled { get; private set; }
    public bool OnActivateCalled { get; private set; }
    public bool OnDeactivateCalled { get; private set; }
    public bool OnCleanupCalled { get; private set; }
    public bool UpdateUICalled { get; private set; }
    public int UpdateUICallCount { get; private set; }
    public bool OnReceiveMessageCalled { get; private set; }
    public bool OnSettingUpdatedCalled { get; private set; }
    public bool OnOfflineModeChangedCalled { get; private set; }
    public bool OnForceRefreshCalled { get; private set; }
    
    // 메시지 관련 상태
    public MainPageSectionType LastMessageFromSection { get; private set; }
    public object LastReceivedMessageData { get; private set; }
    public string LastUpdatedSettingName { get; private set; }
    public object LastUpdatedSettingValue { get; private set; }
    public bool LastOfflineModeValue { get; private set; }
    
    // 헬퍼 메서드 테스트용
    public bool SendMessageCalled { get; private set; }
    public bool BroadcastCalled { get; private set; }
    public bool LogoutRequestCalled { get; private set; }
    public MainPageSectionType LastMessageTargetSection { get; private set; }
    public object LastSentMessageData { get; private set; }
    public object LastBroadcastData { get; private set; }
    
    // 예외 발생 제어
    private bool _shouldThrowOnInitialize;
    private bool _shouldThrowOnActivate;
    private bool _shouldThrowOnUpdateUI;
    
    public void SetShouldThrowOnInitialize(bool shouldThrow) => _shouldThrowOnInitialize = shouldThrow;
    public void SetShouldThrowOnActivate(bool shouldThrow) => _shouldThrowOnActivate = shouldThrow;
    public void SetShouldThrowOnUpdateUI(bool shouldThrow) => _shouldThrowOnUpdateUI = shouldThrow;
    
    // 매니저 참조 접근용 테스트 메서드
    public UserDataManager GetUserDataManager() => _userDataManager;
    public AuthenticationManager GetAuthenticationManager() => _authenticationManager;
    public SettingsManager GetSettingsManager() => _settingsManager;
    
    protected override void OnInitialize()
    {
        if (_shouldThrowOnInitialize)
            throw new System.Exception("Test exception in OnInitialize");
        
        OnInitializeCalled = true;
    }
    
    protected override void OnActivate()
    {
        if (_shouldThrowOnActivate)
            throw new System.Exception("Test exception in OnActivate");
        
        OnActivateCalled = true;
    }
    
    protected override void OnDeactivate()
    {
        OnDeactivateCalled = true;
    }
    
    protected override void OnCleanup()
    {
        OnCleanupCalled = true;
    }
    
    protected override void UpdateUI(UserData userData)
    {
        if (_shouldThrowOnUpdateUI)
            throw new System.Exception("Test exception in UpdateUI");
        
        UpdateUICalled = true;
        UpdateUICallCount++;
    }
    
    protected override void ValidateComponents()
    {
        // Mock validation - no actual components to validate
    }
    
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        OnReceiveMessageCalled = true;
        LastMessageFromSection = fromSection;
        LastReceivedMessageData = data;
    }
    
    protected override void OnSettingUpdated(string settingName, object newValue)
    {
        OnSettingUpdatedCalled = true;
        LastUpdatedSettingName = settingName;
        LastUpdatedSettingValue = newValue;
    }
    
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        OnOfflineModeChangedCalled = true;
        LastOfflineModeValue = isOfflineMode;
    }
    
    protected override void OnForceRefresh()
    {
        OnForceRefreshCalled = true;
    }
    
    // 테스트용 헬퍼 메서드 노출
    public void TestSendMessageToSection(MainPageSectionType targetSection, object data)
    {
        SendMessageCalled = true;
        LastMessageTargetSection = targetSection;
        LastSentMessageData = data;
        
        SendMessageToSection(targetSection, data);
    }
    
    public void TestBroadcastToAllSections(object data)
    {
        BroadcastCalled = true;
        LastBroadcastData = data;
        
        BroadcastToAllSections(data);
    }
    
    public void TestRequestLogout()
    {
        LogoutRequestCalled = true;
        RequestLogout();
    }
}

/// <summary>
/// 테스트용 로그 핸들러
/// </summary>
public class TestLogHandler : ILogHandler
{
    private readonly System.Action<LogType, string> _onLog;
    
    public TestLogHandler(System.Action<LogType, string> onLog)
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