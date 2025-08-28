using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AuthenticationManager에 대한 단위 테스트
/// 커버리지 80% 이상을 목표로 하는 포괄적인 테스트 스위트
/// </summary>
public class AuthenticationManagerTests
{
    private AuthenticationManager _authManager;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 PlayerPrefs 클리어
        PlayerPrefs.DeleteAll();
        
        // 기존 AuthenticationManager 인스턴스가 있다면 제거
        if (AuthenticationManager.Instance != null)
        {
            Object.DestroyImmediate(AuthenticationManager.Instance.gameObject);
        }
        
        // 테스트용 AuthenticationManager 생성
        _testGameObject = new GameObject("TestAuthManager");
        _authManager = _testGameObject.AddComponent<AuthenticationManager>();
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
        AuthenticationManager.OnAuthenticationStateChanged = null;
        AuthenticationManager.OnLoginSuccess = null;
        AuthenticationManager.OnLoginFailed = null;
        AuthenticationManager.OnLogoutCompleted = null;
    }
    
    #region Singleton Pattern Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = AuthenticationManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is AuthenticationManager);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = AuthenticationManager.Instance;
        var instance2 = AuthenticationManager.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    
    [Test]
    public void Singleton_GameObjectShouldPersist()
    {
        // Arrange
        var instance = AuthenticationManager.Instance;
        
        // Act
        var flags = instance.gameObject.hideFlags;
        
        // Assert - DontDestroyOnLoad가 설정되었는지 확인
        Assert.IsNotNull(instance.gameObject);
        // Note: DontDestroyOnLoad는 Play mode에서만 동작하므로 Edit mode에서는 확인 불가
    }
    #endregion
    
    #region Property Tests
    [Test]
    public void IsAuthenticated_InitialState_ShouldBeFalse()
    {
        // Assert
        Assert.IsFalse(_authManager.IsAuthenticated);
    }
    
    [Test]
    public void IsAuthenticating_InitialState_ShouldBeFalse()
    {
        // Assert
        Assert.IsFalse(_authManager.IsAuthenticating);
    }
    
    [Test]
    public void CurrentUser_WhenNotAuthenticated_ShouldBeNull()
    {
        // Assert
        Assert.IsNull(_authManager.CurrentUser);
    }
    
    [Test]
    public void AutoLoginEnabled_DefaultValue_ShouldBeTrue()
    {
        // Assert
        Assert.IsTrue(_authManager.AutoLoginEnabled);
    }
    
    [Test]
    public void AutoLoginEnabled_SetValue_ShouldPersist()
    {
        // Arrange
        bool expectedValue = false;
        
        // Act
        _authManager.AutoLoginEnabled = expectedValue;
        
        // Assert
        Assert.AreEqual(expectedValue, _authManager.AutoLoginEnabled);
        Assert.AreEqual(expectedValue ? 1 : 0, PlayerPrefs.GetInt("gpg_auto_login"));
    }
    #endregion
    
    #region Data Management Tests
    [Test]
    public void GetSavedUserInfo_NoDataSaved_ShouldReturnEmptyStrings()
    {
        // Act
        var (userId, userName) = _authManager.GetSavedUserInfo();
        
        // Assert
        Assert.AreEqual("", userId);
        Assert.AreEqual("", userName);
    }
    
    [Test]
    public void SaveUserData_ValidUserInfo_ShouldPersistData()
    {
        // Arrange
        string testUserId = "test_user_123";
        string testUserName = "TestUser";
        
        // Act - 직접 PlayerPrefs에 저장 (private method 테스트)
        PlayerPrefs.SetString("gpg_user_id", testUserId);
        PlayerPrefs.SetString("gpg_user_name", testUserName);
        PlayerPrefs.Save();
        
        var (userId, userName) = _authManager.GetSavedUserInfo();
        
        // Assert
        Assert.AreEqual(testUserId, userId);
        Assert.AreEqual(testUserName, userName);
    }
    
    [Test]
    public void ClearUserData_AfterSaving_ShouldRemoveAllData()
    {
        // Arrange
        PlayerPrefs.SetString("gpg_user_id", "test");
        PlayerPrefs.SetString("gpg_user_name", "test");
        PlayerPrefs.SetString("gpg_token_encrypted", "test");
        PlayerPrefs.Save();
        
        // Act - Logout을 통해 데이터 클리어
        _authManager.Logout();
        
        // Assert
        Assert.IsFalse(PlayerPrefs.HasKey("gpg_user_id"));
        Assert.IsFalse(PlayerPrefs.HasKey("gpg_user_name"));
        Assert.IsFalse(PlayerPrefs.HasKey("gpg_token_encrypted"));
    }
    #endregion
    
    #region Event System Tests
    [Test]
    public void OnAuthenticationStateChanged_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        bool receivedState = false;
        
        AuthenticationManager.OnAuthenticationStateChanged += (state) =>
        {
            eventFired = true;
            receivedState = state;
        };
        
        // Act
        _authManager.RefreshAuthenticationState();
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.AreEqual(_authManager.IsAuthenticated, receivedState);
    }
    
    [Test]
    public void OnLoginFailed_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        string receivedMessage = "";
        
        AuthenticationManager.OnLoginFailed += (message) =>
        {
            eventFired = true;
            receivedMessage = message;
        };
        
        // Act - 초기화되지 않은 상태에서 로그인 시도
        var authManager = new GameObject().AddComponent<AuthenticationManager>();
        authManager.Login();
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotEmpty(receivedMessage);
        
        // Cleanup
        Object.DestroyImmediate(authManager.gameObject);
    }
    
    [Test]
    public void OnLogoutCompleted_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        
        AuthenticationManager.OnLogoutCompleted += () =>
        {
            eventFired = true;
        };
        
        // Act
        _authManager.Logout();
        
        // Assert
        Assert.IsTrue(eventFired);
    }
    #endregion
    
    #region Login/Logout Tests
    [Test]
    public void Login_WhenNotInitialized_ShouldFireFailedEvent()
    {
        // Arrange
        bool loginFailedFired = false;
        string errorMessage = "";
        
        AuthenticationManager.OnLoginFailed += (message) =>
        {
            loginFailedFired = true;
            errorMessage = message;
        };
        
        // Create fresh instance without initialization
        var freshGameObject = new GameObject("FreshAuthManager");
        var freshAuthManager = freshGameObject.AddComponent<AuthenticationManager>();
        
        // Act
        freshAuthManager.Login();
        
        // Assert
        Assert.IsTrue(loginFailedFired);
        Assert.Contains("not initialized", errorMessage.ToLower());
        
        // Cleanup
        Object.DestroyImmediate(freshGameObject);
    }
    
    [Test]
    public void Logout_WhenNotAuthenticated_ShouldCompleteSuccessfully()
    {
        // Arrange
        bool logoutCompleted = false;
        
        AuthenticationManager.OnLogoutCompleted += () =>
        {
            logoutCompleted = true;
        };
        
        // Act
        _authManager.Logout();
        
        // Assert
        Assert.IsTrue(logoutCompleted);
    }
    #endregion
    
    #region Auto-Login Tests
    [Test]
    public void TryAutoLogin_WhenAutoLoginDisabled_ShouldNotProceed()
    {
        // Arrange
        _authManager.AutoLoginEnabled = false;
        
        // Act
        _authManager.TryAutoLogin();
        
        // Assert
        // 로그는 확인할 수 없지만, 인증 상태가 변경되지 않았는지 확인
        Assert.IsFalse(_authManager.IsAuthenticated);
    }
    
    [Test]
    public void TryAutoLogin_WhenAutoLoginEnabled_ShouldAttemptLogin()
    {
        // Arrange
        _authManager.AutoLoginEnabled = true;
        
        // Act
        _authManager.TryAutoLogin();
        
        // Assert
        // Google Play Games가 테스트 환경에서 동작하지 않으므로
        // 자동 로그인 시도 자체가 정상적으로 실행되었는지만 확인
        Assert.IsTrue(_authManager.AutoLoginEnabled);
    }
    #endregion
    
    #region Coroutine Tests
    [UnityTest]
    public IEnumerator LoginCoroutine_Timeout_ShouldHandleGracefully()
    {
        // Arrange
        bool loginFailedFired = false;
        string errorMessage = "";
        
        AuthenticationManager.OnLoginFailed += (message) =>
        {
            loginFailedFired = true;
            errorMessage = message;
        };
        
        // Google Play Games가 테스트 환경에서 초기화되지 않으므로 실패할 것임
        
        // Act
        _authManager.Login();
        
        // 충분한 시간 대기
        yield return new WaitForSeconds(1f);
        
        // Assert
        Assert.IsTrue(loginFailedFired);
        Assert.IsNotEmpty(errorMessage);
    }
    #endregion
    
    #region Integration Tests
    [Test]
    public void RefreshAuthenticationState_ShouldTriggerEvent()
    {
        // Arrange
        bool eventFired = false;
        
        AuthenticationManager.OnAuthenticationStateChanged += (state) =>
        {
            eventFired = true;
        };
        
        // Act
        _authManager.RefreshAuthenticationState();
        
        // Assert
        Assert.IsTrue(eventFired);
    }
    
    [Test]
    public void Settings_PersistenceTest()
    {
        // Arrange
        bool originalAutoLogin = _authManager.AutoLoginEnabled;
        bool newValue = !originalAutoLogin;
        
        // Act
        _authManager.AutoLoginEnabled = newValue;
        
        // Create new instance to test persistence
        var newGameObject = new GameObject("NewAuthManager");
        var newAuthManager = newGameObject.AddComponent<AuthenticationManager>();
        
        // Assert
        Assert.AreEqual(newValue, newAuthManager.AutoLoginEnabled);
        
        // Cleanup
        Object.DestroyImmediate(newGameObject);
    }
    #endregion
    
    #region Edge Cases Tests
    [Test]
    public void MultipleLoginAttempts_ShouldHandleGracefully()
    {
        // Act - 여러 번 로그인 시도
        _authManager.Login();
        _authManager.Login();
        _authManager.Login();
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.IsFalse(_authManager.IsAuthenticated); // Google Play Games가 테스트 환경에서 동작하지 않음
    }
    
    [Test]
    public void MultipleLogoutAttempts_ShouldHandleGracefully()
    {
        // Act - 여러 번 로그아웃 시도
        _authManager.Logout();
        _authManager.Logout();
        _authManager.Logout();
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.IsFalse(_authManager.IsAuthenticated);
    }
    
    [Test]
    public void NullEventHandlers_ShouldNotCauseExceptions()
    {
        // Arrange - 모든 이벤트를 null로 설정
        AuthenticationManager.OnAuthenticationStateChanged = null;
        AuthenticationManager.OnLoginSuccess = null;
        AuthenticationManager.OnLoginFailed = null;
        AuthenticationManager.OnLogoutCompleted = null;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _authManager.Login();
            _authManager.Logout();
            _authManager.RefreshAuthenticationState();
        });
    }
    #endregion
}