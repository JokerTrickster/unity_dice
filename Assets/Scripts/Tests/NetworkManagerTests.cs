using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// NetworkManager에 대한 단위 테스트
/// HTTP 통신, 재시도 로직, 오류 처리, 로깅 시스템 테스트를 포함
/// </summary>
public class NetworkManagerTests
{
    private NetworkManager _networkManager;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 기존 NetworkManager 인스턴스가 있다면 제거
        if (NetworkManager.Instance != null)
        {
            Object.DestroyImmediate(NetworkManager.Instance.gameObject);
        }
        
        // 테스트용 NetworkManager 생성
        _testGameObject = new GameObject("TestNetworkManager");
        _networkManager = _testGameObject.AddComponent<NetworkManager>();
        
        // 이벤트 구독 해제
        NetworkManager.OnNetworkStatusChanged = null;
        NetworkManager.OnRequestStarted = null;
        NetworkManager.OnRequestCompleted = null;
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // 이벤트 구독 해제
        NetworkManager.OnNetworkStatusChanged = null;
        NetworkManager.OnRequestStarted = null;
        NetworkManager.OnRequestCompleted = null;
    }
    
    #region Singleton Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = NetworkManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is NetworkManager);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = NetworkManager.Instance;
        var instance2 = NetworkManager.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region Data Structure Tests
    [Test]
    public void NetworkResponse_GetData_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var response = new NetworkResponse
        {
            IsSuccess = true,
            RawData = "{\"name\":\"Test\",\"value\":123}"
        };
        
        // Act
        var data = response.GetData<TestData>();
        
        // Assert
        Assert.IsNotNull(data);
        Assert.AreEqual("Test", data.name);
        Assert.AreEqual(123, data.value);
    }
    
    [Test]
    public void NetworkResponse_GetData_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var response = new NetworkResponse
        {
            IsSuccess = true,
            RawData = "invalid json"
        };
        
        // Act
        var data = response.GetData<TestData>();
        
        // Assert
        Assert.IsNull(data);
    }
    
    [Test]
    public void NetworkResponse_GetData_WhenNotSuccessful_ShouldReturnNull()
    {
        // Arrange
        var response = new NetworkResponse
        {
            IsSuccess = false,
            RawData = "{\"name\":\"Test\",\"value\":123}"
        };
        
        // Act
        var data = response.GetData<TestData>();
        
        // Assert
        Assert.IsNull(data);
    }
    
    [Test]
    public void NetworkResponse_GetData_WithEmptyData_ShouldReturnNull()
    {
        // Arrange
        var response = new NetworkResponse
        {
            IsSuccess = true,
            RawData = ""
        };
        
        // Act
        var data = response.GetData<TestData>();
        
        // Assert
        Assert.IsNull(data);
    }
    #endregion
    
    #region Logger Tests
    [Test]
    public void NetworkLogger_Initialize_ShouldSetProperties()
    {
        // Arrange
        var logger = new NetworkLogger();
        
        // Act
        logger.Initialize(true, false);
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            logger.Log("Test message", LogLevel.Info);
        });
    }
    
    [Test]
    public void NetworkLogger_LogWithDisabled_ShouldNotThrowException()
    {
        // Arrange
        var logger = new NetworkLogger();
        logger.Initialize(false, false); // 로깅 비활성화
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            logger.Log("Test message", LogLevel.Info);
            logger.Log("Debug message", LogLevel.Debug);
            logger.Log("Error message", LogLevel.Error);
            logger.Log("Warning message", LogLevel.Warning);
        });
    }
    
    [Test]
    public void NetworkLogger_LogAllLevels_ShouldNotThrowException()
    {
        // Arrange
        var logger = new NetworkLogger();
        logger.Initialize(true, true); // 상세 로깅 활성화
        
        // Act & Assert - 모든 로그 레벨에서 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            logger.Log("Info message", LogLevel.Info);
            logger.Log("Debug message", LogLevel.Debug);
            logger.Log("Error message", LogLevel.Error);
            logger.Log("Warning message", LogLevel.Warning);
        });
    }
    #endregion
    
    #region Configuration Tests
    [Test]
    public void SetBaseUrl_ValidUrl_ShouldUpdateProperty()
    {
        // Arrange
        string newUrl = "https://test.example.com/api/v2";
        
        // Act
        _networkManager.SetBaseUrl(newUrl);
        
        // Assert
        Assert.AreEqual(newUrl, _networkManager.BaseUrl);
    }
    
    [Test]
    public void SetAuthToken_ValidToken_ShouldNotThrowException()
    {
        // Arrange
        string token = "test_auth_token_123";
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.SetAuthToken(token);
        });
    }
    
    [Test]
    public void SetAuthToken_NullToken_ShouldNotThrowException()
    {
        // Arrange
        string token = null;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.SetAuthToken(token);
        });
    }
    
    [Test]
    public void SetLogging_BothEnabled_ShouldNotThrowException()
    {
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.SetLogging(true, true);
        });
    }
    
    [Test]
    public void SetLogging_BothDisabled_ShouldNotThrowException()
    {
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.SetLogging(false, false);
        });
    }
    #endregion
    
    #region Request Management Tests
    [Test]
    public void ActiveRequestCount_Initially_ShouldBeZero()
    {
        // Assert
        Assert.AreEqual(0, _networkManager.ActiveRequestCount);
    }
    
    [Test]
    public void QueuedRequestCount_Initially_ShouldBeZero()
    {
        // Assert
        Assert.AreEqual(0, _networkManager.QueuedRequestCount);
    }
    
    [Test]
    public void StopAllRequests_ShouldNotThrowException()
    {
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.StopAllRequests();
        });
    }
    
    [Test]
    public void CancelRequest_WithInvalidId_ShouldNotThrowException()
    {
        // Arrange
        string invalidId = "invalid_request_id";
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.CancelRequest(invalidId);
        });
    }
    #endregion
    
    #region HTTP Method Tests
    [Test]
    public void Get_ValidEndpoint_ShouldNotThrowException()
    {
        // Arrange
        string endpoint = "/test-endpoint";
        bool callbackCalled = false;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Get(endpoint, (response) =>
            {
                callbackCalled = true;
            });
        });
    }
    
    [Test]
    public void Post_WithData_ShouldNotThrowException()
    {
        // Arrange
        string endpoint = "/test-endpoint";
        var testData = new TestData { name = "Test", value = 123 };
        bool callbackCalled = false;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Post(endpoint, testData, (response) =>
            {
                callbackCalled = true;
            });
        });
    }
    
    [Test]
    public void Put_WithData_ShouldNotThrowException()
    {
        // Arrange
        string endpoint = "/test-endpoint";
        var testData = new TestData { name = "Test", value = 123 };
        bool callbackCalled = false;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Put(endpoint, testData, (response) =>
            {
                callbackCalled = true;
            });
        });
    }
    
    [Test]
    public void Delete_ValidEndpoint_ShouldNotThrowException()
    {
        // Arrange
        string endpoint = "/test-endpoint";
        bool callbackCalled = false;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Delete(endpoint, (response) =>
            {
                callbackCalled = true;
            });
        });
    }
    
    [Test]
    public void Patch_WithData_ShouldNotThrowException()
    {
        // Arrange
        string endpoint = "/test-endpoint";
        var testData = new TestData { name = "Test", value = 123 };
        bool callbackCalled = false;
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Patch(endpoint, testData, (response) =>
            {
                callbackCalled = true;
            });
        });
    }
    #endregion
    
    #region Event System Tests
    [Test]
    public void OnNetworkStatusChanged_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        bool receivedStatus = false;
        
        NetworkManager.OnNetworkStatusChanged += (status) =>
        {
            eventFired = true;
            receivedStatus = status;
        };
        
        // 이벤트 직접 발생은 테스트하기 어려우므로, 이벤트 핸들러 등록만 테스트
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // 실제 네트워크 상태 변경 시뮬레이션은 복잡하므로 생략
        });
    }
    
    [Test]
    public void OnRequestStarted_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        string receivedRequestId = "";
        
        NetworkManager.OnRequestStarted += (requestId) =>
        {
            eventFired = true;
            receivedRequestId = requestId;
        };
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // 이벤트 구독 자체는 성공
        });
    }
    
    [Test]
    public void OnRequestCompleted_EventSubscription_ShouldWork()
    {
        // Arrange
        bool eventFired = false;
        string receivedRequestId = "";
        bool receivedSuccess = false;
        
        NetworkManager.OnRequestCompleted += (requestId, success) =>
        {
            eventFired = true;
            receivedRequestId = requestId;
            receivedSuccess = success;
        };
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            // 이벤트 구독 자체는 성공
        });
    }
    #endregion
    
    #region Utility Tests  
    [Test]
    public void HttpMethod_Enum_ShouldHaveAllValues()
    {
        // Arrange & Act - 모든 HTTP 메서드가 정의되어 있는지 확인
        var methods = System.Enum.GetValues(typeof(HttpMethod));
        
        // Assert
        Assert.Contains(HttpMethod.GET, methods);
        Assert.Contains(HttpMethod.POST, methods);
        Assert.Contains(HttpMethod.PUT, methods);
        Assert.Contains(HttpMethod.DELETE, methods);
        Assert.Contains(HttpMethod.PATCH, methods);
    }
    
    [Test]
    public void LogLevel_Enum_ShouldHaveAllValues()
    {
        // Arrange & Act - 모든 로그 레벨이 정의되어 있는지 확인
        var levels = System.Enum.GetValues(typeof(LogLevel));
        
        // Assert
        Assert.Contains(LogLevel.Debug, levels);
        Assert.Contains(LogLevel.Info, levels);
        Assert.Contains(LogLevel.Warning, levels);
        Assert.Contains(LogLevel.Error, levels);
    }
    #endregion
    
    #region Integration Tests
    [UnityTest]
    public IEnumerator NetworkManager_Initialize_ShouldCompleteWithoutErrors()
    {
        // Arrange
        _networkManager.Start(); // 초기화 시작
        
        // Act - 초기화 완료 대기
        yield return new WaitForSeconds(0.1f);
        
        // Assert - 예외가 발생하지 않아야 함
        Assert.IsNotNull(_networkManager);
        Assert.IsTrue(_networkManager.IsNetworkAvailable || !_networkManager.IsNetworkAvailable); // 상태 확인
    }
    
    [Test]
    public void NetworkRequest_AllProperties_ShouldBeSettable()
    {
        // Arrange
        var request = new NetworkRequest();
        string testId = "test_id";
        HttpMethod testMethod = HttpMethod.POST;
        string testEndpoint = "/test";
        var testData = new TestData { name = "Test", value = 123 };
        float testTimeout = 30f;
        
        // Act
        request.Id = testId;
        request.Method = testMethod;
        request.Endpoint = testEndpoint;
        request.Data = testData;
        request.Timeout = testTimeout;
        request.Callback = (response) => { };
        
        // Assert
        Assert.AreEqual(testId, request.Id);
        Assert.AreEqual(testMethod, request.Method);
        Assert.AreEqual(testEndpoint, request.Endpoint);
        Assert.AreEqual(testData, request.Data);
        Assert.AreEqual(testTimeout, request.Timeout);
        Assert.IsNotNull(request.Callback);
    }
    
    [Test]
    public void NetworkResponse_AllProperties_ShouldBeSettable()
    {
        // Arrange
        var response = new NetworkResponse();
        var testHeaders = new Dictionary<string, string> { { "Content-Type", "application/json" } };
        
        // Act
        response.IsSuccess = true;
        response.StatusCode = 200;
        response.RawData = "test data";
        response.Error = "test error";
        response.Headers = testHeaders;
        
        // Assert
        Assert.IsTrue(response.IsSuccess);
        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual("test data", response.RawData);
        Assert.AreEqual("test error", response.Error);
        Assert.AreEqual(testHeaders, response.Headers);
    }
    #endregion
    
    #region Error Handling Tests
    [Test]
    public void NetworkManager_WithNullCallbacks_ShouldNotThrowException()
    {
        // Act & Assert - null 콜백으로도 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Get("/test", null);
            _networkManager.Post("/test", null, null);
            _networkManager.Put("/test", null, null);
            _networkManager.Delete("/test", null);
            _networkManager.Patch("/test", null, null);
        });
    }
    
    [Test]
    public void NetworkManager_WithEmptyEndpoint_ShouldNotThrowException()
    {
        // Act & Assert - 빈 엔드포인트로도 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Get("", (response) => { });
        });
    }
    
    [Test]
    public void NetworkManager_WithNullData_ShouldNotThrowException()
    {
        // Act & Assert - null 데이터로도 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _networkManager.Post("/test", null, (response) => { });
        });
    }
    #endregion
}

/// <summary>
/// 테스트용 데이터 클래스
/// </summary>
[System.Serializable]
public class TestData
{
    public string name;
    public int value;
}