using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// HostManager 단위 테스트
/// 방장 권한 관리, 게임 시작, 권한 위임 기능 검증
/// </summary>
public class HostManagerTests
{
    private HostManager hostManager;
    private RoomData testRoom;
    private string testPlayerId = "test_player_123";
    private string otherPlayerId = "other_player_456";

    [SetUp]
    public void SetUp()
    {
        hostManager = new HostManager(testPlayerId);
        
        // 테스트용 방 데이터 생성
        testRoom = new RoomData("1234", testPlayerId, 4);
        var hostPlayer = new PlayerInfo(testPlayerId, "Test Host", true);
        var otherPlayer = new PlayerInfo(otherPlayerId, "Other Player", false);
        
        testRoom.AddPlayer(hostPlayer);
        testRoom.AddPlayer(otherPlayer);
        
        hostManager.SetRoom(testRoom);
    }

    [TearDown]
    public void TearDown()
    {
        hostManager?.Cleanup();
    }

    #region Initialization Tests

    [Test]
    public void Constructor_WithValidPlayerId_ShouldInitialize()
    {
        // Arrange & Act
        var manager = new HostManager("player123");

        // Assert
        Assert.IsFalse(manager.IsHost, "Should not be host initially");
        Assert.IsNull(manager.CurrentHostId, "Should have no current host initially");
        Assert.IsFalse(manager.CanStartGame, "Should not be able to start game initially");

        manager.Cleanup();
    }

    [Test]
    public void SetRoom_WithHostRoom_ShouldBecomeHost()
    {
        // Arrange
        var manager = new HostManager(testPlayerId);
        bool hostChangedEventFired = false;
        bool privilegesChangedEventFired = false;
        
        manager.OnHostChanged += (hostId) => hostChangedEventFired = true;
        manager.OnHostPrivilegesChanged += (isHost) => privilegesChangedEventFired = isHost;

        // Act
        manager.SetRoom(testRoom);

        // Assert
        Assert.IsTrue(manager.IsHost, "Should become host");
        Assert.AreEqual(testPlayerId, manager.CurrentHostId, "Current host should be test player");
        Assert.IsTrue(manager.CanStartGame, "Should be able to start game as host");
        Assert.IsTrue(hostChangedEventFired, "Host changed event should fire");
        Assert.IsTrue(privilegesChangedEventFired, "Privileges changed event should fire");

        manager.Cleanup();
    }

    [Test]
    public void SetRoom_WithNonHostRoom_ShouldNotBecomeHost()
    {
        // Arrange
        var nonHostRoom = new RoomData("5678", otherPlayerId, 3);
        var hostPlayer = new PlayerInfo(otherPlayerId, "Other Host", true);
        var nonHostPlayer = new PlayerInfo(testPlayerId, "Non Host", false);
        
        nonHostRoom.AddPlayer(hostPlayer);
        nonHostRoom.AddPlayer(nonHostPlayer);

        var manager = new HostManager(testPlayerId);

        // Act
        manager.SetRoom(nonHostRoom);

        // Assert
        Assert.IsFalse(manager.IsHost, "Should not become host");
        Assert.AreEqual(otherPlayerId, manager.CurrentHostId, "Current host should be other player");
        Assert.IsFalse(manager.CanStartGame, "Should not be able to start game");

        manager.Cleanup();
    }

    #endregion

    #region Host Transfer Tests

    [Test]
    public void TransferHostTo_WithValidTarget_ShouldSucceed()
    {
        // Arrange
        bool hostChangedEventFired = false;
        string newHostId = null;
        
        hostManager.OnHostChanged += (hostId) =>
        {
            hostChangedEventFired = true;
            newHostId = hostId;
        };

        // Act
        bool result = hostManager.TransferHostTo(otherPlayerId);

        // Assert
        Assert.IsTrue(result, "Should successfully transfer host");
        Assert.IsFalse(hostManager.IsHost, "Original host should lose host status");
        Assert.AreEqual(otherPlayerId, hostManager.CurrentHostId, "Host should be transferred to target");
        Assert.IsTrue(hostChangedEventFired, "Host changed event should fire");
        Assert.AreEqual(otherPlayerId, newHostId, "Event should contain new host ID");
    }

