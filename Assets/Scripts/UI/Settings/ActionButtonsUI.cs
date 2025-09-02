using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ActionButtonsUI - 액션 버튼 UI 컴포넌트
/// 로그아웃, 약관보기 등의 액션 버튼들과 진행상태 표시를 관리합니다.
/// SettingsIntegration과 연동하여 안전한 로그아웃 프로세스를 제공합니다.
/// </summary>
public class ActionButtonsUI : MonoBehaviour
{
    #region UI References
    [Header("Action Buttons")]
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button termsButton;
    [SerializeField] private Button privacyButton;
    [SerializeField] private Button mailboxButton;
    
    [Header("Button Labels")]
    [SerializeField] private Text logoutButtonText;
    [SerializeField] private Text termsButtonText;
    [SerializeField] private Text privacyButtonText;
    [SerializeField] private Text mailboxButtonText;
    
    [Header("Progress Indicators")]
    [SerializeField] private GameObject logoutProgressPanel;
    [SerializeField] private Slider logoutProgressSlider;
    [SerializeField] private Text logoutProgressText;
    [SerializeField] private Button cancelLogoutButton;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image logoutButtonImage;
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color disabledButtonColor = Color.gray;
    [SerializeField] private Color progressButtonColor = Color.yellow;
    
    [Header("Animation Settings")]
    [SerializeField] private float buttonAnimationDuration = 0.2f;
    [SerializeField] private AnimationCurve buttonPressAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float pressScaleMultiplier = 0.9f;
    
    [Header("Localization Keys")]
    [SerializeField] private string logoutTextKey = "settings.logout";
    [SerializeField] private string termsTextKey = "settings.terms";
    [SerializeField] private string privacyTextKey = "settings.privacy";
    [SerializeField] private string mailboxTextKey = "settings.mailbox";
    [SerializeField] private string loggingOutTextKey = "settings.loggingOut";
    [SerializeField] private string cancelTextKey = "settings.cancel";
    #endregion

    #region Events
    /// <summary>
    /// 로그아웃 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnLogoutButtonClicked;
    
    /// <summary>
    /// 약관 보기 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnTermsButtonClicked;
    
    /// <summary>
    /// 개인정보 처리방침 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnPrivacyButtonClicked;
    
    /// <summary>
    /// 우편함 버튼 클릭 이벤트
    /// </summary>
    public static event Action OnMailboxButtonClicked;
    
    /// <summary>
    /// 로그아웃 취소 이벤트
    /// </summary>
    public static event Action OnLogoutCancelRequested;
    
    /// <summary>
    /// UI 초기화 완료 이벤트
    /// </summary>
    public static event Action OnActionButtonsInitialized;
    #endregion

    #region Properties
    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized { get; private set; } = false;
    
    /// <summary>
    /// 로그아웃 진행 중 여부
    /// </summary>
    public bool IsLogoutInProgress { get; private set; } = false;
    
    /// <summary>
    /// 현재 로그아웃 진행률 (0.0 ~ 1.0)
    /// </summary>
    public float LogoutProgress { get; private set; } = 0f;
    
    /// <summary>
    /// 버튼 애니메이션 진행 중 여부
    /// </summary>
    public bool IsAnimating { get; private set; } = false;
    #endregion

    #region Private Fields
    private bool _isConnectedToIntegration = false;
    private Coroutine _logoutAnimationCoroutine;
    private Coroutine _buttonPressAnimationCoroutine;
    private Vector3 _logoutButtonOriginalScale;
    private Vector3 _termsButtonOriginalScale;
    private Vector3 _privacyButtonOriginalScale;
    private Vector3 _mailboxButtonOriginalScale;
    
