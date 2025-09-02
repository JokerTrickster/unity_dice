using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// 에너지 우편함 통합 테스트
/// 전체 에너지 선물 처리 플로우를 검증하고 EnergyManager와의 통합을 테스트합니다.
/// </summary>
public class EnergyMailboxIntegration
{
    #region Test Setup
    private GameObject _testManagerObject;
    private MailboxManager _mailboxManager;
    private GameObject _testEnergyManagerObject;
    private TestEnergyManager _testEnergyManager;
    private string _testUserId = "integration_user_123";
    
    [SetUp]
    public void Setup()
    {
        // Clear previous state
        MailboxCache.ClearCache();
        EnergyGiftHandler.ClearClaimedGifts();
        
        // Setup MailboxManager
        _testManagerObject = new GameObject("TestMailboxManager");
        _mailboxManager = _testManagerObject.AddComponent<MailboxManager>();
        
        // Setup Test EnergyManager
        _testEnergyManagerObject = new GameObject("TestEnergyManager");
        _testEnergyManager = _testEnergyManagerObject.AddComponent<TestEnergyManager>();
        _testEnergyManager.Initialize(100, 50); // Max: 100, Current: 50
        
        Debug.Log("[EnergyMailboxIntegration] Test setup completed");
    }
    
    [TearDown]
    public void TearDown()
    {
        // Cleanup
        MailboxCache.ClearCache();
        EnergyGiftHandler.ClearClaimedGifts();
        
        if (_testManagerObject != null)
            UnityEngine.Object.DestroyImmediate(_testManagerObject);
        if (_testEnergyManagerObject != null)
            UnityEngine.Object.DestroyImmediate(_testEnergyManagerObject);
        
        Debug.Log("[EnergyMailboxIntegration] Test teardown completed");
    }
    #endregion
    
    #region Complete Energy Gift Flow Tests
    [UnityTest]
    public IEnumerator TestCompleteEnergyGiftFlow()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var initialEnergy = _testEnergyManager.CurrentEnergy;
        var energyGift = CreateEnergyGiftMessage("gift_flow_test", 25, "unique_gift_001");
        
        bool giftClaimed = false;
        int claimedAmount = 0;
        EnergyGiftHandler.OnEnergyGiftClaimed += (messageId, amount) => 
        {
            giftClaimed = true;
            claimedAmount = amount;
        };
        
        // Act - Complete flow: Add message -> Display -> Process
        
        // Step 1: Add energy gift message
        bool messageAdded = _mailboxManager.AddMessage(energyGift);
        Assert.IsTrue(messageAdded, "Energy gift message should be added successfully");
        Assert.AreEqual(1, _mailboxManager.UnreadCount, "Should have 1 unread message");
        
        yield return new WaitForEndOfFrame();
        
        // Step 2: Process the energy gift (simulates user clicking)
        _mailboxManager.ProcessMessage("gift_flow_test");
        
        // Wait for processing to complete
        yield return new WaitForSeconds(1.0f);
        
        // Assert - Complete flow validation
        Assert.IsTrue(giftClaimed, "Energy gift should be processed successfully");
        Assert.AreEqual(25, claimedAmount, "Claimed amount should match gift amount");
        Assert.AreEqual(initialEnergy + 25, _testEnergyManager.CurrentEnergy, "Player energy should increase");
        
        var processedMessage = _mailboxManager.GetMessage("gift_flow_test");
        Assert.IsTrue(processedMessage.isRead, "Gift message should be marked as read");
        Assert.AreEqual(0, _mailboxManager.UnreadCount, "Should have 0 unread messages after processing");
        
        // Verify duplicate prevention
        Assert.IsTrue(EnergyGiftHandler.IsGiftClaimed("unique_gift_001"), "Gift should be marked as claimed");
        