    [Test]
    public void TransferHostTo_WithInvalidTarget_ShouldFail()
    {
        // Act
        bool result = hostManager.TransferHostTo("nonexistent_player");

        // Assert
        Assert.IsFalse(result, "Should fail to transfer to nonexistent player");
        Assert.IsTrue(hostManager.IsHost, "Original host should retain host status");
        Assert.AreEqual(testPlayerId, hostManager.CurrentHostId, "Host should remain unchanged");
    }

    [Test]
    public void TransferHostTo_ToSelf_ShouldFail()
    {
        // Act
        bool result = hostManager.TransferHostTo(testPlayerId);

        // Assert
        Assert.IsFalse(result, "Should fail to transfer host to self");
        Assert.IsTrue(hostManager.IsHost, "Should remain host");
    }

    [Test]
    public void TransferHostTo_WithoutHostPermission_ShouldFail()
    {
        // Arrange - Transfer host to make current player non-host
        hostManager.TransferHostTo(otherPlayerId);

        // Act
        bool result = hostManager.TransferHostTo(testPlayerId);

        // Assert
        Assert.IsFalse(result, "Non-host should not be able to transfer host");
    }

    [Test]
    public void HandleHostDisconnection_ShouldTransferToNextPlayer()
    {
        // Arrange
        bool hostChangedEventFired = false;
        hostManager.OnHostChanged += (hostId) => hostChangedEventFired = true;

        // Act
        hostManager.HandleHostDisconnection();

        // Assert
        Assert.IsTrue(hostChangedEventFired, "Host changed event should fire");
        Assert.AreNotEqual(testPlayerId, hostManager.CurrentHostId, "Host should be transferred away");
    }

    #endregion

    #region Game Start Tests

    [Test]
    public void RequestStartGame_AsHost_ShouldSucceed()
    {
        // Arrange
        bool gameStartRequestedEventFired = false;
        string requestedRoomCode = null;
        
        hostManager.OnGameStartRequested += (roomCode) =>
        {
            gameStartRequestedEventFired = true;
            requestedRoomCode = roomCode;
        };

        // Act
        hostManager.RequestStartGame();

        // Assert
        Assert.IsTrue(gameStartRequestedEventFired, "Game start requested event should fire");
        Assert.AreEqual(testRoom.RoomCode, requestedRoomCode, "Should request start for correct room");
    }

    [Test]
    public void RequestStartGame_AsNonHost_ShouldFail()
    {
        // Arrange - Transfer host away
        hostManager.TransferHostTo(otherPlayerId);
        
        bool gameStartFailedEventFired = false;
        string failureReason = null;
        
        hostManager.OnGameStartFailed += (reason) =>
        {
            gameStartFailedEventFired = true;
            failureReason = reason;
        };

        // Act
        hostManager.RequestStartGame();

        // Assert
        Assert.IsTrue(gameStartFailedEventFired, "Game start failed event should fire");
        Assert.IsNotNull(failureReason, "Should provide failure reason");
        Assert.IsTrue(failureReason.Contains("방장"), "Should mention host requirement");
    }

    [Test]
    public void RequestStartGame_WithInsufficientPlayers_ShouldFail()
    {
        // Arrange - Remove other player to have only 1 player
        testRoom.RemovePlayer(otherPlayerId);
        
        bool gameStartFailedEventFired = false;
        hostManager.OnGameStartFailed += (reason) => gameStartFailedEventFired = true;

        // Act
        hostManager.RequestStartGame();

        // Assert
        Assert.IsTrue(gameStartFailedEventFired, "Should fail with insufficient players");
    }

    [Test]
    public void RequestStartGame_WithWrongRoomStatus_ShouldFail()
    {
        // Arrange - Set room status to non-waiting
        testRoom.Status = RoomStatus.Starting;
        
        bool gameStartFailedEventFired = false;
        hostManager.OnGameStartFailed += (reason) => gameStartFailedEventFired = true;

        // Act
        hostManager.RequestStartGame();

        // Assert
        Assert.IsTrue(gameStartFailedEventFired, "Should fail with wrong room status");
    }

    [Test]
    public void ValidateGameStart_WithValidConditions_ShouldPass()
    {
        // Act
        var result = hostManager.ValidateGameStart();

        // Assert
        Assert.IsTrue(result.IsValid, "Should pass validation with valid conditions");
        Assert.AreEqual(0, result.Errors.Count, "Should have no errors");
    }

