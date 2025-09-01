using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설정 섹션 비즈니스 로직 컨트롤러
/// SettingsSectionUI와 분리된 로직 관리자로 설정 관리, 로그아웃, 알림 시스템을 담당합니다.
/// MainPageManager와 연동하여 다른 섹션들과의 통신을 조정합니다.
/// </summary>
public class SettingsSection : SectionBase
{
    #region UI Component References
    [Header("UI Component Reference")]
    [SerializeField] private SettingsSectionUIComponent _settingsUI;
    #endregion

    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Settings;
    public override string SectionDisplayName => "설정 관리";
    #endregion

    #region Private Fields
    private Dictionary<string, object> _settingsCache = new Dictionary<string, object>();
    private Queue<SettingsNotification> _notificationQueue = new Queue<SettingsNotification>();
    private bool _isLogoutInProgress = false;
    private Coroutine _settingsSyncCoroutine;
    
    // 설정 동기화 관련
    private const float SETTINGS_SYNC_INTERVAL = 60f; // 1분마다 설정 동기화
    private const string SETTINGS_SYNC_KEY = "settings_last_sync";
    
    // 주요 설정 키들
    private readonly string[] _criticalSettings = {
        "MasterVolume", "MusicVolume", "SfxVolume", "IsMuted",
        "IsFullscreen", "QualityLevel", "VibrationEnabled", "Brightness"
    };
    #endregion

    #region Section Implementation
    protected override void OnInitialize()
    {
        ValidateUIComponent();
        SetupUIEventHandlers();
        InitializeSettingsCache();
        StartSettingsSync();
        
        Debug.Log($"[{SectionType}Section] Business logic initialized");
    }

    protected override void OnActivate()
    {
        if (_settingsUI != null)
        {
            _settingsUI.gameObject.SetActive(true);
        }
        
        RefreshSettingsFromCache();
        ProcessPendingNotifications();
        
        Debug.Log($"[{SectionType}Section] Activated with settings management");
    }

    protected override void OnDeactivate()
    {
        if (_settingsUI != null)
        {
            // UI는 비활성화하지 않음 (Footer는 항상 표시)
            // _settingsUI.gameObject.SetActive(false);
        }
        
        SaveCurrentSettings();
        Debug.Log($"[{SectionType}Section] Deactivated, settings saved");
    }

    protected override void OnCleanup()
    {
        UnsubscribeFromUIEvents();
        SaveCurrentSettings();
        StopSettingsSync();
        
        Debug.Log($"[{SectionType}Section] Cleaned up");
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null || _settingsUI == null) return;
        
        // SettingsSectionUI가 자체적으로 UpdateUI를 처리하므로 위임
        SafeUIUpdate(() =>
        {
            // 사용자별 개인화 설정 적용
            ApplyUserSpecificSettings(userData);
            
            // 알림 확인
            CheckUserSpecificNotifications(userData);
            
        }, "User Data Update");
        
