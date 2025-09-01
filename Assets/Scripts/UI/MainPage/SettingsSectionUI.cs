using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 설정 섹션 UI 컴포넌트
/// 게임 설정과 사용자 계정 관리를 제공하는 UI 섹션입니다.
/// 하단 Footer 영역에 배치되어 빠른 접근을 제공합니다.
/// </summary>
public class SettingsSectionUI : SectionBase
{
    #region UI References
    [Header("Quick Actions")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button helpButton;
    [SerializeField] private Button notificationButton;
    
    [Header("Audio Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle muteToggle;
    
    [Header("Display Settings")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Dropdown qualityDropdown;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Slider brightnessSlider;
    
    [Header("Notification Panel")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private Text notificationText;
    [SerializeField] private Button closeNotificationButton;
    [SerializeField] private Image notificationIcon;
    
    [Header("Visual Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject newNotificationIndicator;
    [SerializeField] private Text versionText;
    #endregion

    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Settings;
    public override string SectionDisplayName => "설정";
    #endregion

    #region Private Fields
    private bool _settingsChanged = false;
    private Dictionary<string, object> _pendingSettings = new Dictionary<string, object>();
    private Coroutine _settingsSaveCoroutine;
    
    // Audio settings cache
    private float _masterVolume = 1.0f;
    private float _musicVolume = 0.8f;
    private float _sfxVolume = 0.9f;
    private bool _isMuted = false;
    
    // Display settings cache
    private bool _isFullscreen = false;
    private int _qualityLevel = 2;
    private bool _vibrationEnabled = true;
    private float _brightness = 0.8f;
    
    // Notification system
    private Queue<NotificationMessage> _notificationQueue = new Queue<NotificationMessage>();
    private bool _isShowingNotification = false;
    #endregion

    #region Section Implementation
    protected override void OnInitialize()
    {
        SetupUIEvents();
        LoadCurrentSettings();
        InitializeUI();
        SetupVersionInfo();
        ValidateSettingsComponents();
    }

    protected override void OnActivate()
    {
        RefreshSettingsDisplay();
        CheckForNewNotifications();
    }

    protected override void OnDeactivate()
    {
        SavePendingSettings();
    }

    protected override void OnCleanup()
    {
        UnsubscribeFromUIEvents();
        SavePendingSettings();
        StopAllCoroutines();
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null) return;
        
        // 사용자별 설정 업데이트
        LoadUserSpecificSettings(userData);
        
        Debug.Log($"[SettingsSectionUI] UI updated for user: {userData.DisplayName}");
    }

    protected override void ValidateComponents()
    {
        // 필수 컴포넌트 검증
        if (settingsButton == null)
            ReportError("Settings button is missing!");
            
        if (logoutButton == null)
            ReportError("Logout button is missing!");
            
        // 경고 레벨 컴포넌트
        if (masterVolumeSlider == null)
            Debug.LogWarning("[SettingsSectionUI] Master volume slider is not assigned");
            
        if (notificationPanel == null)
            Debug.LogWarning("[SettingsSectionUI] Notification panel is not assigned");
    }
    #endregion

    #region UI Event Setup
    private void SetupUIEvents()
    {
        // Quick action buttons
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
            
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);
            
        if (helpButton != null)
            helpButton.onClick.AddListener(OnHelpClicked);
            
        if (notificationButton != null)
            notificationButton.onClick.AddListener(OnNotificationClicked);
            
        // Audio controls
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            
        if (muteToggle != null)
            muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
            
        // Display settings
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
            
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.AddListener(OnVibrationToggleChanged);
            
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            
        // Notification panel
        if (closeNotificationButton != null)
            closeNotificationButton.onClick.AddListener(OnCloseNotificationClicked);
    }

    private void UnsubscribeFromUIEvents()
    {
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            
        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
            
        if (helpButton != null)
            helpButton.onClick.RemoveListener(OnHelpClicked);
            
        if (notificationButton != null)
            notificationButton.onClick.RemoveListener(OnNotificationClicked);
            
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            
        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveListener(OnMuteToggleChanged);
            
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenToggleChanged);
            
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
            
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.RemoveListener(OnVibrationToggleChanged);
            
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            
        if (closeNotificationButton != null)
            closeNotificationButton.onClick.RemoveListener(OnCloseNotificationClicked);
    }
    #endregion

    #region Settings Management
    private void LoadCurrentSettings()
    {
        // SettingsManager에서 현재 설정 로드
        if (_settingsManager != null)
        {
            _masterVolume = GetSetting<float>("MasterVolume") ?? 1.0f;
            _musicVolume = GetSetting<float>("MusicVolume") ?? 0.8f;
            _sfxVolume = GetSetting<float>("SfxVolume") ?? 0.9f;
            _isMuted = GetSetting<bool>("IsMuted") ?? false;
            
            _isFullscreen = GetSetting<bool>("IsFullscreen") ?? false;
            _qualityLevel = GetSetting<int>("QualityLevel") ?? 2;
            _vibrationEnabled = GetSetting<bool>("VibrationEnabled") ?? true;
            _brightness = GetSetting<float>("Brightness") ?? 0.8f;
        }
        
        Debug.Log("[SettingsSectionUI] Settings loaded from SettingsManager");
    }

    private void InitializeUI()
    {
        SafeUIUpdate(() =>
        {
            // Audio sliders
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = _masterVolume;
                
            if (musicVolumeSlider != null)
                musicVolumeSlider.value = _musicVolume;
                
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = _sfxVolume;
                
            if (muteToggle != null)
                muteToggle.isOn = _isMuted;
                
            // Display settings
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = _isFullscreen;
                
            if (qualityDropdown != null)
            {
                PopulateQualityDropdown();
                qualityDropdown.value = _qualityLevel;
            }
                
            if (vibrationToggle != null)
                vibrationToggle.isOn = _vibrationEnabled;
                
            if (brightnessSlider != null)
                brightnessSlider.value = _brightness;
                
        }, "Initialize UI");
    }

    private void PopulateQualityDropdown()
    {
        if (qualityDropdown == null) return;
        
        qualityDropdown.ClearOptions();
        
        List<string> qualityOptions = new List<string>
        {
            "낮음",
            "보통",
            "높음",
            "매우 높음"
        };
        
        qualityDropdown.AddOptions(qualityOptions);
    }

    private void LoadUserSpecificSettings(UserData userData)
    {
        // 사용자별 개인화 설정 로드 (향후 구현)
        Debug.Log($"[SettingsSectionUI] Loading user specific settings for: {userData.DisplayName}");
    }

    private void SavePendingSettings()
    {
        if (!_settingsChanged || _pendingSettings.Count == 0) return;
        
        if (_settingsSaveCoroutine != null)
            StopCoroutine(_settingsSaveCoroutine);
            
        _settingsSaveCoroutine = StartCoroutine(SaveSettingsCoroutine());
    }

    private IEnumerator SaveSettingsCoroutine()
    {
        yield return new WaitForSeconds(0.5f); // 설정 변경이 완전히 끝날 때까지 대기
        
        foreach (var kvp in _pendingSettings)
        {
            SetSetting(kvp.Key, kvp.Value);
            Debug.Log($"[SettingsSectionUI] Saved setting: {kvp.Key} = {kvp.Value}");
        }
        
        _pendingSettings.Clear();
        _settingsChanged = false;
        
        // 다른 섹션들에게 설정 변경 알림
        BroadcastToAllSections(new SettingsChangedMessage
        {
            Timestamp = DateTime.Now,
            SettingsCount = _pendingSettings.Count
        });
        
        Debug.Log("[SettingsSectionUI] All pending settings saved");
    }

    private void QueueSettingChange(string settingName, object value)
    {
        _pendingSettings[settingName] = value;
        _settingsChanged = true;
        
        // 자동 저장 타이머 재시작
        if (_settingsSaveCoroutine != null)
            StopCoroutine(_settingsSaveCoroutine);
            
        _settingsSaveCoroutine = StartCoroutine(SaveSettingsCoroutine());
    }
    #endregion

    #region Event Handlers
    private void OnSettingsClicked()
    {
        Debug.Log("[SettingsSectionUI] Settings button clicked");
        
        // 상세 설정 화면으로 전환 (향후 구현)
        SendMessageToSection(MainPageSectionType.Profile, new SettingsRequest
        {
            RequestType = "show_detailed_settings",
            FromSection = SectionType
        });
    }

    private void OnLogoutClicked()
    {
        Debug.Log("[SettingsSectionUI] Logout button clicked");
        
        ShowLogoutConfirmation();
    }

    private void OnHelpClicked()
    {
        Debug.Log("[SettingsSectionUI] Help button clicked");
        
        // 도움말 화면으로 전환
        SendMessageToSection(MainPageSectionType.Profile, new HelpRequest
        {
            RequestType = "show_help",
            Topic = "main_menu"
        });
    }

    private void OnNotificationClicked()
    {
        Debug.Log("[SettingsSectionUI] Notification button clicked");
        
        ShowNextNotification();
    }

    // Audio event handlers
    private void OnMasterVolumeChanged(float value)
    {
        _masterVolume = value;
        ApplyAudioSetting("MasterVolume", value);
        QueueSettingChange("MasterVolume", value);
        
        Debug.Log($"[SettingsSectionUI] Master volume changed: {value}");
    }

    private void OnMusicVolumeChanged(float value)
    {
        _musicVolume = value;
        ApplyAudioSetting("MusicVolume", value);
        QueueSettingChange("MusicVolume", value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        _sfxVolume = value;
        ApplyAudioSetting("SfxVolume", value);
        QueueSettingChange("SfxVolume", value);
    }

    private void OnMuteToggleChanged(bool isMuted)
    {
        _isMuted = isMuted;
        ApplyAudioSetting("IsMuted", isMuted);
        QueueSettingChange("IsMuted", isMuted);
        
        // 음소거 시 슬라이더 비활성화
        if (masterVolumeSlider != null)
            masterVolumeSlider.interactable = !isMuted;
        if (musicVolumeSlider != null)
            musicVolumeSlider.interactable = !isMuted;
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.interactable = !isMuted;
    }

    // Display event handlers
    private void OnFullscreenToggleChanged(bool isFullscreen)
    {
        _isFullscreen = isFullscreen;
        ApplyDisplaySetting("IsFullscreen", isFullscreen);
        QueueSettingChange("IsFullscreen", isFullscreen);
        
        Screen.fullScreen = isFullscreen;
    }

    private void OnQualityChanged(int qualityIndex)
    {
        _qualityLevel = qualityIndex;
        ApplyDisplaySetting("QualityLevel", qualityIndex);
        QueueSettingChange("QualityLevel", qualityIndex);
        
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    private void OnVibrationToggleChanged(bool enabled)
    {
        _vibrationEnabled = enabled;
        ApplyDisplaySetting("VibrationEnabled", enabled);
        QueueSettingChange("VibrationEnabled", enabled);
    }

    private void OnBrightnessChanged(float value)
    {
        _brightness = value;
        ApplyDisplaySetting("Brightness", value);
        QueueSettingChange("Brightness", value);
        
        Screen.brightness = value;
    }

    private void OnCloseNotificationClicked()
    {
        HideNotificationPanel();
    }
    #endregion

    #region Settings Application
    private void ApplyAudioSetting(string settingName, object value)
    {
        // 오디오 설정 즉시 적용 (AudioManager와 연동)
        switch (settingName)
        {
            case "MasterVolume":
                AudioListener.volume = _isMuted ? 0f : (float)value;
                break;
            case "IsMuted":
                AudioListener.volume = (bool)value ? 0f : _masterVolume;
                break;
        }
        
        Debug.Log($"[SettingsSectionUI] Applied audio setting: {settingName} = {value}");
    }

    private void ApplyDisplaySetting(string settingName, object value)
    {
        // 디스플레이 설정 즉시 적용
        Debug.Log($"[SettingsSectionUI] Applied display setting: {settingName} = {value}");
    }
    #endregion

    #region Notification System
    private void CheckForNewNotifications()
    {
        // 새로운 알림 확인 (향후 서버 연동)
        ShowNewNotificationIndicator(_notificationQueue.Count > 0);
    }

    private void ShowNewNotificationIndicator(bool show)
    {
        if (newNotificationIndicator != null)
            newNotificationIndicator.SetActive(show);
    }

    private void ShowNextNotification()
    {
        if (_notificationQueue.Count == 0 || _isShowingNotification) return;
        
        NotificationMessage notification = _notificationQueue.Dequeue();
        ShowNotificationPanel(notification);
    }

    private void ShowNotificationPanel(NotificationMessage notification)
    {
        if (notificationPanel == null) return;
        
        _isShowingNotification = true;
        
        SafeUIUpdate(() =>
        {
            if (notificationText != null)
                notificationText.text = notification.Message;
                
            if (notificationIcon != null && notification.IconSprite != null)
                notificationIcon.sprite = notification.IconSprite;
                
            notificationPanel.SetActive(true);
            
        }, "Show Notification");
        
        // 자동 닫기 타이머 시작
        StartCoroutine(AutoCloseNotificationCoroutine(notification.AutoCloseDelay));
    }

    private void HideNotificationPanel()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
            
        _isShowingNotification = false;
        
        // 다음 알림이 있으면 표시
        if (_notificationQueue.Count > 0)
        {
            StartCoroutine(ShowNextNotificationWithDelay());
        }
        else
        {
            ShowNewNotificationIndicator(false);
        }
    }

    private IEnumerator ShowNextNotificationWithDelay()
    {
        yield return new WaitForSeconds(1f);
        ShowNextNotification();
    }

    private IEnumerator AutoCloseNotificationCoroutine(float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
            if (_isShowingNotification)
            {
                HideNotificationPanel();
            }
        }
    }

    public void AddNotification(NotificationMessage notification)
    {
        _notificationQueue.Enqueue(notification);
        ShowNewNotificationIndicator(true);
        
        if (!_isShowingNotification)
        {
            ShowNextNotification();
        }
    }
    #endregion

    #region Logout Confirmation
    private void ShowLogoutConfirmation()
    {
        // 로그아웃 확인 대화상자 표시
        AddNotification(new NotificationMessage
        {
            Message = "정말 로그아웃하시겠습니까?",
            Type = NotificationType.Confirmation,
            AutoCloseDelay = 0f, // 수동으로 닫아야 함
            OnConfirm = ConfirmLogout,
            OnCancel = CancelLogout
        });
    }

    private void ConfirmLogout()
    {
        Debug.Log("[SettingsSectionUI] Logout confirmed");
        
        // 설정 저장 후 로그아웃
        SavePendingSettings();
        
        // MainPageManager를 통한 로그아웃
        RequestLogout();
    }

    private void CancelLogout()
    {
        Debug.Log("[SettingsSectionUI] Logout cancelled");
        HideNotificationPanel();
    }
    #endregion

    #region Version and Info
    private void SetupVersionInfo()
    {
        if (versionText != null)
        {
            versionText.text = $"v{Application.version}";
        }
    }
    #endregion

    #region Virtual Method Overrides
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        // 오프라인 모드에서는 일부 설정 비활성화
        if (logoutButton != null)
            logoutButton.interactable = !isOfflineMode;
    }

    protected override void OnForceRefresh()
    {
        LoadCurrentSettings();
        InitializeUI();
        RefreshSettingsDisplay();
    }

    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[SettingsSectionUI] Received message from {fromSection}: {data?.GetType().Name}");
        
        if (data is ProfileDetailRequest profileRequest)
        {
            HandleProfileRequest(profileRequest);
        }
        else if (data is AchievementRequest achievementRequest)
        {
            HandleAchievementRequest(achievementRequest);
        }
        else if (data is StatisticsRequest statsRequest)
        {
            HandleStatisticsRequest(statsRequest);
        }
        else if (data is string message && message == "focus_requested")
        {
            RefreshSettingsDisplay();
        }
    }

    protected override void OnSettingUpdated(string settingName, object newValue)
    {
        // 외부에서 설정 변경 시 UI 업데이트
        RefreshSettingsDisplay();
    }
    #endregion

    #region Message Handling
    private void HandleProfileRequest(ProfileDetailRequest request)
    {
        switch (request.RequestType)
        {
            case "show_detail":
                ShowProfileDetail(request.UserData);
                break;
        }
    }

    private void HandleAchievementRequest(AchievementRequest request)
    {
        switch (request.RequestType)
        {
            case "show_achievements":
                ShowAchievements(request.UserId);
                break;
        }
    }

    private void HandleStatisticsRequest(StatisticsRequest request)
    {
        switch (request.RequestType)
        {
            case "show_statistics":
                ShowStatistics(request.UserId);
                break;
        }
    }

    private void ShowProfileDetail(UserData userData)
    {
        Debug.Log($"[SettingsSectionUI] Showing profile detail for: {userData?.DisplayName}");
        // 프로필 상세 화면 표시 (향후 구현)
    }

    private void ShowAchievements(string userId)
    {
        Debug.Log($"[SettingsSectionUI] Showing achievements for user: {userId}");
        // 업적 화면 표시 (향후 구현)
    }

    private void ShowStatistics(string userId)
    {
        Debug.Log($"[SettingsSectionUI] Showing statistics for user: {userId}");
        // 통계 화면 표시 (향후 구현)
    }
    #endregion

    #region Utility Methods
    private void RefreshSettingsDisplay()
    {
        LoadCurrentSettings();
        InitializeUI();
        CheckForNewNotifications();
    }
    #endregion
}

#region Data Classes
[System.Serializable]
public class NotificationMessage
{
    public string Message;
    public NotificationType Type;
    public Sprite IconSprite;
    public float AutoCloseDelay = 5f;
    public Action OnConfirm;
    public Action OnCancel;
}

[System.Serializable]
public class SettingsRequest
{
    public string RequestType;
    public MainPageSectionType FromSection;
}

[System.Serializable]
public class HelpRequest
{
    public string RequestType;
    public string Topic;
}

[System.Serializable]
public class SettingsChangedMessage
{
    public DateTime Timestamp;
    public int SettingsCount;
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success,
    Confirmation
}
#endregion