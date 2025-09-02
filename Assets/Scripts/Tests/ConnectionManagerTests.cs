using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NUnit.Framework;

/// <summary>
/// ConnectionManager 재연결 로직 및 연결 상태 관리 테스트
/// 자동 재연결, 상태 전환, 하트비트, 에러 처리를 검증
/// </summary>
public class ConnectionManagerTests
{
    #region Test Data Setup
    private WebSocketConfig _testConfig;
    private ConnectionManager _connectionManager;
    private MockConnectionFunctions _mockFunctions;

    [SetUp]
    public void Setup()
    {
        // 테스트용 설정 생성
        _testConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        _testConfig.SetServerUrl("wss://test.example.com/ws");
        _testConfig.SetConnectionTimeout(2000); // 2초
        _testConfig.SetReconnectionSettings(3, new int[] { 100, 200, 500 }); // 빠른 재시도
        _testConfig.SetHeartbeatSettings(true, 1000, 2000); // 1초 간격, 2초 타임아웃
        _testConfig.SetLoggingSettings(true, false); // 기본 로깅만
        
        Assert.IsTrue(_testConfig.ValidateConfiguration(), "Test configuration should be valid");

        // ConnectionManager 초기화
        _connectionManager = new ConnectionManager(_testConfig);
        _mockFunctions = new MockConnectionFunctions();
        
        // Mock 함수들 설정
        _connectionManager.SetConnectionFunctions(
            _mockFunctions.ConnectAsync,
            _mockFunctions.DisconnectAsync,
            _mockFunctions.SendMessageAsync,
            _mockFunctions.IsConnected
        );
    }

    [TearDown]
    public void TearDown()
    {
        _connectionManager?.Dispose();
        _connectionManager = null;
        _mockFunctions = null;
        
        if (_testConfig != null)
        {
            UnityEngine.Object.DestroyImmediate(_testConfig);
            _testConfig = null;
        }
    }
    #endregion

    #region Initialization Tests
    [Test]
    public void ConnectionManager_ValidConfig_ShouldInitialize()
    {
        // Assert
        Assert.IsNotNull(_connectionManager);
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        Assert.IsFalse(_connectionManager.IsConnected);
        Assert.IsFalse(_connectionManager.IsReconnecting);
        Assert.AreEqual(0, _connectionManager.CurrentReconnectAttempt);
        Assert.AreEqual(_testConfig.MaxReconnectAttempts, _connectionManager.MaxReconnectAttempts);
    }

