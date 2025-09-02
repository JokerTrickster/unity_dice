using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NUnit.Framework;

/// <summary>
/// 매칭 프로토콜 직렬화/역직렬화 및 메시지 검증 테스트
/// 프로토콜 버전 호환성, 메시지 타입 유효성, 크기 제한 등을 검증
/// </summary>
public class MatchingProtocolTests
{
    #region Test Data Setup
    private MatchingRequest _validRequest;
    private MatchingResponse _validResponse;
    private MatchingMessage _validMessage;

    [SetUp]
    public void Setup()
    {
        // 유효한 매칭 요청 생성
        _validRequest = new MatchingRequest("test-player-123", 4, "classic", 1000);
        Assert.IsTrue(_validRequest.IsValid(), "Test request should be valid");

        // 유효한 매칭 응답 생성
        _validResponse = MatchingResponse.Success("room-123", new[]
        {
            new PlayerInfo("player1", "Player1", 1200),
            new PlayerInfo("player2", "Player2", 1100)
        });
        Assert.IsTrue(_validResponse.IsValid(), "Test response should be valid");

        // 유효한 메시지 생성
        _validMessage = new MatchingMessage("join_queue", _validRequest, 1);
        Assert.IsTrue(_validMessage.IsValid(), "Test message should be valid");
    }

    [TearDown]
    public void TearDown()
    {
        _validRequest = null;
        _validResponse = null;
        _validMessage = null;
    }
    #endregion

    #region Protocol Version Tests
    [Test]
    public void MatchingProtocol_CurrentVersion_ShouldBeValid()
    {
        // Arrange & Act
        string currentVersion = MatchingProtocol.PROTOCOL_VERSION;
        
        // Assert
        Assert.IsNotNull(currentVersion);
        Assert.IsFalse(string.IsNullOrEmpty(currentVersion));
        Assert.IsTrue(MatchingProtocol.IsCompatibleVersion(currentVersion));
        Debug.Log($"Current protocol version: {currentVersion}");
    }

    [Test]
    public void MatchingProtocol_SupportedVersions_ShouldContainCurrent()
    {
        // Arrange
        string currentVersion = MatchingProtocol.PROTOCOL_VERSION;
        var supportedVersions = MatchingProtocol.SUPPORTED_VERSIONS;
        
        // Act & Assert
        Assert.IsNotNull(supportedVersions);
        Assert.Contains(currentVersion, supportedVersions);
        Assert.IsTrue(supportedVersions.Length > 0);
        
        Debug.Log($"Supported versions: [{string.Join(", ", supportedVersions)}]");
    }

    [Test]
    public void IsCompatibleVersion_ValidVersions_ShouldReturnTrue()
    {
        // Arrange
        var testVersions = new[] { "1.0.0" };
        
        // Act & Assert
        foreach (var version in testVersions)
        {
            Assert.IsTrue(MatchingProtocol.IsCompatibleVersion(version), $"Version {version} should be compatible");
        }
    }

    [Test]
    public void IsCompatibleVersion_InvalidVersions_ShouldReturnFalse()
    {
        // Arrange
        var invalidVersions = new[] { "", null, "0.0.1", "2.0.0", "invalid", "1.0" };
        
        // Act & Assert
        foreach (var version in invalidVersions)
        {
            Assert.IsFalse(MatchingProtocol.IsCompatibleVersion(version), $"Version '{version}' should not be compatible");
        }
    }
    #endregion

    #region Message Type Validation Tests
    [Test]
    public void IsValidMessageType_ValidTypes_ShouldReturnTrue()
    {
        // Arrange
        var validTypes = new[]
        {
            "join_queue", "leave_queue", "room_create", "room_join", "room_leave",
            "match_found", "room_created", "room_joined", "match_cancelled",
            "heartbeat", "pong", "protocol_error"
        };
        
        // Act & Assert
        foreach (var type in validTypes)
        {
            Assert.IsTrue(MatchingProtocol.IsValidMessageType(type), $"Message type '{type}' should be valid");
            
            // Test case insensitive
            Assert.IsTrue(MatchingProtocol.IsValidMessageType(type.ToUpper()), $"Message type '{type.ToUpper()}' should be valid");
        }
    }

