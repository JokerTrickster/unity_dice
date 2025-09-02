using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// SettingsIntegration 통합 테스트
/// </summary>
public class SettingsIntegrationTests
{
    private GameObject _testGameObject;
    private SettingsIntegration _integration;

    [SetUp]
    public void Setup()
    {
        // 테스트용 GameObject 생성
        _testGameObject = new GameObject("TestSettingsIntegration");
        _integration = _testGameObject.AddComponent<SettingsIntegration>();
        
        // PlayerPrefs 초기화
        PlayerPrefs.DeleteAll();
    }

    [TearDown]
    public void TearDown()
    {
        // 이벤트 정리
        SettingsIntegration.OnIntegrationInitialized = null;
        SettingsIntegration.OnSettingChanged = null;
        SettingsIntegration.OnSystemStatusChanged = null;
        
        // GameObject 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // PlayerPrefs 정리
        PlayerPrefs.DeleteAll();
    }

    [UnityTest]
    public IEnumerator Integration_ShouldInitializeAllComponents()
    {
        // Wait for initialization
        yield return new WaitForSeconds(0.1f);

        // Assert
        Assert.IsTrue(_integration.IsInitialized, "Integration should be initialized");
        Assert.IsNotNull(_integration.MainPageSettings, "MainPageSettings should be created");
        Assert.IsNotNull(_integration.LogoutHandler, "LogoutHandler should be created");
        Assert.IsNotNull(_integration.TermsHandler, "TermsHandler should be created");
        Assert.IsTrue(_integration.AllComponentsAvailable, "All components should be available");
    }

    [UnityTest]
    public IEnumerator ToggleMusic_ShouldFireSettingChangedEvent()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        bool eventFired = false;
        string eventKey = "";
        object eventValue = null;
        
        SettingsIntegration.OnSettingChanged += (key, value) =>
        {
            eventFired = true;
            eventKey = key;
            eventValue = value;
        };

        // Act
        _integration.ToggleMusic(false);

