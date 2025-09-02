using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MatchingStateManager 단위 테스트
/// 상태 전환, 유효성 검사, 타임아웃, 지속성을 검증합니다.
/// </summary>
public class MatchingStateManagerTests
{
    private GameObject testGameObject;
    private MatchingStateManager stateManager;
    private MatchingConfig testConfig;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // 테스트용 설정 생성
        testConfig = ScriptableObject.CreateInstance<MatchingConfig>();
        testConfig.ResetToDefault();
    }
    
    [SetUp]
    public void SetUp()
    {
        // 테스트용 GameObject 생성
        testGameObject = new GameObject("MatchingStateManagerTest");
        stateManager = testGameObject.AddComponent<MatchingStateManager>();
        
        // PlayerPrefs 초기화 (테스트 격리)
        PlayerPrefs.DeleteKey("MatchingState");
        PlayerPrefs.DeleteKey("LastStateTime");
        
        // 초기화
        stateManager.Initialize(testConfig);
    }
    
    [TearDown]
    public void TearDown()
    {
        if (testGameObject != null)
        {
            Object.DestroyImmediate(testGameObject);
        }
        
        // 테스트 데이터 정리
        PlayerPrefs.DeleteKey("MatchingState");
        PlayerPrefs.DeleteKey("LastStateTime");
    }
    
    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (testConfig != null)
        {
            Object.DestroyImmediate(testConfig);
        }
    }

    #region Initialization Tests
    [Test]
    public void Initialize_ShouldSetInitializedFlag()
    {
        // Assert
        Assert.IsTrue(stateManager.IsInitialized, "StateManager should be initialized");
    }
    
    [Test]
    public void Initialize_ShouldSetInitialState()
    {
        // Assert
        Assert.AreEqual(MatchingState.Idle, stateManager.State, "Initial state should be Idle");
    }
    
    [Test]
    public void Initialize_WithNullConfig_ShouldThrowException()
    {
        // Arrange
        var newGameObject = new GameObject("TestStateManager");
        var newStateManager = newGameObject.AddComponent<MatchingStateManager>();
        
        // Act & Assert
        Assert.Throws<System.ArgumentNullException>(() => newStateManager.Initialize(null));
        
        // Cleanup
        Object.DestroyImmediate(newGameObject);
    }
    
    [Test]
    public void DoubleInitialize_ShouldLogWarning()
    {
        // Act
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Already initialized.*"));
        stateManager.Initialize(testConfig);
        
        // Assert - Log assertion handles verification
    }
    #endregion

    #region State Transition Tests
    [Test]
    public void ChangeState_ValidTransition_ShouldSucceed()
    {
        // Act
        bool result = stateManager.ChangeState(MatchingState.Searching, "Test transition");
        
        // Assert
        Assert.IsTrue(result, "Valid state transition should succeed");
        Assert.AreEqual(MatchingState.Searching, stateManager.State, "State should be updated");
    }
    
    [Test]
    public void ChangeState_InvalidTransition_ShouldFail()
    {
        // Act
        bool result = stateManager.ChangeState(MatchingState.Starting, "Invalid transition");
        
        // Assert
        Assert.IsFalse(result, "Invalid state transition should fail");
        Assert.AreEqual(MatchingState.Idle, stateManager.State, "State should remain unchanged");
    }
    
    [Test]
    public void ChangeState_SameState_ShouldSucceedButNotChangeState()
    {
        // Act
        bool result = stateManager.ChangeState(MatchingState.Idle, "Same state");
        
        // Assert
        Assert.IsTrue(result, "Same state transition should succeed");
        Assert.AreEqual(MatchingState.Idle, stateManager.State, "State should remain Idle");
    }
    
    [Test]
    public void ChangeState_ValidTransition_ShouldFireEvent()
    {
        // Arrange
        MatchingState previousState = MatchingState.Idle;
        MatchingState newState = MatchingState.Idle;
        bool eventFired = false;
        
        MatchingStateManager.OnStateChanged += (prev, next) => {
            previousState = prev;
            newState = next;
            eventFired = true;
        };
        
        // Act
        stateManager.ChangeState(MatchingState.Searching, "Test event");
        
        // Assert
        Assert.IsTrue(eventFired, "State change event should fire");
        Assert.AreEqual(MatchingState.Idle, previousState, "Previous state should be Idle");
        Assert.AreEqual(MatchingState.Searching, newState, "New state should be Searching");
        
        // Cleanup
        MatchingStateManager.OnStateChanged = null;
    }
    
    [Test]
    public void ChangeState_InvalidTransition_ShouldFireTransitionFailedEvent()
    {
        // Arrange
        bool eventFired = false;
        MatchingState fromState = MatchingState.Idle;
        MatchingState toState = MatchingState.Idle;
        string reason = "";
        
        MatchingStateManager.OnStateTransitionFailed += (from, to, error) => {
            fromState = from;
            toState = to;
            reason = error;
            eventFired = true;
        };
        
        // Act
        stateManager.ChangeState(MatchingState.Starting, "Invalid transition");
        
        // Assert
        Assert.IsTrue(eventFired, "Transition failed event should fire");
        Assert.AreEqual(MatchingState.Idle, fromState, "From state should be Idle");
        Assert.AreEqual(MatchingState.Starting, toState, "To state should be Starting");
        Assert.IsNotEmpty(reason, "Reason should be provided");
        
        // Cleanup
        MatchingStateManager.OnStateTransitionFailed = null;
    }
    #endregion

    #region Transition Validation Tests
    [Test]
    public void IsValidTransition_ValidTransitions_ShouldReturnTrue()
    {
        // Test valid transitions from Idle
        Assert.IsTrue(stateManager.IsValidTransition(MatchingState.Idle, MatchingState.Searching),
            "Idle -> Searching should be valid");
        
        // Test valid transitions from Searching
        Assert.IsTrue(stateManager.IsValidTransition(MatchingState.Searching, MatchingState.Found),
            "Searching -> Found should be valid");
        Assert.IsTrue(stateManager.IsValidTransition(MatchingState.Searching, MatchingState.Cancelled),
            "Searching -> Cancelled should be valid");
        Assert.IsTrue(stateManager.IsValidTransition(MatchingState.Searching, MatchingState.Failed),
            "Searching -> Failed should be valid");
    }
    
    [Test]
    public void IsValidTransition_InvalidTransitions_ShouldReturnFalse()
    {
        // Test invalid transitions
        Assert.IsFalse(stateManager.IsValidTransition(MatchingState.Idle, MatchingState.Found),
            "Idle -> Found should be invalid");
        Assert.IsFalse(stateManager.IsValidTransition(MatchingState.Found, MatchingState.Searching),
            "Found -> Searching should be invalid");
        Assert.IsFalse(stateManager.IsValidTransition(MatchingState.Cancelled, MatchingState.Found),
            "Cancelled -> Found should be invalid");
    }
    
    [Test]
    public void CanTransitionTo_FromCurrentState_ShouldWork()
    {
        // Idle state - can only transition to Searching
        Assert.IsTrue(stateManager.CanTransitionTo(MatchingState.Searching),
            "Should be able to transition from Idle to Searching");
        Assert.IsFalse(stateManager.CanTransitionTo(MatchingState.Found),
            "Should not be able to transition from Idle to Found");
        
        // Change to Searching state
        stateManager.ChangeState(MatchingState.Searching);
        
        // Searching state - can transition to Found, Cancelled, Failed
        Assert.IsTrue(stateManager.CanTransitionTo(MatchingState.Found),
            "Should be able to transition from Searching to Found");
        Assert.IsTrue(stateManager.CanTransitionTo(MatchingState.Cancelled),
            "Should be able to transition from Searching to Cancelled");
        Assert.IsTrue(stateManager.CanTransitionTo(MatchingState.Failed),
            "Should be able to transition from Searching to Failed");
        Assert.IsFalse(stateManager.CanTransitionTo(MatchingState.Starting),
            "Should not be able to transition from Searching to Starting");
    }
    
    [Test]
    public void GetPossibleNextStates_ShouldReturnValidStates()
    {
        // From Idle state
        var possibleStates = stateManager.GetPossibleNextStates();
        Assert.Contains(MatchingState.Searching, possibleStates.ToArray(),
            "Searching should be possible from Idle");
        Assert.AreEqual(1, possibleStates.Count, "Only one transition should be possible from Idle");
        
        // Change to Searching state
        stateManager.ChangeState(MatchingState.Searching);
        possibleStates = stateManager.GetPossibleNextStates();
        
        Assert.Contains(MatchingState.Found, possibleStates.ToArray(),
            "Found should be possible from Searching");
        Assert.Contains(MatchingState.Cancelled, possibleStates.ToArray(),
            "Cancelled should be possible from Searching");
        Assert.Contains(MatchingState.Failed, possibleStates.ToArray(),
            "Failed should be possible from Searching");
        Assert.AreEqual(3, possibleStates.Count, "Three transitions should be possible from Searching");
    }
    #endregion

    #region State Information Tests
    [Test]
    public void SetSelectedGameMode_ShouldUpdateStateInfo()
    {
        // Act
        stateManager.SetSelectedGameMode(GameMode.Speed);
        
        // Assert
        Assert.AreEqual(GameMode.Speed, stateManager.CurrentState.selectedGameMode,
            "Selected game mode should be updated");
    }
    
    [Test]
    public void SetMatchType_ShouldUpdateStateInfo()
    {
        // Act
        stateManager.SetMatchType(MatchType.Ranked);
        
        // Assert
        Assert.AreEqual(MatchType.Ranked, stateManager.CurrentState.matchType,
            "Match type should be updated");
    }
    
    [Test]
    public void SetSelectedPlayerCount_ShouldUpdateStateInfo()
    {
        // Act
        stateManager.SetSelectedPlayerCount(3);
        
        // Assert
        Assert.AreEqual(3, stateManager.CurrentState.selectedPlayerCount,
            "Selected player count should be updated");
    }
    
    [Test]
    public void SetSelectedPlayerCount_OutOfRange_ShouldClamp()
    {
        // Test below minimum
        stateManager.SetSelectedPlayerCount(1);
        Assert.AreEqual(2, stateManager.CurrentState.selectedPlayerCount,
            "Player count should be clamped to minimum");
        
        // Test above maximum
        stateManager.SetSelectedPlayerCount(10);
        Assert.AreEqual(4, stateManager.CurrentState.selectedPlayerCount,
            "Player count should be clamped to maximum");
    }
    
    [Test]
    public void SetMatchedPlayers_ShouldUpdatePlayerList()
    {
        // Arrange
        var players = new List<PlayerInfo>
        {
            new PlayerInfo { playerId = "player1", displayName = "Player 1" },
            new PlayerInfo { playerId = "player2", displayName = "Player 2" }
        };
        
        // Act
        stateManager.SetMatchedPlayers(players);
        
        // Assert
        Assert.AreEqual(2, stateManager.CurrentState.matchedPlayers.Count,
            "Matched players list should be updated");
        Assert.AreEqual("player1", stateManager.CurrentState.matchedPlayers[0].playerId,
            "First player should be correct");
    }
    
    [Test]
    public void SetCurrentRoomCode_ShouldUpdateRoomCode()
    {
        // Act
        stateManager.SetCurrentRoomCode("ABCD");
        
        // Assert
        Assert.AreEqual("ABCD", stateManager.CurrentState.currentRoomCode,
            "Room code should be updated");
    }
    #endregion

    #region Timeout Tests
    [UnityTest]
    public IEnumerator StateTimeout_SearchingState_ShouldTransitionToFailed()
    {
        // Arrange
        // 타임아웃을 매우 짧게 설정 (테스트용)
        var shortTimeoutConfig = ScriptableObject.CreateInstance<MatchingConfig>();
        shortTimeoutConfig.ResetToDefault();
        
        // 리플렉션을 통해 짧은 타임아웃 설정
        var stateTimeoutsField = typeof(MatchingStateManager).GetField("_stateTimeouts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var timeouts = stateTimeoutsField?.GetValue(stateManager) as Dictionary<MatchingState, float>;
        if (timeouts != null)
        {
            timeouts[MatchingState.Searching] = 0.1f; // 0.1초 타임아웃
        }
        
        bool timeoutEventFired = false;
        MatchingStateManager.OnStateTimeout += (state) => {
            if (state == MatchingState.Searching) timeoutEventFired = true;
        };
        
        // Act
        stateManager.ChangeState(MatchingState.Searching, "Test timeout");
        
        // Wait for timeout
        yield return new WaitForSeconds(0.2f);
        
        // Assert
        Assert.IsTrue(timeoutEventFired, "Timeout event should fire");
        Assert.AreEqual(MatchingState.Failed, stateManager.State,
            "State should transition to Failed after timeout");
        
        // Cleanup
        MatchingStateManager.OnStateTimeout = null;
        Object.DestroyImmediate(shortTimeoutConfig);
    }
    #endregion

    #region State Control Tests
    [Test]
    public void SetCanChangeState_False_ShouldPreventStateChanges()
    {
        // Arrange
        stateManager.SetCanChangeState(false);
        
        // Act
        bool result = stateManager.ChangeState(MatchingState.Searching, "Should be prevented");
        
        // Assert
        Assert.IsFalse(result, "State change should be prevented");
        Assert.AreEqual(MatchingState.Idle, stateManager.State, "State should remain unchanged");
    }
    
    [Test]
    public void SetCanChangeState_True_ShouldAllowStateChanges()
    {
        // Arrange
        stateManager.SetCanChangeState(false);
        stateManager.SetCanChangeState(true);
        
        // Act
        bool result = stateManager.ChangeState(MatchingState.Searching, "Should be allowed");
        
        // Assert
        Assert.IsTrue(result, "State change should be allowed");
        Assert.AreEqual(MatchingState.Searching, stateManager.State, "State should change");
    }
    
    [Test]
    public void ForceReset_ShouldResetToIdle()
    {
        // Arrange
        stateManager.ChangeState(MatchingState.Searching);
        Assert.AreEqual(MatchingState.Searching, stateManager.State, "Precondition: should be Searching");
        
        // Act
        stateManager.ForceReset();
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, stateManager.State, "State should be reset to Idle");
    }
    #endregion

    #region Persistence Tests
    [Test]
    public void StatePersistence_ShouldSaveAndRestore()
    {
        // Arrange - 상태 정보 설정
        stateManager.SetSelectedGameMode(GameMode.Speed);
        stateManager.SetMatchType(MatchType.Ranked);
        stateManager.SetSelectedPlayerCount(3);
        
        // 상태 저장을 위한 이벤트 대기
        bool stateSaved = false;
        MatchingStateManager.OnStateSaved += (info) => stateSaved = true;
        
        // Act - 상태 변경으로 자동 저장 트리거
        stateManager.ChangeState(MatchingState.Searching, "Trigger save");
        
        // 새 StateManager 생성하여 복원 테스트
        var newGameObject = new GameObject("NewStateManager");
        var newStateManager = newGameObject.AddComponent<MatchingStateManager>();
        
        bool stateRestored = false;
        MatchingStateManager.OnStateRestored += (info) => stateRestored = true;
        
        // 복원 테스트
        newStateManager.Initialize(testConfig);
        
        // Assert
        Assert.IsTrue(stateSaved, "State should be saved");
        
        // 복원된 상태는 검색 중이 아닌 Idle이어야 함 (임시 상태는 초기화)
        Assert.AreEqual(MatchingState.Idle, newStateManager.State,
            "Restored state should be Idle (transient states reset)");
        Assert.AreEqual(GameMode.Speed, newStateManager.CurrentState.selectedGameMode,
            "Game mode should be restored");
        Assert.AreEqual(MatchType.Ranked, newStateManager.CurrentState.matchType,
            "Match type should be restored");
        Assert.AreEqual(3, newStateManager.CurrentState.selectedPlayerCount,
            "Player count should be restored");
        
        // Cleanup
        Object.DestroyImmediate(newGameObject);
        MatchingStateManager.OnStateSaved = null;
        MatchingStateManager.OnStateRestored = null;
    }
    #endregion

    #region State Property Tests
    [Test]
    public void MatchingStateInfo_Properties_ShouldWorkCorrectly()
    {
        // Test IsSearching
        Assert.IsFalse(stateManager.CurrentState.IsSearching, "Should not be searching in Idle state");
        
        stateManager.ChangeState(MatchingState.Searching);
        Assert.IsTrue(stateManager.CurrentState.IsSearching, "Should be searching in Searching state");
        
        // Test IsMatched
        stateManager.ChangeState(MatchingState.Found);
        Assert.IsTrue(stateManager.CurrentState.IsMatched, "Should be matched in Found state");
        
        stateManager.ChangeState(MatchingState.Starting);
        Assert.IsTrue(stateManager.CurrentState.IsMatched, "Should be matched in Starting state");
        
        // Test HasError
        stateManager.ChangeState(MatchingState.Failed);
        Assert.IsTrue(stateManager.CurrentState.HasError, "Should have error in Failed state");
    }
    
    [Test]
    public void CurrentSearchTime_ShouldReturnCorrectValue()
    {
        // Idle state - should return 0
        Assert.AreEqual(0f, stateManager.CurrentState.CurrentSearchTime,
            "Search time should be 0 in Idle state");
        
        // Change to Searching - should return positive value after delay
        stateManager.ChangeState(MatchingState.Searching);
        
        // Small delay to ensure time has passed
        System.Threading.Thread.Sleep(10);
        
        Assert.Greater(stateManager.CurrentState.CurrentSearchTime, 0f,
            "Search time should be positive in Searching state");
    }
    #endregion
}