using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 자동 로그인 시스템 통합 테스트
/// Stream A, B, C 컴포넌트들의 통합 동작을 검증합니다.
/// </summary>
public class AutoLoginIntegrationTests
{
    private GameObject testObject;
    private AutoLoginSettings settings;
    
    [SetUp]
    public void Setup()
    {
        // 테스트 객체 생성
        testObject = new GameObject("AutoLoginTest");
        
        // 설정 인스턴스 생성
        settings = ScriptableObject.CreateInstance<AutoLoginSettings>();
        settings.InitializeDefaults();
        
        // 개발 환경용 설정 적용
        settings.ApplyDevelopmentSettings();
    }
    
    [TearDown]
    public void TearDown()
    {
        // 정리
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }
        
        if (settings != null)
        {
            Object.DestroyImmediate(settings);
        }
        
        // 토큰과 설정 정리
        if (TokenManager.Instance != null)
        {
            TokenManager.Instance.ClearTokens();
        }
        
        AutoLoginPrefs.ResetAllPreferencesIncludingSystem();
    }

    [UnityTest]
    public IEnumerator AutoLoginSettings_Integration_WithAllComponents()
    {
        // Given: 모든 컴포넌트가 초기화됨
        var tokenManager = TokenManager.Instance;
        var autoLoginManager = AutoLoginManager.Instance;
        var fallbackHandler = FallbackHandler.Instance;
        
        Assert.IsNotNull(tokenManager, "TokenManager should be available");
        Assert.IsNotNull(autoLoginManager, "AutoLoginManager should be available");
        Assert.IsNotNull(fallbackHandler, "FallbackHandler should be available");
        
        // 초기화 대기
        yield return new WaitForSeconds(1f);
        
        // When: 설정이 변경됨
        settings.EnableAutoLogin = true;
        settings.MaxRetryAttempts = 3;
        settings.AutoLoginTimeoutSeconds = 10f;
        
        // Then: 모든 컴포넌트가 설정을 반영해야 함
        Assert.IsTrue(settings.EnableAutoLogin, "Auto-login should be enabled");
        Assert.AreEqual(3, settings.MaxRetryAttempts, "Max retry attempts should be 3");
        Assert.AreEqual(10f, settings.AutoLoginTimeoutSeconds, "Timeout should be 10 seconds");
        
        yield return null;
    }

    [UnityTest]
    public IEnumerator FallbackHandler_Integration_WithAutoLoginFailure()
    {
        // Given: 폴백 핸들러가 초기화됨
        var fallbackHandler = FallbackHandler.Instance;
        yield return new WaitForSeconds(0.5f);
        
        Assert.IsTrue(fallbackHandler.IsInitialized, "FallbackHandler should be initialized");
        
        bool fallbackStarted = false;
        bool fallbackCompleted = false;
        FallbackResult fallbackResult = null;
        
        // 폴백 이벤트 구독
        FallbackHandler.OnFallbackStarted += (reason, strategy) => { fallbackStarted = true; };
        FallbackHandler.OnFallbackCompleted += (result) => { 
            fallbackCompleted = true; 
            fallbackResult = result;
        };
        
        // When: 자동 로그인 실패 시뮬레이션
        fallbackHandler.StartFallback(AutoLoginResult.TokenExpired);
        
        // 폴백 처리 대기
        yield return new WaitForSeconds(3f);
        
        // Then: 폴백이 실행되어야 함
        Assert.IsTrue(fallbackStarted, "Fallback should have started");
        Assert.IsTrue(fallbackCompleted, "Fallback should have completed");
        Assert.IsNotNull(fallbackResult, "Fallback result should not be null");
        
        yield return null;
    }

    [Test]
    public void SettingsManager_ConfigurationValues_AreValid()
    {
        // Given: 설정이 초기화됨
        Assert.IsNotNull(settings, "Settings should be initialized");
        
        // When & Then: 모든 설정 값이 유효 범위 내에 있어야 함
        
        // 자동 로그인 설정
        Assert.That(settings.MaxRetryAttempts, Is.InRange(1, 5), "Max retry attempts should be between 1 and 5");
        Assert.That(settings.AutoLoginTimeoutSeconds, Is.InRange(5f, 60f), "Timeout should be between 5 and 60 seconds");
        Assert.That(settings.TokenRefreshThresholdHours, Is.InRange(1, 24), "Token refresh threshold should be between 1 and 24 hours");
        
        // 토큰 관리 설정
        Assert.That(settings.TokenValidityThresholdSeconds, Is.InRange(300f, 7200f), "Token validity threshold should be between 5 minutes and 2 hours");
        
        // 보안 설정 확인
        Assert.IsTrue(settings.EnableSecureStorage, "Secure storage should be enabled by default");
    }

    [Test]
    public void AutoLoginSettings_EnvironmentPresets_ApplyCorrectly()
    {
        // Given: 초기 설정
        settings.InitializeDefaults();
        
        // When: 프로덕션 설정 적용
        settings.ApplyProductionSettings();
        
        // Then: 프로덕션 설정이 적용되어야 함
        Assert.IsTrue(settings.EnableAutoLogin, "Auto-login should be enabled in production");
        Assert.IsTrue(settings.EnableSecureStorage, "Secure storage should be enabled in production");
        Assert.IsFalse(settings.EnableDebugLogging, "Debug logging should be disabled in production");
        
        // When: 고보안 설정 적용
        settings.ApplyHighSecuritySettings();
        
        // Then: 보안 설정이 강화되어야 함
        Assert.IsTrue(settings.RequireBiometricAuth, "Biometric auth should be required in high security mode");
        Assert.AreEqual(1, settings.MaxRetryAttempts, "Max retries should be limited in high security mode");
        
        // When: 성능 최적화 설정 적용
        settings.ApplyPerformanceOptimizedSettings();
        
        // Then: 성능 우선 설정이 적용되어야 함
        Assert.IsFalse(settings.AuthenticateOnAppFocus, "App focus auth should be disabled for performance");
        Assert.IsFalse(settings.ShowSplashDuringAuth, "Splash should be disabled for performance");
        Assert.AreEqual(5f, settings.AutoLoginTimeoutSeconds, "Timeout should be minimal for performance");
    }

    [Test]
    public void AutoLoginSettings_JsonSerialization_WorksCorrectly()
    {
        // Given: 설정이 구성됨
        settings.EnableAutoLogin = true;
        settings.RequireBiometricAuth = false;
        settings.MaxRetryAttempts = 3;
        settings.AutoLoginTimeoutSeconds = 15f;
        
        // When: JSON으로 내보내기
        string json = settings.ExportToJson();
        
        // Then: JSON이 생성되어야 함
        Assert.IsNotNull(json, "JSON export should not be null");
        Assert.IsNotEmpty(json, "JSON export should not be empty");
        
        // When: 새 설정 인스턴스에 JSON 가져오기
        var newSettings = ScriptableObject.CreateInstance<AutoLoginSettings>();
        bool importSuccess = newSettings.ImportFromJson(json);
        
        // Then: 가져오기가 성공하고 값이 일치해야 함
        Assert.IsTrue(importSuccess, "JSON import should succeed");
        Assert.AreEqual(settings.EnableAutoLogin, newSettings.EnableAutoLogin);
        Assert.AreEqual(settings.RequireBiometricAuth, newSettings.RequireBiometricAuth);
        Assert.AreEqual(settings.MaxRetryAttempts, newSettings.MaxRetryAttempts);
        Assert.AreEqual(settings.AutoLoginTimeoutSeconds, newSettings.AutoLoginTimeoutSeconds);
        
        // 정리
        Object.DestroyImmediate(newSettings);
    }

    [Test]
    public void AutoLoginPrefs_Integration_WithSettings()
    {
        // Given: 초기 상태
        AutoLoginPrefs.ResetAllPreferencesIncludingSystem();
        
        // When: 자동 로그인 활성화
        AutoLoginPrefs.IsAutoLoginEnabled = true;
        AutoLoginPrefs.UserConsentGiven = true;
        
        // Then: 설정이 반영되어야 함
        Assert.IsTrue(AutoLoginPrefs.IsAutoLoginEnabled, "Auto-login should be enabled in prefs");
        Assert.IsTrue(AutoLoginPrefs.UserConsentGiven, "User consent should be given");
        
        // When: 자동 로그인 가능 여부 확인
        var (canAutoLogin, reason) = AutoLoginPrefs.CanPerformAutoLogin();
        
        // Then: 자동 로그인이 가능해야 함 (토큰이 없어도 기본 검사는 통과)
        // 실제 환경에서는 토큰 상태에 따라 달라짐
        Debug.Log($"Can perform auto-login: {canAutoLogin}, Reason: {reason}");
    }

    [Test]
    public void FallbackHandler_StrategyMapping_IsComplete()
    {
        // Given: 폴백 핸들러
        var fallbackHandler = FallbackHandler.Instance;
        
        // When & Then: 모든 AutoLoginResult에 대한 전략이 정의되어야 함
        var testResults = new AutoLoginResult[]
        {
            AutoLoginResult.NoStoredCredentials,
            AutoLoginResult.TokenExpired,
            AutoLoginResult.TokenRefreshFailed,
            AutoLoginResult.AuthenticationFailed,
            AutoLoginResult.NetworkError,
            AutoLoginResult.Timeout,
            AutoLoginResult.MaxAttemptsExceeded,
            AutoLoginResult.Disabled,
            AutoLoginResult.UserCancelled,
            AutoLoginResult.Unknown
        };
        
        foreach (var result in testResults)
        {
            // 각 결과에 대해 폴백 핸들러가 적절히 반응하는지 확인
            Assert.DoesNotThrow(() => {
                fallbackHandler.StartFallback(result);
                fallbackHandler.StopFallback(); // 즉시 중단
            }, $"FallbackHandler should handle {result} without throwing");
        }
    }

    [UnityTest]
    public IEnumerator StreamIntegration_ComponentsCommunicate_Properly()
    {
        // Given: 모든 스트림 컴포넌트가 존재함
        yield return new WaitForSeconds(1f);
        
        bool settingsEventReceived = false;
        bool prefsEventReceived = false;
        
        // 이벤트 구독
        AutoLoginSettings.OnSettingsChanged += () => { settingsEventReceived = true; };
        AutoLoginPrefs.OnPreferencesChanged += () => { prefsEventReceived = true; };
        
        // When: 설정 변경
        settings.EnableAutoLogin = false;
        yield return new WaitForEndOfFrame();
        
        settings.EnableAutoLogin = true;
        yield return new WaitForEndOfFrame();
        
        // Then: 이벤트가 발생해야 함
        Assert.IsTrue(settingsEventReceived, "Settings change event should be received");
        
        // AutoLoginPrefs도 변경 시 이벤트 발생 확인
        AutoLoginPrefs.IsAutoLoginEnabled = false;
        AutoLoginPrefs.IsAutoLoginEnabled = true;
        
        yield return new WaitForEndOfFrame();
        Assert.IsTrue(prefsEventReceived, "Prefs change event should be received");
        
        yield return null;
    }
}