        // Assert
        Assert.IsTrue(eventFired, "Setting changed event should fire");
        Assert.AreEqual("MusicEnabled", eventKey, "Event key should be MusicEnabled");
        Assert.AreEqual(false, eventValue, "Event value should be false");
    }

    [UnityTest]
    public IEnumerator ToggleSound_ShouldFireSettingChangedEvent()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        bool eventFired = false;
        string eventKey = "";
        object eventValue = null;
        
        SettingsIntegration.OnSettingChanged += (key, value) =>
        {
            eventFired = true;
            eventKey = key;
            eventValue = value;
        };

        // Act
        _integration.ToggleSound(false);

        // Assert
        Assert.IsTrue(eventFired, "Setting changed event should fire");
        Assert.AreEqual("SoundEnabled", eventKey, "Event key should be SoundEnabled");
        Assert.AreEqual(false, eventValue, "Event value should be false");
    }

    [UnityTest]
    public IEnumerator GetCurrentSettings_ShouldReturnValidStatus()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization

        // Act
        var settings = _integration.GetCurrentSettings();

        // Assert
        Assert.IsNotNull(settings, "Settings status should not be null");
        Assert.IsTrue(settings.IsInitialized, "Settings should be initialized");
        Assert.IsNotNull(settings.SystemStatus, "System status should not be null");
        Assert.IsTrue(settings.MusicEnabled, "Music should be enabled by default");
        Assert.IsTrue(settings.SoundEnabled, "Sound should be enabled by default");
    }

    [UnityTest]
    public IEnumerator ResetAllSettings_ShouldFireResetEvent()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        bool eventFired = false;
        string eventKey = "";
        
        SettingsIntegration.OnSettingChanged += (key, value) =>
        {
            if (key == "SettingsReset")
            {
                eventFired = true;
                eventKey = key;
            }
        };

        // Act
        _integration.ResetAllSettings();

        // Assert
        Assert.IsTrue(eventFired, "Settings reset event should fire");
        Assert.AreEqual("SettingsReset", eventKey, "Event key should be SettingsReset");
    }

    [UnityTest]
    public IEnumerator RefreshIntegration_ShouldFireRefreshEvent()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        bool eventFired = false;
        string eventKey = "";
        
        SettingsIntegration.OnSettingChanged += (key, value) =>
        {
            if (key == "IntegrationRefreshed")
            {
                eventFired = true;
                eventKey = key;
            }
        };

        // Act
        _integration.RefreshIntegration();

        // Assert
        Assert.IsTrue(eventFired, "Integration refresh event should fire");
        Assert.AreEqual("IntegrationRefreshed", eventKey, "Event key should be IntegrationRefreshed");
    }

    [Test]
    public void Singleton_ShouldReturnSameInstance()
    {
        // Act
        var instance1 = SettingsIntegration.Instance;
        var instance2 = SettingsIntegration.Instance;

        // Assert
        Assert.AreSame(instance1, instance2, "Singleton should return same instance");
        Assert.IsNotNull(instance1, "Instance should not be null");
    }

    [UnityTest]
    public IEnumerator SystemStatusChange_ShouldFireEvent()
    {
        // Arrange
        bool eventFired = false;
        SystemStatus receivedStatus = null;
        
        SettingsIntegration.OnSystemStatusChanged += (status) =>
        {
            eventFired = true;
            receivedStatus = status;
        };

        // Wait for initialization to complete
        yield return new WaitForSeconds(0.2f);

        // Assert
        Assert.IsTrue(eventFired, "System status changed event should fire during initialization");
        Assert.IsNotNull(receivedStatus, "Received status should not be null");
        Assert.IsTrue(receivedStatus.IsIntegrationInitialized, "Integration should be initialized");
    }

    [UnityTest]
    public IEnumerator MultipleSettingChanges_ShouldHandleCorrectly()
    {
        // Arrange
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        int eventCount = 0;
        List<string> eventKeys = new List<string>();
        
        SettingsIntegration.OnSettingChanged += (key, value) =>
        {
            eventCount++;
            eventKeys.Add(key);
        };

        // Act
        _integration.ToggleMusic(false);
        _integration.ToggleSound(false);
        _integration.ToggleMusic(true);
        _integration.ToggleSound(true);

        // Assert
        Assert.AreEqual(4, eventCount, "Should fire 4 events for 4 changes");
        Assert.Contains("MusicEnabled", eventKeys, "Should contain music events");
        Assert.Contains("SoundEnabled", eventKeys, "Should contain sound events");
        
        var finalSettings = _integration.GetCurrentSettings();
        Assert.IsTrue(finalSettings.MusicEnabled, "Final music state should be true");
        Assert.IsTrue(finalSettings.SoundEnabled, "Final sound state should be true");
    }

    [Test]
    public void ComponentAvailability_ShouldBeCheckedCorrectly()
    {
        // Act & Assert
        Assert.IsNotNull(_integration.MainPageSettings, "MainPageSettings should be available");
        Assert.IsNotNull(_integration.LogoutHandler, "LogoutHandler should be available");
        Assert.IsNotNull(_integration.TermsHandler, "TermsHandler should be available");
        
        // These should be available after component creation
        Assert.IsTrue(_integration.AllComponentsAvailable, "All components should be available");
    }

    [UnityTest]
    public IEnumerator InitializationEvent_ShouldFireOnce()
    {
        // Arrange
        int eventCount = 0;
        SettingsIntegration.OnIntegrationInitialized += () => eventCount++;

        // Create new integration to trigger initialization
        var newGameObject = new GameObject("NewIntegration");
        var newIntegration = newGameObject.AddComponent<SettingsIntegration>();

        // Wait for initialization
        yield return new WaitForSeconds(0.2f);

        // Assert
        Assert.AreEqual(1, eventCount, "Initialization event should fire exactly once");

        // Cleanup
        Object.DestroyImmediate(newGameObject);
    }
}