using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MainPageSettings 단위 테스트
/// </summary>
public class MainPageSettingsTests
{
    private GameObject _testGameObject;
    private MainPageSettings _settings;

    [SetUp]
    public void Setup()
    {
        // 테스트용 GameObject 생성
        _testGameObject = new GameObject("TestMainPageSettings");
        _settings = _testGameObject.AddComponent<MainPageSettings>();
        
        // PlayerPrefs 초기화
        PlayerPrefs.DeleteKey("MusicEnabled");
        PlayerPrefs.DeleteKey("SoundEnabled");
        PlayerPrefs.DeleteKey("LastSettingsUpdate");
    }

    [TearDown]
    public void TearDown()
    {
        // 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // PlayerPrefs 정리
        PlayerPrefs.DeleteKey("MusicEnabled");
        PlayerPrefs.DeleteKey("SoundEnabled");
        PlayerPrefs.DeleteKey("LastSettingsUpdate");
    }

    [Test]
    public void Settings_InitialValues_ShouldBeDefaultTrue()
    {
        // Act & Assert
        Assert.IsTrue(_settings.IsMusicEnabled, "Music should be enabled by default");
        Assert.IsTrue(_settings.IsSoundEnabled, "Sound should be enabled by default");
    }

    [Test]
    public void MusicSetting_WhenChanged_ShouldPersist()
    {
        // Arrange
        bool testValue = false;

        // Act
        _settings.IsMusicEnabled = testValue;

        // Assert
        Assert.AreEqual(testValue, _settings.IsMusicEnabled);
        Assert.AreEqual(testValue ? 1 : 0, PlayerPrefs.GetInt("MusicEnabled"));
    }

    [Test]
    public void SoundSetting_WhenChanged_ShouldPersist()
    {
        // Arrange
        bool testValue = false;

        // Act
        _settings.IsSoundEnabled = testValue;

        // Assert
        Assert.AreEqual(testValue, _settings.IsSoundEnabled);
        Assert.AreEqual(testValue ? 1 : 0, PlayerPrefs.GetInt("SoundEnabled"));
    }

    [Test]
    public void OnMusicToggleChanged_ShouldFireEvent()
    {
        // Arrange
        bool eventFired = false;
        bool eventValue = false;
        MainPageSettings.OnMusicSettingChanged += (value) =>
        {
            eventFired = true;
            eventValue = value;
        };

        // Act
        _settings.OnMusicToggleChanged(false);

        // Assert
        Assert.IsTrue(eventFired, "Music setting changed event should fire");
        Assert.IsFalse(eventValue, "Event should carry the correct value");
        
        // Cleanup
        MainPageSettings.OnMusicSettingChanged = null;
    }

    [Test]
    public void OnSoundToggleChanged_ShouldFireEvent()
    {
        // Arrange
        bool eventFired = false;
        bool eventValue = false;
        MainPageSettings.OnSoundSettingChanged += (value) =>
        {
            eventFired = true;
            eventValue = value;
        };

        // Act
        _settings.OnSoundToggleChanged(false);

        // Assert
        Assert.IsTrue(eventFired, "Sound setting changed event should fire");
        Assert.IsFalse(eventValue, "Event should carry the correct value");
        
        // Cleanup
        MainPageSettings.OnSoundSettingChanged = null;
    }

    [Test]
    public void ResetToDefaults_ShouldRestoreDefaultValues()
    {
        // Arrange
        _settings.IsMusicEnabled = false;
        _settings.IsSoundEnabled = false;

        // Act
        _settings.ResetToDefaults();

        // Assert
        Assert.IsTrue(_settings.IsMusicEnabled, "Music should be reset to default (true)");
        Assert.IsTrue(_settings.IsSoundEnabled, "Sound should be reset to default (true)");
    }

    [Test]
    public void GetStatus_ShouldReturnCorrectInformation()
    {
        // Act
        var status = _settings.GetStatus();

        // Assert
        Assert.IsNotNull(status, "Status should not be null");
        Assert.IsTrue(status.IsMusicEnabled, "Status should reflect current music setting");
        Assert.IsTrue(status.IsSoundEnabled, "Status should reflect current sound setting");
    }

    [UnityTest]
    public IEnumerator LoadSettings_ShouldRestoreFromPersistence()
    {
        // Arrange
        PlayerPrefs.SetInt("MusicEnabled", 0);
        PlayerPrefs.SetInt("SoundEnabled", 0);
        PlayerPrefs.Save();

        // Act
        _settings.LoadSettings();
        yield return null; // Wait one frame

        // Assert
        Assert.IsFalse(_settings.IsMusicEnabled, "Music setting should be loaded from persistence");
        Assert.IsFalse(_settings.IsSoundEnabled, "Sound setting should be loaded from persistence");
    }

    [Test]
    public void SettingsInitialized_EventShouldFire()
    {
        // Arrange
        bool eventFired = false;
        MainPageSettings.OnSettingsInitialized += () => eventFired = true;

        // Create new settings component to trigger initialization
        var newGameObject = new GameObject("NewSettings");
        var newSettings = newGameObject.AddComponent<MainPageSettings>();

        // Wait for initialization
        for (int i = 0; i < 10 && !eventFired; i++)
        {
            System.Threading.Thread.Sleep(100);
        }

        // Assert
        Assert.IsTrue(eventFired, "Settings initialized event should fire");

        // Cleanup
        MainPageSettings.OnSettingsInitialized = null;
        Object.DestroyImmediate(newGameObject);
    }

    [Test]
    public void MultipleToggleChanges_ShouldHandleCorrectly()
    {
        // Arrange
        int musicEventCount = 0;
        int soundEventCount = 0;
        
        MainPageSettings.OnMusicSettingChanged += (value) => musicEventCount++;
        MainPageSettings.OnSoundSettingChanged += (value) => soundEventCount++;

        // Act
        _settings.OnMusicToggleChanged(false);
        _settings.OnMusicToggleChanged(true);
        _settings.OnSoundToggleChanged(false);
        _settings.OnSoundToggleChanged(true);

        // Assert
        Assert.AreEqual(2, musicEventCount, "Music events should fire twice");
        Assert.AreEqual(2, soundEventCount, "Sound events should fire twice");
        Assert.IsTrue(_settings.IsMusicEnabled, "Final music state should be true");
        Assert.IsTrue(_settings.IsSoundEnabled, "Final sound state should be true");

        // Cleanup
        MainPageSettings.OnMusicSettingChanged = null;
        MainPageSettings.OnSoundSettingChanged = null;
    }

    [Test]
    public void SaveSettings_ShouldUpdateLastUpdateTime()
    {
        // Arrange
        var beforeSave = System.DateTime.Now;

        // Act
        _settings.SaveSettings();
        
        // Assert
        var status = _settings.GetStatus();
        Assert.IsTrue(status.LastUpdateTime >= beforeSave, "Last update time should be recent");
        Assert.IsTrue(status.LastUpdateTime <= System.DateTime.Now, "Last update time should not be in future");
    }
}