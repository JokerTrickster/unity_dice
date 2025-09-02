using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MatchingConfig 단위 테스트
/// 설정 관리, 유효성 검사, 게임 모드 설정을 검증합니다.
/// </summary>
public class MatchingConfigTests
{
    private MatchingConfig config;
    
    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<MatchingConfig>();
        config.ResetToDefault();
    }
    
    [TearDown]
    public void TearDown()
    {
        if (config != null)
        {
            Object.DestroyImmediate(config);
        }
    }

    #region Initialization Tests
    [Test]
    public void ResetToDefault_ShouldSetValidDefaults()
    {
        // Act
        config.ResetToDefault();
        
        // Assert
        Assert.IsTrue(config.EnableMatching, "Matching should be enabled by default");
        Assert.AreEqual(300f, config.MaxWaitTimeSeconds, "Max wait time should be 5 minutes");
        Assert.AreEqual(60f, config.MatchingTimeoutSeconds, "Matching timeout should be 1 minute");
        Assert.AreEqual(3, config.MaxRetryAttempts, "Max retry attempts should be 3");
        Assert.AreEqual(5f, config.RetryDelaySeconds, "Retry delay should be 5 seconds");
        Assert.AreEqual(2, config.MinPlayersPerMatch, "Min players should be 2");
        Assert.AreEqual(4, config.MaxPlayersPerMatch, "Max players should be 4");
        Assert.AreEqual(new int[] { 2, 3, 4 }, config.AllowedPlayerCounts, "Allowed player counts should be 2, 3, 4");
        Assert.AreEqual("/api/v1/matching", config.MatchingEndpoint, "Matching endpoint should be correct");
        Assert.IsTrue(config.EnableDebugLogs, "Debug logs should be enabled by default");
    }
    
    [Test]
    public void ValidateConfiguration_WithDefaults_ShouldReturnTrue()
    {
        // Act
        bool isValid = config.ValidateConfiguration();
        
        // Assert
        Assert.IsTrue(isValid, "Default configuration should be valid");
    }
    #endregion

    #region Configuration Validation Tests
    [Test]
    public void ValidateConfiguration_WithInvalidTimeouts_ShouldReturnFalse()
    {
        // Arrange - set invalid timeout
        var timeoutField = typeof(MatchingConfig).GetField("maxWaitTimeSeconds", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        timeoutField?.SetValue(config, -1f);
        
        // Act
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Invalid timeout settings.*"));
        bool isValid = config.ValidateConfiguration();
        
        // Assert
        Assert.IsFalse(isValid, "Configuration with invalid timeout should be invalid");
    }
    
    [Test]
    public void ValidateConfiguration_WithInvalidPlayerCounts_ShouldReturnFalse()
    {
        // Arrange - set invalid player counts
        var minPlayersField = typeof(MatchingConfig).GetField("minPlayersPerMatch", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        minPlayersField?.SetValue(config, 0);
        
        // Act
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Invalid player count settings.*"));
        bool isValid = config.ValidateConfiguration();
        
        // Assert
        Assert.IsFalse(isValid, "Configuration with invalid player counts should be invalid");
    }
    
    [Test]
    public void ValidateConfiguration_WithEmptyAllowedPlayerCounts_ShouldReturnFalse()
    {
        // Arrange - set empty allowed player counts
        var allowedCountsField = typeof(MatchingConfig).GetField("allowedPlayerCounts", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allowedCountsField?.SetValue(config, new int[0]);
        
        // Act
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*No allowed player counts.*"));
        bool isValid = config.ValidateConfiguration();
        
        // Assert
        Assert.IsFalse(isValid, "Configuration with no allowed player counts should be invalid");
    }
    
    [Test]
    public void ValidateConfiguration_WithOutOfRangePlayerCounts_ShouldReturnFalse()
    {
        // Arrange - set out of range allowed player counts
        var allowedCountsField = typeof(MatchingConfig).GetField("allowedPlayerCounts", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        allowedCountsField?.SetValue(config, new int[] { 1, 5 }); // Outside 2-4 range
        
        // Act
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*outside allowed range.*"));
        bool isValid = config.ValidateConfiguration();
        
        // Assert
        Assert.IsFalse(isValid, "Configuration with out of range player counts should be invalid");
    }
    #endregion

    #region Game Mode Configuration Tests
    [Test]
    public void GetGameModeConfig_ForAllModes_ShouldReturnValidConfigs()
    {
        // Test all game modes
        foreach (GameMode mode in System.Enum.GetValues(typeof(GameMode)))
        {
            var modeConfig = config.GetGameModeConfig(mode);
            
            Assert.IsNotNull(modeConfig, $"Config for {mode} should not be null");
            Assert.AreEqual(mode, modeConfig.gameMode, $"Game mode should match for {mode}");
            Assert.IsTrue(modeConfig.energyCost > 0, $"Energy cost should be positive for {mode}");
            Assert.IsTrue(modeConfig.minimumLevel >= 1, $"Minimum level should be at least 1 for {mode}");
            Assert.IsTrue(modeConfig.estimatedWaitTimeSeconds > 0, $"Estimated wait time should be positive for {mode}");
        }
    }
    
    [Test]
    public void GetGameModeConfig_Classic_ShouldHaveCorrectDefaults()
    {
        // Act
        var classicConfig = config.GetGameModeConfig(GameMode.Classic);
        
        // Assert
        Assert.AreEqual(GameMode.Classic, classicConfig.gameMode);
        Assert.AreEqual("Classic", classicConfig.displayName);
        Assert.AreEqual(1, classicConfig.energyCost, "Classic should cost 1 energy");
        Assert.AreEqual(1, classicConfig.minimumLevel, "Classic should require level 1");
        Assert.AreEqual(30f, classicConfig.estimatedWaitTimeSeconds, "Classic should have 30s wait time");
        Assert.IsTrue(classicConfig.isEnabled, "Classic should be enabled by default");
        Assert.AreEqual("Standard 4-player dice game", classicConfig.description);
    }
    
    [Test]
    public void GetGameModeConfig_Speed_ShouldHaveCorrectDefaults()
    {
        // Act
        var speedConfig = config.GetGameModeConfig(GameMode.Speed);
        
        // Assert
        Assert.AreEqual(GameMode.Speed, speedConfig.gameMode);
        Assert.AreEqual("Speed", speedConfig.displayName);
        Assert.AreEqual(2, speedConfig.energyCost, "Speed should cost 2 energy");
        Assert.AreEqual(5, speedConfig.minimumLevel, "Speed should require level 5");
        Assert.AreEqual(45f, speedConfig.estimatedWaitTimeSeconds, "Speed should have 45s wait time");
        Assert.IsTrue(speedConfig.isEnabled, "Speed should be enabled by default");
    }
    
    [Test]
    public void GetGameModeConfig_Challenge_ShouldHaveCorrectDefaults()
    {
        // Act
        var challengeConfig = config.GetGameModeConfig(GameMode.Challenge);
        
        // Assert
        Assert.AreEqual(GameMode.Challenge, challengeConfig.gameMode);
        Assert.AreEqual("Challenge", challengeConfig.displayName);
        Assert.AreEqual(3, challengeConfig.energyCost, "Challenge should cost 3 energy");
        Assert.AreEqual(10, challengeConfig.minimumLevel, "Challenge should require level 10");
        Assert.AreEqual(60f, challengeConfig.estimatedWaitTimeSeconds, "Challenge should have 60s wait time");
        Assert.IsTrue(challengeConfig.isEnabled, "Challenge should be enabled by default");
    }
    
    [Test]
    public void GetGameModeConfig_Ranked_ShouldHaveCorrectDefaults()
    {
        // Act
        var rankedConfig = config.GetGameModeConfig(GameMode.Ranked);
        
        // Assert
        Assert.AreEqual(GameMode.Ranked, rankedConfig.gameMode);
        Assert.AreEqual("Ranked", rankedConfig.displayName);
        Assert.AreEqual(2, rankedConfig.energyCost, "Ranked should cost 2 energy");
        Assert.AreEqual(15, rankedConfig.minimumLevel, "Ranked should require level 15");
        Assert.AreEqual(90f, rankedConfig.estimatedWaitTimeSeconds, "Ranked should have 90s wait time");
        Assert.IsTrue(rankedConfig.isEnabled, "Ranked should be enabled by default");
    }
    
    [Test]
    public void GetAllGameModeConfigs_ShouldReturnAllConfigs()
    {
        // Act
        var allConfigs = config.GetAllGameModeConfigs();
        
        // Assert
        Assert.AreEqual(4, allConfigs.Count, "Should return 4 game mode configurations");
        
        var gameModes = new HashSet<GameMode>();
        foreach (var modeConfig in allConfigs)
        {
            gameModes.Add(modeConfig.gameMode);
        }
        
        Assert.Contains(GameMode.Classic, gameModes.ToArray(), "Should contain Classic mode");
        Assert.Contains(GameMode.Speed, gameModes.ToArray(), "Should contain Speed mode");
        Assert.Contains(GameMode.Challenge, gameModes.ToArray(), "Should contain Challenge mode");
        Assert.Contains(GameMode.Ranked, gameModes.ToArray(), "Should contain Ranked mode");
    }
    
    [Test]
    public void IsGameModeEnabled_WithEnabledMode_ShouldReturnTrue()
    {
        // Act & Assert
        Assert.IsTrue(config.IsGameModeEnabled(GameMode.Classic), "Classic should be enabled");
        Assert.IsTrue(config.IsGameModeEnabled(GameMode.Speed), "Speed should be enabled");
        Assert.IsTrue(config.IsGameModeEnabled(GameMode.Challenge), "Challenge should be enabled");
        Assert.IsTrue(config.IsGameModeEnabled(GameMode.Ranked), "Ranked should be enabled");
    }
    
    [Test]
    public void IsGameModeEnabled_WithDisabledMode_ShouldReturnFalse()
    {
        // Arrange - Disable Classic mode
        var classicConfig = config.GetGameModeConfig(GameMode.Classic);
        classicConfig.isEnabled = false;
        
        // Act & Assert
        Assert.IsFalse(config.IsGameModeEnabled(GameMode.Classic), "Classic should be disabled");
    }
    #endregion

    #region Player Count Validation Tests
    [Test]
    public void IsPlayerCountAllowed_WithAllowedCounts_ShouldReturnTrue()
    {
        // Act & Assert
        Assert.IsTrue(config.IsPlayerCountAllowed(2), "2 players should be allowed");
        Assert.IsTrue(config.IsPlayerCountAllowed(3), "3 players should be allowed");
        Assert.IsTrue(config.IsPlayerCountAllowed(4), "4 players should be allowed");
    }
    
    [Test]
    public void IsPlayerCountAllowed_WithDisallowedCounts_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.IsFalse(config.IsPlayerCountAllowed(1), "1 player should not be allowed");
        Assert.IsFalse(config.IsPlayerCountAllowed(5), "5 players should not be allowed");
        Assert.IsFalse(config.IsPlayerCountAllowed(0), "0 players should not be allowed");
    }
    #endregion

    #region Property Access Tests
    [Test]
    public void Properties_ShouldReturnCorrectValues()
    {
        // Assert all properties return expected default values
        Assert.IsTrue(config.EnableMatching);
        Assert.AreEqual(300f, config.MaxWaitTimeSeconds);
        Assert.AreEqual(60f, config.MatchingTimeoutSeconds);
        Assert.AreEqual(3, config.MaxRetryAttempts);
        Assert.AreEqual(5f, config.RetryDelaySeconds);
        Assert.AreEqual(2, config.MinPlayersPerMatch);
        Assert.AreEqual(4, config.MaxPlayersPerMatch);
        Assert.AreEqual(new int[] { 2, 3, 4 }, config.AllowedPlayerCounts);
        Assert.AreEqual("/api/v1/matching", config.MatchingEndpoint);
        Assert.AreEqual(30f, config.HeartbeatIntervalSeconds);
        Assert.AreEqual(15f, config.ConnectionTimeoutSeconds);
        Assert.AreEqual(1f, config.UIUpdateIntervalSeconds);
        Assert.IsTrue(config.ShowEstimatedWaitTime);
        Assert.IsTrue(config.EnableMatchingAnimations);
        Assert.IsTrue(config.EnableDebugLogs);
        Assert.IsFalse(config.SimulateNetworkDelay);
        Assert.AreEqual(2f, config.SimulatedNetworkDelaySeconds);
    }
    #endregion

    #region Edge Cases Tests
    [Test]
    public void GameModeConfig_WithNullCustomSettings_ShouldWork()
    {
        // Arrange
        var classicConfig = config.GetGameModeConfig(GameMode.Classic);
        
        // Act & Assert - Should not throw exception
        Assert.DoesNotThrow(() => {
            var settings = classicConfig.customSettings;
            Assert.IsNotNull(settings, "Custom settings should not be null");
        });
    }
    
    [Test]
    public void AllowedPlayerCounts_Modification_ShouldNotAffectOriginal()
    {
        // Arrange
        var originalCounts = config.AllowedPlayerCounts;
        
        // Act - Try to modify the returned array
        originalCounts[0] = 999;
        
        // Assert - Original configuration should be unchanged
        var newCounts = config.AllowedPlayerCounts;
        Assert.AreEqual(2, newCounts[0], "Original configuration should not be modified");
    }
    #endregion
}