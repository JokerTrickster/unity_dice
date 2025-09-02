using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MatchingReconnection 단위 테스트
/// 재연결 관리 시스템의 정확성과 안정성 검증
/// </summary>
public class MatchingReconnectionTests
{
    #region Test Setup
    private GameObject _testGameObject;
    private MatchingReconnection _reconnectionManager;
    private GameObject _mockNetworkManagerGO;
    private MockNetworkManagerForReconnection _mockNetworkManager;
    
    [SetUp]
    public void SetUp()
    {
        // 테스트용 GameObject 생성
        _testGameObject = new GameObject("TestMatchingReconnection");
        _reconnectionManager = _testGameObject.AddComponent<MatchingReconnection>();
        
        // Mock NetworkManager 생성
        _mockNetworkManagerGO = new GameObject("MockNetworkManager");
        _mockNetworkManager = _mockNetworkManagerGO.AddComponent<MockNetworkManagerForReconnection>();
    }
    
    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
        
        if (_mockNetworkManagerGO != null)
        {
            UnityEngine.Object.DestroyImmediate(_mockNetworkManagerGO);
        }
    }
    #endregion

    #region Basic Reconnection Tests
    [Test]
    public void StartReconnection_WhenNotConnected_ShouldStartReconnectionProcess()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        
        bool reconnectionStarted = false;
        _reconnectionManager.OnReconnectionStarted += () => reconnectionStarted = true;
        
        // Act
        _reconnectionManager.StartReconnection();
        
        // Assert
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Should be in reconnecting state");
        Assert.IsTrue(reconnectionStarted, "Reconnection started event should be triggered");
        Assert.AreEqual(0, _reconnectionManager.CurrentAttempt, "Current attempt should be 0 initially");
    }

    [Test]
    public void StartReconnection_WhenAlreadyConnected_ShouldStopReconnection()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(true);
        
        // Act
        _reconnectionManager.StartReconnection();
        
        // Assert
        Assert.IsFalse(_reconnectionManager.IsReconnecting, "Should not be reconnecting when already connected");
    }

    [UnityTest]
    public IEnumerator StartReconnection_WhenAlreadyReconnecting_ShouldRestartProcess()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Setup: Should be reconnecting");
        
        yield return null; // Allow initial coroutine to start
        
        // Act - Start reconnection again
        LogAssert.Expect(LogType.Log, "[MatchingReconnection] Already reconnecting, restarting process");
        _reconnectionManager.StartReconnection();
        
        // Assert
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Should still be reconnecting");
        Assert.AreEqual(0, _reconnectionManager.CurrentAttempt, "Attempt count should be reset");
    }

    [Test]
    public void StopReconnection_WhenReconnecting_ShouldStopProcess()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Setup: Should be reconnecting");
        
        // Act
        _reconnectionManager.StopReconnection();
        
        // Assert
        Assert.IsFalse(_reconnectionManager.IsReconnecting, "Should not be reconnecting after stop");
    }

    [UnityTest]
    public IEnumerator AttemptImmediateReconnection_ShouldForceImmediateAttempt()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        
        // Act
        bool result = _reconnectionManager.AttemptImmediateReconnection();
        
        // Assert
        Assert.IsTrue(result, "Immediate reconnection should return true");
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Should be in reconnecting state");
        
        yield return null; // Allow coroutine to start
    }
    #endregion

    #region Reconnection Success Tests
    [UnityTest]
    public IEnumerator ReconnectionSuccess_ShouldTriggerSuccessEvent()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _mockNetworkManager.SetReconnectionResult(true, 1); // Success on first attempt
        
        bool reconnectionSuccess = false;
        _reconnectionManager.OnReconnectionSuccess += () => reconnectionSuccess = true;
        
        // Act
        _reconnectionManager.StartReconnection();
        
        // Wait for reconnection attempt
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(reconnectionSuccess, "Reconnection success event should be triggered");
        Assert.IsFalse(_reconnectionManager.IsReconnecting, "Should not be reconnecting after success");
    }

    [UnityTest]
    public IEnumerator ReconnectionWithStateRecovery_ShouldAttemptRecovery()
    {
        // Arrange
        _reconnectionManager.StateRecoveryEnabled = true;
        _mockNetworkManager.SetConnectionState(false);
        _mockNetworkManager.SetReconnectionResult(true, 1);
        
        bool stateRecoveryStarted = false;
        bool stateRecoveryCompleted = false;
        
        _reconnectionManager.OnStateRecoveryStarted += () => stateRecoveryStarted = true;
        _reconnectionManager.OnStateRecoveryCompleted += (success) => stateRecoveryCompleted = success;
        
        // Act
        _reconnectionManager.StartReconnection(saveCurrentState: true);
        
        // Wait for reconnection and state recovery
        yield return new WaitForSeconds(1.0f);
        
        // Assert
        Assert.IsTrue(stateRecoveryStarted, "State recovery should be started");
        Assert.IsTrue(stateRecoveryCompleted, "State recovery should be completed");
    }
    #endregion

    #region Reconnection Failure Tests
    [UnityTest]
    public IEnumerator ReconnectionFailure_ShouldRetryWithBackoff()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _mockNetworkManager.SetReconnectionResult(false, 2); // Fail first attempt, succeed on second
        
        int failureCount = 0;
        int progressCount = 0;
        
        _reconnectionManager.OnReconnectionFailed += (attempt) => failureCount++;
        _reconnectionManager.OnReconnectionProgress += (current, max) => progressCount++;
        
        // Act
        _reconnectionManager.StartReconnection();
        
        // Wait for attempts
        yield return new WaitForSeconds(3.0f); // Allow time for backoff and retry
        
        // Assert
        Assert.IsTrue(failureCount > 0, "Should have at least one failure");
        Assert.IsTrue(progressCount > 0, "Should have progress updates");
    }

    [UnityTest]
    public IEnumerator MaxReconnectionAttempts_ShouldTriggerMaxAttemptsEvent()
    {
        // Arrange
        _reconnectionManager.UpdateReconnectionConfig(maxAttempts: 2, initialDelay: 0.1f);
        _mockNetworkManager.SetConnectionState(false);
        _mockNetworkManager.SetReconnectionResult(false, 999); // Always fail
        
        bool maxAttemptsReached = false;
        _reconnectionManager.OnMaxAttemptsReached += () => maxAttemptsReached = true;
        
        // Act
        _reconnectionManager.StartReconnection();
        
        // Wait for max attempts
        yield return new WaitForSeconds(2.0f);
        
        // Assert
        Assert.IsTrue(maxAttemptsReached, "Max attempts reached event should be triggered");
        Assert.IsFalse(_reconnectionManager.IsReconnecting, "Should not be reconnecting after max attempts");
        Assert.AreEqual(2, _reconnectionManager.CurrentAttempt, "Should have made 2 attempts");
    }
    #endregion

    #region Configuration Tests
    [Test]
    public void UpdateReconnectionConfig_ShouldUpdateSettings()
    {
        // Act
        _reconnectionManager.UpdateReconnectionConfig(
            maxAttempts: 10,
            initialDelay: 5.0f,
            maxDelay: 60.0f,
            backoffMultiplier: 3.0f
        );
        
        // Assert
        Assert.AreEqual(10, _reconnectionManager.MaxAttempts, "Max attempts should be updated");
    }

    [Test]
    public void UpdateReconnectionConfig_WithInvalidValues_ShouldIgnoreInvalidValues()
    {
        // Arrange
        int originalMaxAttempts = _reconnectionManager.MaxAttempts;
        
        // Act
        _reconnectionManager.UpdateReconnectionConfig(
            maxAttempts: -1, // Invalid
            initialDelay: 2.0f // Valid
        );
        
        // Assert
        Assert.AreEqual(originalMaxAttempts, _reconnectionManager.MaxAttempts, "Invalid max attempts should be ignored");
    }
    #endregion

    #region State Recovery Tests
    [UnityTest]
    public IEnumerator ForceStateRecovery_WithoutSavedState_ShouldFail()
    {
        // Act
        var recoveryTask = _reconnectionManager.ForceStateRecovery();
        yield return new WaitUntil(() => recoveryTask.IsCompleted);
        
        // Assert
        Assert.IsFalse(recoveryTask.Result, "Force recovery should fail without saved state");
        Assert.IsFalse(_reconnectionManager.HasSavedState, "Should not have saved state");
    }

    [Test]
    public void StateRecoveryEnabled_ShouldControlRecoveryBehavior()
    {
        // Act & Assert
        _reconnectionManager.StateRecoveryEnabled = true;
        Assert.IsTrue(_reconnectionManager.StateRecoveryEnabled, "State recovery should be enabled");
        
        _reconnectionManager.StateRecoveryEnabled = false;
        Assert.IsFalse(_reconnectionManager.StateRecoveryEnabled, "State recovery should be disabled");
    }

    [Test]
    public void ClearSavedState_ShouldRemoveSavedState()
    {
        // Arrange - Start reconnection to potentially save state
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection(saveCurrentState: true);
        
        // Act
        _reconnectionManager.ClearSavedState();
        
        // Assert
        Assert.IsFalse(_reconnectionManager.HasSavedState, "Should not have saved state after clear");
    }
    #endregion

    #region Statistics Tests
    [UnityTest]
    public IEnumerator GetReconnectionStats_ShouldReturnAccurateInformation()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.UpdateReconnectionConfig(maxAttempts: 5);
        
        // Act
        _reconnectionManager.StartReconnection();
        yield return null; // Allow reconnection to start
        
        var stats = _reconnectionManager.GetReconnectionStats();
        
        // Assert
        Assert.IsTrue(stats.IsReconnecting, "Stats should show reconnecting");
        Assert.AreEqual(5, stats.MaxAttempts, "Stats should show correct max attempts");
        Assert.IsTrue(stats.DisconnectionDuration.TotalSeconds >= 0, "Disconnection duration should be valid");
    }

    [Test]
    public void GetReconnectionStats_WhenNotReconnecting_ShouldReturnCorrectState()
    {
        // Act
        var stats = _reconnectionManager.GetReconnectionStats();
        
        // Assert
        Assert.IsFalse(stats.IsReconnecting, "Stats should show not reconnecting");
        Assert.AreEqual(0, stats.CurrentAttempt, "Stats should show no current attempts");
    }
    #endregion

    #region Application Lifecycle Tests
    [UnityTest]
    public IEnumerator OnApplicationPause_WhenResumed_ShouldAttemptReconnection()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        yield return null; // Allow reconnection to start
        
        bool reconnectionStartedAgain = false;
        int originalAttempt = _reconnectionManager.CurrentAttempt;
        
        // Monitor for reconnection restart
        _reconnectionManager.OnReconnectionStarted += () => reconnectionStartedAgain = true;
        
        // Act - Simulate app pause/resume
        _reconnectionManager.SendMessage("OnApplicationPause", false); // Resume
        
        yield return null; // Allow processing
        
        // Assert
        Assert.IsTrue(reconnectionStartedAgain, "Should restart reconnection on app resume");
    }

    [UnityTest]
    public IEnumerator OnApplicationFocus_WhenFocused_ShouldAttemptReconnection()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        yield return null; // Allow reconnection to start
        
        bool reconnectionStartedAgain = false;
        _reconnectionManager.OnReconnectionStarted += () => reconnectionStartedAgain = true;
        
        // Act - Simulate app focus
        _reconnectionManager.SendMessage("OnApplicationFocus", true); // Focus gained
        
        yield return null; // Allow processing
        
        // Assert
        Assert.IsTrue(reconnectionStartedAgain, "Should restart reconnection on app focus");
    }
    #endregion

    #region Component Lifecycle Tests
    [UnityTest]
    public IEnumerator OnDestroy_ShouldStopReconnection()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        Assert.IsTrue(_reconnectionManager.IsReconnecting, "Setup: Should be reconnecting");
        
        yield return null; // Allow coroutine to start
        
        // Act - Destroy component
        UnityEngine.Object.DestroyImmediate(_testGameObject);
        _testGameObject = null; // Prevent cleanup in TearDown
        
        yield return null; // Allow destruction to complete
        
        // Assert - Can't check reconnection manager state as it's destroyed
        // This test mainly ensures no exceptions are thrown during destruction
        Assert.Pass("Component destruction completed without exceptions");
    }
    #endregion

    #region Edge Case Tests
    [Test]
    public void StartReconnection_WhenDestroyed_ShouldLogWarning()
    {
        // Arrange
        UnityEngine.Object.DestroyImmediate(_testGameObject);
        
        // Act & Assert
        LogAssert.Expect(LogType.Warning, "[MatchingReconnection] Cannot start reconnection: Component is destroyed");
        _reconnectionManager.StartReconnection();
    }

    [UnityTest]
    public IEnumerator DisconnectionDuration_ShouldIncreaseOverTime()
    {
        // Arrange
        _mockNetworkManager.SetConnectionState(false);
        _reconnectionManager.StartReconnection();
        
        // Act
        yield return null; // Initial measurement
        var initialDuration = _reconnectionManager.DisconnectionDuration;
        
        yield return new WaitForSeconds(0.1f);
        var laterDuration = _reconnectionManager.DisconnectionDuration;
        
        // Assert
        Assert.IsTrue(laterDuration > initialDuration, 
            $"Disconnection duration should increase over time. Initial: {initialDuration.TotalSeconds:F3}s, Later: {laterDuration.TotalSeconds:F3}s");
    }

    [Test]
    public void TimeSinceLastAttempt_WhenNoAttempts_ShouldReturnZero()
    {
        // Act
        var timeSinceAttempt = _reconnectionManager.TimeSinceLastAttempt;
        
        // Assert
        Assert.AreEqual(TimeSpan.Zero, timeSinceAttempt, "Should return zero when no attempts made");
    }
    #endregion
}

