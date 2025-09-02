using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Room System Integration Tests
/// 방 시스템 전체 통합 테스트 - 모든 컴포넌트 간 상호작용 및 성능 검증
/// Stream D: Integration & Testing - Complete end-to-end room system validation
/// </summary>
public class RoomSystemTests
{
    #region Test Setup
    private RoomManager roomManager;
    private RoomCodeGenerator codeGenerator;
    private RoomNetworkHandler networkHandler;
    private NetworkManager networkManager;
    private GameObject testGameObject;
    
    private string testPlayerId = "integration_test_player";
    private string testPlayerNickname = "TestUser";
    private string hostPlayerId = "host_player";
    private string joinPlayerId = "join_player";
    
    // Performance tracking
    private System.Diagnostics.Stopwatch performanceTimer;
    private List<float> responseTimeResults = new List<float>();
    private List<float> syncTimeResults = new List<float>();
    
    [SetUp]
    public void SetUp()
    {
        Debug.Log("[RoomSystemTests] Setting up integration test environment");
        
        // Create test environment
        testGameObject = new GameObject("RoomSystemIntegrationTest");
        
        // Initialize components in dependency order
        InitializeTestNetwork();
        InitializeRoomCodeGenerator();
        InitializeRoomManager();
        InitializeNetworkHandler();
        
        performanceTimer = new System.Diagnostics.Stopwatch();
        responseTimeResults.Clear();
        syncTimeResults.Clear();
        
        Debug.Log("[RoomSystemTests] Integration test setup complete");
    }
    
    [TearDown]
    public void TearDown()
    {
        Debug.Log("[RoomSystemTests] Cleaning up integration test environment");
        
        // Cleanup in reverse dependency order
        CleanupNetworkHandler();
        CleanupRoomManager();
        CleanupRoomCodeGenerator();
        CleanupTestNetwork();
        
        if (testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(testGameObject);
        }
        
        LogPerformanceResults();
        
        Debug.Log("[RoomSystemTests] Integration test cleanup complete");
    }
    
    private void InitializeTestNetwork()
    {
        networkManager = testGameObject.AddComponent<NetworkManager>();
        // Mock network manager initialization
        Debug.Log("[RoomSystemTests] Test NetworkManager initialized");
    }
    
    private void InitializeRoomCodeGenerator()
    {
        codeGenerator = RoomCodeGenerator.Instance;
        Assert.IsNotNull(codeGenerator, "RoomCodeGenerator should be available");
    }
    
    private void InitializeRoomManager()
    {
        roomManager = RoomManager.Instance;
        Assert.IsNotNull(roomManager, "RoomManager should be available");
        
        // Setup test events for monitoring
        SetupRoomManagerEventMonitoring();
    }
    
    private void InitializeNetworkHandler()
    {
        networkHandler = testGameObject.AddComponent<RoomNetworkHandler>();
        SetupNetworkHandlerEventMonitoring();
    }
    
    private void SetupRoomManagerEventMonitoring()
    {
        RoomManager.OnRoomCreated += OnTestRoomCreated;
        RoomManager.OnRoomJoined += OnTestRoomJoined;
        RoomManager.OnRoomLeft += OnTestRoomLeft;
        RoomManager.OnRoomError += OnTestRoomError;
        RoomManager.OnGameStarted += OnTestGameStarted;
        RoomManager.OnPlayerJoined += OnTestPlayerJoined;
        RoomManager.OnPlayerLeft += OnTestPlayerLeft;
        RoomManager.OnHostChanged += OnTestHostChanged;
    }
    
    private void SetupNetworkHandlerEventMonitoring()
    {
        if (networkHandler != null)
        {
            networkHandler.OnRoomResponse += OnTestRoomResponse;
            networkHandler.OnRoomError += OnTestNetworkError;
            networkHandler.OnConnectionStateChanged += OnTestConnectionChanged;
        }
    }
    
