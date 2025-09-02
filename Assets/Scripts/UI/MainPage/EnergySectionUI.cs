using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// 에너지 섹션 UI 컴포넌트 (Stream B Integration)
/// 사용자의 에너지 상태를 표시하고 관리하는 UI 섹션입니다.
/// SectionBase를 상속하여 MainPageManager와 연동됩니다.
/// 
/// 하이브리드 접근 방식:
/// - 새로운 전용 Energy UI 컴포넌트들(EnergyDisplayUI, EnergyBar, EnergyPurchaseUI)을 우선 사용
/// - 새 컴포넌트가 없으면 기존 레거시 시스템으로 폴백
/// - 실시간 업데이트와 시각적 피드백을 위한 이벤트 시스템 통합
/// </summary>
public class EnergySectionUI : SectionBase
{
    #region UI References - New Component Integration
    [Header("Energy Display Components")]
    [SerializeField] private EnergyDisplayUI energyDisplayUI;
    [SerializeField] private EnergyBar energyBar;
    [SerializeField] private EnergyPurchaseUI energyPurchaseUI;
    
    [Header("Legacy UI References (for backward compatibility)")]
    [SerializeField] private Slider energySlider;
    [SerializeField] private Text currentEnergyText;
    [SerializeField] private Text maxEnergyText;
    [SerializeField] private Text energyPercentageText;
    [SerializeField] private Image energyFillImage;
    
    [Header("Recharge Info")]
    [SerializeField] private Text rechargeTimeText;
    [SerializeField] private Text nextRechargeText;
    [SerializeField] private Button rechargeButton;
    [SerializeField] private GameObject rechargeAvailableIndicator;
    
    [Header("Energy Actions")]
    [SerializeField] private Button buyEnergyButton;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button useEnergyButton;
    