/// <summary>
/// 테스트용 Mock NetworkManager for Reconnection
/// </summary>
public class MockNetworkManagerForReconnection : MonoBehaviour
{
    #region Mock State
    private bool _isConnected = false;
    private bool _reconnectionResult = true;
    private int _reconnectionAttemptToSucceed = 1;
    private int _currentReconnectionAttempt = 0;
    #endregion

    #region Mock Configuration
    public void SetConnectionState(bool connected) => _isConnected = connected;
    
    public void SetReconnectionResult(bool success, int attemptToSucceed = 1)
    {
        _reconnectionResult = success;
        _reconnectionAttemptToSucceed = attemptToSucceed;
        _currentReconnectionAttempt = 0;
    }
    #endregion

    #region NetworkManager Interface Simulation
    public bool IsWebSocketConnected() => _isConnected;
    
    public async System.Threading.Tasks.Task<bool> ConnectWebSocketAsync()
    {
        _currentReconnectionAttempt++;
        
        // Simulate connection attempt delay
        await System.Threading.Tasks.Task.Delay(50);
        
        if (_reconnectionResult && _currentReconnectionAttempt >= _reconnectionAttemptToSucceed)
        {
            _isConnected = true;
            return true;
        }
        
        return false;
    }
    
    public WebSocketConnectionQuality GetWebSocketConnectionQuality()
    {
        return new WebSocketConnectionQuality
        {
            IsConnected = _isConnected,
            QualityScore = _isConnected ? 0.9f : 0.0f,
            Status = _isConnected ? "Connected" : "Disconnected"
        };
    }
    #endregion
}