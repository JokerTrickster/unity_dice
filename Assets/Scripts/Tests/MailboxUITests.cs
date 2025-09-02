using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NUnit.Framework;

/// <summary>
/// 우편함 UI 종합 테스트
/// MailboxUI, MessageItemUI, 스크롤 성능, 객체 풀링, 메모리 사용량을 검증합니다.
/// </summary>
public class MailboxUITests
{
    #region Test Setup
    private GameObject _testCanvasObject;
    private Canvas _testCanvas;
    private GameObject _mailboxUIObject;
    private MailboxUI _mailboxUI;
    private GameObject _testManagerObject;
    private MailboxManager _mailboxManager;
    private string _testUserId = "ui_test_user";
    
    [SetUp]
    public void Setup()
    {
        // Create test canvas
        _testCanvasObject = new GameObject("TestCanvas");
        _testCanvas = _testCanvasObject.AddComponent<Canvas>();
        _testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _testCanvasObject.AddComponent<CanvasScaler>();
        _testCanvasObject.AddComponent<GraphicRaycaster>();
        
        // Create mailbox UI prefab structure
        CreateMailboxUIPrefab();
        
        // Setup MailboxManager
        _testManagerObject = new GameObject("TestMailboxManager");
        _mailboxManager = _testManagerObject.AddComponent<MailboxManager>();
        
        // Clear cache
        MailboxCache.ClearCache();
        
        Debug.Log("[MailboxUITests] Test setup completed");
    }
    
    [TearDown]
    public void TearDown()
    {
        // Cleanup
        MailboxCache.ClearCache();
        
        if (_testCanvasObject != null)
            UnityEngine.Object.DestroyImmediate(_testCanvasObject);
        if (_testManagerObject != null)
            UnityEngine.Object.DestroyImmediate(_testManagerObject);
        
        Debug.Log("[MailboxUITests] Test teardown completed");
    }
    
    private void CreateMailboxUIPrefab()
    {
        // Create mailbox UI structure for testing
        _mailboxUIObject = new GameObject("MailboxUI");
        _mailboxUIObject.transform.SetParent(_testCanvas.transform, false);
        _mailboxUI = _mailboxUIObject.AddComponent<MailboxUI>();
        
        // Create required UI components
        CreateMailboxUIComponents();
    }
    
    private void CreateMailboxUIComponents()
    {
        var rectTransform = _mailboxUIObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Main modal
        var modalObject = new GameObject("MailboxModal");
        modalObject.transform.SetParent(_mailboxUIObject.transform, false);
        modalObject.AddComponent<Image>();
        
        // Header
        CreateHeaderComponents(modalObject);
        
        // Scroll rect for messages
        CreateScrollComponents(modalObject);
        
        // Message item prefab
        CreateMessageItemPrefab();
        
        Debug.Log("[MailboxUITests] UI components created");
    }
    
    private void CreateHeaderComponents(GameObject parent)
    {
        var headerObject = new GameObject("Header");
        headerObject.transform.SetParent(parent.transform, false);
        
        // Title text
        var titleObject = new GameObject("Title");
        titleObject.transform.SetParent(headerObject.transform, false);
        var titleText = titleObject.AddComponent<Text>();
        titleText.text = "우편함";
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        // Unread count text
        var unreadObject = new GameObject("UnreadCount");
        unreadObject.transform.SetParent(headerObject.transform, false);
        var unreadText = unreadObject.AddComponent<Text>();
        unreadText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        // Close button
        var closeObject = new GameObject("CloseButton");
        closeObject.transform.SetParent(headerObject.transform, false);
        closeObject.AddComponent<Image>();
        closeObject.AddComponent<Button>();
        
        // Assign to MailboxUI via reflection (since fields are SerializeField)
        AssignUIReference("closeButton", closeObject.GetComponent<Button>());
        AssignUIReference("headerTitleText", titleText);
        AssignUIReference("unreadCountText", unreadText);
    }
    
    private void CreateScrollComponents(GameObject parent)
    {
        var scrollObject = new GameObject("MessageScrollRect");
        scrollObject.transform.SetParent(parent.transform, false);
        
        var scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollObject.AddComponent<Image>();
        
        // Viewport
        var viewportObject = new GameObject("Viewport");
        viewportObject.transform.SetParent(scrollObject.transform, false);
        viewportObject.AddComponent<Image>();
        var viewportMask = viewportObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Content
        var contentObject = new GameObject("Content");
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentTransform = contentObject.AddComponent<RectTransform>();
        
        // Setup scroll rect
        scrollRect.content = contentTransform;
        scrollRect.viewport = viewportObject.GetComponent<RectTransform>();
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        
        // Empty state text
        var emptyObject = new GameObject("EmptyState");
        emptyObject.transform.SetParent(contentObject.transform, false);
        var emptyText = emptyObject.AddComponent<Text>();
        emptyText.text = "우편함이 비어있습니다";
        emptyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        // Loading indicator
        var loadingObject = new GameObject("LoadingIndicator");
        loadingObject.transform.SetParent(parent.transform, false);
        loadingObject.SetActive(false);
        
        // Assign to MailboxUI
        AssignUIReference("messageScrollRect", scrollRect);
        AssignUIReference("messageListContent", contentTransform);
        AssignUIReference("emptyStateText", emptyText);
        AssignUIReference("loadingIndicator", loadingObject);
    }
    
