using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MatchingNetworkHandler 단위 테스트
/// WebSocket 통신 및 매칭 요청/응답 처리 테스트
/// </summary>
public class MatchingNetworkHandlerTests
{
    #region Test Setup
    private GameObject _testGameObject;
    private MatchingNetworkHandler _handler;
    private MockNetworkManager _mockNetworkManager;
    
    [SetUp]
    public void SetUp()
    {
        // 테스트용 GameObject 생성
        _testGameObject = new GameObject("TestMatchingNetworkHandler");
        _handler = _testGameObject.AddComponent<MatchingNetworkHandler>();
        
        // Mock NetworkManager 생성
        var networkManagerGO = new GameObject("MockNetworkManager");
        _mockNetworkManager = networkManagerGO.AddComponent<MockNetworkManager>();
    }
    
    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
        
        if (_mockNetworkManager != null)
        {
            UnityEngine.Object.DestroyImmediate(_mockNetworkManager.gameObject);
        }
    }
    #endregion

    #region Initialization Tests
    [UnityTest]
    public IEnumerator InitializeAsync_WithValidNetworkManager_ShouldSucceed()
    {
        // Arrange
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        
        // Act
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        // Assert
        Assert.IsTrue(_handler.IsInitialized, "Handler should be initialized");
        Assert.IsTrue(_handler.IsConnected, "Handler should be connected");
    }

    [Test]
    public void InitializeAsync_WithNullNetworkManager_ShouldLogError()
    {
        // Act & Assert
        LogAssert.Expect(LogType.Error, "[MatchingNetworkHandler] NetworkManager cannot be null");
        
        var initTask = _handler.InitializeAsync(null);
    }

    [UnityTest]
    public IEnumerator InitializeAsync_WhenAlreadyInitialized_ShouldLogWarning()
    {
        // Arrange
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        
        var firstInit = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => firstInit.IsCompleted);
        
        // Act & Assert
        LogAssert.Expect(LogType.Warning, "[MatchingNetworkHandler] Already initialized");
        
        var secondInit = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => secondInit.IsCompleted);
    }
    #endregion

    #region Connection Tests
    [UnityTest]
    public IEnumerator IsConnected_WhenWebSocketConnected_ShouldReturnTrue()
    {
        // Arrange
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        // Act & Assert
        Assert.IsTrue(_handler.IsConnected, "Should be connected when WebSocket is connected");
    }

    [UnityTest]
    public IEnumerator IsConnected_WhenWebSocketDisconnected_ShouldReturnFalse()
    {
        // Arrange
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(false);
        
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        // Act & Assert
        Assert.IsFalse(_handler.IsConnected, "Should not be connected when WebSocket is disconnected");
    }

    [UnityTest]
    public IEnumerator ConnectionStateChanged_WhenConnected_ShouldTriggerEvent()
    {
        // Arrange
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        
        bool eventTriggered = false;
        bool connectionState = false;
        
        _handler.OnConnectionStateChanged += (connected) => {
            eventTriggered = true;
            connectionState = connected;
        };
        
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        // Act
        _mockNetworkManager.TriggerConnectionChanged(true);
        yield return null; // Wait for event processing
        
        // Assert
        Assert.IsTrue(eventTriggered, "Connection state changed event should be triggered");
        Assert.IsTrue(connectionState, "Connection state should be true");
    }
    #endregion

    #region Matching Request Tests
    [UnityTest]
    public IEnumerator SendJoinQueueRequestAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        _mockNetworkManager.SetSendJoinQueueRequestResult(true);
        
        // Act
        var sendTask = _handler.SendJoinQueueRequestAsync("player123", 2, "classic", 100);
        yield return new WaitUntil(() => sendTask.IsCompleted);
        
        // Assert
        Assert.IsTrue(sendTask.Result, "Send request should succeed");
        Assert.AreEqual(1, _handler.PendingRequestCount, "Should have one pending request");
        
        var lastRequest = _mockNetworkManager.GetLastJoinQueueRequest();
        Assert.AreEqual("player123", lastRequest.playerId);
        Assert.AreEqual(2, lastRequest.playerCount);
        Assert.AreEqual("classic", lastRequest.gameMode);
        Assert.AreEqual(100, lastRequest.betAmount);
    }

    [UnityTest]
    public IEnumerator SendJoinQueueRequestAsync_WhenNotConnected_ShouldFail()
    {
        // Arrange - Not connected
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(false);
        
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        // Act
        LogAssert.Expect(LogType.Warning, "[MatchingNetworkHandler] Cannot join queue: Not connected to server");
        
        var sendTask = _handler.SendJoinQueueRequestAsync("player123", 2, "classic", 0);
        yield return new WaitUntil(() => sendTask.IsCompleted);
        
        // Assert
        Assert.IsFalse(sendTask.Result, "Send request should fail when not connected");
        Assert.AreEqual(0, _handler.PendingRequestCount, "Should have no pending requests");
    }

    [UnityTest]
    public IEnumerator SendRoomCreateRequestAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        _mockNetworkManager.SetSendRoomCreateRequestResult(true);
        
        // Act
        var sendTask = _handler.SendRoomCreateRequestAsync("player123", 4, "classic", 50, true);
        yield return new WaitUntil(() => sendTask.IsCompleted);
        
        // Assert
        Assert.IsTrue(sendTask.Result, "Room create request should succeed");
        Assert.AreEqual(1, _handler.PendingRequestCount, "Should have one pending request");
        
        var lastRequest = _mockNetworkManager.GetLastRoomCreateRequest();
        Assert.AreEqual("player123", lastRequest.playerId);
        Assert.AreEqual(4, lastRequest.playerCount);
        Assert.AreEqual(true, lastRequest.isPrivate);
    }

    [UnityTest]
    public IEnumerator SendRoomJoinRequestAsync_WithValidRoomCode_ShouldSucceed()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        _mockNetworkManager.SetSendRoomJoinRequestResult(true);
        
        // Act
        var sendTask = _handler.SendRoomJoinRequestAsync("player123", "ABCD1234");
        yield return new WaitUntil(() => sendTask.IsCompleted);
        
        // Assert
        Assert.IsTrue(sendTask.Result, "Room join request should succeed");
        Assert.AreEqual(1, _handler.PendingRequestCount, "Should have one pending request");
        
        var lastRequest = _mockNetworkManager.GetLastRoomJoinRequest();
        Assert.AreEqual("player123", lastRequest.playerId);
        Assert.AreEqual("ABCD1234", lastRequest.roomCode);
    }

    [UnityTest]
    public IEnumerator SendMatchingCancelRequestAsync_ShouldClearPendingRequests()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        _mockNetworkManager.SetSendJoinQueueRequestResult(true);
        _mockNetworkManager.SetSendMatchingCancelRequestResult(true);
        
        // Add pending request
        var sendTask = _handler.SendJoinQueueRequestAsync("player123", 2, "classic", 0);
        yield return new WaitUntil(() => sendTask.IsCompleted);
        
        Assert.AreEqual(1, _handler.PendingRequestCount, "Should have one pending request");
        
        // Act
        var cancelTask = _handler.SendMatchingCancelRequestAsync("player123");
        yield return new WaitUntil(() => cancelTask.IsCompleted);
        
        // Assert
        Assert.IsTrue(cancelTask.Result, "Cancel request should succeed");
        Assert.AreEqual(0, _handler.PendingRequestCount, "Should have no pending requests after cancel");
    }
    #endregion

    #region Message Processing Tests
    [UnityTest]
    public IEnumerator HandleWebSocketMessage_WithMatchingResponse_ShouldTriggerEvent()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        bool eventTriggered = false;
        MatchingResponse receivedResponse = null;
        
        _handler.OnMatchingResponse += (response) => {
            eventTriggered = true;
            receivedResponse = response;
        };
        
        var testResponse = MatchingResponse.CreateQueueResponse(1, 30);
        var message = testResponse.ToMessage();
        string jsonMessage = message.ToJson();
        
        // Act
        _mockNetworkManager.TriggerWebSocketMessage(jsonMessage);
        yield return null; // Wait for message processing
        
        // Assert
        Assert.IsTrue(eventTriggered, "Matching response event should be triggered");
        Assert.IsNotNull(receivedResponse, "Received response should not be null");
        Assert.AreEqual("queued", receivedResponse.status, "Response status should match");
        Assert.AreEqual(1, receivedResponse.queuePosition, "Queue position should match");
    }

    [UnityTest]
    public IEnumerator HandleWebSocketMessage_WithHeartbeat_ShouldRespondWithPong()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        var heartbeatMessage = MatchingProtocol.CreateHeartbeatMessage("player123");
        
        // Act
        _mockNetworkManager.TriggerWebSocketMessage(heartbeatMessage);
        yield return null; // Wait for message processing
        
        // Assert
        var sentMessages = _mockNetworkManager.GetSentWebSocketMessages();
        Assert.IsTrue(sentMessages.Exists(msg => msg.Contains("\"type\":\"pong\"")), 
            "Should send pong response to heartbeat");
    }

    [UnityTest]
    public IEnumerator HandleWebSocketMessage_WithInvalidJson_ShouldLogError()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        string invalidJson = "{ invalid json }";
        
        // Act & Assert
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to parse message"));
        
        _mockNetworkManager.TriggerWebSocketMessage(invalidJson);
        yield return null; // Wait for message processing
    }
    #endregion

    #region Error Handling Tests
    [UnityTest]
    public IEnumerator HandleWebSocketError_ShouldTriggerErrorEvent()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        bool errorEventTriggered = false;
        string errorCode = "";
        string errorMessage = "";
        
        _handler.OnNetworkError += (code, message) => {
            errorEventTriggered = true;
            errorCode = code;
            errorMessage = message;
        };
        
        // Act
        _mockNetworkManager.TriggerWebSocketError("Connection failed");
        yield return null; // Wait for event processing
        
        // Assert
        Assert.IsTrue(errorEventTriggered, "Network error event should be triggered");
        Assert.AreEqual("WEBSOCKET_ERROR", errorCode, "Error code should match");
        Assert.AreEqual("Connection failed", errorMessage, "Error message should match");
    }

    [UnityTest]
    public IEnumerator TimeoutManager_OnRequestTimeout_ShouldTriggerCancelEvent()
    {
        // Arrange
        yield return SetupConnectedHandler();
        
        bool cancelEventTriggered = false;
        string cancelledPlayerId = "";
        
        _handler.OnMatchingCancelled += (playerId) => {
            cancelEventTriggered = true;
            cancelledPlayerId = playerId;
        };
        
        // Act - Simulate timeout
        _handler.TimeoutManager.StartRequestTimeout("test-request", "player123", 0.1f); // 0.1 second timeout
        yield return new WaitForSeconds(0.2f); // Wait for timeout
        
        // Assert
        Assert.IsTrue(cancelEventTriggered, "Matching cancelled event should be triggered");
        Assert.AreEqual("player123", cancelledPlayerId, "Cancelled player ID should match");
    }
    #endregion

    #region Heartbeat Tests
    [Test]
    public void SendHeartbeat_WhenConnected_ShouldSucceed()
    {
        // Arrange - Mock as connected
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        _mockNetworkManager.SetSendHeartbeatResult(true);
        
        // Act
        bool result = _handler.SendHeartbeat("player123");
        
        // Assert
        Assert.IsTrue(result, "Heartbeat should succeed when connected");
        
        var sentHeartbeats = _mockNetworkManager.GetSentHeartbeats();
        Assert.IsTrue(sentHeartbeats.Contains("player123"), "Should send heartbeat for specified player");
    }

    [Test]
    public void SendHeartbeat_WhenDisconnected_ShouldFail()
    {
        // Arrange - Mock as disconnected
        _mockNetworkManager.SetWebSocketConnectionResult(false);
        
        // Act
        bool result = _handler.SendHeartbeat("player123");
        
        // Assert
        Assert.IsFalse(result, "Heartbeat should fail when disconnected");
    }
    #endregion

    #region Connection Quality Tests
    [Test]
    public void GetConnectionQuality_WhenInitialized_ShouldReturnQuality()
    {
        // Arrange
        var mockQuality = new WebSocketConnectionQuality
        {
            IsConnected = true,
            QualityScore = 0.9f,
            Status = "Good"
        };
        
        _mockNetworkManager.SetConnectionQuality(mockQuality);
        
        // Act
        var quality = _handler.GetConnectionQuality();
        
        // Assert
        Assert.IsNotNull(quality, "Connection quality should not be null");
        Assert.AreEqual(0.9f, quality.QualityScore, "Quality score should match");
        Assert.AreEqual("Good", quality.Status, "Quality status should match");
    }

    [Test]
    public void GetConnectionQuality_WhenNotInitialized_ShouldReturnDefaultQuality()
    {
        // Act
        var quality = _handler.GetConnectionQuality();
        
        // Assert
        Assert.IsNotNull(quality, "Connection quality should not be null");
        Assert.IsFalse(quality.IsConnected, "Should not be connected");
        Assert.AreEqual("Not initialized", quality.Status, "Status should indicate not initialized");
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 연결된 핸들러 설정
    /// </summary>
    private IEnumerator SetupConnectedHandler()
    {
        _mockNetworkManager.SetWebSocketInitializationResult(true);
        _mockNetworkManager.SetWebSocketConnectionResult(true);
        
        var initTask = _handler.InitializeAsync(_mockNetworkManager);
        yield return new WaitUntil(() => initTask.IsCompleted);
        
        Assert.IsTrue(_handler.IsInitialized, "Handler should be initialized for test");
        Assert.IsTrue(_handler.IsConnected, "Handler should be connected for test");
    }
    #endregion
}

