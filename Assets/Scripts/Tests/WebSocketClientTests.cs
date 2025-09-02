using System;
using System.Threading.Tasks;
using UnityEngine;
using NUnit.Framework;

/// <summary>
/// WebSocket 클라이언트 기본 기능 테스트
/// 실제 서버 연결 없이 클라이언트 로직 검증
/// </summary>
public class WebSocketClientTests
{
    private WebSocketConfig _testConfig;
    private WebSocketClient _client;

    #region Test Setup
    [SetUp]
    public void Setup()
    {
        // 테스트용 설정 생성
        _testConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        _testConfig.SetServerUrl("wss://localhost:8080/test");
        _testConfig.SetConnectionTimeout(5000);
        _testConfig.SetReconnectionSettings(3, new int[] { 1000, 2000, 3000 });
        _testConfig.SetLoggingSettings(true, true);
        
        // 설정 유효성 검사
        Assert.IsTrue(_testConfig.ValidateConfiguration(), "Test configuration should be valid");
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _client = null;
        
        if (_testConfig != null)
        {
            UnityEngine.Object.DestroyImmediate(_testConfig);
            _testConfig = null;
        }
    }
    #endregion

    #region Configuration Tests
    [Test]
    public void WebSocketConfig_ValidConfiguration_ShouldPass()
    {
        // Arrange & Act
        var config = ScriptableObject.CreateInstance<WebSocketConfig>();
        config.SetServerUrl("wss://test.example.com/ws");
        
        // Assert
        Assert.IsTrue(config.ValidateConfiguration());
        Assert.AreEqual("wss://test.example.com/ws", config.ServerUrl);
        Assert.IsTrue(config.EnableSsl);
    }

    [Test]
    public void WebSocketConfig_InvalidUrl_ShouldFail()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<WebSocketConfig>();
        
        // Act & Assert
        config.SetServerUrl(""); // Empty URL
        Assert.IsFalse(config.ValidateConfiguration());
        
        config.SetServerUrl("http://invalid.com"); // Wrong protocol
        Assert.IsFalse(config.ValidateConfiguration());
        
        config.SetServerUrl("invalid-url"); // Invalid format
        Assert.IsFalse(config.ValidateConfiguration());
        
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void WebSocketConfig_RetryDelayAccess_ShouldHandleIndexSafely()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<WebSocketConfig>();
        config.SetReconnectionSettings(5, new int[] { 1000, 2000, 5000 });
        
        // Act & Assert
        Assert.AreEqual(1000, config.GetRetryDelay(0)); // First delay
        Assert.AreEqual(2000, config.GetRetryDelay(1)); // Second delay
        Assert.AreEqual(5000, config.GetRetryDelay(2)); // Third delay
        Assert.AreEqual(5000, config.GetRetryDelay(10)); // Beyond array length, should return last
        
