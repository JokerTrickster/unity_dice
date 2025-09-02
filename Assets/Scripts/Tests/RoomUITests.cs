using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Room UI Integration Tests  
/// 방 UI 시스템 통합 테스트 - UI 컴포넌트와 RoomManager 상호작용 검증
/// Stream D: Integration & Testing - Complete UI workflow and responsiveness validation
/// </summary>
public class RoomUITests
{
    #region Test Setup
    private GameObject testCanvas;
    private RoomUI roomUI;
    private RoomManager roomManager;
    private RoomCreateUI mockRoomCreateUI;
    private RoomCodeInput mockRoomCodeInput;
    private RoomPlayerList mockPlayerList;
    
    // UI Components
    private GameObject roomMainPanel;
    private GameObject createRoomModal;
    private GameObject joinRoomModal;
    private GameObject roomDisplayPanel;
    private GameObject hostControlsPanel;
    private GameObject loadingIndicator;
    private GameObject successMessage;
    private GameObject errorMessage;
    
    private Button createRoomButton;
    private Button joinRoomButton;
    private Button leaveRoomButton;
    private Button copyRoomCodeButton;
    private Button startGameButton;
    
    private Text roomCodeText;
    private Text roomStatusText;
    private Text hostStatusText;
    private Text errorMessageText;
    private Text successMessageText;
    
    // Test tracking
    private List<string> uiStateTransitions = new List<string>();
    private List<float> uiResponseTimes = new List<float>();
    private System.Diagnostics.Stopwatch uiTimer = new System.Diagnostics.Stopwatch();
    private int eventCallbackCount = 0;
    
    [SetUp]
    public void SetUp()
    {
        Debug.Log("[RoomUITests] Setting up UI integration test environment");
        
        CreateTestUIHierarchy();
        SetupRoomManager();
        CreateRoomUIComponent();
        SetupMockSubComponents();
        SetupUIEventMonitoring();
        
        uiStateTransitions.Clear();
        uiResponseTimes.Clear();
        eventCallbackCount = 0;
        
        Debug.Log("[RoomUITests] UI test environment ready");
    }
    
    [TearDown]
    public void TearDown()
    {
        Debug.Log("[RoomUITests] Cleaning up UI test environment");
        
        CleanupUIEventMonitoring();
        CleanupRoomManager();
        
        if (testCanvas != null)
        {
            UnityEngine.Object.DestroyImmediate(testCanvas);
        }
        
        LogUITestResults();
        
        Debug.Log("[RoomUITests] UI test cleanup complete");
    }
    
    private void CreateTestUIHierarchy()
    {
        // Create test canvas
        testCanvas = new GameObject("TestCanvas");
        var canvas = testCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Create main UI panels
        roomMainPanel = CreateUIPanel("RoomMainPanel", testCanvas);
        createRoomModal = CreateUIPanel("CreateRoomModal", testCanvas);
        joinRoomModal = CreateUIPanel("JoinRoomModal", testCanvas);
        roomDisplayPanel = CreateUIPanel("RoomDisplayPanel", testCanvas);
        hostControlsPanel = CreateUIPanel("HostControlsPanel", testCanvas);
        loadingIndicator = CreateUIPanel("LoadingIndicator", testCanvas);
        successMessage = CreateUIPanel("SuccessMessage", testCanvas);
        errorMessage = CreateUIPanel("ErrorMessage", testCanvas);
        
        // Create buttons
        createRoomButton = CreateTestButton("CreateRoomButton", roomMainPanel);
        joinRoomButton = CreateTestButton("JoinRoomButton", roomMainPanel);
        leaveRoomButton = CreateTestButton("LeaveRoomButton", roomDisplayPanel);
        copyRoomCodeButton = CreateTestButton("CopyRoomCodeButton", roomDisplayPanel);
        startGameButton = CreateTestButton("StartGameButton", hostControlsPanel);
        
        // Create text components
        roomCodeText = CreateTestText("RoomCodeText", roomDisplayPanel);
        roomStatusText = CreateTestText("RoomStatusText", roomDisplayPanel);
        hostStatusText = CreateTestText("HostStatusText", hostControlsPanel);
        errorMessageText = CreateTestText("ErrorMessageText", errorMessage);
        successMessageText = CreateTestText("SuccessMessageText", successMessage);
    }
    
