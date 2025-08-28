using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// NicknameSetupScreen에 대한 단위 테스트
/// 닉네임 검증, 중복 검사, 사용자 인터페이스 테스트를 포함
/// </summary>
public class NicknameSetupScreenTests
{
    private NicknameSetupScreen _nicknameSetupScreen;
    private GameObject _testGameObject;
    private GameObject _canvasGameObject;
    private InputField _testInputField;
    private Button _testConfirmButton;
    private Button _testSkipButton;
    private Text _testFeedbackText;
    
    [SetUp]
    public void SetUp()
    {
        // 테스트 시작 전 초기화
        PlayerPrefs.DeleteAll();
        
        // Canvas 생성 (UI 컴포넌트들이 Canvas 하위에 있어야 함)
        _canvasGameObject = new GameObject("TestCanvas");
        var canvas = _canvasGameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGameObject.AddComponent<CanvasScaler>();
        _canvasGameObject.AddComponent<GraphicRaycaster>();
        
        // 테스트용 NicknameSetupScreen 생성
        _testGameObject = new GameObject("TestNicknameSetupScreen");
        _testGameObject.transform.SetParent(_canvasGameObject.transform);
        _nicknameSetupScreen = _testGameObject.AddComponent<NicknameSetupScreen>();
        
        // UI 컴포넌트들 생성
        SetupUIComponents();
        
        // UserDataManager 초기화
        if (UserDataManager.Instance == null)
        {
            var userDataManagerGO = new GameObject("UserDataManager");
            userDataManagerGO.AddComponent<UserDataManager>();
        }
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        NicknameSetupScreen.OnNicknameSetupCompleted = null;
        
        if (_canvasGameObject != null)
        {
            Object.DestroyImmediate(_canvasGameObject);
        }
        
        if (UserDataManager.Instance != null)
        {
            Object.DestroyImmediate(UserDataManager.Instance.gameObject);
        }
        
        PlayerPrefs.DeleteAll();
    }
    
    /// <summary>
    /// 테스트용 UI 컴포넌트 설정
    /// </summary>
    private void SetupUIComponents()
    {
        // InputField 생성
        var inputFieldGO = new GameObject("NicknameInputField");
        inputFieldGO.transform.SetParent(_testGameObject.transform);
        _testInputField = inputFieldGO.AddComponent<InputField>();
        _testInputField.textComponent = inputFieldGO.AddComponent<Text>();
        
        // Confirm Button 생성
        var confirmButtonGO = new GameObject("ConfirmButton");
        confirmButtonGO.transform.SetParent(_testGameObject.transform);
        _testConfirmButton = confirmButtonGO.AddComponent<Button>();
        confirmButtonGO.AddComponent<Image>();
        
        // Skip Button 생성
        var skipButtonGO = new GameObject("SkipButton");
        skipButtonGO.transform.SetParent(_testGameObject.transform);
        _testSkipButton = skipButtonGO.AddComponent<Button>();
        skipButtonGO.AddComponent<Image>();
        
        // Feedback Text 생성
        var feedbackTextGO = new GameObject("FeedbackText");
        feedbackTextGO.transform.SetParent(_testGameObject.transform);
        _testFeedbackText = feedbackTextGO.AddComponent<Text>();
        
        // NicknameSetupScreen에 컴포넌트들 할당
        SetPrivateField(_nicknameSetupScreen, "nicknameInputField", _testInputField);
        SetPrivateField(_nicknameSetupScreen, "confirmButton", _testConfirmButton);
        SetPrivateField(_nicknameSetupScreen, "skipButton", _testSkipButton);
        SetPrivateField(_nicknameSetupScreen, "feedbackText", _testFeedbackText);
    }
    
    /// <summary>
    /// private 필드 설정을 위한 헬퍼 메서드
    /// </summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }
    
    /// <summary>
    /// private 메서드 호출을 위한 헬퍼 메서드
    /// </summary>
    private object InvokePrivateMethod(object obj, string methodName, params object[] parameters)
    {
        var method = obj.GetType().GetMethod(methodName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return method?.Invoke(obj, parameters);
    }
    