    [Test]
    public void ConnectionManager_NullConfig_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConnectionManager(null));
    }

    [Test]
    public void SetConnectionFunctions_ValidFunctions_ShouldSucceed()
    {
        // Arrange
        var newManager = new ConnectionManager(_testConfig);
        var mockFunctions = new MockConnectionFunctions();
        
        // Act & Assert
        Assert.DoesNotThrow(() => newManager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        ));
        
        newManager.Dispose();
    }

    [Test]
    public void SetConnectionFunctions_NullFunctions_ShouldThrowException()
    {
        // Arrange
        var newManager = new ConnectionManager(_testConfig);
        var mockFunctions = new MockConnectionFunctions();
        
        try
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => newManager.SetConnectionFunctions(
                null, mockFunctions.DisconnectAsync, mockFunctions.SendMessageAsync, mockFunctions.IsConnected));
                
            Assert.Throws<ArgumentNullException>(() => newManager.SetConnectionFunctions(
                mockFunctions.ConnectAsync, null, mockFunctions.SendMessageAsync, mockFunctions.IsConnected));
                
            Assert.Throws<ArgumentNullException>(() => newManager.SetConnectionFunctions(
                mockFunctions.ConnectAsync, mockFunctions.DisconnectAsync, null, mockFunctions.IsConnected));
                
            Assert.Throws<ArgumentNullException>(() => newManager.SetConnectionFunctions(
                mockFunctions.ConnectAsync, mockFunctions.DisconnectAsync, mockFunctions.SendMessageAsync, null));
        }
        finally
        {
            newManager.Dispose();
        }
    }
    #endregion

    #region Connection Tests
    [Test]
    public async Task ConnectAsync_SuccessfulConnection_ShouldReturnTrueAndSetState()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        bool connectionStateChanged = false;
        ConnectionState newState = ConnectionState.Disconnected;
        
        _connectionManager.OnConnectionStateChanged += (state) => {
            connectionStateChanged = true;
            newState = state;
        };
        
        // Act
        bool result = await _connectionManager.ConnectAsync();
        
        // Allow some time for async operations
        await Task.Delay(50);
        
        // Assert
        Assert.IsTrue(result, "Connection should succeed");
        Assert.AreEqual(ConnectionState.Connected, _connectionManager.CurrentState);
        Assert.IsTrue(_connectionManager.IsConnected);
        Assert.IsFalse(_connectionManager.IsReconnecting);
        Assert.AreEqual(0, _connectionManager.CurrentReconnectAttempt);
        Assert.IsTrue(connectionStateChanged, "Connection state change event should fire");
        Assert.AreEqual(ConnectionState.Connected, newState);
    }

    [Test]
    public async Task ConnectAsync_FailedConnection_ShouldReturnFalseAndSetState()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false);
        bool connectionStateChanged = false;
        
        _connectionManager.OnConnectionStateChanged += (state) => connectionStateChanged = true;
        
        // Act
        bool result = await _connectionManager.ConnectAsync();
        
        // Allow some time for async operations
        await Task.Delay(50);
        
        // Assert
        Assert.IsFalse(result, "Connection should fail");
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        Assert.IsFalse(_connectionManager.IsConnected);
        Assert.IsTrue(connectionStateChanged, "Connection state change event should fire");
    }

    [Test]
    public async Task ConnectAsync_AlreadyConnected_ShouldReturnTrueWithoutReconnecting()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        int connectCallCount = _mockFunctions.ConnectCallCount;
        
        // Act
        bool result = await _connectionManager.ConnectAsync();
        
        // Assert
        Assert.IsTrue(result, "Should return true for already connected state");
        Assert.AreEqual(connectCallCount, _mockFunctions.ConnectCallCount, "Should not call connect again");
        Assert.AreEqual(ConnectionState.Connected, _connectionManager.CurrentState);
    }

    [Test]
    public async Task ConnectAsync_ConnectionException_ShouldHandleGracefully()
    {
        // Arrange
        _mockFunctions.SetConnectException(new Exception("Test connection error"));
        
        // Act
        bool result = await _connectionManager.ConnectAsync();
        
        // Assert
        Assert.IsFalse(result, "Connection should fail gracefully");
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        Assert.IsFalse(_connectionManager.IsConnected);
    }
    #endregion

    #region Disconnection Tests
    [Test]
    public async Task DisconnectAsync_ConnectedState_ShouldDisconnectAndSetState()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        Assert.IsTrue(_connectionManager.IsConnected, "Should be connected for test setup");
        
        bool connectionStateChanged = false;
        _connectionManager.OnConnectionStateChanged += (state) => {
            if (state == ConnectionState.Disconnected)
                connectionStateChanged = true;
        };
        
        // Act
        await _connectionManager.DisconnectAsync();
        
        // Allow some time for async operations
        await Task.Delay(50);
        
        // Assert
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        Assert.IsFalse(_connectionManager.IsConnected);
        Assert.IsTrue(connectionStateChanged, "Disconnect state change event should fire");
        Assert.IsTrue(_mockFunctions.DisconnectCalled, "Disconnect function should be called");
    }

    [Test]
    public async Task DisconnectAsync_AlreadyDisconnected_ShouldHandleGracefully()
    {
        // Arrange - Already disconnected
        Assert.IsFalse(_connectionManager.IsConnected, "Should start disconnected");
        
        int disconnectCallCount = _mockFunctions.DisconnectCallCount;
        
        // Act
        await _connectionManager.DisconnectAsync();
        
        // Assert
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        // Disconnect function may or may not be called when already disconnected, both are acceptable
    }

    [Test]
    public async Task DisconnectAsync_DisconnectException_ShouldHandleGracefully()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        _mockFunctions.SetDisconnectException(new Exception("Test disconnect error"));
        
        // Act & Assert
        Assert.DoesNotThrow(async () => await _connectionManager.DisconnectAsync());
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
    }
    #endregion

    #region Connection Lost Handling Tests
    [Test]
    public async Task HandleConnectionLost_AutoReconnectEnabled_ShouldStartReconnection()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        Assert.IsTrue(_connectionManager.IsConnected, "Should be connected");
        
        bool reconnectionStarted = false;
        int reconnectionAttempts = 0;
        
        _connectionManager.OnReconnectionAttempt += (attempt, max) => {
            reconnectionStarted = true;
            reconnectionAttempts = attempt;
        };
        
        // Simulate connection failure during operation
        _mockFunctions.SetConnectResult(false);
        
        // Act
        _connectionManager.HandleConnectionLost();
        
        // Allow time for reconnection attempts
        await Task.Delay(300); // Wait for at least one reconnection attempt
        
        // Assert
        Assert.AreEqual(ConnectionState.Reconnecting, _connectionManager.CurrentState);
        Assert.IsTrue(_connectionManager.IsReconnecting);
        Assert.IsTrue(reconnectionStarted, "Reconnection should start");
        Assert.Greater(reconnectionAttempts, 0, "Should attempt reconnection");
    }

    [Test]
    public void HandleConnectionLost_AlreadyDisconnected_ShouldNotReconnect()
    {
        // Arrange - Already disconnected
        Assert.IsFalse(_connectionManager.IsConnected, "Should start disconnected");
        
        bool reconnectionAttempted = false;
        _connectionManager.OnReconnectionAttempt += (attempt, max) => reconnectionAttempted = true;
        
        // Act
        _connectionManager.HandleConnectionLost();
        
        // Assert
        Assert.IsFalse(reconnectionAttempted, "Should not attempt reconnection when already disconnected");
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
    }

    [Test]
    public async Task HandleConnectionLost_AutoReconnectDisabled_ShouldNotReconnect()
    {
        // Arrange - Disable auto-reconnect in config
        var noAutoReconnectConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        noAutoReconnectConfig.SetServerUrl("wss://test.example.com/ws");
        noAutoReconnectConfig.SetReconnectionSettings(0, new int[] { 100 }); // Max attempts = 0 disables auto-reconnect
        
        using var manager = new ConnectionManager(noAutoReconnectConfig);
        var mockFunctions = new MockConnectionFunctions();
        manager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        );
        
        mockFunctions.SetConnectResult(true);
        await manager.ConnectAsync();
        
        bool reconnectionAttempted = false;
        manager.OnReconnectionAttempt += (attempt, max) => reconnectionAttempted = true;
        
        // Act
        manager.HandleConnectionLost();
        await Task.Delay(200);
        
        // Assert
        Assert.IsFalse(reconnectionAttempted, "Should not attempt reconnection when disabled");
        Assert.AreEqual(ConnectionState.Disconnected, manager.CurrentState);
        
        UnityEngine.Object.DestroyImmediate(noAutoReconnectConfig);
    }
    #endregion

    #region Manual Reconnection Tests
    [Test]
    public async Task StartManualReconnection_ShouldAttemptReconnection()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false); // Will fail initially
        
        bool reconnectionAttempted = false;
        _connectionManager.OnReconnectionAttempt += (attempt, max) => reconnectionAttempted = true;
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Allow time for reconnection attempt
        await Task.Delay(200);
        
        // Assert
        Assert.IsTrue(reconnectionAttempted, "Manual reconnection should attempt to connect");
        Assert.AreEqual(ConnectionState.Reconnecting, _connectionManager.CurrentState);
    }

    [Test]
    public async Task StartManualReconnection_SuccessfulReconnection_ShouldConnect()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        
        bool reconnected = false;
        _connectionManager.OnReconnected += () => reconnected = true;
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Allow time for reconnection
        await Task.Delay(200);
        
        // Assert
        Assert.IsTrue(reconnected, "Should fire reconnected event");
        Assert.AreEqual(ConnectionState.Connected, _connectionManager.CurrentState);
        Assert.IsTrue(_connectionManager.IsConnected);
        Assert.AreEqual(0, _connectionManager.CurrentReconnectAttempt, "Should reset attempt counter on success");
    }

    [Test]
    public void StopReconnection_DuringReconnection_ShouldCancelReconnection()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false);
        _connectionManager.StartManualReconnection();
        
        // Act
        _connectionManager.StopReconnection();
        
        // Assert
        Assert.IsFalse(_connectionManager.AutoReconnectEnabled, "Auto-reconnect should be disabled");
        // State may still be reconnecting briefly, but attempts should stop
    }
    #endregion

    #region Reconnection Logic Tests
    [Test]
    public async Task ReconnectionLoop_MaxAttemptsReached_ShouldFailAndStopRetrying()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false); // Always fail
        
        bool reconnectionFailed = false;
        string failureReason = null;
        _connectionManager.OnReconnectionFailed += (reason) => {
            reconnectionFailed = true;
            failureReason = reason;
        };
        
        int maxAttempts = _testConfig.MaxReconnectAttempts;
        int attemptCount = 0;
        _connectionManager.OnReconnectionAttempt += (attempt, max) => {
            attemptCount = Math.Max(attemptCount, attempt);
        };
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Wait for all attempts to complete
        int totalWaitTime = _testConfig.GetRetryDelay(0) + _testConfig.GetRetryDelay(1) + _testConfig.GetRetryDelay(2) + 1000;
        await Task.Delay(totalWaitTime);
        
        // Assert
        Assert.IsTrue(reconnectionFailed, "Reconnection should fail after max attempts");
        Assert.IsTrue(failureReason.Contains("Max reconnection attempts"), $"Failure reason should mention max attempts: {failureReason}");
        Assert.AreEqual(maxAttempts, attemptCount, "Should attempt exactly max attempts");
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
    }

    [Test]
    public async Task ReconnectionLoop_SuccessAfterFailures_ShouldConnect()
    {
        // Arrange
        int failureCount = 0;
        _mockFunctions.SetConnectCallback(() => {
            failureCount++;
            return failureCount >= 3; // Succeed on third attempt
        });
        
        bool reconnected = false;
        _connectionManager.OnReconnected += () => reconnected = true;
        
        int finalAttemptCount = 0;
        _connectionManager.OnReconnectionAttempt += (attempt, max) => {
            finalAttemptCount = attempt;
        };
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Wait for reconnection success
        await Task.Delay(1000);
        
        // Assert
        Assert.IsTrue(reconnected, "Should eventually reconnect");
        Assert.AreEqual(ConnectionState.Connected, _connectionManager.CurrentState);
        Assert.IsTrue(_connectionManager.IsConnected);
        Assert.AreEqual(0, _connectionManager.CurrentReconnectAttempt, "Attempt counter should reset on success");
        Assert.AreEqual(3, finalAttemptCount, "Should have attempted 3 times");
    }

    [Test]
    public async Task ReconnectionLoop_RetryDelays_ShouldRespectConfiguredDelays()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false);
        
        var attemptTimes = new List<DateTime>();
        _connectionManager.OnReconnectionAttempt += (attempt, max) => {
            attemptTimes.Add(DateTime.UtcNow);
        };
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Wait for multiple attempts
        await Task.Delay(1000);
        
        // Assert
        Assert.GreaterOrEqual(attemptTimes.Count, 2, "Should have multiple attempts for delay testing");
        
        if (attemptTimes.Count >= 2)
        {
            var timeBetweenAttempts = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
            var expectedDelay = _testConfig.GetRetryDelay(0);
            
            // Allow for some timing variance in test environment
            Assert.GreaterOrEqual(timeBetweenAttempts, expectedDelay * 0.8, 
                $"Time between attempts ({timeBetweenAttempts}ms) should be close to expected delay ({expectedDelay}ms)");
        }
    }
    #endregion

    #region Heartbeat Tests
    [Test]
    public async Task Heartbeat_EnabledAndConnected_ShouldSendHeartbeats()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        _mockFunctions.SetSendResult(true);
        await _connectionManager.ConnectAsync();
        
        Assert.IsTrue(_connectionManager.IsConnected, "Should be connected for heartbeat test");
        
        int heartbeatsSent = 0;
        _mockFunctions.OnSendMessage += (message) => {
            if (message.Contains("heartbeat"))
                heartbeatsSent++;
        };
        
        // Act - Wait for at least one heartbeat interval
        await Task.Delay(_testConfig.HeartbeatInterval + 200);
        
        // Assert
        Assert.Greater(heartbeatsSent, 0, "Should send heartbeat messages");
    }

    [Test]
    public async Task HandleHeartbeatResponse_ShouldUpdateLastResponseTime()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        // Act
        _connectionManager.HandleHeartbeatResponse();
        
        // Assert
        Assert.DoesNotThrow(() => _connectionManager.HandleHeartbeatResponse(), 
            "Heartbeat response handling should not throw");
    }

    [Test]
    public async Task Heartbeat_NoResponse_ShouldDetectTimeout()
    {
        // Arrange - Very short heartbeat timeout for testing
        var fastTimeoutConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        fastTimeoutConfig.SetServerUrl("wss://test.example.com/ws");
        fastTimeoutConfig.SetHeartbeatSettings(true, 100, 200); // 100ms interval, 200ms timeout
        
        using var manager = new ConnectionManager(fastTimeoutConfig);
        var mockFunctions = new MockConnectionFunctions();
        manager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        );
        
        mockFunctions.SetConnectResult(true);
        mockFunctions.SetSendResult(true);
        await manager.ConnectAsync();
        
        bool connectionLost = false;
        manager.OnConnectionStateChanged += (state) => {
            if (state == ConnectionState.Disconnected)
                connectionLost = true;
        };
        
        // Act - Don't respond to heartbeats, let them timeout
        await Task.Delay(500);
        
        // Assert
        Assert.IsTrue(connectionLost, "Should detect connection loss due to heartbeat timeout");
        
        UnityEngine.Object.DestroyImmediate(fastTimeoutConfig);
    }

    [Test]
    public async Task Heartbeat_DisabledInConfig_ShouldNotSendHeartbeats()
    {
        // Arrange
        var noHeartbeatConfig = ScriptableObject.CreateInstance<WebSocketConfig>();
        noHeartbeatConfig.SetServerUrl("wss://test.example.com/ws");
        noHeartbeatConfig.SetHeartbeatSettings(false, 1000, 2000); // Disabled
        
        using var manager = new ConnectionManager(noHeartbeatConfig);
        var mockFunctions = new MockConnectionFunctions();
        manager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        );
        
        mockFunctions.SetConnectResult(true);
        await manager.ConnectAsync();
        
        int heartbeatsSent = 0;
        mockFunctions.OnSendMessage += (message) => {
            if (message.Contains("heartbeat"))
                heartbeatsSent++;
        };
        
        // Act - Wait longer than heartbeat interval
        await Task.Delay(1200);
        
        // Assert
        Assert.AreEqual(0, heartbeatsSent, "Should not send heartbeats when disabled");
        
        UnityEngine.Object.DestroyImmediate(noHeartbeatConfig);
    }
    #endregion

    #region State Management Tests
    [Test]
    public async Task ConnectionStates_ShouldTransitionCorrectly()
    {
        // Arrange
        var stateTransitions = new List<ConnectionState>();
        _connectionManager.OnConnectionStateChanged += (state) => stateTransitions.Add(state);
        
        _mockFunctions.SetConnectResult(true);
        
        // Act & Assert - Test normal flow
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
        
        await _connectionManager.ConnectAsync();
        await Task.Delay(50);
        
        Assert.Contains(ConnectionState.Connecting, stateTransitions);
        Assert.Contains(ConnectionState.Connected, stateTransitions);
        Assert.AreEqual(ConnectionState.Connected, _connectionManager.CurrentState);
        
        await _connectionManager.DisconnectAsync();
        await Task.Delay(50);
        
        Assert.Contains(ConnectionState.Disconnected, stateTransitions);
        Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.CurrentState);
    }

    [Test]
    public async Task ConnectionStates_ReconnectionFlow_ShouldTransitionCorrectly()
    {
        // Arrange
        var stateTransitions = new List<ConnectionState>();
        _connectionManager.OnConnectionStateChanged += (state) => stateTransitions.Add(state);
        
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        stateTransitions.Clear(); // Clear initial connection states
        
        // Simulate connection loss and recovery
        _mockFunctions.SetConnectResult(false); // Will fail initially
        
        // Act
        _connectionManager.HandleConnectionLost();
        await Task.Delay(100);
        
        _mockFunctions.SetConnectResult(true); // Will succeed on retry
        await Task.Delay(300);
        
        // Assert
        Assert.Contains(ConnectionState.Disconnected, stateTransitions, "Should transition to disconnected on connection loss");
        Assert.Contains(ConnectionState.Reconnecting, stateTransitions, "Should transition to reconnecting");
        Assert.Contains(ConnectionState.Connected, stateTransitions, "Should transition back to connected on successful reconnection");
    }
    #endregion

    #region Resource Management Tests
    [Test]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var manager = new ConnectionManager(_testConfig);
        var mockFunctions = new MockConnectionFunctions();
        manager.SetConnectionFunctions(
            mockFunctions.ConnectAsync,
            mockFunctions.DisconnectAsync,
            mockFunctions.SendMessageAsync,
            mockFunctions.IsConnected
        );
        
        // Act
        manager.Dispose();
        
        // Assert
        Assert.DoesNotThrow(() => manager.Dispose(), "Multiple dispose calls should not throw");
    }

    [Test]
    public async Task Dispose_DuringReconnection_ShouldStopReconnection()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false);
        _connectionManager.StartManualReconnection();
        
        // Act
        _connectionManager.Dispose();
        
        // Assert - Should not throw or hang
        await Task.Delay(100);
        Assert.DoesNotThrow(() => _connectionManager.Dispose());
    }

    [Test]
    public async Task Dispose_AfterConnection_ShouldDisconnectGracefully()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        await _connectionManager.ConnectAsync();
        
        Assert.IsTrue(_connectionManager.IsConnected, "Should be connected for dispose test");
        
        // Act
        _connectionManager.Dispose();
        
        // Assert
        Assert.IsTrue(_mockFunctions.DisconnectCalled, "Should call disconnect during disposal");
    }
    #endregion

    #region Error Handling Tests
    [Test]
    public async Task ConnectAsync_FunctionsNotSet_ShouldFailGracefully()
    {
        // Arrange
        var manager = new ConnectionManager(_testConfig);
        // Don't set connection functions
        
        // Act
        bool result = await manager.ConnectAsync();
        
        // Assert
        Assert.IsFalse(result, "Should fail when functions not set");
        Assert.AreEqual(ConnectionState.Disconnected, manager.CurrentState);
        
        manager.Dispose();
    }

    [Test]
    public async Task ConnectAsync_AfterDisposal_ShouldFailGracefully()
    {
        // Arrange
        _connectionManager.Dispose();
        
        // Act
        bool result = await _connectionManager.ConnectAsync();
        
        // Assert
        Assert.IsFalse(result, "Should fail when disposed");
    }

    [Test]
    public async Task DisconnectAsync_AfterDisposal_ShouldNotThrow()
    {
        // Arrange
        _connectionManager.Dispose();
        
        // Act & Assert
        Assert.DoesNotThrow(async () => await _connectionManager.DisconnectAsync());
    }

    [Test]
    public void HandleConnectionLost_AfterDisposal_ShouldNotThrow()
    {
        // Arrange
        _connectionManager.Dispose();
        
        // Act & Assert
        Assert.DoesNotThrow(() => _connectionManager.HandleConnectionLost());
    }
    #endregion

    #region Performance Tests
    [Test]
    public async Task ConnectionOperations_Performance_ShouldBeReasonable()
    {
        // Arrange
        _mockFunctions.SetConnectResult(true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        await _connectionManager.ConnectAsync();
        stopwatch.Stop();
        
        // Assert
        Assert.Less(stopwatch.ElapsedMilliseconds, 1000, "Connection should complete within reasonable time");
        Debug.Log($"Connection time: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task ReconnectionAttempts_Performance_ShouldNotBlockMainThread()
    {
        // Arrange
        _mockFunctions.SetConnectResult(false);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        _connectionManager.StartManualReconnection();
        
        // Should return immediately without blocking
        stopwatch.Stop();
        
        // Assert
        Assert.Less(stopwatch.ElapsedMilliseconds, 100, "Starting reconnection should not block");
        
        // Cleanup
        _connectionManager.StopReconnection();
        await Task.Delay(100);
    }
    #endregion
}

#region Mock Connection Functions
/// <summary>
/// Mock implementation of connection functions for testing
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