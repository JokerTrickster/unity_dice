using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MatchingTimeout 단위 테스트
/// 타임아웃 관리 시스템의 정확성과 안정성 검증
/// </summary>
public class MatchingTimeoutTests
{
    #region Test Setup
    private GameObject _testGameObject;
    private MatchingTimeout _timeoutManager;
    
    [SetUp]
    public void SetUp()
    {
        _testGameObject = new GameObject("TestMatchingTimeout");
        _timeoutManager = _testGameObject.AddComponent<MatchingTimeout>();
    }
    
    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
    }
    #endregion

    #region Basic Timeout Tests
    [Test]
    public void StartRequestTimeout_WithValidParameters_ShouldSucceed()
    {
        // Act
        bool result = _timeoutManager.StartRequestTimeout("request-1", "player-1", 5.0f);
        
        // Assert
        Assert.IsTrue(result, "Starting timeout should succeed");
        Assert.AreEqual(1, _timeoutManager.ActiveTimeoutCount, "Should have one active timeout");
    }

    [Test]
    public void StartRequestTimeout_WithInvalidParameters_ShouldFail()
    {
        // Act & Assert - Null request ID
        LogAssert.Expect(LogType.Error, "[MatchingTimeout] RequestId and PlayerId cannot be null or empty");
        bool result1 = _timeoutManager.StartRequestTimeout("", "player-1", 5.0f);
        Assert.IsFalse(result1, "Should fail with empty request ID");
        
        // Act & Assert - Null player ID
        LogAssert.Expect(LogType.Error, "[MatchingTimeout] RequestId and PlayerId cannot be null or empty");
        bool result2 = _timeoutManager.StartRequestTimeout("request-1", "", 5.0f);
        Assert.IsFalse(result2, "Should fail with empty player ID");
        
        // Act & Assert - Invalid duration
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Invalid timeout duration"));
        bool result3 = _timeoutManager.StartRequestTimeout("request-1", "player-1", -1.0f);
        Assert.IsFalse(result3, "Should fail with negative duration");
    }

    [UnityTest]
    public IEnumerator StartRequestTimeout_WithShortDuration_ShouldTimeoutCorrectly()
    {
        // Arrange
        bool timeoutTriggered = false;
        string timeoutRequestId = "";
        string timeoutPlayerId = "";
        
        _timeoutManager.OnRequestTimeout += (requestId, playerId) => {
            timeoutTriggered = true;
            timeoutRequestId = requestId;
            timeoutPlayerId = playerId;
        };
        
        // Act
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 0.1f); // 100ms timeout
        Assert.IsTrue(started, "Timeout should start successfully");
        
        // Wait for timeout
        yield return new WaitForSeconds(0.2f);
        
        // Assert
        Assert.IsTrue(timeoutTriggered, "Timeout event should be triggered");
        Assert.AreEqual("request-1", timeoutRequestId, "Timeout request ID should match");
        Assert.AreEqual("player-1", timeoutPlayerId, "Timeout player ID should match");
        Assert.AreEqual(0, _timeoutManager.ActiveTimeoutCount, "Should have no active timeouts after timeout");
    }

    [UnityTest]
    public IEnumerator StartRequestTimeout_WithWarningTime_ShouldTriggerWarning()
    {
        // Arrange
        bool warningTriggered = false;
        string warningRequestId = "";
        int warningRemainingTime = 0;
        
        _timeoutManager.OnTimeoutWarning += (requestId, remainingSeconds) => {
            warningTriggered = true;
            warningRequestId = requestId;
            warningRemainingTime = remainingSeconds;
        };
        
        // Act - Start timeout with 12 seconds (warning at 10 seconds)
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 0.12f); // 120ms timeout
        Assert.IsTrue(started, "Timeout should start successfully");
        
        // Wait for warning (should trigger at ~20ms remaining)
        yield return new WaitForSeconds(0.11f); // Wait 110ms, leaving ~10ms
        
        // Assert
        Assert.IsTrue(warningTriggered, "Warning event should be triggered");
        Assert.AreEqual("request-1", warningRequestId, "Warning request ID should match");
        Assert.IsTrue(warningRemainingTime >= 0, "Remaining time should be valid");
    }
    #endregion

    #region Cancel Timeout Tests
    [Test]
    public void CancelTimeout_WithExistingRequest_ShouldSucceed()
    {
        // Arrange
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        Assert.IsTrue(started, "Setup: Timeout should start");
        Assert.AreEqual(1, _timeoutManager.ActiveTimeoutCount, "Setup: Should have one timeout");
        
        // Act
        bool cancelled = _timeoutManager.CancelTimeout("request-1");
        
        // Assert
        Assert.IsTrue(cancelled, "Cancel should succeed");
        Assert.AreEqual(0, _timeoutManager.ActiveTimeoutCount, "Should have no active timeouts after cancel");
    }

    [Test]
    public void CancelTimeout_WithNonExistentRequest_ShouldFail()
    {
        // Act
        bool cancelled = _timeoutManager.CancelTimeout("non-existent");
        
        // Assert
        Assert.IsFalse(cancelled, "Cancel should fail for non-existent request");
    }

    [Test]
    public void CancelTimeout_WithNullRequestId_ShouldLogError()
    {
        // Act & Assert
        LogAssert.Expect(LogType.Error, "[MatchingTimeout] RequestId cannot be null or empty");
        bool result = _timeoutManager.CancelTimeout("");
        Assert.IsFalse(result, "Should fail with empty request ID");
    }

    [UnityTest]
    public IEnumerator CancelTimeout_ShouldTriggerCancelEvent()
    {
        // Arrange
        bool cancelTriggered = false;
        string cancelRequestId = "";
        
        _timeoutManager.OnTimeoutCancelled += (requestId) => {
            cancelTriggered = true;
            cancelRequestId = requestId;
        };
        
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        Assert.IsTrue(started, "Setup: Timeout should start");
        
        yield return null; // Wait for coroutine to start
        
        // Act
        bool cancelled = _timeoutManager.CancelTimeout("request-1");
        
        // Assert
        Assert.IsTrue(cancelled, "Cancel should succeed");
        Assert.IsTrue(cancelTriggered, "Cancel event should be triggered");
        Assert.AreEqual("request-1", cancelRequestId, "Cancel request ID should match");
    }
    #endregion

    #region Multiple Timeout Tests
    [Test]
    public void StartRequestTimeout_MultipleRequests_ShouldTrackAll()
    {
        // Act
        bool result1 = _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        bool result2 = _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        bool result3 = _timeoutManager.StartRequestTimeout("request-3", "player-1", 10.0f); // Same player
        
        // Assert
        Assert.IsTrue(result1 && result2 && result3, "All requests should start successfully");
        Assert.AreEqual(3, _timeoutManager.ActiveTimeoutCount, "Should have three active timeouts");
        
        var waitingPlayers = _timeoutManager.WaitingPlayers;
        Assert.AreEqual(2, waitingPlayers.Count, "Should have two unique waiting players");
        Assert.Contains("player-1", waitingPlayers);
        Assert.Contains("player-2", waitingPlayers);
    }

    [Test]
    public void CancelPlayerTimeouts_ShouldCancelAllPlayerRequests()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        _timeoutManager.StartRequestTimeout("request-3", "player-1", 10.0f); // Same player as request-1
        
        Assert.AreEqual(3, _timeoutManager.ActiveTimeoutCount, "Setup: Should have three timeouts");
        
        // Act
        int cancelledCount = _timeoutManager.CancelPlayerTimeouts("player-1");
        
        // Assert
        Assert.AreEqual(2, cancelledCount, "Should cancel two requests for player-1");
        Assert.AreEqual(1, _timeoutManager.ActiveTimeoutCount, "Should have one timeout remaining");
    }

    [Test]
    public void CancelAllTimeouts_ShouldCancelAllRequests()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        _timeoutManager.StartRequestTimeout("request-3", "player-3", 10.0f);
        
        Assert.AreEqual(3, _timeoutManager.ActiveTimeoutCount, "Setup: Should have three timeouts");
        
        // Act
        int cancelledCount = _timeoutManager.CancelAllTimeouts();
        
        // Assert
        Assert.AreEqual(3, cancelledCount, "Should cancel all three requests");
        Assert.AreEqual(0, _timeoutManager.ActiveTimeoutCount, "Should have no active timeouts");
    }
    #endregion

    #region Duplicate Request Tests
    [UnityTest]
    public IEnumerator StartRequestTimeout_WithDuplicateRequestId_ShouldReplaceExisting()
    {
        // Arrange
        bool firstStarted = _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        Assert.IsTrue(firstStarted, "Setup: First timeout should start");
        Assert.AreEqual(1, _timeoutManager.ActiveTimeoutCount, "Setup: Should have one timeout");
        
        yield return null; // Allow coroutine to start
        
        // Act & Assert
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already has timeout, replacing"));
        bool secondStarted = _timeoutManager.StartRequestTimeout("request-1", "player-2", 5.0f); // Different player, same request ID
        
        Assert.IsTrue(secondStarted, "Second timeout should start successfully");
        Assert.AreEqual(1, _timeoutManager.ActiveTimeoutCount, "Should still have one timeout (replaced)");
    }
    #endregion

    #region Time Calculation Tests
    [UnityTest]
    public IEnumerator GetRemainingTime_ShouldReturnCorrectTime()
    {
        // Arrange
        float timeoutDuration = 1.0f; // 1 second
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", timeoutDuration);
        Assert.IsTrue(started, "Setup: Timeout should start");
        
        // Act - Check immediately
        float initialRemaining = _timeoutManager.GetRemainingTime("request-1");
        
        // Assert - Should be close to full duration
        Assert.IsTrue(initialRemaining > 0.9f && initialRemaining <= timeoutDuration, 
            $"Initial remaining time should be close to {timeoutDuration}, got {initialRemaining}");
        
        // Wait and check again
        yield return new WaitForSeconds(0.3f);
        
        float laterRemaining = _timeoutManager.GetRemainingTime("request-1");
        Assert.IsTrue(laterRemaining < initialRemaining, 
            $"Remaining time should decrease, initial: {initialRemaining}, later: {laterRemaining}");
        Assert.IsTrue(laterRemaining > 0.5f, 
            $"Should still have significant time remaining, got {laterRemaining}");
    }

    [Test]
    public void GetRemainingTime_WithNonExistentRequest_ShouldReturnNegativeOne()
    {
        // Act
        float remaining = _timeoutManager.GetRemainingTime("non-existent");
        
        // Assert
        Assert.AreEqual(-1f, remaining, "Should return -1 for non-existent request");
    }

    [UnityTest]
    public IEnumerator GetPlayerWaitTime_ShouldReturnMaxWaitTime()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        yield return new WaitForSeconds(0.1f); // Wait a bit
        _timeoutManager.StartRequestTimeout("request-2", "player-1", 10.0f); // Second request for same player
        
        yield return new WaitForSeconds(0.1f); // Wait more
        
        // Act
        float waitTime = _timeoutManager.GetPlayerWaitTime("player-1");
        
        // Assert
        Assert.IsTrue(waitTime > 0.15f, $"Wait time should be greater than 0.15s, got {waitTime}");
        Assert.IsTrue(waitTime < 1.0f, $"Wait time should be less than 1s, got {waitTime}");
    }
    #endregion

    #region Statistics Tests
    [Test]
    public void GetTimeoutStats_WithMultipleTimeouts_ShouldReturnCorrectStats()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        _timeoutManager.StartRequestTimeout("request-3", "player-1", 10.0f); // Same player
        
        // Act
        var stats = _timeoutManager.GetTimeoutStats();
        
        // Assert
        Assert.AreEqual(3, stats.ActiveTimeouts, "Should have 3 active timeouts");
        Assert.AreEqual(2, stats.UniquePlayersWaiting, "Should have 2 unique players waiting");
        Assert.IsTrue(stats.AverageWaitTime >= 0, "Average wait time should be non-negative");
        Assert.IsTrue(stats.MaxWaitTime >= 0, "Max wait time should be non-negative");
        Assert.AreEqual(0, stats.ExpiredTimeouts, "Should have no expired timeouts initially");
    }

    [Test]
    public void GetTimeoutStats_WithNoTimeouts_ShouldReturnZeroStats()
    {
        // Act
        var stats = _timeoutManager.GetTimeoutStats();
        
        // Assert
        Assert.AreEqual(0, stats.ActiveTimeouts, "Should have no active timeouts");
        Assert.AreEqual(0, stats.UniquePlayersWaiting, "Should have no waiting players");
        Assert.AreEqual(0f, stats.AverageWaitTime, "Average wait time should be zero");
        Assert.AreEqual(0f, stats.MaxWaitTime, "Max wait time should be zero");
        Assert.AreEqual(0, stats.ExpiredTimeouts, "Should have no expired timeouts");
    }
    #endregion

    #region Extension Tests
    [Test]
    public void ExtendTimeout_WithValidRequest_ShouldIncreaseTimeout()
    {
        // Arrange
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 1.0f);
        Assert.IsTrue(started, "Setup: Timeout should start");
        
        float initialRemaining = _timeoutManager.GetRemainingTime("request-1");
        
        // Act
        bool extended = _timeoutManager.ExtendTimeout("request-1", 5.0f);
        
        // Assert
        Assert.IsTrue(extended, "Extend should succeed");
        
        float newRemaining = _timeoutManager.GetRemainingTime("request-1");
        Assert.IsTrue(newRemaining > initialRemaining + 4.5f, 
            $"New remaining time should be much larger. Initial: {initialRemaining}, New: {newRemaining}");
    }

    [Test]
    public void ExtendTimeout_WithInvalidRequest_ShouldFail()
    {
        // Act
        bool extended = _timeoutManager.ExtendTimeout("non-existent", 5.0f);
        
        // Assert
        Assert.IsFalse(extended, "Extend should fail for non-existent request");
    }

    [Test]
    public void ExtendTimeout_WithInvalidParameters_ShouldFail()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        
        // Act & Assert
        LogAssert.Expect(LogType.Error, "[MatchingTimeout] Invalid extend parameters");
        bool result1 = _timeoutManager.ExtendTimeout("", 5.0f);
        Assert.IsFalse(result1, "Should fail with empty request ID");
        
        LogAssert.Expect(LogType.Error, "[MatchingTimeout] Invalid extend parameters");
        bool result2 = _timeoutManager.ExtendTimeout("request-1", -1.0f);
        Assert.IsFalse(result2, "Should fail with negative extension");
    }
    #endregion

    #region Component Lifecycle Tests
    [UnityTest]
    public IEnumerator OnDestroy_ShouldStopAllTimeouts()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        
        Assert.AreEqual(2, _timeoutManager.ActiveTimeoutCount, "Setup: Should have two timeouts");
        
        yield return null; // Allow coroutines to start
        
        // Act - Destroy component
        UnityEngine.Object.DestroyImmediate(_testGameObject);
        _testGameObject = null; // Prevent cleanup in TearDown
        
        yield return null; // Allow destruction to complete
        
        // Assert - Can't check timeout manager state as it's destroyed
        // This test mainly ensures no exceptions are thrown during destruction
        Assert.Pass("Component destruction completed without exceptions");
    }

    [Test]
    public void StopAllTimeouts_ShouldClearAllTimeouts()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 10.0f);
        
        Assert.AreEqual(2, _timeoutManager.ActiveTimeoutCount, "Setup: Should have two timeouts");
        
        // Act
        _timeoutManager.StopAllTimeouts();
        
        // Assert
        Assert.AreEqual(0, _timeoutManager.ActiveTimeoutCount, "Should have no active timeouts");
    }
    #endregion

    #region Edge Case Tests
    [UnityTest]
    public IEnumerator CancelTimeout_DuringTimeout_ShouldNotTriggerTimeoutEvent()
    {
        // Arrange
        bool timeoutTriggered = false;
        _timeoutManager.OnRequestTimeout += (requestId, playerId) => {
            timeoutTriggered = true;
        };
        
        bool started = _timeoutManager.StartRequestTimeout("request-1", "player-1", 0.2f); // 200ms timeout
        Assert.IsTrue(started, "Setup: Timeout should start");
        
        yield return new WaitForSeconds(0.1f); // Wait 100ms
        
        // Act - Cancel before timeout
        bool cancelled = _timeoutManager.CancelTimeout("request-1");
        
        // Wait for original timeout time to pass
        yield return new WaitForSeconds(0.2f);
        
        // Assert
        Assert.IsTrue(cancelled, "Cancel should succeed");
        Assert.IsFalse(timeoutTriggered, "Timeout event should not be triggered after cancel");
    }

    [Test]
    public void ActiveTimeouts_ShouldReturnCorrectRemainingTimes()
    {
        // Arrange
        _timeoutManager.StartRequestTimeout("request-1", "player-1", 10.0f);
        _timeoutManager.StartRequestTimeout("request-2", "player-2", 5.0f);
        
        // Act
        var activeTimeouts = _timeoutManager.ActiveTimeouts;
        
        // Assert
        Assert.AreEqual(2, activeTimeouts.Count, "Should have two active timeouts");
        Assert.IsTrue(activeTimeouts.ContainsKey("request-1"), "Should contain request-1");
        Assert.IsTrue(activeTimeouts.ContainsKey("request-2"), "Should contain request-2");
        
        Assert.IsTrue(activeTimeouts["request-1"] > 9.5f, "Request-1 should have close to 10s remaining");
        Assert.IsTrue(activeTimeouts["request-2"] > 4.5f, "Request-2 should have close to 5s remaining");
    }
    #endregion
}