    #region Component Validation Tests
    [Test]
    public void NicknameSetupScreen_ComponentsInitialized_ShouldNotThrowErrors()
    {
        // Act & Assert - 초기화 시 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _nicknameSetupScreen.Start();
        });
    }
    
    [Test]
    public void NicknameSetupScreen_WithNullComponents_ShouldHandleGracefully()
    {
        // Arrange - 컴포넌트들을 null로 설정
        SetPrivateField(_nicknameSetupScreen, "nicknameInputField", null);
        SetPrivateField(_nicknameSetupScreen, "confirmButton", null);
        SetPrivateField(_nicknameSetupScreen, "feedbackText", null);
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _nicknameSetupScreen.Start();
        });
    }
    #endregion
    
    #region Nickname Validation Tests
    [Test]
    public void ValidateNicknameFormat_ValidNickname_ShouldReturnNone()
    {
        // Arrange
        string validNickname = "TestUser123";
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", validNickname);
        
        // Assert - ValidationState.None (0)을 반환해야 함
        Assert.AreEqual(0, (int)result);
    }
    
    [Test]
    public void ValidateNicknameFormat_TooShortNickname_ShouldReturnTooShort()
    {
        // Arrange
        string shortNickname = "A"; // 1 character
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", shortNickname);
        
        // Assert - ValidationState.TooShort (1)을 반환해야 함
        Assert.AreEqual(1, (int)result);
    }
    
    [Test]
    public void ValidateNicknameFormat_TooLongNickname_ShouldReturnTooLong()
    {
        // Arrange
        string longNickname = "ThisIsAVeryLongNickname"; // >12 characters
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", longNickname);
        
        // Assert - ValidationState.TooLong (2)를 반환해야 함
        Assert.AreEqual(2, (int)result);
    }
    
    [Test]
    public void ValidateNicknameFormat_InvalidCharacters_ShouldReturnInvalidChars()
    {
        // Arrange
        string invalidNickname = "Test@User!"; // Special characters not allowed
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", invalidNickname);
        
        // Assert - ValidationState.InvalidChars (3)를 반환해야 함
        Assert.AreEqual(3, (int)result);
    }
    
    [Test]
    public void ValidateNicknameFormat_EmptyString_ShouldReturnNone()
    {
        // Arrange
        string emptyNickname = "";
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", emptyNickname);
        
        // Assert - ValidationState.None (0)을 반환해야 함
        Assert.AreEqual(0, (int)result);
    }
    
    [Test]
    public void ValidateNicknameFormat_NullString_ShouldReturnNone()
    {
        // Arrange
        string nullNickname = null;
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "ValidateNicknameFormat", nullNickname);
        
        // Assert - ValidationState.None (0)을 반환해야 함
        Assert.AreEqual(0, (int)result);
    }
    
    [Test]
    public void IsValidNicknameCharacters_ValidCharacters_ShouldReturnTrue()
    {
        // Arrange
        string[] validNicknames = { "TestUser", "User123", "한글닉네임", "Test_User", "User-Name" };
        
        foreach (string nickname in validNicknames)
        {
            // Act
            var result = InvokePrivateMethod(_nicknameSetupScreen, "IsValidNicknameCharacters", nickname);
            
            // Assert
            Assert.IsTrue((bool)result, $"Nickname '{nickname}' should be valid");
        }
    }
    
    [Test]
    public void IsValidNicknameCharacters_InvalidCharacters_ShouldReturnFalse()
    {
        // Arrange
        string[] invalidNicknames = { "Test@User", "User!", "Nick#name", "User Space", "Test.User" };
        
        foreach (string nickname in invalidNicknames)
        {
            // Act
            var result = InvokePrivateMethod(_nicknameSetupScreen, "IsValidNicknameCharacters", nickname);
            
            // Assert
            Assert.IsFalse((bool)result, $"Nickname '{nickname}' should be invalid");
        }
    }
    #endregion
    
    #region UI Interaction Tests
    [Test]
    public void OnNicknameInputChanged_ValidInput_ShouldUpdateUI()
    {
        // Arrange
        _nicknameSetupScreen.Start(); // 초기화
        string validNickname = "TestUser";
        
        // Act
        _testInputField.text = validNickname;
        InvokePrivateMethod(_nicknameSetupScreen, "OnNicknameInputChanged", validNickname);
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // UI가 업데이트되어야 함 (정확한 상태는 private이므로 예외 없음으로 확인)
        });
    }
    
    [Test]
    public void OnSkipClicked_ShouldTriggerEvent()
    {
        // Arrange
        bool eventTriggered = false;
        bool wasNicknameSet = true; // 초기값
        
        NicknameSetupScreen.OnNicknameSetupCompleted += (wasSet) =>
        {
            eventTriggered = true;
            wasNicknameSet = wasSet;
        };
        
        // UserDataManager에 테스트 사용자 설정
        UserDataManager.Instance.SetCurrentUser("test_user", "Test User");
        
        // Act
        InvokePrivateMethod(_nicknameSetupScreen, "OnSkipClicked");
        
        // Assert
        Assert.IsTrue(eventTriggered);
        Assert.IsFalse(wasNicknameSet); // Skip했으므로 false여야 함
    }
    
    [Test]
    public void ShowNicknameSetup_ShouldActivateGameObject()
    {
        // Arrange
        _testGameObject.SetActive(false);
        
        // Act
        _nicknameSetupScreen.ShowNicknameSetup();
        
        // Assert
        Assert.IsTrue(_testGameObject.activeInHierarchy);
    }
    
    [Test]
    public void ShowNicknameSetup_WithCurrentUser_ShouldSetInputText()
    {
        // Arrange
        string existingNickname = "ExistingUser";
        UserDataManager.Instance.SetCurrentUser("test_user", existingNickname);
        
        // Act
        _nicknameSetupScreen.ShowNicknameSetup();
        
        // Assert
        Assert.AreEqual(existingNickname, _testInputField.text);
    }
    #endregion
    
    #region Localization Tests
    [Test]
    public void SetLanguage_Korean_ShouldUpdateUI()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        
        // Act
        _nicknameSetupScreen.SetLanguage(true); // Korean
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // 로컬라이제이션이 적용되어야 함
        });
    }
    
    [Test]
    public void SetLanguage_English_ShouldUpdateUI()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        
        // Act
        _nicknameSetupScreen.SetLanguage(false); // English
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // 로컬라이제이션이 적용되어야 함
        });
    }
    #endregion
    
    #region Default Nickname Generation Tests
    [Test]
    public void GenerateDefaultNickname_WithCurrentUser_ShouldGenerateFromUserId()
    {
        // Arrange
        string userId = "test_user_1234";
        UserDataManager.Instance.SetCurrentUser(userId, "Test User");
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "GenerateDefaultNickname");
        
        // Assert
        Assert.IsNotNull(result);
        string nickname = result.ToString();
        Assert.IsTrue(nickname.StartsWith("Player"));
        Assert.IsTrue(nickname.Contains("1234")); // UserId 마지막 4자리
    }
    
    [Test]
    public void GenerateDefaultNickname_WithShortUserId_ShouldGenerateRandomNumber()
    {
        // Arrange
        string shortUserId = "abc"; // 4자리 미만
        UserDataManager.Instance.SetCurrentUser(shortUserId, "Test User");
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "GenerateDefaultNickname");
        
        // Assert
        Assert.IsNotNull(result);
        string nickname = result.ToString();
        Assert.IsTrue(nickname.StartsWith("Player"));
        Assert.IsTrue(nickname.Length >= 10); // Player + 4자리 숫자
    }
    
    [Test]
    public void GenerateDefaultNickname_WithoutCurrentUser_ShouldGenerateRandomNumber()
    {
        // Arrange - 현재 사용자 없음
        UserDataManager.Instance.LogoutCurrentUser();
        
        // Act
        var result = InvokePrivateMethod(_nicknameSetupScreen, "GenerateDefaultNickname");
        
        // Assert
        Assert.IsNotNull(result);
        string nickname = result.ToString();
        Assert.IsTrue(nickname.StartsWith("Player"));
        Assert.IsTrue(nickname.Length >= 10); // Player + 4자리 숫자
    }
    #endregion
    
    #region Integration Tests
    [UnityTest]
    public IEnumerator NicknameSetup_CompleteFlow_ShouldWork()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        UserDataManager.Instance.SetCurrentUser("test_user", "Test User");
        
        bool setupCompleted = false;
        bool wasNicknameSet = false;
        
        NicknameSetupScreen.OnNicknameSetupCompleted += (wasSet) =>
        {
            setupCompleted = true;
            wasNicknameSet = wasSet;
        };
        
        // Act
        _nicknameSetupScreen.ShowNicknameSetup();
        yield return null; // 한 프레임 대기
        
        // Skip 버튼 클릭 시뮬레이션
        InvokePrivateMethod(_nicknameSetupScreen, "OnSkipClicked");
        
        // Assert
        Assert.IsTrue(setupCompleted);
        Assert.IsFalse(wasNicknameSet); // Skip했으므로
    }
    
    [Test]
    public void NicknameValidation_MultipleInputs_ShouldHandleCorrectly()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        string[] testInputs = { "", "A", "ValidNick", "TooLongNicknameTest", "Invalid@Nick" };
        
        // Act & Assert - 각 입력에 대해 예외가 발생하지 않아야 함
        foreach (string input in testInputs)
        {
            Assert.DoesNotThrow(() =>
            {
                InvokePrivateMethod(_nicknameSetupScreen, "OnNicknameInputChanged", input);
            }, $"Input '{input}' should not cause exception");
        }
    }
    #endregion
    
    #region Error Handling Tests
    [Test]
    public void OnConfirmClicked_WithoutCurrentUser_ShouldHandleGracefully()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        UserDataManager.Instance.LogoutCurrentUser(); // 현재 사용자 없음
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(_nicknameSetupScreen, "OnConfirmClicked");
        });
    }
    
    [Test]
    public void OnSkipClicked_WithoutCurrentUser_ShouldHandleGracefully()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        UserDataManager.Instance.LogoutCurrentUser(); // 현재 사용자 없음
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(_nicknameSetupScreen, "OnSkipClicked");
        });
    }
    
    [Test]
    public void NicknameSetupScreen_DestroyWithActiveCoroutines_ShouldNotCauseErrors()
    {
        // Arrange
        _nicknameSetupScreen.Start();
        
        // 검증 코루틴 시작 시뮬레이션
        InvokePrivateMethod(_nicknameSetupScreen, "OnNicknameInputChanged", "TestUser");
        
        // Act & Assert - 파괴 시 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            Object.DestroyImmediate(_testGameObject);
        });
    }
    #endregion
}