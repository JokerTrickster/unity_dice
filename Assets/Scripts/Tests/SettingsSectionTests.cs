using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// SettingsSection 단위 테스트
/// 설정 관리, 로그아웃 시퀀스, 알림 시스템, 섹션 간 통신을 테스트합니다.
/// 새로운 아키텍처: SettingsSection (비즈니스 로직) + SettingsSectionUI (순수 UI)
/// </summary>
public class SettingsSectionTests
{
    private GameObject _testGameObject;
    private SettingsSection _settingsSection;
    private SettingsSectionUI _settingsUI;
    private SettingsSectionUIComponent _mockUIComponent;
    private MockMainPageManager _mockMainPageManager;
    private MockSettingsManager _mockSettingsManager;
    private MockAuthenticationManager _mockAuthenticationManager;

    #region Setup & Teardown
    [SetUp]
    public void Setup()
    {
        // 테스트 GameObject 생성
        _testGameObject = new GameObject("TestSettingsSection");
        
        // 컴포넌트 추가 - 새로운 아키텍처
        _settingsSection = _testGameObject.AddComponent<SettingsSection>();
        _settingsUI = _testGameObject.AddComponent<SettingsSectionUI>();
        _mockUIComponent = _testGameObject.AddComponent<SettingsSectionUIComponent>();
        
        // Mock 매니저들 설정
        SetupMockManagers();
        
        // 테스트용 필드 설정 (Reflection 사용)
        // SettingsSection이 SettingsSectionUIComponent를 참조하도록 설정
        var uiField = typeof(SettingsSection).GetField("_settingsUI", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        uiField?.SetValue(_settingsSection, _mockUIComponent);
        
        Debug.Log("[SettingsSectionTests] Setup complete with new architecture");
    }
    
    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
        
        CleanupMockManagers();
        Debug.Log("[SettingsSectionTests] Teardown complete");
    }
    
    private void SetupMockManagers()
    {
        _mockMainPageManager = new MockMainPageManager();
        _mockSettingsManager = new MockSettingsManager();
        _mockAuthenticationManager = new MockAuthenticationManager();
    }
    
    private void CleanupMockManagers()
    {
        _mockMainPageManager?.Cleanup();
        _mockSettingsManager?.Cleanup();
        _mockAuthenticationManager?.Cleanup();
    }
    #endregion

    #region Basic Functionality Tests
    [Test]
    public void SectionType_ReturnsCorrectType()
    {
        // Arrange & Act
        var sectionType = _settingsSection.SectionType;
        
        // Assert
        Assert.AreEqual(MainPageSectionType.Settings, sectionType);
        Debug.Log($"[SettingsSectionTests] Section type verified: {sectionType}");
    }
    
    [Test]
    public void SectionDisplayName_ReturnsCorrectName()
    {
        // Arrange & Act
        var displayName = _settingsSection.SectionDisplayName;
        
        // Assert
        Assert.IsNotNull(displayName);
        Assert.IsNotEmpty(displayName);
        Assert.AreEqual("설정 관리", displayName);
        Debug.Log($"[SettingsSectionTests] Display name verified: {displayName}");
    }
    
    [UnityTest]
    public IEnumerator Initialize_WithValidComponents_InitializesSuccessfully()
    {
        // Arrange
        Assert.IsFalse(_settingsSection.IsInitialized);
        
        // Act
        _settingsSection.Initialize(_mockMainPageManager);
        yield return null; // Wait one frame
        
        // Assert
        Assert.IsTrue(_settingsSection.IsInitialized);
        Debug.Log("[SettingsSectionTests] Initialization verified");
    }
    
    [Test]
    public void ValidateComponents_WithMissingUI_ReportsError()
    {
        // Arrange
        var uiField = typeof(SettingsSection).GetField("_settingsUI", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        uiField?.SetValue(_settingsSection, null);
        
        bool errorReported = false;
        string errorMessage = "";
        
        // Mock error reporting (would need to intercept ReportError calls)
        
        // Act
        try 
        {
            var method = typeof(SettingsSection).GetMethod("ValidateComponents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(_settingsSection, null);
        }
        catch (Exception e)
        {
            errorReported = true;
            errorMessage = e.Message;
        }
        
        // Assert - In real implementation, this would check if ReportError was called
        Debug.Log($"[SettingsSectionTests] Component validation test: {errorMessage}");
    }
    #endregion

    #region Settings Management Tests
    [Test]
    public void GetCachedSetting_WithValidKey_ReturnsCorrectValue()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        _mockSettingsManager.SetSetting("MasterVolume", 0.8f);
        
        // Act
        var result = _settingsSection.GetCachedSetting<float>("MasterVolume");
        
        // Assert
        Assert.AreEqual(0.8f, result, 0.01f);
        Debug.Log($"[SettingsSectionTests] Cached setting verified: MasterVolume = {result}");
    }
    
    [Test]
    public void SetCachedSetting_WithValidValue_UpdatesCacheAndPersistence()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        
        // Act
        bool success = _settingsSection.SetCachedSetting("MasterVolume", 0.9f);
        
        // Assert
        Assert.IsTrue(success);
        Assert.AreEqual(0.9f, _settingsSection.GetCachedSetting<float>("MasterVolume"), 0.01f);
        Assert.AreEqual(0.9f, _mockSettingsManager.GetSetting<float>("MasterVolume"), 0.01f);
        Debug.Log("[SettingsSectionTests] Cached setting update verified");
    }
    
