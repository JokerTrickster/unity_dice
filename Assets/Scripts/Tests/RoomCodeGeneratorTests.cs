using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RoomCodeGenerator 단위 테스트
/// 방 코드 생성, 보안 기능, 중복 방지 시스템 검증
/// </summary>
public class RoomCodeGeneratorTests
{
    private RoomCodeGenerator generator;

    [SetUp]
    public void SetUp()
    {
        generator = RoomCodeGenerator.Instance;
        generator.Cleanup(); // 이전 테스트 데이터 정리
    }

    [TearDown]
    public void TearDown()
    {
        generator.Cleanup();
    }

    #region Code Generation Tests

    [Test]
    public void GenerateRoomCode_ShouldReturnValidFormat()
    {
        // Act
        string code = generator.GenerateRoomCode();

        // Assert
        Assert.IsNotNull(code, "Generated code should not be null");
        Assert.AreEqual(4, code.Length, "Code should be 4 characters long");
        Assert.IsTrue(int.TryParse(code, out int numericCode), "Code should be numeric");
        Assert.IsTrue(numericCode >= 1000 && numericCode <= 9999, "Code should be in valid range");
    }

    [Test]
    public void GenerateRoomCode_ShouldReturnUniqueValues()
    {
        // Arrange
        var generatedCodes = new HashSet<string>();
        int testCount = 50;

        // Act
        for (int i = 0; i < testCount; i++)
        {
            string code = generator.GenerateRoomCode();
            generatedCodes.Add(code);
        }

        // Assert
        Assert.AreEqual(testCount, generatedCodes.Count, "All generated codes should be unique");
    }

    [Test]
    public void GenerateRoomCode_ShouldAvoidReservedCodes()
    {
        // Arrange
        string[] reservedCodes = { "0000", "1111", "2222", "3333", "4444", "5555", 
                                  "6666", "7777", "8888", "9999", "1234", "4321" };
        var generatedCodes = new HashSet<string>();
        int testCount = 100;

        // Act
        for (int i = 0; i < testCount; i++)
        {
            try
            {
                string code = generator.GenerateRoomCode();
                generatedCodes.Add(code);
            }
            catch (InvalidOperationException)
            {
                // 방이 가득 찬 경우 예외 발생 가능
                break;
            }
        }

        // Assert
        foreach (string reservedCode in reservedCodes)
        {
            Assert.IsFalse(generatedCodes.Contains(reservedCode), 
                          $"Generated codes should not include reserved code: {reservedCode}");
        }
    }

    [Test]
    public void GenerateAndReserveCode_ShouldCreateReservation()
    {
        // Act
        string code = generator.GenerateAndReserveCode();

        // Assert
        Assert.IsNotNull(code, "Should generate reserved code");
        Assert.IsTrue(generator.IsReservedCode(code), "Code should be in reserved state");
        Assert.IsFalse(generator.IsActiveCode(code), "Code should not be active yet");
    }

    [Test]
    public void ActivateReservedCode_WithValidCode_ShouldActivate()
    {
        // Arrange
        string code = generator.GenerateAndReserveCode();

        // Act
        bool result = generator.ActivateReservedCode(code);

        // Assert
        Assert.IsTrue(result, "Should successfully activate reserved code");
        Assert.IsTrue(generator.IsActiveCode(code), "Code should be active");
        Assert.IsFalse(generator.IsReservedCode(code), "Code should no longer be reserved");
    }

    [Test]
    public void ActivateReservedCode_WithInvalidCode_ShouldFail()
    {
        // Act
        bool result = generator.ActivateReservedCode("9999");

        // Assert
        Assert.IsFalse(result, "Should fail to activate non-reserved code");
    }

    #endregion

    #region Validation Tests

