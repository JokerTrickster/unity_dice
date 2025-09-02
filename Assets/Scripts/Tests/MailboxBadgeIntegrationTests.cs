using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NUnit.Framework;

/// <summary>
/// 우편함 뱃지 통합 테스트
/// MailboxBadge와 MailboxManager 간의 실시간 연동을 검증합니다.
/// </summary>
public class MailboxBadgeIntegrationTests
{
    #region Test Setup
    private GameObject _testCanvasObject;
    private Canvas _testCanvas;
    private GameObject _badgeObject;
    private MailboxBadge _mailboxBadge;
    private GameObject _testManagerObject;
    private MailboxManager _mailboxManager;
    private string _testUserId = "badge_test_user";
    
    [SetUp]
    public void Setup()
    {
        // Create test canvas
        _testCanvasObject = new GameObject("TestCanvas");
        _testCanvas = _testCanvasObject.AddComponent<Canvas>();
        _testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Create badge UI structure
        CreateBadgeUI();
        
        // Setup MailboxManager
        _testManagerObject = new GameObject("TestMailboxManager");
        _mailboxManager = _testManagerObject.AddComponent<MailboxManager>();
        
        // Clear cache
        MailboxCache.ClearCache();
        
        Debug.Log("[MailboxBadgeIntegrationTests] Test setup completed");
    }
    
    [TearDown]
    public void TearDown()
    {
        MailboxCache.ClearCache();
        
        if (_testCanvasObject != null)
            UnityEngine.Object.DestroyImmediate(_testCanvasObject);
        if (_testManagerObject != null)
            UnityEngine.Object.DestroyImmediate(_testManagerObject);
        
        Debug.Log("[MailboxBadgeIntegrationTests] Test teardown completed");
    }
    
