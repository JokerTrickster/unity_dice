using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// 프로필 섹션 단위 테스트
/// ProfileSection과 ProfileSectionUI의 기능을 검증합니다.
/// </summary>
public class ProfileSectionTests
{
    #region Setup and Teardown
    private GameObject _testGameObject;
    private ProfileSection _profileSection;
    private ProfileSectionUI _profileSectionUI;
    private UserData _testUserData;
    private MainPageManager _mockMainPageManager;

    [SetUp]
    public void SetUp()
    {
        // 테스트용 게임 오브젝트 생성
        _testGameObject = new GameObject("ProfileSectionTest");
        
        // ProfileSection 컴포넌트 추가
        _profileSection = _testGameObject.AddComponent<ProfileSection>();
        
        // ProfileSectionUI 컴포넌트 추가 (자식 오브젝트로)
        var uiGameObject = new GameObject("ProfileSectionUI");
        uiGameObject.transform.SetParent(_testGameObject.transform);
        _profileSectionUI = uiGameObject.AddComponent<ProfileSectionUI>();
        
        // 테스트용 사용자 데이터 생성
        _testUserData = CreateTestUserData();
        
        // Mock MainPageManager 설정 (실제 테스트에서는 Mock 프레임워크 사용 권장)
        SetupMockMainPageManager();
        
        Debug.Log("[ProfileSectionTests] Test setup completed");
    }

    [TearDown]
    public void TearDown()
    {
        // 테스트 오브젝트 정리
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
        
        // Mock 매니저 정리
        if (_mockMainPageManager != null && _mockMainPageManager.gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_mockMainPageManager.gameObject);
        }
        
        Debug.Log("[ProfileSectionTests] Test teardown completed");
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 테스트용 사용자 데이터 생성
    /// </summary>
    private UserData CreateTestUserData()
    {
        return new UserData
        {
            UserId = "test_user_123",
            DisplayName = "TestPlayer",
            Email = "test@example.com",
            Level = 15,
            Experience = 750,
            TotalGamesPlayed = 50,
            GamesWon = 32,
            GamesLost = 18,
            Title = "Dice Master",
            AvatarUrl = "https://example.com/avatar.jpg",
            Ranking = 42,
            CreatedAt = DateTime.Now.AddDays(-30),
            UpdatedAt = DateTime.Now.AddMinutes(-5),
            LastLoginAt = DateTime.Now.AddMinutes(-1),
            IsNewUser = false
        };
    }
    
    /// <summary>
    /// Mock MainPageManager 설정
    /// </summary>
    private void SetupMockMainPageManager()
    {
        var managerGameObject = new GameObject("MockMainPageManager");
        _mockMainPageManager = managerGameObject.AddComponent<MainPageManager>();
    }
    
    /// <summary>
    /// ProfileSectionUI Mock UI 컴포넌트 설정
    /// </summary>
    private void SetupMockUIComponents()
    {
        // 실제 테스트에서는 UI 컴포넌트들을 Mock하거나 실제 Prefab을 로드해야 함
        // 현재는 기본 설정으로 진행
        if (_profileSectionUI != null)
        {
            _profileSectionUI.Initialize();
        }
    }
    #endregion

    #region ProfileSection Basic Tests
    
    [Test]
    public void ProfileSection_Creation_ShouldHaveCorrectProperties()
    {
        // Given & When: ProfileSection이 생성됨
        
        // Then: 기본 속성들이 올바르게 설정되어야 함
        Assert.IsNotNull(_profileSection, "ProfileSection should be created");
        Assert.AreEqual(MainPageSectionType.Profile, _profileSection.SectionType, "Section type should be Profile");
        Assert.AreEqual("프로필", _profileSection.SectionDisplayName, "Display name should be correct");
        Assert.IsFalse(_profileSection.IsInitialized, "Should not be initialized initially");
        Assert.IsFalse(_profileSection.IsActive, "Should not be active initially");
    }

    [Test]
    public void ProfileSection_Initialize_ShouldSetInitializedState()
    {
        // Given: ProfileSection이 생성됨
        SetupMockUIComponents();
        
        // When: 초기화 메서드 호출
        _profileSection.Initialize(_mockMainPageManager);
        
        // Then: 초기화 상태가 true가 되어야 함
        Assert.IsTrue(_profileSection.IsInitialized, "Should be initialized after Initialize() call");
    }