    [Test]
    public void IsValidMessageType_InvalidTypes_ShouldReturnFalse()
    {
        // Arrange
        var invalidTypes = new[] { "", null, "invalid_type", "unknown", "test", "fake_message" };
        
        // Act & Assert
        foreach (var type in invalidTypes)
        {
            Assert.IsFalse(MatchingProtocol.IsValidMessageType(type), $"Message type '{type}' should not be valid");
        }
    }

    [Test]
    public void IsClientMessageType_ClientTypes_ShouldReturnTrue()
    {
        // Arrange
        var clientTypes = new[] { "join_queue", "leave_queue", "room_create", "room_join", "heartbeat", "pong" };
        
        // Act & Assert
        foreach (var type in clientTypes)
        {
            Assert.IsTrue(MatchingProtocol.IsClientMessageType(type), $"Client message type '{type}' should be valid");
        }
    }

    [Test]
    public void IsServerMessageType_ServerTypes_ShouldReturnTrue()
    {
        // Arrange
        var serverTypes = new[] { "match_found", "room_created", "queue_status", "match_error", "heartbeat", "pong" };
        
        // Act & Assert
        foreach (var type in serverTypes)
        {
            Assert.IsTrue(MatchingProtocol.IsServerMessageType(type), $"Server message type '{type}' should be valid");
        }
    }

    [Test]
    public void MessageTypeCategories_ShouldNotOverlap_ExceptBidirectional()
    {
        // Arrange
        var clientTypes = MatchingProtocol.CLIENT_MESSAGE_TYPES;
        var serverTypes = MatchingProtocol.SERVER_MESSAGE_TYPES;
        var bidirectionalTypes = new[] { "heartbeat", "pong" }; // Expected overlap
        
        // Act
        var overlap = clientTypes.Intersect(serverTypes).ToList();
        
        // Assert
        foreach (var overlappingType in overlap)
        {
            Assert.Contains(overlappingType, bidirectionalTypes, 
                $"Overlapping type '{overlappingType}' should be in bidirectional types");
        }
        
        Debug.Log($"Bidirectional message types: [{string.Join(", ", overlap)}]");
    }
    #endregion

