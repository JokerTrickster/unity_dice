using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// 프로필 섹션 컨트롤러
/// UserDataManager와 통합하여 사용자 프로필 정보를 관리하고 표시하는 섹션입니다.
/// SectionBase를 상속하여 MainPageManager와 연동됩니다.
/// </summary>
public class ProfileSection : SectionBase
{
    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Profile;
    public override string SectionDisplayName => "프로필";
    #endregion

    #region Private Fields
    [Header("UI Components")]
    [SerializeField] private ProfileSectionUI _profileSectionUI;
    
    private UserData _currentUserData;
    private bool _hasProfilePicture = false;
    private Coroutine _dataUpdateCoroutine;
    
    // Profile-specific settings
    private const float PROFILE_REFRESH_INTERVAL = 60f; // 1분마다 프로필 데이터 새로고침
    private const int MAX_ACHIEVEMENT_DISPLAY = 5; // 최대 표시할 업적 수
    #endregion

    #region SectionBase Implementation
    protected override void OnInitialize()
    {
        Debug.Log("[ProfileSection] Initializing profile section...");
        
        // UI 컴포넌트 검증 및 설정
        ValidateUIComponents();
        SetupUIComponent();
        
        // 초기 사용자 데이터 로드
        LoadInitialUserData();
        
        Debug.Log("[ProfileSection] Profile section initialized successfully");
    }

    protected override void OnActivate()
    {
        Debug.Log("[ProfileSection] Activating profile section...");
        
        if (_profileSectionUI != null)
        {
            _profileSectionUI.gameObject.SetActive(true);
            _profileSectionUI.SetActive(true);
        }
        
        // 현재 사용자 데이터 새로고침
        RefreshCurrentUserData();
        
        // 주기적 데이터 업데이트 시작
        StartPeriodicDataUpdate();
        
        Debug.Log("[ProfileSection] Profile section activated");
    }

    protected override void OnDeactivate()
    {
        Debug.Log("[ProfileSection] Deactivating profile section...");
        
        if (_profileSectionUI != null)
        {
            _profileSectionUI.SetActive(false);
            _profileSectionUI.gameObject.SetActive(false);
        }
        
        // 주기적 업데이트 중지
        StopPeriodicDataUpdate();
        
        Debug.Log("[ProfileSection] Profile section deactivated");
    }

    protected override void OnCleanup()
    {
        Debug.Log("[ProfileSection] Cleaning up profile section...");
        
        // UI 이벤트 정리
        if (_profileSectionUI != null)
        {
            _profileSectionUI.Cleanup();
        }
        
        // 코루틴 정리
        StopPeriodicDataUpdate();
        
        Debug.Log("[ProfileSection] Profile section cleaned up");
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null || _profileSectionUI == null) return;
        
        _currentUserData = userData;
        