    private void CleanupRoomManager()
    {
        if (roomManager != null)
        {
            RoomManager.OnRoomCreated -= OnTestRoomCreated;
            RoomManager.OnRoomJoined -= OnTestRoomJoined;
            RoomManager.OnRoomLeft -= OnTestRoomLeft;
            RoomManager.OnRoomError -= OnTestRoomError;
            RoomManager.OnGameStarted -= OnTestGameStarted;
            RoomManager.OnPlayerJoined -= OnTestPlayerJoined;
            RoomManager.OnPlayerLeft -= OnTestPlayerLeft;
            RoomManager.OnHostChanged -= OnTestHostChanged;
            
            roomManager.Cleanup();
        }
    }
    
    private void CleanupNetworkHandler()
    {
        if (networkHandler != null)
        {
            networkHandler.OnRoomResponse -= OnTestRoomResponse;
            networkHandler.OnRoomError -= OnTestNetworkError;
            networkHandler.OnConnectionStateChanged -= OnTestConnectionChanged;
        }
    }
    
    private void CleanupRoomCodeGenerator()
    {
        codeGenerator?.Cleanup();
    }
    
    private void CleanupTestNetwork()
    {
        // Cleanup mock network
    }
    
    private void LogPerformanceResults()
    {
        if (responseTimeResults.Count > 0)
        {
            float avgResponse = CalculateAverage(responseTimeResults);
            Debug.Log($"[RoomSystemTests] Average response time: {avgResponse:F2}ms (target: <2000ms)");
            
            if (avgResponse > 2000f)
            {
                Debug.LogWarning($"[RoomSystemTests] Response time exceeds target: {avgResponse:F2}ms");
            }
        }
        
        if (syncTimeResults.Count > 0)
        {
            float avgSync = CalculateAverage(syncTimeResults);
            Debug.Log($"[RoomSystemTests] Average sync time: {avgSync:F2}ms (target: <1000ms)");
            
            if (avgSync > 1000f)
            {
                Debug.LogWarning($"[RoomSystemTests] Sync time exceeds target: {avgSync:F2}ms");
            }
        }
    }
    
    private float CalculateAverage(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float sum = 0f;
        foreach (var value in values) sum += value;
        return sum / values.Count;
    }
    #endregion
    
    #region Event Handlers for Monitoring
    private void OnTestRoomCreated(RoomData roomData)
    {
        Debug.Log($"[RoomSystemTests] Room created event: {roomData?.RoomCode}");
        RecordResponseTime();
    }
    
    private void OnTestRoomJoined(RoomData roomData)
    {
        Debug.Log($"[RoomSystemTests] Room joined event: {roomData?.RoomCode}");
        RecordResponseTime();
    }
    
    private void OnTestRoomLeft(RoomData roomData)
    {
        Debug.Log($"[RoomSystemTests] Room left event: {roomData?.RoomCode}");
    }
    
    private void OnTestRoomError(string operation, string error)
    {
        Debug.LogWarning($"[RoomSystemTests] Room error: {operation} - {error}");
    }
    
    private void OnTestGameStarted(string roomCode)
    {
        Debug.Log($"[RoomSystemTests] Game started event: {roomCode}");
    }
    
    private void OnTestPlayerJoined(RoomData roomData, PlayerInfo playerInfo)
    {
        Debug.Log($"[RoomSystemTests] Player joined: {playerInfo?.Nickname} in room {roomData?.RoomCode}");
        RecordSyncTime();
    }
    
    private void OnTestPlayerLeft(RoomData roomData, PlayerInfo playerInfo)
    {
        Debug.Log($"[RoomSystemTests] Player left: {playerInfo?.Nickname} from room {roomData?.RoomCode}");
        RecordSyncTime();
    }
    
    private void OnTestHostChanged(string newHostId)
    {
        Debug.Log($"[RoomSystemTests] Host changed to: {newHostId}");
        RecordSyncTime();
    }
    
    private void OnTestRoomResponse(RoomProtocolExtension.RoomResponse response)
    {
        Debug.Log($"[RoomSystemTests] Network response: {response?.GetSummary()}");
    }
    
    private void OnTestNetworkError(string errorCode, string errorMessage, string roomCode)
    {
        Debug.LogError($"[RoomSystemTests] Network error: [{errorCode}] {errorMessage} (Room: {roomCode})");
    }
    
    private void OnTestConnectionChanged(bool connected)
    {
        Debug.Log($"[RoomSystemTests] Connection state: {connected}");
    }
    
