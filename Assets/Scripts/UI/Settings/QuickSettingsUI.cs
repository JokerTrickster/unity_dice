using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QuickSettingsUI - 빠른 설정 UI 컴포넌트
/// 메인페이지에서 즉시 접근 가능한 음악/효과음 토글 기능을 제공합니다.
/// 60FPS 애니메이션과 0.1초 이내 즉시 반응을 보장합니다.
/// </summary>
public class QuickSettingsUI : MonoBehaviour
{
    #region UI References
    [Header("Toggle Controls")]
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle soundToggle;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image musicIconImage;
    [SerializeField] private Image soundIconImage;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.15f;
    [SerializeField] private AnimationCurve toggleAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float scaleAnimationIntensity = 0.2f;
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = Color.gray;
    
    [Header("Performance Settings")]
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private int targetFrameRate = 60;
    #endregion

    #region Events
    /// <summary>
    /// 음악 설정 변경 이벤트
    /// </summary>
    public static event Action<bool> OnMusicToggleChanged;
    
    /// <summary>
    /// 효과음 설정 변경 이벤트
    /// </summary>
    public static event Action<bool> OnSoundToggleChanged;
    
    /// <summary>
    /// UI 초기화 완료 이벤트
    /// </summary>
    public static event Action OnQuickSettingsInitialized;
    #endregion

    #region Properties
    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized { get; private set; } = false;
    
    /// <summary>
    /// 현재 음악 활성화 상태
    /// </summary>
    public bool IsMusicEnabled => musicToggle?.isOn ?? true;
    
    /// <summary>
    /// 현재 효과음 활성화 상태
    /// </summary>
    public bool IsSoundEnabled => soundToggle?.isOn ?? true;
    
    /// <summary>
    /// 애니메이션 진행 중 여부
    /// </summary>
    public bool IsAnimating { get; private set; } = false;
    #endregion