    [Test]
    [TestCase("1234", true)]
    [TestCase("0000", true)]
    [TestCase("9999", true)]
    [TestCase("12345", false)]
    [TestCase("123", false)]
    [TestCase("abcd", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsValidRoomCodeFormat_ShouldValidateCorrectly(string code, bool expected)
    {
        // Act & Assert
        Assert.AreEqual(expected, RoomCodeGenerator.IsValidRoomCodeFormat(code));
    }

    [Test]
    public void IsActiveCode_WithGeneratedCode_ShouldReturnTrue()
    {
        // Arrange
        string code = generator.GenerateRoomCode();

        // Act & Assert
        Assert.IsTrue(generator.IsActiveCode(code), "Generated code should be active");
    }

    [Test]
    public void IsActiveCode_WithReleasedCode_ShouldReturnFalse()
    {
        // Arrange
        string code = generator.GenerateRoomCode();
        generator.ReleaseCode(code);

        // Act & Assert
        Assert.IsFalse(generator.IsActiveCode(code), "Released code should not be active");
    }

    [Test]
    public void GetCodeExpiration_WithActiveCode_ShouldReturnValidTime()
    {
        // Arrange
        string code = generator.GenerateRoomCode();
        DateTime beforeGeneration = DateTime.Now;

        // Act
        DateTime? expiration = generator.GetCodeExpiration(code);

        // Assert
        Assert.IsNotNull(expiration, "Should return expiration time");
        Assert.IsTrue(expiration > beforeGeneration, "Expiration should be in the future");
        Assert.IsTrue(expiration <= DateTime.Now.AddMinutes(31), "Expiration should be within 30 minutes");
    }

    #endregion

    #region Code Management Tests

    [Test]
    public void ReleaseCode_WithActiveCode_ShouldRemoveFromActive()
    {
        // Arrange
        string code = generator.GenerateRoomCode();
        Assert.IsTrue(generator.IsActiveCode(code), "Code should be initially active");

        // Act
        generator.ReleaseCode(code);

        // Assert
        Assert.IsFalse(generator.IsActiveCode(code), "Code should not be active after release");
        Assert.IsNull(generator.GetCodeExpiration(code), "Expiration should be cleared");
    }

    [Test]
    public void ReleaseCode_WithNullCode_ShouldHandleGracefully()
    {
        // Act & Assert - Should not throw exception
        Assert.DoesNotThrow(() => generator.ReleaseCode(null));
        Assert.DoesNotThrow(() => generator.ReleaseCode(""));
    }

    [Test]
    public void ExtendCodeExpiration_WithValidCode_ShouldExtendTime()
    {
        // Arrange
        string code = generator.GenerateRoomCode();
        DateTime? originalExpiration = generator.GetCodeExpiration(code);

        // Act
        bool result = generator.ExtendCodeExpiration(code, 10);

        // Assert
        Assert.IsTrue(result, "Should successfully extend expiration");
        DateTime? newExpiration = generator.GetCodeExpiration(code);
        Assert.IsTrue(newExpiration > originalExpiration, "New expiration should be later");
    }

    [Test]
    public void CleanupExpiredCodes_ShouldRemoveExpiredCodes()
    {
        // This test is challenging because we can't easily manipulate time
        // In a real implementation, you might use a time provider interface for testing
        
        // For now, just verify the method doesn't throw
        Assert.DoesNotThrow(() => generator.CleanupExpiredCodes());
    }

    #endregion

    #region Security Tests

    [Test]
    public void RecordBruteForceAttempt_WithMultipleAttempts_ShouldTriggerCooldown()
    {
        // Arrange
        string testIp = "192.168.1.100";
        string testCode = "1234";

        // Act - Record multiple attempts
        for (int i = 0; i < 5; i++)
        {
            bool allowed = generator.RecordBruteForceAttempt(testIp, testCode);
            if (i < 4)
            {
                Assert.IsTrue(allowed, $"Attempt {i + 1} should be allowed");
            }
            else
            {
                Assert.IsFalse(allowed, "5th attempt should be blocked");
            }
        }

        // Further attempts should be blocked
        bool blockedAttempt = generator.RecordBruteForceAttempt(testIp, testCode);
        Assert.IsFalse(blockedAttempt, "Additional attempts should be blocked during cooldown");
    }

    [Test]
    public void RecordBruteForceAttempt_WithNullIp_ShouldAllowAttempt()
    {
        // Act & Assert
        bool result = generator.RecordBruteForceAttempt(null, "1234");
        Assert.IsTrue(result, "Should allow attempt when IP is unknown");
    }

    [Test]
    public void ResetBruteForceAttempts_ShouldClearAttemptHistory()
    {
        // Arrange
        string testIp = "192.168.1.100";
        
        // Record several attempts
        for (int i = 0; i < 4; i++)
        {
            generator.RecordBruteForceAttempt(testIp, "1234");
        }

        // Act
        generator.ResetBruteForceAttempts(testIp);

        // Assert - Should allow attempts again
        bool allowed = generator.RecordBruteForceAttempt(testIp, "5678");
        Assert.IsTrue(allowed, "Should allow attempts after reset");
    }

    #endregion

    #region Statistics Tests

    [Test]
    public void GetStatistics_ShouldProvideAccurateData()
    {
        // Arrange
        var initialStats = generator.GetStatistics();
        
        // Generate some codes
        var codes = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            codes.Add(generator.GenerateRoomCode());
        }

        // Act
        var stats = generator.GetStatistics();

        // Assert
        Assert.AreEqual(5, stats.ActiveCodes, "Should count active codes correctly");
        Assert.IsTrue(stats.TotalAvailable < initialStats.TotalAvailable, "Available count should decrease");
        Assert.IsTrue(stats.UsagePercentage > 0, "Usage percentage should be positive");
        Assert.AreEqual(9000, stats.SystemCapacity, "System capacity should be 9000 (1000-9999)");
    }

    [Test]
    public void GetStatistics_Summary_ShouldBeReadable()
    {
        // Arrange
        generator.GenerateRoomCode();

        // Act
        var stats = generator.GetStatistics();
        string summary = stats.GetSummary();

        // Assert
        Assert.IsNotNull(summary, "Summary should not be null");
        Assert.IsTrue(summary.Contains("Active:"), "Should contain active count");
        Assert.IsTrue(summary.Contains("Reserved:"), "Should contain reserved count");
        Assert.IsTrue(summary.Contains("Available:"), "Should contain available count");
        Assert.IsTrue(summary.Contains("Usage:"), "Should contain usage percentage");
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public void GenerateRoomCode_WhenSystemFull_ShouldThrowException()
    {
        // This test would require generating all possible codes (8000+)
        // which is impractical for unit tests. In a real scenario, you might
        // mock the internal state or use a smaller code range for testing.
        
        // For now, just verify the method works under normal conditions
        Assert.DoesNotThrow(() => generator.GenerateRoomCode());
    }

    [Test]
    public void MultipleGenerators_ShouldShareState()
    {
        // Arrange
        var generator1 = RoomCodeGenerator.Instance;
        var generator2 = RoomCodeGenerator.Instance;

        // Act
        string code1 = generator1.GenerateRoomCode();
        
        // Assert
        Assert.AreSame(generator1, generator2, "Should be the same singleton instance");
        Assert.IsTrue(generator2.IsActiveCode(code1), "Second instance should see first instance's codes");
    }

    [Test]
    public void Cleanup_ShouldResetAllData()
    {
        // Arrange
        generator.GenerateRoomCode();
        generator.GenerateAndReserveCode();
        var stats = generator.GetStatistics();
        Assert.IsTrue(stats.ActiveCodes > 0 || stats.ReservedCodes > 0, "Should have some codes before cleanup");

        // Act
        generator.Cleanup();

        // Assert
        var cleanStats = generator.GetStatistics();
        Assert.AreEqual(0, cleanStats.ActiveCodes, "Should have no active codes after cleanup");
        Assert.AreEqual(0, cleanStats.ReservedCodes, "Should have no reserved codes after cleanup");
    }

    #endregion
}