using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 에너지 섹션 및 관련 시스템에 대한 종합적인 테스트
/// </summary>
public class EnergySectionTests
{
    #region Test Data and Setup
    private EnergySection _energySection;
    private EnergyManager _energyManager;
    private EnergyRecoverySystem _recoverySystem;
    private EnergyEconomySystem _economySystem;
    private EnergyValidationSystem _validationSystem;
    private EnergyConfig _testConfig;
    private GameObject _testGameObject;

    [SetUp]
    public void SetUp()
    {
        // 테스트 게임 오브젝트 생성
        _testGameObject = new GameObject("EnergySection Test");
        _energySection = _testGameObject.AddComponent<EnergySection>();

        // 테스트 설정 생성
        _testConfig = new EnergyConfig
        {
            MaxEnergy = 100,
            RechargeRate = 10,
            RechargeInterval = TimeSpan.FromSeconds(1), // 테스트를 위해 짧게 설정
            LowEnergyThreshold = 0.2f,
            MaxEnergyPurchaseAmount = 50,
            EnergyPurchaseCost = 5,
            EnableEnergyRecovery = true,
            EnableEnergyPurchase = true,
            EnableEnergyValidation = true
        };

        // 개별 시스템 생성
        _energyManager = new EnergyManager(_testConfig);
        _recoverySystem = new EnergyRecoverySystem(_testConfig, _energyManager);
        _economySystem = new EnergyEconomySystem(_testConfig, _energyManager);
        _validationSystem = new EnergyValidationSystem(_energyManager);
    }

    [TearDown]
    public void TearDown()
    {
        // 정리
        _energyManager?.Cleanup();
        _recoverySystem?.Cleanup();
        _economySystem?.Cleanup();
        _validationSystem?.Cleanup();

        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
    }
    #endregion

    #region EnergyManager Tests
    [Test]
    public void EnergyManager_InitializedCorrectly()
    {
        // Arrange & Act
        var manager = new EnergyManager(_testConfig);

        // Assert
        Assert.IsTrue(manager.IsInitialized, "EnergyManager should be initialized");
        Assert.AreEqual(_testConfig.MaxEnergy, manager.MaxEnergy, "Max energy should match config");
        Assert.AreEqual(_testConfig.MaxEnergy, manager.CurrentEnergy, "Should start with full energy");
        Assert.IsTrue(manager.IsEnergyFull, "Should start with full energy");
        Assert.IsFalse(manager.IsEnergyLow, "Should not be low energy initially");
        Assert.IsTrue(manager.CanUseEnergy, "Should be able to use energy initially");

        manager.Cleanup();
    }

    [Test]
    public void EnergyManager_ConsumeEnergy_ValidAmount_Success()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        int consumeAmount = 30;
        bool eventTriggered = false;
        int eventCurrentEnergy = 0, eventMaxEnergy = 0;

        manager.OnEnergyChanged += (current, max) =>
        {
            eventTriggered = true;
            eventCurrentEnergy = current;
            eventMaxEnergy = max;
        };

        // Act
        bool result = manager.ConsumeEnergy(consumeAmount);

        // Assert
        Assert.IsTrue(result, "Energy consumption should succeed");
        Assert.AreEqual(_testConfig.MaxEnergy - consumeAmount, manager.CurrentEnergy, "Current energy should be reduced");
        Assert.IsTrue(eventTriggered, "OnEnergyChanged event should be triggered");
        Assert.AreEqual(manager.CurrentEnergy, eventCurrentEnergy, "Event should report correct current energy");
        Assert.AreEqual(manager.MaxEnergy, eventMaxEnergy, "Event should report correct max energy");