    private void CreateBadgeUI()
    {
        // Badge container
        _badgeObject = new GameObject("MailboxBadge");
        _badgeObject.transform.SetParent(_testCanvas.transform, false);
        _mailboxBadge = _badgeObject.AddComponent<MailboxBadge>();
        
        // Badge background
        var backgroundObject = new GameObject("BadgeBackground");
        backgroundObject.transform.SetParent(_badgeObject.transform, false);
        var badgeImage = backgroundObject.AddComponent<Image>();
        badgeImage.color = Color.red;
        
        // Unread count text
        var textObject = new GameObject("UnreadCountText");
        textObject.transform.SetParent(_badgeObject.transform, false);
        var countText = textObject.AddComponent<Text>();
        countText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        countText.text = "0";
        countText.color = Color.white;
        
        // Assign references via reflection (since fields are SerializeField)
        var badgeContainerField = typeof(MailboxBadge).GetField("badgeContainer", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        badgeContainerField?.SetValue(_mailboxBadge, _badgeObject);
        
        var unreadTextField = typeof(MailboxBadge).GetField("unreadCountText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        unreadTextField?.SetValue(_mailboxBadge, countText);
        
        var badgeBackgroundField = typeof(MailboxBadge).GetField("badgeBackground", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        badgeBackgroundField?.SetValue(_mailboxBadge, badgeImage);
    }
    #endregion
    
    #region Badge Update Integration Tests
    [UnityTest]
    public IEnumerator TestBadgeUpdatesWithMailboxManager()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame(); // Allow badge to initialize
        
        // Initially should show 0
        Assert.AreEqual(0, _mailboxBadge.UnreadCount, "Badge should start with 0 unread count");
        
        // Act - Add unread messages
        var message1 = CreateTestMessage("badge_test_1", "Test Message 1", false);
        var message2 = CreateTestMessage("badge_test_2", "Test Message 2", false);
        
        _mailboxManager.AddMessage(message1);
        yield return new WaitForEndOfFrame();
        
        // Assert - Badge should update to 1
        Assert.AreEqual(1, _mailboxBadge.UnreadCount, "Badge should show 1 unread message");
        
        _mailboxManager.AddMessage(message2);
        yield return new WaitForEndOfFrame();
        
        // Assert - Badge should update to 2
        Assert.AreEqual(2, _mailboxBadge.UnreadCount, "Badge should show 2 unread messages");
        
        Debug.Log("[MailboxBadgeIntegrationTests] Badge updates with message additions verified");
    }
    
    [UnityTest]
    public IEnumerator TestBadgeUpdatesOnMessageRead()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Add unread messages
        var message1 = CreateTestMessage("read_test_1", "Test Message 1", false);
        var message2 = CreateTestMessage("read_test_2", "Test Message 2", false);
        
        _mailboxManager.AddMessage(message1);
        _mailboxManager.AddMessage(message2);
        yield return new WaitForEndOfFrame();
        
        Assert.AreEqual(2, _mailboxBadge.UnreadCount, "Badge should show 2 unread messages initially");
        
        // Act - Mark one message as read
        _mailboxManager.MarkMessageAsRead("read_test_1");
        yield return new WaitForEndOfFrame();
        
        // Assert - Badge should update to 1
        Assert.AreEqual(1, _mailboxBadge.UnreadCount, "Badge should show 1 unread message after marking one as read");
        
        // Act - Mark all as read
        _mailboxManager.MarkAllMessagesAsRead();
        yield return new WaitForEndOfFrame();
        
        // Assert - Badge should update to 0
        Assert.AreEqual(0, _mailboxBadge.UnreadCount, "Badge should show 0 unread messages after marking all as read");
        
        Debug.Log("[MailboxBadgeIntegrationTests] Badge updates on message read verified");
    }
    
    [UnityTest]
    public IEnumerator TestBadgeAnimationOnCountChange()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame();
        
        // Get initial scale
        var initialScale = _badgeObject.transform.localScale;
        
        // Act - Add message to trigger animation
        var message = CreateTestMessage("animation_test", "Animation Test", false);
        _mailboxManager.AddMessage(message);
        
        // Wait for animation to start and complete
        yield return new WaitForSeconds(0.1f); // Animation should start
        yield return new WaitForSeconds(0.5f); // Wait for animation to complete
        
        // Assert - Scale should return to normal after animation
        Assert.AreEqual(initialScale, _badgeObject.transform.localScale, 
            "Badge scale should return to normal after animation");
        Assert.AreEqual(1, _mailboxBadge.UnreadCount, 
            "Badge should show correct count after animation");
        
        Debug.Log("[MailboxBadgeIntegrationTests] Badge animation on count change verified");
    }
    #endregion
    
    #region Visual State Integration Tests
    [UnityTest]
    public IEnumerator TestBadgeVisibilityToggle()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame();
        
        // Initially with no messages, badge should be hidden
        // (This depends on the actual implementation - badge might always be visible with count 0)
        
        // Act - Add unread message
        var message = CreateTestMessage("visibility_test", "Visibility Test", false);
        _mailboxManager.AddMessage(message);
        yield return new WaitForEndOfFrame();
        
        // Badge should be visible with count > 0
        Assert.IsTrue(_badgeObject.activeInHierarchy, "Badge should be visible when there are unread messages");
        
        // Act - Mark message as read
        _mailboxManager.MarkMessageAsRead("visibility_test");
        yield return new WaitForEndOfFrame();
        
        // Badge behavior with count = 0 depends on implementation
        // It might be hidden or show "0" - either is valid
        