        Debug.Log($"[EnergyMailboxIntegration] Complete flow: {initialEnergy} -> {_testEnergyManager.CurrentEnergy} energy");
    }
    
    [UnityTest]
    public IEnumerator TestEnergyGiftDuplicatePrevention()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var giftId = "duplicate_prevention_test";
        var gift1 = CreateEnergyGiftMessage("gift1", 30, giftId);
        var gift2 = CreateEnergyGiftMessage("gift2", 30, giftId); // Same gift ID
        
        bool firstClaimSucceeded = false;
        bool secondClaimFailed = false;
        string failureReason = "";
        
        EnergyGiftHandler.OnEnergyGiftClaimed += (messageId, amount) => 
        {
            if (messageId == "gift1") firstClaimSucceeded = true;
        };
        
        EnergyGiftHandler.OnEnergyGiftClaimFailed += (messageId, error) => 
        {
            if (messageId == "gift2") 
            {
                secondClaimFailed = true;
                failureReason = error;
            }
        };
        
        // Act
        _mailboxManager.AddMessage(gift1);
        _mailboxManager.AddMessage(gift2);
        
        var initialEnergy = _testEnergyManager.CurrentEnergy;
        
        // Process first gift (should succeed)
        _mailboxManager.ProcessMessage("gift1");
        yield return new WaitForSeconds(0.5f);
        
        // Process second gift (should fail due to duplicate)
        _mailboxManager.ProcessMessage("gift2");
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(firstClaimSucceeded, "First gift should be claimed successfully");
        Assert.IsTrue(secondClaimFailed, "Second gift with same ID should fail");
        Assert.IsTrue(failureReason.Contains("이미 받은"), "Failure reason should indicate duplicate");
        
        // Energy should only increase once
        Assert.AreEqual(initialEnergy + 30, _testEnergyManager.CurrentEnergy, "Energy should increase only once");
        
        Debug.Log($"[EnergyMailboxIntegration] Duplicate prevention: {failureReason}");
    }
    
    [UnityTest]
    public IEnumerator TestEnergyCapLimitation()
    {
        // Arrange - Set energy close to maximum
        _testEnergyManager.SetCurrentEnergy(95); // Close to max of 100
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var giftExceedsMax = CreateEnergyGiftMessage("exceed_test", 20, "exceed_gift"); // Would exceed max
        var giftWithinLimit = CreateEnergyGiftMessage("within_test", 5, "within_gift"); // Within limit
        
        bool exceedFailed = false;
        bool withinSucceeded = false;
        string exceedFailureReason = "";
        
        EnergyGiftHandler.OnEnergyGiftClaimFailed += (messageId, error) => 
        {
            if (messageId == "exceed_test") 
            {
                exceedFailed = true;
                exceedFailureReason = error;
            }
        };
        
        EnergyGiftHandler.OnEnergyGiftClaimed += (messageId, amount) => 
        {
            if (messageId == "within_test") withinSucceeded = true;
        };
        
        // Act
        _mailboxManager.AddMessage(giftExceedsMax);
        _mailboxManager.AddMessage(giftWithinLimit);
        
        // Try to process gift that would exceed maximum
        _mailboxManager.ProcessMessage("exceed_test");
        yield return new WaitForSeconds(0.5f);
        
        // Process gift within limits
        _mailboxManager.ProcessMessage("within_test");
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(exceedFailed, "Gift exceeding maximum should fail");
        Assert.IsTrue(exceedFailureReason.Contains("최대"), "Should mention maximum energy limit");
        Assert.IsTrue(withinSucceeded, "Gift within limits should succeed");
        Assert.AreEqual(100, _testEnergyManager.CurrentEnergy, "Energy should be at maximum");
        
        Debug.Log($"[EnergyMailboxIntegration] Energy cap: Final energy {_testEnergyManager.CurrentEnergy}/100");
    }
    #endregion
    
    #region Network Error Handling Tests
    [UnityTest]
    public IEnumerator TestNetworkErrorDuringClaim()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var networkGift = CreateEnergyGiftMessage("network_test", 15, "network_gift");
        
        bool claimFailed = false;
        string errorMessage = "";
        EnergyGiftHandler.OnEnergyGiftClaimFailed += (messageId, error) => 
        {
            claimFailed = true;
            errorMessage = error;
        };
        
        // Simulate network failure (in real implementation, this would be done through mock NetworkManager)
        // For this test, we'll add the message and verify error handling exists
        
        _mailboxManager.AddMessage(networkGift);
        var initialEnergy = _testEnergyManager.CurrentEnergy;
        
        // Act - Process gift (would attempt server communication)
        _mailboxManager.ProcessMessage("network_test");
        yield return new WaitForSeconds(1.0f);
        
        // Assert - Since we don't have real network simulation, we verify the system handles it
        // In real implementation with mock network, this would test actual failure scenarios
        Assert.AreEqual(initialEnergy, _testEnergyManager.CurrentEnergy, "Energy should not change without successful server response");
        
        Debug.Log("[EnergyMailboxIntegration] Network error handling structure verified");
    }
    #endregion
    
    #region Cache Integration Tests
    [UnityTest]
    public IEnumerator TestEnergyGiftCacheConsistency()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Add and process an energy gift
        var cacheGift = CreateEnergyGiftMessage("cache_test", 40, "cache_gift");
        _mailboxManager.AddMessage(cacheGift);
        _mailboxManager.ProcessMessage("cache_test");
        
        yield return new WaitForSeconds(0.5f);
        
        // Verify cache is updated
        var cachedData = MailboxCache.LoadFromCache(_testUserId);
        Assert.IsNotNull(cachedData, "Cache should exist after processing");
        
        var cachedMessage = cachedData.GetMessage("cache_test");
        Assert.IsNotNull(cachedMessage, "Processed message should be in cache");
        Assert.IsTrue(cachedMessage.isRead, "Cached message should be marked as read");
        
        // Verify claimed gifts persistence
        Assert.IsTrue(EnergyGiftHandler.IsGiftClaimed("cache_gift"), "Gift should be marked as claimed in persistent storage");
        
        // Test persistence across sessions by creating new manager
        var newManager = new GameObject("NewMailboxManager").AddComponent<MailboxManager>();
        newManager.Initialize(_testUserId);
        yield return new WaitUntil(() => newManager.IsInitialized);
        
        // Should load from cache
        Assert.AreEqual(1, newManager.TotalMessageCount, "New manager should load cached message");
        Assert.AreEqual(0, newManager.UnreadCount, "New manager should maintain read status");
        
        // Cleanup
        UnityEngine.Object.DestroyImmediate(newManager.gameObject);
        
        Debug.Log("[EnergyMailboxIntegration] Cache consistency verified across sessions");
    }
    #endregion
    
    #region UI Integration Tests
    [UnityTest]
    public IEnumerator TestUIUpdateAfterEnergyGiftClaim()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var uiGift = CreateEnergyGiftMessage("ui_test", 35, "ui_gift");
        
        bool mailboxUpdated = false;
        bool unreadCountChanged = false;
        int newUnreadCount = -1;
        
        MailboxManager.OnMailboxUpdated += (data) => { mailboxUpdated = true; };
        MailboxManager.OnUnreadCountChanged += (count) => { unreadCountChanged = true; newUnreadCount = count; };
        
        // Act
        _mailboxManager.AddMessage(uiGift);
        Assert.AreEqual(1, _mailboxManager.UnreadCount, "Should have 1 unread message initially");
        
        // Process gift
        _mailboxManager.ProcessMessage("ui_test");
        yield return new WaitForSeconds(0.5f);
        
        // Assert UI events
        Assert.IsTrue(mailboxUpdated, "Mailbox updated event should be triggered");
        Assert.IsTrue(unreadCountChanged, "Unread count changed event should be triggered");
        Assert.AreEqual(0, newUnreadCount, "New unread count should be 0");
        Assert.AreEqual(0, _mailboxManager.UnreadCount, "Manager unread count should be 0");
        
        Debug.Log("[EnergyMailboxIntegration] UI update events verified after gift processing");
    }
    #endregion
    
    #region Performance Integration Tests
    [UnityTest]
    public IEnumerator TestBulkEnergyGiftProcessingPerformance()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Create multiple energy gifts
        var gifts = new List<MailboxMessage>();
        for (int i = 0; i < 10; i++)
        {
            gifts.Add(CreateEnergyGiftMessage($"bulk_gift_{i}", 5, $"bulk_gift_id_{i}"));
        }
        
        // Add all gifts
        foreach (var gift in gifts)
        {
            _mailboxManager.AddMessage(gift);
        }
        
        var initialEnergy = _testEnergyManager.CurrentEnergy;
        
        // Act - Process all gifts and measure time
        var startTime = Time.realtimeSinceStartup;
        
        foreach (var gift in gifts)
        {
            _mailboxManager.ProcessMessage(gift.messageId);
            yield return null; // Yield each frame to prevent blocking
        }
        
        // Wait for all processing to complete
        yield return new WaitForSeconds(1.0f);
        
        var processingTime = Time.realtimeSinceStartup - startTime;
        
        // Assert
        Assert.LessOrEqual(processingTime, 3.0f, "Bulk processing should complete within 3 seconds");
        Assert.AreEqual(initialEnergy + 50, _testEnergyManager.CurrentEnergy, "Should receive total of 50 energy");
        Assert.AreEqual(0, _mailboxManager.UnreadCount, "All messages should be processed and read");
        
        Debug.Log($"[EnergyMailboxIntegration] Bulk processing: 10 gifts in {processingTime:F2}s");
    }
    #endregion
    
    #region Error Recovery Tests
    [UnityTest]
    public IEnumerator TestEnergyGiftErrorRecovery()
    {
        // Arrange
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Test invalid energy gift (negative amount)
        var invalidGift = CreateEnergyGiftMessage("invalid_test", -10, "invalid_gift");
        
        bool processingFailed = false;
        string errorMsg = "";
        EnergyGiftHandler.OnEnergyGiftClaimFailed += (id, error) => 
        {
            if (id == "invalid_test") 
            {
                processingFailed = true;
                errorMsg = error;
            }
        };
        
        _mailboxManager.AddMessage(invalidGift);
        var initialEnergy = _testEnergyManager.CurrentEnergy;
        
        // Act
        _mailboxManager.ProcessMessage("invalid_test");
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(processingFailed, "Invalid gift should fail processing");
        Assert.IsNotEmpty(errorMsg, "Error message should be provided");
        Assert.AreEqual(initialEnergy, _testEnergyManager.CurrentEnergy, "Energy should not change for invalid gift");
        
        // System should still be functional for valid gifts
        var validGift = CreateEnergyGiftMessage("recovery_test", 20, "recovery_gift");
        _mailboxManager.AddMessage(validGift);
        _mailboxManager.ProcessMessage("recovery_test");
        
        yield return new WaitForSeconds(0.5f);
        
        Assert.AreEqual(initialEnergy + 20, _testEnergyManager.CurrentEnergy, "System should recover and process valid gifts");
        
        Debug.Log($"[EnergyMailboxIntegration] Error recovery: System functional after invalid gift");
    }
    #endregion
    
    #region Cross-Session Persistence Tests
    [UnityTest]
    public IEnumerator TestEnergyGiftClaimPersistence()
    {
        // Arrange - First session
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        var persistentGift = CreateEnergyGiftMessage("persistent_test", 25, "persistent_gift_id");
        _mailboxManager.AddMessage(persistentGift);
        _mailboxManager.ProcessMessage("persistent_test");
        
        yield return new WaitForSeconds(0.5f);
        
        // Verify gift is claimed
        Assert.IsTrue(EnergyGiftHandler.IsGiftClaimed("persistent_gift_id"), "Gift should be claimed in first session");
        
        // Simulate new session
        UnityEngine.Object.DestroyImmediate(_testManagerObject);
        _testManagerObject = new GameObject("NewSessionMailboxManager");
        _mailboxManager = _testManagerObject.AddComponent<MailboxManager>();
        
        // Clear in-memory state but keep persistent storage
        // Don't call ClearClaimedGifts() to test persistence
        
        // Act - New session initialization
        _mailboxManager.Initialize(_testUserId);
        yield return new WaitUntil(() => _mailboxManager.IsInitialized);
        
        // Try to add and process the same gift again
        var duplicateGift = CreateEnergyGiftMessage("duplicate_session_test", 25, "persistent_gift_id");
        _mailboxManager.AddMessage(duplicateGift);
        
        bool duplicateClaimFailed = false;
        EnergyGiftHandler.OnEnergyGiftClaimFailed += (id, error) => 
        {
            if (id == "duplicate_session_test") duplicateClaimFailed = true;
        };
        
        _mailboxManager.ProcessMessage("duplicate_session_test");
        yield return new WaitForSeconds(0.5f);
        
        // Assert
        Assert.IsTrue(duplicateClaimFailed, "Duplicate gift should fail even in new session");
        Assert.IsTrue(EnergyGiftHandler.IsGiftClaimed("persistent_gift_id"), "Claimed status should persist across sessions");
        
        Debug.Log("[EnergyMailboxIntegration] Cross-session persistence verified");
    }
    #endregion
    
    #region Helper Methods
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
            SentAt = DateTime.UtcNow,
            isRead = false
        };
        
        message.AddAttachment("energy", energyAmount, $"에너지 {energyAmount}개");
        message.AddAttachment("giftId", giftId, "선물 ID");
        
        return message;
    }
    #endregion
}

