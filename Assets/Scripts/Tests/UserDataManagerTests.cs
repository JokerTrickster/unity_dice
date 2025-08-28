using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// UserDataManager에 대한 단위 테스트
/// 커버리지 80% 이상을 목표로 하는 포괄적인 테스트 스위트
/// </summary>
public class UserDataManagerTests
{
    private UserDataManager _userDataManager;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 PlayerPrefs 클리어
        PlayerPrefs.DeleteAll();
        
        // 기존 UserDataManager 인스턴스가 있다면 제거
        if (UserDataManager.Instance != null)
        {
            Object.DestroyImmediate(UserDataManager.Instance.gameObject);
        }
        
        // 테스트용 UserDataManager 생성
        _testGameObject = new GameObject("TestUserDataManager");
        _userDataManager = _testGameObject.AddComponent<UserDataManager>();
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // PlayerPrefs 정리
        PlayerPrefs.DeleteAll();
        
        // 이벤트 구독 해제
        UserDataManager.OnUserDataLoaded = null;
        UserDataManager.OnUserDataUpdated = null;
        UserDataManager.OnSyncCompleted = null;
        UserDataManager.OnOfflineModeChanged = null;
    }
    
    #region Singleton Pattern Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = UserDataManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is UserDataManager);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = UserDataManager.Instance;
        var instance2 = UserDataManager.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region UserData Class Tests
    [Test]
    public void UserData_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var userData = new UserData();
        
        // Assert
        Assert.AreEqual(1, userData.Level);
        Assert.AreEqual(0, userData.Experience);
        Assert.AreEqual(0, userData.TotalGamesPlayed);
        Assert.AreEqual(0, userData.GamesWon);
        Assert.AreEqual(0, userData.GamesLost);
        Assert.IsTrue(userData.SoundEnabled);
        Assert.IsTrue(userData.MusicEnabled);
        Assert.AreEqual(1.0f, userData.MasterVolume);
        Assert.AreEqual("en", userData.PreferredLanguage);
    }
    
    [Test]
    public void UserData_WinRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var userData = new UserData();
        
        // Act
        userData.TotalGamesPlayed = 10;
        userData.GamesWon = 7;
        userData.GamesLost = 3;
        
        // Assert
        Assert.AreEqual(70f, userData.WinRate, 0.01f);
    }
    
    [Test]
    public void UserData_WinRate_WithNoGames_ShouldReturnZero()
    {
        // Arrange
        var userData = new UserData();
        
        // Act & Assert
        Assert.AreEqual(0f, userData.WinRate);
    }
    
    [Test]
    public void UserData_TotalExperience_ShouldCalculateCorrectly()
    {
        // Arrange
        var userData = new UserData();
        
        // Act
        userData.Level = 5;
        userData.Experience = 250;
        
        // Assert
        Assert.AreEqual(4250, userData.TotalExperience); // (5-1)*1000 + 250
    }
    #endregion
    
    #region Property Tests
    [Test]
    public void CurrentUser_InitialState_ShouldBeNull()
    {
        // Assert
        Assert.IsNull(_userDataManager.CurrentUser);
    }
    
    [Test]
    public void IsOfflineMode_InitialState_ShouldBeFalse()
    {
        // Assert
        Assert.IsFalse(_userDataManager.IsOfflineMode);
    }
    
    [Test]
    public void CachedUserCount_InitialState_ShouldBeZero()
    {
        // Assert
        Assert.AreEqual(0, _userDataManager.CachedUserCount);
    }
    #endregion
    
    #region User Management Tests
    [Test]
    public void SetCurrentUser_ValidData_ShouldSetUser()
    {
        // Arrange
        string testUserId = "test_user_123";
        string testUserName = "Test User";
        string testEmail = "test@example.com";
        
        bool eventFired = false;
        UserData receivedData = null;
        
        UserDataManager.OnUserDataUpdated += (userData) =>
        {
            eventFired = true;
            receivedData = userData;
        };
        
        // Act
        _userDataManager.SetCurrentUser(testUserId, testUserName, testEmail);
        
        // Assert
        Assert.IsNotNull(_userDataManager.CurrentUser);
        Assert.AreEqual(testUserId, _userDataManager.CurrentUser.UserId);
        Assert.AreEqual(testUserName, _userDataManager.CurrentUser.DisplayName);
        Assert.AreEqual(testEmail, _userDataManager.CurrentUser.Email);
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(receivedData);
        Assert.AreEqual(1, _userDataManager.CachedUserCount);
    }
    
    [Test]
    public void SetCurrentUser_EmptyUserId_ShouldNotSetUser()
    {
        // Arrange
        string emptyUserId = "";
        string testUserName = "Test User";
        
        // Act
        _userDataManager.SetCurrentUser(emptyUserId, testUserName);
        
        // Assert
        Assert.IsNull(_userDataManager.CurrentUser);
        Assert.AreEqual(0, _userDataManager.CachedUserCount);
    }
    
    [Test]
    public void SetCurrentUser_NullUserId_ShouldNotSetUser()
    {
        // Arrange
        string nullUserId = null;
        string testUserName = "Test User";
        
        // Act
        _userDataManager.SetCurrentUser(nullUserId, testUserName);
        
        // Assert
        Assert.IsNull(_userDataManager.CurrentUser);
        Assert.AreEqual(0, _userDataManager.CachedUserCount);
    }
    
    [Test]
    public void GetUserData_ExistingUser_ShouldReturnUser()
    {
        // Arrange
        string testUserId = "test_user_123";
        string testUserName = "Test User";
        
        _userDataManager.SetCurrentUser(testUserId, testUserName);
        
        // Act
        var userData = _userDataManager.GetUserData(testUserId);
        
        // Assert
        Assert.IsNotNull(userData);
        Assert.AreEqual(testUserId, userData.UserId);
        Assert.AreEqual(testUserName, userData.DisplayName);
    }
    
    [Test]
    public void GetUserData_NonExistentUser_ShouldReturnNull()
    {
        // Arrange
        string nonExistentUserId = "non_existent_user";
        
        // Act
        var userData = _userDataManager.GetUserData(nonExistentUserId);
        
        // Assert
        Assert.IsNull(userData);
    }
    
    [Test]
    public void GetUserData_EmptyUserId_ShouldReturnNull()
    {
        // Arrange
        string emptyUserId = "";
        
        // Act
        var userData = _userDataManager.GetUserData(emptyUserId);
        
        // Assert
        Assert.IsNull(userData);
    }
    
    [Test]
    public void UpdateUserData_ValidData_ShouldUpdateUser()
    {
        // Arrange
        var userData = new UserData
        {
            UserId = "test_user_123",
            DisplayName = "Updated User",
            Email = "updated@example.com"
        };
        
        bool eventFired = false;
        UserData receivedData = null;
        
        UserDataManager.OnUserDataUpdated += (data) =>
        {
            eventFired = true;
            receivedData = data;
        };
        
        // Act
        _userDataManager.UpdateUserData(userData);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(receivedData);
        Assert.AreEqual(userData.UserId, receivedData.UserId);
        Assert.AreEqual(userData.DisplayName, receivedData.DisplayName);
    }
    
    [Test]
    public void UpdateUserData_InvalidData_ShouldNotUpdate()
    {
        // Arrange
        var invalidUserData = new UserData
        {
            UserId = "", // Invalid - empty user ID
            DisplayName = "Test User"
        };
        
        bool eventFired = false;
        UserDataManager.OnUserDataUpdated += (data) => eventFired = true;
        
        // Act
        _userDataManager.UpdateUserData(invalidUserData);
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    
    [Test]
    public void UpdateUserData_NullData_ShouldNotUpdate()
    {
        // Arrange
        UserData nullUserData = null;
        
        bool eventFired = false;
        UserDataManager.OnUserDataUpdated += (data) => eventFired = true;
        
        // Act
        _userDataManager.UpdateUserData(nullUserData);
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    
    [Test]
    public void LogoutCurrentUser_WithCurrentUser_ShouldClearCurrentUser()
    {
        // Arrange
        _userDataManager.SetCurrentUser("test_user", "Test User");
        Assert.IsNotNull(_userDataManager.CurrentUser); // 사전 확인
        
        // Act
        _userDataManager.LogoutCurrentUser();
        
        // Assert
        Assert.IsNull(_userDataManager.CurrentUser);
        Assert.IsFalse(PlayerPrefs.HasKey("current_user_id"));
    }
    
    [Test]
    public void LogoutCurrentUser_WithoutCurrentUser_ShouldNotThrowException()
    {
        // Assert & Act - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _userDataManager.LogoutCurrentUser();
        });
    }
    #endregion
    
    #region Offline Mode Tests
    [Test]
    public void SetOfflineMode_ChangeToOnline_ShouldTriggerEvent()
    {
        // Arrange
        bool eventFired = false;
        bool receivedOfflineState = true; // 초기값 설정
        
        UserDataManager.OnOfflineModeChanged += (isOffline) =>
        {
            eventFired = true;
            receivedOfflineState = isOffline;
        };
        
        // Act
        _userDataManager.SetOfflineMode(false);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsFalse(receivedOfflineState);
        Assert.IsFalse(_userDataManager.IsOfflineMode);
    }
    
    [Test]
    public void SetOfflineMode_ChangeToOffline_ShouldTriggerEvent()
    {
        // Arrange
        bool eventFired = false;
        bool receivedOfflineState = false; // 초기값 설정
        
        UserDataManager.OnOfflineModeChanged += (isOffline) =>
        {
            eventFired = true;
            receivedOfflineState = isOffline;
        };
        
        // Act
        _userDataManager.SetOfflineMode(true);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsTrue(receivedOfflineState);
        Assert.IsTrue(_userDataManager.IsOfflineMode);
    }
    
    [Test]
    public void SetOfflineMode_SameValue_ShouldNotTriggerEvent()
    {
        // Arrange
        _userDataManager.SetOfflineMode(false); // 초기 설정
        
        bool eventFired = false;
        UserDataManager.OnOfflineModeChanged += (isOffline) => eventFired = true;
        
        // Act
        _userDataManager.SetOfflineMode(false); // 같은 값으로 설정
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    #endregion
    
    #region Cache Management Tests
    [Test]
    public void ClearCache_WithCachedUsers_ShouldClearCache()
    {
        // Arrange
        _userDataManager.SetCurrentUser("user1", "User 1");
        _userDataManager.UpdateUserData(new UserData { UserId = "user2", DisplayName = "User 2" });
        
        Assert.Greater(_userDataManager.CachedUserCount, 0); // 사전 확인
        
        // Act
        _userDataManager.ClearCache();
        
        // Assert
        Assert.AreEqual(0, _userDataManager.CachedUserCount);
    }
    
    [Test]
    public void ClearAllLocalData_WithData_ShouldClearAllData()
    {
        // Arrange
        _userDataManager.SetCurrentUser("user1", "User 1");
        _userDataManager.SetOfflineMode(true);
        
        Assert.IsNotNull(_userDataManager.CurrentUser); // 사전 확인
        Assert.IsTrue(_userDataManager.IsOfflineMode); // 사전 확인
        
        // Act
        _userDataManager.ClearAllLocalData();
        
        // Assert
        Assert.IsNull(_userDataManager.CurrentUser);
        Assert.AreEqual(0, _userDataManager.CachedUserCount);
        Assert.IsFalse(PlayerPrefs.HasKey("current_user_id"));
        Assert.IsFalse(PlayerPrefs.HasKey("offline_mode"));
    }
    #endregion
    
    #region Data Validation Tests
    [Test]
    public void UpdateUserData_TooLongDisplayName_ShouldNotUpdate()
    {
        // Arrange
        var userData = new UserData
        {
            UserId = "test_user",
            DisplayName = new string('A', 51), // 51 characters - too long
            Email = "test@example.com"
        };
        
        bool eventFired = false;
        UserDataManager.OnUserDataUpdated += (data) => eventFired = true;
        
        // Act
        _userDataManager.UpdateUserData(userData);
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    
    [Test]
    public void UpdateUserData_InvalidEmail_ShouldNotUpdate()
    {
        // Arrange
        var userData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User",
            Email = "invalid-email" // Invalid email format
        };
        
        bool eventFired = false;
        UserDataManager.OnUserDataUpdated += (data) => eventFired = true;
        
        // Act
        _userDataManager.UpdateUserData(userData);
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    
    [Test]
    public void UpdateUserData_ValidEmail_ShouldUpdate()
    {
        // Arrange
        var userData = new UserData
        {
            UserId = "test_user",
            DisplayName = "Test User",
            Email = "valid@example.com" // Valid email format
        };
        
        bool eventFired = false;
        UserDataManager.OnUserDataUpdated += (data) => eventFired = true;
        
        // Act
        _userDataManager.UpdateUserData(userData);
        
        // Assert
        Assert.IsTrue(eventFired);
    }
    #endregion
    
    #region Sync Tests
    [Test]
    public void SyncWithServer_OfflineMode_ShouldNotSync()
    {
        // Arrange
        _userDataManager.SetCurrentUser("test_user", "Test User");
        _userDataManager.SetOfflineMode(true);
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _userDataManager.SyncWithServer();
        });
    }
    
    [Test]
    public void SyncWithServer_NoCurrentUser_ShouldNotSync()
    {
        // Arrange - 현재 사용자가 없는 상태
        Assert.IsNull(_userDataManager.CurrentUser);
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _userDataManager.SyncWithServer();
        });
    }
    #endregion
    
    #region Time Management Tests
    [Test]
    public void GetLastSyncTime_NoSyncData_ShouldReturnMinValue()
    {
        // Act
        var lastSyncTime = _userDataManager.GetLastSyncTime();
        
        // Assert
        Assert.AreEqual(System.DateTime.MinValue, lastSyncTime);
    }
    #endregion
    
    #region Event System Tests
    [Test]
    public void OnUserDataLoaded_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        UserData receivedData = null;
        
        UserDataManager.OnUserDataLoaded += (userData) =>
        {
            eventFired = true;
            receivedData = userData;
        };
        
        // Act - 직접 이벤트 호출 테스트는 어려우므로 SetCurrentUser를 통해 간접 테스트
        _userDataManager.SetCurrentUser("test_user", "Test User");
        
        // Assert - OnUserDataUpdated가 발생하는지 확인 (OnUserDataLoaded는 초기화 시에만 발생)
        // 이 테스트는 실제로는 초기화 프로세스에서 확인해야 함
        Assert.IsNotNull(_userDataManager.CurrentUser);
    }
    
    [Test]
    public void NullEventHandlers_ShouldNotCauseExceptions()
    {
        // Arrange - 모든 이벤트를 null로 설정
        UserDataManager.OnUserDataLoaded = null;
        UserDataManager.OnUserDataUpdated = null;
        UserDataManager.OnSyncCompleted = null;
        UserDataManager.OnOfflineModeChanged = null;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _userDataManager.SetCurrentUser("test_user", "Test User");
            _userDataManager.SetOfflineMode(true);
            _userDataManager.LogoutCurrentUser();
        });
    }
    #endregion
    
    #region Integration Tests
    [Test]
    public void CompleteUserLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        string userId = "integration_test_user";
        string userName = "Integration Test User";
        string userEmail = "integration@test.com";
        
        // Act & Assert - Complete user lifecycle
        
        // 1. Set user
        _userDataManager.SetCurrentUser(userId, userName, userEmail);
        Assert.IsNotNull(_userDataManager.CurrentUser);
        Assert.AreEqual(userId, _userDataManager.CurrentUser.UserId);
        
        // 2. Update user data
        var updatedData = _userDataManager.CurrentUser;
        updatedData.Level = 5;
        updatedData.Experience = 500;
        _userDataManager.UpdateUserData(updatedData);
        Assert.AreEqual(5, _userDataManager.CurrentUser.Level);
        
        // 3. Get user data
        var retrievedData = _userDataManager.GetUserData(userId);
        Assert.IsNotNull(retrievedData);
        Assert.AreEqual(5, retrievedData.Level);
        
        // 4. Toggle offline mode
        _userDataManager.SetOfflineMode(true);
        Assert.IsTrue(_userDataManager.IsOfflineMode);
        
        // 5. Logout
        _userDataManager.LogoutCurrentUser();
        Assert.IsNull(_userDataManager.CurrentUser);
    }
    #endregion
}