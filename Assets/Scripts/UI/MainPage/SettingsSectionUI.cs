using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 설정 섹션 순수 UI 컴포넌트
/// SettingsSection 비즈니스 로직과 분리된 UI 전용 컴포넌트입니다.
/// 사용자 입력을 이벤트로 전달하고 UI 업데이트만 담당합니다.
/// 하단 Footer 영역에 배치되어 빠른 접근을 제공합니다.
/// </summary>
public class SettingsSectionUI : MonoBehaviour
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
    
    [Header("UI Animation")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Events
    /// <summary>
    /// 설정 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnSettingsButtonClicked;
    
    /// <summary>
    /// 로그아웃 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnLogoutButtonClicked;
    
    /// <summary>
    /// 도움말 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnHelpButtonClicked;
    
    /// <summary>
    /// 알림 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnNotificationButtonClicked;
    
    /// <summary>
    /// 오디오 설정 변경 이벤트
    /// </summary>
    public static event Action<string, float> OnAudioSettingChanged;
    
    /// <summary>
    /// 토글 설정 변경 이벤트
    /// </summary>
    public static event Action<string, bool> OnToggleSettingChanged;
    
    /// <summary>
    /// 품질 설정 변경 이벤트
    /// </summary>
    public static event Action<int> OnQualitySettingChanged;
    
    /// <summary>
    /// 알림 닫기 이벤트
    /// </summary>
    public static event Action OnNotificationClosed;
    #endregion

    #region Private Fields
    private Queue<NotificationMessage> _notificationQueue = new Queue<NotificationMessage>();
    private bool _isShowingNotification = false;
    private bool _isInitialized = false;
    private Coroutine _currentAnimationCoroutine;
    
    // UI 상태 캐시
    private Dictionary<string, object> _uiStateCache = new Dictionary<string, object>();
    #endregion
    
    #region Public Properties
    /// <summary>
    /// UI 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 알림 표시 중 여부
    /// </summary>
    public bool IsShowingNotification => _isShowingNotification;
    
    /// <summary>
    /// 대기 중인 알림 수
    /// </summary>
    public int PendingNotificationCount => _notificationQueue.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
    }

    private void Start()
    {
        InitializeUI();
    }

    private void OnDestroy()
    {
        UnsubscribeFromUIEvents();
        StopAllCoroutines();
        
        // 이벤트 정리
        OnSettingsButtonClicked = null;
        OnLogoutButtonClicked = null;
        OnHelpButtonClicked = null;
        OnNotificationButtonClicked = null;
        OnAudioSettingChanged = null;
        OnToggleSettingChanged = null;
        OnQualitySettingChanged = null;
        OnNotificationClosed = null;
    }
    #endregion

    #region Initialization
    private void InitializeUI()
    {
        try
        {
            SetupUIEvents();
            PopulateQualityDropdown();
            SetupVersionInfo();
            InitializeUIState();
            
            _isInitialized = true;
            Debug.Log("[SettingsSectionUI] UI component initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsSectionUI] Initialization failed: {e.Message}");
            _isInitialized = false;
        }
    }

    private void ValidateComponents()
    {
        // 필수 컴포넌트 검증
        if (settingsButton == null)
            Debug.LogError("[SettingsSectionUI] Settings button is missing!");
            
        if (logoutButton == null)
            Debug.LogError("[SettingsSectionUI] Logout button is missing!");
            
        // 경고 레벨 컴포넌트
        if (masterVolumeSlider == null)
            Debug.LogWarning("[SettingsSectionUI] Master volume slider is not assigned");
            
        if (notificationPanel == null)
            Debug.LogWarning("[SettingsSectionUI] Notification panel is not assigned");
    }
    
    private void InitializeUIState()
    {
        // 초기 UI 상태 캐시
        _uiStateCache["masterVolume"] = masterVolumeSlider?.value ?? 1.0f;
        _uiStateCache["musicVolume"] = musicVolumeSlider?.value ?? 0.8f;
        _uiStateCache["sfxVolume"] = sfxVolumeSlider?.value ?? 0.9f;
        _uiStateCache["isMuted"] = muteToggle?.isOn ?? false;
        _uiStateCache["isFullscreen"] = fullscreenToggle?.isOn ?? false;
        _uiStateCache["qualityLevel"] = qualityDropdown?.value ?? 2;
        _uiStateCache["vibrationEnabled"] = vibrationToggle?.isOn ?? true;
        _uiStateCache["brightness"] = brightnessSlider?.value ?? 0.8f;
        
        Debug.Log("[SettingsSectionUI] UI state cache initialized");
    }
    #endregion

    #region UI Event Setup
    private void SetupUIEvents()
    {
        // Quick action buttons
        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => OnSettingsButtonClicked?.Invoke());
            
        if (logoutButton != null)
            logoutButton.onClick.AddListener(() => OnLogoutButtonClicked?.Invoke());
            
        if (helpButton != null)
            helpButton.onClick.AddListener(() => OnHelpButtonClicked?.Invoke());
            
        if (notificationButton != null)
            notificationButton.onClick.AddListener(() => OnNotificationButtonClicked?.Invoke());
            
        // Audio controls
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(value => HandleAudioSettingChanged("MasterVolume", value));
            
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(value => HandleAudioSettingChanged("MusicVolume", value));
            
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(value => HandleAudioSettingChanged("SfxVolume", value));
            
        if (muteToggle != null)
            muteToggle.onValueChanged.AddListener(value => HandleToggleSettingChanged("IsMuted", value));
            
        // Display settings
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(value => HandleToggleSettingChanged("IsFullscreen", value));
            
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(value => OnQualitySettingChanged?.Invoke(value));
            
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.AddListener(value => HandleToggleSettingChanged("VibrationEnabled", value));
            
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(value => HandleAudioSettingChanged("Brightness", value));
            
        // Notification panel
        if (closeNotificationButton != null)
            closeNotificationButton.onClick.AddListener(() => OnNotificationClosed?.Invoke());
            
        Debug.Log("[SettingsSectionUI] UI events setup complete");
    }

    private void UnsubscribeFromUIEvents()
    {
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
            
        if (logoutButton != null)
            logoutButton.onClick.RemoveAllListeners();
            
        if (helpButton != null)
            helpButton.onClick.RemoveAllListeners();
            
        if (notificationButton != null)
            notificationButton.onClick.RemoveAllListeners();
            
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            
        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveAllListeners();
            
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveAllListeners();
            
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.RemoveAllListeners();
            
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveAllListeners();
            
        if (closeNotificationButton != null)
            closeNotificationButton.onClick.RemoveAllListeners();
            
        Debug.Log("[SettingsSectionUI] UI events unsubscribed");
    }
    #endregion

    #region Event Handlers
    private void HandleAudioSettingChanged(string settingName, float value)
    {
        // UI 상태 캐시 업데이트
        _uiStateCache[settingName.ToLower()] = value;
        
        // 이벤트 발생
        OnAudioSettingChanged?.Invoke(settingName, value);
        
        Debug.Log($"[SettingsSectionUI] Audio setting changed: {settingName} = {value}");
    }

    private void HandleToggleSettingChanged(string settingName, bool value)
    {
        // UI 상태 캐시 업데이트
        _uiStateCache[settingName.ToLower()] = value;
        
        // 음소거 특별 처리
        if (settingName == "IsMuted")
        {
            UpdateAudioSlidersInteractable(!value);
        }
        
        // 이벤트 발생
        OnToggleSettingChanged?.Invoke(settingName, value);
        
        Debug.Log($"[SettingsSectionUI] Toggle setting changed: {settingName} = {value}");
    }
    
    private void UpdateAudioSlidersInteractable(bool interactable)
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.interactable = interactable;
        if (musicVolumeSlider != null)
            musicVolumeSlider.interactable = interactable;
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.interactable = interactable;
    }
    #endregion

    #region UI Updates (Public API for SettingsSection)
    /// <summary>
    /// 오디오 슬라이더 값 업데이트 (외부 호출용)
    /// </summary>
    public void UpdateAudioSlider(string settingName, float value, bool triggerEvent = false)
    {
        Slider targetSlider = settingName switch
        {
            "MasterVolume" => masterVolumeSlider,
            "MusicVolume" => musicVolumeSlider,
            "SfxVolume" => sfxVolumeSlider,
            "Brightness" => brightnessSlider,
            _ => null
        };
        
        if (targetSlider != null)
        {
            // 이벤트 발생 방지를 위해 임시 구독 해제
            if (!triggerEvent)
            {
                var currentValue = targetSlider.value;
                targetSlider.onValueChanged.RemoveAllListeners();
                targetSlider.value = value;
                
                // 이벤트 다시 등록
                if (settingName == "Brightness")
                    targetSlider.onValueChanged.AddListener(val => HandleAudioSettingChanged("Brightness", val));
                else
                    targetSlider.onValueChanged.AddListener(val => HandleAudioSettingChanged(settingName, val));
            }
            else
            {
                targetSlider.value = value;
            }
            
            _uiStateCache[settingName.ToLower()] = value;
            Debug.Log($"[SettingsSectionUI] Updated {settingName} slider to {value}");
        }
    }
    
    /// <summary>
    /// 토글 상태 업데이트 (외부 호출용)
    /// </summary>
    public void UpdateToggle(string settingName, bool value, bool triggerEvent = false)
    {
        Toggle targetToggle = settingName switch
        {
            "IsMuted" => muteToggle,
            "IsFullscreen" => fullscreenToggle,
            "VibrationEnabled" => vibrationToggle,
            _ => null
        };
        
        if (targetToggle != null)
        {
            // 이벤트 발생 방지를 위해 임시 구독 해제
            if (!triggerEvent)
            {
                targetToggle.onValueChanged.RemoveAllListeners();
                targetToggle.isOn = value;
                
                // 이벤트 다시 등록
                targetToggle.onValueChanged.AddListener(val => HandleToggleSettingChanged(settingName, val));
            }
            else
            {
                targetToggle.isOn = value;
            }
            
            _uiStateCache[settingName.ToLower()] = value;
            
            // 음소거 특별 처리
            if (settingName == "IsMuted")
            {
                UpdateAudioSlidersInteractable(!value);
            }
            
            Debug.Log($"[SettingsSectionUI] Updated {settingName} toggle to {value}");
        }
    }
    
    /// <summary>
    /// 품질 드롭다운 업데이트 (외부 호출용)
    /// </summary>
    public void UpdateQualityDropdown(int value, bool triggerEvent = false)
    {
        if (qualityDropdown != null)
        {
            // 이벤트 발생 방지를 위해 임시 구독 해제
            if (!triggerEvent)
            {
                qualityDropdown.onValueChanged.RemoveAllListeners();
                qualityDropdown.value = value;
                
                // 이벤트 다시 등록
                qualityDropdown.onValueChanged.AddListener(val => OnQualitySettingChanged?.Invoke(val));
            }
            else
            {
                qualityDropdown.value = value;
            }
            
            _uiStateCache["qualityLevel"] = value;
            Debug.Log($"[SettingsSectionUI] Updated quality dropdown to {value}");
        }
    }
    
    /// <summary>
    /// 버튼 활성화 상태 업데이트
    /// </summary>
    public void UpdateButtonInteractable(string buttonName, bool interactable)
    {
        Button targetButton = buttonName switch
        {
            "Settings" => settingsButton,
            "Logout" => logoutButton,
            "Help" => helpButton,
            "Notification" => notificationButton,
            _ => null
        };
        
        if (targetButton != null)
        {
            targetButton.interactable = interactable;
            Debug.Log($"[SettingsSectionUI] Updated {buttonName} button interactable to {interactable}");
        }
    }
    #endregion

    #region Notification System
    /// <summary>
    /// 알림 추가 (외부 호출용)
    /// </summary>
    public void AddNotification(NotificationMessage notification)
    {
        if (notification == null) return;
        
        _notificationQueue.Enqueue(notification);
        UpdateNotificationIndicator();
        
        if (!_isShowingNotification)
        {
            ShowNextNotification();
        }
        
        Debug.Log($"[SettingsSectionUI] Added notification: {notification.Message}");
    }
    
    /// <summary>
    /// 다음 알림 표시
    /// </summary>
    public void ShowNextNotification()
    {
        if (_notificationQueue.Count == 0 || _isShowingNotification) return;
        
        NotificationMessage notification = _notificationQueue.Dequeue();
        StartCoroutine(ShowNotificationCoroutine(notification));
    }
    
    private IEnumerator ShowNotificationCoroutine(NotificationMessage notification)
    {
        _isShowingNotification = true;
        
        if (notificationPanel != null)
        {
            // UI 업데이트
            if (notificationText != null)
                notificationText.text = notification.Message;
                
            if (notificationIcon != null && notification.IconSprite != null)
                notificationIcon.sprite = notification.IconSprite;
            
            // 패널 표시 애니메이션
            yield return StartCoroutine(AnimateNotificationPanel(true));
            
            // 자동 닫기 타이머
            if (notification.AutoCloseDelay > 0)
            {
                yield return new WaitForSeconds(notification.AutoCloseDelay);
                if (_isShowingNotification)
                {
                    HideNotificationPanel();
                }
            }
        }
    }
    
    /// <summary>
    /// 알림 패널 숨기기 (외부 호출용)
    /// </summary>
    public void HideNotificationPanel()
    {
        if (!_isShowingNotification) return;
        
        StartCoroutine(HideNotificationCoroutine());
    }
    
    private IEnumerator HideNotificationCoroutine()
    {
        if (notificationPanel != null)
        {
            yield return StartCoroutine(AnimateNotificationPanel(false));
        }
        
        _isShowingNotification = false;
        UpdateNotificationIndicator();
        
        // 다음 알림이 있으면 표시
        if (_notificationQueue.Count > 0)
        {
            yield return new WaitForSeconds(1f);
            ShowNextNotification();
        }
    }
    
    private IEnumerator AnimateNotificationPanel(bool show)
    {
        if (notificationPanel == null) yield break;
        
        if (_currentAnimationCoroutine != null)
        {
            StopCoroutine(_currentAnimationCoroutine);
        }
        
        float startScale = show ? 0f : 1f;
        float endScale = show ? 1f : 0f;
        float elapsedTime = 0f;
        
        notificationPanel.SetActive(true);
        notificationPanel.transform.localScale = Vector3.one * startScale;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / animationDuration;
            float animatedValue = animationCurve.Evaluate(normalizedTime);
            float currentScale = Mathf.Lerp(startScale, endScale, animatedValue);
            
            notificationPanel.transform.localScale = Vector3.one * currentScale;
            yield return null;
        }
        
        notificationPanel.transform.localScale = Vector3.one * endScale;
        
        if (!show)
        {
            notificationPanel.SetActive(false);
        }
    }
    
    private void UpdateNotificationIndicator()
    {
        if (newNotificationIndicator != null)
        {
            newNotificationIndicator.SetActive(_notificationQueue.Count > 0);
        }
    }
    
    /// <summary>
    /// 모든 알림 제거 (외부 호출용)
    /// </summary>
    public void ClearAllNotifications()
    {
        _notificationQueue.Clear();
        UpdateNotificationIndicator();
        
        if (_isShowingNotification)
        {
            HideNotificationPanel();
        }
        
        Debug.Log("[SettingsSectionUI] All notifications cleared");
    }
    #endregion

    #region Utility Methods
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

    private void SetupVersionInfo()
    {
        if (versionText != null)
        {
            versionText.text = $"v{Application.version}";
        }
    }
    
    /// <summary>
    /// 현재 UI 상태 가져오기
    /// </summary>
    public Dictionary<string, object> GetCurrentUIState()
    {
        return new Dictionary<string, object>(_uiStateCache);
    }
    
    /// <summary>
    /// UI 상태 일괄 업데이트
    /// </summary>
    public void UpdateUIState(Dictionary<string, object> newState, bool triggerEvents = false)
    {
        foreach (var kvp in newState)
        {
            switch (kvp.Key.ToLower())
            {
                case "mastervolume":
                case "musicvolume":
                case "sfxvolume":
                case "brightness":
                    if (kvp.Value is float floatValue)
                        UpdateAudioSlider(kvp.Key, floatValue, triggerEvents);
                    break;
                    
                case "ismuted":
                case "isfullscreen":
                case "vibrationenabled":
                    if (kvp.Value is bool boolValue)
                        UpdateToggle(kvp.Key, boolValue, triggerEvents);
                    break;
                    
                case "qualitylevel":
                    if (kvp.Value is int intValue)
                        UpdateQualityDropdown(intValue, triggerEvents);
                    break;
            }
        }
        
        Debug.Log($"[SettingsSectionUI] Updated UI state with {newState.Count} values");
    }
    #endregion
}

#region Data Classes (Reusing from SettingsSectionUIComponent)
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

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success,
    Confirmation
}
#endregion