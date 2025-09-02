using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에너지 표시 UI 메인 컨트롤러
/// EnergyManager.Instance와 연동하여 실시간 에너지 정보를 표시합니다.
/// 1초 간격으로 UI를 업데이트하며, 시각적 피드백을 제공합니다.
/// </summary>
public class EnergyDisplayUI : MonoBehaviour
{
    #region UI References
    [Header("Core Components")]
    [SerializeField] private EnergyBar energyBar;
    [SerializeField] private Text currentEnergyText;
    [SerializeField] private Text maxEnergyText;
    [SerializeField] private Text energyRatioText;
    
    [Header("Timer Display")]
    [SerializeField] private Text recoveryTimerText;
    [SerializeField] private GameObject timerContainer;
    
    [Header("Action Buttons")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private EnergyPurchaseUI purchaseModal;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject lowEnergyWarning;
    [SerializeField] private GameObject fullEnergyEffect;
    [SerializeField] private CanvasGroup mainContainer;
    #endregion

    #region Private Fields
    private Coroutine _updateCoroutine;
    private EnergyManager _energyManager;
    
    // UI State
    private int _lastCurrentEnergy = -1;
    private int _lastMaxEnergy = -1;
    private bool _isLowEnergy = false;
    private bool _isFullEnergy = false;
    
    // Update Configuration
    private const float UPDATE_INTERVAL = 1.0f; // 1-second interval as specified
    private const float LOW_ENERGY_THRESHOLD = 0.2f;
    #endregion

    #region Events
    public static event Action<int, int> OnEnergyDisplayUpdated;
    public static event Action<bool> OnLowEnergyStateChanged;
    public static event Action OnPurchaseRequested;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        SetupUI();
    }

    private void Start()
    {
        InitializeEnergyManager();
        SetupEventListeners();
        StartRealtimeUpdates();
    }

    private void OnDestroy()
    {
        StopRealtimeUpdates();
        CleanupEventListeners();
    }

    private void OnEnable()
    {
        if (_energyManager != null)
        {
            RefreshDisplay();
            StartRealtimeUpdates();
        }
    }

    private void OnDisable()
    {
        StopRealtimeUpdates();
    }
    #endregion

    #region Initialization
    private void ValidateComponents()
    {
        if (energyBar == null)
            Debug.LogError("[EnergyDisplayUI] EnergyBar component is missing!");
            
        if (currentEnergyText == null)
            Debug.LogError("[EnergyDisplayUI] Current energy text component is missing!");
            
        if (maxEnergyText == null)
            Debug.LogError("[EnergyDisplayUI] Max energy text component is missing!");
            
        if (purchaseButton == null)
            Debug.LogWarning("[EnergyDisplayUI] Purchase button is not assigned");
            
        if (purchaseModal == null)
            Debug.LogWarning("[EnergyDisplayUI] Purchase modal is not assigned");
    }

    private void SetupUI()
    {
        // Setup purchase button
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        }

        // Initialize visual feedback elements
        SetLowEnergyWarning(false);
        SetFullEnergyEffect(false);