    // 기본 텍스트 (로컬라이제이션 fallback)
    private const string DefaultLogoutText = "로그아웃";
    private const string DefaultTermsText = "약관 보기";
    private const string DefaultPrivacyText = "개인정보 처리방침";
    private const string DefaultMailboxText = "우편함";
    private const string DefaultLoggingOutText = "로그아웃 중...";
    private const string DefaultCancelText = "취소";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        CacheOriginalScales();
    }

    private void Start()
    {
        InitializeActionButtons();
        ConnectToSettingsIntegration();
    }

    private void OnDestroy()
    {
        DisconnectFromSettingsIntegration();
        CleanupAnimations();
        
        // 이벤트 정리
        OnLogoutButtonClicked = null;
        OnTermsButtonClicked = null;
        OnPrivacyButtonClicked = null;
        OnMailboxButtonClicked = null;
        OnLogoutCancelRequested = null;
        OnActionButtonsInitialized = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 컴포넌트 유효성 검사
    /// </summary>
    private void ValidateComponents()
    {
        if (logoutButton == null)
            Debug.LogError("[ActionButtonsUI] Logout button is not assigned!");
            
        if (termsButton == null)
            Debug.LogError("[ActionButtonsUI] Terms button is not assigned!");
            
        if (logoutProgressPanel == null)
            Debug.LogWarning("[ActionButtonsUI] Logout progress panel is not assigned");
            
        if (logoutProgressSlider == null)
            Debug.LogWarning("[ActionButtonsUI] Logout progress slider is not assigned");
    }

    /// <summary>
    /// 원본 스케일 캐시
    /// </summary>
    private void CacheOriginalScales()
    {
        if (logoutButton != null)
            _logoutButtonOriginalScale = logoutButton.transform.localScale;
            
        if (termsButton != null)
            _termsButtonOriginalScale = termsButton.transform.localScale;
            
        if (privacyButton != null)
            _privacyButtonOriginalScale = privacyButton.transform.localScale;
            
        if (mailboxButton != null)
            _mailboxButtonOriginalScale = mailboxButton.transform.localScale;
    }

    /// <summary>
    /// 액션 버튼 UI 초기화
    /// </summary>
    private void InitializeActionButtons()
    {
        try
        {
            SetupButtonEvents();
            SetupButtonTexts();
            InitializeProgressPanel();
            
            IsInitialized = true;
            OnActionButtonsInitialized?.Invoke();
            
            Debug.Log("[ActionButtonsUI] Action buttons initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ActionButtonsUI] Initialization failed: {ex.Message}");
            IsInitialized = false;
        }
    }

    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private void SetupButtonEvents()
    {
        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(OnLogoutButtonPressed);
        }
        
        if (termsButton != null)
        {
            termsButton.onClick.AddListener(OnTermsButtonPressed);
        }
        
        if (privacyButton != null)
        {
            privacyButton.onClick.AddListener(OnPrivacyButtonPressed);
        }
        
        if (mailboxButton != null)
        {
            mailboxButton.onClick.AddListener(OnMailboxButtonPressed);
        }
        
        if (cancelLogoutButton != null)
        {
            cancelLogoutButton.onClick.AddListener(OnCancelLogoutButtonPressed);
        }
    }

    /// <summary>
    /// 버튼 텍스트 설정
    /// </summary>
    private void SetupButtonTexts()
    {
        // 로컬라이제이션 시스템이 있다면 사용하고, 없다면 기본 텍스트 사용
        if (logoutButtonText != null)
            logoutButtonText.text = GetLocalizedText(logoutTextKey, DefaultLogoutText);
            
        if (termsButtonText != null)
            termsButtonText.text = GetLocalizedText(termsTextKey, DefaultTermsText);
            
        if (privacyButtonText != null)
            privacyButtonText.text = GetLocalizedText(privacyTextKey, DefaultPrivacyText);
            
        if (mailboxButtonText != null)
            mailboxButtonText.text = GetLocalizedText(mailboxTextKey, DefaultMailboxText);
    }

    /// <summary>
    /// 진행 패널 초기화
    /// </summary>
    private void InitializeProgressPanel()
    {
        if (logoutProgressPanel != null)
        {
            logoutProgressPanel.SetActive(false);
        }
        
        if (logoutProgressSlider != null)
        {
            logoutProgressSlider.value = 0f;
        }
        
        if (logoutProgressText != null)
        {
            logoutProgressText.text = "";
        }
    }

    /// <summary>
    /// 로컬라이제이션 텍스트 가져오기 (fallback 포함)
    /// </summary>
    private string GetLocalizedText(string key, string defaultText)
    {
        // 로컬라이제이션 시스템이 있다면 사용
        // 여기서는 간단히 기본 텍스트 반환 (필요시 LocalizationManager 연동)
        return defaultText;
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
            
            Debug.Log("[ActionButtonsUI] Connected to SettingsIntegration");
        }
        else
        {
            Debug.LogWarning("[ActionButtonsUI] SettingsIntegration not available, using fallback mode");
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
    /// 로그아웃 버튼 클릭 이벤트
    /// </summary>
    private void OnLogoutButtonPressed()
    {
        if (IsLogoutInProgress) return;
        
        // 버튼 애니메이션
        StartButtonPressAnimation(logoutButton, _logoutButtonOriginalScale);
        
        // 통합 시스템에 로그아웃 요청
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.InitiateLogout();
        }
        
        // 이벤트 발생
        OnLogoutButtonClicked?.Invoke();
        
        Debug.Log("[ActionButtonsUI] Logout button pressed");
    }

    /// <summary>
    /// 약관 보기 버튼 클릭 이벤트
    /// </summary>
    private void OnTermsButtonPressed()
    {
        // 버튼 애니메이션
        StartButtonPressAnimation(termsButton, _termsButtonOriginalScale);
        
        // 통합 시스템에 약관 보기 요청
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.ShowTermsAndConditions();
        }
        
        // 이벤트 발생
        OnTermsButtonClicked?.Invoke();
        
        Debug.Log("[ActionButtonsUI] Terms button pressed");
    }

    /// <summary>
    /// 개인정보 처리방침 버튼 클릭 이벤트
    /// </summary>
    private void OnPrivacyButtonPressed()
    {
        // 버튼 애니메이션
        StartButtonPressAnimation(privacyButton, _privacyButtonOriginalScale);
        
        // 통합 시스템에 개인정보 처리방침 보기 요청
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.ShowPrivacyPolicy();
        }
        
        // 이벤트 발생
        OnPrivacyButtonClicked?.Invoke();
        
        Debug.Log("[ActionButtonsUI] Privacy button pressed");
    }

    /// <summary>
    /// 우편함 버튼 클릭 이벤트
    /// </summary>
    private void OnMailboxButtonPressed()
    {
        // 버튼 애니메이션
        StartButtonPressAnimation(mailboxButton, _mailboxButtonOriginalScale);
        
        // 통합 시스템에 우편함 열기 요청
        if (_isConnectedToIntegration && SettingsIntegration.Instance != null)
        {
            SettingsIntegration.Instance.OpenMailbox();
        }
        
        // 이벤트 발생
        OnMailboxButtonClicked?.Invoke();
        
        Debug.Log("[ActionButtonsUI] Mailbox button pressed");
    }

    /// <summary>
    /// 로그아웃 취소 버튼 클릭 이벤트
    /// </summary>
    private void OnCancelLogoutButtonPressed()
    {
        // 로그아웃 취소 요청
        OnLogoutCancelRequested?.Invoke();
        
        // 진행 패널 숨기기
        HideLogoutProgress();
        
        Debug.Log("[ActionButtonsUI] Logout cancel requested");
    }

    /// <summary>
    /// 통합 시스템 설정 변경 이벤트
    /// </summary>
    private void OnIntegrationSettingChanged(string key, object value)
    {
        switch (key)
        {
            case "LogoutStatus":
                HandleLogoutStatusChange(value);
                break;
                
            case "LogoutProgress":
                HandleLogoutProgressChange(value);
                break;
        }
    }

    /// <summary>
    /// 통합 시스템 초기화 완료 이벤트
    /// </summary>
    private void OnIntegrationInitialized()
    {
        // 초기화 완료 후 추가 설정 필요시 처리
        Debug.Log("[ActionButtonsUI] SettingsIntegration initialized");
    }
    #endregion

    #region Logout Progress Handling
    /// <summary>
    /// 로그아웃 상태 변경 처리
    /// </summary>
    private void HandleLogoutStatusChange(object statusValue)
    {
        if (statusValue is string status)
        {
            switch (status)
            {
                case "Started":
                    StartLogoutProgress();
                    break;
            }
        }
        else if (statusValue is System.Collections.IDictionary statusDict)
        {
            // LogoutCompleted 이벤트 처리
            if (statusDict.Contains("Success"))
            {
                bool success = (bool)statusDict["Success"];
                string message = statusDict["Message"]?.ToString() ?? "";
                CompleteLogoutProgress(success, message);
            }
        }
    }

    /// <summary>
    /// 로그아웃 진행률 변경 처리
    /// </summary>
    private void HandleLogoutProgressChange(object progressValue)
    {
        if (progressValue is System.Collections.IDictionary progressDict)
        {
            if (progressDict.Contains("Progress"))
            {
                float progress = Convert.ToSingle(progressDict["Progress"]);
                string message = progressDict["Message"]?.ToString() ?? "";
                UpdateLogoutProgress(progress, message);
            }
        }
    }

    /// <summary>
    /// 로그아웃 진행 시작
    /// </summary>
    private void StartLogoutProgress()
    {
        IsLogoutInProgress = true;
        LogoutProgress = 0f;
        
        // UI 업데이트
        ShowLogoutProgress();
        SetLogoutButtonState(false, progressButtonColor);
        
        Debug.Log("[ActionButtonsUI] Logout progress started");
    }

    /// <summary>
    /// 로그아웃 진행률 업데이트
    /// </summary>
    private void UpdateLogoutProgress(float progress, string message)
    {
        LogoutProgress = Mathf.Clamp01(progress);
        
        // 진행률 슬라이더 업데이트
        if (logoutProgressSlider != null)
        {
            logoutProgressSlider.value = LogoutProgress;
        }
        
        // 진행 메시지 업데이트
        if (logoutProgressText != null)
        {
            logoutProgressText.text = message;
        }
        
        Debug.Log($"[ActionButtonsUI] Logout progress: {progress * 100:F0}% - {message}");
    }

    /// <summary>
    /// 로그아웃 진행 완료
    /// </summary>
    private void CompleteLogoutProgress(bool success, string message)
    {
        IsLogoutInProgress = false;
        LogoutProgress = success ? 1f : 0f;
        
        // 완료 상태 표시
        UpdateLogoutProgress(LogoutProgress, message);
        
        // 잠시 대기 후 UI 복원
        StartCoroutine(CompleteLogoutProgressCoroutine(success));
        
        Debug.Log($"[ActionButtonsUI] Logout completed: {success} - {message}");
    }

    /// <summary>
    /// 로그아웃 완료 처리 코루틴
    /// </summary>
    private IEnumerator CompleteLogoutProgressCoroutine(bool success)
    {
        // 결과 표시 시간
        yield return new WaitForSeconds(2f);
        
        // UI 복원
        HideLogoutProgress();
        
        if (success)
        {
            // 성공 시 버튼 비활성화 (씬 전환 대기)
            SetLogoutButtonState(false, disabledButtonColor);
        }
        else
        {
            // 실패 시 버튼 복원
            SetLogoutButtonState(true, normalButtonColor);
        }
    }
    #endregion

    #region UI Update Methods
    /// <summary>
    /// 로그아웃 진행 패널 표시
    /// </summary>
    private void ShowLogoutProgress()
    {
        if (logoutProgressPanel != null)
        {
            logoutProgressPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 로그아웃 진행 패널 숨기기
    /// </summary>
    private void HideLogoutProgress()
    {
        if (logoutProgressPanel != null)
        {
            logoutProgressPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 로그아웃 버튼 상태 설정
    /// </summary>
    private void SetLogoutButtonState(bool interactable, Color color)
    {
        if (logoutButton != null)
        {
            logoutButton.interactable = interactable;
        }
        
        if (logoutButtonImage != null)
        {
            logoutButtonImage.color = color;
        }
        
        if (logoutButtonText != null)
        {
            logoutButtonText.text = interactable ? 
                GetLocalizedText(logoutTextKey, DefaultLogoutText) : 
                GetLocalizedText(loggingOutTextKey, DefaultLoggingOutText);
        }
    }
    #endregion

    #region Animation Methods
    /// <summary>
    /// 버튼 눌림 애니메이션 시작
    /// </summary>
    private void StartButtonPressAnimation(Button button, Vector3 originalScale)
    {
        if (button == null) return;
        
        if (_buttonPressAnimationCoroutine != null)
        {
            StopCoroutine(_buttonPressAnimationCoroutine);
        }
        
        _buttonPressAnimationCoroutine = StartCoroutine(ButtonPressAnimationCoroutine(button, originalScale));
    }

    /// <summary>
    /// 버튼 눌림 애니메이션 코루틴
    /// </summary>
    private IEnumerator ButtonPressAnimationCoroutine(Button button, Vector3 originalScale)
    {
        IsAnimating = true;
        
        Transform buttonTransform = button.transform;
        Vector3 pressedScale = originalScale * pressScaleMultiplier;
        
        // 눌림 애니메이션
        float elapsedTime = 0f;
        while (elapsedTime < buttonAnimationDuration * 0.5f)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / (buttonAnimationDuration * 0.5f);
            float curveValue = buttonPressAnimationCurve.Evaluate(normalizedTime);
            
            Vector3 currentScale = Vector3.Lerp(originalScale, pressedScale, curveValue);
            buttonTransform.localScale = currentScale;
            
            yield return null;
        }
        
        // 복원 애니메이션
        elapsedTime = 0f;
        while (elapsedTime < buttonAnimationDuration * 0.5f)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / (buttonAnimationDuration * 0.5f);
            float curveValue = buttonPressAnimationCurve.Evaluate(normalizedTime);
            
            Vector3 currentScale = Vector3.Lerp(pressedScale, originalScale, curveValue);
            buttonTransform.localScale = currentScale;
            
            yield return null;
        }
        
        // 최종 스케일 설정
        buttonTransform.localScale = originalScale;
        
        IsAnimating = false;
    }

    /// <summary>
    /// 애니메이션 정리
    /// </summary>
    private void CleanupAnimations()
    {
        if (_logoutAnimationCoroutine != null)
        {
            StopCoroutine(_logoutAnimationCoroutine);
            _logoutAnimationCoroutine = null;
        }
        
        if (_buttonPressAnimationCoroutine != null)
        {
            StopCoroutine(_buttonPressAnimationCoroutine);
            _buttonPressAnimationCoroutine = null;
        }
        
        IsAnimating = false;
    }
    #endregion

    #region Public API
    /// <summary>
    /// 모든 버튼 상호작용 활성화/비활성화
    /// </summary>
    /// <param name="interactable">상호작용 가능 여부</param>
    public void SetButtonsInteractable(bool interactable)
    {
        if (logoutButton != null && !IsLogoutInProgress)
            logoutButton.interactable = interactable;
            
        if (termsButton != null)
            termsButton.interactable = interactable;
            
        if (privacyButton != null)
            privacyButton.interactable = interactable;
            
        if (mailboxButton != null)
            mailboxButton.interactable = interactable;
            
        Debug.Log($"[ActionButtonsUI] Buttons interactable set to {interactable}");
    }

    /// <summary>
    /// 로그아웃 진행 강제 취소
    /// </summary>
    public void ForceStopLogoutProgress()
    {
        if (IsLogoutInProgress)
        {
            IsLogoutInProgress = false;
            LogoutProgress = 0f;
            
            HideLogoutProgress();
            SetLogoutButtonState(true, normalButtonColor);
            
            Debug.Log("[ActionButtonsUI] Logout progress force stopped");
        }
    }

    /// <summary>
    /// 현재 액션 버튼 상태 반환
    /// </summary>
    public ActionButtonsState GetCurrentState()
    {
        return new ActionButtonsState
        {
            IsInitialized = IsInitialized,
            IsLogoutInProgress = IsLogoutInProgress,
            LogoutProgress = LogoutProgress,
            IsAnimating = IsAnimating
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 액션 버튼 상태 정보
/// </summary>
[Serializable]
public class ActionButtonsState
{
    public bool IsInitialized { get; set; }
    public bool IsLogoutInProgress { get; set; }
    public float LogoutProgress { get; set; }
    public bool IsAnimating { get; set; }
}
#endregion