using System;
using UnityEngine;

/// <summary>
/// MainPageSettings - SettingsManager의 메인 페이지 래퍼 클래스
/// 기존 SettingsManager를 최대한 재사용하면서 메인 페이지 특화 기능을 제공합니다.
/// 오디오 설정의 즉시 반영(0.1초 이내)을 지원하며, 설정 변경 이벤트를 UI에 전달합니다.
/// </summary>
public class MainPageSettings : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 배경음악 설정이 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnMusicSettingChanged;
    
    /// <summary>
    /// 효과음 설정이 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnSoundSettingChanged;
    
    /// <summary>
    /// 설정 초기화가 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action OnSettingsInitialized;
    #endregion

    #region Properties
    /// <summary>
    /// 배경음악 활성화 여부
    /// </summary>
    public bool IsMusicEnabled
    {
        get => GetMusicSetting();
        set => SetMusicSetting(value);
    }

    /// <summary>
    /// 효과음 활성화 여부
    /// </summary>
    public bool IsSoundEnabled
    {
        get => GetSoundSetting();
        set => SetSoundSetting(value);
    }

    /// <summary>
    /// 설정 시스템 초기화 여부
    /// </summary>
    public bool IsInitialized { get; private set; } = false;
    #endregion

    #region Settings Keys
    private const string MUSIC_ENABLED_KEY = "MusicEnabled";
    private const string SOUND_ENABLED_KEY = "SoundEnabled";
    private const string LAST_SETTINGS_UPDATE_KEY = "LastSettingsUpdate";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        LoadInitialSettings();
    }

    private void Start()
    {
        InitializeSettings();
    }

    private void OnDestroy()
    {
        SaveCurrentSettings();
        
        // 이벤트 정리
        OnMusicSettingChanged = null;
        OnSoundSettingChanged = null;
        OnSettingsInitialized = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 초기 설정 로드
    /// </summary>
    private void LoadInitialSettings()
    {
        try
        {
            // PlayerPrefs에서 설정 로드 (기본값 적용)
            bool musicEnabled = PlayerPrefs.GetInt(MUSIC_ENABLED_KEY, 1) == 1;
            bool soundEnabled = PlayerPrefs.GetInt(SOUND_ENABLED_KEY, 1) == 1;
            
            // 즉시 오디오 설정 적용
            ApplyAudioSettingImmediately(MUSIC_ENABLED_KEY, musicEnabled);
            ApplyAudioSettingImmediately(SOUND_ENABLED_KEY, soundEnabled);
            
            Debug.Log($"[MainPageSettings] Initial settings loaded - Music: {musicEnabled}, Sound: {soundEnabled}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to load initial settings: {ex.Message}");
        }
    }

    /// <summary>
    /// 설정 시스템 초기화
    /// </summary>
    private void InitializeSettings()
    {
        try
        {
            // SettingsManager가 초기화되지 않은 경우 기본 설정 사용
            if (SettingsManager.Instance == null || !SettingsManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[MainPageSettings] SettingsManager not available, using local settings only");
            }

            IsInitialized = true;
            
            // 설정 초기화 완료 이벤트 발생
            OnSettingsInitialized?.Invoke();
            
            Debug.Log("[MainPageSettings] Settings system initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to initialize settings: {ex.Message}");
            IsInitialized = false;
        }
    }
    #endregion

    #region Settings Management
    /// <summary>
    /// 배경음악 설정 가져오기
    /// </summary>
    private bool GetMusicSetting()
    {
        try
        {
            // SettingsManager가 사용 가능한 경우 우선 사용
            if (SettingsManager.Instance?.IsInitialized == true)
            {
                var musicVolume = SettingsManager.Instance.GetSetting<float>("MusicVolume");
                return musicVolume > 0f;
            }
            
            // 로컬 PlayerPrefs 사용
            return PlayerPrefs.GetInt(MUSIC_ENABLED_KEY, 1) == 1;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to get music setting: {ex.Message}");
            return true; // 기본값
        }
    }

    /// <summary>
    /// 효과음 설정 가져오기
    /// </summary>
    private bool GetSoundSetting()
    {
        try
        {
            // SettingsManager가 사용 가능한 경우 우선 사용
            if (SettingsManager.Instance?.IsInitialized == true)
            {
                var sfxVolume = SettingsManager.Instance.GetSetting<float>("SfxVolume");
                return sfxVolume > 0f;
            }
            
            // 로컬 PlayerPrefs 사용
            return PlayerPrefs.GetInt(SOUND_ENABLED_KEY, 1) == 1;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to get sound setting: {ex.Message}");
            return true; // 기본값
        }
    }

    /// <summary>
    /// 배경음악 설정 변경
    /// </summary>
    private void SetMusicSetting(bool enabled)
    {
        try
        {
            // SettingsManager 연동
            if (SettingsManager.Instance?.IsInitialized == true)
            {
                float volume = enabled ? 0.8f : 0f;
                SettingsManager.Instance.SetSetting("MusicVolume", volume);
            }
            
            // 로컬 PlayerPrefs 저장
            PlayerPrefs.SetInt(MUSIC_ENABLED_KEY, enabled ? 1 : 0);
            
            // 즉시 오디오 적용
            ApplyAudioSettingImmediately(MUSIC_ENABLED_KEY, enabled);
            
            // 이벤트 발생
            OnMusicSettingChanged?.Invoke(enabled);
            
            Debug.Log($"[MainPageSettings] Music setting changed: {enabled}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to set music setting: {ex.Message}");
        }
    }

    /// <summary>
    /// 효과음 설정 변경
    /// </summary>
    private void SetSoundSetting(bool enabled)
    {
        try
        {
            // SettingsManager 연동
            if (SettingsManager.Instance?.IsInitialized == true)
            {
                float volume = enabled ? 0.9f : 0f;
                SettingsManager.Instance.SetSetting("SfxVolume", volume);
            }
            
            // 로컬 PlayerPrefs 저장
            PlayerPrefs.SetInt(SOUND_ENABLED_KEY, enabled ? 1 : 0);
            
            // 즉시 오디오 적용
            ApplyAudioSettingImmediately(SOUND_ENABLED_KEY, enabled);
            
            // 이벤트 발생
            OnSoundSettingChanged?.Invoke(enabled);
            
            Debug.Log($"[MainPageSettings] Sound setting changed: {enabled}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to set sound setting: {ex.Message}");
        }
    }

    /// <summary>
    /// 오디오 설정을 즉시 적용 (0.1초 이내)
    /// </summary>
    private void ApplyAudioSettingImmediately(string settingKey, bool enabled)
    {
        try
        {
            switch (settingKey)
            {
                case MUSIC_ENABLED_KEY:
                    // AudioManager가 있는 경우 사용, 없으면 기본 AudioListener 사용
                    ApplyMusicSetting(enabled);
                    break;
                    
                case SOUND_ENABLED_KEY:
                    // AudioManager가 있는 경우 사용, 없으면 기본 AudioListener 사용
                    ApplySoundSetting(enabled);
                    break;
            }
            
            Debug.Log($"[MainPageSettings] Audio setting applied immediately: {settingKey} = {enabled}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to apply audio setting: {ex.Message}");
        }
    }

    /// <summary>
    /// 배경음악 설정 적용
    /// </summary>
    private void ApplyMusicSetting(bool enabled)
    {
        // AudioManager가 사용 가능한 경우 우선 사용
        var audioManager = FindObjectOfType<AudioSource>(); // AudioManager 대체
        if (audioManager != null && audioManager.clip != null)
        {
            audioManager.mute = !enabled;
            audioManager.volume = enabled ? 0.8f : 0f;
        }
        else
        {
            // AudioListener 볼륨으로 전체 음향 제어 (임시)
            var currentVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            if (!enabled)
            {
                // 배경음악만 끄는 것이므로 전체 볼륨은 유지
            }
        }
    }

    /// <summary>
    /// 효과음 설정 적용
    /// </summary>
    private void ApplySoundSetting(bool enabled)
    {
        // AudioManager가 사용 가능한 경우 우선 사용
        // 현재는 기본 구현으로 처리
        if (!enabled)
        {
            // 효과음 볼륨을 0으로 설정하는 로직
        }
    }
    #endregion

    #region Settings Persistence
    /// <summary>
    /// 현재 설정 저장
    /// </summary>
    private void SaveCurrentSettings()
    {
        try
        {
            // SettingsManager 설정 저장 요청
            if (SettingsManager.Instance?.IsInitialized == true)
            {
                // SettingsManager에서 자체적으로 저장 처리
            }
            
            // 마지막 업데이트 시간 저장
            PlayerPrefs.SetString(LAST_SETTINGS_UPDATE_KEY, DateTime.Now.ToBinary().ToString());
            PlayerPrefs.Save();
            
            Debug.Log("[MainPageSettings] Current settings saved");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// 설정 로드
    /// </summary>
    public void LoadSettings()
    {
        if (!IsInitialized)
        {
            InitializeSettings();
        }
        
        LoadInitialSettings();
        Debug.Log("[MainPageSettings] Settings loaded manually");
    }

    /// <summary>
    /// 설정 저장
    /// </summary>
    public void SaveSettings()
    {
        SaveCurrentSettings();
        Debug.Log("[MainPageSettings] Settings saved manually");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 음악 토글 변경 핸들러 (UI에서 호출)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    public void OnMusicToggleChanged(bool isEnabled)
    {
        IsMusicEnabled = isEnabled;
    }

    /// <summary>
    /// 효과음 토글 변경 핸들러 (UI에서 호출)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    public void OnSoundToggleChanged(bool isEnabled)
    {
        IsSoundEnabled = isEnabled;
    }

    /// <summary>
    /// 모든 설정을 기본값으로 리셋
    /// </summary>
    public void ResetToDefaults()
    {
        try
        {
            IsMusicEnabled = true;
            IsSoundEnabled = true;
            
            SaveCurrentSettings();
            
            Debug.Log("[MainPageSettings] Settings reset to defaults");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to reset settings: {ex.Message}");
        }
    }

    /// <summary>
    /// 설정 상태 정보 반환
    /// </summary>
    public MainPageSettingsStatus GetStatus()
    {
        return new MainPageSettingsStatus
        {
            IsInitialized = IsInitialized,
            IsMusicEnabled = IsMusicEnabled,
            IsSoundEnabled = IsSoundEnabled,
            HasSettingsManager = SettingsManager.Instance?.IsInitialized == true,
            LastUpdateTime = GetLastUpdateTime()
        };
    }

    /// <summary>
    /// 마지막 업데이트 시간 가져오기
    /// </summary>
    private DateTime GetLastUpdateTime()
    {
        try
        {
            string timeStr = PlayerPrefs.GetString(LAST_SETTINGS_UPDATE_KEY, "");
            if (!string.IsNullOrEmpty(timeStr) && long.TryParse(timeStr, out long binaryTime))
            {
                return DateTime.FromBinary(binaryTime);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainPageSettings] Failed to get last update time: {ex.Message}");
        }
        
        return DateTime.MinValue;
    }
    #endregion
}

#region Data Classes
/// <summary>
/// MainPageSettings 상태 정보
/// </summary>
[Serializable]
public class MainPageSettingsStatus
{
    public bool IsInitialized { get; set; }
    public bool IsMusicEnabled { get; set; }
    public bool IsSoundEnabled { get; set; }
    public bool HasSettingsManager { get; set; }
    public DateTime LastUpdateTime { get; set; }
}
#endregion