        Debug.Log($"[{SectionType}Section] UI updated for user: {userData.DisplayName}");
    }

    protected override void ValidateComponents()
    {
        if (_settingsUI == null)
        {
            _settingsUI = GetComponent<SettingsSectionUIComponent>();
            if (_settingsUI == null)
            {
                _settingsUI = GetComponentInChildren<SettingsSectionUIComponent>();
            }
        }
        
        if (_settingsUI == null)
        {
            ReportError("SettingsSectionUIComponent component is required!");
            return;
        }
        
        Debug.Log($"[{SectionType}Section] Components validated successfully");
    }
    #endregion

    #region UI Integration
    private void ValidateUIComponent()
    {
        if (_settingsUI == null)
        {
            ReportError("SettingsSectionUIComponent component reference is missing!");
            return;
        }
        
        if (!_settingsUI.IsInitialized)
        {
            Debug.LogWarning($"[{SectionType}Section] UI component not yet initialized, waiting...");
        }
    }
    
    private void SetupUIEventHandlers()
    {
        if (_settingsUI == null) return;
        
        // UI 이벤트 구독
        SettingsSectionUIComponent.OnSettingsButtonClicked += HandleSettingsButtonClicked;
        SettingsSectionUIComponent.OnLogoutButtonClicked += HandleLogoutButtonClicked;
        SettingsSectionUIComponent.OnHelpButtonClicked += HandleHelpButtonClicked;
        SettingsSectionUIComponent.OnNotificationButtonClicked += HandleNotificationButtonClicked;
        SettingsSectionUIComponent.OnAudioSettingChanged += HandleAudioSettingChanged;
        SettingsSectionUIComponent.OnToggleSettingChanged += HandleToggleSettingChanged;
        SettingsSectionUIComponent.OnQualitySettingChanged += HandleQualitySettingChanged;
        SettingsSectionUIComponent.OnNotificationClosed += HandleNotificationClosed;
        
        Debug.Log($"[{SectionType}Section] UI event handlers setup complete");
    }
    
    private void UnsubscribeFromUIEvents()
    {
        // UI 이벤트 구독 해제
        SettingsSectionUIComponent.OnSettingsButtonClicked -= HandleSettingsButtonClicked;
        SettingsSectionUIComponent.OnLogoutButtonClicked -= HandleLogoutButtonClicked;
        SettingsSectionUIComponent.OnHelpButtonClicked -= HandleHelpButtonClicked;
        SettingsSectionUIComponent.OnNotificationButtonClicked -= HandleNotificationButtonClicked;
        SettingsSectionUIComponent.OnAudioSettingChanged -= HandleAudioSettingChanged;
        SettingsSectionUIComponent.OnToggleSettingChanged -= HandleToggleSettingChanged;
        SettingsSectionUIComponent.OnQualitySettingChanged -= HandleQualitySettingChanged;
        SettingsSectionUIComponent.OnNotificationClosed -= HandleNotificationClosed;
        
        Debug.Log($"[{SectionType}Section] UI event handlers unsubscribed");
    }
    
    #region UI Event Handlers
    private void HandleSettingsButtonClicked()
    {
        Debug.Log($"[{SectionType}Section] Settings button clicked");
        
        // 상세 설정 화면 요청을 다른 섹션으로 전달
        SendMessageToSection(MainPageSectionType.Profile, new SettingsRequest
        {
            RequestType = "show_detailed_settings",
            FromSection = SectionType
        });
    }
    
    private void HandleLogoutButtonClicked()
    {
        Debug.Log($"[{SectionType}Section] Logout button clicked - initiating logout sequence");
        InitiateLogout();
    }
    
    private void HandleHelpButtonClicked()
    {
        Debug.Log($"[{SectionType}Section] Help button clicked");
        
        // 도움말 요청을 다른 섹션으로 전달
        SendMessageToSection(MainPageSectionType.Profile, new HelpRequest
        {
            RequestType = "show_help",
            Topic = "main_menu"
        });
    }
    
    private void HandleNotificationButtonClicked()
    {
        Debug.Log($"[{SectionType}Section] Notification button clicked");
        
        if (_settingsUI != null)
        {
            _settingsUI.ShowNextNotification();
        }
    }
    
    private void HandleAudioSettingChanged(string settingName, float value)
    {
        Debug.Log($"[{SectionType}Section] Audio setting changed: {settingName} = {value}");
        
        // 캐시 업데이트
        _settingsCache[settingName] = value;
        
        // SettingsManager에 저장
        SetCachedSetting(settingName, value);
        
        // 즉시 적용 (오디오 시스템에)
        ApplyAudioSettingImmediately(settingName, value);
    }
    
    private void HandleToggleSettingChanged(string settingName, bool value)
    {
        Debug.Log($"[{SectionType}Section] Toggle setting changed: {settingName} = {value}");
        
        // 캐시 업데이트
        _settingsCache[settingName] = value;
        
        // SettingsManager에 저장
        SetCachedSetting(settingName, value);
        
        // 즉시 적용
        ApplyDisplaySettingImmediately(settingName, value);
    }
    
    private void HandleQualitySettingChanged(int qualityLevel)
    {
        Debug.Log($"[{SectionType}Section] Quality setting changed: {qualityLevel}");
        
        // 캐시 업데이트
        _settingsCache["QualityLevel"] = qualityLevel;
        
        // SettingsManager에 저장
        SetCachedSetting("QualityLevel", qualityLevel);
        
        // Unity 품질 설정 즉시 적용
        QualitySettings.SetQualityLevel(qualityLevel);
    }
    
    private void HandleNotificationClosed()
    {
        Debug.Log($"[{SectionType}Section] Notification closed by user");
        
        if (_settingsUI != null)
        {
            _settingsUI.HideNotificationPanel();
        }
    }
    
    private void ApplyAudioSettingImmediately(string settingName, float value)
    {
        switch (settingName)
        {
            case "MasterVolume":
                bool isMuted = GetCachedSetting<bool>("IsMuted");
                AudioListener.volume = isMuted ? 0f : value;
                break;
                
            case "Brightness":
                Screen.brightness = value;
                break;
                
            // 음악/효과음 볼륨은 AudioManager가 있을 때 처리
            case "MusicVolume":
            case "SfxVolume":
                // AudioManager와 연동 필요
                Debug.Log($"[{SectionType}Section] {settingName} updated, AudioManager integration needed");
                break;
        }
    }
    
    private void ApplyDisplaySettingImmediately(string settingName, bool value)
    {
        switch (settingName)
        {
            case "IsFullscreen":
                Screen.fullScreen = value;
                break;
                
            case "IsMuted":
                float masterVolume = GetCachedSetting<float>("MasterVolume");
                AudioListener.volume = value ? 0f : masterVolume;
                break;
                
            case "VibrationEnabled":
                // 진동 설정은 모바일에서만 적용
                #if UNITY_ANDROID || UNITY_IOS
                // 진동 설정 적용 로직 필요
                #endif
                break;
        }
    }
    #endregion
    #endregion

    #region Settings Management
    private void InitializeSettingsCache()
    {
        // 주요 설정들을 캐시에 로드
        foreach (string settingKey in _criticalSettings)
        {
            var value = GetSetting<object>(settingKey);
            if (value != null)
            {
                _settingsCache[settingKey] = value;
            }
        }
        
        Debug.Log($"[{SectionType}Section] Settings cache initialized with {_settingsCache.Count} entries");
    }
    
    private void RefreshSettingsFromCache()
    {
        foreach (var kvp in _settingsCache)
        {
            // UI에 설정 값 적용 (SettingsSectionUI의 메서드 호출)
            ApplySettingToUI(kvp.Key, kvp.Value);
        }
        
        Debug.Log($"[{SectionType}Section] Settings refreshed from cache");
    }
    
    private void ApplySettingToUI(string settingName, object value)
    {
        if (_settingsUI == null) return;
        
        SafeUIUpdate(() =>
        {
            switch (settingName)
            {
                case "MasterVolume":
                case "MusicVolume":
                case "SfxVolume":
                case "Brightness":
                    if (value is float floatValue)
                        _settingsUI.UpdateAudioSlider(settingName, floatValue, false);
                    break;
                    
                case "IsMuted":
                case "IsFullscreen":
                case "VibrationEnabled":
                    if (value is bool boolValue)
                        _settingsUI.UpdateToggle(settingName, boolValue, false);
                    break;
                    
                case "QualityLevel":
                    if (value is int intValue)
                        _settingsUI.UpdateQualityDropdown(intValue, false);
                    break;
                    
                default:
                    Debug.LogWarning($"[{SectionType}Section] Unknown setting for UI update: {settingName}");
                    break;
            }
            
            Debug.Log($"[{SectionType}Section] Applied setting to UI: {settingName} = {value}");
        }, $"Apply Setting {settingName}");
    }
    
    private void SaveCurrentSettings()
    {
        int savedCount = 0;
        
        foreach (var kvp in _settingsCache)
        {
            if (SetSetting(kvp.Key, kvp.Value))
            {
                savedCount++;
            }
        }
        
        // 마지막 동기화 시간 업데이트
        PlayerPrefs.SetString(SETTINGS_SYNC_KEY, DateTime.Now.ToBinary().ToString());
        PlayerPrefs.Save();
        
        Debug.Log($"[{SectionType}Section] Saved {savedCount} settings to persistent storage");
    }
    
    private void StartSettingsSync()
    {
        StopSettingsSync();
        _settingsSyncCoroutine = StartCoroutine(SettingsSyncCoroutine());
    }
    
    private void StopSettingsSync()
    {
        if (_settingsSyncCoroutine != null)
        {
            StopCoroutine(_settingsSyncCoroutine);
            _settingsSyncCoroutine = null;
        }
    }
    
    private IEnumerator SettingsSyncCoroutine()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(SETTINGS_SYNC_INTERVAL);
            
            if (_isActive && !_isRefreshing)
            {
                SyncSettingsWithServer();
            }
        }
    }
    
    private void SyncSettingsWithServer()
    {
        // 실제 구현에서는 서버와 설정 동기화
        // 현재는 로컬 동기화만 수행
        SaveCurrentSettings();
        
        // 다른 섹션들에게 설정 동기화 완료 알림
        BroadcastToAllSections(new SettingsSyncMessage
        {
            SyncTime = DateTime.Now,
            SettingsCount = _settingsCache.Count,
            Success = true
        });
        
        Debug.Log($"[{SectionType}Section] Settings synced with server");
    }
    #endregion

    #region User-Specific Settings
    private void ApplyUserSpecificSettings(UserData userData)
    {
        if (userData == null) return;
        
        // 사용자별 개인화 설정 적용
        var userSettingsKey = $"user_settings_{userData.UserId}";
        
        // 사용자별 볼륨 설정 등을 적용할 수 있음
        Debug.Log($"[{SectionType}Section] Applied user-specific settings for {userData.DisplayName}");
    }
    
    private void CheckUserSpecificNotifications(UserData userData)
    {
        if (userData == null) return;
        
        // 사용자별 알림 확인
        if (userData.IsNewUser)
        {
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Info,
                Title = "환영합니다!",
                Message = "게임 설정을 확인하고 개인화해보세요.",
                Priority = NotificationPriority.Normal,
                AutoDismiss = true,
                DismissAfter = 10f
            });
        }
        
        // 레벨업 시 새로운 설정 옵션 알림
        if (userData.Level >= 5)
        {
            CheckAdvancedSettingsNotification();
        }
    }
    
    private void CheckAdvancedSettingsNotification()
    {
        bool hasShownAdvancedNotification = PlayerPrefs.GetInt("shown_advanced_settings", 0) == 1;
        
        if (!hasShownAdvancedNotification)
        {
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Info,
                Title = "새로운 기능!",
                Message = "레벨 5 달성으로 고급 설정이 해제되었습니다.",
                Priority = NotificationPriority.High,
                AutoDismiss = false
            });
            
            PlayerPrefs.SetInt("shown_advanced_settings", 1);
            PlayerPrefs.Save();
        }
    }
    #endregion

    #region Logout Management
    public void InitiateLogout()
    {
        if (_isLogoutInProgress)
        {
            Debug.LogWarning($"[{SectionType}Section] Logout already in progress");
            return;
        }
        
        StartCoroutine(LogoutSequence());
    }
    
    private IEnumerator LogoutSequence()
    {
        _isLogoutInProgress = true;
        
        try
        {
            Debug.Log($"[{SectionType}Section] Starting logout sequence...");
            
            // 1. 현재 설정 저장
            SaveCurrentSettings();
            yield return new WaitForSeconds(0.5f);
            
            // 2. 다른 섹션들에게 로그아웃 예정 알림
            BroadcastToAllSections(new LogoutMessage
            {
                Phase = LogoutPhase.Starting,
                EstimatedTime = 3f
            });
            yield return new WaitForSeconds(0.5f);
            
            // 3. 사용자 데이터 저장 대기
            if (_userDataManager?.CurrentUser != null)
            {
                Debug.Log($"[{SectionType}Section] Saving user data before logout...");
                // UserDataManager에 저장 요청
                yield return new WaitForSeconds(1f);
            }
            
            // 4. 로그아웃 확인 알림
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Info,
                Title = "로그아웃 중...",
                Message = "데이터를 저장하고 있습니다.",
                Priority = NotificationPriority.High,
                AutoDismiss = true,
                DismissAfter = 2f
            });
            yield return new WaitForSeconds(1f);
            
            // 5. AuthenticationManager를 통한 실제 로그아웃
            Debug.Log($"[{SectionType}Section] Executing logout...");
            RequestLogout();
            
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SectionType}Section] Logout sequence failed: {e.Message}");
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Error,
                Title = "로그아웃 실패",
                Message = "로그아웃 중 문제가 발생했습니다.",
                Priority = NotificationPriority.High,
                AutoDismiss = false
            });
        }
        finally
        {
            _isLogoutInProgress = false;
        }
    }
    #endregion

    #region Notification System
    public void AddNotification(SettingsNotification notification)
    {
        if (notification == null) return;
        
        notification.Id = Guid.NewGuid().ToString();
        notification.Timestamp = DateTime.Now;
        
        _notificationQueue.Enqueue(notification);
        
        // SettingsSectionUIComponent에 알림 전달
        if (_settingsUI != null)
        {
            var uiNotification = ConvertToUINotification(notification);
            _settingsUI.AddNotification(uiNotification);
        }
        
        Debug.Log($"[{SectionType}Section] Added notification: {notification.Title}");
    }
    
    private NotificationMessage ConvertToUINotification(SettingsNotification settingsNotification)
    {
        return new NotificationMessage
        {
            Message = $"{settingsNotification.Title}\n{settingsNotification.Message}",
            Type = settingsNotification.Type,
            AutoCloseDelay = settingsNotification.AutoDismiss ? settingsNotification.DismissAfter : 0f
        };
    }
    
    private void ProcessPendingNotifications()
    {
        int processedCount = 0;
        while (_notificationQueue.Count > 0)
        {
            var notification = _notificationQueue.Dequeue();
            
            // UI에 알림 표시
            if (_settingsUI != null)
            {
                var uiNotification = ConvertToUINotification(notification);
                _settingsUI.AddNotification(uiNotification);
                processedCount++;
            }
        }
        
        if (processedCount > 0)
        {
            Debug.Log($"[{SectionType}Section] Processed {processedCount} pending notifications");
        }
    }
    #endregion

    #region Message Handling
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[{SectionType}Section] Received message from {fromSection}: {data?.GetType().Name}");
        
        switch (data)
        {
            case SettingsRequest settingsRequest:
                HandleSettingsRequest(fromSection, settingsRequest);
                break;
                
            case ProfileUpdateMessage profileUpdate:
                HandleProfileUpdate(profileUpdate);
                break;
                
            case EnergyChangeMessage energyChange:
                HandleEnergyChange(energyChange);
                break;
                
            case MatchResultMessage matchResult:
                HandleMatchResult(matchResult);
                break;
                
            case string message when message == "request_settings_sync":
                SyncSettingsWithServer();
                break;
                
            default:
                Debug.Log($"[{SectionType}Section] Unhandled message type from {fromSection}");
                break;
        }
    }
    
    private void HandleSettingsRequest(MainPageSectionType fromSection, SettingsRequest request)
    {
        switch (request.RequestType)
        {
            case "get_setting":
                RespondWithSetting(fromSection, request);
                break;
                
            case "bulk_settings":
                RespondWithAllSettings(fromSection);
                break;
                
            case "reset_to_defaults":
                ResetSettingsToDefaults();
                break;
                
            default:
                Debug.LogWarning($"[{SectionType}Section] Unknown settings request: {request.RequestType}");
                break;
        }
    }
    
    private void HandleProfileUpdate(ProfileUpdateMessage profileUpdate)
    {
        // 프로필 업데이트에 따른 설정 조정
        if (profileUpdate.LevelChanged)
        {
            CheckAdvancedSettingsNotification();
        }
        
        Debug.Log($"[{SectionType}Section] Handled profile update for level {profileUpdate.NewLevel}");
    }
    
    private void HandleEnergyChange(EnergyChangeMessage energyChange)
    {
        // 에너지 변화에 따른 알림 설정
        if (energyChange.Energy <= 10)
        {
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Warning,
                Title = "에너지 부족",
                Message = "에너지 알림 설정을 확인해보세요.",
                Priority = NotificationPriority.Normal,
                AutoDismiss = true,
                DismissAfter = 5f
            });
        }
    }
    
    private void HandleMatchResult(MatchResultMessage matchResult)
    {
        // 매치 결과에 따른 설정 추천
        if (matchResult.IsVictory && matchResult.PerformanceRating > 8.0f)
        {
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Success,
                Title = "훌륭한 경기!",
                Message = "현재 설정이 좋은 결과를 만들고 있습니다.",
                Priority = NotificationPriority.Low,
                AutoDismiss = true,
                DismissAfter = 8f
            });
        }
    }
    
    private void RespondWithSetting(MainPageSectionType fromSection, SettingsRequest request)
    {
        if (!string.IsNullOrEmpty(request.SettingName))
        {
            var value = GetSetting<object>(request.SettingName);
            
            SendMessageToSection(fromSection, new SettingsResponse
            {
                SettingName = request.SettingName,
                Value = value,
                Success = value != null
            });
        }
    }
    
    private void RespondWithAllSettings(MainPageSectionType fromSection)
    {
        var allSettings = new Dictionary<string, object>(_settingsCache);
        
        SendMessageToSection(fromSection, new BulkSettingsResponse
        {
            Settings = allSettings,
            Count = allSettings.Count,
            Success = true
        });
    }
    
    private void ResetSettingsToDefaults()
    {
        Debug.Log($"[{SectionType}Section] Resetting all settings to defaults...");
        
        // 설정 초기화 실행
        foreach (string settingKey in _criticalSettings)
        {
            ResetSettingToDefault(settingKey);
        }
        
        // UI 새로고침
        RefreshSettingsFromCache();
        
        // 알림 추가
        AddNotification(new SettingsNotification
        {
            Type = NotificationType.Info,
            Title = "설정 초기화 완료",
            Message = "모든 설정이 기본값으로 초기화되었습니다.",
            Priority = NotificationPriority.Normal,
            AutoDismiss = true,
            DismissAfter = 3f
        });
        
        // 다른 섹션들에게 알림
        BroadcastToAllSections(new SettingsResetMessage
        {
            ResetTime = DateTime.Now,
            ResettedSettingsCount = _criticalSettings.Length
        });
    }
    
    private void ResetSettingToDefault(string settingName)
    {
        object defaultValue = settingName switch
        {
            "MasterVolume" => 1.0f,
            "MusicVolume" => 0.8f,
            "SfxVolume" => 0.9f,
            "IsMuted" => false,
            "IsFullscreen" => false,
            "QualityLevel" => 2,
            "VibrationEnabled" => true,
            "Brightness" => 0.8f,
            _ => null
        };
        
        if (defaultValue != null)
        {
            _settingsCache[settingName] = defaultValue;
            SetSetting(settingName, defaultValue);
        }
    }
    #endregion

    #region Override Methods
    protected override void OnSettingUpdated(string settingName, object newValue)
    {
        // 설정이 외부에서 변경될 때 캐시 업데이트
        _settingsCache[settingName] = newValue;
        ApplySettingToUI(settingName, newValue);
        
        Debug.Log($"[{SectionType}Section] Setting updated in cache: {settingName} = {newValue}");
    }
    
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        // 오프라인 모드 변경 시 UI 업데이트
        if (_settingsUI != null)
        {
            SafeUIUpdate(() =>
            {
                // 오프라인 모드에서는 서버 동기화 관련 UI 비활성화
                // _settingsUI에 해당 메서드가 있다고 가정
                Debug.Log($"[{SectionType}Section] Offline mode changed: {isOfflineMode}");
            }, "Offline Mode Change");
        }
        
        if (isOfflineMode)
        {
            AddNotification(new SettingsNotification
            {
                Type = NotificationType.Warning,
                Title = "오프라인 모드",
                Message = "일부 설정 동기화가 제한됩니다.",
                Priority = NotificationPriority.Normal,
                AutoDismiss = true,
                DismissAfter = 5f
            });
        }
    }
    
    protected override void OnForceRefresh()
    {
        InitializeSettingsCache();
        RefreshSettingsFromCache();
        ProcessPendingNotifications();
        
        Debug.Log($"[{SectionType}Section] Force refresh completed");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 특정 설정 값 가져오기 (캐시에서)
    /// </summary>
    public T GetCachedSetting<T>(string settingName)
    {
        if (_settingsCache.TryGetValue(settingName, out var value) && value is T)
        {
            return (T)value;
        }
        
        // 캐시에 없으면 기본 GetSetting 사용
        return GetSetting<T>(settingName);
    }
    
    /// <summary>
    /// 설정 값 설정 및 캐시 업데이트
    /// </summary>
    public bool SetCachedSetting<T>(string settingName, T value)
    {
        bool success = SetSetting(settingName, value);
        
        if (success)
        {
            _settingsCache[settingName] = value;
            ApplySettingToUI(settingName, value);
        }
        
        return success;
    }
    
    /// <summary>
    /// 즉시 로그아웃 실행
    /// </summary>
    public void ForceLogout()
    {
        StopAllCoroutines();
        _isLogoutInProgress = false;
        
        RequestLogout();
        Debug.Log($"[{SectionType}Section] Force logout executed");
    }
    
    /// <summary>
    /// 현재 알림 큐 크기
    /// </summary>
    public int PendingNotificationsCount => _notificationQueue.Count;
    #endregion
}

