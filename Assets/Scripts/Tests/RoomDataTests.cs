using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RoomData 단위 테스트
/// 방 데이터 구조, 플레이어 관리, 유효성 검증 기능 검증
/// </summary>
public class RoomDataTests
{
    private RoomData roomData;
    private string testRoomCode = "1234";
    private string testHostId = "host123";

    [SetUp]
    public void SetUp()
    {
        roomData = new RoomData(testRoomCode, testHostId, 4);
    }

    #region Constructor Tests

    [Test]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Assert
        Assert.AreEqual(testRoomCode, roomData.RoomCode);
        Assert.AreEqual(testHostId, roomData.HostPlayerId);
        Assert.AreEqual(4, roomData.MaxPlayers);
        Assert.AreEqual(RoomStatus.Waiting, roomData.Status);
        Assert.AreEqual(0, roomData.CurrentPlayerCount);
        Assert.IsFalse(roomData.IsExpired);
        Assert.IsTrue(roomData.ExpiresAt > DateTime.Now.AddMinutes(25)); // 30분 만료 확인
    }

    [Test]
    public void Constructor_WithInvalidMaxPlayers_ShouldClampToValidRange()
    {
        // Arrange & Act
        var roomWithLowMax = new RoomData("1111", "host1", 1);
        var roomWithHighMax = new RoomData("2222", "host2", 10);

        // Assert
        Assert.AreEqual(2, roomWithLowMax.MaxPlayers, "Should clamp minimum to 2");
        Assert.AreEqual(4, roomWithHighMax.MaxPlayers, "Should clamp maximum to 4");
    }

    [Test]
    public void DefaultConstructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var defaultRoom = new RoomData();

        // Assert
        Assert.IsNull(defaultRoom.RoomCode);
        Assert.IsNull(defaultRoom.HostPlayerId);
        Assert.AreEqual(0, defaultRoom.MaxPlayers);
        Assert.AreEqual(RoomStatus.Waiting, defaultRoom.Status);
        Assert.IsNotNull(defaultRoom.Players);
        Assert.AreEqual(0, defaultRoom.CurrentPlayerCount);
    }

    #endregion

    #region Player Management Tests

    [Test]
    public void AddPlayer_WithValidPlayer_ShouldSucceed()
    {
        // Arrange
        var player = new PlayerInfo("player1", "Player One", false);

        // Act
        bool result = roomData.AddPlayer(player);

        // Assert
        Assert.IsTrue(result, "Should successfully add player");
        Assert.AreEqual(1, roomData.CurrentPlayerCount, "Player count should increase");
        Assert.IsTrue(roomData.Players.Contains(player), "Player should be in list");
    }

    [Test]
    public void AddPlayer_WithNullPlayer_ShouldFail()
    {
        // Act
        bool result = roomData.AddPlayer(null);

        // Assert
        Assert.IsFalse(result, "Should fail to add null player");
        Assert.AreEqual(0, roomData.CurrentPlayerCount, "Player count should remain zero");
    }

    [Test]
    public void AddPlayer_WhenRoomFull_ShouldFail()
    {
        // Arrange - Fill the room
        for (int i = 0; i < 4; i++)
        {
            var player = new PlayerInfo($"player{i}", $"Player {i}", false);
            roomData.AddPlayer(player);
        }

        var extraPlayer = new PlayerInfo("extra", "Extra Player", false);

        // Act
        bool result = roomData.AddPlayer(extraPlayer);

        // Assert
        Assert.IsFalse(result, "Should fail to add player when room is full");
        Assert.AreEqual(4, roomData.CurrentPlayerCount, "Player count should remain at max");
        Assert.IsTrue(roomData.IsFull, "Room should be marked as full");
    }

    [Test]
    public void AddPlayer_WithDuplicateId_ShouldFail()
    {
        // Arrange
        var player1 = new PlayerInfo("player1", "Player One", false);
        var player2 = new PlayerInfo("player1", "Player One Duplicate", false);
        
        roomData.AddPlayer(player1);

        // Act
        bool result = roomData.AddPlayer(player2);

        // Assert
        Assert.IsFalse(result, "Should fail to add duplicate player ID");
        Assert.AreEqual(1, roomData.CurrentPlayerCount, "Player count should remain unchanged");
    }

    [Test]
    public void RemovePlayer_WithValidId_ShouldSucceed()
    {
        // Arrange
        var player = new PlayerInfo("player1", "Player One", false);
        roomData.AddPlayer(player);

        // Act
        bool result = roomData.RemovePlayer("player1");

        // Assert
        Assert.IsTrue(result, "Should successfully remove player");
        Assert.AreEqual(0, roomData.CurrentPlayerCount, "Player count should decrease");
        Assert.IsFalse(roomData.Players.Contains(player), "Player should not be in list");
    }

    [Test]
    public void RemovePlayer_WithInvalidId_ShouldFail()
    {
        // Act
        bool result = roomData.RemovePlayer("nonexistent");

        // Assert
        Assert.IsFalse(result, "Should fail to remove nonexistent player");
        Assert.AreEqual(0, roomData.CurrentPlayerCount, "Player count should remain unchanged");
    }

    [Test]
    public void RemovePlayer_WhenHostLeaves_ShouldTransferHost()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host Player", true);
        var player2 = new PlayerInfo("player2", "Player Two", false);
        
        roomData.AddPlayer(host);
        roomData.AddPlayer(player2);

        // Act
        roomData.RemovePlayer(testHostId);

        // Assert
        Assert.AreEqual("player2", roomData.HostPlayerId, "Host should transfer to remaining player");
        Assert.IsTrue(player2.IsHost, "Remaining player should be marked as host");
        Assert.IsFalse(host.IsHost, "Old host should no longer be host");
    }

    [Test]
    public void UpdatePlayer_WithValidInfo_ShouldSucceed()
    {
        // Arrange
        var originalPlayer = new PlayerInfo("player1", "Original Name", false);
        roomData.AddPlayer(originalPlayer);

        var updatedInfo = new PlayerInfo("player1", "Updated Name", true);

        // Act
        bool result = roomData.UpdatePlayer(updatedInfo);

        // Assert
        Assert.IsTrue(result, "Should successfully update player");
        var player = roomData.GetPlayer("player1");
        Assert.AreEqual("Updated Name", player.Nickname, "Nickname should be updated");
        Assert.IsTrue(player.IsHost, "Host status should be updated");
    }

    [Test]
    public void GetPlayer_WithValidId_ShouldReturnPlayer()
    {
        // Arrange
        var player = new PlayerInfo("player1", "Player One", false);
        roomData.AddPlayer(player);

        // Act
        var result = roomData.GetPlayer("player1");

        // Assert
        Assert.IsNotNull(result, "Should return player");
        Assert.AreEqual(player.PlayerId, result.PlayerId, "Should return correct player");
    }

    [Test]
    public void GetPlayer_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = roomData.GetPlayer("nonexistent");

        // Assert
        Assert.IsNull(result, "Should return null for nonexistent player");
    }

    #endregion

    #region Host Management Tests

    [Test]
    public void TransferHostTo_WithValidPlayer_ShouldSucceed()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true);
        var newHost = new PlayerInfo("newhost", "New Host", false);
        
        roomData.AddPlayer(host);
        roomData.AddPlayer(newHost);

        // Act
        bool result = roomData.TransferHostTo("newhost");

        // Assert
        Assert.IsTrue(result, "Should successfully transfer host");
        Assert.AreEqual("newhost", roomData.HostPlayerId, "Host ID should be updated");
        Assert.IsTrue(newHost.IsHost, "New player should be marked as host");
        Assert.IsFalse(host.IsHost, "Old host should no longer be host");
    }

    [Test]
    public void TransferHostTo_WithInvalidPlayer_ShouldFail()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true);
        roomData.AddPlayer(host);

        // Act
        bool result = roomData.TransferHostTo("nonexistent");

        // Assert
        Assert.IsFalse(result, "Should fail to transfer to nonexistent player");
        Assert.AreEqual(testHostId, roomData.HostPlayerId, "Host should remain unchanged");
    }

    [Test]
    public void TransferHostToNextPlayer_WithMultiplePlayers_ShouldTransferToFirst()
    {
        // Arrange
        var player1 = new PlayerInfo("player1", "Player One", false);
        var player2 = new PlayerInfo("player2", "Player Two", false);
        
        roomData.AddPlayer(player1);
        roomData.AddPlayer(player2);

        // Act
        roomData.TransferHostToNextPlayer();

        // Assert
        Assert.AreEqual("player1", roomData.HostPlayerId, "Should transfer to first player");
        Assert.IsTrue(player1.IsHost, "First player should be marked as host");
    }

    [Test]
    public void IsPlayerHost_ShouldReturnCorrectStatus()
    {
        // Assert
        Assert.IsTrue(roomData.IsPlayerHost(testHostId), "Host player should return true");
        Assert.IsFalse(roomData.IsPlayerHost("other"), "Non-host player should return false");
        Assert.IsFalse(roomData.IsPlayerHost(null), "Null ID should return false");
        Assert.IsFalse(roomData.IsPlayerHost(""), "Empty ID should return false");
    }

    #endregion

    #region Game Ready Tests

    [Test]
    public void AllPlayersReady_WithAllReady_ShouldReturnTrue()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true) { IsReady = true };
        var player2 = new PlayerInfo("player2", "Player Two", false) { IsReady = true };
        
        roomData.AddPlayer(host);
        roomData.AddPlayer(player2);

        // Act & Assert
        Assert.IsTrue(roomData.AllPlayersReady(), "Should return true when all players ready");
    }

    [Test]
    public void AllPlayersReady_WithSomeNotReady_ShouldReturnFalse()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true) { IsReady = true };
        var player2 = new PlayerInfo("player2", "Player Two", false) { IsReady = false };
        
        roomData.AddPlayer(host);
        roomData.AddPlayer(player2);

        // Act & Assert
        Assert.IsFalse(roomData.AllPlayersReady(), "Should return false when some players not ready");
    }

    [Test]
    public void AllPlayersReady_WithOnlyHost_ShouldReturnFalse()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true) { IsReady = true };
        roomData.AddPlayer(host);

        // Act & Assert
        Assert.IsFalse(roomData.AllPlayersReady(), "Should return false with only one player");
    }

    #endregion

    #region Computed Properties Tests

    [Test]
    public void ComputedProperties_ShouldReflectCurrentState()
    {
        // Initially empty
        Assert.IsTrue(roomData.IsEmpty, "Should be empty initially");
        Assert.IsFalse(roomData.IsFull, "Should not be full initially");
        Assert.IsFalse(roomData.HasMinimumPlayers, "Should not have minimum players initially");
        Assert.IsFalse(roomData.CanStart, "Should not be able to start initially");

        // Add minimum players
        var player1 = new PlayerInfo("p1", "Player 1", false);
        var player2 = new PlayerInfo("p2", "Player 2", false);
        roomData.AddPlayer(player1);
        roomData.AddPlayer(player2);

        Assert.IsFalse(roomData.IsEmpty, "Should not be empty with players");
        Assert.IsTrue(roomData.HasMinimumPlayers, "Should have minimum players");
        Assert.IsTrue(roomData.CanStart, "Should be able to start with minimum players");

        // Fill to capacity
        var player3 = new PlayerInfo("p3", "Player 3", false);
        var player4 = new PlayerInfo("p4", "Player 4", false);
        roomData.AddPlayer(player3);
        roomData.AddPlayer(player4);

        Assert.IsTrue(roomData.IsFull, "Should be full at capacity");
        Assert.AreEqual(4, roomData.CurrentPlayerCount, "Should have all players");
    }

    [Test]
    public void HostPlayer_ShouldReturnCorrectPlayer()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host Player", true);
        roomData.AddPlayer(host);

        // Act
        var hostPlayer = roomData.HostPlayer;

        // Assert
        Assert.IsNotNull(hostPlayer, "Should return host player");
        Assert.AreEqual(testHostId, hostPlayer.PlayerId, "Should return correct host player");
        Assert.IsTrue(hostPlayer.IsHost, "Returned player should be marked as host");
    }

    #endregion

    #region Validation Tests

    [Test]
    public void Validate_WithValidData_ShouldPass()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host", true);
        roomData.AddPlayer(host);

        // Act
        var result = roomData.Validate();

        // Assert
        Assert.IsTrue(result.IsValid, "Valid room should pass validation");
        Assert.AreEqual(0, result.Errors.Count, "Should have no errors");
    }

    [Test]
    public void Validate_WithInvalidRoomCode_ShouldFail()
    {
        // Arrange
        roomData.RoomCode = "invalid";

        // Act
        var result = roomData.Validate();

        // Assert
        Assert.IsFalse(result.IsValid, "Invalid room code should fail validation");
        Assert.IsTrue(result.Errors.Any(e => e.Contains("room code")), "Should mention room code error");
    }

    [Test]
    public void Validate_WithMissingHost_ShouldFail()
    {
        // Arrange
        roomData.HostPlayerId = "";

        // Act
        var result = roomData.Validate();

        // Assert
        Assert.IsFalse(result.IsValid, "Missing host should fail validation");
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Host player ID")), "Should mention host error");
    }

    [Test]
    public void Validate_WithHostNotInPlayerList_ShouldFail()
    {
        // Arrange - Host ID is set but no matching player in list
        var player = new PlayerInfo("other", "Other Player", false);
        roomData.AddPlayer(player);

        // Act
        var result = roomData.Validate();

        // Assert
        Assert.IsFalse(result.IsValid, "Host not in player list should fail validation");
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Host player not found")), "Should mention host not found");
    }

    #endregion

    #region JSON Serialization Tests

    [Test]
    public void ToJson_ShouldProduceValidJson()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host Player", true);
        roomData.AddPlayer(host);

        // Act
        string json = roomData.ToJson();

        // Assert
        Assert.IsNotNull(json, "Should produce JSON string");
        Assert.IsTrue(json.Contains(testRoomCode), "Should contain room code");
        Assert.IsTrue(json.Contains(testHostId), "Should contain host ID");
        Assert.IsTrue(json.Contains("Host Player"), "Should contain player nickname");
    }

    [Test]
    public void FromJson_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host Player", true);
        roomData.AddPlayer(host);
        string json = roomData.ToJson();

        // Act
        var deserialized = RoomData.FromJson(json);

        // Assert
        Assert.IsNotNull(deserialized, "Should deserialize successfully");
        Assert.AreEqual(roomData.RoomCode, deserialized.RoomCode, "Room code should match");
        Assert.AreEqual(roomData.HostPlayerId, deserialized.HostPlayerId, "Host ID should match");
        Assert.AreEqual(roomData.CurrentPlayerCount, deserialized.CurrentPlayerCount, "Player count should match");
    }

    [Test]
    public void FromJson_WithInvalidJson_ShouldReturnNull()
    {
        // Act
        var result = RoomData.FromJson("invalid json");

        // Assert
        Assert.IsNull(result, "Should return null for invalid JSON");
    }

    #endregion

    #region Utility Tests

    [Test]
    public void GetSummary_ShouldProvideReadableInfo()
    {
        // Arrange
        var host = new PlayerInfo(testHostId, "Host Player", true);
        roomData.AddPlayer(host);

        // Act
        string summary = roomData.GetSummary();

        // Assert
        Assert.IsNotNull(summary, "Should provide summary");
        Assert.IsTrue(summary.Contains(testRoomCode), "Should contain room code");
        Assert.IsTrue(summary.Contains("1/4"), "Should contain player count");
        Assert.IsTrue(summary.Contains("Waiting"), "Should contain status");
        Assert.IsTrue(summary.Contains("Host Player"), "Should contain host name");
    }

    #endregion
}