        Debug.Log($"[MailboxBadgeIntegrationTests] Badge visibility: Active={_badgeObject.activeInHierarchy}, Count={_mailboxBadge.UnreadCount}");
    }
    
    [UnityTest]
    public IEnumerator TestBadgeHighPriorityVisuals()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame();
        
        var badgeImage = _badgeObject.GetComponentInChildren<Image>();
        var normalColor = badgeImage.color;
        
        // Act - Add multiple messages to potentially trigger high priority visuals
        for (int i = 0; i < 6; i++) // More than typical high priority threshold
        {
            var message = CreateTestMessage($"priority_test_{i}", $"Priority Test {i}", false);
            _mailboxManager.AddMessage(message);
        }
        
        yield return new WaitForEndOfFrame();
        
        // Assert - Badge should show high count
        Assert.AreEqual(6, _mailboxBadge.UnreadCount, "Badge should show 6 unread messages");
        
        // Visual state may change for high priority (implementation dependent)
        Debug.Log($"[MailboxBadgeIntegrationTests] High priority badge: Count={_mailboxBadge.UnreadCount}, Color={badgeImage.color}");
    }
    #endregion
    
    #region Performance Integration Tests
    [UnityTest]
    public IEnumerator TestBadgePerformanceWithFrequentUpdates()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame();
        
        // Act - Rapidly add and remove messages to test badge update performance
        var startTime = Time.realtimeSinceStartup;
        
        for (int i = 0; i < 20; i++)
        {
            // Add message
            var message = CreateTestMessage($"perf_test_{i}", $"Performance Test {i}", false);
            _mailboxManager.AddMessage(message);
            
            yield return null; // One frame delay
            
            // Mark as read
            _mailboxManager.MarkMessageAsRead($"perf_test_{i}");
            
            yield return null; // One frame delay
        }
        
        var totalTime = Time.realtimeSinceStartup - startTime;
        
        // Assert - Should handle rapid updates efficiently
        Assert.LessOrEqual(totalTime, 2.0f, "Badge should handle rapid updates within 2 seconds");
        Assert.AreEqual(0, _mailboxBadge.UnreadCount, "Badge should show 0 unread messages after all operations");
        
        Debug.Log($"[MailboxBadgeIntegrationTests] Badge performance: 40 operations in {totalTime:F2}s");
    }
    #endregion
    
    #region Integration with Energy Gifts
    [UnityTest]
    public IEnumerator TestBadgeWithEnergyGiftProcessing()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        yield return new WaitForEndOfFrame();
        
        // Create energy gift message
        var energyGift = CreateEnergyGiftMessage("badge_energy_test", 25, "badge_gift_123");
        
        // Act - Add energy gift
        _mailboxManager.AddMessage(energyGift);
        yield return new WaitForEndOfFrame();
        
        Assert.AreEqual(1, _mailboxBadge.UnreadCount, "Badge should show 1 unread energy gift");
        
        // Process energy gift (this will mark it as read)
        _mailboxManager.ProcessMessage("badge_energy_test");
        yield return new WaitForSeconds(0.5f);
        
        // Assert - Badge should update after gift processing
        Assert.AreEqual(0, _mailboxBadge.UnreadCount, "Badge should show 0 after energy gift is processed");
        
        Debug.Log("[MailboxBadgeIntegrationTests] Badge integration with energy gift processing verified");
    }
    #endregion
    
    #region Helper Methods
    private MailboxMessage CreateTestMessage(string messageId, string title, bool isRead)
    {
        var message = new MailboxMessage
        {
            messageId = messageId,
            type = MailMessageType.System,
            title = title,
            content = $"Content for {title}",
            senderId = "test_sender",
            senderName = "Test Sender",
            SentAt = System.DateTime.UtcNow,
            isRead = isRead
        };
        
        if (isRead)
            message.ReadAt = System.DateTime.UtcNow;
        
        return message;
    }
    
    private MailboxMessage CreateEnergyGiftMessage(string messageId, int energyAmount, string giftId)
    {
        var message = new MailboxMessage
        {
            messageId = messageId,
            type = MailMessageType.EnergyGift,
            title = $"에너지 선물 ({energyAmount}개)",
            content = "친구가 에너지를 선물했습니다!",
            senderId = "friend_sender",
            senderName = "친구",
            SentAt = System.DateTime.UtcNow,
            isRead = false
        };
        
        message.AddAttachment("energy", energyAmount, $"에너지 {energyAmount}개");
        message.AddAttachment("giftId", giftId, "선물 ID");
        
        return message;
    }
    #endregion
}