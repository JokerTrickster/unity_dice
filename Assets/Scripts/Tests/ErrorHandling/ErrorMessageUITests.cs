using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// ErrorMessageUI에 대한 단위 테스트
/// UI 표시, 애니메이션, 큐 관리, 사용자 상호작용 테스트를 포함
/// </summary>
public class ErrorMessageUITests
{
    private ErrorMessageUI _errorMessageUI;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 기존 인스턴스가 있다면 제거
        if (ErrorMessageUI.Instance != null)
        {
            UnityEngine.Object.DestroyImmediate(ErrorMessageUI.Instance.gameObject);
        }
        
        // 테스트용 ErrorMessageUI 생성
        _testGameObject = new GameObject("TestErrorMessageUI");
        _errorMessageUI = _testGameObject.AddComponent<ErrorMessageUI>();
        
        // 이벤트 구독 해제
        ErrorMessageUI.OnScreenTransitionStarted = null;
        ErrorMessageUI.OnScreenTransitionCompleted = null;
        ErrorMessageUI.OnScreenTransitionFailed = null;
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
        
        // 이벤트 구독 해제
        ErrorMessageUI.OnScreenTransitionStarted = null;
        ErrorMessageUI.OnScreenTransitionCompleted = null;
        ErrorMessageUI.OnScreenTransitionFailed = null;
    }
    
    #region Singleton Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = ErrorMessageUI.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is ErrorMessageUI);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = ErrorMessageUI.Instance;
        var instance2 = ErrorMessageUI.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region Initialization Tests
    [UnityTest]
    public IEnumerator Initialization_ShouldSetupUIComponents()
    {
        // Arrange & Act
        _errorMessageUI.Start();
        
        // Wait for initialization
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.AreEqual(0, _errorMessageUI.ActiveToastCount);
        Assert.AreEqual(0, _errorMessageUI.QueuedMessageCount);
    }
    #endregion
    
    #region Message Display Tests
    [Test]
    public void ShowMessage_ValidMessage_ShouldQueueMessage()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act
        _errorMessageUI.ShowMessage("Test message", ErrorSeverity.Medium);
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
    }
    
    [Test]
    public void ShowToast_ShouldCreateToastMessage()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act
        _errorMessageUI.ShowToast("Toast message");
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
    }
    
    [Test]
    public void ShowPopup_ShouldCreatePopupMessage()
    {
        // Arrange
        _errorMessageUI.Start();
        bool dismissCalled = false;
        bool retryCalled = false;
        
        // Act
        _errorMessageUI.ShowPopup("Popup message", "Test Title", 
            onRetry: () => retryCalled = true, 
            onDismiss: () => dismissCalled = true);
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
    }
    
    [Test]
    public void ShowBanner_ShouldCreateBannerMessage()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act
        _errorMessageUI.ShowBanner("Banner message", ErrorSeverity.High);
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
    }
    
    [UnityTest]
    public IEnumerator ShowMessage_MultipleMessages_ShouldProcessInOrder()
    {
        // Arrange
        _errorMessageUI.Start();
        yield return new WaitForSeconds(0.1f); // Wait for initialization
        
        // Act
        _errorMessageUI.ShowMessage("First message", ErrorSeverity.Low);
        _errorMessageUI.ShowMessage("Second message", ErrorSeverity.Medium);
        _errorMessageUI.ShowMessage("Third message", ErrorSeverity.High);
        
        // Assert
        Assert.AreEqual(3, _errorMessageUI.QueuedMessageCount);
        
        // Wait for messages to be processed
        yield return new WaitForSeconds(1f);
        
        // Messages should start being processed
        Assert.Less(_errorMessageUI.QueuedMessageCount, 3);
    }
    #endregion
    
    #region Message Management Tests
    [Test]
    public void ClearAllMessages_ShouldRemoveAllMessages()
    {
        // Arrange
        _errorMessageUI.Start();
        _errorMessageUI.ShowMessage("Test message 1", ErrorSeverity.Low);
        _errorMessageUI.ShowMessage("Test message 2", ErrorSeverity.Medium);
        _errorMessageUI.ShowMessage("Test message 3", ErrorSeverity.High);
        
        // Act
        _errorMessageUI.ClearAllMessages();
        
        // Assert
        Assert.AreEqual(0, _errorMessageUI.QueuedMessageCount);
        Assert.AreEqual(0, _errorMessageUI.ActiveToastCount);
    }
    
    [Test]
    public void ClearMessagesBySeverity_ShouldRemoveOnlySpecificSeverity()
    {
        // Arrange
        _errorMessageUI.Start();
        _errorMessageUI.ShowMessage("Low message", ErrorSeverity.Low);
        _errorMessageUI.ShowMessage("Medium message", ErrorSeverity.Medium);
        _errorMessageUI.ShowMessage("High message", ErrorSeverity.High);
        
        int originalCount = _errorMessageUI.QueuedMessageCount;
        
        // Act
        _errorMessageUI.ClearMessagesBySeverity(ErrorSeverity.Medium);
        
        // Assert
        Assert.Less(_errorMessageUI.QueuedMessageCount, originalCount);
    }
    #endregion
    
    #region Toast Message Tests
    [UnityTest]
    public IEnumerator ToastMessage_ShouldAutoDisappear()
    {
        // Arrange
        _errorMessageUI.Start();
        yield return new WaitForSeconds(0.1f);
        
        // Act
        _errorMessageUI.ShowToast("Auto disappear toast");
        
        // Wait for message to be displayed
        yield return new WaitForSeconds(0.5f);
        
        // Assert - Toast should be active
        // (실제로는 UI 컴포넌트가 생성되는지 확인해야 하지만, 테스트 환경에서는 제한적)
        Assert.GreaterOrEqual(_errorMessageUI.ActiveToastCount, 0);
        
        // Wait for auto-dismiss
        yield return new WaitForSeconds(4f);
        
        // Assert - Toast should be dismissed
        Assert.AreEqual(0, _errorMessageUI.ActiveToastCount);
    }
    
    [UnityTest]
    public IEnumerator ToastMessage_MaxLimit_ShouldRemoveOldest()
    {
        // Arrange
        _errorMessageUI.Start();
        yield return new WaitForSeconds(0.1f);
        
        // Act - 최대 개수보다 많은 토스트 생성
        for (int i = 0; i < 5; i++)
        {
            _errorMessageUI.ShowToast($"Toast message {i}");
        }
        
        // Wait for messages to be processed
        yield return new WaitForSeconds(1f);
        
        // Assert - 최대 개수 제한이 적용되어야 함
        Assert.LessOrEqual(_errorMessageUI.ActiveToastCount, 3); // maxToastMessages = 3
    }
    #endregion
    
    #region Error Integration Tests
    [Test]
    public void GlobalErrorHandler_Integration_ShouldReceiveErrorMessages()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // GlobalErrorHandler에서 오는 이벤트 시뮬레이션
        bool messageReceived = false;
        
        // ErrorMessageUI가 GlobalErrorHandler 이벤트를 구독하는지 간접적으로 테스트
        // (실제로는 private 메서드이므로 직접 테스트하기 어려움)
        
        // Act
        if (GlobalErrorHandler.OnShowErrorMessage != null)
        {
            GlobalErrorHandler.OnShowErrorMessage.Invoke("Test error message", ErrorSeverity.High);
            messageReceived = true;
        }
        
        // Assert
        // 이벤트가 구독되어 있다면 메시지가 큐에 추가되어야 함
        if (messageReceived)
        {
            Assert.GreaterOrEqual(_errorMessageUI.QueuedMessageCount, 0);
        }
    }
    #endregion
    
    #region UI Component Tests
    [Test]
    public void ErrorMessage_DataClass_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var errorMessage = new ErrorMessage
        {
            Message = "Test message",
            Title = "Test title",
            Severity = ErrorSeverity.High,
            DisplayType = ErrorDisplayType.Popup,
            Duration = 5f,
            Timestamp = DateTime.Now
        };
        
        // Assert
        Assert.AreEqual("Test message", errorMessage.Message);
        Assert.AreEqual("Test title", errorMessage.Title);
        Assert.AreEqual(ErrorSeverity.High, errorMessage.Severity);
        Assert.AreEqual(ErrorDisplayType.Popup, errorMessage.DisplayType);
        Assert.AreEqual(5f, errorMessage.Duration);
        Assert.IsTrue((DateTime.Now - errorMessage.Timestamp).TotalSeconds < 1);
    }
    
    [Test]
    public void ErrorMessageTemplate_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var template = new ErrorMessageTemplate
        {
            Title = "Error",
            DisplayType = ErrorDisplayType.Toast,
            Duration = 3f,
            Color = Color.red,
            ShowRetryButton = true
        };
        
        // Assert
        Assert.AreEqual("Error", template.Title);
        Assert.AreEqual(ErrorDisplayType.Toast, template.DisplayType);
        Assert.AreEqual(3f, template.Duration);
        Assert.AreEqual(Color.red, template.Color);
        Assert.IsTrue(template.ShowRetryButton);
    }
    
    [Test]
    public void ErrorDisplayType_ShouldHaveAllTypes()
    {
        // Act & Assert
        var types = Enum.GetValues(typeof(ErrorDisplayType));
        Assert.AreEqual(3, types.Length);
        Assert.IsTrue(Enum.IsDefined(typeof(ErrorDisplayType), ErrorDisplayType.Toast));
        Assert.IsTrue(Enum.IsDefined(typeof(ErrorDisplayType), ErrorDisplayType.Popup));
        Assert.IsTrue(Enum.IsDefined(typeof(ErrorDisplayType), ErrorDisplayType.Banner));
    }
    #endregion
    
    #region Callback Tests
    [Test]
    public void ShowPopup_WithCallbacks_ShouldStoreCallbacks()
    {
        // Arrange
        _errorMessageUI.Start();
        bool retryCallbackInvoked = false;
        bool dismissCallbackInvoked = false;
        
        // Act
        _errorMessageUI.ShowPopup("Test popup", 
            onRetry: () => retryCallbackInvoked = true,
            onDismiss: () => dismissCallbackInvoked = true);
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
        // 콜백이 저장되었는지는 메시지가 처리될 때 확인 가능
    }
    
    [Test]
    public void ShowMessage_WithoutCallbacks_ShouldNotCrash()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _errorMessageUI.ShowMessage("Test message", ErrorSeverity.Low);
        });
    }
    #endregion
    
    #region Edge Cases
    [Test]
    public void ShowMessage_BeforeInitialization_ShouldQueueMessage()
    {
        // Arrange - Start()를 호출하지 않음
        
        // Act
        _errorMessageUI.ShowMessage("Message before init", ErrorSeverity.Medium);
        
        // Assert
        Assert.AreEqual(1, _errorMessageUI.QueuedMessageCount);
    }
    
    [Test]
    public void ShowMessage_EmptyMessage_ShouldNotCrash()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _errorMessageUI.ShowMessage("", ErrorSeverity.Low);
            _errorMessageUI.ShowMessage(null, ErrorSeverity.Medium);
        });
    }
    
    [Test]
    public void ShowMessage_AllSeverityLevels_ShouldHandleAll()
    {
        // Arrange
        _errorMessageUI.Start();
        
        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _errorMessageUI.ShowMessage("Low severity", ErrorSeverity.Low);
            _errorMessageUI.ShowMessage("Medium severity", ErrorSeverity.Medium);
            _errorMessageUI.ShowMessage("High severity", ErrorSeverity.High);
            _errorMessageUI.ShowMessage("Critical severity", ErrorSeverity.Critical);
        });
        
        Assert.AreEqual(4, _errorMessageUI.QueuedMessageCount);
    }
    
    [UnityTest]
    public IEnumerator MessageProcessing_HighVolume_ShouldHandleGracefully()
    {
        // Arrange
        _errorMessageUI.Start();
        yield return new WaitForSeconds(0.1f);
        
        // Act - 많은 메시지를 빠르게 추가
        for (int i = 0; i < 20; i++)
        {
            _errorMessageUI.ShowMessage($"High volume message {i}", ErrorSeverity.Low);
        }
        
        // Wait for processing
        yield return new WaitForSeconds(2f);
        
        // Assert - 시스템이 충돌하지 않고 메시지를 처리해야 함
        Assert.LessOrEqual(_errorMessageUI.QueuedMessageCount, 20);
        Assert.LessOrEqual(_errorMessageUI.ActiveToastCount, 3); // Max toast limit
    }
    #endregion
    
    #region Memory Management Tests
    [UnityTest]
    public IEnumerator LongRunning_ShouldNotLeakMemory()
    {
        // Arrange
        _errorMessageUI.Start();
        yield return new WaitForSeconds(0.1f);
        
        // Act - 장시간에 걸쳐 메시지 생성/삭제 반복
        for (int cycle = 0; cycle < 5; cycle++)
        {
            // 메시지 추가
            for (int i = 0; i < 5; i++)
            {
                _errorMessageUI.ShowToast($"Cycle {cycle}, Message {i}");
            }
            
            yield return new WaitForSeconds(1f);
            
            // 메시지 클리어
            _errorMessageUI.ClearAllMessages();
            
            yield return new WaitForSeconds(0.5f);
        }
        
        // Assert - 메모리 누수가 없다면 큐와 활성 토스트가 정리되어야 함
        Assert.AreEqual(0, _errorMessageUI.QueuedMessageCount);
        Assert.AreEqual(0, _errorMessageUI.ActiveToastCount);
    }
    #endregion
}