    [Header("Visual Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Gradient energyColorGradient;
    [SerializeField] private GameObject lowEnergyWarning;
    [SerializeField] private GameObject fullEnergyGlow;
    #endregion

    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Energy;
    public override string SectionDisplayName => "에너지";
    #endregion

    #region Events for EnergySection Integration
    public event System.Action<EnergyActionRequest> OnEnergyActionRequested;
    public event System.Action<EnergyPurchaseRequest> OnEnergyPurchaseRequested;
    #endregion

    #region Private Fields
    private int _currentEnergy = 0;
    private int _maxEnergy = 100;
    private DateTime _lastRechargeTime;
    private TimeSpan _rechargeInterval = TimeSpan.FromMinutes(10); // 10분마다 1에너지 충전
    private int _rechargeAmount = 1;
    
    // Animation and visual state
    private Coroutine _energyAnimationCoroutine;
    private Coroutine _rechargeTimerCoroutine;
    private bool _isEnergyLow = false;
    private bool _isEnergyFull = false;
    
    // Energy thresholds
    private const float LOW_ENERGY_THRESHOLD = 0.2f; // 20% 이하 시 경고
    private const int ENERGY_RECHARGE_COST = 100; // 에너지 구매 비용 (임시)
    #endregion

    #region SectionBase Implementation
    protected override void OnInitialize()
    {
        // Initialize new energy components first
        InitializeEnergyComponents();
        
        // Legacy initialization for backward compatibility
        SetupUIEvents();
        SetupEnergyGradient();
        InitializeEnergyData();
        ValidateEnergyComponents();
    }

    protected override void OnActivate()
    {
        RefreshEnergyDisplay();
        StartRechargeTimer();
    }

    protected override void OnDeactivate()
    {
        StopEnergyAnimations();
        StopRechargeTimer();
    }

    protected override void OnCleanup()
    {
        // Cleanup new component event subscriptions
        if (energyDisplayUI != null)
        {
            EnergyDisplayUI.OnEnergyDisplayUpdated -= OnEnergyDisplayUpdated;
            EnergyDisplayUI.OnLowEnergyStateChanged -= OnLowEnergyStateChanged;
            EnergyDisplayUI.OnPurchaseRequested -= OnPurchaseRequested;
        }

        if (energyPurchaseUI != null)
        {
            EnergyPurchaseUI.OnPurchaseCompleted -= OnPurchaseCompleted;
            EnergyPurchaseUI.OnPurchaseFailed -= OnPurchaseFailed;
        }

        // Legacy cleanup
        UnsubscribeFromUIEvents();
        StopAllCoroutines();
        
        Debug.Log("[EnergySectionUI] Cleanup completed");
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null) return;
        
        // 사용자 데이터에서 에너지 정보 업데이트
        _currentEnergy = userData.CurrentEnergy;
        _maxEnergy = userData.MaxEnergy;
        _lastRechargeTime = userData.LastEnergyRechargeTime;
        
        // Use new components if available, otherwise fallback to legacy
        if (energyDisplayUI != null)
        {
            // New component system handles the updates automatically
            energyDisplayUI.TriggerUpdate();
        }
        else
        {
            // Legacy system
            UpdateEnergyDisplay();
            UpdateRechargeInfo();
            UpdateVisualState();
        }
        
        Debug.Log($"[EnergySectionUI] UI updated - Energy: {_currentEnergy}/{_maxEnergy}");
    }

    protected override void ValidateComponents()
    {
        // 필수 컴포넌트 검증
        if (energySlider == null)
            ReportError("Energy slider component is missing!");
            
        if (currentEnergyText == null)
            ReportError("Current energy text component is missing!");
            
        if (maxEnergyText == null)
            ReportError("Max energy text component is missing!");
            
        // 경고 레벨 컴포넌트
        if (rechargeTimeText == null)
            Debug.LogWarning("[EnergySectionUI] Recharge time text is not assigned");
            
        if (rechargeButton == null)
            Debug.LogWarning("[EnergySectionUI] Recharge button is not assigned");
    }
    #endregion

    #region UI Event Setup
    private void SetupUIEvents()
    {
        if (rechargeButton != null)
            rechargeButton.onClick.AddListener(OnRechargeButtonClicked);
            
        if (buyEnergyButton != null)
            buyEnergyButton.onClick.AddListener(OnBuyEnergyClicked);
            
        if (watchAdButton != null)
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
            
        if (useEnergyButton != null)
            useEnergyButton.onClick.AddListener(OnUseEnergyClicked);
    }

    private void UnsubscribeFromUIEvents()
    {
        if (rechargeButton != null)
            rechargeButton.onClick.RemoveListener(OnRechargeButtonClicked);
            
        if (buyEnergyButton != null)
            buyEnergyButton.onClick.RemoveListener(OnBuyEnergyClicked);
            
        if (watchAdButton != null)
            watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
            
        if (useEnergyButton != null)
            useEnergyButton.onClick.RemoveListener(OnUseEnergyClicked);
    }
    #endregion

    #region Energy Management
    /// <summary>
    /// 새로운 에너지 컴포넌트 초기화
    /// </summary>
    private void InitializeEnergyComponents()
    {
        // Validate new components
        if (energyDisplayUI == null)
        {
            energyDisplayUI = GetComponentInChildren<EnergyDisplayUI>();
            if (energyDisplayUI == null)
            {
                Debug.LogWarning("[EnergySectionUI] EnergyDisplayUI component not found in children");
            }
        }

        if (energyBar == null)
        {
            energyBar = GetComponentInChildren<EnergyBar>();
            if (energyBar == null)
            {
                Debug.LogWarning("[EnergySectionUI] EnergyBar component not found in children");
            }
        }

        if (energyPurchaseUI == null)
        {
            energyPurchaseUI = GetComponentInChildren<EnergyPurchaseUI>();
            if (energyPurchaseUI == null)
            {
                Debug.LogWarning("[EnergySectionUI] EnergyPurchaseUI component not found in children");
            }
        }

        // Subscribe to events from new components
        if (energyDisplayUI != null)
        {
            EnergyDisplayUI.OnEnergyDisplayUpdated += OnEnergyDisplayUpdated;
            EnergyDisplayUI.OnLowEnergyStateChanged += OnLowEnergyStateChanged;
            EnergyDisplayUI.OnPurchaseRequested += OnPurchaseRequested;
        }

        if (energyPurchaseUI != null)
        {
            EnergyPurchaseUI.OnPurchaseCompleted += OnPurchaseCompleted;
            EnergyPurchaseUI.OnPurchaseFailed += OnPurchaseFailed;
        }

        Debug.Log("[EnergySectionUI] New energy components initialized");
    }

    private void InitializeEnergyData()
    {
        // UserDataManager에서 에너지 정보 로드
        if (_userDataManager?.CurrentUser != null)
        {
            var userData = _userDataManager.CurrentUser;
            _currentEnergy = userData.CurrentEnergy;
            _maxEnergy = userData.MaxEnergy;
            _lastRechargeTime = userData.LastEnergyRechargeTime;
        }
        else
        {
            // 기본값 설정
            _currentEnergy = 50;
            _maxEnergy = 100;
            _lastRechargeTime = DateTime.Now;
        }
        
        Debug.Log($"[EnergySectionUI] Initialized energy: {_currentEnergy}/{_maxEnergy}");
    }

    private void UpdateEnergyDisplay()
    {
        SafeUIUpdate(() =>
        {
            if (energySlider != null)
                AnimateEnergySlider(_currentEnergy, _maxEnergy);
                
            if (currentEnergyText != null)
                currentEnergyText.text = _currentEnergy.ToString();
                
            if (maxEnergyText != null)
                maxEnergyText.text = _maxEnergy.ToString();
                
            if (energyPercentageText != null)
            {
                float percentage = _maxEnergy > 0 ? (float)_currentEnergy / _maxEnergy * 100f : 0f;
                energyPercentageText.text = $"{percentage:F0}%";
            }
                
            UpdateEnergyColor();
            
        }, "Energy Display");
    }

    private void UpdateRechargeInfo()
    {
        SafeUIUpdate(() =>
        {
            var now = DateTime.Now;
            var timeSinceLastRecharge = now - _lastRechargeTime;
            var timeToNextRecharge = _rechargeInterval - timeSinceLastRecharge;
            
            bool canRecharge = _currentEnergy < _maxEnergy && timeToNextRecharge.TotalSeconds <= 0;
            
            if (rechargeButton != null)
                rechargeButton.interactable = canRecharge;
                
            if (rechargeAvailableIndicator != null)
                rechargeAvailableIndicator.SetActive(canRecharge);
                
            if (nextRechargeText != null)
            {
                if (canRecharge)
                {
                    nextRechargeText.text = "충전 가능!";
                }
                else if (_currentEnergy >= _maxEnergy)
                {
                    nextRechargeText.text = "에너지 가득참";
                }
                else
                {
                    nextRechargeText.text = FormatTimeSpan(timeToNextRecharge);
                }
            }
            
        }, "Recharge Info");
    }

    private void UpdateVisualState()
    {
        float energyRatio = _maxEnergy > 0 ? (float)_currentEnergy / _maxEnergy : 0f;
        
        // 에너지 부족 경고
        bool shouldShowLowWarning = energyRatio <= LOW_ENERGY_THRESHOLD;
        if (_isEnergyLow != shouldShowLowWarning)
        {
            _isEnergyLow = shouldShowLowWarning;
            if (lowEnergyWarning != null)
                lowEnergyWarning.SetActive(_isEnergyLow);
        }
        
        // 에너지 가득참 효과
        bool shouldShowFullGlow = energyRatio >= 1.0f;
        if (_isEnergyFull != shouldShowFullGlow)
        {
            _isEnergyFull = shouldShowFullGlow;
            if (fullEnergyGlow != null)
                fullEnergyGlow.SetActive(_isEnergyFull);
        }
        
        // 버튼 상태 업데이트
        if (useEnergyButton != null)
            useEnergyButton.interactable = _currentEnergy > 0;
    }
    #endregion

    #region Animation and Visual Effects
    private void SetupEnergyGradient()
    {
        if (energyColorGradient == null)
        {
            // 기본 그라데이션 생성 (빨강 -> 노랑 -> 초록)
            energyColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            
            colorKeys[0] = new GradientColorKey(Color.red, 0.0f);
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);
            
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            
            energyColorGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    private void AnimateEnergySlider(int currentEnergy, int maxEnergy)
    {
        if (energySlider == null) return;
        
        float targetValue = maxEnergy > 0 ? (float)currentEnergy / maxEnergy : 0f;
        
        if (_energyAnimationCoroutine != null)
            StopCoroutine(_energyAnimationCoroutine);
            
        _energyAnimationCoroutine = StartCoroutine(AnimateSliderCoroutine(targetValue));
    }

    private IEnumerator AnimateSliderCoroutine(float targetValue)
    {
        float startValue = energySlider.value;
        float duration = 0.8f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Ease-out 애니메이션 커브
            t = 1f - Mathf.Pow(1f - t, 2f);
            
            float currentValue = Mathf.Lerp(startValue, targetValue, t);
            energySlider.value = currentValue;
            
            UpdateEnergyColor();
            
            yield return null;
        }
        
        energySlider.value = targetValue;
        UpdateEnergyColor();
        _energyAnimationCoroutine = null;
    }

    private void UpdateEnergyColor()
    {
        if (energyFillImage != null && energySlider != null)
        {
            Color energyColor = energyColorGradient.Evaluate(energySlider.value);
            energyFillImage.color = energyColor;
        }
    }

    private void StopEnergyAnimations()
    {
        if (_energyAnimationCoroutine != null)
        {
            StopCoroutine(_energyAnimationCoroutine);
            _energyAnimationCoroutine = null;
        }
    }
    #endregion

    #region Recharge Timer
    private void StartRechargeTimer()
    {
        if (_rechargeTimerCoroutine == null)
        {
            _rechargeTimerCoroutine = StartCoroutine(RechargeTimerCoroutine());
        }
    }

    private void StopRechargeTimer()
    {
        if (_rechargeTimerCoroutine != null)
        {
            StopCoroutine(_rechargeTimerCoroutine);
            _rechargeTimerCoroutine = null;
        }
    }

    private IEnumerator RechargeTimerCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // 매 초마다 업데이트
            
            if (_currentEnergy < _maxEnergy)
            {
                var now = DateTime.Now;
                var timeSinceLastRecharge = now - _lastRechargeTime;
                
                // 자동 에너지 충전 체크
                if (timeSinceLastRecharge >= _rechargeInterval)
                {
                    AutoRechargeEnergy();
                }
                
                UpdateRechargeInfo();
            }
        }
    }

    private void AutoRechargeEnergy()
    {
        if (_currentEnergy < _maxEnergy)
        {
            _currentEnergy = Mathf.Min(_currentEnergy + _rechargeAmount, _maxEnergy);
            _lastRechargeTime = DateTime.Now;
            
            // UserDataManager에 에너지 업데이트 알림
            NotifyEnergyChanged();
            
            UpdateEnergyDisplay();
            UpdateVisualState();
            
            Debug.Log($"[EnergySectionUI] Auto recharged energy: {_currentEnergy}/{_maxEnergy}");
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 새로운 컴포넌트 이벤트 핸들러들
    /// </summary>
    private void OnEnergyDisplayUpdated(int currentEnergy, int maxEnergy)
    {
        // Sync with legacy system
        _currentEnergy = currentEnergy;
        _maxEnergy = maxEnergy;
        
        // Update legacy UI if new components are not available
        if (energyDisplayUI == null)
        {
            UpdateEnergyDisplay();
        }
        
        Debug.Log($"[EnergySectionUI] Energy display updated: {currentEnergy}/{maxEnergy}");
    }

    private void OnLowEnergyStateChanged(bool isLowEnergy)
    {
        Debug.Log($"[EnergySectionUI] Low energy state changed: {isLowEnergy}");
        
        // Additional logic for low energy state changes
        if (isLowEnergy)
        {
            // Could trigger notifications, sound effects, etc.
            Debug.Log("[EnergySectionUI] Low energy warning triggered!");
        }
    }

    private void OnPurchaseRequested()
    {
        Debug.Log("[EnergySectionUI] Purchase requested from EnergyDisplayUI");
        
        // Handle purchase request - delegate to purchase modal
        if (energyPurchaseUI != null)
        {
            energyPurchaseUI.ShowPurchaseModal();
        }
        else
        {
            // Fallback to legacy purchase handling
            OnBuyEnergyClicked();
        }
    }

    private void OnPurchaseCompleted(EnergyPurchaseUI.PurchaseOption option)
    {
        Debug.Log($"[EnergySectionUI] Purchase completed: {option.displayName}");
        
        // Refresh display after purchase
        RefreshEnergyDisplay();
        
        // Broadcast energy purchase success
        BroadcastToAllSections(new EnergyChangedMessage
        {
            CurrentEnergy = _currentEnergy,
            MaxEnergy = _maxEnergy,
            ChangeAmount = option.energyAmount + option.bonusAmount,
            ChangeReason = "Purchase",
            Timestamp = DateTime.Now
        });
    }

    private void OnPurchaseFailed(EnergyPurchaseUI.PurchaseOption option, string errorMessage)
    {
        Debug.LogError($"[EnergySectionUI] Purchase failed: {option.displayName} - {errorMessage}");
        
        // Handle purchase failure - could show additional UI feedback
        // The EnergyPurchaseUI already handles showing error messages
    }

    /// <summary>
    /// Legacy event handlers
    /// </summary>
    private void OnRechargeButtonClicked()
    {
        Debug.Log("[EnergySectionUI] Recharge button clicked");
        
        if (_currentEnergy < _maxEnergy)
        {
            _currentEnergy = Mathf.Min(_currentEnergy + _rechargeAmount, _maxEnergy);
            _lastRechargeTime = DateTime.Now;
            
            NotifyEnergyChanged();
            UpdateEnergyDisplay();
            UpdateRechargeInfo();
            UpdateVisualState();
        }
    }

    private void OnBuyEnergyClicked()
    {
        Debug.Log("[EnergySectionUI] Buy energy button clicked");
        
        // 에너지 구매 요청 이벤트 발생
        OnEnergyPurchaseRequested?.Invoke(new EnergyPurchaseRequest
        {
            Amount = 50,
            PaymentMethod = "coins"
        });
    }

    private void OnWatchAdClicked()
    {
        Debug.Log("[EnergySectionUI] Watch ad button clicked");
        
        // 광고 시청 후 에너지 충전 (향후 구현)
        SendMessageToSection(MainPageSectionType.Settings, new AdRewardRequest
        {
            RequestType = "watch_ad_for_energy",
            RewardType = "energy",
            RewardAmount = 10
        });
    }

    private void OnUseEnergyClicked()
    {
        Debug.Log("[EnergySectionUI] Use energy button clicked");
        
        if (_currentEnergy > 0)
        {
            ConsumeEnergy(1);
        }
    }
    #endregion

    #region Energy Operations
    /// <summary>
    /// 에너지 소모
    /// </summary>
    public bool ConsumeEnergy(int amount)
    {
        if (_currentEnergy >= amount)
        {
            _currentEnergy -= amount;
            NotifyEnergyChanged();
            UpdateEnergyDisplay();
            UpdateVisualState();
            
            Debug.Log($"[EnergySectionUI] Consumed {amount} energy: {_currentEnergy}/{_maxEnergy}");
            return true;
        }
        
        Debug.LogWarning($"[EnergySectionUI] Not enough energy to consume {amount}. Current: {_currentEnergy}");
        return false;
    }

    /// <summary>
    /// 에너지 충전
    /// </summary>
    public void AddEnergy(int amount)
    {
        _currentEnergy = Mathf.Min(_currentEnergy + amount, _maxEnergy);
        NotifyEnergyChanged();
        UpdateEnergyDisplay();
        UpdateVisualState();
        
        Debug.Log($"[EnergySectionUI] Added {amount} energy: {_currentEnergy}/{_maxEnergy}");
    }

    /// <summary>
    /// 에너지 변경 알림
    /// </summary>
    private void NotifyEnergyChanged()
    {
        if (_userDataManager?.CurrentUser != null)
        {
            var userData = _userDataManager.CurrentUser;
            userData.CurrentEnergy = _currentEnergy;
            userData.LastEnergyRechargeTime = _lastRechargeTime;
            
            // 다른 섹션에 에너지 변경 알림
            BroadcastToAllSections(new EnergyChangedMessage
            {
                CurrentEnergy = _currentEnergy,
                MaxEnergy = _maxEnergy,
                Timestamp = DateTime.Now
            });
        }
    }
    #endregion

    #region Virtual Method Overrides
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        // 오프라인 모드에서는 구매 관련 버튼 비활성화
        if (buyEnergyButton != null)
            buyEnergyButton.interactable = !isOfflineMode;
            
        if (watchAdButton != null)
            watchAdButton.interactable = !isOfflineMode;
    }

    protected override void OnForceRefresh()
    {
        InitializeEnergyData();
        UpdateEnergyDisplay();
        UpdateRechargeInfo();
        UpdateVisualState();
    }

    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[EnergySectionUI] Received message from {fromSection}: {data?.GetType().Name}");
        
        if (data is EnergyRequest energyRequest)
        {
            HandleEnergyRequest(energyRequest);
        }
    }
    #endregion

    #region Message Handling
    private void HandleEnergyRequest(EnergyRequest request)
    {
        switch (request.RequestType)
        {
            case "consume":
                ConsumeEnergy(request.Amount);
                break;
                
            case "add":
                AddEnergy(request.Amount);
                break;
                
            case "check":
                // 에너지 상태 정보 반환
                SendMessageToSection(request.RequesterSection, new EnergyStatusResponse
                {
                    CurrentEnergy = _currentEnergy,
                    MaxEnergy = _maxEnergy,
                    CanUseEnergy = _currentEnergy > 0,
                    TimeToNextRecharge = _rechargeInterval - (DateTime.Now - _lastRechargeTime)
                });
                break;
        }
    }
    #endregion

    #region Utility Methods
    private void RefreshEnergyDisplay()
    {
        if (_userDataManager?.CurrentUser != null)
        {
            UpdateUI(_userDataManager.CurrentUser);
        }
        else
        {
            UpdateEnergyDisplay();
            UpdateRechargeInfo();
            UpdateVisualState();
        }
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds <= 0)
            return "00:00";
            
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        else
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
    #endregion

    #region Additional Methods for EnergySection Integration
    /// <summary>
    /// 에너지 설정으로 UI 초기화
    /// </summary>
    public void Initialize(EnergyConfig config)
    {
        if (config != null)
        {
            _maxEnergy = config.MaxEnergy;
            _rechargeAmount = config.RechargeRate;
            _rechargeInterval = config.RechargeInterval;
        }
        
        ValidateEnergyComponents();
        SetupEnergyGradient();
        Debug.Log("[EnergySectionUI] Initialized with energy config");
    }

    /// <summary>
    /// 에너지 표시 업데이트 (매개변수 오버로드)
    /// </summary>
    public void UpdateEnergyDisplay(int currentEnergy, int maxEnergy, float energyPercentage, TimeSpan timeUntilRecharge)
    {
        _currentEnergy = currentEnergy;
        _maxEnergy = maxEnergy;
        
        UpdateEnergyDisplay();
        
        // 충전 시간 업데이트
        if (nextRechargeText != null)
        {
            if (_currentEnergy >= _maxEnergy)
            {
                nextRechargeText.text = "에너지 가득참";
            }
            else if (timeUntilRecharge.TotalSeconds <= 0)
            {
                nextRechargeText.text = "충전 가능!";
            }
            else
            {
                nextRechargeText.text = FormatTimeSpan(timeUntilRecharge);
            }
        }
    }

    /// <summary>
    /// 시각적 상태 업데이트
    /// </summary>
    public void UpdateVisualState(bool isEnergyLow, bool isEnergyFull, bool canUseEnergy)
    {
        // 에너지 부족 경고
        if (lowEnergyWarning != null && _isEnergyLow != isEnergyLow)
        {
            _isEnergyLow = isEnergyLow;
            lowEnergyWarning.SetActive(_isEnergyLow);
        }
        
        // 에너지 가득참 효과
        if (fullEnergyGlow != null && _isEnergyFull != isEnergyFull)
        {
            _isEnergyFull = isEnergyFull;
            fullEnergyGlow.SetActive(_isEnergyFull);
        }
        
        // 버튼 상태 업데이트
        if (useEnergyButton != null)
            useEnergyButton.interactable = canUseEnergy;
    }

    /// <summary>
    /// 오프라인 모드 설정
    /// </summary>
    public void SetOfflineMode(bool isOfflineMode)
    {
        // 오프라인 모드에서는 구매 관련 버튼 비활성화
        if (buyEnergyButton != null)
            buyEnergyButton.interactable = !isOfflineMode;
            
        if (watchAdButton != null)
            watchAdButton.interactable = !isOfflineMode;
    }
    #endregion
}

#region Message Data Classes
[System.Serializable]
public class EnergyPurchaseRequest
{
    public string RequestType;
    public int Amount;
    public int Cost;
}

[System.Serializable]
public class AdRewardRequest
{
    public string RequestType;
    public string RewardType;
    public int RewardAmount;
}

[System.Serializable]
public class EnergyRequest
{
    public string RequestType;
    public int Amount;
    public MainPageSectionType RequesterSection;
}

[System.Serializable]
public class EnergyStatusResponse
{
    public int CurrentEnergy;
    public int MaxEnergy;
    public bool CanUseEnergy;
    public TimeSpan TimeToNextRecharge;
}

[System.Serializable]
public class EnergyChangedMessage
{
    public int CurrentEnergy;
    public int MaxEnergy;
    public DateTime Timestamp;
}
#endregion