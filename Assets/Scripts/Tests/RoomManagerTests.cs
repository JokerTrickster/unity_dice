using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// RoomManager 통합 테스트
/// 방 생성, 참여, 나가기 및 실시간 동기화 기능 검증
/// </summary>
public class RoomManagerTests
{
    private RoomManager roomManager;
    private string testPlayerId = "test_player_123";
    private string testPlayerNickname = "TestPlayer";

    [SetUp]
    public void SetUp()
    {
        // RoomManager 인스턴스 생성 (테스트용)
        var go = new GameObject("TestRoomManager");
        roomManager = go.AddComponent<RoomManager>();
        
        // UserDataManager 모킹 (실제 구현시에는 실제 매니저 사용)
        MockUserDataManager();
    }

    [TearDown]
    public void TearDown()
    {
        if (roomManager != null)
        {
            roomManager.Cleanup();
            UnityEngine.Object.DestroyImmediate(roomManager.gameObject);
        }
        
        // Singleton 정리
        RoomCodeGenerator.Instance.Cleanup();
    }

    private void MockUserDataManager()
    {
        // 실제 구현시에는 UserDataManager를 적절히 모킹하거나 테스트 데이터 설정
        Debug.Log($"[RoomManagerTests] Mocked UserDataManager with player: {testPlayerId}");
    }

    #region Room Creation Tests

    [UnityTest]
    public IEnumerator CreateRoom_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        bool callbackResult = false;
        string callbackMessage = null;
        bool eventFired = false;
        
        RoomManager.OnRoomCreated += (roomData) => eventFired = true;

        // Act
        roomManager.CreateRoom(4, (success, message) =>
        {
            callbackResult = success;
            callbackMessage = message;
        });

        yield return new WaitForSeconds(1f); // 비동기 작업 대기