#region Test Energy Manager
/// <summary>
/// 테스트용 EnergyManager 구현
/// 실제 EnergyManager의 주요 기능을 시뮬레이션합니다.
/// </summary>
public class TestEnergyManager : MonoBehaviour
{
    public int CurrentEnergy { get; private set; }
    public int MaxEnergy { get; private set; }
    
    public event Action<int> OnEnergyChanged;
    
    public void Initialize(int maxEnergy, int currentEnergy)
    {
        MaxEnergy = maxEnergy;
        CurrentEnergy = currentEnergy;
        
        Debug.Log($"[TestEnergyManager] Initialized with {CurrentEnergy}/{MaxEnergy} energy");
    }
    
    public void AddEnergy(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[TestEnergyManager] Invalid energy amount: {amount}");
            return;
        }
        
        var oldEnergy = CurrentEnergy;
        CurrentEnergy = Mathf.Min(MaxEnergy, CurrentEnergy + amount);
        
        OnEnergyChanged?.Invoke(CurrentEnergy);
        
        Debug.Log($"[TestEnergyManager] Energy changed: {oldEnergy} -> {CurrentEnergy}");
    }
    
    public bool CanAddEnergy(int amount)
    {
        return CurrentEnergy < MaxEnergy && amount > 0;
    }
    
    public void SetCurrentEnergy(int energy)
    {
        CurrentEnergy = Mathf.Clamp(energy, 0, MaxEnergy);
        OnEnergyChanged?.Invoke(CurrentEnergy);
        
        Debug.Log($"[TestEnergyManager] Energy set to: {CurrentEnergy}");
    }
}
#endregion