/// <summary>
/// 테스트용 Mock NetworkManager
/// </summary>
public class MockNetworkManager : MonoBehaviour
{
    #region Mock State
    private bool _webSocketInitResult = true;
    private bool _webSocketConnectionResult = true;
    private bool _sendJoinQueueResult = true;
    private bool _sendRoomCreateResult = true;
    private bool _sendRoomJoinResult = true;
    private bool _sendCancelResult = true;
    private bool _sendHeartbeatResult = true;
    private WebSocketConnectionQuality _connectionQuality = new WebSocketConnectionQuality();
    
    // Test data tracking
    private System.Collections.Generic.List<string> _sentWebSocketMessages = new();
    private System.Collections.Generic.List<string> _sentHeartbeats = new();
    private MockJoinQueueRequest _lastJoinQueueRequest;
    private MockRoomCreateRequest _lastRoomCreateRequest;
    private MockRoomJoinRequest _lastRoomJoinRequest;
    
    // Events for testing
    public event Action<string> OnWebSocketMessage;
    public event Action<bool> OnConnectionChanged;
    public event Action<string> OnWebSocketError;
    #endregion

    #region Mock Configuration
    public void SetWebSocketInitializationResult(bool result) => _webSocketInitResult = result;
    public void SetWebSocketConnectionResult(bool result) => _webSocketConnectionResult = result;
    public void SetSendJoinQueueRequestResult(bool result) => _sendJoinQueueResult = result;
    public void SetSendRoomCreateRequestResult(bool result) => _sendRoomCreateResult = result;
    public void SetSendRoomJoinRequestResult(bool result) => _sendRoomJoinResult = result;
    public void SetSendMatchingCancelRequestResult(bool result) => _sendCancelResult = result;
    public void SetSendHeartbeatResult(bool result) => _sendHeartbeatResult = result;
    public void SetConnectionQuality(WebSocketConnectionQuality quality) => _connectionQuality = quality;
    #endregion