/// <summary>
/// PlayerInfo 단위 테스트
/// </summary>
public class PlayerInfoTests
{
    [Test]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var player = new PlayerInfo("player1", "Player One", true);

        // Assert
        Assert.AreEqual("player1", player.PlayerId);
        Assert.AreEqual("Player One", player.Nickname);
        Assert.IsTrue(player.IsHost);
        Assert.IsTrue(player.IsReady); // Host should be ready by default
        Assert.IsTrue(player.JoinedAt <= DateTime.Now);
    }

    [Test]
    public void DefaultConstructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var player = new PlayerInfo();

        // Assert
        Assert.IsNull(player.PlayerId);
        Assert.IsNull(player.Nickname);
        Assert.IsFalse(player.IsHost);
        Assert.IsFalse(player.IsReady);
        Assert.IsTrue(player.JoinedAt <= DateTime.Now);
    }

    [Test]
    public void ToJson_ShouldProduceValidJson()
    {
        // Arrange
        var player = new PlayerInfo("player1", "Player One", false);

        // Act
        string json = player.ToJson();

        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("player1"));
        Assert.IsTrue(json.Contains("Player One"));
    }

    [Test]
    public void FromJson_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var original = new PlayerInfo("player1", "Player One", false);
        string json = original.ToJson();

        // Act
        var deserialized = PlayerInfo.FromJson(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.PlayerId, deserialized.PlayerId);
        Assert.AreEqual(original.Nickname, deserialized.Nickname);
        Assert.AreEqual(original.IsHost, deserialized.IsHost);
    }

    [Test]
    public void FromJson_WithInvalidJson_ShouldReturnNull()
    {
        // Act & Assert
        Assert.IsNull(PlayerInfo.FromJson("invalid json"));
    }
}