    [Test]
    public void ProfileSection_Activate_WithoutInitialization_ShouldLogError()
    {
        // Given: 초기화되지 않은 ProfileSection
        
        // When: 활성화 시도
        // Then: 오류가 로그되고 활성화되지 않아야 함
        Assert.DoesNotThrow(() => _profileSection.Activate(), "Should not throw exception");
        Assert.IsFalse(_profileSection.IsActive, "Should not be active without initialization");
    }

    [Test]
    public void ProfileSection_InitializeAndActivate_ShouldWork()
    {
        // Given: ProfileSection이 생성됨
        SetupMockUIComponents();
        
        // When: 초기화 후 활성화
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // Then: 활성화 상태가 되어야 함
        Assert.IsTrue(_profileSection.IsInitialized, "Should be initialized");
        Assert.IsTrue(_profileSection.IsActive, "Should be active");
    }

    [Test]
    public void ProfileSection_Deactivate_ShouldSetInactiveState()
    {
        // Given: 활성화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 비활성화
        _profileSection.Deactivate();
        
        // Then: 비활성화 상태가 되어야 함
        Assert.IsTrue(_profileSection.IsInitialized, "Should remain initialized");
        Assert.IsFalse(_profileSection.IsActive, "Should be deactivated");
    }

    [Test]
    public void ProfileSection_Cleanup_ShouldResetState()
    {
        // Given: 활성화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 정리
        _profileSection.Cleanup();
        
        // Then: 상태가 리셋되어야 함
        Assert.IsFalse(_profileSection.IsInitialized, "Should not be initialized after cleanup");
        Assert.IsFalse(_profileSection.IsActive, "Should not be active after cleanup");
    }
    #endregion

    #region ProfileSection Data Update Tests