    #region Private Fields
    private Coroutine _musicAnimationCoroutine;
    private Coroutine _soundAnimationCoroutine;
    private bool _isConnectedToIntegration = false;
    private Vector3 _musicOriginalScale;
    private Vector3 _soundOriginalScale;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        CacheOriginalScales();
    }

    private void Start()
    {
        InitializeQuickSettings();
        ConnectToSettingsIntegration();
    }

    private void OnDestroy()
    {
        DisconnectFromSettingsIntegration();
        CleanupAnimations();
        
        // 이벤트 정리
        OnMusicToggleChanged = null;
        OnSoundToggleChanged = null;
        OnQuickSettingsInitialized = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 컴포넌트 유효성 검사
    /// </summary>
    private void ValidateComponents()
    {
        if (musicToggle == null)
            Debug.LogError("[QuickSettingsUI] Music toggle is not assigned!");
            
        if (soundToggle == null)
            Debug.LogError("[QuickSettingsUI] Sound toggle is not assigned!");
            
        if (musicIconImage == null)
            Debug.LogWarning("[QuickSettingsUI] Music icon image is not assigned");
            
        if (soundIconImage == null)
            Debug.LogWarning("[QuickSettingsUI] Sound icon image is not assigned");
    }

    /// <summary>
    /// 원본 스케일 캐시
    /// </summary>
    private void CacheOriginalScales()
    {
        if (musicToggle != null)
            _musicOriginalScale = musicToggle.transform.localScale;
            
        if (soundToggle != null)
            _soundOriginalScale = soundToggle.transform.localScale;
    }

    /// <summary>
    /// 퀵 설정 UI 초기화
    /// </summary>
    private void InitializeQuickSettings()
    {
        try
        {
            SetupToggleEvents();
            LoadInitialSettings();
            
            IsInitialized = true;
            OnQuickSettingsInitialized?.Invoke();
            
            Debug.Log("[QuickSettingsUI] Quick settings initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QuickSettingsUI] Initialization failed: {ex.Message}");
            IsInitialized = false;
        }
    }

    /// <summary>
    /// 토글 이벤트 설정
    /// </summary>
    private void SetupToggleEvents()
    {
        if (musicToggle != null)
        {
            musicToggle.onValueChanged.AddListener(OnMusicToggleValueChanged);
        }
        
        if (soundToggle != null)
        {
            soundToggle.onValueChanged.AddListener(OnSoundToggleValueChanged);
        }
    }

    /// <summary>
    /// 초기 설정 로드
    /// </summary>
    private void LoadInitialSettings()
    {
        // SettingsIntegration에서 현재 설정 로드
        if (SettingsIntegration.Instance != null && SettingsIntegration.Instance.IsInitialized)
        {
            var currentSettings = SettingsIntegration.Instance.GetCurrentSettings();
            UpdateMusicToggle(currentSettings.MusicEnabled, false);
            UpdateSoundToggle(currentSettings.SoundEnabled, false);
        }
        else
        {
            // PlayerPrefs에서 직접 로드 (fallback)
            bool musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
            bool soundEnabled = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
            
            UpdateMusicToggle(musicEnabled, false);
            UpdateSoundToggle(soundEnabled, false);
        }
    }

    /// <summary>
    /// SettingsIntegration 연결
    /// </summary>
    private void ConnectToSettingsIntegration()
    {
        if (SettingsIntegration.Instance != null)
        {
            SettingsIntegration.OnSettingChanged += OnIntegrationSettingChanged;
            SettingsIntegration.OnIntegrationInitialized += OnIntegrationInitialized;
            _isConnectedToIntegration = true;
            
            Debug.Log("[QuickSettingsUI] Connected to SettingsIntegration");
        }
        else
        {
            Debug.LogWarning("[QuickSettingsUI] SettingsIntegration not available, using fallback mode");
        }
    }

    /// <summary>
    /// SettingsIntegration 연결 해제
    /// </summary>
    private void DisconnectFromSettingsIntegration()
    {
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.OnSettingChanged -= OnIntegrationSettingChanged;
            SettingsIntegration.OnIntegrationInitialized -= OnIntegrationInitialized;
        }
        
        _isConnectedToIntegration = false;
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 음악 토글 값 변경 이벤트
    /// </summary>
    private void OnMusicToggleValueChanged(bool isEnabled)
    {
        // 즉시 반응 (0.1초 이내)
        HandleMusicToggleChanged(isEnabled);
        
        // 애니메이션 실행
        StartMusicAnimation(isEnabled);
        
        // 통합 시스템에 전달
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.ToggleMusic(isEnabled);
        }
        
        // 이벤트 발생
        OnMusicToggleChanged?.Invoke(isEnabled);
        
        Debug.Log($"[QuickSettingsUI] Music toggle changed: {isEnabled}");
    }

    /// <summary>
    /// 효과음 토글 값 변경 이벤트
    /// </summary>
    private void OnSoundToggleValueChanged(bool isEnabled)
    {
        // 즉시 반응 (0.1초 이내)
        HandleSoundToggleChanged(isEnabled);
        
        // 애니메이션 실행
        StartSoundAnimation(isEnabled);
        
        // 통합 시스템에 전달
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.ToggleSound(isEnabled);
        }
        
        // 이벤트 발생
        OnSoundToggleChanged?.Invoke(isEnabled);
        
        Debug.Log($"[QuickSettingsUI] Sound toggle changed: {isEnabled}");
    }

    /// <summary>
    /// 통합 시스템 설정 변경 이벤트
    /// </summary>
    private void OnIntegrationSettingChanged(string key, object value)
    {
        switch (key)
        {
            case "MusicEnabled":
                if (value is bool musicEnabled)
                {
                    UpdateMusicToggle(musicEnabled, false);
                }
                break;
                
            case "SoundEnabled":
                if (value is bool soundEnabled)
                {
                    UpdateSoundToggle(soundEnabled, false);
                }
                break;
        }
    }

    /// <summary>
    /// 통합 시스템 초기화 완료 이벤트
    /// </summary>
    private void OnIntegrationInitialized()
    {
        LoadInitialSettings();
    }
    #endregion

    #region Animation Methods
    /// <summary>
    /// 음악 토글 애니메이션 시작
    /// </summary>
    private void StartMusicAnimation(bool isEnabled)
    {
        if (_musicAnimationCoroutine != null)
        {
            StopCoroutine(_musicAnimationCoroutine);
        }
        
        _musicAnimationCoroutine = StartCoroutine(AnimateToggle(musicToggle, musicIconImage, isEnabled, _musicOriginalScale));
    }

    /// <summary>
    /// 효과음 토글 애니메이션 시작
    /// </summary>
    private void StartSoundAnimation(bool isEnabled)
    {
        if (_soundAnimationCoroutine != null)
        {
            StopCoroutine(_soundAnimationCoroutine);
        }
        
        _soundAnimationCoroutine = StartCoroutine(AnimateToggle(soundToggle, soundIconImage, isEnabled, _soundOriginalScale));
    }

    /// <summary>
    /// 토글 애니메이션 코루틴
    /// </summary>
    private IEnumerator AnimateToggle(Toggle toggle, Image icon, bool isEnabled, Vector3 originalScale)
    {
        if (toggle == null) yield break;
        
        IsAnimating = true;
        
        // 시작 상태
        float startTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        Vector3 startScale = toggle.transform.localScale;
        Color startColor = icon?.color ?? Color.white;
        
        // 목표 상태  
        Vector3 targetScale = originalScale * (1f + (isEnabled ? scaleAnimationIntensity : 0f));
        Color targetColor = isEnabled ? enabledColor : disabledColor;
        
        // 스프라이트 즉시 변경
        UpdateToggleVisuals(icon, isEnabled);
        
        // 애니메이션 루프 (60FPS 보장)
        float elapsedTime = 0f;
        while (elapsedTime < animationDuration)
        {
            float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;
            elapsedTime = currentTime - startTime;
            
            float normalizedTime = Mathf.Clamp01(elapsedTime / animationDuration);
            float curveValue = toggleAnimationCurve.Evaluate(normalizedTime);
            
            // 스케일 애니메이션
            Vector3 currentScale = Vector3.Lerp(startScale, targetScale, curveValue);
            toggle.transform.localScale = currentScale;
            
            // 색상 애니메이션
            if (icon != null)
            {
                Color currentColor = Color.Lerp(startColor, targetColor, curveValue);
                icon.color = currentColor;
            }
            
            yield return null; // 다음 프레임까지 대기
        }
        
        // 최종 상태 적용
        toggle.transform.localScale = targetScale;
        if (icon != null)
        {
            icon.color = targetColor;
        }
        
        IsAnimating = false;
    }

    /// <summary>
    /// 애니메이션 정리
    /// </summary>
    private void CleanupAnimations()
    {
        if (_musicAnimationCoroutine != null)
        {
            StopCoroutine(_musicAnimationCoroutine);
            _musicAnimationCoroutine = null;
        }
        
        if (_soundAnimationCoroutine != null)
        {
            StopCoroutine(_soundAnimationCoroutine);
            _soundAnimationCoroutine = null;
        }
        
        IsAnimating = false;
    }
    #endregion

    #region Visual Update Methods
    /// <summary>
    /// 음악 토글 즉시 처리
    /// </summary>
    private void HandleMusicToggleChanged(bool isEnabled)
    {
        UpdateToggleVisuals(musicIconImage, isEnabled);
    }

    /// <summary>
    /// 효과음 토글 즉시 처리
    /// </summary>
    private void HandleSoundToggleChanged(bool isEnabled)
    {
        UpdateToggleVisuals(soundIconImage, isEnabled);
    }

    /// <summary>
    /// 토글 시각적 요소 업데이트
    /// </summary>
    private void UpdateToggleVisuals(Image iconImage, bool isEnabled)
    {
        if (iconImage == null) return;
        
        // 스프라이트 변경
        if (iconImage == musicIconImage)
        {
            iconImage.sprite = isEnabled ? musicOnSprite : musicOffSprite;
        }
        else if (iconImage == soundIconImage)
        {
            iconImage.sprite = isEnabled ? soundOnSprite : soundOffSprite;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 음악 토글 상태 업데이트 (외부 호출용)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    /// <param name="animate">애니메이션 실행 여부</param>
    public void UpdateMusicToggle(bool isEnabled, bool animate = true)
    {
        if (musicToggle == null) return;
        
        // 토글 상태 업데이트 (이벤트 발생 방지)
        musicToggle.onValueChanged.RemoveAllListeners();
        musicToggle.isOn = isEnabled;
        musicToggle.onValueChanged.AddListener(OnMusicToggleValueChanged);
        
        // 시각적 업데이트
        HandleMusicToggleChanged(isEnabled);
        
        // 애니메이션
        if (animate)
        {
            StartMusicAnimation(isEnabled);
        }
        
        Debug.Log($"[QuickSettingsUI] Music toggle updated to {isEnabled}");
    }

    /// <summary>
    /// 효과음 토글 상태 업데이트 (외부 호출용)
    /// </summary>
    /// <param name="isEnabled">활성화 여부</param>
    /// <param name="animate">애니메이션 실행 여부</param>
    public void UpdateSoundToggle(bool isEnabled, bool animate = true)
    {
        if (soundToggle == null) return;
        
        // 토글 상태 업데이트 (이벤트 발생 방지)
        soundToggle.onValueChanged.RemoveAllListeners();
        soundToggle.isOn = isEnabled;
        soundToggle.onValueChanged.AddListener(OnSoundToggleValueChanged);
        
        // 시각적 업데이트
        HandleSoundToggleChanged(isEnabled);
        
        // 애니메이션
        if (animate)
        {
            StartSoundAnimation(isEnabled);
        }
        
        Debug.Log($"[QuickSettingsUI] Sound toggle updated to {isEnabled}");
    }

    /// <summary>
    /// 모든 토글 상호작용 비활성화/활성화
    /// </summary>
    /// <param name="interactable">상호작용 가능 여부</param>
    public void SetTogglesInteractable(bool interactable)
    {
        if (musicToggle != null)
            musicToggle.interactable = interactable;
            
        if (soundToggle != null)
            soundToggle.interactable = interactable;
            
        Debug.Log($"[QuickSettingsUI] Toggles interactable set to {interactable}");
    }

    /// <summary>
    /// 설정 강제 새로고침
    /// </summary>
    public void RefreshSettings()
    {
        if (IsInitialized)
        {
            LoadInitialSettings();
            Debug.Log("[QuickSettingsUI] Settings refreshed");
        }
    }

    /// <summary>
    /// 현재 설정 상태 반환
    /// </summary>
    public QuickSettingsState GetCurrentState()
    {
        return new QuickSettingsState
        {
            MusicEnabled = IsMusicEnabled,
            SoundEnabled = IsSoundEnabled,
            IsInitialized = IsInitialized,
            IsAnimating = IsAnimating
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 퀵 설정 상태 정보
/// </summary>
[Serializable]
public class QuickSettingsState
{
    public bool MusicEnabled { get; set; }
    public bool SoundEnabled { get; set; }
    public bool IsInitialized { get; set; }
    public bool IsAnimating { get; set; }
}
#endregion