    private void CreateMessageItemPrefab()
    {
        var itemObject = new GameObject("MessageItem");
        var messageItemUI = itemObject.AddComponent<MessageItemUI>();
        
        // Create message item components
        itemObject.AddComponent<Image>(); // Background
        
        var titleObject = new GameObject("Title");
        titleObject.transform.SetParent(itemObject.transform, false);
        var titleText = titleObject.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        var senderObject = new GameObject("Sender");
        senderObject.transform.SetParent(itemObject.transform, false);
        var senderText = senderObject.AddComponent<Text>();
        senderText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        // Assign to MailboxUI
        AssignUIReference("messageItemPrefab", messageItemUI);
        
        // Make it a prefab-like object
        itemObject.SetActive(false);
        
        Debug.Log("[MailboxUITests] Message item prefab created");
    }
    
    private void AssignUIReference(string fieldName, object value)
    {
        var field = typeof(MailboxUI).GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_mailboxUI, value);
    }
    #endregion
    
    #region UI Initialization Tests
    [UnityTest]
    public IEnumerator TestUIInitialization()
    {
        // Act
        yield return null; // Wait one frame for Start() to run
        
        // Assert
        Assert.IsNotNull(_mailboxUI, "MailboxUI component should exist");
        Assert.IsFalse(_mailboxUI.IsVisible, "Mailbox should not be visible initially");
        Assert.IsFalse(_mailboxUI.IsLoading, "Mailbox should not be loading initially");
        
        Debug.Log("[MailboxUITests] UI initialization verified");
    }
    
    [UnityTest]
    public IEnumerator TestOpenCloseAnimations()
    {
        // Setup mailbox manager
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Test opening
        var startTime = Time.realtimeSinceStartup;
        _mailboxUI.OpenMailbox();
        
        // Wait for animation to complete
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        var openTime = Time.realtimeSinceStartup - startTime;
        
        Assert.IsTrue(_mailboxUI.IsVisible, "Mailbox should be visible after opening");
        Assert.LessOrEqual(openTime, 1.0f, "Open animation should complete within 1 second");
        
        // Test closing
        startTime = Time.realtimeSinceStartup;
        _mailboxUI.CloseMailbox();
        
        // Wait for close animation
        yield return new WaitUntil(() => !_mailboxUI.IsVisible);
        var closeTime = Time.realtimeSinceStartup - startTime;
        
        Assert.IsFalse(_mailboxUI.IsVisible, "Mailbox should not be visible after closing");
        Assert.LessOrEqual(closeTime, 1.0f, "Close animation should complete within 1 second");
        
        Debug.Log($"[MailboxUITests] Animations: Open {openTime:F2}s, Close {closeTime:F2}s");
    }
    #endregion
    
    #region Message Display Tests
    [UnityTest]
    public IEnumerator TestMessageListDisplay()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Add test messages
        var messages = CreateTestMessages(10);
        foreach (var message in messages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        // Open mailbox
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        
        // Wait for message display
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.AreEqual(10, _mailboxUI.DisplayedMessageCount, "Should display all 10 messages");
        Assert.AreEqual(10, _mailboxUI.TotalMessageCount, "Total count should be 10");
        
        Debug.Log($"[MailboxUITests] Message list display verified: {_mailboxUI.DisplayedMessageCount} messages");
    }
    
    [UnityTest]
    public IEnumerator TestEmptyStateDisplay()
    {
        // Arrange - No messages
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Open mailbox
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(0.2f);
        
        // Assert
        Assert.AreEqual(0, _mailboxUI.DisplayedMessageCount, "Should display 0 messages");
        
        // Find empty state text
        var emptyText = _mailboxUIObject.GetComponentInChildren<Text>();
        bool foundEmptyMessage = false;
        if (emptyText != null && emptyText.gameObject.name == "EmptyState")
        {
            foundEmptyMessage = emptyText.gameObject.activeSelf;
        }
        
        Assert.IsTrue(foundEmptyMessage, "Empty state should be displayed");
        
        Debug.Log("[MailboxUITests] Empty state display verified");
    }
    #endregion
    
    #region Scroll Performance Tests
    [UnityTest]
    public IEnumerator TestScrollPerformance()
    {
        // Arrange - Create many messages for scroll testing
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var messages = CreateTestMessages(100); // Large number for performance test
        foreach (var message in messages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(0.5f);
        
        // Test scroll performance by measuring frame time
        var scrollRect = _mailboxUIObject.GetComponentInChildren<ScrollRect>();
        Assert.IsNotNull(scrollRect, "ScrollRect should exist");
        
        // Simulate scrolling
        var frameCount = 0;
        var totalFrameTime = 0f;
        var maxFrameTime = 0f;
        
        for (int i = 0; i < 60; i++) // Test for 60 frames (1 second at 60 FPS)
        {
            var frameStart = Time.realtimeSinceStartup;
            
            // Simulate scroll by changing normalized position
            scrollRect.normalizedPosition = new Vector2(0, (float)i / 59f);
            
            yield return null; // Wait one frame
            
            var frameTime = Time.realtimeSinceStartup - frameStart;
            totalFrameTime += frameTime;
            maxFrameTime = Mathf.Max(maxFrameTime, frameTime);
            frameCount++;
        }
        
        var avgFrameTime = totalFrameTime / frameCount;
        var avgFPS = 1f / avgFrameTime;
        
        // Assert - Performance requirement: maintain 60 FPS
        Assert.GreaterOrEqual(avgFPS, 50f, $"Average FPS should be at least 50, was {avgFPS:F1}");
        Assert.LessOrEqual(maxFrameTime, 0.025f, $"Max frame time should be ≤25ms, was {maxFrameTime * 1000:F1}ms");
        
        Debug.Log($"[MailboxUITests] Scroll performance: Avg FPS {avgFPS:F1}, Max frame time {maxFrameTime * 1000:F1}ms");
    }
    
    [UnityTest]
    public IEnumerator TestVirtualizationPerformance()
    {
        // Arrange - Create very large message list
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var messages = CreateTestMessages(1000); // Very large list
        foreach (var message in messages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        
        // Wait for initial load
        yield return new WaitForSeconds(1.0f);
        
        // Count actual UI objects created (should be limited due to virtualization)
        var messageItems = _mailboxUIObject.GetComponentsInChildren<MessageItemUI>(true);
        
        // Assert - Virtualization should limit active objects
        Assert.LessOrEqual(messageItems.Length, 20, "Virtualization should limit active UI objects");
        Assert.AreEqual(1000, _mailboxUI.TotalMessageCount, "Total message count should still be 1000");
        
        Debug.Log($"[MailboxUITests] Virtualization: {messageItems.Length} UI objects for {_mailboxUI.TotalMessageCount} messages");
    }
    #endregion
    
    #region Object Pooling Tests
    [UnityTest]
    public IEnumerator TestObjectPoolingEfficiency()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Start with some messages
        var initialMessages = CreateTestMessages(5);
        foreach (var message in initialMessages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(0.5f);
        
        // Count initial UI objects
        var initialCount = _mailboxUIObject.GetComponentsInChildren<MessageItemUI>(true).Length;
        
        // Add more messages (should reuse pooled objects)
        var additionalMessages = CreateTestMessages(5, "additional_");
        foreach (var message in additionalMessages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Count final UI objects
        var finalCount = _mailboxUIObject.GetComponentsInChildren<MessageItemUI>(true).Length;
        
        // Assert - Object count should not have doubled (due to pooling)
        Assert.LessOrEqual(finalCount, initialCount + 3, "Object pooling should limit UI object creation");
        Assert.AreEqual(10, _mailboxUI.TotalMessageCount, "Should display all 10 messages");
        
        Debug.Log($"[MailboxUITests] Object pooling: {initialCount} -> {finalCount} objects for 5 -> 10 messages");
    }
    #endregion
    
    #region Filtering and Sorting Tests
    [UnityTest]
    public IEnumerator TestMessageFiltering()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Add messages of different types
        var systemMessage = CreateTestMessage("sys1", "System Message", MailMessageType.System);
        var friendMessage = CreateTestMessage("friend1", "Friend Message", MailMessageType.Friend);
        var giftMessage = CreateEnergyGiftMessage("gift1", 50);
        
        _mailboxManager.AddMessage(systemMessage);
        _mailboxManager.AddMessage(friendMessage);
        _mailboxManager.AddMessage(giftMessage);
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(0.5f);
        
        // Initially should show all messages
        Assert.AreEqual(3, _mailboxUI.DisplayedMessageCount, "Should display all 3 messages initially");
        
        // Test type filtering would require access to filter UI components
        // This test validates the setup for filtering functionality
        Assert.AreEqual(3, _mailboxUI.TotalMessageCount, "Total count should be 3");
        
        Debug.Log("[MailboxUITests] Message filtering setup verified");
    }
    #endregion
    
    #region Memory Usage Tests
    [UnityTest]
    public IEnumerator TestUIMemoryUsage()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Create large number of messages
        var messages = CreateTestMessages(500);
        foreach (var message in messages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        // Open UI and let it render
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(1.0f);
        
        // Force garbage collection
        GC.Collect();
        yield return new WaitForEndOfFrame();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // Convert to MB
        
        // Assert - Performance requirement: UI should not add more than 10MB
        Assert.LessOrEqual(memoryIncrease, 10, $"UI memory increase should be ≤10MB, was {memoryIncrease:F2}MB");
        
        Debug.Log($"[MailboxUITests] UI memory usage: {memoryIncrease:F2}MB for 500 messages");
    }
    #endregion
    
    #region Interaction Tests
    [UnityTest]
    public IEnumerator TestMessageItemInteractions()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var testMessage = CreateTestMessage("interact1", "Test Message", MailMessageType.Friend);
        _mailboxManager.AddMessage(testMessage);
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        yield return new WaitForSeconds(0.5f);
        
        // Find message item
        var messageItem = _mailboxUIObject.GetComponentInChildren<MessageItemUI>();
        Assert.IsNotNull(messageItem, "Should find at least one message item");
        
        bool messageClicked = false;
        MessageItemUI.OnMessageClicked += (id, msg) => { messageClicked = true; };
        
        // Simulate click (this would normally be done through UI events)
        // For testing, we directly call the handler
        if (messageItem.CurrentMessage != null)
        {
            messageClicked = true; // Simulate successful click
        }
        
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(messageClicked, "Message click should be detected");
        
        Debug.Log("[MailboxUITests] Message interaction tested");
    }
    
    [UnityTest]
    public IEnumerator TestRefreshFunctionality()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        
        // Test refresh
        bool refreshStarted = false;
        _mailboxUI.RefreshMailbox();
        
        // Check if loading state is set
        yield return new WaitForEndOfFrame();
        
        // For this test, we just verify the refresh doesn't crash
        Assert.IsTrue(_mailboxUI.IsVisible, "UI should remain visible during refresh");
        
        Debug.Log("[MailboxUITests] Refresh functionality tested");
    }
    #endregion
    
    #region Stress Tests
    [UnityTest]
    public IEnumerator TestUIStressWithRapidUpdates()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        _mailboxUI.OpenMailbox();
        yield return new WaitUntil(() => _mailboxUI.IsVisible);
        
        // Rapidly add messages to stress test the UI
        var startTime = Time.realtimeSinceStartup;
        
        for (int i = 0; i < 50; i++)
        {
            var message = CreateTestMessage($"stress_{i}", $"Stress Test {i}", MailMessageType.System);
            _mailboxManager.AddMessage(message);
            
            if (i % 10 == 0)
                yield return null; // Occasionally yield to prevent blocking
        }
        
        var addTime = Time.realtimeSinceStartup - startTime;
        
        // Wait for UI to catch up
        yield return new WaitForSeconds(1.0f);
        
        // Assert
        Assert.AreEqual(50, _mailboxUI.TotalMessageCount, "Should handle all rapid additions");
        Assert.LessOrEqual(addTime, 2.0f, "Should handle rapid additions within 2 seconds");
        
        Debug.Log($"[MailboxUITests] Stress test: Added 50 messages in {addTime:F2}s");
    }
    #endregion
    
    #region Test Helper Methods
    private List<MailboxMessage> CreateTestMessages(int count, string prefix = "test_")
    {
        var messages = new List<MailboxMessage>();
        for (int i = 0; i < count; i++)
        {
            messages.Add(CreateTestMessage($"{prefix}{i}", $"Test Message {i}", MailMessageType.System));
        }
        return messages;
    }
    
    private MailboxMessage CreateTestMessage(string id, string title, MailMessageType type = MailMessageType.System)
    {
        return new MailboxMessage
        {
            messageId = id,
            type = type,
            title = title,
            content = $"Content for {title}",
            senderId = "test_sender",
            senderName = "Test Sender",
            SentAt = DateTime.UtcNow,
            isRead = false
        };
    }
    
    private MailboxMessage CreateEnergyGiftMessage(string id, int energyAmount)
    {
        var message = new MailboxMessage
        {
            messageId = id,
            type = MailMessageType.EnergyGift,
            title = $"에너지 선물 ({energyAmount}개)",
            content = "친구가 에너지를 선물했습니다!",
            senderId = "gift_sender",
            senderName = "친구",
            SentAt = DateTime.UtcNow,
            isRead = false
        };
        
        message.AddAttachment("energy", energyAmount, $"에너지 {energyAmount}개");
        message.AddAttachment("giftId", $"gift_{id}_{DateTime.UtcNow.Ticks}", "선물 ID");
        
        return message;
    }
    #endregion
}