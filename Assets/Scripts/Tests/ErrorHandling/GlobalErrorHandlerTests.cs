using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// GlobalErrorHandler에 대한 단위 테스트
/// 오류 처리, 복구, 로깅, UI 통합 테스트를 포함
/// </summary>
public class GlobalErrorHandlerTests
{
    private GlobalErrorHandler _errorHandler;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 기존 인스턴스가 있다면 제거
        if (GlobalErrorHandler.Instance != null)
        {
            UnityEngine.Object.DestroyImmediate(GlobalErrorHandler.Instance.gameObject);
        }
        
        // 테스트용 ErrorHandler 생성
        _testGameObject = new GameObject("TestGlobalErrorHandler");
        _errorHandler = _testGameObject.AddComponent<GlobalErrorHandler>();
        
        // 이벤트 구독 해제
        GlobalErrorHandler.OnErrorOccurred = null;
        GlobalErrorHandler.OnErrorRecovered = null;
        GlobalErrorHandler.OnCriticalError = null;
        GlobalErrorHandler.OnShowErrorMessage = null;
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
        GlobalErrorHandler.OnErrorOccurred = null;
        GlobalErrorHandler.OnErrorRecovered = null;
        GlobalErrorHandler.OnCriticalError = null;
        GlobalErrorHandler.OnShowErrorMessage = null;
    }
    
    #region Singleton Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = GlobalErrorHandler.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is GlobalErrorHandler);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = GlobalErrorHandler.Instance;
        var instance2 = GlobalErrorHandler.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region Error Handling Tests
    [Test]
    public void HandleError_ValidErrorInfo_ShouldProcessSuccessfully()
    {
        // Arrange
        _errorHandler.Start();
        bool eventFired = false;
        ErrorInfo receivedError = null;
        
        GlobalErrorHandler.OnErrorOccurred += (error) =>
        {
            eventFired = true;
            receivedError = error;
        };
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = "TEST_ERROR",
            Message = "Test error message",
            Severity = ErrorSeverity.Medium,
            Context = "Unit Test",
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.HandleError(errorInfo);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(receivedError);
        Assert.AreEqual("TEST_ERROR", receivedError.Code);
    }
    
    [Test]
    public void HandleError_NullErrorInfo_ShouldNotCrash()
    {
        // Arrange
        _errorHandler.Start();
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _errorHandler.HandleError(null);
        });
    }
    
    [Test]
    public void HandleError_CriticalError_ShouldFireCriticalEvent()
    {
        // Arrange
        _errorHandler.Start();
        bool criticalEventFired = false;
        
        GlobalErrorHandler.OnCriticalError += (error) =>
        {
            criticalEventFired = true;
        };
        
        var criticalError = new ErrorInfo
        {
            Type = ErrorType.System,
            Code = "CRITICAL_ERROR",
            Message = "Critical system error",
            Severity = ErrorSeverity.Critical,
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.HandleError(criticalError);
        
        // Assert
        Assert.IsTrue(criticalEventFired);
    }
    
    [Test]
    public void HandleError_WithUserMessage_ShouldFireShowMessageEvent()
    {
        // Arrange
        _errorHandler.Start();
        bool showMessageEventFired = false;
        string receivedMessage = "";
        ErrorSeverity receivedSeverity = ErrorSeverity.Low;
        
        GlobalErrorHandler.OnShowErrorMessage += (message, severity) =>
        {
            showMessageEventFired = true;
            receivedMessage = message;
            receivedSeverity = severity;
        };
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Validation,
            Code = "VALIDATION_ERROR",
            Message = "Internal validation error",
            UserMessage = "Please check your input",
            Severity = ErrorSeverity.Medium,
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.HandleError(errorInfo);
        
        // Assert
        Assert.IsTrue(showMessageEventFired);
        Assert.AreEqual("Please check your input", receivedMessage);
        Assert.AreEqual(ErrorSeverity.Medium, receivedSeverity);
    }
    
    [Test]
    public void HandleError_DisabledHandler_ShouldNotProcess()
    {
        // Arrange
        _errorHandler.Start();
        _errorHandler.IsEnabled = false;
        
        bool eventFired = false;
        GlobalErrorHandler.OnErrorOccurred += (error) => eventFired = true;
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = "TEST_ERROR",
            Message = "Test error",
            Severity = ErrorSeverity.Medium,
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.HandleError(errorInfo);
        
        // Assert
        Assert.IsFalse(eventFired);
    }
    #endregion
    
    #region API Method Tests
    [Test]
    public void HandleNetworkError_ShouldCreateProperErrorInfo()
    {
        // Arrange
        _errorHandler.Start();
        ErrorInfo receivedError = null;
        
        GlobalErrorHandler.OnErrorOccurred += (error) =>
        {
            receivedError = error;
        };
        
        // Act
        _errorHandler.HandleNetworkError("Connection timeout", 408, "Authentication");
        
        // Assert
        Assert.IsNotNull(receivedError);
        Assert.AreEqual(ErrorType.Network, receivedError.Type);
        Assert.AreEqual("NETWORK_408", receivedError.Code);
        Assert.AreEqual("Connection timeout", receivedError.Message);
        Assert.AreEqual("Authentication", receivedError.Context);
        Assert.IsNotEmpty(receivedError.UserMessage);
    }
    
    [Test]
    public void HandleAuthenticationError_ShouldCreateProperErrorInfo()
    {
        // Arrange
        _errorHandler.Start();
        ErrorInfo receivedError = null;
        
        GlobalErrorHandler.OnErrorOccurred += (error) =>
        {
            receivedError = error;
        };
        
        // Act
        _errorHandler.HandleAuthenticationError("Invalid token", "Login Process");
        
        // Assert
        Assert.IsNotNull(receivedError);
        Assert.AreEqual(ErrorType.Authentication, receivedError.Type);
        Assert.AreEqual("AUTH_FAILED", receivedError.Code);
        Assert.AreEqual("Invalid token", receivedError.Message);
        Assert.AreEqual("Login Process", receivedError.Context);
        Assert.AreEqual(ErrorSeverity.High, receivedError.Severity);
    }
    
    [Test]
    public void HandleValidationError_ShouldCreateProperErrorInfo()
    {
        // Arrange
        _errorHandler.Start();
        ErrorInfo receivedError = null;
        
        GlobalErrorHandler.OnErrorOccurred += (error) =>
        {
            receivedError = error;
        };
        
        // Act
        _errorHandler.HandleValidationError("nickname", "닉네임이 너무 짧습니다", "Nickname Setup");
        
        // Assert
        Assert.IsNotNull(receivedError);
        Assert.AreEqual(ErrorType.Validation, receivedError.Type);
        Assert.AreEqual("VALIDATION_NICKNAME", receivedError.Code);
        Assert.AreEqual("닉네임이 너무 짧습니다", receivedError.Message);
        Assert.AreEqual("Nickname Setup", receivedError.Context);
        Assert.AreEqual(ErrorSeverity.Medium, receivedError.Severity);
    }
    
    [Test]
    public void HandleSystemError_WithException_ShouldCreateProperErrorInfo()
    {
        // Arrange
        _errorHandler.Start();
        ErrorInfo receivedError = null;
        
        GlobalErrorHandler.OnErrorOccurred += (error) =>
        {
            receivedError = error;
        };
        
        var testException = new InvalidOperationException("Test system error");
        
        // Act
        _errorHandler.HandleSystemError(testException, "System Initialization");
        
        // Assert
        Assert.IsNotNull(receivedError);
        Assert.AreEqual(ErrorType.System, receivedError.Type);
        Assert.AreEqual("InvalidOperationException", receivedError.Code);
        Assert.AreEqual("Test system error", receivedError.Message);
        Assert.AreEqual("System Initialization", receivedError.Context);
        Assert.AreEqual(ErrorSeverity.High, receivedError.Severity);
        Assert.IsNotEmpty(receivedError.StackTrace);
    }
    #endregion
    
    #region Error Recovery Tests
    [Test]
    public void NotifyErrorRecovered_ShouldFireRecoveryEvent()
    {
        // Arrange
        _errorHandler.Start();
        bool recoveryEventFired = false;
        ErrorInfo recoveredError = null;
        
        GlobalErrorHandler.OnErrorRecovered += (error) =>
        {
            recoveryEventFired = true;
            recoveredError = error;
        };
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = "NETWORK_TIMEOUT",
            Message = "Network timeout recovered",
            Severity = ErrorSeverity.Medium,
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.NotifyErrorRecovered(errorInfo);
        
        // Assert
        Assert.IsTrue(recoveryEventFired);
        Assert.IsNotNull(recoveredError);
        Assert.AreEqual("NETWORK_TIMEOUT", recoveredError.Code);
    }
    #endregion
    
    #region Statistics Tests
    [Test]
    public void GetRecentErrors_WithMultipleErrors_ShouldReturnCorrectCount()
    {
        // Arrange
        _errorHandler.Start();
        
        // 여러 오류 추가
        for (int i = 0; i < 5; i++)
        {
            var error = new ErrorInfo
            {
                Type = ErrorType.Network,
                Code = $"TEST_ERROR_{i}",
                Message = $"Test error {i}",
                Severity = ErrorSeverity.Low,
                Timestamp = DateTime.Now.AddMinutes(-i)
            };
            _errorHandler.HandleError(error);
        }
        
        // Act
        var recentErrors = _errorHandler.GetRecentErrors(3);
        
        // Assert
        Assert.AreEqual(3, recentErrors.Count);
        Assert.AreEqual("TEST_ERROR_4", recentErrors[0].Code); // 가장 최근
    }
    
    [Test]
    public void GetErrorStatistics_ShouldReturnProperCounts()
    {
        // Arrange
        _errorHandler.Start();
        
        // 동일한 오류를 여러 번 발생
        for (int i = 0; i < 3; i++)
        {
            var error = new ErrorInfo
            {
                Type = ErrorType.Network,
                Code = "NETWORK_ERROR",
                Message = "Network error",
                Severity = ErrorSeverity.Medium,
                Timestamp = DateTime.Now
            };
            _errorHandler.HandleError(error);
        }
        
        // Act
        var statistics = _errorHandler.GetErrorStatistics();
        
        // Assert
        Assert.IsTrue(statistics.ContainsKey("Network_NETWORK_ERROR"));
        Assert.AreEqual(3, statistics["Network_NETWORK_ERROR"]);
    }
    #endregion
    
    #region Configuration Tests
    [Test]
    public void UpdateSettings_ShouldChangeConfiguration()
    {
        // Arrange
        _errorHandler.Start();
        
        // Act
        _errorHandler.UpdateSettings(false, false, 5);
        
        // Assert - 설정이 업데이트되었는지는 내부 동작으로만 확인 가능
        // 로깅이 비활성화되었는지 간접적으로 확인
        Assert.DoesNotThrow(() =>
        {
            var error = new ErrorInfo
            {
                Type = ErrorType.System,
                Code = "TEST_AFTER_SETTINGS",
                Message = "Test after settings change",
                Severity = ErrorSeverity.Low,
                Timestamp = DateTime.Now
            };
            _errorHandler.HandleError(error);
        });
    }
    #endregion
    
    #region Initialization Tests
    [UnityTest]
    public IEnumerator Initialization_ShouldCompleteWithoutErrors()
    {
        // Arrange & Act
        _errorHandler.Start();
        
        // Wait for initialization
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(_errorHandler.IsInitialized);
        Assert.IsTrue(_errorHandler.IsEnabled);
    }
    
    [Test]
    public void Initialization_ShouldSetupErrorHandlers()
    {
        // Arrange & Act
        _errorHandler.Start();
        
        // Assert - 각 오류 타입에 대한 핸들러가 설정되었는지 간접적으로 확인
        Assert.DoesNotThrow(() =>
        {
            // 각 타입별로 오류를 발생시켜 핸들러가 존재하는지 확인
            var networkError = new ErrorInfo { Type = ErrorType.Network, Code = "TEST", Message = "Test", Severity = ErrorSeverity.Low, Timestamp = DateTime.Now };
            var authError = new ErrorInfo { Type = ErrorType.Authentication, Code = "TEST", Message = "Test", Severity = ErrorSeverity.Low, Timestamp = DateTime.Now };
            var validationError = new ErrorInfo { Type = ErrorType.Validation, Code = "TEST", Message = "Test", Severity = ErrorSeverity.Low, Timestamp = DateTime.Now };
            
            _errorHandler.HandleError(networkError);
            _errorHandler.HandleError(authError);
            _errorHandler.HandleError(validationError);
        });
    }
    #endregion
    
    #region Edge Cases
    [Test]
    public void HandleError_RateLimiting_ShouldPreventSpam()
    {
        // Arrange
        _errorHandler.Start();
        int eventCount = 0;
        
        GlobalErrorHandler.OnErrorOccurred += (error) => eventCount++;
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = "SPAM_ERROR",
            Message = "Spam error",
            Severity = ErrorSeverity.Low,
            Timestamp = DateTime.Now
        };
        
        // Act - 동일한 오류를 빠르게 여러 번 발생
        for (int i = 0; i < 20; i++)
        {
            _errorHandler.HandleError(errorInfo);
        }
        
        // Assert - 모든 오류가 처리되지 않아야 함 (레이트 리미팅)
        Assert.Less(eventCount, 20);
    }
    
    [Test]
    public void MultipleEventHandlers_ShouldAllBeInvoked()
    {
        // Arrange
        _errorHandler.Start();
        int eventCount = 0;
        
        GlobalErrorHandler.OnErrorOccurred += (error) => eventCount++;
        GlobalErrorHandler.OnErrorOccurred += (error) => eventCount++;
        GlobalErrorHandler.OnErrorOccurred += (error) => eventCount++;
        
        var errorInfo = new ErrorInfo
        {
            Type = ErrorType.Network,
            Code = "MULTI_HANDLER_TEST",
            Message = "Multi handler test",
            Severity = ErrorSeverity.Low,
            Timestamp = DateTime.Now
        };
        
        // Act
        _errorHandler.HandleError(errorInfo);
        
        // Assert
        Assert.AreEqual(3, eventCount);
    }
    #endregion
}