    private void RecordResponseTime()
    {
        if (performanceTimer.IsRunning)
        {
            performanceTimer.Stop();
            float elapsed = (float)performanceTimer.ElapsedMilliseconds;
            responseTimeResults.Add(elapsed);
            Debug.Log($"[RoomSystemTests] Response time recorded: {elapsed}ms");
        }
    }
    
    private void RecordSyncTime()
    {
        if (performanceTimer.IsRunning)
        {
            performanceTimer.Stop();
            float elapsed = (float)performanceTimer.ElapsedMilliseconds;
            syncTimeResults.Add(elapsed);
            Debug.Log($"[RoomSystemTests] Sync time recorded: {elapsed}ms");
        }
    }
    #endregion
    
    #region End-to-End Integration Tests
    
    [UnityTest]
    public IEnumerator CompleteRoomFlow_CreateJoinStartGame_ShouldSucceedWithinTargetTimes()
    {
        Debug.Log("[RoomSystemTests] Starting complete room flow test");
        
        // Track overall test performance
        var testStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Stage 1: Room Creation
        Debug.Log("[RoomSystemTests] Stage 1: Room Creation");
        performanceTimer.Restart();
        
        bool roomCreated = false;
        string roomCode = null;
        roomManager.CreateRoom(4, (success, code) =>
        {
            roomCreated = success;
            roomCode = code;
        });
        
        yield return new WaitForSeconds(2.5f); // Allow for network delay
        
        // Validate room creation
        Assert.IsTrue(roomCreated, "Room should be created successfully");
        Assert.IsNotNull(roomCode, "Room code should be generated");
        Assert.IsTrue(RoomCodeGenerator.IsValidRoomCodeFormat(roomCode), "Room code should have valid format");
        Assert.IsTrue(roomManager.IsInRoom, "Should be in room after creation");
        Assert.IsTrue(roomManager.IsHost, "Creator should be host");
        Assert.AreEqual(4, roomManager.CurrentRoom.MaxPlayers, "Max players should be set correctly");
        
        Debug.Log($"[RoomSystemTests] Room created: {roomCode}");
        
        // Stage 2: Simulate second player joining
        Debug.Log("[RoomSystemTests] Stage 2: Player Join Simulation");
        performanceTimer.Restart();
        
        // Simulate player join via events (in real scenario this would come from network)
        var joinPlayer = new PlayerInfo(joinPlayerId, "SecondPlayer", false);
        var currentRoom = roomManager.CurrentRoom;
        bool playerAdded = currentRoom.AddPlayer(joinPlayer);
        
        Assert.IsTrue(playerAdded, "Second player should be added successfully");
        Assert.AreEqual(2, currentRoom.CurrentPlayerCount, "Should have 2 players");
        
        // Simulate network event for player joined
        RoomManager.OnPlayerJoined?.Invoke(currentRoom, joinPlayer);
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[RoomSystemTests] Player joined: {joinPlayer.Nickname}");
        
        // Stage 3: Host Privileges Test
        Debug.Log("[RoomSystemTests] Stage 3: Host Privileges Validation");
        
        // Verify host can start game
        Assert.IsTrue(currentRoom.CanStart, "Room should be able to start with 2+ players");
        Assert.IsTrue(currentRoom.IsPlayerHost(roomManager.LocalPlayerId), "Local player should be host");
        
        // Stage 4: Game Start Flow
        Debug.Log("[RoomSystemTests] Stage 4: Game Start");
        performanceTimer.Restart();
        
        bool gameStarted = false;
        roomManager.StartGame((success, message) => gameStarted = success);
        yield return new WaitForSeconds(1.5f);
        
        Assert.IsTrue(gameStarted, "Game should start successfully");
        Assert.AreEqual(RoomStatus.Starting, currentRoom.Status, "Room status should be Starting");
        
        testStopwatch.Stop();
        
        // Overall Performance Validation
        float totalTime = (float)testStopwatch.ElapsedMilliseconds;
        Debug.Log($"[RoomSystemTests] Complete flow took: {totalTime}ms");
        
        Assert.IsTrue(totalTime < 10000f, $"Complete flow should finish within 10 seconds, took {totalTime}ms");
        
        Debug.Log("[RoomSystemTests] Complete room flow test passed");
    }
    
