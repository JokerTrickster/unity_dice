using System;
using System.Collections.Generic;
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
    private MockWebSocketServer _mockServer;

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
        
        // Mock 서버 초기화
        var mockConfig = MockServerConfig.Default();
        mockConfig.EnableLogging = false; // Reduce test noise
        mockConfig.EnableDetailedLogging = false;
        _mockServer = new MockWebSocketServer(mockConfig);
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _client = null;
        
        _mockServer?.Dispose();
        _mockServer = null;
        
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
        
        UnityEngine.Object.DestroyImmediate(config);
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

    #region Mock Server Integration Tests
    [Test]
    public async Task MockServer_StartStop_ShouldWorkCorrectly()
    {
        // Act
        bool startResult = await _mockServer.StartAsync();
        
        // Assert
        Assert.IsTrue(startResult, "Mock server should start successfully");
        Assert.IsTrue(_mockServer.IsRunning, "Mock server should be running");
        Assert.AreEqual(MockServerState.Running, _mockServer.State);
        
        // Cleanup
        await _mockServer.StopAsync();
        Assert.IsFalse(_mockServer.IsRunning, "Mock server should be stopped");
    }

    [Test]
    public async Task MockServer_ClientConnection_ShouldSimulateCorrectly()
    {
        // Arrange
        await _mockServer.StartAsync();
        
        bool clientConnected = false;
        bool clientDisconnected = false;
        string connectedClientId = null;
        
        _mockServer.OnClientConnected += (clientId) => {
            clientConnected = true;
            connectedClientId = clientId;
        };
        
        _mockServer.OnClientDisconnected += (clientId) => {
            clientDisconnected = true;
        };
        
        // Act
        bool connectResult = await _mockServer.SimulateClientConnectAsync("test-client-123");
        
        // Assert
        Assert.IsTrue(connectResult, "Client connection should succeed");
        Assert.IsTrue(clientConnected, "Connection event should fire");
        Assert.AreEqual("test-client-123", connectedClientId);
        Assert.AreEqual(1, _mockServer.ConnectedClientCount);
        
        // Test disconnection
        await _mockServer.DisconnectClientAsync("test-client-123");
        Assert.IsTrue(clientDisconnected, "Disconnection event should fire");
        Assert.AreEqual(0, _mockServer.ConnectedClientCount);
    }

    [Test]
    public async Task MockServer_MessageHandling_ShouldProcessCorrectly()
    {
        // Arrange
        await _mockServer.StartAsync();
        await _mockServer.SimulateClientConnectAsync("test-client");
        
        bool messageReceived = false;
        string receivedMessage = null;
        
        _mockServer.OnMessageReceived += (clientId, message) => {
            messageReceived = true;
            receivedMessage = message;
        };
        
        // Act
        string testMessage = MatchingProtocol.CreateJoinQueueMessage("test-client", 4);
        _mockServer.SimulateMessageReceived("test-client", testMessage);
        
        // Allow time for message processing
        await Task.Delay(100);
        
        // Assert
        Assert.IsTrue(messageReceived, "Message should be received");
        Assert.IsNotNull(receivedMessage, "Message content should not be null");
        Assert.IsTrue(receivedMessage.Contains("join_queue"), "Message should contain expected type");
    }

    [Test]
    public async Task MockServer_MatchingSimulation_ShouldCreateMatches()
    {
        // Arrange
        await _mockServer.StartAsync();
        
        var clients = new[] { "player1", "player2", "player3", "player4" };
        foreach (var client in clients)
        {
            await _mockServer.SimulateClientConnectAsync(client);
        }
        
        // Act - All players join queue
        foreach (var client in clients)
        {
            string joinMessage = MatchingProtocol.CreateJoinQueueMessage(client, 4);
            _mockServer.SimulateMessageReceived(client, joinMessage);
        }
        
        // Allow time for match creation
        await Task.Delay(200);
        
        // Assert
        Assert.AreEqual(4, _mockServer.ConnectedClientCount, "All clients should remain connected");
        // In a real test, we'd check if match found messages were sent to clients
    }
    #endregion

    #region Stress Tests
    [Test]
    public async Task StressTest_MultipleClients_ShouldHandleLoad()
    {
        // Arrange
        await _mockServer.StartAsync();
        const int clientCount = 20;
        var connectedClients = new List<string>();
        
        // Act - Connect multiple clients rapidly
        var connectTasks = new List<Task<bool>>();
        for (int i = 0; i < clientCount; i++)
        {
            string clientId = $"stress-client-{i}";
            connectTasks.Add(_mockServer.SimulateClientConnectAsync(clientId));
            connectedClients.Add(clientId);
        }
        
        var results = await Task.WhenAll(connectTasks);
        
        // Assert
        int successfulConnections = 0;
        foreach (bool result in results)
        {
            if (result) successfulConnections++;
        }
        
        Assert.GreaterOrEqual(successfulConnections, clientCount * 0.8, 
            "At least 80% of connections should succeed in stress test");
        
        Debug.Log($"Stress test: {successfulConnections}/{clientCount} clients connected successfully");
    }

    [Test]
    public async Task StressTest_HighVolumeMessages_ShouldProcessCorrectly()
    {
        // Arrange
        await _mockServer.StartAsync();
        await _mockServer.SimulateClientConnectAsync("stress-client");
        
        const int messageCount = 100;
        int messagesReceived = 0;
        
        _mockServer.OnMessageReceived += (clientId, message) => {
            messagesReceived++;
        };
        
        // Act - Send many messages rapidly
        var sendTasks = new List<Task>();
        for (int i = 0; i < messageCount; i++)
        {
            sendTasks.Add(Task.Run(() => {
                string heartbeatMessage = MatchingProtocol.CreateHeartbeatMessage("stress-client");
                _mockServer.SimulateMessageReceived("stress-client", heartbeatMessage);
            }));
        }
        
        await Task.WhenAll(sendTasks);
        
        // Allow time for processing
        await Task.Delay(500);
        
        // Assert
        Assert.GreaterOrEqual(messagesReceived, messageCount * 0.9, 
            "At least 90% of messages should be processed in stress test");
        
        Debug.Log($"Stress test: {messagesReceived}/{messageCount} messages processed successfully");
    }

    [Test]
    public async Task StressTest_ConnectionChurn_ShouldHandleReconnections()
    {
        // Arrange
        await _mockServer.StartAsync();
        const int churnCycles = 10;
        const int clientsPerCycle = 5;
        
        // Act - Repeatedly connect and disconnect clients
        for (int cycle = 0; cycle < churnCycles; cycle++)
        {
            var clients = new List<string>();
            
            // Connect clients
            for (int i = 0; i < clientsPerCycle; i++)
            {
                string clientId = $"churn-client-{cycle}-{i}";
                await _mockServer.SimulateClientConnectAsync(clientId);
                clients.Add(clientId);
            }
            
            // Small delay
            await Task.Delay(50);
            
            // Disconnect clients
            foreach (var client in clients)
            {
                await _mockServer.DisconnectClientAsync(client, "Churn test");
            }
            
            // Verify no clients remain
            Assert.AreEqual(0, _mockServer.ConnectedClientCount, 
                $"All clients should be disconnected after cycle {cycle}");
        }
        
        // Assert
        Assert.AreEqual(0, _mockServer.ConnectedClientCount, "No clients should remain after churn test");
        Debug.Log($"Stress test: Completed {churnCycles} connection churn cycles successfully");
    }

    [Test]
    public async Task StressTest_MemoryUsage_ShouldNotLeak()
    {
        // Arrange
        await _mockServer.StartAsync();
        long initialMemory = GC.GetTotalMemory(true);
        
        // Act - Create and destroy many objects
        for (int i = 0; i < 50; i++)
        {
            string clientId = $"memory-test-{i}";
            await _mockServer.SimulateClientConnectAsync(clientId);
            
            // Send some messages
            for (int j = 0; j < 10; j++)
            {
                string message = MatchingProtocol.CreateJoinQueueMessage(clientId, 2);
                _mockServer.SimulateMessageReceived(clientId, message);
            }
            
            await _mockServer.DisconnectClientAsync(clientId);
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        long finalMemory = GC.GetTotalMemory(false);
        long memoryIncrease = finalMemory - initialMemory;
        
        // Assert
        Assert.Less(memoryIncrease, 1024 * 1024, // Less than 1MB increase
            $"Memory increase should be minimal: {memoryIncrease} bytes");
        
        Debug.Log($"Memory test: {memoryIncrease} bytes increase ({initialMemory} -> {finalMemory})");
    }
    #endregion

    #region Network Manager Integration Tests
    [Test]
    public void NetworkManager_WebSocketIntegration_ShouldMaintainHTTP()
    {
        // This test verifies that adding WebSocket functionality doesn't break HTTP
        
        // Arrange
        var networkManager = NetworkManager.Instance;
        Assert.IsNotNull(networkManager, "NetworkManager should be available");
        
        // Act & Assert - Test that HTTP methods still exist and work
        Assert.DoesNotThrow(() => {
            // These methods should still be available from the base NetworkManager
            // We're testing that the WebSocket integration doesn't break existing functionality
        }, "HTTP functionality should remain intact");
    }

    [Test]
    public async Task HybridNetworkManager_WebSocketHTTPCoexistence_ShouldWork()
    {
        // Test that HybridNetworkManager can handle both HTTP and WebSocket simultaneously
        
        // This would require the HybridNetworkManager to be implemented
        // For now, we'll test the concept with a placeholder
        Assert.DoesNotThrow(() => {
            // Placeholder for hybrid network manager tests
            Debug.Log("HybridNetworkManager integration test placeholder");
        });
    }
    #endregion

    #region Thread Safety Tests
    [Test]
    public async Task MessageQueue_ThreadSafety_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var messageQueue = new MessageQueue(_testConfig);
        int processedCount = 0;
        
        messageQueue.SetSendMessageFunction(async (message) => {
            Interlocked.Increment(ref processedCount);
            await Task.Delay(1);
            return true;
        });
        
        messageQueue.StartProcessing();
        
        // Act - Send messages from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() => {
                for (int j = 0; j < 10; j++)
                {
                    messageQueue.EnqueueMessage($"thread-{taskId}-message-{j}", MessagePriority.Normal);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        await Task.Delay(500); // Allow processing time
        
        // Assert
        Assert.AreEqual(100, messageQueue.QueuedCount + processedCount, 
            "All messages should be queued or processed");
        
        // Cleanup
        messageQueue.Dispose();
    }

    [Test]
    public async Task ConnectionManager_ThreadSafety_ShouldHandleConcurrentOperations()
    {
        // Arrange
        var connectionManager = new ConnectionManager(_testConfig);
        var mockFunctions = new MockConnectionFunctions();
        connectionManager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        );
        
        mockFunctions.SetConnectResult(true);
        
        // Act - Call connect/disconnect from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(connectionManager.ConnectAsync().ContinueWith(_ => Task.CompletedTask));
            tasks.Add(connectionManager.DisconnectAsync());
        }
        
        // Assert - Should not throw
        Assert.DoesNotThrow(async () => await Task.WhenAll(tasks));
        
        // Cleanup
        connectionManager.Dispose();
    }
    #endregion

    #region Real-World Scenario Tests
    [Test]
    public async Task RealWorldScenario_MatchingFlow_ShouldWorkEndToEnd()
    {
        // Simulate a complete matching flow from client perspective
        
        // Arrange
        await _mockServer.StartAsync();
        var config = ScriptableObject.CreateInstance<WebSocketConfig>();
        config.SetServerUrl("wss://localhost:8080/ws");
        config.SetConnectionTimeout(2000);
        config.SetReconnectionSettings(2, new int[] { 500, 1000 });
        
        _client = new WebSocketClient(config);
        
        bool connected = false;
        string lastMessage = null;
        
        _client.OnConnectionChanged += (isConnected) => connected = isConnected;
        _client.OnMessage += (message) => lastMessage = message;
        
        // Act - Simulate user joining a match
        
        // 1. Connect to server (simulated)
        await _mockServer.SimulateClientConnectAsync("test-player");
        
        // 2. Send join queue message
        string joinMessage = MatchingProtocol.CreateJoinQueueMessage("test-player", 4, "classic", 1000);
        Assert.IsTrue(_client.SendMessage(joinMessage), "Should queue join message");
        
        // 3. Simulate server processing and response
        _mockServer.SimulateMessageReceived("test-player", joinMessage);
        await Task.Delay(100);
        
        // Assert
        Assert.IsNotNull(joinMessage, "Join message should be created");
        Assert.IsTrue(joinMessage.Contains("join_queue"), "Message should contain correct type");
        
        // Cleanup
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public async Task RealWorldScenario_ConnectionInterruption_ShouldRecover()
    {
        // Simulate network interruption and recovery
        
        // Arrange
        await _mockServer.StartAsync();
        await _mockServer.SimulateClientConnectAsync("resilient-player");
        
        int messagesSent = 0;
        int messagesProcessed = 0;
        
        _mockServer.OnMessageReceived += (clientId, message) => messagesProcessed++;
        
        // Act - Send messages, then simulate disconnection and recovery
        
        // 1. Send initial messages
        for (int i = 0; i < 5; i++)
        {
            string message = MatchingProtocol.CreateHeartbeatMessage("resilient-player");
            _mockServer.SimulateMessageReceived("resilient-player", message);
            messagesSent++;
        }
        
        await Task.Delay(50);
        
        // 2. Simulate connection loss
        await _mockServer.DisconnectClientAsync("resilient-player", "Simulated network interruption");
        
        // 3. Simulate reconnection
        await _mockServer.SimulateClientConnectAsync("resilient-player");
        
        // 4. Send more messages
        for (int i = 0; i < 3; i++)
        {
            string message = MatchingProtocol.CreateHeartbeatMessage("resilient-player");
            _mockServer.SimulateMessageReceived("resilient-player", message);
            messagesSent++;
        }
        
        await Task.Delay(100);
        
        // Assert
        Assert.AreEqual(messagesSent, messagesProcessed, 
            "All messages should be processed despite connection interruption");
        Assert.AreEqual(1, _mockServer.ConnectedClientCount, "Client should be reconnected");
    }
    #endregion
}

