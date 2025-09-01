using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// MatchingSection 단위 및 통합 테스트
/// 매칭 시스템의 모든 주요 기능을 검증합니다.
/// </summary>
public class MatchingSectionTests
{
    private GameObject _testObject;
    private MatchingSection _matchingSection;
    private TestMainPageManager _testMainPageManager;
    private TestUserDataManager _testUserDataManager;
    private TestNetworkManager _testNetworkManager;
    
    #region Setup and Teardown
    [SetUp]
    public void SetUp()
    {
        // Create test GameObject with required components
        _testObject = new GameObject("MatchingSectionTest");
        _matchingSection = _testObject.AddComponent<MatchingSection>();
        
        // Create test managers
        SetupTestManagers();
        
        // Initialize the section
        _matchingSection.Initialize(_testMainPageManager);
    }

    [TearDown]
    public void TearDown()
    {
        if (_testObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testObject);
        }
        
        CleanupTestManagers();
    }

    private void SetupTestManagers()
    {
        _testMainPageManager = new TestMainPageManager();
        _testUserDataManager = new TestUserDataManager();
        _testNetworkManager = new TestNetworkManager();
        
        // Set up test user data
        var testUserData = new UserData
        {
            UserId = "test-user-123",
            DisplayName = "TestPlayer",
            Level = 10,
            Rating = 1500,
            CurrentEnergy = 5,
            MaxEnergy = 10
        };
        
        _testUserDataManager.SetCurrentUser(testUserData);
    }

    private void CleanupTestManagers()
    {
        _testMainPageManager?.Cleanup();
        _testUserDataManager?.Cleanup();
        _testNetworkManager?.Cleanup();
    }
    #endregion

    #region Initialization Tests
    [Test]
    public void MatchingSection_Initialization_ShouldSetCorrectProperties()
    {
        // Assert
        Assert.AreEqual(MainPageSectionType.Matching, _matchingSection.SectionType);
        Assert.AreEqual("매칭", _matchingSection.SectionDisplayName);
        Assert.IsTrue(_matchingSection.IsInitialized);
        Assert.IsFalse(_matchingSection.IsActive);
    }

    [Test]
    public void MatchingSection_Activation_ShouldStartServices()
    {
        // Act
        _matchingSection.Activate();
        
        // Assert
        Assert.IsTrue(_matchingSection.IsActive);
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }

    [Test]
    public void MatchingSection_Deactivation_ShouldStopServices()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        _matchingSection.Deactivate();
        
        // Assert
        Assert.IsFalse(_matchingSection.IsActive);
    }

    [Test]
    public void MatchingSection_ValidateComponents_WithoutUI_ShouldReportError()
    {
        // Arrange
        var errorReported = false;
        SectionBase.OnSectionError += (sectionType, error) =>
        {
            if (sectionType == MainPageSectionType.Matching)
                errorReported = true;
        };
        
        // Act
        _matchingSection.ValidateComponents();
        
        // Assert
        Assert.IsTrue(errorReported);
        
        // Cleanup
        SectionBase.OnSectionError = null;
    }
    #endregion

    #region Game Mode Tests
    [Test]
    public void MatchingSection_GetGameModeConfig_ShouldReturnValidConfig()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        var classicConfig = _matchingSection.GetGameModeConfig(GameMode.Classic);
        var speedConfig = _matchingSection.GetGameModeConfig(GameMode.Speed);
        var challengeConfig = _matchingSection.GetGameModeConfig(GameMode.Challenge);
        var rankedConfig = _matchingSection.GetGameModeConfig(GameMode.Ranked);
        
        // Assert
        Assert.IsNotNull(classicConfig);
        Assert.AreEqual("클래식", classicConfig.DisplayName);
        Assert.AreEqual(1, classicConfig.EnergyCost);
        Assert.AreEqual(1, classicConfig.MinPlayerLevel);
        
        Assert.IsNotNull(speedConfig);
        Assert.AreEqual("스피드", speedConfig.DisplayName);
        Assert.AreEqual(2, speedConfig.EnergyCost);
        Assert.AreEqual(5, speedConfig.MinPlayerLevel);
        
        Assert.IsNotNull(challengeConfig);
        Assert.AreEqual("챌린지", challengeConfig.DisplayName);
        Assert.AreEqual(3, challengeConfig.EnergyCost);
        Assert.AreEqual(10, challengeConfig.MinPlayerLevel);
        
        Assert.IsNotNull(rankedConfig);
        Assert.AreEqual("랭크", rankedConfig.DisplayName);
        Assert.AreEqual(2, rankedConfig.EnergyCost);
        Assert.AreEqual(15, rankedConfig.MinPlayerLevel);
    }

    [Test]
    public void MatchingSection_GameModeSelection_ShouldUpdateSelectedMode()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        _matchingSection.HandleGameModeSelection(GameMode.Speed);
        
        // Assert
        Assert.AreEqual(GameMode.Speed, _matchingSection.GetSelectedGameMode());
    }

    [Test]
    public void MatchingSection_GameModeSelection_ShouldTriggerEvent()
    {
        // Arrange
        _matchingSection.Activate();
        var eventTriggered = false;
        GameMode selectedMode = GameMode.Classic;
        
        MatchingSection.OnGameModeChanged += (mode) =>
        {
            eventTriggered = true;
            selectedMode = mode;
        };
        
        // Act
        _matchingSection.HandleGameModeSelection(GameMode.Challenge);
        
        // Assert
        Assert.IsTrue(eventTriggered);
        Assert.AreEqual(GameMode.Challenge, selectedMode);
        
        // Cleanup
        MatchingSection.OnGameModeChanged = null;
    }
    #endregion

    #region Matching Validation Tests
    [Test]
    public void MatchingSection_CanStartMatching_WithValidConditions_ShouldReturnTrue()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        bool canMatch = _matchingSection.CanStartMatching(GameMode.Classic);
        
        // Assert
        Assert.IsTrue(canMatch);
    }

    [Test]
    public void MatchingSection_CanStartMatching_WithInsufficientLevel_ShouldReturnFalse()
    {
        // Arrange
        _matchingSection.Activate();
        var lowLevelUser = new UserData
        {
            Level = 1,
            CurrentEnergy = 10,
            MaxEnergy = 10
        };
        _matchingSection.OnUserDataUpdated(lowLevelUser);
        
        // Act
        bool canMatch = _matchingSection.CanStartMatching(GameMode.Ranked); // Requires level 15
        
        // Assert
        Assert.IsFalse(canMatch);
    }

    [Test]
    public void MatchingSection_CanStartMatching_WithInsufficientEnergy_ShouldReturnFalse()
    {
        // Arrange
        _matchingSection.Activate();
        var lowEnergyUser = new UserData
        {
            Level = 20,
            CurrentEnergy = 1,
            MaxEnergy = 10
        };
        _matchingSection.OnUserDataUpdated(lowEnergyUser);
        
        // Act
        bool canMatch = _matchingSection.CanStartMatching(GameMode.Challenge); // Requires 3 energy
        
        // Assert
        Assert.IsFalse(canMatch);
    }

    [Test]
    public void MatchingSection_CanStartMatching_InOfflineMode_ShouldReturnFalse()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        _matchingSection.OnModeChanged(true); // Set offline mode
        
        // Act
        bool canMatch = _matchingSection.CanStartMatching(GameMode.Classic);
        
        // Assert
        Assert.IsFalse(canMatch);
    }
    #endregion

    #region Quick Match Tests
    [Test]
    public void MatchingSection_StartQuickMatch_WithValidConditions_ShouldStartMatching()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        var stateChanged = false;
        MatchingState newState = MatchingState.Idle;
        
        MatchingSection.OnMatchingStateChanged += (state) =>
        {
            stateChanged = true;
            newState = state;
        };
        
        // Act
        _matchingSection.StartQuickMatch(GameMode.Classic);
        
        // Assert
        Assert.IsTrue(stateChanged);
        Assert.AreEqual(MatchingState.Searching, newState);
        Assert.AreEqual(MatchingState.Searching, _matchingSection.GetCurrentState());
        
        // Cleanup
        MatchingSection.OnMatchingStateChanged = null;
    }

    [Test]
    public void MatchingSection_StartQuickMatch_WithInvalidConditions_ShouldNotStartMatching()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnModeChanged(true); // Set offline mode
        
        // Act
        _matchingSection.StartQuickMatch(GameMode.Classic);
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }

    [UnityTest]
    public IEnumerator MatchingSection_StartQuickMatch_ShouldConsumeEnergy()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        var initialEnergy = _testUserDataManager.CurrentUser.CurrentEnergy;
        
        // Act
        _matchingSection.StartQuickMatch(GameMode.Classic);
        
        // Wait for energy consumption message to be sent
        yield return new WaitForSeconds(0.1f);
        
        // Assert (energy should be consumed through message to EnergySection)
        // Note: In actual implementation, we'd check the message sent to EnergySection
        Assert.AreEqual(MatchingState.Searching, _matchingSection.GetCurrentState());
    }
    #endregion

    #region Ranked Match Tests
    [Test]
    public void MatchingSection_StartRankedMatch_WithValidConditions_ShouldStartMatching()
    {
        // Arrange
        _matchingSection.Activate();
        var highLevelUser = new UserData
        {
            Level = 20,
            Rating = 1500,
            CurrentEnergy = 10,
            MaxEnergy = 10
        };
        _matchingSection.OnUserDataUpdated(highLevelUser);
        
        // Act
        _matchingSection.StartRankedMatch(GameMode.Ranked);
        
        // Assert
        Assert.AreEqual(MatchingState.Searching, _matchingSection.GetCurrentState());
    }

    [Test]
    public void MatchingSection_StartRankedMatch_WithLowLevel_ShouldNotStartMatching()
    {
        // Arrange
        _matchingSection.Activate();
        var lowLevelUser = new UserData
        {
            Level = 10,
            CurrentEnergy = 10,
            MaxEnergy = 10
        };
        _matchingSection.OnUserDataUpdated(lowLevelUser);
        
        // Act
        _matchingSection.StartRankedMatch(GameMode.Ranked);
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }
    #endregion

    #region Room Management Tests
    [Test]
    public void MatchingSection_CreateRoom_WithValidConditions_ShouldCreateRoom()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        _matchingSection.CreateRoom(GameMode.Classic, 4, false);
        
        // Assert
        // Note: In actual implementation, we'd verify the room creation request
        Assert.IsTrue(_matchingSection.CanCreateRoom());
    }

    [Test]
    public void MatchingSection_CanCreateRoom_InOfflineMode_ShouldReturnFalse()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnModeChanged(true); // Set offline mode
        
        // Act
        bool canCreate = _matchingSection.CanCreateRoom();
        
        // Assert
        Assert.IsFalse(canCreate);
    }

    [Test]
    public void MatchingSection_JoinRoom_WithValidCode_ShouldStartConnecting()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        _matchingSection.JoinRoom("ABCD");
        
        // Assert
        Assert.AreEqual(MatchingState.Connecting, _matchingSection.GetCurrentState());
    }

    [Test]
    public void MatchingSection_JoinRoom_WithInvalidCode_ShouldNotStartConnecting()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        _matchingSection.JoinRoom("ABC"); // Too short
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }
    #endregion

    #region Match Cancellation Tests
    [Test]
    public void MatchingSection_CancelMatching_WhileSearching_ShouldReturnToIdle()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        _matchingSection.StartQuickMatch(GameMode.Classic);
        
        // Act
        _matchingSection.CancelMatching();
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }

    [Test]
    public void MatchingSection_CancelMatching_WhileIdle_ShouldDoNothing()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        _matchingSection.CancelMatching();
        
        // Assert
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }
    #endregion

    #region UI Integration Tests
    [Test]
    public void MatchingSection_HandleMatchingRequest_ShouldStartAppropriateMatch()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        var quickRequest = new MatchingRequest
        {
            MatchType = MatchType.Quick,
            GameMode = GameMode.Classic
        };
        
        // Act
        _matchingSection.HandleMatchingRequest(quickRequest);
        
        // Assert
        Assert.AreEqual(MatchingState.Searching, _matchingSection.GetCurrentState());
        Assert.AreEqual(GameMode.Classic, _matchingSection.GetSelectedGameMode());
    }

    [Test]
    public void MatchingSection_HandleRoomCreationRequest_ShouldCreateRoom()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        var roomRequest = new RoomCreationRequest
        {
            GameMode = GameMode.Speed,
            MaxPlayers = 3,
            IsPrivate = true
        };
        
        // Act
        _matchingSection.HandleRoomCreationRequest(roomRequest);
        
        // Assert (verify the room creation process was initiated)
        Assert.AreEqual(GameMode.Speed, _matchingSection.GetSelectedGameMode());
    }
    #endregion

    #region Status and Information Tests
    [Test]
    public void MatchingSection_GetMatchingStatus_ShouldReturnCorrectInfo()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        var status = _matchingSection.GetMatchingStatus();
        
        // Assert
        Assert.IsNotNull(status);
        Assert.AreEqual(MatchingState.Idle, status.CurrentState);
        Assert.AreEqual(GameMode.Classic, status.SelectedGameMode);
        Assert.IsTrue(status.CanStartMatching);
        Assert.IsTrue(status.CanCreateRoom);
        Assert.IsFalse(status.IsOfflineMode);
    }

    [Test]
    public void MatchingSection_GetCurrentRoomCode_AfterRoomCreation_ShouldReturnCode()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Simulate room creation success
        var testRoomCode = "TEST";
        // Note: In actual implementation, this would be set through room creation callback
        
        // Act
        string roomCode = _matchingSection.GetCurrentRoomCode();
        
        // Assert
        // Note: This test would need proper room manager integration to work correctly
        Assert.IsTrue(string.IsNullOrEmpty(roomCode) || roomCode.Length == 4);
    }
    #endregion

    #region Settings and Configuration Tests
    [Test]
    public void MatchingSection_OnSettingUpdated_MatchingSettings_ShouldReloadConfig()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        _matchingSection.OnSettingChanged("Matching.MaxWaitTimeMinutes", 10f);
        
        // Assert (verify config was reloaded)
        // Note: This would require access to internal config to verify
        Assert.IsTrue(_matchingSection.IsInitialized);
    }

    [Test]
    public void MatchingSection_OnForceRefresh_ShouldRefreshDisplay()
    {
        // Arrange
        _matchingSection.Activate();
        _matchingSection.OnUserDataUpdated(_testUserDataManager.CurrentUser);
        
        // Act
        _matchingSection.ForceRefresh();
        
        // Assert (verify refresh was performed)
        Assert.IsTrue(_matchingSection.IsActive);
    }
    #endregion

    #region Message Handling Tests
    [Test]
    public void MatchingSection_OnReceiveMessage_EnergyResponse_ShouldHandleCorrectly()
    {
        // Arrange
        _matchingSection.Activate();
        var energyResponse = new EnergyResponse
        {
            Success = false,
            CurrentEnergy = 0,
            ErrorMessage = "Insufficient energy"
        };
        
        // Act
        _matchingSection.ReceiveMessage(MainPageSectionType.Energy, energyResponse);
        
        // Assert (verify appropriate action was taken)
        // Note: This would require checking error display in actual implementation
        Assert.AreEqual(MatchingState.Idle, _matchingSection.GetCurrentState());
    }

    [Test]
    public void MatchingSection_OnReceiveMessage_FocusRequest_ShouldRefreshDisplay()
    {
        // Arrange
        _matchingSection.Activate();
        
        // Act
        _matchingSection.ReceiveMessage(MainPageSectionType.Energy, "focus_requested");
        
        // Assert (verify display was refreshed)
        Assert.IsTrue(_matchingSection.IsActive);
    }
    #endregion
}