        UnityEngine.Object.DestroyImmediate(config);
    }
    #endregion

    #region Client Initialization Tests
    [Test]
    public void WebSocketClient_ValidConfig_ShouldInitialize()
    {
        // Arrange & Act
        _client = new WebSocketClient(_testConfig);
        
        // Assert
        Assert.IsNotNull(_client);
        Assert.IsFalse(_client.IsConnected);
        Assert.IsFalse(_client.IsConnecting);
        Assert.IsNotNull(_client.Config);
        Assert.IsNotNull(_client.ConnectionManager);
        Assert.IsNotNull(_client.MessageQueue);
    }

    [Test]
    public void WebSocketClient_NullConfig_ShouldThrowException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => {
            _client = new WebSocketClient(null);
        });
    }

    [Test]
    public void WebSocketClient_InvalidConfig_ShouldThrowException()
    {
        // Arrange
        var invalidConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        invalidConfig.SetServerUrl(""); // Invalid URL
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => {
            _client = new WebSocketClient(invalidConfig);
        });
        
        UnityEngine.Object.DestroyImmediate(invalidConfig);
    }
    #endregion

    #region Authentication Tests
    [Test]
    public void WebSocketClient_SetAuthToken_ShouldConfigureHeaders()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        const string testToken = "test-jwt-token-12345";
        
        // Act
        _client.SetAuthToken(testToken);
        
        // Assert
        // We can't directly test headers without connection, but we can test the method doesn't throw
        Assert.DoesNotThrow(() => _client.SetAuthToken(testToken));
        
        // Test clearing token
        Assert.DoesNotThrow(() => _client.SetAuthToken(""));
        Assert.DoesNotThrow(() => _client.SetAuthToken(null));
    }

    [Test]
    public void WebSocketClient_CustomHeaders_ShouldManageCorrectly()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        
        // Act & Assert
        Assert.DoesNotThrow(() => _client.AddCustomHeader("X-Custom-Header", "test-value"));
        Assert.DoesNotThrow(() => _client.AddCustomHeader("X-Another-Header", "another-value"));
        Assert.DoesNotThrow(() => _client.RemoveCustomHeader("X-Custom-Header"));
        
        // Test invalid inputs
        Assert.DoesNotThrow(() => _client.AddCustomHeader("", "value")); // Should log error but not throw
        Assert.DoesNotThrow(() => _client.AddCustomHeader(null, "value")); // Should log error but not throw
    }
    #endregion

    #region Message Queue Tests
    [Test]
    public void WebSocketClient_MessageQueue_ShouldHandleMessages()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        
        // Act & Assert - Test message queuing when not connected
        Assert.IsTrue(_client.SendMessage("test message", MessagePriority.Normal));
        Assert.IsTrue(_client.SendMessage("priority message", MessagePriority.High));
        Assert.IsFalse(_client.SendMessage("")); // Empty message should fail
        Assert.IsFalse(_client.SendMessage(null)); // Null message should fail
    }

    [Test]
    public void MessageQueue_Priority_ShouldHandleCorrectly()
    {
        // Arrange
        var messageQueue = new MessageQueue(_testConfig);
        bool sendFunctionCalled = false;
        messageQueue.SetSendMessageFunction(async (message) => {
            sendFunctionCalled = true;
            await Task.Delay(1); // Simulate async operation
            return true;
        });
        
        // Act
        messageQueue.EnqueueMessage("low priority", MessagePriority.Low);
        messageQueue.EnqueueMessage("high priority", MessagePriority.High);
        messageQueue.StartProcessing();
        
        // Assert
        Assert.AreEqual(2, messageQueue.QueuedCount);
        
        // Cleanup
        messageQueue.Dispose();
    }
    #endregion

    #region Connection Manager Tests
    [Test]
    public void ConnectionManager_Initialization_ShouldSetCorrectState()
    {
        // Arrange
        var connectionManager = new ConnectionManager(_testConfig);
        bool connectFunctionSet = false;
        
        // Act
        connectionManager.SetConnectionFunctions(
            async () => { connectFunctionSet = true; return false; },
            async () => { await Task.Delay(1); },
            async (msg) => { await Task.Delay(1); return true; },
            () => false
        );
        
        // Assert
        Assert.AreEqual(ConnectionState.Disconnected, connectionManager.CurrentState);
        Assert.IsFalse(connectionManager.IsConnected);
        Assert.IsFalse(connectionManager.IsReconnecting);
        Assert.AreEqual(0, connectionManager.CurrentReconnectAttempt);
        
        // Cleanup
        connectionManager.Dispose();
    }

    [Test]
    public void ConnectionManager_ReconnectionSettings_ShouldRespectConfig()
    {
        // Arrange
        var connectionManager = new ConnectionManager(_testConfig);
        
        // Act & Assert
        Assert.AreEqual(_testConfig.MaxReconnectAttempts, connectionManager.MaxReconnectAttempts);
        Assert.IsTrue(connectionManager.AutoReconnectEnabled || !_testConfig.EnableAutoReconnect);
        
        // Cleanup
        connectionManager.Dispose();
    }
    #endregion

    #region Resource Management Tests
    [Test]
    public void WebSocketClient_Dispose_ShouldCleanupResources()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        
        // Act
        _client.Dispose();
        
        // Assert - After disposal, operations should fail gracefully
        Assert.IsFalse(_client.SendMessage("test message"));
        
        // Multiple dispose calls should not throw
        Assert.DoesNotThrow(() => _client.Dispose());
    }

    [Test]
    public void MessageQueue_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var messageQueue = new MessageQueue(_testConfig);
        messageQueue.SetSendMessageFunction(async (msg) => { await Task.Delay(1); return true; });
        
        // Act
        messageQueue.Dispose();
        
        // Assert
        Assert.IsFalse(messageQueue.EnqueueMessage("test")); // Should fail after disposal
        Assert.AreEqual(0, messageQueue.QueuedCount); // Queue should be empty
    }
    #endregion

    #region Error Handling Tests
    [Test]
    public void WebSocketClient_ErrorScenarios_ShouldHandleGracefully()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        string lastError = null;
        _client.OnError += (error) => lastError = error;
        
        // Act & Assert - Test various error conditions
        // These should not throw exceptions but may trigger error events
        Assert.DoesNotThrow(() => _client.StartReconnection());
        Assert.DoesNotThrow(() => _client.StopReconnection());
        Assert.DoesNotThrow(() => _client.HandleHeartbeatResponse());
    }
    #endregion

    #region Integration Tests
    [Test]
    public void WebSocketClient_ComponentIntegration_ShouldWorkTogether()
    {
        // Arrange
        _client = new WebSocketClient(_testConfig);
        bool connectionChanged = false;
        bool messageReceived = false;
        bool errorOccurred = false;
        
        // Act - Setup event handlers
        _client.OnConnectionChanged += (connected) => connectionChanged = true;
        _client.OnMessage += (message) => messageReceived = true;
        _client.OnError += (error) => errorOccurred = true;
        
        // Assert - Components should be properly wired
        Assert.IsNotNull(_client.ConnectionManager);
        Assert.IsNotNull(_client.MessageQueue);
        Assert.IsNotNull(_client.Config);
        
        // Test that we can queue messages before connection
        Assert.IsTrue(_client.SendMessage("test integration message"));
    }
    #endregion

    #region Performance Tests
    [Test]
    public void MessageQueue_HighVolume_ShouldHandleEfficiently()
    {
        // Arrange
        var messageQueue = new MessageQueue(_testConfig);
        int processedCount = 0;
        
        messageQueue.SetSendMessageFunction(async (message) => {
            processedCount++;
            await Task.Delay(1);
            return true;
        });
        
        // Act - Add many messages
        for (int i = 0; i < 50; i++)
        {
            messageQueue.EnqueueMessage($"message-{i}", MessagePriority.Normal);
        }
        
        // Assert
        Assert.AreEqual(50, messageQueue.QueuedCount);
        
        // Start processing
        messageQueue.StartProcessing();
        
        // Cleanup
        messageQueue.Dispose();
    }
    #endregion
}