        try
        {
            // UI 컴포넌트에 데이터 전달
            _profileSectionUI.UpdateUserProfile(userData);
            
            // 프로필별 추가 정보 업데이트
            UpdateProfileStats(userData);
            UpdateAchievementDisplay(userData);
            UpdateOnlineStatus();
            
            Debug.Log($"[ProfileSection] UI updated for user: {userData.DisplayName}");
        }
        catch (Exception e)
        {
            ReportError($"Failed to update profile UI: {e.Message}");
        }
    }

    protected override void ValidateComponents()
    {
        if (_profileSectionUI == null)
        {
            _profileSectionUI = GetComponentInChildren<ProfileSectionUI>();
            if (_profileSectionUI == null)
            {
                ReportError("ProfileSectionUI component not found! Please ensure ProfileSectionUI prefab is properly assigned.");
                return;
            }
        }
        
        // UI 컴포넌트 유효성 검사
        if (!_profileSectionUI.ValidateUIComponents())
        {
            ReportError("ProfileSectionUI contains missing UI components!");
        }
        
        Debug.Log("[ProfileSection] Component validation completed");
    }
    #endregion

    #region UserDataManager Integration
    /// <summary>
    /// 초기 사용자 데이터 로드
    /// </summary>
    private void LoadInitialUserData()
    {
        if (_userDataManager?.CurrentUser != null)
        {
            _currentUserData = _userDataManager.CurrentUser;
            Debug.Log($"[ProfileSection] Initial user data loaded: {_currentUserData.DisplayName}");
        }
        else
        {
            Debug.LogWarning("[ProfileSection] No current user data available");
        }
    }
    
    /// <summary>
    /// 현재 사용자 데이터 새로고침
    /// </summary>
    private void RefreshCurrentUserData()
    {
        if (_userDataManager == null) return;
        
        try
        {
            // 서버에서 최신 데이터 요청
            if (!_isOfflineMode)
            {
                _userDataManager.SyncWithServer();
            }
            
            var currentUser = _userDataManager.CurrentUser;
            if (currentUser != null)
            {
                UpdateUI(currentUser);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSection] Failed to refresh user data: {e.Message}");
            
            // 오프라인 모드로 전환
            if (!_isOfflineMode)
            {
                OnModeChanged(true);
            }
        }
    }
    
    /// <summary>
    /// 사용자 프로필 통계 업데이트
    /// </summary>
    private void UpdateProfileStats(UserData userData)
    {
        if (_profileSectionUI == null) return;
        
        try
        {
            var profileStats = new ProfileStats
            {
                TotalGamesPlayed = userData.TotalGamesPlayed,
                GamesWon = userData.GamesWon,
                GamesLost = userData.GamesLost,
                WinRate = userData.WinRate,
                Level = userData.Level,
                Experience = userData.Experience,
                NextLevelExperience = CalculateNextLevelExperience(userData.Level),
                Ranking = GetUserRanking(userData.UserId), // 서버에서 가져오는 랭킹 정보
                PlayTime = CalculateTotalPlayTime(userData), // 총 플레이 시간 계산
                LastPlayedDate = userData.UpdatedAt
            };
            
            _profileSectionUI.UpdateProfileStats(profileStats);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSection] Failed to update profile stats: {e.Message}");
        }
    }
    
    /// <summary>
    /// 업적 표시 업데이트
    /// </summary>
    private void UpdateAchievementDisplay(UserData userData)
    {
        if (_profileSectionUI == null) return;
        
        try
        {
            // 최근 달성한 업적들 가져오기 (향후 AchievementManager와 연동)
            var recentAchievements = GetRecentAchievements(userData.UserId, MAX_ACHIEVEMENT_DISPLAY);
            _profileSectionUI.UpdateAchievements(recentAchievements);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSection] Failed to update achievements: {e.Message}");
        }
    }
    
    /// <summary>
    /// 온라인 상태 업데이트
    /// </summary>
    private void UpdateOnlineStatus()
    {
        if (_profileSectionUI == null) return;
        
        var onlineStatus = new OnlineStatus
        {
            IsOnline = !_isOfflineMode && _authenticationManager != null && _authenticationManager.IsAuthenticated,
            LastSeen = _isOfflineMode ? _userDataManager?.CurrentUser?.UpdatedAt ?? DateTime.Now : DateTime.Now,
            ConnectionStatus = GetConnectionQuality()
        };
        
        _profileSectionUI.UpdateOnlineStatus(onlineStatus);
    }
    #endregion

    #region UI Component Management
    /// <summary>
    /// UI 컴포넌트 설정
    /// </summary>
    private void SetupUIComponent()
    {
        if (_profileSectionUI == null) return;
        
        // UI 이벤트 구독
        _profileSectionUI.OnEditProfileRequested += OnEditProfileRequested;
        _profileSectionUI.OnAchievementsRequested += OnAchievementsRequested;
        _profileSectionUI.OnStatisticsRequested += OnStatisticsRequested;
        _profileSectionUI.OnProfileDetailRequested += OnProfileDetailRequested;
        
        // 초기 UI 설정
        _profileSectionUI.Initialize();
        
        Debug.Log("[ProfileSection] UI component setup completed");
    }
    
    /// <summary>
    /// UI 컴포넌트 유효성 검사
    /// </summary>
    private void ValidateUIComponents()
    {
        if (_profileSectionUI == null)
        {
            _profileSectionUI = GetComponentInChildren<ProfileSectionUI>();
        }
        
        if (_profileSectionUI == null)
        {
            Debug.LogError("[ProfileSection] ProfileSectionUI component not found in children!");
            return;
        }
        
        Debug.Log("[ProfileSection] UI components validated successfully");
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 프로필 편집 요청 처리
    /// </summary>
    private void OnEditProfileRequested()
    {
        Debug.Log("[ProfileSection] Profile edit requested");
        
        if (_isOfflineMode)
        {
            Debug.LogWarning("[ProfileSection] Cannot edit profile in offline mode");
            return;
        }
        
        // 프로필 편집 화면으로 전환
        RequestScreenTransition(ScreenType.NicknameSetup); // 임시로 닉네임 설정 화면 사용
    }
    
    /// <summary>
    /// 업적 화면 요청 처리
    /// </summary>
    private void OnAchievementsRequested()
    {
        Debug.Log("[ProfileSection] Achievements screen requested");
        
        // Settings 섹션에 업적 표시 요청
        SendMessageToSection(MainPageSectionType.Settings, new AchievementViewRequest
        {
            UserId = _currentUserData?.UserId ?? "",
            RequestType = "show_achievements"
        });
    }
    
    /// <summary>
    /// 통계 화면 요청 처리
    /// </summary>
    private void OnStatisticsRequested()
    {
        Debug.Log("[ProfileSection] Statistics screen requested");
        
        // Settings 섹션에 통계 표시 요청
        SendMessageToSection(MainPageSectionType.Settings, new StatisticsViewRequest
        {
            UserId = _currentUserData?.UserId ?? "",
            RequestType = "show_statistics"
        });
    }
    
    /// <summary>
    /// 프로필 상세 화면 요청 처리
    /// </summary>
    private void OnProfileDetailRequested()
    {
        Debug.Log("[ProfileSection] Profile detail screen requested");
        
        // Settings 섹션에 프로필 상세 표시 요청
        SendMessageToSection(MainPageSectionType.Settings, new ProfileDetailViewRequest
        {
            UserData = _currentUserData,
            RequestType = "show_profile_detail"
        });
    }
    #endregion

    #region Periodic Data Updates
    /// <summary>
    /// 주기적 데이터 업데이트 시작
    /// </summary>
    private void StartPeriodicDataUpdate()
    {
        StopPeriodicDataUpdate();
        _dataUpdateCoroutine = StartCoroutine(PeriodicDataUpdateCoroutine());
    }
    
    /// <summary>
    /// 주기적 데이터 업데이트 중지
    /// </summary>
    private void StopPeriodicDataUpdate()
    {
        if (_dataUpdateCoroutine != null)
        {
            StopCoroutine(_dataUpdateCoroutine);
            _dataUpdateCoroutine = null;
        }
    }
    
    /// <summary>
    /// 주기적 데이터 업데이트 코루틴
    /// </summary>
    private IEnumerator PeriodicDataUpdateCoroutine()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(PROFILE_REFRESH_INTERVAL);
            
            if (!_isOfflineMode)
            {
                RefreshCurrentUserData();
            }
        }
    }
    #endregion

    #region Virtual Method Overrides
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        Debug.Log($"[ProfileSection] Offline mode changed to: {isOfflineMode}");
        
        if (_profileSectionUI != null)
        {
            _profileSectionUI.SetOfflineMode(isOfflineMode);
        }
        
        // 오프라인 모드에서는 주기적 업데이트 중지
        if (isOfflineMode)
        {
            StopPeriodicDataUpdate();
        }
        else
        {
            StartPeriodicDataUpdate();
            RefreshCurrentUserData(); // 온라인 복구 시 데이터 새로고침
        }
    }

    protected override void OnForceRefresh()
    {
        Debug.Log("[ProfileSection] Force refresh requested");
        
        RefreshCurrentUserData();
        
        if (_profileSectionUI != null)
        {
            _profileSectionUI.ForceRefresh();
        }
    }

    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[ProfileSection] Received message from {fromSection}: {data?.GetType().Name}");
        
        if (data is UserData userData)
        {
            UpdateUI(userData);
        }
        else if (data is string command)
        {
            HandleStringCommand(command);
        }
    }
    
    /// <summary>
    /// 문자열 명령 처리
    /// </summary>
    private void HandleStringCommand(string command)
    {
        switch (command.ToLower())
        {
            case "refresh":
                ForceRefresh();
                break;
            case "logout":
                RequestLogout();
                break;
            default:
                Debug.LogWarning($"[ProfileSection] Unknown command: {command}");
                break;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 다음 레벨까지 필요한 경험치 계산
    /// </summary>
    private int CalculateNextLevelExperience(int currentLevel)
    {
        // 레벨당 1000 경험치 (간단한 공식)
        return (currentLevel + 1) * 1000;
    }
    
    /// <summary>
    /// 사용자 랭킹 가져오기 (향후 서버 연동)
    /// </summary>
    private int GetUserRanking(string userId)
    {
        // TODO: 실제 랭킹 시스템과 연동
        return -1; // -1은 랭킹 없음을 의미
    }
    
    /// <summary>
    /// 총 플레이 시간 계산
    /// </summary>
    private TimeSpan CalculateTotalPlayTime(UserData userData)
    {
        // TODO: 실제 플레이 시간 추적 시스템과 연동
        // 현재는 게임 수 기반으로 추정
        var estimatedMinutesPerGame = 10; // 게임당 평균 10분
        return TimeSpan.FromMinutes(userData.TotalGamesPlayed * estimatedMinutesPerGame);
    }
    
    /// <summary>
    /// 연결 품질 확인
    /// </summary>
    private ConnectionQuality GetConnectionQuality()
    {
        if (_isOfflineMode) return ConnectionQuality.Offline;
        
        // TODO: 실제 네트워크 품질 측정 구현
        return ConnectionQuality.Good;
    }
    
    /// <summary>
    /// 최근 업적 가져오기 (향후 구현)
    /// </summary>
    private Achievement[] GetRecentAchievements(string userId, int maxCount)
    {
        // TODO: AchievementManager와 연동하여 실제 업적 데이터 가져오기
        return new Achievement[0]; // 임시로 빈 배열 반환
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 프로필 사진 업로드
    /// </summary>
    public void UploadProfilePicture(Texture2D profileTexture)
    {
        if (_isOfflineMode)
        {
            Debug.LogWarning("[ProfileSection] Cannot upload profile picture in offline mode");
            return;
        }
        
        if (profileTexture == null)
        {
            Debug.LogError("[ProfileSection] Profile texture is null");
            return;
        }
        
        // TODO: 실제 프로필 사진 업로드 구현
        Debug.Log("[ProfileSection] Profile picture upload requested");
        _hasProfilePicture = true;
    }
    
    /// <summary>
    /// 프로필 정보 업데이트
    /// </summary>
    public void UpdateProfile(string displayName, string title = null)
    {
        if (_isOfflineMode)
        {
            Debug.LogWarning("[ProfileSection] Cannot update profile in offline mode");
            return;
        }
        
        if (_currentUserData == null)
        {
            Debug.LogError("[ProfileSection] No current user data to update");
            return;
        }
        
        try
        {
            // 로컬 데이터 업데이트
            _currentUserData.DisplayName = displayName ?? _currentUserData.DisplayName;
            _currentUserData.UpdatedAt = DateTime.Now;
            
            // UI 업데이트
            UpdateUI(_currentUserData);
            
            // 서버 동기화
            _userDataManager?.SyncWithServer();
            
            Debug.Log($"[ProfileSection] Profile updated: {displayName}");
        }
        catch (Exception e)
        {
            ReportError($"Failed to update profile: {e.Message}");
        }
    }
    
    /// <summary>
    /// 프로필 섹션 상태 정보
    /// </summary>
    public ProfileSectionStatus GetProfileSectionStatus()
    {
        return new ProfileSectionStatus
        {
            BaseStatus = GetSectionStatus(),
            CurrentUserId = _currentUserData?.UserId ?? "",
            HasProfilePicture = _hasProfilePicture,
            IsDataSynced = !_isOfflineMode && _userDataManager?.IsOfflineMode == false,
            LastDataUpdate = _currentUserData?.UpdatedAt ?? DateTime.MinValue
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 프로필 통계 정보
/// </summary>
[Serializable]
public class ProfileStats
{
    public int TotalGamesPlayed;
    public int GamesWon;
    public int GamesLost;
    public float WinRate;
    public int Level;
    public int Experience;
    public int NextLevelExperience;
    public int Ranking;
    public TimeSpan PlayTime;
    public DateTime LastPlayedDate;
}

/// <summary>
/// 온라인 상태 정보
/// </summary>
[Serializable]
public class OnlineStatus
{
    public bool IsOnline;
    public DateTime LastSeen;
    public ConnectionQuality ConnectionStatus;
}

/// <summary>
/// 연결 품질
/// </summary>
public enum ConnectionQuality
{
    Offline,
    Poor,
    Fair,
    Good,
    Excellent
}

/// <summary>
/// 업적 정보
/// </summary>
[Serializable]
public class Achievement
{
    public string Id;
    public string Name;
    public string Description;
    public string IconUrl;
    public DateTime UnlockedAt;
    public bool IsUnlocked;
}

/// <summary>
/// 프로필 섹션 상태
/// </summary>
[Serializable]
public class ProfileSectionStatus
{
    public SectionStatus BaseStatus;
    public string CurrentUserId;
    public bool HasProfilePicture;
    public bool IsDataSynced;
    public DateTime LastDataUpdate;
}

/// <summary>
/// 업적 뷰 요청
/// </summary>
[Serializable]
public class AchievementViewRequest
{
    public string UserId;
    public string RequestType;
}

/// <summary>
/// 통계 뷰 요청
/// </summary>
[Serializable]
public class StatisticsViewRequest
{
    public string UserId;
    public string RequestType;
}

/// <summary>
/// 프로필 상세 뷰 요청
/// </summary>
[Serializable]
public class ProfileDetailViewRequest
{
    public UserData UserData;
    public string RequestType;
}
#endregion