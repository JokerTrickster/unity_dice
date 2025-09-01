using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로필 섹션 UI 컴포넌트
/// 사용자 프로필 정보를 표시하는 순수 UI 컴포넌트입니다.
/// ProfileSection 컨트롤러에 의해 관리됩니다.
/// </summary>
public class ProfileSectionUI : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 프로필 편집 요청 이벤트
    /// </summary>
    public event Action OnEditProfileRequested;
    
    /// <summary>
    /// 업적 화면 요청 이벤트
    /// </summary>
    public event Action OnAchievementsRequested;
    
    /// <summary>
    /// 통계 화면 요청 이벤트
    /// </summary>
    public event Action OnStatisticsRequested;
    
    /// <summary>
    /// 프로필 상세 화면 요청 이벤트
    /// </summary>
    public event Action OnProfileDetailRequested;
    #endregion

    #region UI References
    [Header("Profile Display")]
    [SerializeField] private Image profileAvatarImage;
    [SerializeField] private Text userNameText;
    [SerializeField] private Text userLevelText;
    [SerializeField] private Text userTitleText;
    [SerializeField] private Button profileDetailButton;
    
    [Header("Stats Display")]
    [SerializeField] private Text gamesPlayedText;
    [SerializeField] private Text winRateText;
    [SerializeField] private Text rankingText;
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private Text experienceText;
    [SerializeField] private Text totalPlayTimeText;
    [SerializeField] private Text lastPlayedText;
    
    [Header("Action Buttons")]
    [SerializeField] private Button editProfileButton;
    [SerializeField] private Button achievementsButton;
    [SerializeField] private Button statisticsButton;
    
    [Header("Visual Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject onlineIndicator;
    [SerializeField] private GameObject offlineOverlay;
    [SerializeField] private GameObject[] achievementSlots = new GameObject[5]; // 최대 5개 업적 표시
    
    [Header("Animation Settings")]
    [SerializeField] private float experienceAnimationDuration = 1.0f;
    [SerializeField] private AnimationCurve experienceAnimationCurve = AnimationCurve.EaseOut(0f, 0f, 1f, 1f);
    #endregion

    #region Private Fields
    private UserData _currentUserData;
    private ProfileStats _currentStats;
    private OnlineStatus _currentOnlineStatus;
    private bool _isActive = false;
    private bool _isOfflineMode = false;
    private bool _profileImageLoaded = false;
    
    // Animation
    private Coroutine _experienceAnimationCoroutine;
    private Coroutine _onlineIndicatorBlinkCoroutine;
    
    // UI State
    private float _currentExperienceValue = 0f;
    private float _targetExperienceValue = 0f;
    
    // Constants
    private const float ONLINE_INDICATOR_BLINK_INTERVAL = 2f;
    private const float UI_UPDATE_THROTTLE = 0.1f;
    private DateTime _lastUIUpdate = DateTime.MinValue;
    #endregion

    #region Public Properties
    /// <summary>
    /// UI 활성화 상태
    /// </summary>
    public bool IsActive => _isActive;
    
    /// <summary>
    /// 오프라인 모드 상태
    /// </summary>
    public bool IsOfflineMode => _isOfflineMode;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateUIComponents();
        SetupUIEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeUIEvents();
        StopAllAnimations();
    }

    private void OnEnable()
    {
        if (_isActive)
        {
            RefreshDisplay();
        }
    }

    private void OnDisable()
    {
        StopAllAnimations();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// UI 컴포넌트 초기화
    /// </summary>
    public void Initialize()
    {
        Debug.Log("[ProfileSectionUI] Initializing UI component...");
        
        SetupInitialUIState();
        ValidateUIComponents();
        
        _isActive = false;
        _isOfflineMode = false;
        
        Debug.Log("[ProfileSectionUI] UI component initialized");
    }
    
    /// <summary>
    /// UI 컴포넌트 유효성 검사
    /// </summary>
    public bool ValidateUIComponents()
    {
        bool isValid = true;
        
        // 필수 컴포넌트 검사
        if (userNameText == null)
        {
            Debug.LogError("[ProfileSectionUI] User name text is missing!");
            isValid = false;
        }
        
        if (userLevelText == null)
        {
            Debug.LogError("[ProfileSectionUI] User level text is missing!");
            isValid = false;
        }
        
        if (profileDetailButton == null)
        {
            Debug.LogError("[ProfileSectionUI] Profile detail button is missing!");
            isValid = false;
        }
        
        // 선택적 컴포넌트 경고
        if (profileAvatarImage == null)
            Debug.LogWarning("[ProfileSectionUI] Profile avatar image is not assigned");
            
        if (experienceSlider == null)
            Debug.LogWarning("[ProfileSectionUI] Experience slider is not assigned");
        
        if (onlineIndicator == null)
            Debug.LogWarning("[ProfileSectionUI] Online indicator is not assigned");
        
        return isValid;
    }
    #endregion

    #region UI State Management
    /// <summary>
    /// UI 활성화
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
        
        if (active)
        {
            RefreshDisplay();
            StartOnlineIndicatorBlink();
        }
        else
        {
            StopAllAnimations();
        }
        
        Debug.Log($"[ProfileSectionUI] UI set to {(active ? "active" : "inactive")}");
    }
    
    /// <summary>
    /// 오프라인 모드 설정
    /// </summary>
    public void SetOfflineMode(bool isOfflineMode)
    {
        _isOfflineMode = isOfflineMode;
        
        UpdateOfflineVisuals();
        UpdateButtonInteractivity();
        
        Debug.Log($"[ProfileSectionUI] Offline mode set to: {isOfflineMode}");
    }
    
    /// <summary>
    /// 강제 새로고침
    /// </summary>
    public void ForceRefresh()
    {
        Debug.Log("[ProfileSectionUI] Force refresh triggered");
        
        RefreshDisplay();
        
        // 프로필 이미지 재로드
        if (!_profileImageLoaded && _currentUserData != null)
        {
            LoadProfileAvatar(_currentUserData.AvatarUrl);
        }
    }
    
    /// <summary>
    /// UI 정리
    /// </summary>
    public void Cleanup()
    {
        Debug.Log("[ProfileSectionUI] Cleaning up UI component...");
        
        UnsubscribeUIEvents();
        StopAllAnimations();
        
        _isActive = false;
        
        Debug.Log("[ProfileSectionUI] UI component cleaned up");
    }
    #endregion

    #region Data Updates
    /// <summary>
    /// 사용자 프로필 정보 업데이트
    /// </summary>
    public void UpdateUserProfile(UserData userData)
    {
        if (userData == null || !ShouldUpdateUI()) return;
        
        _currentUserData = userData;
        
        SafeUIUpdate(() =>
        {
            UpdateUserInfoDisplay(userData);
            _lastUIUpdate = DateTime.Now;
        }, "User Profile Update");
        
        Debug.Log($"[ProfileSectionUI] User profile updated: {userData.DisplayName}");
    }
    
    /// <summary>
    /// 프로필 통계 업데이트
    /// </summary>
    public void UpdateProfileStats(ProfileStats stats)
    {
        if (stats == null || !ShouldUpdateUI()) return;
        
        _currentStats = stats;
        
        SafeUIUpdate(() =>
        {
            UpdateStatsDisplay(stats);
            UpdateExperienceDisplay(stats);
            _lastUIUpdate = DateTime.Now;
        }, "Profile Stats Update");
        
        Debug.Log("[ProfileSectionUI] Profile stats updated");
    }
    
    /// <summary>
    /// 업적 정보 업데이트
    /// </summary>
    public void UpdateAchievements(Achievement[] achievements)
    {
        if (achievements == null) return;
        
        SafeUIUpdate(() =>
        {
            UpdateAchievementDisplay(achievements);
        }, "Achievements Update");
        
        Debug.Log($"[ProfileSectionUI] Achievements updated: {achievements.Length} items");
    }
    
    /// <summary>
    /// 온라인 상태 업데이트
    /// </summary>
    public void UpdateOnlineStatus(OnlineStatus status)
    {
        if (status == null) return;
        
        _currentOnlineStatus = status;
        
        SafeUIUpdate(() =>
        {
            UpdateOnlineStatusDisplay(status);
        }, "Online Status Update");
        
        Debug.Log($"[ProfileSectionUI] Online status updated: {status.IsOnline}");
    }
    #endregion

    #region UI Display Updates
    /// <summary>
    /// 사용자 정보 표시 업데이트
    /// </summary>
    private void UpdateUserInfoDisplay(UserData userData)
    {
        if (userNameText != null)
            userNameText.text = userData.DisplayName ?? "Unknown Player";
            
        if (userLevelText != null)
            userLevelText.text = $"Lv. {userData.Level}";
            
        if (userTitleText != null)
            userTitleText.text = userData.Title ?? "";
        
        // 프로필 이미지 로드
        LoadProfileAvatar(userData.AvatarUrl);
    }
    
    /// <summary>
    /// 통계 표시 업데이트
    /// </summary>
    private void UpdateStatsDisplay(ProfileStats stats)
    {
        if (gamesPlayedText != null)
            gamesPlayedText.text = stats.TotalGamesPlayed.ToString();
            
        if (winRateText != null)
            winRateText.text = $"{stats.WinRate:F1}%";
            
        if (rankingText != null)
            rankingText.text = stats.Ranking > 0 ? $"#{stats.Ranking}" : "Unranked";
        
        if (totalPlayTimeText != null)
            totalPlayTimeText.text = FormatPlayTime(stats.PlayTime);
            
        if (lastPlayedText != null)
            lastPlayedText.text = FormatLastPlayedTime(stats.LastPlayedDate);
    }
    
    /// <summary>
    /// 경험치 표시 업데이트
    /// </summary>
    private void UpdateExperienceDisplay(ProfileStats stats)
    {
        if (experienceSlider == null || experienceText == null) return;
        
        int currentLevelExp = stats.Experience;
        int nextLevelExp = stats.NextLevelExperience;
        int currentLevelTotal = (stats.Level - 1) * 1000; // 이전 레벨까지 누적 경험치
        
        float normalizedExp = nextLevelExp > currentLevelTotal ? 
            (float)(currentLevelExp - currentLevelTotal) / (nextLevelExp - currentLevelTotal) : 0f;
        
        // 애니메이션으로 경험치 바 업데이트
        AnimateExperienceBar(normalizedExp);
        
        experienceText.text = $"{currentLevelExp - currentLevelTotal}/{nextLevelExp - currentLevelTotal}";
    }
    
    /// <summary>
    /// 업적 표시 업데이트
    /// </summary>
    private void UpdateAchievementDisplay(Achievement[] achievements)
    {
        int maxSlots = Mathf.Min(achievementSlots.Length, achievements.Length);
        
        for (int i = 0; i < achievementSlots.Length; i++)
        {
            if (achievementSlots[i] == null) continue;
            
            bool shouldShow = i < maxSlots && achievements[i].IsUnlocked;
            achievementSlots[i].SetActive(shouldShow);
            
            if (shouldShow)
            {
                // TODO: 업적 슬롯 UI 업데이트 (아이콘, 이름 등)
                var achievement = achievements[i];
                Debug.Log($"[ProfileSectionUI] Showing achievement: {achievement.Name}");
            }
        }
    }
    
    /// <summary>
    /// 온라인 상태 표시 업데이트
    /// </summary>
    private void UpdateOnlineStatusDisplay(OnlineStatus status)
    {
        if (onlineIndicator != null)
        {
            onlineIndicator.SetActive(status.IsOnline);
            
            if (status.IsOnline)
            {
                StartOnlineIndicatorBlink();
            }
            else
            {
                StopOnlineIndicatorBlink();
            }
        }
    }
    #endregion

    #region UI Events
    /// <summary>
    /// UI 이벤트 설정
    /// </summary>
    private void SetupUIEvents()
    {
        if (profileDetailButton != null)
            profileDetailButton.onClick.AddListener(() => OnProfileDetailRequested?.Invoke());
            
        if (editProfileButton != null)
            editProfileButton.onClick.AddListener(() => OnEditProfileRequested?.Invoke());
            
        if (achievementsButton != null)
            achievementsButton.onClick.AddListener(() => OnAchievementsRequested?.Invoke());
            
        if (statisticsButton != null)
            statisticsButton.onClick.AddListener(() => OnStatisticsRequested?.Invoke());
    }
    
    /// <summary>
    /// UI 이벤트 해제
    /// </summary>
    private void UnsubscribeUIEvents()
    {
        if (profileDetailButton != null)
            profileDetailButton.onClick.RemoveAllListeners();
            
        if (editProfileButton != null)
            editProfileButton.onClick.RemoveAllListeners();
            
        if (achievementsButton != null)
            achievementsButton.onClick.RemoveAllListeners();
            
        if (statisticsButton != null)
            statisticsButton.onClick.RemoveAllListeners();
    }
    #endregion

    #region Visual Effects & Animations
    /// <summary>
    /// 경험치 바 애니메이션
    /// </summary>
    private void AnimateExperienceBar(float targetValue)
    {
        if (experienceSlider == null) return;
        
        _targetExperienceValue = targetValue;
        
        if (_experienceAnimationCoroutine != null)
            StopCoroutine(_experienceAnimationCoroutine);
            
        _experienceAnimationCoroutine = StartCoroutine(ExperienceAnimationCoroutine());
    }
    
    /// <summary>
    /// 경험치 애니메이션 코루틴
    /// </summary>
    private IEnumerator ExperienceAnimationCoroutine()
    {
        float startValue = experienceSlider.value;
        float elapsed = 0f;
        
        while (elapsed < experienceAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / experienceAnimationDuration;
            t = experienceAnimationCurve.Evaluate(t);
            
            float currentValue = Mathf.Lerp(startValue, _targetExperienceValue, t);
            experienceSlider.value = currentValue;
            
            yield return null;
        }
        
        experienceSlider.value = _targetExperienceValue;
        _experienceAnimationCoroutine = null;
    }
    
    /// <summary>
    /// 온라인 인디케이터 깜빡임 시작
    /// </summary>
    private void StartOnlineIndicatorBlink()
    {
        StopOnlineIndicatorBlink();
        
        if (onlineIndicator != null && _currentOnlineStatus?.IsOnline == true)
        {
            _onlineIndicatorBlinkCoroutine = StartCoroutine(OnlineIndicatorBlinkCoroutine());
        }
    }
    
    /// <summary>
    /// 온라인 인디케이터 깜빡임 중지
    /// </summary>
    private void StopOnlineIndicatorBlink()
    {
        if (_onlineIndicatorBlinkCoroutine != null)
        {
            StopCoroutine(_onlineIndicatorBlinkCoroutine);
            _onlineIndicatorBlinkCoroutine = null;
        }
    }
    
    /// <summary>
    /// 온라인 인디케이터 깜빡임 코루틴
    /// </summary>
    private IEnumerator OnlineIndicatorBlinkCoroutine()
    {
        while (_currentOnlineStatus?.IsOnline == true)
        {
            if (onlineIndicator != null)
            {
                var canvasGroup = onlineIndicator.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.PingPong(Time.time, 1f);
                }
            }
            
            yield return new WaitForSeconds(ONLINE_INDICATOR_BLINK_INTERVAL);
        }
    }
    
    /// <summary>
    /// 모든 애니메이션 중지
    /// </summary>
    private void StopAllAnimations()
    {
        if (_experienceAnimationCoroutine != null)
        {
            StopCoroutine(_experienceAnimationCoroutine);
            _experienceAnimationCoroutine = null;
        }
        
        StopOnlineIndicatorBlink();
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// UI 업데이트 스로틀링 확인
    /// </summary>
    private bool ShouldUpdateUI()
    {
        var timeSinceLastUpdate = DateTime.Now - _lastUIUpdate;
        return timeSinceLastUpdate.TotalSeconds >= UI_UPDATE_THROTTLE;
    }
    
    /// <summary>
    /// 안전한 UI 업데이트 실행
    /// </summary>
    private void SafeUIUpdate(Action updateAction, string operationName = "UI Update")
    {
        if (!_isActive)
        {
            return;
        }
        
        try
        {
            updateAction?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSectionUI] {operationName} failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// 초기 UI 상태 설정
    /// </summary>
    private void SetupInitialUIState()
    {
        UpdateOfflineVisuals();
        UpdateButtonInteractivity();
        
        if (experienceSlider != null)
            experienceSlider.value = 0f;
    }
    
    /// <summary>
    /// 오프라인 시각 효과 업데이트
    /// </summary>
    private void UpdateOfflineVisuals()
    {
        if (offlineOverlay != null)
            offlineOverlay.SetActive(_isOfflineMode);
        
        if (onlineIndicator != null)
            onlineIndicator.SetActive(!_isOfflineMode);
    }
    
    /// <summary>
    /// 버튼 상호작용성 업데이트
    /// </summary>
    private void UpdateButtonInteractivity()
    {
        // 오프라인 모드에서는 일부 기능 비활성화
        if (editProfileButton != null)
            editProfileButton.interactable = !_isOfflineMode;
        
        if (achievementsButton != null)
            achievementsButton.interactable = !_isOfflineMode;
    }
    
    /// <summary>
    /// 전체 디스플레이 새로고침
    /// </summary>
    private void RefreshDisplay()
    {
        if (_currentUserData != null)
        {
            UpdateUserInfoDisplay(_currentUserData);
        }
        
        if (_currentStats != null)
        {
            UpdateStatsDisplay(_currentStats);
            UpdateExperienceDisplay(_currentStats);
        }
        
        if (_currentOnlineStatus != null)
        {
            UpdateOnlineStatusDisplay(_currentOnlineStatus);
        }
    }
    
    /// <summary>
    /// 프로필 아바타 로드
    /// </summary>
    private void LoadProfileAvatar(string avatarUrl)
    {
        if (profileAvatarImage == null || string.IsNullOrEmpty(avatarUrl))
            return;
        
        // TODO: 실제 이미지 로딩 구현 (향후 NetworkManager와 연동)
        Debug.Log($"[ProfileSectionUI] Loading avatar: {avatarUrl}");
        _profileImageLoaded = false;
    }
    
    /// <summary>
    /// 플레이 시간 포맷팅
    /// </summary>
    private string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime.TotalDays >= 1)
            return $"{(int)playTime.TotalDays}d {playTime.Hours}h";
        else if (playTime.TotalHours >= 1)
            return $"{(int)playTime.TotalHours}h {playTime.Minutes}m";
        else
            return $"{(int)playTime.TotalMinutes}m";
    }
    
    /// <summary>
    /// 마지막 플레이 시간 포맷팅
    /// </summary>
    private string FormatLastPlayedTime(DateTime lastPlayed)
    {
        var timeSince = DateTime.Now - lastPlayed;
        
        if (timeSince.TotalDays >= 1)
            return $"{(int)timeSince.TotalDays} days ago";
        else if (timeSince.TotalHours >= 1)
            return $"{(int)timeSince.TotalHours} hours ago";
        else if (timeSince.TotalMinutes >= 1)
            return $"{(int)timeSince.TotalMinutes} minutes ago";
        else
            return "Just now";
    }
    #endregion
}