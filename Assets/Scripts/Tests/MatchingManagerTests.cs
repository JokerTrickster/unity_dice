using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MatchingManager 단위 테스트
/// 매칭 시스템의 핵심 로직, 상태 관리, 종속성 통합을 검증합니다.
/// </summary>
public class MatchingManagerTests
{
    private GameObject testGameObject;
    private MatchingManager matchingManager;
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
        testGameObject = new GameObject("MatchingManagerTest");
        matchingManager = testGameObject.AddComponent<MatchingManager>();
        
        // 설정 적용
        var configField = typeof(MatchingManager).GetField("matchingConfig", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(matchingManager, testConfig);
        
        // EnergyManager와 UserDataManager 초기화 확인
        EnsureManagersExist();
    }
    
    [TearDown]
    public void TearDown()
    {
        if (testGameObject != null)
        {
            Object.DestroyImmediate(testGameObject);
        }
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
        // Act
        matchingManager.Initialize();
        
        // Assert
        Assert.IsTrue(matchingManager.IsInitialized, "MatchingManager should be initialized");
    }
    
    [Test]
    public void Initialize_ShouldSetIdleState()
    {
        // Act
        matchingManager.Initialize();
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, matchingManager.CurrentState, 
            "Initial state should be Idle");
    }
    
    [Test]
    public void Initialize_WithoutManagers_ShouldThrowException()
    {
        // Arrange
        DestroyManagers();
        
        // Act & Assert
        Assert.Throws<System.Exception>(() => matchingManager.Initialize(),
            "Should throw exception when required managers are missing");
    }
    
    [Test]
    public void DoubleInitialize_ShouldLogWarning()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Act
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Already initialized.*"));
        matchingManager.Initialize();
        
        // Assert - Log assertion handles verification
    }
    #endregion

    #region State Management Tests
    [Test]
    public void CurrentState_BeforeInitialization_ShouldReturnIdle()
    {
        // Assert
        Assert.AreEqual(MatchingState.Idle, matchingManager.CurrentState,
            "Should return Idle state before initialization");
    }
    
    [Test]
    public void CanStartMatching_WhenIdle_ShouldReturnTrue()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Assert
        Assert.IsTrue(matchingManager.CanStartMatching,
            "Should be able to start matching when in Idle state");
    }
    
    [Test]
    public void IsMatching_WhenIdle_ShouldReturnFalse()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Assert
        Assert.IsFalse(matchingManager.IsMatching,
            "Should not be matching when in Idle state");
    }
    #endregion

    #region Matching Request Tests
    [UnityTest]
    public IEnumerator StartRandomMatching_WithValidParameters_ShouldStartMatching()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserAndEnergy();
        
        bool stateChanged = false;
        MatchingManager.OnStateChanged += (state) => {
            if (state == MatchingState.Searching) stateChanged = true;
        };
        
        // Act
        var task = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        yield return new WaitUntil(() => task.IsCompleted);
        
        // Assert
        Assert.IsTrue(task.Result, "StartRandomMatchingAsync should return true");
        Assert.IsTrue(stateChanged, "State should change to Searching");
        Assert.AreEqual(MatchingState.Searching, matchingManager.CurrentState,
            "Current state should be Searching");
        
        // Cleanup
        MatchingManager.OnStateChanged = null;
    }
    
    [UnityTest]
    public IEnumerator StartRandomMatching_WithInsufficientEnergy_ShouldFail()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserButNoEnergy();
        
        bool insufficientEnergyEventFired = false;
        MatchingManager.OnInsufficientEnergy += (required, current) => insufficientEnergyEventFired = true;
        
        // Act
        var task = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        yield return new WaitUntil(() => task.IsCompleted);
        
        // Assert
        Assert.IsFalse(task.Result, "Should fail when insufficient energy");
        Assert.IsTrue(insufficientEnergyEventFired, "Should fire insufficient energy event");
        Assert.AreEqual(MatchingState.Idle, matchingManager.CurrentState,
            "Should remain in Idle state");
        
        // Cleanup
        MatchingManager.OnInsufficientEnergy = null;
    }
    
    [UnityTest]
    public IEnumerator StartRandomMatching_WhenAlreadyMatching_ShouldFail()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserAndEnergy();
        
        var firstTask = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        yield return new WaitUntil(() => firstTask.IsCompleted);
        
        // Act
        var secondTask = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        yield return new WaitUntil(() => secondTask.IsCompleted);
        
        // Assert
        Assert.IsFalse(secondTask.Result, "Second matching attempt should fail");
    }
    
    [Test]
    public void StartRandomMatching_WithInvalidGameMode_ShouldReturnFalse()
    {
        // Arrange
        matchingManager.Initialize();
        testConfig.GetAllGameModeConfigs().ForEach(config => config.isEnabled = false);
        
        // Act & Assert
        var task = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        Assert.IsFalse(task.Result, "Should fail with disabled game mode");
    }
    #endregion

    #region Cancellation Tests
    [UnityTest]
    public IEnumerator CancelMatching_WhenMatching_ShouldCancel()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserAndEnergy();
        
        var startTask = matchingManager.StartRandomMatchingAsync(GameMode.Classic, 2);
        yield return new WaitUntil(() => startTask.IsCompleted);
        
        bool cancelEventFired = false;
        MatchingManager.OnMatchingCancelled += (reason) => cancelEventFired = true;
        
        // Act
        bool result = matchingManager.CancelMatching("Test cancellation");
        
        // Assert
        Assert.IsTrue(result, "Cancellation should succeed");
        Assert.IsTrue(cancelEventFired, "Should fire cancellation event");
        
        // Wait a frame for state change
        yield return null;
        Assert.AreEqual(MatchingState.Cancelled, matchingManager.CurrentState,
            "State should be Cancelled");
        
        // Cleanup
        MatchingManager.OnMatchingCancelled = null;
    }
    
    [Test]
    public void CancelMatching_WhenNotMatching_ShouldReturnFalse()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Act
        bool result = matchingManager.CancelMatching();
        
        // Assert
        Assert.IsFalse(result, "Should fail when not matching");
    }
    #endregion

    #region Message Handling Tests
    [Test]
    public void HandleMatchingResponse_WithMatchFound_ShouldTransitionToFound()
    {
        // Arrange
        matchingManager.Initialize();
        SimulateSearchingState();
        
        var response = new MatchingResponse
        {
            type = "matching_found",
            success = true,
            roomId = "test_room",
            players = new System.Collections.Generic.List<PlayerInfo>
            {
                new PlayerInfo { playerId = "player1", displayName = "Player 1" },
                new PlayerInfo { playerId = "player2", displayName = "Player 2" }
            }
        };
        
        bool matchFoundEventFired = false;
        MatchingManager.OnMatchFound += (players) => matchFoundEventFired = true;
        
        // Act
        matchingManager.HandleMatchingResponse(response);
        
        // Assert
        Assert.AreEqual(MatchingState.Found, matchingManager.CurrentState,
            "State should transition to Found");
        Assert.IsTrue(matchFoundEventFired, "Should fire match found event");
        
        // Cleanup
        MatchingManager.OnMatchFound = null;
    }
    
    [Test]
    public void HandleMatchingResponse_WithGameStarting_ShouldTransitionToStarting()
    {
        // Arrange
        matchingManager.Initialize();
        SimulateFoundState();
        
        var response = new MatchingResponse
        {
            type = "game_starting",
            success = true,
            roomId = "test_room",
            gameMode = GameMode.Classic
        };
        
        bool gameStartingEventFired = false;
        MatchingManager.OnGameStarting += (data) => gameStartingEventFired = true;
        
        // Act
        matchingManager.HandleMatchingResponse(response);
        
        // Assert
        Assert.AreEqual(MatchingState.Starting, matchingManager.CurrentState,
            "State should transition to Starting");
        Assert.IsTrue(gameStartingEventFired, "Should fire game starting event");
        
        // Cleanup
        MatchingManager.OnGameStarting = null;
    }
    
    [Test]
    public void HandleMatchingResponse_WithFailure_ShouldTransitionToFailed()
    {
        // Arrange
        matchingManager.Initialize();
        SimulateSearchingState();
        
        var response = new MatchingResponse
        {
            type = "matching_failed",
            success = false,
            error = "Test error"
        };
        
        bool failEventFired = false;
        MatchingManager.OnMatchingFailed += (error) => failEventFired = true;
        
        // Act
        matchingManager.HandleMatchingResponse(response);
        
        // Assert
        Assert.AreEqual(MatchingState.Failed, matchingManager.CurrentState,
            "State should transition to Failed");
        Assert.IsTrue(failEventFired, "Should fire matching failed event");
        
        // Cleanup
        MatchingManager.OnMatchingFailed = null;
    }
    #endregion

    #region Validation Tests
    [Test]
    public void CanMatchWithGameMode_WithValidMode_ShouldReturnTrue()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserAndEnergy();
        
        // Act
        bool result = matchingManager.CanMatchWithGameMode(GameMode.Classic);
        
        // Assert
        Assert.IsTrue(result, "Should be able to match with valid game mode");
    }
    
    [Test]
    public void CanMatchWithGameMode_WithDisabledMode_ShouldReturnFalse()
    {
        // Arrange
        matchingManager.Initialize();
        SetupValidUserAndEnergy();
        testConfig.GetGameModeConfig(GameMode.Classic).isEnabled = false;
        
        // Act
        bool result = matchingManager.CanMatchWithGameMode(GameMode.Classic);
        
        // Assert
        Assert.IsFalse(result, "Should not be able to match with disabled game mode");
    }
    #endregion

    #region Configuration Tests
    [Test]
    public void GetConfiguration_ShouldReturnValidConfig()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Act
        var config = matchingManager.GetConfiguration();
        
        // Assert
        Assert.IsNotNull(config, "Should return valid configuration");
        Assert.AreSame(testConfig, config, "Should return the same config instance");
    }
    
    [Test]
    public void GetCurrentMatchingInfo_ShouldReturnValidInfo()
    {
        // Arrange
        matchingManager.Initialize();
        
        // Act
        var info = matchingManager.GetCurrentMatchingInfo();
        
        // Assert
        Assert.IsNotNull(info, "Should return valid matching info");
        Assert.AreEqual(MatchingState.Idle, info.currentState, "Initial state should be Idle");
    }
    #endregion

    #region Helper Methods
    private void EnsureManagersExist()
    {
        // EnergyManager가 없으면 생성
        if (EnergyManager.Instance == null)
        {
            var energyGO = new GameObject("EnergyManager");
            energyGO.AddComponent<EnergyManager>();
            
            // EnergyConfig 설정
            var energyConfig = ScriptableObject.CreateInstance<EnergyConfig>();
            var configField = typeof(EnergyManager).GetField("configOverride",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(EnergyManager.Instance, energyConfig);
        }
        
        // UserDataManager가 없으면 생성
        if (UserDataManager.Instance == null)
        {
            var userGO = new GameObject("UserDataManager");
            userGO.AddComponent<UserDataManager>();
        }
        
        // NetworkManager가 없으면 생성
        if (NetworkManager.Instance == null)
        {
            var networkGO = new GameObject("NetworkManager");
            networkGO.AddComponent<NetworkManager>();
        }
    }
    
    private void DestroyManagers()
    {
        var energyInstance = Object.FindObjectOfType<EnergyManager>();
        if (energyInstance != null) Object.DestroyImmediate(energyInstance.gameObject);
        
        var userInstance = Object.FindObjectOfType<UserDataManager>();
        if (userInstance != null) Object.DestroyImmediate(userInstance.gameObject);
        
        var networkInstance = Object.FindObjectOfType<NetworkManager>();
        if (networkInstance != null) Object.DestroyImmediate(networkInstance.gameObject);
    }
    
    private void SetupValidUserAndEnergy()
    {
        // 유효한 사용자 데이터 설정
        UserDataManager.Instance.SetCurrentUser("test_user", "Test User");
        
        // 충분한 에너지 설정
        try
        {
            var addEnergyMethod = typeof(EnergyManager).GetMethod("AddEnergy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            addEnergyMethod?.Invoke(EnergyManager.Instance, new object[] { 10 });
        }
        catch (System.Exception)
        {
            // 에너지 추가가 실패하면 대체 방법 사용
            Debug.LogWarning("Could not add energy via reflection, using direct method if available");
        }
    }
    
    private void SetupValidUserButNoEnergy()
    {
        // 유효한 사용자 데이터만 설정, 에너지는 0으로 유지
        UserDataManager.Instance.SetCurrentUser("test_user", "Test User");
    }
    
    private void SimulateSearchingState()
    {
        // StateManager에 직접 접근하여 검색 중 상태로 설정
        var stateManagerField = typeof(MatchingManager).GetField("_stateManager",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stateManager = stateManagerField?.GetValue(matchingManager) as MatchingStateManager;
        stateManager?.ChangeState(MatchingState.Searching, "Test simulation");
    }
    
    private void SimulateFoundState()
    {
        // StateManager에 직접 접근하여 매칭 완료 상태로 설정
        var stateManagerField = typeof(MatchingManager).GetField("_stateManager",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stateManager = stateManagerField?.GetValue(matchingManager) as MatchingStateManager;
        stateManager?.ChangeState(MatchingState.Found, "Test simulation");
    }
    #endregion
}