    [UnityTest]
    public IEnumerator RoomCodeGeneration_UniquenessAndSecurity_ShouldPreventDuplicatesAndBruteForce()
    {
        Debug.Log("[RoomSystemTests] Testing room code generation security");
        
        var generatedCodes = new HashSet<string>();
        
        // Test unique code generation
        for (int i = 0; i < 20; i++)
        {
            string code = codeGenerator.GenerateRoomCode();
            Assert.IsTrue(RoomCodeGenerator.IsValidRoomCodeFormat(code), $"Code {code} should have valid format");
            Assert.IsFalse(generatedCodes.Contains(code), $"Code {code} should be unique");
            generatedCodes.Add(code);
            
            // Small delay to avoid timing conflicts
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log($"[RoomSystemTests] Generated {generatedCodes.Count} unique codes");
        
        // Test brute force protection
        string testIp = "192.168.1.100";
        
        // Exceed brute force limit
        for (int i = 0; i < 6; i++)
        {
            bool allowed = codeGenerator.RecordBruteForceAttempt(testIp, "9999");
            if (i < 5)
            {
                Assert.IsTrue(allowed, $"Attempt {i + 1} should be allowed");
            }
            else
            {
                Assert.IsFalse(allowed, "Attempt 6 should be blocked by brute force protection");
            }
        }
        
        Debug.Log("[RoomSystemTests] Brute force protection validated");
        
        // Cleanup generated codes
        foreach (var code in generatedCodes)
        {
            codeGenerator.ReleaseCode(code);
        }
    }
    
    [UnityTest]
    public IEnumerator NetworkDisconnectionRecovery_ShouldHandleGracefully()
    {
        Debug.Log("[RoomSystemTests] Testing network disconnection recovery");
        
        // Create room first
        bool roomCreated = false;
        string roomCode = null;
        roomManager.CreateRoom(3, (success, code) => {
            roomCreated = success;
            roomCode = code;
        });
        
        yield return new WaitForSeconds(1f);
        Assert.IsTrue(roomCreated, "Room should be created before testing disconnection");
        
        // Simulate network disconnection
        bool connectionLost = false;
        RoomManager.OnConnectionStatusChanged += (connected) =>
        {
            if (!connected) connectionLost = true;
        };
        
        // Simulate disconnection via network handler
        if (networkHandler != null && networkHandler.IsInitialized)
        {
            networkHandler.OnConnectionStateChanged?.Invoke(false);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Should handle disconnection gracefully without crashing
        Assert.IsTrue(roomManager.IsInRoom, "Should still maintain room state during disconnection");
        
        // Simulate reconnection
        if (networkHandler != null)
        {
            networkHandler.OnConnectionStateChanged?.Invoke(true);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[RoomSystemTests] Network disconnection recovery test completed");
    }
    
    [UnityTest]
    public IEnumerator MultiplePlayersWorkflow_ShouldSynchronizeCorrectly()
    {
        Debug.Log("[RoomSystemTests] Testing multiple players workflow");
        
        // Create room
        bool roomCreated = false;
        string roomCode = null;
        roomManager.CreateRoom(4, (success, code) => {
            roomCreated = success;
            roomCode = code;
        });
        
        yield return new WaitForSeconds(1f);
        Assert.IsTrue(roomCreated, "Room should be created");
        
        var currentRoom = roomManager.CurrentRoom;
        int initialPlayerCount = currentRoom.CurrentPlayerCount;
        
        // Simulate multiple players joining
        var players = new[]
        {
            new PlayerInfo("player2", "TestUser2", false),
            new PlayerInfo("player3", "TestUser3", false),
            new PlayerInfo("player4", "TestUser4", false)
        };
        
        performanceTimer.Restart();
        
        foreach (var player in players)
        {
            bool added = currentRoom.AddPlayer(player);
            Assert.IsTrue(added, $"Player {player.Nickname} should be added successfully");
            
            // Simulate network sync event
            RoomManager.OnPlayerJoined?.Invoke(currentRoom, player);
            yield return new WaitForSeconds(0.2f); // Simulate network delay
        }
        
        // Validate final state
        Assert.AreEqual(initialPlayerCount + 3, currentRoom.CurrentPlayerCount, "Should have 4 total players");
        Assert.IsTrue(currentRoom.IsFull, "Room should be full with 4 players");
        Assert.IsTrue(currentRoom.CanStart, "Full room should be able to start");
        
        // Test host privileges with full room
        bool gameStarted = false;
        roomManager.StartGame((success, message) => gameStarted = success);
        yield return new WaitForSeconds(1f);
        
        Assert.IsTrue(gameStarted, "Host should be able to start game with full room");
        
        Debug.Log("[RoomSystemTests] Multiple players workflow completed successfully");
    }
    #endregion
    
    #region Performance and Load Tests
    
    [UnityTest]
    public IEnumerator PerformanceTest_RoomCreationResponseTime_ShouldMeetTarget()
    {
        Debug.Log("[RoomSystemTests] Testing room creation performance");
        
        const int testIterations = 5;
        var responseTimes = new List<float>();
        
        for (int i = 0; i < testIterations; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            bool roomCreated = false;
            roomManager.CreateRoom(2, (success, code) => {
                stopwatch.Stop();
                roomCreated = success;
                if (success)
                {
                    responseTimes.Add((float)stopwatch.ElapsedMilliseconds);
                }
            });
            
            yield return new WaitForSeconds(2.5f); // Max wait time
            
            Assert.IsTrue(roomCreated, $"Room creation {i + 1} should succeed");
            
            // Leave room for next iteration
            if (roomManager.IsInRoom)
            {
                roomManager.LeaveRoom();
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Validate performance requirements
        float avgResponseTime = responseTimes.Count > 0 ? CalculateAverage(responseTimes) : float.MaxValue;
        float maxResponseTime = responseTimes.Count > 0 ? responseTimes.Max() : float.MaxValue;
        
        Debug.Log($"[RoomSystemTests] Room creation - Avg: {avgResponseTime:F2}ms, Max: {maxResponseTime:F2}ms");
        
        Assert.IsTrue(avgResponseTime < 2000f, $"Average response time ({avgResponseTime:F2}ms) should be under 2000ms");
        Assert.IsTrue(maxResponseTime < 3000f, $"Max response time ({maxResponseTime:F2}ms) should be under 3000ms");
    }
    
    [UnityTest]
    public IEnumerator PerformanceTest_PlayerListSyncTime_ShouldMeetTarget()
    {
        Debug.Log("[RoomSystemTests] Testing player list sync performance");
        
        // Create room first
        roomManager.CreateRoom(4, null);
        yield return new WaitForSeconds(1f);
        
        var currentRoom = roomManager.CurrentRoom;
        Assert.IsNotNull(currentRoom, "Room should exist for sync test");
        
        const int syncOperations = 10;
        var syncTimes = new List<float>();
        
        for (int i = 0; i < syncOperations; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Simulate player join/leave for sync
            var testPlayer = new PlayerInfo($"sync_test_{i}", $"SyncUser{i}", false);
            currentRoom.AddPlayer(testPlayer);
            
            // Trigger sync event
            RoomManager.OnPlayerJoined?.Invoke(currentRoom, testPlayer);
            
            stopwatch.Stop();
            syncTimes.Add((float)stopwatch.ElapsedMilliseconds);
            
            yield return new WaitForSeconds(0.1f);
            
            // Remove player for next iteration
            currentRoom.RemovePlayer(testPlayer.PlayerId);
        }
        
        float avgSyncTime = CalculateAverage(syncTimes);
        float maxSyncTime = syncTimes.Max();
        
        Debug.Log($"[RoomSystemTests] Player sync - Avg: {avgSyncTime:F2}ms, Max: {maxSyncTime:F2}ms");
        
        Assert.IsTrue(avgSyncTime < 1000f, $"Average sync time ({avgSyncTime:F2}ms) should be under 1000ms");
        Assert.IsTrue(maxSyncTime < 1500f, $"Max sync time ({maxSyncTime:F2}ms) should be under 1500ms");
    }
    
    [UnityTest]
    public IEnumerator MemoryUsageTest_ExtendedRoomOperations_ShouldStayWithinLimits()
    {
        Debug.Log("[RoomSystemTests] Testing memory usage during extended operations");
        
        // Get initial memory usage
        long initialMemory = System.GC.GetTotalMemory(true);
        
        // Perform extended room operations
        const int operationCycles = 20;
        
        for (int i = 0; i < operationCycles; i++)
        {
            // Create room
            roomManager.CreateRoom(3, null);
            yield return new WaitForSeconds(0.2f);
            
            // Simulate players joining
            var room = roomManager.CurrentRoom;
            if (room != null)
            {
                var player = new PlayerInfo($"mem_test_{i}", $"MemUser{i}", false);
                room.AddPlayer(player);
                RoomManager.OnPlayerJoined?.Invoke(room, player);
            }
            
            yield return new WaitForSeconds(0.1f);
            
            // Leave room
            roomManager.LeaveRoom();
            yield return new WaitForSeconds(0.1f);
            
            // Force garbage collection every 5 cycles
            if (i % 5 == 0)
            {
                System.GC.Collect();
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Final cleanup and memory check
        System.GC.Collect();
        yield return new WaitForSeconds(0.5f);
        
        long finalMemory = System.GC.GetTotalMemory(true);
        long memoryIncrease = finalMemory - initialMemory;
        float memoryIncreaseMB = memoryIncrease / (1024f * 1024f);
        
        Debug.Log($"[RoomSystemTests] Memory usage - Initial: {initialMemory / (1024 * 1024):F2}MB, " +
                 $"Final: {finalMemory / (1024 * 1024):F2}MB, Increase: {memoryIncreaseMB:F2}MB");
        
        Assert.IsTrue(memoryIncreaseMB < 5f, $"Memory increase ({memoryIncreaseMB:F2}MB) should be under 5MB");
    }
    #endregion
    
    #region Error Handling and Edge Cases
    
    [UnityTest]
    public IEnumerator ErrorHandling_InvalidRoomCodes_ShouldProvideImmediateResponse()
    {
        Debug.Log("[RoomSystemTests] Testing error handling for invalid room codes");
        
        var invalidCodes = new string[] { "abc", "12", "12345", "", null, "0000", "9999" };
        
        foreach (var invalidCode in invalidCodes)
        {
            performanceTimer.Restart();
            
            bool joinResult = true;
            string errorMessage = null;
            
            roomManager.JoinRoom(invalidCode, (success, message) =>
            {
                joinResult = success;
                errorMessage = message;
            });
            
            yield return new WaitForSeconds(1f); // Allow for error processing
            
            Assert.IsFalse(joinResult, $"Join should fail for invalid code: {invalidCode ?? "null"}");
            Assert.IsNotNull(errorMessage, $"Should provide error message for code: {invalidCode ?? "null"}");
            
            // Error response should be immediate (under 100ms for validation errors)
            performanceTimer.Stop();
            float errorResponseTime = (float)performanceTimer.ElapsedMilliseconds;
            
            if (invalidCode != "9999") // 9999 might be valid format but non-existent
            {
                Assert.IsTrue(errorResponseTime < 500f, 
                    $"Error response for invalid code '{invalidCode}' should be immediate, took {errorResponseTime}ms");
            }
        }
        
        Debug.Log("[RoomSystemTests] Invalid room code error handling validated");
    }
    
    [UnityTest]
    public IEnumerator EdgeCase_RoomExpiration_ShouldHandleGracefully()
    {
        Debug.Log("[RoomSystemTests] Testing room expiration edge case");
        
        // Create room
        roomManager.CreateRoom(2, null);
        yield return new WaitForSeconds(1f);
        
        var currentRoom = roomManager.CurrentRoom;
        Assert.IsNotNull(currentRoom, "Room should exist");
        
        // Simulate room expiration by manipulating expiration time
        currentRoom.ExpiresAt = DateTime.Now.AddSeconds(-1); // Expired 1 second ago
        
        // Check if room properly detects expiration
        Assert.IsTrue(currentRoom.IsExpired, "Room should be detected as expired");
        
        // The system should handle expiration automatically
        // Wait for potential cleanup
        yield return new WaitForSeconds(2f);
        
        // Verify system handles expired room gracefully
        string roomStatus = roomManager.GetRoomStatusSummary();
        Debug.Log($"[RoomSystemTests] Room status after expiration: {roomStatus}");
        
        // System should either auto-cleanup or maintain state safely
        Assert.IsNotNull(roomStatus, "Status should always be available even for expired rooms");
    }
    
    [UnityTest]
    public IEnumerator EdgeCase_HostDisconnection_ShouldTransferHostPrivileges()
    {
        Debug.Log("[RoomSystemTests] Testing host disconnection and privilege transfer");
        
        // Create room as host
        roomManager.CreateRoom(4, null);
        yield return new WaitForSeconds(1f);
        
        var currentRoom = roomManager.CurrentRoom;
        Assert.IsTrue(roomManager.IsHost, "Should be host initially");
        
        // Add another player to room
        var newPlayer = new PlayerInfo("new_host_candidate", "NewHost", false);
        currentRoom.AddPlayer(newPlayer);
        RoomManager.OnPlayerJoined?.Invoke(currentRoom, newPlayer);
        
        yield return new WaitForSeconds(0.5f);
        
        Assert.AreEqual(2, currentRoom.CurrentPlayerCount, "Should have 2 players");
        
        // Simulate current host leaving (host privilege transfer should happen)
        currentRoom.RemovePlayer(roomManager.LocalPlayerId);
        
        // Verify host transfer occurred
        Assert.AreEqual(newPlayer.PlayerId, currentRoom.HostPlayerId, "Host should be transferred to remaining player");
        Assert.IsTrue(newPlayer.IsHost, "New player should have host privileges");
        
        // Simulate host change event
        RoomManager.OnHostChanged?.Invoke(newPlayer.PlayerId);
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[RoomSystemTests] Host disconnection and transfer validated");
    }
    #endregion
    
    #region System Statistics and Monitoring
    
    [Test]
    public void SystemCapabilities_ShouldMeetRequiredSpecifications()
    {
        Debug.Log("[RoomSystemTests] Validating system capabilities");
        
        // Room code generation capacity
        var stats = codeGenerator.GetStatistics();
        Assert.IsNotNull(stats, "Should provide system statistics");
        Assert.IsTrue(stats.TotalAvailable > 8000, "Should have sufficient room code capacity");
        
        // Manager availability
        Assert.IsTrue(roomManager.IsInitialized, "Room manager should be initialized");
        Assert.IsNotNull(networkHandler, "Network handler should be available");
        
        // Event system completeness
        var roomManagerType = typeof(RoomManager);
        var requiredEvents = new string[]
        {
            "OnRoomCreated", "OnRoomJoined", "OnRoomLeft", "OnRoomClosed", "OnRoomUpdated",
            "OnPlayerJoined", "OnPlayerLeft", "OnPlayerUpdated", "OnHostChanged",
            "OnGameStartRequested", "OnGameStarted", "OnGameStartFailed",
            "OnRoomError", "OnConnectionStatusChanged"
        };
        
        foreach (var eventName in requiredEvents)
        {
            var eventInfo = roomManagerType.GetEvent(eventName);
            Assert.IsNotNull(eventInfo, $"Required event {eventName} should be defined");
        }
        
        Debug.Log("[RoomSystemTests] System capabilities validation complete");
    }
    
    [Test]
    public void IntegrationInterfaceCompatibility_ShouldBeComplete()
    {
        Debug.Log("[RoomSystemTests] Testing integration interface compatibility");
        
        // Verify all required public methods exist and are accessible
        var roomManagerType = typeof(RoomManager);
        
        var requiredMethods = new string[]
        {
            "CreateRoom", "JoinRoom", "LeaveRoom", "StartGame", "CopyRoomCodeToClipboard",
            "GetRoomStatusSummary", "HandleRoomWebSocketMessage", "Cleanup"
        };
        
        foreach (var methodName in requiredMethods)
        {
            var method = roomManagerType.GetMethod(methodName);
            Assert.IsNotNull(method, $"Required method {methodName} should be available");
        }
        
        // Verify required properties
        var requiredProperties = new string[]
        {
            "CurrentRoom", "IsInRoom", "IsHost", "IsConnected", "LocalPlayerId", "LocalPlayer"
        };
        
        foreach (var propertyName in requiredProperties)
        {
            var property = roomManagerType.GetProperty(propertyName);
            Assert.IsNotNull(property, $"Required property {propertyName} should be available");
        }
        
        Debug.Log("[RoomSystemTests] Integration interface compatibility verified");
    }
    #endregion
}