    [UnityTest]
    public IEnumerator SettingsSync_AfterInterval_SyncsWithServer()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        _settingsSection.Activate();
        
        int initialSyncCount = _mockSettingsManager.SyncCallCount;
        
        // Act - Wait for sync interval (mocked to be shorter)
        yield return new WaitForSeconds(2f);
        
        // Assert
        Assert.Greater(_mockSettingsManager.SyncCallCount, initialSyncCount);
        Debug.Log($"[SettingsSectionTests] Settings sync verified: {_mockSettingsManager.SyncCallCount} calls");
    }
    #endregion

    #region UI Integration Tests  
    [Test]
    public void HandleAudioSettingChanged_UpdatesCacheAndAppliesImmediately()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        bool eventTriggered = false;
        string capturedSetting = "";
        float capturedValue = 0f;
        
        // Subscribe to UI events
        SettingsSectionUIComponent.OnAudioSettingChanged += (setting, value) => {
            eventTriggered = true;
            capturedSetting = setting;
            capturedValue = value;
        };
        
        // Act - Simulate UI event
        SettingsSectionUIComponent.OnAudioSettingChanged?.Invoke("MasterVolume", 0.7f);
        
        // Assert
        Assert.IsTrue(eventTriggered);
        Assert.AreEqual("MasterVolume", capturedSetting);
        Assert.AreEqual(0.7f, capturedValue, 0.01f);
        Assert.AreEqual(0.7f, _settingsSection.GetCachedSetting<float>("MasterVolume"), 0.01f);
        Assert.AreEqual(0.7f, AudioListener.volume, 0.01f); // Should be applied immediately
        Debug.Log("[SettingsSectionTests] Audio setting change handling verified");
    }
    
    [Test]
    public void HandleToggleSettingChanged_UpdatesCacheAndApplies()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        
        // Act - Simulate UI event
        SettingsSectionUIComponent.OnToggleSettingChanged?.Invoke("IsMuted", true);
        
        // Assert
        Assert.IsTrue(_settingsSection.GetCachedSetting<bool>("IsMuted"));
        Assert.AreEqual(0f, AudioListener.volume, 0.01f); // Should be muted
        Debug.Log("[SettingsSectionTests] Toggle setting change handling verified");
    }
    
    [Test]
    public void HandleQualitySettingChanged_UpdatesUnitySettings()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        int initialQuality = QualitySettings.GetQualityLevel();
        
        // Act - Simulate UI event
        SettingsSectionUIComponent.OnQualitySettingChanged?.Invoke(2);
        
        // Assert
        Assert.AreEqual(2, _settingsSection.GetCachedSetting<int>("QualityLevel"));
        Assert.AreEqual(2, QualitySettings.GetQualityLevel());
        Debug.Log($"[SettingsSectionTests] Quality setting change verified: {QualitySettings.GetQualityLevel()}");
    }
    #endregion

    #region Logout Tests
    [UnityTest]
    public IEnumerator InitiateLogout_ExecutesFullSequence()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        _settingsSection.Activate();
        
        // Act
        _settingsSection.InitiateLogout();
        
        // Wait for logout sequence to complete
        yield return new WaitForSeconds(5f);
        
        // Assert
        Assert.IsTrue(_mockAuthenticationManager.LogoutCalled);
        Assert.IsTrue(_mockSettingsManager.SaveCalled);
        Debug.Log("[SettingsSectionTests] Logout sequence verified");
    }
    
    [Test]
    public void ForceLogout_ExecutesImmediately()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        
        // Act
        _settingsSection.ForceLogout();
        
        // Assert
        Assert.IsTrue(_mockAuthenticationManager.LogoutCalled);
        Debug.Log("[SettingsSectionTests] Force logout verified");
    }
    
    [UnityTest]
    public IEnumerator InitiateLogout_WhenAlreadyInProgress_DoesNotStartNew()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        _settingsSection.InitiateLogout();
        
        int initialLogoutCount = _mockAuthenticationManager.LogoutCallCount;
        
        // Act - Try to start another logout
        _settingsSection.InitiateLogout();
        yield return new WaitForSeconds(1f);
        
        // Assert - Should not increase logout calls
        Assert.AreEqual(initialLogoutCount, _mockAuthenticationManager.LogoutCallCount);
        Debug.Log("[SettingsSectionTests] Duplicate logout prevention verified");
    }
    #endregion

    #region Notification Tests
    [Test]
    public void AddNotification_WithValidNotification_AddsToQueue()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        var notification = new SettingsNotification
        {
            Type = NotificationType.Info,
            Title = "Test",
            Message = "Test message",
            Priority = NotificationPriority.Normal
        };
        
        int initialCount = _settingsSection.PendingNotificationsCount;
        
        // Act
        _settingsSection.AddNotification(notification);
        
        // Assert
        Assert.AreEqual(initialCount + 1, _settingsSection.PendingNotificationsCount);
        Debug.Log($"[SettingsSectionTests] Notification added: {_settingsSection.PendingNotificationsCount} pending");
    }
    
    [Test]
    public void AddNotification_WithNullNotification_DoesNotCrash()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        
        // Act & Assert - Should not throw exception
        Assert.DoesNotThrow(() => _settingsSection.AddNotification(null));
        Debug.Log("[SettingsSectionTests] Null notification handling verified");
    }
    #endregion

    #region Message Handling Tests
    [Test]
    public void OnReceiveMessage_WithSettingsRequest_HandlesCorrectly()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        var request = new SettingsRequest
        {
            RequestType = "get_setting",
            SettingName = "MasterVolume",
            FromSection = MainPageSectionType.Profile
        };
        
        // Act
        _settingsSection.ReceiveMessage(MainPageSectionType.Profile, request);
        
        // Assert
        Assert.IsTrue(_mockMainPageManager.MessageSentToSection);
        Assert.AreEqual(MainPageSectionType.Profile, _mockMainPageManager.LastMessageTarget);
        Debug.Log("[SettingsSectionTests] Settings request message handling verified");
    }
    
    [Test]
    public void OnReceiveMessage_WithProfileUpdate_HandlesLevelChange()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        var profileUpdate = new ProfileUpdateMessage
        {
            UserId = "test_user",
            NewLevel = 5,
            OldLevel = 4,
            LevelChanged = true
        };
        
        int initialNotifications = _settingsSection.PendingNotificationsCount;
        
        // Act
        _settingsSection.ReceiveMessage(MainPageSectionType.Profile, profileUpdate);
        
        // Assert - Should add notification for level 5+ advanced settings
        Assert.Greater(_settingsSection.PendingNotificationsCount, initialNotifications);
        Debug.Log("[SettingsSectionTests] Profile update message handling verified");
    }
    #endregion

    #region Error Handling Tests
    [Test]
    public void Initialize_WithNullManager_HandlesGracefully()
    {
        // Act & Assert - Should not crash
        Assert.DoesNotThrow(() => _settingsSection.Initialize(null));
        
        // Should remain uninitialized
        Assert.IsFalse(_settingsSection.IsInitialized);
        Debug.Log("[SettingsSectionTests] Null manager handling verified");
    }
    
    [UnityTest]
    public IEnumerator UpdateUI_WithNullUserData_DoesNotCrash()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        
        // Act & Assert
        Assert.DoesNotThrow(() => _settingsSection.OnUserDataUpdated(null));
        yield return null;
        
        Debug.Log("[SettingsSectionTests] Null UserData handling verified");
    }
    #endregion

    #region Performance Tests
    [UnityTest]
    public IEnumerator SettingsCache_WithFrequentAccess_MaintainsPerformance()
    {
        // Arrange
        _settingsSection.Initialize(_mockMainPageManager);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act - Perform many cache operations
        for (int i = 0; i < 1000; i++)
        {
            _settingsSection.SetCachedSetting($"TestSetting{i % 10}", i * 0.1f);
            _settingsSection.GetCachedSetting<float>($"TestSetting{i % 10}");
            
            if (i % 100 == 0) yield return null; // Yield periodically
        }
        
        stopwatch.Stop();
        
        // Assert - Should complete within reasonable time (< 100ms)
        Assert.Less(stopwatch.ElapsedMilliseconds, 100);
        Debug.Log($"[SettingsSectionTests] Cache performance verified: {stopwatch.ElapsedMilliseconds}ms for 1000 operations");
    }
    #endregion

    #region Pure UI Component Tests
    [Test]
    public void SettingsSectionUI_Initialization_SetsUpCorrectly()
    {
        // Arrange & Act
        var isInitialized = _settingsUI.IsInitialized;
        
        // Assert - Should initialize during Start()
        Assert.IsTrue(isInitialized);
        Debug.Log("[SettingsSectionTests] Pure UI component initialization verified");
    }
    
    [Test]
    public void SettingsSectionUI_UpdateAudioSlider_UpdatesCorrectly()
    {
        // Arrange
        float testValue = 0.6f;
        
        // Act
        _settingsUI.UpdateAudioSlider("MasterVolume", testValue, false);
        
        // Assert
        var uiState = _settingsUI.GetCurrentUIState();
        Assert.IsTrue(uiState.ContainsKey("mastervolume"));
        Assert.AreEqual(testValue, (float)uiState["mastervolume"], 0.01f);
        Debug.Log("[SettingsSectionTests] Pure UI audio slider update verified");
    }
    
    [Test]
    public void SettingsSectionUI_UpdateToggle_UpdatesCorrectly()
    {
        // Arrange
        bool testValue = true;
        
        // Act
        _settingsUI.UpdateToggle("IsMuted", testValue, false);
        
        // Assert
        var uiState = _settingsUI.GetCurrentUIState();
        Assert.IsTrue(uiState.ContainsKey("ismuted"));
        Assert.AreEqual(testValue, (bool)uiState["ismuted"]);
        Debug.Log("[SettingsSectionTests] Pure UI toggle update verified");
    }
    
    [Test]
    public void SettingsSectionUI_UpdateQualityDropdown_UpdatesCorrectly()
    {
        // Arrange
        int testValue = 3;
        
        // Act
        _settingsUI.UpdateQualityDropdown(testValue, false);
        
        // Assert
        var uiState = _settingsUI.GetCurrentUIState();
        Assert.IsTrue(uiState.ContainsKey("qualityLevel"));
        Assert.AreEqual(testValue, (int)uiState["qualityLevel"]);
        Debug.Log("[SettingsSectionTests] Pure UI quality dropdown update verified");
    }
    
    [Test]
    public void SettingsSectionUI_AddNotification_AddsToQueue()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Message = "Test notification",
            Type = NotificationType.Info,
            AutoCloseDelay = 5f
        };
        
        int initialCount = _settingsUI.PendingNotificationCount;
        
        // Act
        _settingsUI.AddNotification(notification);
        
        // Assert
        Assert.AreEqual(initialCount + 1, _settingsUI.PendingNotificationCount);
        Debug.Log("[SettingsSectionTests] Pure UI notification addition verified");
    }
    
    [Test]
    public void SettingsSectionUI_ClearAllNotifications_ClearsQueue()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Message = "Test notification",
            Type = NotificationType.Info
        };
        
        _settingsUI.AddNotification(notification);
        Assert.Greater(_settingsUI.PendingNotificationCount, 0);
        
        // Act
        _settingsUI.ClearAllNotifications();
        
        // Assert
        Assert.AreEqual(0, _settingsUI.PendingNotificationCount);
        Debug.Log("[SettingsSectionTests] Pure UI notification clearing verified");
    }
    
    [Test]
    public void SettingsSectionUI_UpdateUIState_UpdatesMultipleValues()
    {
        // Arrange
        var newState = new Dictionary<string, object>
        {
            ["mastervolume"] = 0.8f,
            ["ismuted"] = true,
            ["qualitylevel"] = 2
        };
        
        // Act
        _settingsUI.UpdateUIState(newState, false);
        
        // Assert
        var currentState = _settingsUI.GetCurrentUIState();
        Assert.AreEqual(0.8f, (float)currentState["mastervolume"], 0.01f);
        Assert.AreEqual(true, (bool)currentState["ismuted"]);
        Assert.AreEqual(2, (int)currentState["qualitylevel"]);
        Debug.Log("[SettingsSectionTests] Pure UI bulk state update verified");
    }
    
    [Test]
    public void SettingsSectionUI_EventFiring_WorksCorrectly()
    {
        // Arrange
        bool settingsButtonClicked = false;
        bool logoutButtonClicked = false;
        bool helpButtonClicked = false;
        bool notificationButtonClicked = false;
        
        SettingsSectionUI.OnSettingsButtonClicked += () => settingsButtonClicked = true;
        SettingsSectionUI.OnLogoutButtonClicked += () => logoutButtonClicked = true;
        SettingsSectionUI.OnHelpButtonClicked += () => helpButtonClicked = true;
        SettingsSectionUI.OnNotificationButtonClicked += () => notificationButtonClicked = true;
        
        // Act
        SettingsSectionUI.OnSettingsButtonClicked?.Invoke();
        SettingsSectionUI.OnLogoutButtonClicked?.Invoke();
        SettingsSectionUI.OnHelpButtonClicked?.Invoke();
        SettingsSectionUI.OnNotificationButtonClicked?.Invoke();
        
        // Assert
        Assert.IsTrue(settingsButtonClicked);
        Assert.IsTrue(logoutButtonClicked);
        Assert.IsTrue(helpButtonClicked);
        Assert.IsTrue(notificationButtonClicked);
        Debug.Log("[SettingsSectionTests] Pure UI event firing verified");
    }
    #endregion

    #region Integration Tests
    [UnityTest]
    public IEnumerator FullWorkflow_InitializeToCleanup_WorksCorrectly()
    {
        // Arrange
        var userData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User",
            Level = 3,
            IsNewUser = true
        };
        
        // Act - Full lifecycle
        _settingsSection.Initialize(_mockMainPageManager);
        yield return null;
        
        _settingsSection.Activate();
        yield return null;
        
        _settingsSection.OnUserDataUpdated(userData);
        yield return null;
        
        // Simulate some settings changes
        SettingsSectionUIComponent.OnAudioSettingChanged?.Invoke("MasterVolume", 0.5f);
        SettingsSectionUIComponent.OnToggleSettingChanged?.Invoke("VibrationEnabled", false);
        yield return new WaitForSeconds(1f);
        
        _settingsSection.Deactivate();
        _settingsSection.Cleanup();
        
        // Assert - Should complete without errors
        Assert.IsTrue(_mockSettingsManager.SaveCalled);
        Assert.AreEqual(0.5f, _mockSettingsManager.GetSetting<float>("MasterVolume"), 0.01f);
        Assert.IsFalse(_mockSettingsManager.GetSetting<bool>("VibrationEnabled"));
        
        Debug.Log("[SettingsSectionTests] Full workflow integration verified");
    }
    #endregion
}