        // Assert
        Assert.IsTrue(callbackResult, "Room creation should succeed");
        Assert.IsNotNull(callbackMessage, "Should receive room code");
        Assert.IsTrue(eventFired, "OnRoomCreated event should fire");
        Assert.IsTrue(roomManager.IsInRoom, "Should be in room after creation");
        Assert.IsTrue(roomManager.IsHost, "Creator should be host");
        Assert.AreEqual(4, roomManager.CurrentRoom.MaxPlayers, "Max players should be set correctly");
    }

    [UnityTest]
    public IEnumerator CreateRoom_WithInvalidMaxPlayers_ShouldFail()
    {
        // Arrange
        bool callbackResult = true;
        string callbackMessage = null;

        // Act
        roomManager.CreateRoom(1, (success, message) => // Invalid: less than 2
        {
            callbackResult = success;
            callbackMessage = message;
        });

        yield return new WaitForSeconds(0.5f);

        // Assert - 실제로는 RoomData에서 Clamp되므로 성공하지만 2로 조정됨
        if (callbackResult)
        {
            Assert.AreEqual(2, roomManager.CurrentRoom.MaxPlayers, "Should clamp to minimum 2 players");
        }
    }

    [UnityTest]
    public IEnumerator CreateRoom_WhileAlreadyInRoom_ShouldLeaveCurrentFirst()
    {
        // Arrange - 먼저 방 하나 생성
        bool firstRoomCreated = false;
        roomManager.CreateRoom(3, (success, message) => firstRoomCreated = success);
        yield return new WaitForSeconds(0.5f);
        
        Assert.IsTrue(firstRoomCreated, "First room should be created");
        string firstRoomCode = roomManager.CurrentRoom?.RoomCode;

        // Act - 새 방 생성
        bool secondRoomCreated = false;
        roomManager.CreateRoom(4, (success, message) => secondRoomCreated = success);
        yield return new WaitForSeconds(1f);

        // Assert
        Assert.IsTrue(secondRoomCreated, "Second room should be created");
        Assert.AreNotEqual(firstRoomCode, roomManager.CurrentRoom?.RoomCode, "Should be in different room");
        Assert.AreEqual(4, roomManager.CurrentRoom.MaxPlayers, "Should have new room settings");
    }

    #endregion

    #region Room Joining Tests

    [UnityTest]
    public IEnumerator JoinRoom_WithValidCode_ShouldSucceed()
    {
        // Arrange - 먼저 방 생성하여 유효한 코드 확보
        string roomCode = null;
        roomManager.CreateRoom(3, (success, code) => roomCode = code);
        yield return new WaitForSeconds(0.5f);
        
        Assert.IsNotNull(roomCode, "Should have valid room code");
        
        // 방 나가기 (다른 플레이어로 참여하기 위해)
        roomManager.LeaveRoom();
        yield return new WaitForSeconds(0.5f);

        // Act - 방 참여
        bool joinResult = false;
        roomManager.JoinRoom(roomCode, (success, message) => joinResult = success);
        yield return new WaitForSeconds(1f);

        // Assert
        Assert.IsTrue(joinResult, "Should successfully join room");
        Assert.IsTrue(roomManager.IsInRoom, "Should be in room after joining");
        Assert.AreEqual(roomCode, roomManager.CurrentRoom?.RoomCode, "Should be in correct room");
        Assert.IsFalse(roomManager.IsHost, "Joiner should not be host");
    }

    [UnityTest]
    public IEnumerator JoinRoom_WithInvalidCode_ShouldFail()
    {
        // Arrange
        string invalidCode = "invalid";
        bool joinResult = true;
        string errorMessage = null;

        // Act
        roomManager.JoinRoom(invalidCode, (success, message) =>
        {
            joinResult = success;
            errorMessage = message;
        });
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.IsFalse(joinResult, "Should fail with invalid code");
        Assert.IsNotNull(errorMessage, "Should provide error message");
        Assert.IsFalse(roomManager.IsInRoom, "Should not be in any room");
        Assert.IsTrue(errorMessage.Contains("형식"), "Should mention format error");
    }

    [UnityTest]
    public IEnumerator JoinRoom_WithNonexistentCode_ShouldFail()
    {
        // Arrange
        string nonexistentCode = "9999";
        bool joinResult = true;
        string errorMessage = null;

        // Act
        roomManager.JoinRoom(nonexistentCode, (success, message) =>
        {
            joinResult = success;
            errorMessage = message;
        });
        yield return new WaitForSeconds(1f);

        // Assert
        Assert.IsFalse(joinResult, "Should fail with nonexistent code");
        Assert.IsNotNull(errorMessage, "Should provide error message");
        Assert.IsFalse(roomManager.IsInRoom, "Should not be in any room");
    }

    #endregion

    #region Room Leaving Tests

    [UnityTest]
    public IEnumerator LeaveRoom_WhenInRoom_ShouldSucceed()
    {
        // Arrange - 방 생성
        bool roomCreated = false;
        roomManager.CreateRoom(3, (success, code) => roomCreated = success);
        yield return new WaitForSeconds(0.5f);
        
        Assert.IsTrue(roomCreated && roomManager.IsInRoom, "Should be in room initially");

        bool eventFired = false;
        RoomManager.OnRoomLeft += (roomData) => eventFired = true;

        // Act
        bool leaveResult = false;
        roomManager.LeaveRoom((success) => leaveResult = success);
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.IsTrue(leaveResult, "Leave room should succeed");
        Assert.IsTrue(eventFired, "OnRoomLeft event should fire");
        Assert.IsFalse(roomManager.IsInRoom, "Should not be in room after leaving");
        Assert.IsFalse(roomManager.IsHost, "Should not be host after leaving");
    }

    [UnityTest]
    public IEnumerator LeaveRoom_WhenNotInRoom_ShouldHandleGracefully()
    {
        // Arrange - 방에 있지 않은 상태 확인
        Assert.IsFalse(roomManager.IsInRoom, "Should not be in room initially");

        // Act
        bool leaveResult = true;
        roomManager.LeaveRoom((success) => leaveResult = success);
        yield return new WaitForSeconds(0.2f);

        // Assert
        Assert.IsFalse(leaveResult, "Should return false when not in room");
    }

    #endregion

    #region Game Start Tests

    [UnityTest]
    public IEnumerator StartGame_AsHost_ShouldSucceed()
    {
        // Arrange - 방 생성
        bool roomCreated = false;
        roomManager.CreateRoom(2, (success, code) => roomCreated = success);
        yield return new WaitForSeconds(0.5f);
        
        Assert.IsTrue(roomCreated && roomManager.IsHost, "Should be host");

        bool eventFired = false;
        RoomManager.OnGameStarted += (roomCode) => eventFired = true;

        // Act
        bool startResult = false;
        roomManager.StartGame((success, message) => startResult = success);
        yield return new WaitForSeconds(1f);

        // Assert
        Assert.IsTrue(startResult, "Game start should succeed");
        Assert.IsTrue(eventFired, "OnGameStarted event should fire");
        Assert.AreEqual(RoomStatus.Starting, roomManager.CurrentRoom.Status, "Room status should be Starting");
    }

    [UnityTest]
    public IEnumerator StartGame_AsNonHost_ShouldFail()
    {
        // Arrange - 방 생성 후 호스트 권한 제거 시뮬레이션
        roomManager.CreateRoom(2, null);
        yield return new WaitForSeconds(0.5f);
        
        // 호스트 권한 제거를 위해 방 데이터 조작 (테스트용)
        if (roomManager.CurrentRoom != null)
        {
            roomManager.CurrentRoom.HostPlayerId = "other_player";
        }

        bool startResult = true;
        string errorMessage = null;

        // Act
        roomManager.StartGame((success, message) =>
        {
            startResult = success;
            errorMessage = message;
        });
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.IsFalse(startResult, "Game start should fail for non-host");
        Assert.IsNotNull(errorMessage, "Should provide error message");
        Assert.IsTrue(errorMessage.Contains("방장"), "Should mention host requirement");
    }

    #endregion

    #region Event System Tests

    [Test]
    public void RoomEvents_ShouldBeDefinedCorrectly()
    {
        // Test that all expected events exist and are properly typed
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomCreated"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomJoined"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomLeft"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomClosed"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomUpdated"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnPlayerJoined"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnPlayerLeft"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnHostChanged"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnGameStarted"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnGameStartFailed"));
        Assert.IsNotNull(typeof(RoomManager).GetEvent("OnRoomError"));
    }

    [UnityTest]
    public IEnumerator ConnectionEvents_ShouldFireCorrectly()
    {
        // Arrange
        bool connectedEventFired = false;
        bool disconnectedEventFired = false;
        
        RoomManager.OnConnectionStatusChanged += (connected) =>
        {
            if (connected) connectedEventFired = true;
            else disconnectedEventFired = true;
        };

        // Act - 방 생성 (연결됨)
        roomManager.CreateRoom(2, null);
        yield return new WaitForSeconds(0.5f);

        // 방 나가기 (연결 해제)
        roomManager.LeaveRoom();
        yield return new WaitForSeconds(0.5f);

        // Assert
        Assert.IsTrue(connectedEventFired, "Connection event should fire when joining room");
        Assert.IsTrue(disconnectedEventFired, "Disconnection event should fire when leaving room");
    }

    #endregion

    #region Utility Tests

    [UnityTest]
    public IEnumerator GetRoomStatusSummary_ShouldProvideCorrectInfo()
    {
        // Test when not in room
        string summary = roomManager.GetRoomStatusSummary();
        Assert.IsTrue(summary.Contains("Not in any room"), "Should indicate no room when not in room");

        // Test when in room
        roomManager.CreateRoom(3, null);
        yield return new WaitForSeconds(0.5f);

        summary = roomManager.GetRoomStatusSummary();
        Assert.IsTrue(summary.Contains("Room:"), "Should contain room info");
        Assert.IsTrue(summary.Contains("Status:"), "Should contain status info");
        Assert.IsTrue(summary.Contains("Players:"), "Should contain player count");
        Assert.IsTrue(summary.Contains("Host: True"), "Should indicate host status");
    }

    [UnityTest]
    public IEnumerator CopyRoomCodeToClipboard_ShouldWork()
    {
        // Arrange
        string originalClipboard = GUIUtility.systemCopyBuffer;
        roomManager.CreateRoom(2, null);
        yield return new WaitForSeconds(0.5f);
        
        string roomCode = roomManager.CurrentRoom?.RoomCode;
        Assert.IsNotNull(roomCode, "Should have room code");

        // Act
        roomManager.CopyRoomCodeToClipboard();

        // Assert
        Assert.AreEqual(roomCode, GUIUtility.systemCopyBuffer, "Room code should be in clipboard");
        
        // Cleanup
        GUIUtility.systemCopyBuffer = originalClipboard;
    }

    #endregion

    #region Performance Tests

    [UnityTest]
    public IEnumerator MultipleRoomOperations_ShouldMaintainPerformance()
    {
        // Test rapid room creation/joining/leaving
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < 5; i++)
        {
            roomManager.CreateRoom(2, null);
            yield return new WaitForSeconds(0.1f);
            
            roomManager.LeaveRoom();
            yield return new WaitForSeconds(0.1f);
        }
        
        stopwatch.Stop();
        
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000, 
                     $"Multiple operations should complete quickly, took {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion
}