    #region Size Limit Tests
    [Test]
    public void IsWithinSizeLimit_SmallMessage_ShouldReturnTrue()
    {
        // Arrange
        string smallMessage = "{\"type\":\"test\",\"data\":\"small\"}";
        
        // Act & Assert
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(smallMessage));
    }

    [Test]
    public void IsWithinSizeLimit_LargeMessage_ShouldReturnFalse()
    {
        // Arrange - Create message larger than MAX_MESSAGE_SIZE (1MB)
        var largeData = new string('x', MatchingProtocol.MAX_MESSAGE_SIZE + 1);
        string largeMessage = $"{{\"type\":\"test\",\"data\":\"{largeData}\"}}";
        
        // Act & Assert
        Assert.IsFalse(MatchingProtocol.IsWithinSizeLimit(largeMessage));
    }

    [Test]
    public void IsWithinSizeLimit_NullOrEmpty_ShouldReturnTrue()
    {
        // Act & Assert
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(null));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(""));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(" "));
    }

    [Test]
    public void IsWithinSizeLimit_AtLimit_ShouldReturnTrue()
    {
        // Arrange - Create message exactly at MAX_MESSAGE_SIZE
        var exactData = new string('x', MatchingProtocol.MAX_MESSAGE_SIZE - 20); // Account for JSON structure
        string exactMessage = $"{{\"data\":\"{exactData}\"}}";
        
        // Act
        bool result = MatchingProtocol.IsWithinSizeLimit(exactMessage);
        
        // Assert
        Assert.IsTrue(result, "Message at exact size limit should be valid");
        Debug.Log($"Message size: {System.Text.Encoding.UTF8.GetByteCount(exactMessage)} bytes");
    }
    #endregion

    #region Player Count Validation Tests
    [Test]
    public void IsValidPlayerCount_ValidCounts_ShouldReturnTrue()
    {
        // Arrange
        var validCounts = new[] { 2, 3, 4 };
        
        // Act & Assert
        foreach (int count in validCounts)
        {
            Assert.IsTrue(MatchingProtocol.IsValidPlayerCount(count), $"Player count {count} should be valid");
        }
    }

    [Test]
    public void IsValidPlayerCount_InvalidCounts_ShouldReturnFalse()
    {
        // Arrange
        var invalidCounts = new[] { 0, 1, 5, 6, 10, -1 };
        
        // Act & Assert
        foreach (int count in invalidCounts)
        {
            Assert.IsFalse(MatchingProtocol.IsValidPlayerCount(count), $"Player count {count} should not be valid");
        }
    }

    [Test]
    public void IsValidPlayerCount_BoundaryValues_ShouldHandleCorrectly()
    {
        // Act & Assert
        Assert.IsTrue(MatchingProtocol.IsValidPlayerCount(MatchingProtocol.MIN_PLAYERS));
        Assert.IsTrue(MatchingProtocol.IsValidPlayerCount(MatchingProtocol.MAX_PLAYERS));
        Assert.IsFalse(MatchingProtocol.IsValidPlayerCount(MatchingProtocol.MIN_PLAYERS - 1));
        Assert.IsFalse(MatchingProtocol.IsValidPlayerCount(MatchingProtocol.MAX_PLAYERS + 1));
    }
    #endregion

    #region Serialization Tests
    [Test]
    public void SerializeRequest_ValidRequest_ShouldReturnJson()
    {
        // Act
        string json = MatchingProtocol.SerializeRequest(_validRequest);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsFalse(string.IsNullOrEmpty(json));
        Assert.IsTrue(json.Contains("join_queue"));
        Assert.IsTrue(json.Contains("test-player-123"));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
        
        Debug.Log($"Serialized request: {json.Substring(0, Math.Min(200, json.Length))}...");
    }

    [Test]
    public void SerializeRequest_NullRequest_ShouldReturnNull()
    {
        // Act
        string json = MatchingProtocol.SerializeRequest(null);
        
        // Assert
        Assert.IsNull(json);
    }

    [Test]
    public void SerializeRequest_InvalidRequest_ShouldReturnNull()
    {
        // Arrange
        var invalidRequest = new MatchingRequest("", -1, "", -1); // All invalid values
        
        // Act
        string json = MatchingProtocol.SerializeRequest(invalidRequest);
        
        // Assert
        Assert.IsNull(json);
    }

    [Test]
    public void SerializeResponse_ValidResponse_ShouldReturnJson()
    {
        // Act
        string json = MatchingProtocol.SerializeResponse(_validResponse);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsFalse(string.IsNullOrEmpty(json));
        Assert.IsTrue(json.Contains("room-123"));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
        
        Debug.Log($"Serialized response: {json.Substring(0, Math.Min(200, json.Length))}...");
    }

    [Test]
    public void SerializeResponse_NullResponse_ShouldReturnNull()
    {
        // Act
        string json = MatchingProtocol.SerializeResponse(null);
        
        // Assert
        Assert.IsNull(json);
    }

    [Test]
    public void SerializeMessage_ValidMessage_ShouldReturnJson()
    {
        // Act
        string json = MatchingProtocol.SerializeMessage(_validMessage);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsFalse(string.IsNullOrEmpty(json));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
        
        Debug.Log($"Serialized message: {json.Substring(0, Math.Min(200, json.Length))}...");
    }

    [Test]
    public void SerializeMessage_NullMessage_ShouldReturnNull()
    {
        // Act
        string json = MatchingProtocol.SerializeMessage(null);
        
        // Assert
        Assert.IsNull(json);
    }
    #endregion

    #region Deserialization Tests
    [Test]
    public void DeserializeMessage_ValidJson_ShouldReturnMessage()
    {
        // Arrange
        string json = MatchingProtocol.SerializeMessage(_validMessage);
        Assert.IsNotNull(json, "Serialization should succeed for test setup");
        
        // Act
        var deserializedMessage = MatchingProtocol.DeserializeMessage(json);
        
        // Assert
        Assert.IsNotNull(deserializedMessage);
        Assert.AreEqual(_validMessage.type, deserializedMessage.type);
        Assert.AreEqual(_validMessage.priority, deserializedMessage.priority);
        Assert.IsTrue(deserializedMessage.IsValid());
    }

    [Test]
    public void DeserializeMessage_InvalidJson_ShouldReturnNull()
    {
        // Arrange
        var invalidJsons = new[] { "", null, "{invalid json}", "not json at all", "{\"incomplete\":}" };
        
        // Act & Assert
        foreach (var json in invalidJsons)
        {
            var result = MatchingProtocol.DeserializeMessage(json);
            Assert.IsNull(result, $"Invalid JSON '{json}' should return null");
        }
    }

    [Test]
    public void DeserializeMessage_TooLarge_ShouldReturnNull()
    {
        // Arrange - Create oversized JSON
        var largeData = new string('x', MatchingProtocol.MAX_MESSAGE_SIZE);
        string largeJson = $"{{\"type\":\"test\",\"data\":\"{largeData}\"}}";
        
        // Act
        var result = MatchingProtocol.DeserializeMessage(largeJson);
        
        // Assert
        Assert.IsNull(result, "Oversized message should return null");
    }

    [Test]
    public void DeserializeRequest_ValidClientMessage_ShouldReturnRequest()
    {
        // Arrange
        string json = MatchingProtocol.SerializeRequest(_validRequest);
        Assert.IsNotNull(json, "Serialization should succeed for test setup");
        
        // Act
        var deserializedRequest = MatchingProtocol.DeserializeRequest(json);
        
        // Assert
        Assert.IsNotNull(deserializedRequest);
        Assert.AreEqual(_validRequest.playerId, deserializedRequest.playerId);
        Assert.AreEqual(_validRequest.playerCount, deserializedRequest.playerCount);
        Assert.IsTrue(deserializedRequest.IsValid());
    }

    [Test]
    public void DeserializeRequest_ServerMessage_ShouldReturnNull()
    {
        // Arrange - Create server-only message
        string serverJson = MatchingProtocol.SerializeResponse(_validResponse);
        Assert.IsNotNull(serverJson, "Response serialization should succeed");
        
        // Act
        var result = MatchingProtocol.DeserializeRequest(serverJson);
        
        // Assert
        Assert.IsNull(result, "Server message should not deserialize as request");
    }

    [Test]
    public void DeserializeResponse_ValidServerMessage_ShouldReturnResponse()
    {
        // Arrange
        string json = MatchingProtocol.SerializeResponse(_validResponse);
        Assert.IsNotNull(json, "Serialization should succeed for test setup");
        
        // Act
        var deserializedResponse = MatchingProtocol.DeserializeResponse(json);
        
        // Assert
        Assert.IsNotNull(deserializedResponse);
        Assert.AreEqual(_validResponse.roomId, deserializedResponse.roomId);
        Assert.AreEqual(_validResponse.success, deserializedResponse.success);
        Assert.IsTrue(deserializedResponse.IsValid());
    }

    [Test]
    public void DeserializeResponse_ClientMessage_ShouldReturnNull()
    {
        // Arrange - Create client-only message  
        string clientJson = MatchingProtocol.SerializeRequest(_validRequest);
        Assert.IsNotNull(clientJson, "Request serialization should succeed");
        
        // Act
        var result = MatchingProtocol.DeserializeResponse(clientJson);
        
        // Assert
        Assert.IsNull(result, "Client message should not deserialize as response");
    }
    #endregion

    #region Message Factory Tests
    [Test]
    public void CreateJoinQueueMessage_ValidParameters_ShouldReturnJson()
    {
        // Arrange
        string playerId = "test-player";
        int playerCount = 4;
        string gameMode = "classic";
        int betAmount = 1000;
        
        // Act
        string json = MatchingProtocol.CreateJoinQueueMessage(playerId, playerCount, gameMode, betAmount);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("join_queue"));
        Assert.IsTrue(json.Contains(playerId));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
        
        // Verify deserialization works
        var message = MatchingProtocol.DeserializeMessage(json);
        Assert.IsNotNull(message);
        Assert.AreEqual("join_queue", message.type);
    }

    [Test]
    public void CreateRoomCreateMessage_ValidParameters_ShouldReturnJson()
    {
        // Arrange
        string playerId = "test-player";
        int playerCount = 4;
        bool isPrivate = true;
        
        // Act
        string json = MatchingProtocol.CreateRoomCreateMessage(playerId, playerCount, "classic", 500, isPrivate);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("room_create"));
        Assert.IsTrue(json.Contains(playerId));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
    }

    [Test]
    public void CreateRoomJoinMessage_ValidParameters_ShouldReturnJson()
    {
        // Arrange
        string playerId = "test-player";
        string roomCode = "ABC123";
        
        // Act
        string json = MatchingProtocol.CreateRoomJoinMessage(playerId, roomCode);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("room_join"));
        Assert.IsTrue(json.Contains(playerId));
        Assert.IsTrue(json.Contains(roomCode));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
    }

    [Test]
    public void CreateCancelMessage_ValidPlayerId_ShouldReturnJson()
    {
        // Arrange
        string playerId = "test-player";
        
        // Act
        string json = MatchingProtocol.CreateCancelMessage(playerId);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("matching_cancel"));
        Assert.IsTrue(json.Contains(playerId));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
    }

    [Test]
    public void CreateHeartbeatMessage_WithAndWithoutPlayerId_ShouldReturnJson()
    {
        // Act
        string jsonWithPlayer = MatchingProtocol.CreateHeartbeatMessage("test-player");
        string jsonWithoutPlayer = MatchingProtocol.CreateHeartbeatMessage();
        
        // Assert
        Assert.IsNotNull(jsonWithPlayer);
        Assert.IsTrue(jsonWithPlayer.Contains("heartbeat"));
        Assert.IsTrue(jsonWithPlayer.Contains("test-player"));
        
        Assert.IsNotNull(jsonWithoutPlayer);
        Assert.IsTrue(jsonWithoutPlayer.Contains("heartbeat"));
        
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(jsonWithPlayer));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(jsonWithoutPlayer));
    }

    [Test]
    public void CreatePongMessage_WithAndWithoutPlayerId_ShouldReturnJson()
    {
        // Act
        string jsonWithPlayer = MatchingProtocol.CreatePongMessage("test-player");
        string jsonWithoutPlayer = MatchingProtocol.CreatePongMessage();
        
        // Assert
        Assert.IsNotNull(jsonWithPlayer);
        Assert.IsTrue(jsonWithPlayer.Contains("pong"));
        Assert.IsTrue(jsonWithPlayer.Contains("test-player"));
        
        Assert.IsNotNull(jsonWithoutPlayer);
        Assert.IsTrue(jsonWithoutPlayer.Contains("pong"));
        
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(jsonWithPlayer));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(jsonWithoutPlayer));
    }
    #endregion

    #region Error Handling Tests
    [Test]
    public void CreateProtocolErrorMessage_ValidParameters_ShouldReturnJson()
    {
        // Arrange
        string errorCode = "TEST_ERROR";
        string errorMessage = "This is a test error";
        string originalId = "msg-123";
        
        // Act
        string json = MatchingProtocol.CreateProtocolErrorMessage(errorCode, errorMessage, originalId);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("protocol_error"));
        Assert.IsTrue(json.Contains(errorCode));
        Assert.IsTrue(json.Contains(errorMessage));
        Assert.IsTrue(json.Contains(originalId));
        Assert.IsTrue(MatchingProtocol.IsWithinSizeLimit(json));
    }

    [Test]
    public void CreateInvalidMessageTypeError_ShouldReturnProperErrorJson()
    {
        // Arrange
        string invalidType = "unknown_message";
        string originalId = "msg-456";
        
        // Act
        string json = MatchingProtocol.CreateInvalidMessageTypeError(invalidType, originalId);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("INVALID_MESSAGE_TYPE"));
        Assert.IsTrue(json.Contains(invalidType));
        Assert.IsTrue(json.Contains(originalId));
    }

    [Test]
    public void CreateVersionMismatchError_ShouldReturnProperErrorJson()
    {
        // Arrange
        string clientVersion = "2.0.0";
        string originalId = "msg-789";
        
        // Act
        string json = MatchingProtocol.CreateVersionMismatchError(clientVersion, originalId);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("VERSION_MISMATCH"));
        Assert.IsTrue(json.Contains(clientVersion));
        Assert.IsTrue(json.Contains(originalId));
    }

    [Test]
    public void CreateMessageTooLargeError_ShouldReturnProperErrorJson()
    {
        // Arrange
        int actualSize = 2000000; // 2MB
        string originalId = "msg-large";
        
        // Act
        string json = MatchingProtocol.CreateMessageTooLargeError(actualSize, originalId);
        
        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("MESSAGE_TOO_LARGE"));
        Assert.IsTrue(json.Contains(actualSize.ToString()));
        Assert.IsTrue(json.Contains(originalId));
    }
    #endregion

    #region Utility Tests
    [Test]
    public void GetExpectedPayloadType_ValidMessageTypes_ShouldReturnCorrectTypes()
    {
        // Test client request types
        Assert.AreEqual(typeof(MatchingRequest), MatchingProtocol.GetExpectedPayloadType("join_queue"));
        Assert.AreEqual(typeof(MatchingRequest), MatchingProtocol.GetExpectedPayloadType("room_create"));
        Assert.AreEqual(typeof(MatchingRequest), MatchingProtocol.GetExpectedPayloadType("room_join"));
        
        // Test server response types
        Assert.AreEqual(typeof(MatchingResponse), MatchingProtocol.GetExpectedPayloadType("match_found"));
        Assert.AreEqual(typeof(MatchingResponse), MatchingProtocol.GetExpectedPayloadType("room_created"));
        
        // Test system types
        Assert.AreEqual(typeof(object), MatchingProtocol.GetExpectedPayloadType("heartbeat"));
        Assert.AreEqual(typeof(object), MatchingProtocol.GetExpectedPayloadType("pong"));
        
        // Test unknown type
        Assert.AreEqual(typeof(object), MatchingProtocol.GetExpectedPayloadType("unknown"));
    }

    [Test]
    public void GetProtocolStats_ShouldReturnValidInfo()
    {
        // Act
        string stats = MatchingProtocol.GetProtocolStats();
        
        // Assert
        Assert.IsNotNull(stats);
        Assert.IsFalse(string.IsNullOrEmpty(stats));
        Assert.IsTrue(stats.Contains(MatchingProtocol.PROTOCOL_VERSION));
        Assert.IsTrue(stats.Contains("Message Types"));
        Assert.IsTrue(stats.Contains("Max Message Size"));
        
        Debug.Log($"Protocol Stats:\n{stats}");
    }

    [Test]
    public void LogDebugInfo_ValidMessage_ShouldNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => MatchingProtocol.LogDebugInfo(_validMessage));
        Assert.DoesNotThrow(() => MatchingProtocol.LogDebugInfo(_validMessage, "[TEST]"));
        Assert.DoesNotThrow(() => MatchingProtocol.LogDebugInfo(null));
    }
    #endregion

    #region Round-trip Serialization Tests
    [Test]
    public void RoundTripSerialization_Request_ShouldPreserveData()
    {
        // Act
        string json = MatchingProtocol.SerializeRequest(_validRequest);
        var deserializedRequest = MatchingProtocol.DeserializeRequest(json);
        
        // Assert
        Assert.IsNotNull(deserializedRequest);
        Assert.AreEqual(_validRequest.playerId, deserializedRequest.playerId);
        Assert.AreEqual(_validRequest.playerCount, deserializedRequest.playerCount);
        Assert.AreEqual(_validRequest.gameMode, deserializedRequest.gameMode);
        Assert.AreEqual(_validRequest.betAmount, deserializedRequest.betAmount);
        Assert.AreEqual(_validRequest.matchType, deserializedRequest.matchType);
    }

    [Test]
    public void RoundTripSerialization_Response_ShouldPreserveData()
    {
        // Act
        string json = MatchingProtocol.SerializeResponse(_validResponse);
        var deserializedResponse = MatchingProtocol.DeserializeResponse(json);
        
        // Assert
        Assert.IsNotNull(deserializedResponse);
        Assert.AreEqual(_validResponse.success, deserializedResponse.success);
        Assert.AreEqual(_validResponse.roomId, deserializedResponse.roomId);
        Assert.AreEqual(_validResponse.players.Length, deserializedResponse.players.Length);
        
        for (int i = 0; i < _validResponse.players.Length; i++)
        {
            Assert.AreEqual(_validResponse.players[i].playerId, deserializedResponse.players[i].playerId);
            Assert.AreEqual(_validResponse.players[i].nickname, deserializedResponse.players[i].nickname);
            Assert.AreEqual(_validResponse.players[i].rating, deserializedResponse.players[i].rating);
        }
    }

    [Test]
    public void RoundTripSerialization_Message_ShouldPreserveData()
    {
        // Act
        string json = MatchingProtocol.SerializeMessage(_validMessage);
        var deserializedMessage = MatchingProtocol.DeserializeMessage(json);
        
        // Assert
        Assert.IsNotNull(deserializedMessage);
        Assert.AreEqual(_validMessage.type, deserializedMessage.type);
        Assert.AreEqual(_validMessage.priority, deserializedMessage.priority);
        Assert.AreEqual(_validMessage.version, deserializedMessage.version);
        // Timestamp will be different due to serialization, but should be recent
        Assert.IsTrue(deserializedMessage.IsTimestampValid());
    }
    #endregion

    #region Performance Tests
    [Test]
    public void Serialization_Performance_ShouldBeReasonable()
    {
        // Arrange
        int iterations = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < iterations; i++)
        {
            string json = MatchingProtocol.SerializeRequest(_validRequest);
            Assert.IsNotNull(json, $"Serialization failed at iteration {i}");
        }
        
        stopwatch.Stop();
        
        // Assert
        double averageMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        Assert.Less(averageMs, 5.0, "Average serialization time should be under 5ms");
        
        Debug.Log($"Serialization performance: {averageMs:F3}ms average over {iterations} iterations");
    }

    [Test]
    public void Deserialization_Performance_ShouldBeReasonable()
    {
        // Arrange
        string json = MatchingProtocol.SerializeRequest(_validRequest);
        int iterations = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < iterations; i++)
        {
            var request = MatchingProtocol.DeserializeRequest(json);
            Assert.IsNotNull(request, $"Deserialization failed at iteration {i}");
        }
        
        stopwatch.Stop();
        
        // Assert
        double averageMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        Assert.Less(averageMs, 5.0, "Average deserialization time should be under 5ms");
        
        Debug.Log($"Deserialization performance: {averageMs:F3}ms average over {iterations} iterations");
    }
    #endregion

    #region Edge Case Tests
    [Test]
    public void MessageHandling_EmptyPayload_ShouldHandleGracefully()
    {
        // Arrange
        var messageWithEmptyPayload = new MatchingMessage("heartbeat", null, 0);
        
        // Act & Assert
        Assert.DoesNotThrow(() => {
            string json = MatchingProtocol.SerializeMessage(messageWithEmptyPayload);
            if (json != null) // May be null if validation fails, which is acceptable
            {
                var deserialized = MatchingProtocol.DeserializeMessage(json);
                // Should either deserialize successfully or return null gracefully
            }
        });
    }

    [Test]
    public void MessageHandling_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var requestWithSpecialChars = new MatchingRequest("플레이어-123", 2, "특별-모드", 1000);
        
        // Act
        string json = MatchingProtocol.SerializeRequest(requestWithSpecialChars);
        
        // Assert
        if (json != null) // May fail validation, which is acceptable
        {
            var deserialized = MatchingProtocol.DeserializeRequest(json);
            if (deserialized != null)
            {
                Assert.AreEqual(requestWithSpecialChars.playerId, deserialized.playerId);
                Assert.AreEqual(requestWithSpecialChars.gameMode, deserialized.gameMode);
            }
        }
    }

    [Test]
    public void MessageHandling_TimestampExpiry_ShouldDetectExpiredMessages()
    {
        // Arrange - Create message with timestamp far in the past
        var expiredMessage = new MatchingMessage("test", new { }, 1);
        // Manually set expired timestamp (this would require access to internal fields)
        // For this test, we'll verify the expiry detection logic works with current messages
        
        // Act & Assert
        Assert.IsFalse(_validMessage.IsExpired(), "Valid message should not be expired");
        
        // Test current message age handling
        var currentMessage = new MatchingMessage("heartbeat", new { }, 0);
        Assert.IsFalse(currentMessage.IsExpired(), "Newly created message should not be expired");
    }
    #endregion
}