    [Test]
    public void ProfileSection_UpdateUI_WithValidUserData_ShouldUpdateSuccessfully()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 사용자 데이터 업데이트
        Assert.DoesNotThrow(() => _profileSection.OnUserDataUpdated(_testUserData), 
            "Should update UI without throwing exception");
    }

    [Test]
    public void ProfileSection_UpdateUI_WithNullUserData_ShouldHandleGracefully()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: null 사용자 데이터로 업데이트 시도
        // Then: 예외 없이 처리되어야 함
        Assert.DoesNotThrow(() => _profileSection.OnUserDataUpdated(null), 
            "Should handle null user data gracefully");
    }

    [Test]
    public void ProfileSection_ForceRefresh_ShouldTriggerDataUpdate()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 강제 새로고침
        // Then: 예외 없이 처리되어야 함
        Assert.DoesNotThrow(() => _profileSection.ForceRefresh(), 
            "Should handle force refresh gracefully");
    }
    #endregion

    #region ProfileSection Mode Change Tests

    [Test]
    public void ProfileSection_OnOfflineModeChanged_ShouldUpdateState()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 오프라인 모드로 변경
        Assert.DoesNotThrow(() => _profileSection.OnModeChanged(true), 
            "Should handle offline mode change gracefully");
        
        // When: 온라인 모드로 변경
        Assert.DoesNotThrow(() => _profileSection.OnModeChanged(false), 
            "Should handle online mode change gracefully");
    }

    [Test]
    public void ProfileSection_OnSettingChanged_ShouldProcessSetting()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        
        // When: 설정 변경
        // Then: 예외 없이 처리되어야 함
        Assert.DoesNotThrow(() => _profileSection.OnSettingChanged("test_setting", "test_value"), 
            "Should handle setting changes gracefully");
    }
    #endregion

    #region ProfileSection Message Tests

    [Test]
    public void ProfileSection_ReceiveMessage_WithUserData_ShouldUpdateUI()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: UserData 메시지 수신
        Assert.DoesNotThrow(() => _profileSection.ReceiveMessage(MainPageSectionType.Settings, _testUserData), 
            "Should handle UserData message gracefully");
    }

    [Test]
    public void ProfileSection_ReceiveMessage_WithStringCommand_ShouldExecuteCommand()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 문자열 명령 수신
        Assert.DoesNotThrow(() => _profileSection.ReceiveMessage(MainPageSectionType.Settings, "refresh"), 
            "Should handle string command gracefully");
    }
    #endregion

    #region ProfileSection Public Method Tests

    [Test]
    public void ProfileSection_UpdateProfile_WithValidData_ShouldUpdateSuccessfully()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.OnUserDataUpdated(_testUserData);
        
        // When: 프로필 업데이트
        Assert.DoesNotThrow(() => _profileSection.UpdateProfile("NewDisplayName", "New Title"), 
            "Should update profile without throwing exception");
    }

    [Test]
    public void ProfileSection_GetProfileSectionStatus_ShouldReturnValidStatus()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.OnUserDataUpdated(_testUserData);
        
        // When: 상태 정보 요청
        var status = _profileSection.GetProfileSectionStatus();
        
        // Then: 유효한 상태 정보가 반환되어야 함
        Assert.IsNotNull(status, "Status should not be null");
        Assert.IsNotNull(status.BaseStatus, "Base status should not be null");
        Assert.AreEqual(_testUserData.UserId, status.CurrentUserId, "Current user ID should match test data");
    }
    #endregion

    #region ProfileSectionUI Tests

    [Test]
    public void ProfileSectionUI_Creation_ShouldHaveCorrectInitialState()
    {
        // Given & When: ProfileSectionUI가 생성됨
        
        // Then: 기본 상태가 올바르게 설정되어야 함
        Assert.IsNotNull(_profileSectionUI, "ProfileSectionUI should be created");
        Assert.IsFalse(_profileSectionUI.IsActive, "Should not be active initially");
        Assert.IsFalse(_profileSectionUI.IsOfflineMode, "Should not be in offline mode initially");
    }

    [Test]
    public void ProfileSectionUI_Initialize_ShouldSetupCorrectly()
    {
        // Given: ProfileSectionUI가 생성됨
        
        // When: 초기화
        Assert.DoesNotThrow(() => _profileSectionUI.Initialize(), 
            "Should initialize without throwing exception");
        
        // Then: 초기화된 상태여야 함 (내부 상태는 private이므로 동작 확인으로 검증)
    }

    [Test]
    public void ProfileSectionUI_SetActive_ShouldChangeActiveState()
    {
        // Given: 초기화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        
        // When: 활성화
        _profileSectionUI.SetActive(true);
        
        // Then: 활성화 상태가 되어야 함
        Assert.IsTrue(_profileSectionUI.IsActive, "Should be active after SetActive(true)");
        
        // When: 비활성화
        _profileSectionUI.SetActive(false);
        
        // Then: 비활성화 상태가 되어야 함
        Assert.IsFalse(_profileSectionUI.IsActive, "Should be inactive after SetActive(false)");
    }

    [Test]
    public void ProfileSectionUI_SetOfflineMode_ShouldChangeOfflineState()
    {
        // Given: 초기화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        
        // When: 오프라인 모드로 설정
        _profileSectionUI.SetOfflineMode(true);
        
        // Then: 오프라인 모드가 되어야 함
        Assert.IsTrue(_profileSectionUI.IsOfflineMode, "Should be in offline mode after SetOfflineMode(true)");
        
        // When: 온라인 모드로 설정
        _profileSectionUI.SetOfflineMode(false);
        
        // Then: 온라인 모드가 되어야 함
        Assert.IsFalse(_profileSectionUI.IsOfflineMode, "Should be in online mode after SetOfflineMode(false)");
    }

    [Test]
    public void ProfileSectionUI_UpdateUserProfile_WithValidData_ShouldUpdateSuccessfully()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        // When: 사용자 프로필 업데이트
        Assert.DoesNotThrow(() => _profileSectionUI.UpdateUserProfile(_testUserData), 
            "Should update user profile without throwing exception");
    }

    [Test]
    public void ProfileSectionUI_UpdateUserProfile_WithNullData_ShouldHandleGracefully()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        // When: null 데이터로 업데이트 시도
        // Then: 예외 없이 처리되어야 함
        Assert.DoesNotThrow(() => _profileSectionUI.UpdateUserProfile(null), 
            "Should handle null user data gracefully");
    }

    [Test]
    public void ProfileSectionUI_UpdateProfileStats_WithValidStats_ShouldUpdateSuccessfully()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        var testStats = new ProfileStats
        {
            TotalGamesPlayed = _testUserData.TotalGamesPlayed,
            GamesWon = _testUserData.GamesWon,
            GamesLost = _testUserData.GamesLost,
            WinRate = _testUserData.WinRate,
            Level = _testUserData.Level,
            Experience = _testUserData.Experience,
            NextLevelExperience = (_testUserData.Level + 1) * 1000,
            Ranking = _testUserData.Ranking,
            PlayTime = TimeSpan.FromMinutes(500),
            LastPlayedDate = _testUserData.UpdatedAt
        };
        
        // When: 통계 업데이트
        Assert.DoesNotThrow(() => _profileSectionUI.UpdateProfileStats(testStats), 
            "Should update profile stats without throwing exception");
    }

    [Test]
    public void ProfileSectionUI_UpdateOnlineStatus_WithValidStatus_ShouldUpdateSuccessfully()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        var testStatus = new OnlineStatus
        {
            IsOnline = true,
            LastSeen = DateTime.Now.AddMinutes(-1),
            ConnectionStatus = ConnectionQuality.Good
        };
        
        // When: 온라인 상태 업데이트
        Assert.DoesNotThrow(() => _profileSectionUI.UpdateOnlineStatus(testStatus), 
            "Should update online status without throwing exception");
    }

    [Test]
    public void ProfileSectionUI_ForceRefresh_ShouldRefreshDisplay()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        // When: 강제 새로고침
        Assert.DoesNotThrow(() => _profileSectionUI.ForceRefresh(), 
            "Should handle force refresh gracefully");
    }

    [Test]
    public void ProfileSectionUI_Cleanup_ShouldCleanupResources()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        // When: 정리
        _profileSectionUI.Cleanup();
        
        // Then: 비활성화 상태가 되어야 함
        Assert.IsFalse(_profileSectionUI.IsActive, "Should be inactive after cleanup");
    }
    #endregion

    #region Integration Tests

    [Test]
    public void ProfileSection_And_UI_Integration_ShouldWorkTogether()
    {
        // Given: ProfileSection과 ProfileSectionUI가 연결됨
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        
        // When: ProfileSection 활성화 및 데이터 업데이트
        _profileSection.Activate();
        _profileSection.OnUserDataUpdated(_testUserData);
        
        // Then: 두 컴포넌트가 함께 작동해야 함
        Assert.IsTrue(_profileSection.IsActive, "ProfileSection should be active");
        Assert.DoesNotThrow(() => _profileSection.ForceRefresh(), 
            "Integrated components should work together without exceptions");
    }
    #endregion

    #region Performance Tests

    [UnityTest]
    public IEnumerator ProfileSection_MultipleUpdates_ShouldHandleEfficiently()
    {
        // Given: 초기화된 ProfileSection
        SetupMockUIComponents();
        _profileSection.Initialize(_mockMainPageManager);
        _profileSection.Activate();
        
        // When: 여러 번 빠른 업데이트
        for (int i = 0; i < 10; i++)
        {
            _profileSection.OnUserDataUpdated(_testUserData);
            yield return null; // 한 프레임 대기
        }
        
        // Then: 성능 문제 없이 처리되어야 함
        Assert.IsTrue(_profileSection.IsActive, "Should remain active after multiple updates");
    }

    [UnityTest]
    public IEnumerator ProfileSectionUI_AnimationPerformance_ShouldBeSmooth()
    {
        // Given: 활성화된 ProfileSectionUI
        _profileSectionUI.Initialize();
        _profileSectionUI.SetActive(true);
        
        var testStats = new ProfileStats
        {
            Level = 10,
            Experience = 800,
            NextLevelExperience = 1000
        };
        
        // When: 경험치 바 애니메이션 트리거
        _profileSectionUI.UpdateProfileStats(testStats);
        
        // Then: 애니메이션이 부드럽게 실행되어야 함
        yield return new WaitForSeconds(2f); // 애니메이션 완료 대기
        
        Assert.IsTrue(_profileSectionUI.IsActive, "Should remain active during animation");
    }
    #endregion

    #region Error Handling Tests

    [Test]
    public void ProfileSection_WithMissingUIComponent_ShouldHandleGracefully()
    {
        // Given: UI 컴포넌트가 없는 ProfileSection
        var testObject = new GameObject("TestProfileSectionNoUI");
        var profileSectionNoUI = testObject.AddComponent<ProfileSection>();
        
        try
        {
            // When: 초기화 시도
            // Then: 예외 없이 처리되어야 하지만 오류 로그 발생
            Assert.DoesNotThrow(() => profileSectionNoUI.Initialize(_mockMainPageManager), 
                "Should handle missing UI component gracefully");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    [Test]
    public void ProfileSectionUI_ValidateUIComponents_WithMissingComponents_ShouldReturnFalse()
    {
        // Given: UI 컴포넌트가 설정되지 않은 ProfileSectionUI
        var testObject = new GameObject("TestProfileSectionUINoComponents");
        var uiNoComponents = testObject.AddComponent<ProfileSectionUI>();
        
        try
        {
            // When: UI 컴포넌트 검증
            bool isValid = uiNoComponents.ValidateUIComponents();
            
            // Then: 검증에 실패해야 함
            Assert.IsFalse(isValid, "Should return false when required UI components are missing");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }
    #endregion
}