    [Test]
    public void ValidateGameStart_WithInvalidConditions_ShouldFail()
    {
        // Arrange - Create invalid conditions
        testRoom.RemovePlayer(otherPlayerId); // Insufficient players
        testRoom.Status = RoomStatus.InGame; // Wrong status
        
        // Act
        var result = hostManager.ValidateGameStart();

        // Assert
        Assert.IsFalse(result.IsValid, "Should fail validation");
        Assert.IsTrue(result.Errors.Count > 0, "Should have errors");
        Assert.IsTrue(result.Errors.Exists(e => e.Contains("players")), "Should mention player error");
        Assert.IsTrue(result.Errors.Exists(e => e.Contains("status")), "Should mention status error");
    }

    #endregion

    #region Room Configuration Tests

    [Test]
    public void UpdateRoomSettings_AsHost_ShouldSucceed()
    {
        // Act
        bool result = hostManager.UpdateRoomSettings(3);

        // Assert
        Assert.IsTrue(result, "Should successfully update room settings");
        Assert.AreEqual(3, testRoom.MaxPlayers, "Max players should be updated");
    }

    [Test]
    public void UpdateRoomSettings_AsNonHost_ShouldFail()
    {
        // Arrange - Transfer host away
        hostManager.TransferHostTo(otherPlayerId);

        // Act
        bool result = hostManager.UpdateRoomSettings(3);

        // Assert
        Assert.IsFalse(result, "Non-host should not be able to update settings");
        Assert.AreEqual(4, testRoom.MaxPlayers, "Max players should remain unchanged");
    }

    [Test]
    public void UpdateRoomSettings_BelowCurrentPlayerCount_ShouldFail()
    {
        // Act - Try to set max players below current count
        bool result = hostManager.UpdateRoomSettings(1);

        // Assert
        Assert.IsFalse(result, "Should fail when setting max below current count");
        Assert.AreEqual(4, testRoom.MaxPlayers, "Max players should remain unchanged");
    }

    [Test]
    public void KickPlayer_AsHost_ShouldSucceed()
    {
        // Act
        bool result = hostManager.KickPlayer(otherPlayerId);

        // Assert
        Assert.IsTrue(result, "Should successfully kick player");
        Assert.IsNull(testRoom.GetPlayer(otherPlayerId), "Player should be removed from room");
        Assert.AreEqual(1, testRoom.CurrentPlayerCount, "Player count should decrease");
    }

    [Test]
    public void KickPlayer_AsNonHost_ShouldFail()
    {
        // Arrange - Transfer host away
        hostManager.TransferHostTo(otherPlayerId);

        // Act
        bool result = hostManager.KickPlayer(testPlayerId);

        // Assert
        Assert.IsFalse(result, "Non-host should not be able to kick players");
        Assert.IsNotNull(testRoom.GetPlayer(testPlayerId), "Player should remain in room");
    }

    [Test]
    public void KickPlayer_Self_ShouldFail()
    {
        // Act
        bool result = hostManager.KickPlayer(testPlayerId);

        // Assert
        Assert.IsFalse(result, "Should not be able to kick self");
        Assert.IsNotNull(testRoom.GetPlayer(testPlayerId), "Player should remain in room");
    }

    [Test]
    public void KickPlayer_NonexistentPlayer_ShouldFail()
    {
        // Act
        bool result = hostManager.KickPlayer("nonexistent");

        // Assert
        Assert.IsFalse(result, "Should fail to kick nonexistent player");
    }

    #endregion

    #region Permission Tests

    [Test]
    public void PermissionProperties_AsHost_ShouldReturnTrue()
    {
        // Assert
        Assert.IsTrue(hostManager.CanStartGame, "Host should be able to start game");
        Assert.IsTrue(hostManager.CanModifyRoom, "Host should be able to modify room");
        Assert.IsTrue(hostManager.CanKickPlayers, "Host should be able to kick players");
        Assert.IsTrue(hostManager.CanTransferHost, "Host should be able to transfer host");
    }