        manager.Cleanup();
    }

    [Test]
    public void EnergyManager_ConsumeEnergy_InsufficientEnergy_Failure()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(95); // 거의 모든 에너지 소비
        int remainingEnergy = manager.CurrentEnergy;
        bool eventTriggered = false;

        manager.OnEnergyChanged += (current, max) => eventTriggered = true;

        // Act
        bool result = manager.ConsumeEnergy(remainingEnergy + 10); // 남은 에너지보다 많이 소비 시도

        // Assert
        Assert.IsFalse(result, "Energy consumption should fail with insufficient energy");
        Assert.AreEqual(remainingEnergy, manager.CurrentEnergy, "Energy should remain unchanged");
        Assert.IsFalse(eventTriggered, "OnEnergyChanged event should not be triggered");

        manager.Cleanup();
    }

    [Test]
    public void EnergyManager_AddEnergy_ValidAmount_Success()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(50); // 에너지를 50 소비
        int addAmount = 20;
        bool eventTriggered = false;

        manager.OnEnergyChanged += (current, max) => eventTriggered = true;

        // Act
        manager.AddEnergy(addAmount);

        // Assert
        Assert.AreEqual(70, manager.CurrentEnergy, "Energy should be added correctly");
        Assert.IsTrue(eventTriggered, "OnEnergyChanged event should be triggered");

        manager.Cleanup();
    }

    [Test]
    public void EnergyManager_AddEnergy_ExceedsMax_CappedAtMax()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(10); // 10 소비 (90 남음)
        bool fullEventTriggered = false;

        manager.OnEnergyFull += () => fullEventTriggered = true;

        // Act
        manager.AddEnergy(50); // 90 + 50 = 140, 하지만 최대 100

        // Assert
        Assert.AreEqual(_testConfig.MaxEnergy, manager.CurrentEnergy, "Energy should be capped at max");
        Assert.IsTrue(manager.IsEnergyFull, "Should be full energy");
        Assert.IsTrue(fullEventTriggered, "OnEnergyFull event should be triggered");

        manager.Cleanup();
    }

    [Test]
    public void EnergyManager_EnergyDepletion_TriggersEvent()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        bool depletedEventTriggered = false;

        manager.OnEnergyDepleted += () => depletedEventTriggered = true;

        // Act
        manager.ConsumeEnergy(_testConfig.MaxEnergy); // 모든 에너지 소비

        // Assert
        Assert.AreEqual(0, manager.CurrentEnergy, "Energy should be completely depleted");
        Assert.IsFalse(manager.CanUseEnergy, "Should not be able to use energy");
        Assert.IsTrue(depletedEventTriggered, "OnEnergyDepleted event should be triggered");

        manager.Cleanup();
    }
    #endregion

    #region EnergyRecoverySystem Tests
    [UnityTest]
    public IEnumerator EnergyRecoverySystem_AutoRecovery_Works()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var recovery = new EnergyRecoverySystem(_testConfig, manager);
        
        manager.ConsumeEnergy(50); // 에너지를 50 소비
        int initialEnergy = manager.CurrentEnergy;
        bool recoveryEventTriggered = false;
        int recoveredAmount = 0;

        recovery.OnEnergyRecovered += amount =>
        {
            recoveryEventTriggered = true;
            recoveredAmount = amount;
        };

        // Act - 회복 간격보다 조금 더 기다리기
        yield return new WaitForSeconds(1.5f);
        recovery.TryRecoverEnergy();

        // Assert
        Assert.IsTrue(recoveryEventTriggered, "Energy recovery event should be triggered");
        Assert.AreEqual(_testConfig.RechargeRate, recoveredAmount, "Recovered amount should match config");
        Assert.Greater(manager.CurrentEnergy, initialEnergy, "Energy should have increased");

        manager.Cleanup();
        recovery.Cleanup();
    }

    [UnityTest]
    public IEnumerator EnergyRecoverySystem_PendingRecoveries_ProcessedCorrectly()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var recovery = new EnergyRecoverySystem(_testConfig, manager);
        
        manager.ConsumeEnergy(80); // 에너지를 80 소비 (20 남음)
        int initialEnergy = manager.CurrentEnergy;

        // 여러 회복 기회가 지나가도록 시뮬레이션
        recovery.ResetRecoveryTime();
        yield return new WaitForSeconds(3.5f); // 3회 회복 기회 이상

        // Act
        int recoveredTotal = recovery.ProcessPendingRecoveries();

        // Assert
        Assert.Greater(recoveredTotal, 0, "Should recover some energy");
        Assert.Greater(manager.CurrentEnergy, initialEnergy, "Energy should have increased");
        Assert.LessOrEqual(manager.CurrentEnergy, _testConfig.MaxEnergy, "Energy should not exceed max");

        manager.Cleanup();
        recovery.Cleanup();
    }

    [Test]
    public void EnergyRecoverySystem_ForceRecharge_Works()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var recovery = new EnergyRecoverySystem(_testConfig, manager);
        
        manager.ConsumeEnergy(30);
        int initialEnergy = manager.CurrentEnergy;
        bool eventTriggered = false;

        recovery.OnEnergyRecovered += amount => eventTriggered = true;

        // Act
        bool result = recovery.ForceRecharge();

        // Assert
        Assert.IsTrue(result, "Force recharge should succeed");
        Assert.IsTrue(eventTriggered, "Recovery event should be triggered");
        Assert.Greater(manager.CurrentEnergy, initialEnergy, "Energy should have increased");

        manager.Cleanup();
        recovery.Cleanup();
    }

    [Test]
    public void EnergyRecoverySystem_FullEnergy_NoRecovery()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var recovery = new EnergyRecoverySystem(_testConfig, manager);
        
        // 에너지가 이미 가득참
        bool eventTriggered = false;
        recovery.OnEnergyRecovered += amount => eventTriggered = true;

        // Act
        bool result = recovery.ForceRecharge();

        // Assert
        Assert.IsFalse(result, "Force recharge should fail when energy is full");
        Assert.IsFalse(eventTriggered, "Recovery event should not be triggered");

        manager.Cleanup();
        recovery.Cleanup();
    }
    #endregion

    #region EnergyEconomySystem Tests
    [Test]
    public void EnergyEconomySystem_GetPurchaseQuote_ValidAmount_ReturnsCorrectQuote()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(50); // 50 에너지 소비
        var economy = new EnergyEconomySystem(_testConfig, manager);
        int purchaseAmount = 25;

        // Act
        var quote = economy.GetPurchaseQuote(purchaseAmount);

        // Assert
        Assert.IsNotNull(quote, "Quote should not be null");
        Assert.IsTrue(quote.IsValid, "Quote should be valid");
        Assert.AreEqual(purchaseAmount, quote.RequestedAmount, "Requested amount should match");
        Assert.AreEqual(purchaseAmount, quote.ActualAmount, "Actual amount should match requested (no cap reached)");
        Assert.Greater(quote.TotalCost, 0, "Total cost should be greater than 0");
        Assert.AreEqual(75, quote.EnergyAfterPurchase, "Energy after purchase should be calculated correctly");

        manager.Cleanup();
        economy.Cleanup();
    }

    [Test]
    public void EnergyEconomySystem_GetPurchaseQuote_ExceedsCapacity_ReturnsAdjustedQuote()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(10); // 10 에너지 소비 (90 남음)
        var economy = new EnergyEconomySystem(_testConfig, manager);
        int purchaseAmount = 50; // 최대 100까지만 가능하므로 10개만 구매 가능

        // Act
        var quote = economy.GetPurchaseQuote(purchaseAmount);

        // Assert
        Assert.IsNotNull(quote, "Quote should not be null");
        Assert.IsTrue(quote.IsValid, "Quote should be valid");
        Assert.AreEqual(purchaseAmount, quote.RequestedAmount, "Requested amount should be preserved");
        Assert.AreEqual(10, quote.ActualAmount, "Actual amount should be limited to available space");
        Assert.AreEqual(100, quote.EnergyAfterPurchase, "Should reach max energy");

        manager.Cleanup();
        economy.Cleanup();
    }

    [Test]
    public void EnergyEconomySystem_PurchaseEnergy_Success()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(40); // 40 에너지 소비
        var economy = new EnergyEconomySystem(_testConfig, manager);
        int purchaseAmount = 20;
        bool eventTriggered = false;
        EnergyTransactionResult eventResult = null;

        economy.OnTransactionCompleted += result =>
        {
            eventTriggered = true;
            eventResult = result;
        };

        // Act
        var result = economy.PurchaseEnergy(purchaseAmount);

        // Assert
        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsTrue(result.Success, "Purchase should succeed");
        Assert.AreEqual(purchaseAmount, result.ActualAmount, "Actual amount should match requested");
        Assert.Greater(result.Cost, 0, "Cost should be greater than 0");
        Assert.AreEqual(80, manager.CurrentEnergy, "Energy should be increased after purchase");
        Assert.IsTrue(eventTriggered, "Transaction event should be triggered");
        Assert.IsNotNull(eventResult, "Event result should not be null");
        Assert.IsTrue(eventResult.Success, "Event result should indicate success");

        manager.Cleanup();
        economy.Cleanup();
    }

    [Test]
    public void EnergyEconomySystem_CanPurchaseEnergy_FullEnergy_ReturnsFalse()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        // 에너지가 이미 가득참
        var economy = new EnergyEconomySystem(_testConfig, manager);

        // Act
        bool canPurchase = economy.CanPurchaseEnergy(10);

        // Assert
        Assert.IsFalse(canPurchase, "Should not be able to purchase when energy is full");

        manager.Cleanup();
        economy.Cleanup();
    }

    [Test]
    public void EnergyEconomySystem_OfflineMode_DisablesPurchase()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(30);
        var economy = new EnergyEconomySystem(_testConfig, manager);
        economy.SetOfflineMode(true);

        // Act
        bool canPurchase = economy.CanPurchaseEnergy(20);
        var result = economy.PurchaseEnergy(20);

        // Assert
        Assert.IsFalse(canPurchase, "Should not be able to purchase in offline mode");
        Assert.IsFalse(result.Success, "Purchase should fail in offline mode");
        Assert.IsNotNull(result.ErrorMessage, "Should have error message");

        manager.Cleanup();
        economy.Cleanup();
    }
    #endregion

    #region EnergyValidationSystem Tests
    [Test]
    public void EnergyValidationSystem_CanUseEnergy_SufficientEnergy_ReturnsTrue()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        int useAmount = 30;

        // Act
        bool canUse = validation.CanUseEnergy(useAmount);

        // Assert
        Assert.IsTrue(canUse, "Should be able to use energy when sufficient");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_CanUseEnergy_InsufficientEnergy_ReturnsFalse()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(95); // 거의 모든 에너지 소비
        var validation = new EnergyValidationSystem(manager);
        int useAmount = 30;

        // Act
        bool canUse = validation.CanUseEnergy(useAmount);

        // Assert
        Assert.IsFalse(canUse, "Should not be able to use energy when insufficient");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_ValidateEnergyUsage_ReturnsDetailedResult()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        int useAmount = 40;
        var context = new EnergyValidationContext
        {
            ActionName = "test_action",
            RequestSource = "unit_test"
        };

        // Act
        var result = validation.ValidateEnergyUsage(useAmount, context);

        // Assert
        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsTrue(result.IsValid, "Validation should pass");
        Assert.AreEqual(useAmount, result.RequestedAmount, "Requested amount should be preserved");
        Assert.AreEqual(useAmount, result.ValidatedAmount, "Validated amount should match requested");
        Assert.IsNotNull(result.ValidationContext, "Validation context should be preserved");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_ValidateGameAction_SpecificRules_Applied()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        string actionName = "dice_roll";
        int energyCost = 3;

        // Act
        bool isValid = validation.ValidateGameAction(actionName, energyCost);

        // Assert
        Assert.IsTrue(isValid, "Dice roll with valid energy cost should pass validation");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_ValidateGameAction_ExcessiveEnergy_Fails()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        string actionName = "dice_roll";
        int energyCost = 10; // 주사위 굴리기 최대 에너지(5)를 초과

        // Act
        bool isValid = validation.ValidateGameAction(actionName, energyCost);

        // Assert
        Assert.IsFalse(isValid, "Dice roll with excessive energy cost should fail validation");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_BatchValidation_MixedRequests_ReturnsCorrectResult()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        
        var requests = new List<EnergyUsageRequest>
        {
            new EnergyUsageRequest { Amount = 20 }, // Valid
            new EnergyUsageRequest { Amount = 30 }, // Valid
            new EnergyUsageRequest { Amount = 200 }, // Invalid (exceeds available)
            new EnergyUsageRequest { Amount = 10 }  // Valid
        };

        // Act
        var batchResult = validation.ValidateBatchEnergyUsage(requests);

        // Assert
        Assert.IsNotNull(batchResult, "Batch result should not be null");
        Assert.AreEqual(4, batchResult.TotalRequests, "Total requests should match");
        Assert.AreEqual(3, batchResult.ValidRequests.Count, "Should have 3 valid requests");
        Assert.AreEqual(1, batchResult.InvalidRequests.Count, "Should have 1 invalid request");
        Assert.IsFalse(batchResult.HasSufficientEnergyForAll, "Should not have sufficient energy for all (including invalid)");

        manager.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void EnergyValidationSystem_DisabledValidation_AlwaysPasses()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(99); // 거의 모든 에너지 소비
        var validation = new EnergyValidationSystem(manager);
        validation.SetValidationEnabled(false);
        int useAmount = 50; // 사용 가능한 에너지보다 많음

        // Act
        bool canUse = validation.CanUseEnergy(useAmount);
        var result = validation.ValidateEnergyUsage(useAmount);

        // Assert
        Assert.IsTrue(canUse, "Should pass when validation is disabled");
        Assert.IsTrue(result.IsValid, "Should be valid when validation is disabled");

        manager.Cleanup();
        validation.Cleanup();
    }
    #endregion

    #region Integration Tests
    [Test]
    public void Integration_EnergySystemsWorking_Together()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var recovery = new EnergyRecoverySystem(_testConfig, manager);
        var validation = new EnergyValidationSystem(manager);
        
        // Act & Assert - 여러 시스템 상호작용 테스트
        
        // 1. 초기 상태 검증
        Assert.IsTrue(validation.CanUseEnergy(50), "Should be able to use energy initially");
        
        // 2. 에너지 소비
        Assert.IsTrue(manager.ConsumeEnergy(60), "Should consume energy successfully");
        Assert.AreEqual(40, manager.CurrentEnergy, "Energy should be reduced");
        
        // 3. 유효성 검사
        Assert.IsTrue(validation.CanUseEnergy(30), "Should still be able to use some energy");
        Assert.IsFalse(validation.CanUseEnergy(50), "Should not be able to use more than available");
        
        // 4. 회복 시스템
        recovery.ForceRecharge();
        Assert.Greater(manager.CurrentEnergy, 40, "Energy should be recovered");
        
        // 5. 최종 검증
        Assert.IsTrue(validation.CanUseEnergy(45), "Should be able to use energy after recovery");
        
        // Cleanup
        manager.Cleanup();
        recovery.Cleanup();
        validation.Cleanup();
    }

    [Test]
    public void Integration_UserDataIntegration_UpdatesCorrectly()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var userData = new UserData
        {
            UserId = "test_user",
            CurrentEnergy = 75,
            MaxEnergy = 120,
            LastEnergyRechargeTime = DateTime.Now.AddMinutes(-5)
        };

        // Act
        manager.UpdateFromUserData(userData);

        // Assert
        Assert.AreEqual(75, manager.CurrentEnergy, "Current energy should match user data");
        Assert.AreEqual(120, manager.MaxEnergy, "Max energy should match user data");
        Assert.IsFalse(manager.IsEnergyFull, "Should not be full energy");
        Assert.IsFalse(manager.IsEnergyLow, "Should not be low energy");

        manager.Cleanup();
    }
    #endregion

    #region Performance Tests
    [Test]
    [Performance]
    public void Performance_EnergyManager_MultipleOperations_PerformanceAcceptable()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        int operationCount = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            manager.ConsumeEnergy(1);
            manager.AddEnergy(1);
        }

        stopwatch.Stop();

        // Assert
        Assert.Less(stopwatch.ElapsedMilliseconds, 100, "1000 operations should complete within 100ms");
        
        manager.Cleanup();
    }

    [Test]
    [Performance]
    public void Performance_ValidationSystem_BatchValidation_PerformanceAcceptable()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        var validation = new EnergyValidationSystem(manager);
        
        var requests = new List<EnergyUsageRequest>();
        for (int i = 0; i < 100; i++)
        {
            requests.Add(new EnergyUsageRequest { Amount = i % 20 + 1 });
        }
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = validation.ValidateBatchEnergyUsage(requests);

        stopwatch.Stop();

        // Assert
        Assert.Less(stopwatch.ElapsedMilliseconds, 50, "Batch validation of 100 requests should complete within 50ms");
        Assert.IsNotNull(result, "Result should not be null");

        manager.Cleanup();
        validation.Cleanup();
    }
    #endregion

    #region Edge Case Tests
    [Test]
    public void EdgeCase_ZeroEnergy_HandledCorrectly()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(_testConfig.MaxEnergy); // 모든 에너지 소비

        // Act & Assert
        Assert.AreEqual(0, manager.CurrentEnergy, "Energy should be zero");
        Assert.IsFalse(manager.CanUseEnergy, "Should not be able to use energy");
        Assert.IsFalse(manager.ConsumeEnergy(1), "Should not be able to consume any energy");
        
        // 에너지 추가는 가능해야 함
        manager.AddEnergy(10);
        Assert.AreEqual(10, manager.CurrentEnergy, "Should be able to add energy");
        Assert.IsTrue(manager.CanUseEnergy, "Should be able to use energy after adding");

        manager.Cleanup();
    }

    [Test]
    public void EdgeCase_NegativeValues_HandledCorrectly()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);

        // Act & Assert
        Assert.IsFalse(manager.ConsumeEnergy(-10), "Should not consume negative energy");
        Assert.AreEqual(_testConfig.MaxEnergy, manager.CurrentEnergy, "Energy should remain unchanged");
        
        // 음수 추가는 경고만 로그하고 무시해야 함
        manager.AddEnergy(-5);
        Assert.AreEqual(_testConfig.MaxEnergy, manager.CurrentEnergy, "Energy should remain unchanged with negative add");

        manager.Cleanup();
    }

    [Test]
    public void EdgeCase_MaxEnergyChange_HandledCorrectly()
    {
        // Arrange
        var manager = new EnergyManager(_testConfig);
        manager.ConsumeEnergy(50); // 50 에너지 소비

        // Act - 최대 에너지를 현재 에너지보다 낮게 설정
        manager.SetMaxEnergy(40);

        // Assert
        Assert.AreEqual(40, manager.MaxEnergy, "Max energy should be updated");
        Assert.AreEqual(40, manager.CurrentEnergy, "Current energy should be capped to new max");
        Assert.IsTrue(manager.IsEnergyFull, "Should be full energy after max change");

        manager.Cleanup();
    }
    #endregion
}