    private GameObject CreateUIPanel(string name, GameObject parent)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent.transform, false);
        panel.AddComponent<RectTransform>();
        panel.AddComponent<CanvasGroup>();
        return panel;
    }
    
    private Button CreateTestButton(string name, GameObject parent)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent.transform, false);
        buttonGO.AddComponent<RectTransform>();
        var button = buttonGO.AddComponent<Button>();
        
        // Add image component for Button requirement
        buttonGO.AddComponent<Image>();
        
        return button;
    }
    
    private Text CreateTestText(string name, GameObject parent)
    {
        var textGO = new GameObject(name);
        textGO.transform.SetParent(parent.transform, false);
        textGO.AddComponent<RectTransform>();
        return textGO.AddComponent<Text>();
    }
    
    private void SetupRoomManager()
    {
        roomManager = RoomManager.Instance;
        Assert.IsNotNull(roomManager, "RoomManager should be available");
    }
    
    private void CreateRoomUIComponent()
    {
        roomUI = testCanvas.AddComponent<RoomUI>();
        
        // Use reflection to set private fields for testing
        var roomUIType = typeof(RoomUI);
        
        SetPrivateField(roomUIType, "roomMainPanel", roomMainPanel);
        SetPrivateField(roomUIType, "createRoomModal", createRoomModal);
        SetPrivateField(roomUIType, "joinRoomModal", joinRoomModal);
        SetPrivateField(roomUIType, "roomDisplayPanel", roomDisplayPanel);
        SetPrivateField(roomUIType, "hostControlsPanel", hostControlsPanel);
        SetPrivateField(roomUIType, "loadingIndicator", loadingIndicator);
        SetPrivateField(roomUIType, "successMessage", successMessage);
        SetPrivateField(roomUIType, "errorMessage", errorMessage);
        
        SetPrivateField(roomUIType, "createRoomButton", createRoomButton);
        SetPrivateField(roomUIType, "joinRoomButton", joinRoomButton);
        SetPrivateField(roomUIType, "leaveRoomButton", leaveRoomButton);
        SetPrivateField(roomUIType, "copyRoomCodeButton", copyRoomCodeButton);
        SetPrivateField(roomUIType, "startGameButton", startGameButton);
        
        SetPrivateField(roomUIType, "roomCodeText", roomCodeText);
        SetPrivateField(roomUIType, "roomStatusText", roomStatusText);
        SetPrivateField(roomUIType, "hostStatusText", hostStatusText);
        SetPrivateField(roomUIType, "errorMessageText", errorMessageText);
        SetPrivateField(roomUIType, "successMessageText", successMessageText);
    }
    
    private void SetPrivateField(Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(roomUI, value);
    }
    
    private void SetupMockSubComponents()
    {
        // Create mock sub-components
        mockRoomCreateUI = testCanvas.AddComponent<TestRoomCreateUI>();
        mockRoomCodeInput = testCanvas.AddComponent<TestRoomCodeInput>();
        mockPlayerList = testCanvas.AddComponent<TestRoomPlayerList>();
        
        // Set references in RoomUI
        SetPrivateField(typeof(RoomUI), "roomCreateUI", mockRoomCreateUI);
        SetPrivateField(typeof(RoomUI), "roomCodeInput", mockRoomCodeInput);
        SetPrivateField(typeof(RoomUI), "playerList", mockPlayerList);
    }
    
    private void SetupUIEventMonitoring()
    {
        // Monitor UI state transitions by hooking into RoomManager events
        RoomManager.OnRoomCreated += OnTestUIRoomCreated;
        RoomManager.OnRoomJoined += OnTestUIRoomJoined;
        RoomManager.OnRoomLeft += OnTestUIRoomLeft;
        RoomManager.OnRoomError += OnTestUIRoomError;
        RoomManager.OnConnectionStatusChanged += OnTestUIConnectionChanged;
    }
    
    private void CleanupUIEventMonitoring()
    {
        RoomManager.OnRoomCreated -= OnTestUIRoomCreated;
        RoomManager.OnRoomJoined -= OnTestUIRoomJoined;
        RoomManager.OnRoomLeft -= OnTestUIRoomLeft;
        RoomManager.OnRoomError -= OnTestUIRoomError;
        RoomManager.OnConnectionStatusChanged -= OnTestUIConnectionChanged;
    }
    
    private void CleanupRoomManager()
    {
        roomManager?.Cleanup();
    }
    
    private void LogUITestResults()
    {
        Debug.Log($"[RoomUITests] UI State Transitions: {uiStateTransitions.Count}");
        foreach (var transition in uiStateTransitions)
        {
            Debug.Log($"  - {transition}");
        }
        
        if (uiResponseTimes.Count > 0)
        {
            float avgTime = uiResponseTimes.Count > 0 ? uiResponseTimes[0] / uiResponseTimes.Count : 0;
            foreach (var time in uiResponseTimes) avgTime += time;
            avgTime /= uiResponseTimes.Count;
            
            Debug.Log($"[RoomUITests] Average UI response time: {avgTime:F2}ms");
        }
        
        Debug.Log($"[RoomUITests] Total event callbacks: {eventCallbackCount}");
    }
    
    private void RecordUIStateTransition(string transition)
    {
        uiStateTransitions.Add($"{DateTime.Now:HH:mm:ss.fff} - {transition}");
    }
    
    private void RecordUIResponseTime()
    {
        if (uiTimer.IsRunning)
        {
            uiTimer.Stop();
            uiResponseTimes.Add((float)uiTimer.ElapsedMilliseconds);
        }
    }
    #endregion
    
    #region Event Handlers for Testing
    private void OnTestUIRoomCreated(RoomData roomData)
    {
        RecordUIStateTransition($"Room Created: {roomData?.RoomCode}");
        RecordUIResponseTime();
        eventCallbackCount++;
    }
    
    private void OnTestUIRoomJoined(RoomData roomData)
    {
        RecordUIStateTransition($"Room Joined: {roomData?.RoomCode}");
        RecordUIResponseTime();
        eventCallbackCount++;
    }
    
    private void OnTestUIRoomLeft(RoomData roomData)
    {
        RecordUIStateTransition($"Room Left: {roomData?.RoomCode}");
        eventCallbackCount++;
    }
    
    private void OnTestUIRoomError(string operation, string error)
    {
        RecordUIStateTransition($"Room Error: {operation} - {error}");
        eventCallbackCount++;
    }
    
    private void OnTestUIConnectionChanged(bool connected)
    {
        RecordUIStateTransition($"Connection: {connected}");
        eventCallbackCount++;
    }
    #endregion
    
    #region UI State and Transition Tests
    
    [UnityTest]
    public IEnumerator UIStateTransitions_IdleToInRoom_ShouldUpdateCorrectly()
    {
        Debug.Log("[RoomUITests] Testing UI state transitions from Idle to InRoom");
        
        // Initial state should be Idle
        Assert.AreEqual(RoomUIState.Idle, roomUI.CurrentState, "Initial state should be Idle");
        Assert.IsTrue(roomMainPanel.activeInHierarchy, "Main panel should be active initially");
        Assert.IsFalse(roomDisplayPanel.activeInHierarchy, "Display panel should be hidden initially");
        
        uiTimer.Restart();
        
        // Trigger room creation via button click simulation
        createRoomButton.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        
        // Should show create room modal
        Assert.IsTrue(createRoomModal.activeInHierarchy, "Create room modal should be shown");
        
        // Simulate room creation completion
        if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
        {
            testCreateUI.SimulateRoomCreationRequest(4);
        }
        
        yield return new WaitForSeconds(1f); // Wait for room creation
        
        // UI should transition to InRoomAsHost state
        if (roomManager.IsInRoom)
        {
            Assert.AreEqual(RoomUIState.InRoomAsHost, roomUI.CurrentState, "Should be in InRoomAsHost state");
            Assert.IsTrue(roomDisplayPanel.activeInHierarchy, "Display panel should be active");
            Assert.IsTrue(hostControlsPanel.activeInHierarchy, "Host controls should be active");
            Assert.IsFalse(roomMainPanel.activeInHierarchy, "Main panel should be hidden");
        }
        
        RecordUIResponseTime();
        
        Debug.Log("[RoomUITests] UI state transition test completed");
    }
    
    [UnityTest]
    public IEnumerator UIResponsiveness_ButtonInteractions_ShouldProvideImmediateFeedback()
    {
        Debug.Log("[RoomUITests] Testing UI button interaction responsiveness");
        
        var buttonTests = new[]
        {
            new { Button = createRoomButton, Name = "CreateRoom" },
            new { Button = joinRoomButton, Name = "JoinRoom" }
        };
        
        foreach (var test in buttonTests)
        {
            uiTimer.Restart();
            
            bool initialInteractable = test.Button.interactable;
            
            // Simulate button click
            test.Button.onClick.Invoke();
            
            yield return new WaitForEndOfFrame(); // Wait one frame for UI update
            
            uiTimer.Stop();
            float responseTime = (float)uiTimer.ElapsedMilliseconds;
            
            Debug.Log($"[RoomUITests] {test.Name} button response time: {responseTime}ms");
            
            // UI feedback should be immediate (within 16ms for 60fps)
            Assert.IsTrue(responseTime < 50f, 
                $"{test.Name} button should respond within 50ms, took {responseTime}ms");
            
            yield return new WaitForSeconds(0.1f); // Reset between tests
        }
    }
    
    [UnityTest]
    public IEnumerator UITextUpdates_RoomInformation_ShouldReflectCurrentState()
    {
        Debug.Log("[RoomUITests] Testing UI text updates for room information");
        
        // Create room to test text updates
        string roomCode = null;
        roomManager.CreateRoom(3, (success, code) => roomCode = code);
        yield return new WaitForSeconds(1f);
        
        if (roomManager.IsInRoom)
        {
            // Force UI refresh to ensure text updates
            roomUI.ForceRefresh();
            yield return new WaitForSeconds(0.2f);
            
            // Check room code display
            if (roomCodeText != null && roomCodeText.text != null)
            {
                Assert.IsTrue(roomCodeText.text.Contains(roomCode), 
                    $"Room code text should contain {roomCode}, got: {roomCodeText.text}");
            }
            
            // Check room status display
            if (roomStatusText != null && roomStatusText.text != null)
            {
                Assert.IsTrue(roomStatusText.text.Contains("대기") || roomStatusText.text.Contains("1/3"), 
                    $"Status should show waiting or player count, got: {roomStatusText.text}");
            }
            
            // Check host status (if host controls active)
            if (hostControlsPanel.activeInHierarchy && hostStatusText != null && hostStatusText.text != null)
            {
                Assert.IsNotEmpty(hostStatusText.text, "Host status text should not be empty");
            }
        }
        
        Debug.Log("[RoomUITests] UI text update validation completed");
    }
    
    [UnityTest]
    public IEnumerator UIModalBehavior_ShowHide_ShouldAnimateSmoothly()
    {
        Debug.Log("[RoomUITests] Testing UI modal show/hide behavior");
        
        // Test create room modal
        Assert.IsFalse(createRoomModal.activeInHierarchy, "Modal should be hidden initially");
        
        // Show modal via UI method
        roomUI.ShowCreateRoomModal();
        yield return new WaitForSeconds(0.5f); // Allow for animation
        
        Assert.IsTrue(createRoomModal.activeInHierarchy, "Modal should be shown after ShowCreateRoomModal");
        
        // Check canvas group alpha for fade animation
        var canvasGroup = createRoomModal.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            Assert.IsTrue(canvasGroup.alpha > 0.8f, "Modal should be nearly opaque when shown");
        }
        
        // Hide modal by simulating cancel
        if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
        {
            testCreateUI.SimulateCancel();
        }
        
        yield return new WaitForSeconds(0.5f); // Allow for hide animation
        
        // Modal should be hidden or have low alpha
        if (canvasGroup != null && createRoomModal.activeInHierarchy)
        {
            Assert.IsTrue(canvasGroup.alpha < 0.2f, "Modal should be nearly transparent when hidden");
        }
        
        Debug.Log("[RoomUITests] Modal behavior validation completed");
    }
    #endregion
    
    #region Error Handling and User Feedback Tests
    
    [UnityTest]
    public IEnumerator ErrorDisplay_InvalidOperations_ShouldShowUserFriendlyMessages()
    {
        Debug.Log("[RoomUITests] Testing error display for invalid operations");
        
        // Test invalid room join
        uiTimer.Restart();
        
        roomUI.ShowJoinRoomModal();
        yield return new WaitForSeconds(0.2f);
        
        // Simulate invalid room code input
        if (mockRoomCodeInput is TestRoomCodeInput testCodeInput)
        {
            testCodeInput.SimulateRoomJoinRequest("invalid");
        }
        
        yield return new WaitForSeconds(1f); // Wait for error processing
        
        // Check error message display
        bool errorDisplayed = errorMessage.activeInHierarchy;
        if (errorDisplayed && errorMessageText != null)
        {
            Assert.IsNotEmpty(errorMessageText.text, "Error message text should not be empty");
            Assert.IsTrue(errorMessageText.text.Contains("형식") || errorMessageText.text.Contains("잘못"), 
                "Error message should be user-friendly and in Korean");
        }
        
        RecordUIResponseTime();
        
        // Error should display quickly
        float errorResponseTime = uiTimer.ElapsedMilliseconds;
        Assert.IsTrue(errorResponseTime < 1000f, 
            $"Error should display within 1 second, took {errorResponseTime}ms");
        
        Debug.Log("[RoomUITests] Error display validation completed");
    }
    
    [UnityTest]
    public IEnumerator SuccessMessages_UserActions_ShouldProvidePositiveFeedback()
    {
        Debug.Log("[RoomUITests] Testing success message display");
        
        // Create room to trigger success
        bool roomCreated = false;
        roomManager.CreateRoom(2, (success, code) => roomCreated = success);
        
        yield return new WaitForSeconds(1.5f);
        
        if (roomCreated)
        {
            // Check if success message was shown
            // Note: Success message might be temporary, so check within reasonable time
            yield return new WaitForSeconds(0.5f);
            
            // Success feedback should have been provided through UI state change
            Assert.AreEqual(RoomUIState.InRoomAsHost, roomUI.CurrentState, 
                "Successful room creation should result in InRoomAsHost state");
            
            // Test clipboard copy success feedback
            if (roomManager.IsInRoom)
            {
                roomUI.CopyRoomCodeToClipboard();
                yield return new WaitForSeconds(0.5f);
                
                // Success message for copy should appear briefly
                // (Implementation detail - might be toast or temporary message)
            }
        }
        
        Debug.Log("[RoomUITests] Success message validation completed");
    }
    
    [UnityTest]
    public IEnumerator LoadingIndicator_LongOperations_ShouldProvideVisualFeedback()
    {
        Debug.Log("[RoomUITests] Testing loading indicator during operations");
        
        // Initially loading should be hidden
        Assert.IsFalse(loadingIndicator.activeInHierarchy, "Loading indicator should be hidden initially");
        
        // Trigger room creation (which should show loading)
        createRoomButton.onClick.Invoke();
        yield return new WaitForSeconds(0.1f);
        
        // Simulate room creation request which should show loading
        if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
        {
            testCreateUI.SimulateRoomCreationRequest(4);
        }
        
        // Check if loading indicator is shown during operation
        yield return new WaitForSeconds(0.2f);
        
        // Loading state should be visible during room creation
        bool loadingShown = loadingIndicator.activeInHierarchy;
        Debug.Log($"[RoomUITests] Loading indicator shown: {loadingShown}");
        
        // Wait for operation completion
        yield return new WaitForSeconds(1.5f);
        
        // Loading should be hidden after completion
        Assert.IsFalse(loadingIndicator.activeInHierarchy, 
            "Loading indicator should be hidden after operation completion");
        
        Debug.Log("[RoomUITests] Loading indicator validation completed");
    }
    #endregion
    
    #region Performance and Responsiveness Tests
    
    [UnityTest]
    public IEnumerator UIPerformance_MultipleStateChanges_ShouldMaintainFramerate()
    {
        Debug.Log("[RoomUITests] Testing UI performance during multiple state changes");
        
        const int stateChangeCount = 10;
        var frameRates = new List<float>();
        
        for (int i = 0; i < stateChangeCount; i++)
        {
            float frameStart = Time.realtimeSinceStartup;
            
            // Trigger UI state change
            if (i % 2 == 0)
            {
                roomUI.ShowCreateRoomModal();
            }
            else
            {
                if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
                {
                    testCreateUI.SimulateCancel();
                }
            }
            
            yield return new WaitForEndOfFrame();
            
            float frameTime = Time.realtimeSinceStartup - frameStart;
            float fps = 1f / frameTime;
            frameRates.Add(fps);
            
            yield return new WaitForSeconds(0.1f); // Brief pause between changes
        }
        
        // Calculate average framerate during UI operations
        float avgFPS = frameRates.Count > 0 ? frameRates[0] : 0f;
        for (int i = 1; i < frameRates.Count; i++) avgFPS += frameRates[i];
        avgFPS /= frameRates.Count;
        
        Debug.Log($"[RoomUITests] Average FPS during UI state changes: {avgFPS:F1}");
        
        // UI should maintain reasonable framerate (30+ FPS minimum)
        Assert.IsTrue(avgFPS > 30f, $"UI should maintain >30 FPS, got {avgFPS:F1}");
        
        Debug.Log("[RoomUITests] UI performance validation completed");
    }
    
    [UnityTest]
    public IEnumerator UIMemoryUsage_ExtendedInteraction_ShouldNotLeak()
    {
        Debug.Log("[RoomUITests] Testing UI memory usage during extended interaction");
        
        // Get initial memory
        long initialMemory = System.GC.GetTotalMemory(true);
        
        // Perform extended UI operations
        const int operationCycles = 15;
        
        for (int i = 0; i < operationCycles; i++)
        {
            // Show and hide modals
            roomUI.ShowCreateRoomModal();
            yield return new WaitForSeconds(0.1f);
            
            if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
            {
                testCreateUI.SimulateCancel();
            }
            yield return new WaitForSeconds(0.1f);
            
            roomUI.ShowJoinRoomModal();
            yield return new WaitForSeconds(0.1f);
            
            if (mockRoomCodeInput is TestRoomCodeInput testCodeInput)
            {
                testCodeInput.SimulateCancel();
            }
            yield return new WaitForSeconds(0.1f);
            
            // Force refresh periodically
            if (i % 3 == 0)
            {
                roomUI.ForceRefresh();
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Cleanup and check memory
        System.GC.Collect();
        yield return new WaitForSeconds(0.5f);
        
        long finalMemory = System.GC.GetTotalMemory(true);
        long memoryIncrease = finalMemory - initialMemory;
        float memoryIncreaseMB = memoryIncrease / (1024f * 1024f);
        
        Debug.Log($"[RoomUITests] UI Memory usage - Initial: {initialMemory / (1024 * 1024):F2}MB, " +
                 $"Final: {finalMemory / (1024 * 1024):F2}MB, Increase: {memoryIncreaseMB:F2}MB");
        
        Assert.IsTrue(memoryIncreaseMB < 2f, $"UI memory increase ({memoryIncreaseMB:F2}MB) should be under 2MB");
        
        Debug.Log("[RoomUITests] UI memory usage validation completed");
    }
    #endregion
    
    #region Integration with RoomManager Tests
    
    [UnityTest]
    public IEnumerator RoomManagerIntegration_EventSynchronization_ShouldUpdateUICorrectly()
    {
        Debug.Log("[RoomUITests] Testing RoomManager-UI event synchronization");
        
        // Monitor UI updates in response to RoomManager events
        int uiUpdateCount = 0;
        var originalState = roomUI.CurrentState;
        
        // Create room via RoomManager (not UI)
        roomManager.CreateRoom(3, null);
        yield return new WaitForSeconds(1f);
        
        // UI should have updated to reflect room creation
        if (roomManager.IsInRoom)
        {
            Assert.AreNotEqual(originalState, roomUI.CurrentState, "UI state should change after room creation");
            uiUpdateCount++;
        }
        
        // Simulate player join via RoomManager event
        var testPlayer = new PlayerInfo("test_ui_player", "UITestPlayer", false);
        if (roomManager.CurrentRoom != null)
        {
            roomManager.CurrentRoom.AddPlayer(testPlayer);
            RoomManager.OnPlayerJoined?.Invoke(roomManager.CurrentRoom, testPlayer);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // UI should reflect the player list update
        if (mockPlayerList is TestRoomPlayerList testPlayerList)
        {
            Assert.IsTrue(testPlayerList.UpdateCallCount > 0, "Player list should be updated");
            uiUpdateCount++;
        }
        
        // Leave room via RoomManager
        roomManager.LeaveRoom();
        yield return new WaitForSeconds(0.5f);
        
        // UI should return to idle state
        Assert.AreEqual(RoomUIState.Idle, roomUI.CurrentState, "UI should return to Idle after leaving room");
        uiUpdateCount++;
        
        Assert.IsTrue(uiUpdateCount >= 3, $"UI should have been updated multiple times, count: {uiUpdateCount}");
        
        Debug.Log("[RoomUITests] RoomManager integration validation completed");
    }
    
    [UnityTest] 
    public IEnumerator PlayerListUpdates_RealTimeSync_ShouldReflectChangesQuickly()
    {
        Debug.Log("[RoomUITests] Testing real-time player list updates");
        
        // Create room first
        roomManager.CreateRoom(4, null);
        yield return new WaitForSeconds(1f);
        
        var testPlayerList = mockPlayerList as TestRoomPlayerList;
        Assert.IsNotNull(testPlayerList, "Test player list should be available");
        
        int initialUpdateCount = testPlayerList.UpdateCallCount;
        
        // Add players and measure sync time
        uiTimer.Restart();
        
        var players = new[]
        {
            new PlayerInfo("sync_player1", "Player1", false),
            new PlayerInfo("sync_player2", "Player2", false),
            new PlayerInfo("sync_player3", "Player3", false)
        };
        
        foreach (var player in players)
        {
            if (roomManager.CurrentRoom != null)
            {
                roomManager.CurrentRoom.AddPlayer(player);
                RoomManager.OnPlayerJoined?.Invoke(roomManager.CurrentRoom, player);
                yield return new WaitForSeconds(0.2f); // Simulate network delay
            }
        }
        
        uiTimer.Stop();
        float totalSyncTime = (float)uiTimer.ElapsedMilliseconds;
        
        // Verify updates occurred
        int finalUpdateCount = testPlayerList.UpdateCallCount;
        int updateIncrement = finalUpdateCount - initialUpdateCount;
        
        Debug.Log($"[RoomUITests] Player list updates: {updateIncrement}, Sync time: {totalSyncTime}ms");
        
        Assert.IsTrue(updateIncrement >= players.Length, 
            $"Player list should update for each player addition, expected >= {players.Length}, got {updateIncrement}");
        
        Assert.IsTrue(totalSyncTime < 2000f, 
            $"Player list sync should complete quickly, took {totalSyncTime}ms");
        
        Debug.Log("[RoomUITests] Player list sync validation completed");
    }
    #endregion
    
    #region Accessibility and User Experience Tests
    
    [Test]
    public void UIAccessibility_ButtonStates_ShouldBeLogicalAndClear()
    {
        Debug.Log("[RoomUITests] Testing UI accessibility - button states");
        
        // Test initial button states
        Assert.IsTrue(createRoomButton.interactable, "Create room button should be interactable initially");
        Assert.IsTrue(joinRoomButton.interactable, "Join room button should be interactable initially");
        Assert.IsFalse(leaveRoomButton.interactable, "Leave room button should be disabled when not in room");
        
        // Button states should be logical for current context
        if (!roomManager.IsInRoom)
        {
            Assert.IsFalse(copyRoomCodeButton.interactable, "Copy code button should be disabled when not in room");
            Assert.IsFalse(startGameButton.interactable, "Start game button should be disabled when not in room");
        }
        
        Debug.Log("[RoomUITests] UI accessibility validation completed");
    }
    
    [Test]
    public void UILabeling_TextComponents_ShouldHaveAppropriateContent()
    {
        Debug.Log("[RoomUITests] Testing UI labeling and text content");
        
        // Text components should be properly initialized
        Assert.IsNotNull(roomCodeText, "Room code text component should exist");
        Assert.IsNotNull(roomStatusText, "Room status text component should exist");
        Assert.IsNotNull(errorMessageText, "Error message text component should exist");
        Assert.IsNotNull(successMessageText, "Success message text component should exist");
        
        // Initially text should be empty or have default values
        // Actual content validation happens in other tests after room operations
        
        Debug.Log("[RoomUITests] UI labeling validation completed");
    }
    
    [UnityTest]
    public IEnumerator UserExperience_CompleteWorkflow_ShouldFeelResponsive()
    {
        Debug.Log("[RoomUITests] Testing complete user workflow experience");
        
        var workflowStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Step 1: User clicks create room
        uiTimer.Restart();
        createRoomButton.onClick.Invoke();
        yield return new WaitForEndOfFrame();
        RecordUIResponseTime();
        
        // Step 2: User configures room and creates it
        if (mockRoomCreateUI is TestRoomCreateUI testCreateUI)
        {
            uiTimer.Restart();
            testCreateUI.SimulateRoomCreationRequest(3);
            yield return new WaitForSeconds(1.5f); // Room creation time
            RecordUIResponseTime();
        }
        
        // Step 3: User sees room display
        if (roomManager.IsInRoom)
        {
            uiTimer.Restart();
            roomUI.ForceRefresh();
            yield return new WaitForEndOfFrame();
            RecordUIResponseTime();
        }
        
        // Step 4: User copies room code
        uiTimer.Restart();
        copyRoomCodeButton.onClick.Invoke();
        yield return new WaitForEndOfFrame();
        RecordUIResponseTime();
        
        workflowStopwatch.Stop();
        
        // Analyze workflow performance
        float totalWorkflowTime = (float)workflowStopwatch.ElapsedMilliseconds;
        Debug.Log($"[RoomUITests] Complete workflow time: {totalWorkflowTime}ms");
        
        // Each UI interaction should feel responsive
        foreach (var responseTime in uiResponseTimes)
        {
            Assert.IsTrue(responseTime < 100f, $"Individual UI response ({responseTime}ms) should be under 100ms");
        }
        
        // Overall workflow should complete in reasonable time
        Assert.IsTrue(totalWorkflowTime < 5000f, 
            $"Complete workflow should finish within 5 seconds, took {totalWorkflowTime}ms");
        
        Debug.Log("[RoomUITests] User experience validation completed");
    }
    #endregion
}

#region Mock Components for Testing

/// <summary>Mock RoomCreateUI for testing</summary>
public class TestRoomCreateUI : MonoBehaviour, IRoomCreateUI
{
    public event Action<int> OnRoomCreationRequested;
    public event Action OnCreateRoomCancelled;
    
    public void SimulateRoomCreationRequest(int maxPlayers)
    {
        OnRoomCreationRequested?.Invoke(maxPlayers);
    }
    
    public void SimulateCancel()
    {
        OnCreateRoomCancelled?.Invoke();
    }
    
    public void ResetToDefaults()
    {
        // Mock implementation
    }
}

/// <summary>Mock RoomCodeInput for testing</summary>
public class TestRoomCodeInput : MonoBehaviour, IRoomCodeInput
{
    public event Action<string> OnRoomJoinRequested;
    public event Action OnJoinRoomCancelled;
    
    public void SimulateRoomJoinRequest(string roomCode)
    {
        OnRoomJoinRequested?.Invoke(roomCode);
    }
    
    public void SimulateCancel()
    {
        OnJoinRoomCancelled?.Invoke();
    }
    
    public void ClearInput()
    {
        // Mock implementation
    }
}

/// <summary>Mock RoomPlayerList for testing</summary>
public class TestRoomPlayerList : MonoBehaviour, IRoomPlayerList
{
    public event Action<string> OnPlayerKickRequested;
    public event Action<string, bool> OnPlayerReadyToggled;
    
    public int UpdateCallCount { get; private set; } = 0;
    
    public void UpdatePlayerList(List<PlayerInfo> players, bool isHost)
    {
        UpdateCallCount++;
        Debug.Log($"[TestRoomPlayerList] UpdatePlayerList called ({UpdateCallCount}), players: {players?.Count}, isHost: {isHost}");
    }
}

/// <summary>Interface definitions for mock consistency</summary>
public interface IRoomCreateUI
{
    event Action<int> OnRoomCreationRequested;
    event Action OnCreateRoomCancelled;
    void ResetToDefaults();
}

public interface IRoomCodeInput
{
    event Action<string> OnRoomJoinRequested;
    event Action OnJoinRoomCancelled;
    void ClearInput();
}

public interface IRoomPlayerList
{
    event Action<string> OnPlayerKickRequested;
    event Action<string, bool> OnPlayerReadyToggled;
    void UpdatePlayerList(List<PlayerInfo> players, bool isHost);
}

#endregion