#region Data Classes
/// <summary>
/// 설정 섹션 전용 알림 데이터
/// </summary>
[Serializable]
public class SettingsNotification
{
    public string Id;
    public NotificationType Type;
    public string Title;
    public string Message;
    public NotificationPriority Priority;
    public bool AutoDismiss;
    public float DismissAfter;
    public DateTime Timestamp;
}

/// <summary>
/// 설정 요청 메시지
/// </summary>
[Serializable]
public class SettingsRequest
{
    public string RequestType;
    public string SettingName;
    public object Value;
    public MainPageSectionType FromSection;
}

/// <summary>
/// 도움말 요청 메시지
/// </summary>
[Serializable]
public class HelpRequest
{
    public string RequestType;
    public string Topic;
    public MainPageSectionType FromSection;
}

/// <summary>
/// 설정 응답 메시지
/// </summary>
[Serializable]
public class SettingsResponse
{
    public string SettingName;
    public object Value;
    public bool Success;
    public string ErrorMessage;
}

/// <summary>
/// 대량 설정 응답 메시지
/// </summary>
[Serializable]
public class BulkSettingsResponse
{
    public Dictionary<string, object> Settings;
    public int Count;
    public bool Success;
}

/// <summary>
/// 설정 동기화 메시지
/// </summary>
[Serializable]
public class SettingsSyncMessage
{
    public DateTime SyncTime;
    public int SettingsCount;
    public bool Success;
    public string ErrorMessage;
}