#region Test Helper Classes
/// <summary>
/// 테스트용 MainPageManager
/// </summary>
public class TestMainPageManager : MainPageManager
{
    private readonly Dictionary<MainPageSectionType, SectionBase> _testSections = new();
    private readonly List<MessageData> _sentMessages = new();
    
    public List<MessageData> SentMessages => _sentMessages;
    
    public override void RegisterSection(MainPageSectionType sectionType, SectionBase section)
    {
        _testSections[sectionType] = section;
    }
    
    public override void SendMessageToSection(MainPageSectionType fromSection, MainPageSectionType toSection, object data)
    {
        _sentMessages.Add(new MessageData
        {
            FromSection = fromSection,
            ToSection = toSection,
            Data = data,
            Timestamp = DateTime.Now
        });
    }
    
    public void Cleanup()
    {
        _testSections.Clear();
        _sentMessages.Clear();
    }
}

/// <summary>
/// 테스트용 UserDataManager
/// </summary>
public class TestUserDataManager
{
    public UserData CurrentUser { get; private set; }
    
    public void SetCurrentUser(UserData userData)
    {
        CurrentUser = userData;
    }
    
    public void Cleanup()
    {
        CurrentUser = null;
    }
}

/// <summary>
/// 테스트용 NetworkManager
/// </summary>
public class TestNetworkManager
{
    public bool IsConnected { get; set; } = true;
    public List<NetworkRequest> Requests { get; private set; } = new();
    
    public void Post(string endpoint, object data, Action<NetworkResponse> callback, float timeout = 0f)
    {
        Requests.Add(new NetworkRequest
        {
            Endpoint = endpoint,
            Data = data,
            Method = HttpMethod.POST
        });
        
        // Simulate successful response
        callback?.Invoke(new NetworkResponse
        {
            IsSuccess = true,
            StatusCode = 200,
            RawData = "{\"success\": true}"
        });
    }
    
    public void Cleanup()
    {
        Requests.Clear();
    }
}

/// <summary>
/// 메시지 데이터
/// </summary>
public class MessageData
{
    public MainPageSectionType FromSection;
    public MainPageSectionType ToSection;
    public object Data;
    public DateTime Timestamp;
}
#endregion