    #region Test Data Access
    public System.Collections.Generic.List<string> GetSentWebSocketMessages() => _sentWebSocketMessages;
    public System.Collections.Generic.List<string> GetSentHeartbeats() => _sentHeartbeats;
    public MockJoinQueueRequest GetLastJoinQueueRequest() => _lastJoinQueueRequest;
    public MockRoomCreateRequest GetLastRoomCreateRequest() => _lastRoomCreateRequest;
    public MockRoomJoinRequest GetLastRoomJoinRequest() => _lastRoomJoinRequest;
    #endregion

    #region Event Triggers
    public void TriggerWebSocketMessage(string message) => OnWebSocketMessage?.Invoke(message);
    public void TriggerConnectionChanged(bool connected) => OnConnectionChanged?.Invoke(connected);
    public void TriggerWebSocketError(string error) => OnWebSocketError?.Invoke(error);
    #endregion
}

#region Mock Data Structures
public class MockJoinQueueRequest
{
    public string playerId;
    public int playerCount;
    public string gameMode;
    public int betAmount;
}

public class MockRoomCreateRequest
{
    public string playerId;
    public int playerCount;
    public string gameMode;
    public int betAmount;
    public bool isPrivate;
}

public class MockRoomJoinRequest
{
    public string playerId;
    public string roomCode;
}
#endregion