        // Set initial container opacity
        if (mainContainer != null)
        {
            mainContainer.alpha = 1.0f;
        }
    }

    private void InitializeEnergyManager()
    {
        // Get EnergyManager instance (provided by Stream A)
        _energyManager = EnergyManager.Instance;
        
        if (_energyManager == null)
        {
            Debug.LogError("[EnergyDisplayUI] EnergyManager.Instance is null! Cannot initialize.");
            return;
        }

        Debug.Log("[EnergyDisplayUI] Successfully connected to EnergyManager.Instance");
    }

    private void SetupEventListeners()
    {
        if (_energyManager != null)
        {
            _energyManager.OnEnergyChanged += OnEnergyChanged;
            _energyManager.OnEnergyDepleted += OnEnergyDepleted;
            _energyManager.OnEnergyFull += OnEnergyFull;
        }
    }

    private void CleanupEventListeners()
    {
        if (_energyManager != null)
        {
            _energyManager.OnEnergyChanged -= OnEnergyChanged;
            _energyManager.OnEnergyDepleted -= OnEnergyDepleted;
            _energyManager.OnEnergyFull -= OnEnergyFull;
        }
    }
    #endregion

    #region Real-time Updates
    /// <summary>
    /// 실시간 UI 업데이트 시작 (1초 간격)
    /// </summary>
    private void StartRealtimeUpdates()
    {
        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(RealtimeUpdateCoroutine());
        }
    }

    /// <summary>
    /// 실시간 UI 업데이트 중지
    /// </summary>
    private void StopRealtimeUpdates()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    /// <summary>
    /// 1초 간격 업데이트 코루틴
    /// </summary>
    private IEnumerator RealtimeUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UPDATE_INTERVAL);
            
            if (_energyManager != null && gameObject.activeInHierarchy)
            {
                UpdateDisplay();
                UpdateRecoveryTimer();
            }
        }
    }
    #endregion

    #region Display Updates
    /// <summary>
    /// 전체 디스플레이 업데이트
    /// </summary>
    public void UpdateDisplay()
    {
        if (_energyManager == null) return;

        var stateInfo = _energyManager.GetStateInfo();
        UpdateEnergyValues(stateInfo.CurrentEnergy, stateInfo.MaxEnergy);
        UpdateVisualStates(stateInfo);
    }

    /// <summary>
    /// 에너지 수치 업데이트
    /// </summary>
    private void UpdateEnergyValues(int currentEnergy, int maxEnergy)
    {
        // Check if values actually changed to avoid unnecessary updates
        if (currentEnergy == _lastCurrentEnergy && maxEnergy == _lastMaxEnergy)
            return;

        _lastCurrentEnergy = currentEnergy;
        _lastMaxEnergy = maxEnergy;

        // Update text displays
        if (currentEnergyText != null)
            currentEnergyText.text = currentEnergy.ToString();

        if (maxEnergyText != null)
            maxEnergyText.text = maxEnergy.ToString();

        if (energyRatioText != null)
            energyRatioText.text = $"{currentEnergy}/{maxEnergy}";

        // Update energy bar
        if (energyBar != null)
        {
            float percentage = maxEnergy > 0 ? (float)currentEnergy / maxEnergy : 0f;
            energyBar.UpdateEnergyBar(currentEnergy, maxEnergy, percentage);
        }

        // Fire update event
        OnEnergyDisplayUpdated?.Invoke(currentEnergy, maxEnergy);

        Debug.Log($"[EnergyDisplayUI] Updated display: {currentEnergy}/{maxEnergy}");
    }

    /// <summary>
    /// 시각적 상태 업데이트
    /// </summary>
    private void UpdateVisualStates(EnergyStateInfo stateInfo)
    {
        // Update low energy warning
        bool shouldShowLowWarning = stateInfo.IsEnergyLow;
        if (shouldShowLowWarning != _isLowEnergy)
        {
            _isLowEnergy = shouldShowLowWarning;
            SetLowEnergyWarning(_isLowEnergy);
            OnLowEnergyStateChanged?.Invoke(_isLowEnergy);
        }

        // Update full energy effect
        bool shouldShowFullEffect = stateInfo.IsEnergyFull;
        if (shouldShowFullEffect != _isFullEnergy)
        {
            _isFullEnergy = shouldShowFullEffect;
            SetFullEnergyEffect(_isFullEnergy);
        }

        // Update purchase button state
        if (purchaseButton != null)
        {
            // Enable purchase if not at max energy
            purchaseButton.interactable = !stateInfo.IsEnergyFull;
        }
    }

    /// <summary>
    /// 회복 타이머 업데이트
    /// </summary>
    private void UpdateRecoveryTimer()
    {
        if (recoveryTimerText == null || _energyManager == null) return;

        var stateInfo = _energyManager.GetStateInfo();
        
        if (stateInfo.IsEnergyFull)
        {
            recoveryTimerText.text = "에너지 가득참";
            SetTimerContainerActive(false);
        }
        else
        {
            // Get time until next recovery from recovery system
            var recoverySystem = EnergyRecoverySystem.Instance; // Assuming Stream A provides this
            if (recoverySystem != null)
            {
                var timeUntilNext = recoverySystem.TimeUntilNextRecharge;
                recoveryTimerText.text = $"다음 회복: {FormatTimeSpan(timeUntilNext)}";
                SetTimerContainerActive(true);
            }
            else
            {
                recoveryTimerText.text = "회복 중...";
                SetTimerContainerActive(true);
            }
        }
    }

    /// <summary>
    /// 즉시 디스플레이 새로고침
    /// </summary>
    public void RefreshDisplay()
    {
        if (_energyManager == null) return;

        UpdateDisplay();
        UpdateRecoveryTimer();
    }
    #endregion

    #region Visual Feedback
    /// <summary>
    /// 저에너지 경고 표시 제어
    /// </summary>
    private void SetLowEnergyWarning(bool show)
    {
        if (lowEnergyWarning != null)
        {
            lowEnergyWarning.SetActive(show);
            
            if (show)
            {
                Debug.Log("[EnergyDisplayUI] Low energy warning activated");
            }
        }
    }

    /// <summary>
    /// 가득찬 에너지 효과 제어
    /// </summary>
    private void SetFullEnergyEffect(bool show)
    {
        if (fullEnergyEffect != null)
        {
            fullEnergyEffect.SetActive(show);
            
            if (show)
            {
                Debug.Log("[EnergyDisplayUI] Full energy effect activated");
            }
        }
    }

    /// <summary>
    /// 타이머 컨테이너 표시 제어
    /// </summary>
    private void SetTimerContainerActive(bool active)
    {
        if (timerContainer != null)
        {
            timerContainer.SetActive(active);
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// EnergyManager 에너지 변경 이벤트 처리
    /// </summary>
    private void OnEnergyChanged(int currentEnergy, int maxEnergy)
    {
        // This will be handled by the real-time update coroutine
        // but we can trigger immediate update for responsiveness
        UpdateEnergyValues(currentEnergy, maxEnergy);
    }

    /// <summary>
    /// 에너지 고갈 이벤트 처리
    /// </summary>
    private void OnEnergyDepleted()
    {
        Debug.Log("[EnergyDisplayUI] Energy depleted!");
        
        // Force immediate visual update
        SetLowEnergyWarning(true);
        
        // Trigger any energy depletion feedback
        if (energyBar != null)
        {
            energyBar.TriggerDepletionEffect();
        }
    }

    /// <summary>
    /// 에너지 가득참 이벤트 처리
    /// </summary>
    private void OnEnergyFull()
    {
        Debug.Log("[EnergyDisplayUI] Energy full!");
        
        // Force immediate visual update
        SetFullEnergyEffect(true);
        SetLowEnergyWarning(false);
        
        // Trigger any full energy feedback
        if (energyBar != null)
        {
            energyBar.TriggerFullEnergyEffect();
        }
    }

    /// <summary>
    /// 구매 버튼 클릭 처리
    /// </summary>
    private void OnPurchaseButtonClicked()
    {
        Debug.Log("[EnergyDisplayUI] Purchase button clicked");
        
        OnPurchaseRequested?.Invoke();
        
        if (purchaseModal != null)
        {
            purchaseModal.ShowPurchaseModal();
        }
        else
        {
            Debug.LogWarning("[EnergyDisplayUI] Purchase modal is not assigned!");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 수동 업데이트 트리거
    /// </summary>
    public void TriggerUpdate()
    {
        RefreshDisplay();
    }

    /// <summary>
    /// 에너지 바 애니메이션 트리거
    /// </summary>
    public void TriggerEnergyBarAnimation()
    {
        if (energyBar != null && _energyManager != null)
        {
            var stateInfo = _energyManager.GetStateInfo();
            float percentage = stateInfo.MaxEnergy > 0 ? 
                (float)stateInfo.CurrentEnergy / stateInfo.MaxEnergy : 0f;
            
            energyBar.AnimateToValue(percentage);
        }
    }

    /// <summary>
    /// UI 활성화 상태 제어
    /// </summary>
    public void SetUIActive(bool active)
    {
        if (mainContainer != null)
        {
            mainContainer.alpha = active ? 1.0f : 0.5f;
            mainContainer.interactable = active;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 시간 형식화 (mm:ss 또는 hh:mm:ss)
    /// </summary>
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

    #region Debug Methods
    #if UNITY_EDITOR
    [ContextMenu("Simulate Low Energy")]
    private void SimulateLowEnergy()
    {
        SetLowEnergyWarning(true);
        if (energyBar != null)
        {
            energyBar.UpdateEnergyBar(10, 100, 0.1f);
        }
    }

    [ContextMenu("Simulate Full Energy")]
    private void SimulateFullEnergy()
    {
        SetFullEnergyEffect(true);
        SetLowEnergyWarning(false);
        if (energyBar != null)
        {
            energyBar.UpdateEnergyBar(100, 100, 1.0f);
        }
    }

    [ContextMenu("Force Refresh")]
    private void ForceRefresh()
    {
        RefreshDisplay();
    }
    #endif
    #endregion
}