/// <summary>
/// 설정 초기화 메시지
/// </summary>
[Serializable]
public class SettingsResetMessage
{
    public DateTime ResetTime;
    public int ResettedSettingsCount;
}

/// <summary>
/// 로그아웃 메시지
/// </summary>
[Serializable]
public class LogoutMessage
{
    public LogoutPhase Phase;
    public float EstimatedTime;
    public string Message;
}

/// <summary>
/// 프로필 업데이트 메시지 (다른 섹션에서 받는 용도)
/// </summary>
[Serializable]
public class ProfileUpdateMessage
{
    public string UserId;
    public int NewLevel;
    public int OldLevel;
    public bool LevelChanged;
    public DateTime UpdateTime;
}

/// <summary>
/// 에너지 변화 메시지 (다른 섹션에서 받는 용도)
/// </summary>
[Serializable]
public class EnergyChangeMessage
{
    public int Energy;
    public int MaxEnergy;
    public int Delta;
    public string Reason;
}

/// <summary>
/// 매치 결과 메시지 (다른 섹션에서 받는 용도)
/// </summary>
[Serializable]
public class MatchResultMessage
{
    public bool IsVictory;
    public float PerformanceRating;
    public int ScoreEarned;
    public DateTime MatchTime;
}

/// <summary>
/// 알림 우선순위
/// </summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// 로그아웃 단계
/// </summary>
public enum LogoutPhase
{
    Starting,
    SavingData,
    CleaningUp,
    Complete,
    Failed
}
#endregion