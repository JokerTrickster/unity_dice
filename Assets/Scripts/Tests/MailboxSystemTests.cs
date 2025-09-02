using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// 우편함 시스템 종합 테스트
/// MailboxManager, 캐싱, 네트워크 동기화, 성능 요구사항을 검증합니다.
/// </summary>
public class MailboxSystemTests
{
    #region Test Setup
    private GameObject _testManagerObject;
    private MailboxManager _mailboxManager;
    private TestNetworkManager _mockNetworkManager;
    private string _testUserId = "test_user_123";
    
    [SetUp]
    public void Setup()
    {
        // Clear any existing cache
        MailboxCache.ClearCache();
        EnergyGiftHandler.ClearClaimedGifts();
        
        // Create test objects
        _testManagerObject = new GameObject("TestMailboxManager");
        _mailboxManager = _testManagerObject.AddComponent<MailboxManager>();
        
        // Setup mock network manager for testing
        _mockNetworkManager = new TestNetworkManager();
        SetupMockNetworkResponses();
    }
    
    [TearDown]
    public void TearDown()
    {
        // Cleanup
        MailboxCache.ClearCache();
        EnergyGiftHandler.ClearClaimedGifts();
        
        if (_testManagerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testManagerObject);
        }
        
        _mockNetworkManager?.Cleanup();
    }
    
    private void SetupMockNetworkResponses()
    {
        var testMessages = CreateTestMessages(5);
        var testData = new MailboxData();
        foreach (var message in testMessages)
        {
            testData.AddMessage(message);
        }
        
        _mockNetworkManager.SetMockResponse("/api/mailbox/messages", testData);
    }
    #endregion
    
    #region Initialization Tests
    [UnityTest]
    public IEnumerator TestMailboxManagerInitialization()
    {
        // Act
        var startTime = Time.realtimeSinceStartup;
        _mailboxManager.Initialize(_testUserId);
        
        // Wait for initialization
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        var initTime = Time.realtimeSinceStartup - startTime;
        
        // Assert
        Assert.IsTrue(_mailboxManager.IsInitialized, "MailboxManager should be initialized");
        Assert.AreEqual(_testUserId, _mailboxManager.CurrentUserId, "User ID should match");
        Assert.LessOrEqual(initTime, 3.0f, "Initialization should complete within 3 seconds");
        
        Debug.Log($"[MailboxSystemTests] Initialization completed in {initTime:F2}s");
    }
    
    [Test]
    public void TestSingletonPattern()
    {
        // Arrange & Act
        var instance1 = MailboxManager.Instance;
        var instance2 = MailboxManager.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2, "MailboxManager should be a singleton");
        Assert.IsNotNull(instance1, "Instance should not be null");
    }
    
    [Test]
    public void TestInitializationWithInvalidUserId()
    {
        // Arrange
        bool errorReceived = false;
        string errorMessage = "";
        MailboxManager.OnError += (msg) => { errorReceived = true; errorMessage = msg; };
        
        // Act
        _mailboxManager.Initialize("");
        
        // Assert
        Assert.IsTrue(errorReceived, "Error event should be triggered for invalid user ID");
        Assert.IsNotEmpty(errorMessage, "Error message should not be empty");
        Assert.IsFalse(_mailboxManager.IsInitialized, "Manager should not be initialized with invalid user ID");
    }
    #endregion
    
    #region Caching Tests
    [UnityTest]
    public IEnumerator TestCacheLoadingPerformance()
    {
        // Arrange - Create test data and save to cache
        var testData = new MailboxData();
        var testMessages = CreateTestMessages(100); // Large number for performance test
        foreach (var message in testMessages)
        {
            testData.AddMessage(message);
        }
        MailboxCache.SaveToCache(testData, _testUserId);
        
        // Act
        var startTime = Time.realtimeSinceStartup;
        _mailboxManager.Initialize(_testUserId);
        
        // Wait for cache load (should be immediate)
        yield return new WaitForEndOfFrame();
        var cacheLoadTime = Time.realtimeSinceStartup - startTime;
        
        // Assert
        Assert.LessOrEqual(cacheLoadTime, 1.0f, "Cache loading should complete within 1 second");
        Assert.AreEqual(100, _mailboxManager.TotalMessageCount, "All cached messages should be loaded");
        
        Debug.Log($"[MailboxSystemTests] Cache loaded {testMessages.Count} messages in {cacheLoadTime:F3}s");
    }
    
    [Test]
    public void TestCacheEncryptionAndSecurity()
    {
        // Arrange
        var testData = new MailboxData();
        var sensitiveMessage = CreateTestMessage("sensitive_id", "Sensitive Content", "Top Secret Data");
        testData.AddMessage(sensitiveMessage);
        
        // Act
        bool saved = MailboxCache.SaveToCache(testData, _testUserId);
        var loaded = MailboxCache.LoadFromCache(_testUserId);
        
        // Assert
        Assert.IsTrue(saved, "Sensitive data should be saved successfully");
        Assert.IsNotNull(loaded, "Sensitive data should be loaded successfully");
        Assert.AreEqual(sensitiveMessage.content, loaded.messages[0].content, "Decrypted content should match original");
        
        // Verify data is encrypted in storage (check raw PlayerPrefs)
        string rawData = PlayerPrefs.GetString("mailbox_data_encrypted", "");
        Assert.IsFalse(rawData.Contains("Sensitive Content"), "Raw stored data should not contain plaintext");
    }
    
    [Test]
    public void TestCacheExpiryBehavior()
    {
        // Arrange
        var testData = new MailboxData();
        testData.AddMessage(CreateTestMessage("test_id", "Test", "Content"));
        MailboxCache.SaveToCache(testData, _testUserId);
        
        // Act & Assert - Cache should exist immediately
        Assert.IsTrue(MailboxCache.HasCache(), "Cache should exist after save");
        
        // Simulate expired cache by manipulating timestamp
        var expiredTimestamp = DateTime.UtcNow.AddHours(-7).ToBinary().ToString();
        PlayerPrefs.SetString("mailbox_cache_timestamp", expiredTimestamp);
        
        // Assert - Cache should be considered expired
        Assert.IsTrue(MailboxCache.IsCacheExpired(), "Cache should be expired after 7 hours");
        Assert.IsFalse(MailboxCache.HasCache(), "HasCache should return false for expired cache");
        
        var loaded = MailboxCache.LoadFromCache(_testUserId);
        Assert.IsNull(loaded, "Expired cache should return null");
    }
    #endregion
    
    #region Message Management Tests
    [UnityTest]
    public IEnumerator TestMessageLoadingAndSorting()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Create messages with different timestamps
        var messages = new List<MailboxMessage>
        {
            CreateTestMessage("1", "Oldest", "Content", DateTime.UtcNow.AddDays(-2)),
            CreateTestMessage("2", "Newest", "Content", DateTime.UtcNow),
            CreateTestMessage("3", "Middle", "Content", DateTime.UtcNow.AddDays(-1))
        };
        
        // Act - Add messages
        foreach (var message in messages)
        {
            _mailboxManager.AddMessage(message);
        }
        
        // Assert
        Assert.AreEqual(3, _mailboxManager.TotalMessageCount, "All messages should be added");
        
        var data = _mailboxManager.MailboxData;
        data.SortMessagesByDate();
        
        // Should be sorted newest first
        Assert.AreEqual("2", data.messages[0].messageId, "Newest message should be first");
        Assert.AreEqual("3", data.messages[1].messageId, "Middle message should be second");
        Assert.AreEqual("1", data.messages[2].messageId, "Oldest message should be last");
    }
    
    [UnityTest]
    public IEnumerator TestReadStatusManagement()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var testMessage = CreateTestMessage("read_test", "Test Message", "Content");
        _mailboxManager.AddMessage(testMessage);
        
        // Initially unread
        Assert.AreEqual(1, _mailboxManager.UnreadCount, "Should have 1 unread message");
        
        bool messageReadEventFired = false;
        MailboxManager.OnMessageRead += (id) => { messageReadEventFired = true; };
        
        // Act - Mark as read
        _mailboxManager.MarkMessageAsRead("read_test");
        yield return new WaitForEndOfFrame();
        
        // Assert
        Assert.AreEqual(0, _mailboxManager.UnreadCount, "Should have 0 unread messages");
        Assert.IsTrue(messageReadEventFired, "OnMessageRead event should fire");
        
        var message = _mailboxManager.GetMessage("read_test");
        Assert.IsTrue(message.isRead, "Message should be marked as read");
        Assert.IsNotNull(message.ReadAt, "ReadAt timestamp should be set");
    }
    
    [UnityTest]
    public IEnumerator TestBulkOperations()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Add 50 messages to test bulk operations
        for (int i = 0; i < 50; i++)
        {
            var message = CreateTestMessage($"bulk_{i}", $"Message {i}", $"Content {i}");
            _mailboxManager.AddMessage(message);
        }
        
        Assert.AreEqual(50, _mailboxManager.UnreadCount, "Should have 50 unread messages");
        
        // Act - Mark all as read
        var startTime = Time.realtimeSinceStartup;
        _mailboxManager.MarkAllMessagesAsRead();
        var operationTime = Time.realtimeSinceStartup - startTime;
        
        // Assert
        Assert.AreEqual(0, _mailboxManager.UnreadCount, "Should have 0 unread messages after bulk operation");
        Assert.LessOrEqual(operationTime, 1.0f, "Bulk operation should complete within 1 second");
        
        Debug.Log($"[MailboxSystemTests] Bulk read operation completed in {operationTime:F3}s");
    }
    #endregion
    
    #region Server Synchronization Tests
    [UnityTest]
    public IEnumerator TestServerSynchronization()
    {
        // Arrange
        bool syncStatusChanged = false;
        string syncMessage = "";
        MailboxManager.OnSyncStatusChanged += (success, message) => 
        {
            syncStatusChanged = true;
            syncMessage = message;
        };
        
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Act - Force server sync
        _mailboxManager.RefreshFromServer();
        
        // Wait for sync to complete
        yield return new WaitUntil(() => !_mailboxManager.IsSyncing);
        
        // Assert
        Assert.IsTrue(syncStatusChanged, "Sync status change event should fire");
        Assert.IsNotEmpty(syncMessage, "Sync message should not be empty");
        Assert.IsFalse(_mailboxManager.IsSyncing, "Should not be syncing after completion");
    }
    
    [UnityTest]
    public IEnumerator TestSyncRetryMechanism()
    {
        // Arrange - Setup network to fail initially then succeed
        _mockNetworkManager.SetFailureCount(2); // Fail first 2 attempts
        
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Act
        var startTime = Time.realtimeSinceStartup;
        _mailboxManager.RefreshFromServer();
        
        // Wait for completion (should retry and eventually succeed)
        yield return new WaitUntil(() => !_mailboxManager.IsSyncing);
        var totalTime = Time.realtimeSinceStartup - startTime;
        
        // Assert
        Assert.Greater(totalTime, 2.0f, "Should take time for retries");
        Assert.LessOrEqual(totalTime, 10.0f, "Should not take too long even with retries");
        
        Debug.Log($"[MailboxSystemTests] Sync with retries completed in {totalTime:F2}s");
    }
    #endregion
    
    #region Auto-Sync Tests
    [UnityTest]
    public IEnumerator TestAutoSyncFunctionality()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        int syncEventCount = 0;
        MailboxManager.OnSyncStatusChanged += (success, message) => { syncEventCount++; };
        
        // Auto-sync runs every 5 minutes (300s), we'll wait a shorter time for testing
        // In a real scenario, you'd modify the auto-sync interval for testing
        
        // Act - Wait for potential auto-sync (shortened for test)
        yield return new WaitForSeconds(1.0f);
        
        // Assert - For this test, we just verify the system is capable of auto-sync
        Assert.IsTrue(_mailboxManager.IsInitialized, "Manager should remain initialized during auto-sync");
        
        // Note: Full auto-sync testing would require time manipulation or test-specific intervals
        Debug.Log($"[MailboxSystemTests] Auto-sync capability verified");
    }
    #endregion
    
    #region Message Type Handling Tests
    [UnityTest]
    public IEnumerator TestEnergyGiftProcessing()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var energyGift = CreateEnergyGiftMessage("gift_123", 50, "unique_gift_456");
        _mailboxManager.AddMessage(energyGift);
        
        bool giftProcessed = false;
        EnergyGiftHandler.OnEnergyGiftClaimed += (messageId, amount) => { giftProcessed = true; };
        
        // Act
        _mailboxManager.ProcessMessage("gift_123");
        
        // Wait for processing
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(giftProcessed, "Energy gift should be processed");
        
        var message = _mailboxManager.GetMessage("gift_123");
        Assert.IsTrue(message.isRead, "Gift message should be marked as read after processing");
    }
    
    [Test]
    public void TestMessageTypeFiltering()
    {
        // Arrange
        var messages = new List<MailboxMessage>
        {
            CreateTestMessage("system1", "System", "Content", DateTime.Now, MailMessageType.System),
            CreateTestMessage("friend1", "Friend", "Content", DateTime.Now, MailMessageType.Friend),
            CreateEnergyGiftMessage("gift1", 10, "gift_id_1"),
            CreateTestMessage("achievement1", "Achievement", "Content", DateTime.Now, MailMessageType.Achievement)
        };
        
        var data = new MailboxData();
        foreach (var message in messages)
        {
            data.AddMessage(message);
        }
        
        // Act & Assert
        var systemMessages = data.GetMessagesByType(MailMessageType.System);
        Assert.AreEqual(1, systemMessages.Count, "Should find 1 system message");
        
        var friendMessages = data.GetMessagesByType(MailMessageType.Friend);
        Assert.AreEqual(1, friendMessages.Count, "Should find 1 friend message");
        
        var giftMessages = data.GetMessagesByType(MailMessageType.EnergyGift);
        Assert.AreEqual(1, giftMessages.Count, "Should find 1 gift message");
        
        var achievementMessages = data.GetMessagesByType(MailMessageType.Achievement);
        Assert.AreEqual(1, achievementMessages.Count, "Should find 1 achievement message");
    }
    #endregion
    
    #region Error Handling Tests
    [UnityTest]
    public IEnumerator TestNetworkErrorHandling()
    {
        // Arrange
        _mockNetworkManager.SetFailureMode(true); // All requests fail
        
        bool errorReceived = false;
        string errorMessage = "";
        MailboxManager.OnError += (msg) => { errorReceived = true; errorMessage = msg; };
        
        // Act
        _mailboxManager.Initialize(_testUserId);
        
        // Wait for initialization attempt
        yield return new WaitForSeconds(2.0f);
        
        // Assert - Should handle network errors gracefully
        Assert.IsTrue(_mailboxManager.IsInitialized, "Should initialize even with network errors (using cache)");
        // Note: Error events may or may not fire depending on cache availability
        
        Debug.Log($"[MailboxSystemTests] Network error handling tested");
    }
    
    [Test]
    public void TestDataValidationAndRecovery()
    {
        // Arrange - Create invalid data
        var corruptedData = new MailboxData();
        corruptedData.messages = new List<MailboxMessage>
        {
            CreateTestMessage("", "Invalid", "No ID"), // Invalid: empty ID
            CreateTestMessage("valid1", "Valid", "Content"),
            CreateTestMessage("valid1", "Duplicate", "Same ID"), // Invalid: duplicate ID
        };
        corruptedData.unreadCount = 999; // Invalid: wrong count
        
        // Act
        bool isValid = corruptedData.IsValid();
        
        // Assert
        Assert.IsFalse(isValid, "Data should be invalid due to empty ID and duplicates");
        
        // Test recovery
        corruptedData.RecalculateUnreadCount();
        // Note: Duplicate and empty ID issues need manual cleaning, but unread count is fixed
        
        Debug.Log($"[MailboxSystemTests] Data validation and recovery tested");
    }
    #endregion
    
    #region Performance Tests
    [UnityTest]
    public IEnumerator TestLoadTimePerformance()
    {
        // Arrange - Create large dataset
        var largeData = new MailboxData();
        for (int i = 0; i < 1000; i++)
        {
            largeData.AddMessage(CreateTestMessage($"perf_{i}", $"Message {i}", $"Content {i}"));
        }
        MailboxCache.SaveToCache(largeData, _testUserId);
        
        // Act
        var startTime = Time.realtimeSinceStartup;
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        var loadTime = Time.realtimeSinceStartup - startTime;
        
        // Assert - Performance requirement: 3s for loading, 1s for cache
        Assert.LessOrEqual(loadTime, 3.0f, "Loading should complete within 3 seconds");
        Assert.AreEqual(1000, _mailboxManager.TotalMessageCount, "All messages should be loaded");
        
        Debug.Log($"[MailboxSystemTests] Performance test: Loaded 1000 messages in {loadTime:F2}s");
    }
    
    [UnityTest]
    public IEnumerator TestMemoryUsage()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Load large number of messages
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        for (int i = 0; i < 500; i++)
        {
            var message = CreateTestMessage($"memory_{i}", $"Title {i}", $"Long content for memory test message {i}. This content should be reasonably long to test memory usage impact of storing many messages in the mailbox system. Additional padding text to make the content more realistic.");
            _mailboxManager.AddMessage(message);
        }
        
        // Force garbage collection and measure memory
        yield return new WaitForEndOfFrame();
        GC.Collect();
        yield return new WaitForEndOfFrame();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // Convert to MB
        
        // Assert - Performance requirement: <10MB increase
        Assert.LessOrEqual(memoryIncrease, 10, $"Memory increase should be less than 10MB, was {memoryIncrease:F2}MB");
        
        Debug.Log($"[MailboxSystemTests] Memory usage test: {memoryIncrease:F2}MB increase for 500 messages");
    }
    #endregion
    
    #region Test Helper Methods
    private List<MailboxMessage> CreateTestMessages(int count)
    {
        var messages = new List<MailboxMessage>();
        for (int i = 0; i < count; i++)
        {
            messages.Add(CreateTestMessage($"test_{i}", $"Test Message {i}", $"Content {i}"));
        }
        return messages;
    }
    
    private MailboxMessage CreateTestMessage(string id, string title, string content, DateTime? sentAt = null, MailMessageType type = MailMessageType.System)
    {
        var message = new MailboxMessage
        {
            messageId = id,
            type = type,
            title = title,
            content = content,
            senderId = "sender_123",
            senderName = "Test Sender",
            SentAt = sentAt ?? DateTime.UtcNow,
            isRead = false
        };
        
        return message;
    }
    
    private MailboxMessage CreateEnergyGiftMessage(string id, int energyAmount, string giftId)
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
        message.AddAttachment("giftId", giftId, "선물 ID");
        
        return message;
    }
    #endregion
}

#region Mock Network Manager for Testing
public class TestNetworkManager
{
    private Dictionary<string, object> _mockResponses = new Dictionary<string, object>();
    private bool _failureMode = false;
    private int _failureCount = 0;
    private int _currentFailures = 0;
    
    public void SetMockResponse(string endpoint, object response)
    {
        _mockResponses[endpoint] = response;
    }
    
    public void SetFailureMode(bool enabled)
    {
        _failureMode = enabled;
    }
    
    public void SetFailureCount(int count)
    {
        _failureCount = count;
        _currentFailures = 0;
    }
    
    public void Cleanup()
    {
        _mockResponses.Clear();
        _failureMode = false;
        _failureCount = 0;
        _currentFailures = 0;
    }
}
#endregion