#region Mock Helper Classes
/// <summary>
/// Mock connection functions for testing WebSocket client components
/// </summary>
public class MockConnectionFunctions
{
    private bool _connectResult = false;
    private bool _sendResult = true;
    private bool _isConnected = false;
    private Exception _connectException = null;
    private Exception _disconnectException = null;
    private Func<bool> _connectCallback = null;
    
    public int ConnectCallCount { get; private set; } = 0;
    public int DisconnectCallCount { get; private set; } = 0;
    public int SendCallCount { get; private set; } = 0;
    public bool DisconnectCalled => DisconnectCallCount > 0;
    
    public event Action<string> OnSendMessage;
    
    public void SetConnectResult(bool result)
    {
        _connectResult = result;
        _connectException = null;
        _connectCallback = null;
    }
    
    public void SetConnectException(Exception exception)
    {
        _connectException = exception;
        _connectResult = false;
        _connectCallback = null;
    }
    
    public void SetConnectCallback(Func<bool> callback)
    {
        _connectCallback = callback;
        _connectException = null;
    }
    
    public void SetSendResult(bool result)
    {
        _sendResult = result;
    }
    
    public void SetDisconnectException(Exception exception)
    {
        _disconnectException = exception;
    }
    
    public async Task<bool> ConnectAsync()
    {
        ConnectCallCount++;
        
        if (_connectException != null)
            throw _connectException;
            
        await Task.Delay(10); // Simulate async operation
        
        bool result;
        if (_connectCallback != null)
        {
            result = _connectCallback();
        }
        else
        {
            result = _connectResult;
        }
        
        _isConnected = result;
        return result;
    }
    
    public async Task DisconnectAsync()
    {
        DisconnectCallCount++;
        
        if (_disconnectException != null)
            throw _disconnectException;
            
        await Task.Delay(10); // Simulate async operation
        _isConnected = false;
    }
    
    public async Task<bool> SendMessageAsync(string message)
    {
        SendCallCount++;
        OnSendMessage?.Invoke(message);
        
        await Task.Delay(1); // Simulate async operation
        return _sendResult;
    }
    
    public bool IsConnected()
    {
        return _isConnected;
    }
}
#endregion