    [Test]
    public void PermissionProperties_AsNonHost_ShouldReturnFalse()
    {
        // Arrange - Transfer host away
        hostManager.TransferHostTo(otherPlayerId);

        // Assert
        Assert.IsFalse(hostManager.CanStartGame, "Non-host should not be able to start game");
        Assert.IsFalse(hostManager.CanModifyRoom, "Non-host should not be able to modify room");
        Assert.IsFalse(hostManager.CanKickPlayers, "Non-host should not be able to kick players");
        Assert.IsFalse(hostManager.CanTransferHost, "Non-host should not be able to transfer host");
    }

    [Test]
    public void SetPermission_ShouldUpdateSpecificPermission()
    {
        // Act
        hostManager.SetPermission(HostPermissionType.KickPlayers, false);

        // Assert
        Assert.IsFalse(hostManager.HasPermission(HostPermissionType.KickPlayers), 
                      "Should disable kick permission");
        Assert.IsTrue(hostManager.HasPermission(HostPermissionType.StartGame), 
                     "Other permissions should remain enabled");
    }

    [Test]
    public void HasPermission_AsNonHost_ShouldReturnFalse()
    {
        // Arrange - Transfer host away
        hostManager.TransferHostTo(otherPlayerId);

        // Act & Assert
        Assert.IsFalse(hostManager.HasPermission(HostPermissionType.StartGame), 
                      "Non-host should not have any permissions");
        Assert.IsFalse(hostManager.HasPermission(HostPermissionType.KickPlayers), 
                      "Non-host should not have kick permission");
    }

    #endregion

    #region Current Host Tests

    [Test]
    public void CurrentHost_Properties_ShouldReturnCorrectValues()
    {
        // Assert
        Assert.AreEqual(testPlayerId, hostManager.CurrentHostId, "Should return correct host ID");
        Assert.IsNotNull(hostManager.CurrentHost, "Should return host player info");
        Assert.AreEqual("Test Host", hostManager.CurrentHost.Nickname, "Should return correct host info");
    }

    [Test]
    public void CurrentHost_AfterTransfer_ShouldUpdateCorrectly()
    {
        // Act
        hostManager.TransferHostTo(otherPlayerId);

        // Assert
        Assert.AreEqual(otherPlayerId, hostManager.CurrentHostId, "Host ID should be updated");
        Assert.AreEqual("Other Player", hostManager.CurrentHost.Nickname, "Host info should be updated");
    }

    #endregion

    #region Cleanup Tests

    [Test]
    public void Cleanup_ShouldResetAllState()
    {
        // Arrange
        bool eventSubscribed = false;
        hostManager.OnHostChanged += (hostId) => eventSubscribed = true;

        // Act
        hostManager.Cleanup();

        // Assert
        Assert.IsFalse(hostManager.IsHost, "Should reset host status");
        Assert.IsNull(hostManager.CurrentHostId, "Should reset current host");
        Assert.IsFalse(hostManager.CanStartGame, "Should reset game start ability");
        
        // Verify events are unsubscribed by firing an event that should do nothing
        hostManager.SetRoom(null); // This would normally fire events
        Assert.IsFalse(eventSubscribed, "Events should be unsubscribed");
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public void SetRoom_WithNullRoom_ShouldHandleGracefully()
    {
        // Act & Assert - Should not throw exception
        Assert.DoesNotThrow(() => hostManager.SetRoom(null));
        Assert.IsFalse(hostManager.IsHost, "Should not be host with null room");
        Assert.IsNull(hostManager.CurrentHostId, "Should have no current host");
    }

    [Test]
    public void TransferHostTo_WithEmptyPlayerId_ShouldFail()
    {
        // Act
        bool result1 = hostManager.TransferHostTo("");
        bool result2 = hostManager.TransferHostTo(null);

        // Assert
        Assert.IsFalse(result1, "Should fail with empty player ID");
        Assert.IsFalse(result2, "Should fail with null player ID");
        Assert.IsTrue(hostManager.IsHost, "Should remain host");
    }

    [Test]
    public void RequestStartGame_WithNoRoom_ShouldHandleGracefully()
    {
        // Arrange
        hostManager.SetRoom(null);
        bool eventFired = false;
        hostManager.OnGameStartFailed += (reason) => eventFired = true;

        // Act
        hostManager.RequestStartGame();

        // Assert
        Assert.IsTrue(eventFired, "Should fire failure event");
    }

    #endregion
}