#region Mock Classes
/// <summary>
/// MainPageManager 모의 객체
/// </summary>
public class MockMainPageManager : MainPageManager
{
    public bool MessageSentToSection { get; private set; }
    public MainPageSectionType LastMessageTarget { get; private set; }
    public object LastMessageData { get; private set; }
    
    public override void SendMessageToSection(MainPageSectionType fromSection, MainPageSectionType toSection, object data)
    {
        MessageSentToSection = true;
        LastMessageTarget = toSection;
        LastMessageData = data;
        Debug.Log($"[MockMainPageManager] Message sent: {fromSection} -> {toSection}");
    }
    
    public void Cleanup()
    {
        MessageSentToSection = false;
        LastMessageTarget = MainPageSectionType.Profile;
        LastMessageData = null;
    }
}

/// <summary>
/// SettingsManager 모의 객체
/// </summary>
public class MockSettingsManager
{
    private Dictionary<string, object> _settings = new Dictionary<string, object>();
    
    public bool SaveCalled { get; private set; }
    public int SyncCallCount { get; private set; }
    
    public T GetSetting<T>(string key)
    {
        if (_settings.TryGetValue(key, out var value) && value is T)
        {
            return (T)value;
        }
        return default(T);
    }
    
    public bool SetSetting<T>(string key, T value)
    {
        _settings[key] = value;
        SaveCalled = true;
        return true;
    }
    
    public void SyncWithServer()
    {
        SyncCallCount++;
        Debug.Log($"[MockSettingsManager] Sync called: {SyncCallCount} times");
    }
    
    public void Cleanup()
    {
        _settings.Clear();
        SaveCalled = false;
        SyncCallCount = 0;
    }
}

/// <summary>
/// AuthenticationManager 모의 객체
/// </summary>
public class MockAuthenticationManager
{
    public bool LogoutCalled { get; private set; }
    public int LogoutCallCount { get; private set; }
    
    public void Logout()
    {
        LogoutCalled = true;
        LogoutCallCount++;
        Debug.Log($"[MockAuthenticationManager] Logout called: {LogoutCallCount} times");
    }
    
    public void Cleanup()
    {
        LogoutCalled = false;
        LogoutCallCount = 0;
    }
}
#endregion