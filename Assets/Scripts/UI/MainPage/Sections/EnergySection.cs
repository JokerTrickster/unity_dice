using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에너지 섹션 컨트롤러
/// 사용자의 에너지/스태미나 시스템을 관리하고 UI와 연동합니다.
/// 에너지 회복, 소비, 구매 등의 모든 에너지 관련 로직을 담당합니다.
/// </summary>
public class EnergySection : SectionBase
{
    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Energy;
    public override string SectionDisplayName => "에너지";
    #endregion

    #region UI Components
    [Header("UI References")]
    [SerializeField] private EnergySectionUI energyUI;
    [SerializeField] private GameObject energyNotificationPrefab;
    [SerializeField] private Transform notificationParent;
    #endregion

    #region Energy Management
    private EnergyManager _energyManager;
    private EnergyRecoverySystem _recoverySystem;
    private EnergyEconomySystem _economySystem;
    private EnergyValidationSystem _validationSystem;
    
    private Coroutine _energyRecoveryCoroutine;
    private Coroutine _energyUIUpdateCoroutine;
    
    // Energy Configuration
    private EnergyConfig _energyConfig;
    #endregion

    #region Events
    public static event Action<int, int> OnEnergyChanged; // current, max
    public static event Action OnEnergyDepleted;
    public static event Action OnEnergyRestored;
    public static event Action<int> OnEnergyPurchased; // amount
    public static event Action<EnergyTransactionResult> OnEnergyTransaction;
    #endregion

    #region SectionBase Implementation
    protected override void OnInitialize()
    {
        InitializeEnergyConfig();
        InitializeEnergySystems();
        InitializeUI();
        ValidateEnergyComponents();
        
        Debug.Log($"[EnergySection] Initialized with config: MaxEnergy={_energyConfig.MaxEnergy}, RechargeRate={_energyConfig.RechargeRate}");
    }

    protected override void OnActivate()
    {
        StartEnergyRecovery();
        StartUIUpdates();
        RefreshEnergyDisplay();
        
        // Subscribe to energy UI events
        if (energyUI != null)
        {
            energyUI.OnEnergyActionRequested += HandleEnergyActionRequest;
            energyUI.OnEnergyPurchaseRequested += HandleEnergyPurchaseRequest;
        }
    }

    protected override void OnDeactivate()
    {
        StopEnergyRecovery();
        StopUIUpdates();
        
        // Unsubscribe from energy UI events
        if (energyUI != null)
        {
            energyUI.OnEnergyActionRequested -= HandleEnergyActionRequest;
            energyUI.OnEnergyPurchaseRequested -= HandleEnergyPurchaseRequest;
        }
    }

    protected override void OnCleanup()
    {
        _energyManager?.Cleanup();
        _recoverySystem?.Cleanup();
        _economySystem?.Cleanup();
        _validationSystem?.Cleanup();
        
        StopAllCoroutines();
        
        // Clear events
        OnEnergyChanged = null;
        OnEnergyDepleted = null;
        OnEnergyRestored = null;
        OnEnergyPurchased = null;
        OnEnergyTransaction = null;
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null || energyUI == null) return;
        
        // Update energy manager with latest data
        _energyManager.UpdateFromUserData(userData);
        
        // Update UI
        energyUI.UpdateEnergyDisplay(
            userData.CurrentEnergy,
            userData.MaxEnergy,
            userData.EnergyPercentage,
            userData.TimeUntilNextRecharge
        );
        
        // Update visual state
        UpdateEnergyVisualState(userData);
        
        Debug.Log($"[EnergySection] UI updated - Energy: {userData.CurrentEnergy}/{userData.MaxEnergy} ({userData.EnergyPercentage:P1})");
    }

    protected override void ValidateComponents()
    {
        if (energyUI == null)
        {
            // Try to find in children
            energyUI = GetComponentInChildren<EnergySectionUI>();
            if (energyUI == null)
            {
                ReportError("EnergySectionUI component is missing!");
                return;
            }
        }
        
        if (notificationParent == null)
            Debug.LogWarning("[EnergySection] Notification parent not assigned");
            
        if (energyNotificationPrefab == null)
            Debug.LogWarning("[EnergySection] Energy notification prefab not assigned");
    }
    #endregion

    #region Energy System Initialization
    private void InitializeEnergyConfig()
    {
        // Load energy configuration from settings or use defaults
        _energyConfig = new EnergyConfig
        {
            MaxEnergy = GetSetting<int>("Energy.MaxEnergy", 100),
            RechargeRate = GetSetting<int>("Energy.RechargeRate", 1),
            RechargeInterval = TimeSpan.FromMinutes(GetSetting<float>("Energy.RechargeIntervalMinutes", 10f)),
            LowEnergyThreshold = GetSetting<float>("Energy.LowEnergyThreshold", 0.2f),
            MaxEnergyPurchaseAmount = GetSetting<int>("Energy.MaxPurchaseAmount", 50),
            EnergyPurchaseCost = GetSetting<int>("Energy.PurchaseCostPerUnit", 2)
        };
    }

    private void InitializeEnergySystems()
    {
        // Initialize energy manager
        _energyManager = new EnergyManager(_energyConfig);
        _energyManager.OnEnergyChanged += OnEnergyManagerChanged;
        _energyManager.OnEnergyDepleted += () => OnEnergyDepleted?.Invoke();
        
        // Initialize recovery system
        _recoverySystem = new EnergyRecoverySystem(_energyConfig, _energyManager);
        _recoverySystem.OnEnergyRecovered += OnEnergyRecovered;
        
        // Initialize economy system
        _economySystem = new EnergyEconomySystem(_energyConfig, _energyManager);
        _economySystem.OnEnergyPurchased += OnEnergyPurchaseCompleted;
        
        // Initialize validation system
        _validationSystem = new EnergyValidationSystem(_energyManager);
        
        // Load current user data
        if (_cachedUserData != null)
        {
            _energyManager.UpdateFromUserData(_cachedUserData);
        }
    }

    private void InitializeUI()
    {
        if (energyUI != null)
        {
            energyUI.Initialize(_energyConfig);
            energyUI.OnEnergyActionRequested += HandleEnergyActionRequest;
            energyUI.OnEnergyPurchaseRequested += HandleEnergyPurchaseRequest;
        }
    }
    #endregion

    #region Energy Recovery System
    private void StartEnergyRecovery()
    {
        if (_energyRecoveryCoroutine == null && _recoverySystem != null)
        {
            _energyRecoveryCoroutine = StartCoroutine(EnergyRecoveryCoroutine());
        }
    }

    private void StopEnergyRecovery()
    {
        if (_energyRecoveryCoroutine != null)
        {
            StopCoroutine(_energyRecoveryCoroutine);
            _energyRecoveryCoroutine = null;
        }
    }

    private IEnumerator EnergyRecoveryCoroutine()
    {
        while (_isActive && _recoverySystem != null)
        {
            yield return new WaitForSeconds(1f); // Check every second
            
            bool energyRecovered = _recoverySystem.TryRecoverEnergy();
            if (energyRecovered)
            {
                UpdateUserData();
                UpdateEnergyUI();
            }
        }
    }

    private void OnEnergyRecovered(int amount)
    {
        ShowEnergyNotification($"+{amount} 에너지", NotificationType.EnergyGain);
        Debug.Log($"[EnergySection] Energy recovered: +{amount}");
    }
    #endregion

    #region UI Updates
    private void StartUIUpdates()
    {
        if (_energyUIUpdateCoroutine == null)
        {
            _energyUIUpdateCoroutine = StartCoroutine(UIUpdateCoroutine());
        }
    }

    private void StopUIUpdates()
    {
        if (_energyUIUpdateCoroutine != null)
        {
            StopCoroutine(_energyUIUpdateCoroutine);
            _energyUIUpdateCoroutine = null;
        }
    }

    private IEnumerator UIUpdateCoroutine()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(0.5f); // Update UI every 0.5 seconds
            
            if (energyUI != null && _energyManager != null)
            {
                UpdateEnergyUI();
            }
        }
    }

    private void UpdateEnergyUI()
    {
        if (energyUI != null && _energyManager != null)
        {
            var currentEnergy = _energyManager.CurrentEnergy;
            var maxEnergy = _energyManager.MaxEnergy;
            var energyPercentage = _energyManager.EnergyPercentage;
            var timeUntilRecharge = _recoverySystem?.TimeUntilNextRecharge ?? TimeSpan.Zero;
            
            energyUI.UpdateEnergyDisplay(currentEnergy, maxEnergy, energyPercentage, timeUntilRecharge);
        }
    }

    private void RefreshEnergyDisplay()
    {
        if (_cachedUserData != null)
        {
            UpdateUI(_cachedUserData);
        }
        else
        {
            UpdateEnergyUI();
        }
    }

    private void UpdateEnergyVisualState(UserData userData)
    {
        if (energyUI != null)
        {
            energyUI.UpdateVisualState(
                userData.IsEnergyLow,
                userData.IsEnergyFull,
                userData.CanUseEnergy
            );
        }
    }
    #endregion

    #region Event Handlers
    private void OnEnergyManagerChanged(int currentEnergy, int maxEnergy)
    {
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        UpdateUserData();
        
        // Check for energy depletion
        if (currentEnergy == 0)
        {
            OnEnergyDepleted?.Invoke();
        }
        else if (currentEnergy > 0 && _cachedUserData?.CurrentEnergy == 0)
        {
            OnEnergyRestored?.Invoke();
        }
    }

    private void HandleEnergyActionRequest(EnergyActionRequest request)
    {
        switch (request.ActionType)
        {
            case EnergyActionType.Consume:
                ConsumeEnergy(request.Amount, request.Source);
                break;
                
            case EnergyActionType.Restore:
                AddEnergy(request.Amount, request.Source);
                break;
                
            case EnergyActionType.SetMax:
                SetMaxEnergy(request.Amount);
                break;
                
            case EnergyActionType.ForceRecharge:
                ForceRecharge();
                break;
        }
    }

    private void HandleEnergyPurchaseRequest(EnergyPurchaseRequest request)
    {
        if (_economySystem != null && !_isOfflineMode)
        {
            var result = _economySystem.PurchaseEnergy(request.Amount);
            OnEnergyTransaction?.Invoke(result);
            
            if (result.Success)
            {
                OnEnergyPurchased?.Invoke(request.Amount);
                ShowEnergyNotification($"+{request.Amount} 에너지 구매!", NotificationType.EnergyPurchase);
                UpdateUserData();
                UpdateEnergyUI();
            }
            else
            {
                ShowEnergyNotification(result.ErrorMessage, NotificationType.Error);
            }
        }
    }

    private void OnEnergyPurchaseCompleted(int amount, int cost)
    {
        Debug.Log($"[EnergySection] Energy purchased: +{amount} for {cost} coins");
    }
    #endregion

    #region Public Energy Operations
    /// <summary>
    /// 에너지 소모
    /// </summary>
    public bool ConsumeEnergy(int amount, string source = "unknown")
    {
        if (_energyManager == null) return false;
        
        bool success = _energyManager.ConsumeEnergy(amount);
        if (success)
        {
            ShowEnergyNotification($"-{amount} 에너지", NotificationType.EnergyConsume);
            Debug.Log($"[EnergySection] Energy consumed: -{amount} from {source}");
        }
        else
        {
            ShowEnergyNotification("에너지 부족!", NotificationType.Warning);
            Debug.LogWarning($"[EnergySection] Insufficient energy for {amount} consumption from {source}");
        }
        
        return success;
    }

    /// <summary>
    /// 에너지 추가
    /// </summary>
    public void AddEnergy(int amount, string source = "unknown")
    {
        if (_energyManager == null) return;
        
        _energyManager.AddEnergy(amount);
        ShowEnergyNotification($"+{amount} 에너지", NotificationType.EnergyGain);
        Debug.Log($"[EnergySection] Energy added: +{amount} from {source}");
    }

    /// <summary>
    /// 최대 에너지 설정
    /// </summary>
    public void SetMaxEnergy(int maxEnergy)
    {
        if (_energyManager == null) return;
        
        _energyManager.SetMaxEnergy(maxEnergy);
        Debug.Log($"[EnergySection] Max energy set to: {maxEnergy}");
    }

    /// <summary>
    /// 강제 에너지 충전
    /// </summary>
    public void ForceRecharge()
    {
        if (_recoverySystem == null) return;
        
        _recoverySystem.ForceRecharge();
        ShowEnergyNotification("에너지 충전!", NotificationType.EnergyGain);
        UpdateUserData();
        UpdateEnergyUI();
        Debug.Log("[EnergySection] Force recharge executed");
    }

    /// <summary>
    /// 에너지 사용 가능 여부 확인
    /// </summary>
    public bool CanUseEnergy(int amount = 1)
    {
        return _validationSystem?.CanUseEnergy(amount) ?? false;
    }

    /// <summary>
    /// 에너지 구매 가능 여부 확인
    /// </summary>
    public bool CanPurchaseEnergy(int amount)
    {
        return _economySystem?.CanPurchaseEnergy(amount) ?? false;
    }

    /// <summary>
    /// 현재 에너지 상태 정보 반환
    /// </summary>
    public EnergyStatus GetEnergyStatus()
    {
        if (_energyManager == null) return null;
        
        return new EnergyStatus
        {
            CurrentEnergy = _energyManager.CurrentEnergy,
            MaxEnergy = _energyManager.MaxEnergy,
            EnergyPercentage = _energyManager.EnergyPercentage,
            IsEnergyLow = _energyManager.IsEnergyLow,
            IsEnergyFull = _energyManager.IsEnergyFull,
            CanUseEnergy = _energyManager.CanUseEnergy,
            TimeUntilNextRecharge = _recoverySystem?.TimeUntilNextRecharge ?? TimeSpan.Zero,
            CanRechargeNow = _recoverySystem?.CanRechargeNow ?? false
        };
    }
    #endregion

    #region Message Handling
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[EnergySection] Received message from {fromSection}: {data?.GetType().Name}");
        
        switch (data)
        {
            case EnergyRequest energyRequest:
                HandleEnergyRequest(energyRequest, fromSection);
                break;
                
            case GameActionRequest gameAction when gameAction.RequiresEnergy:
                HandleGameActionEnergyCheck(gameAction, fromSection);
                break;
        }
    }

    private void HandleEnergyRequest(EnergyRequest request, MainPageSectionType requester)
    {
        switch (request.RequestType.ToLower())
        {
            case "consume":
                var success = ConsumeEnergy(request.Amount, requester.ToString());
                SendMessageToSection(requester, new EnergyResponse
                {
                    Success = success,
                    CurrentEnergy = _energyManager?.CurrentEnergy ?? 0,
                    RequestId = request.RequestId
                });
                break;
                
            case "check":
                SendMessageToSection(requester, new EnergyResponse
                {
                    Success = true,
                    CurrentEnergy = _energyManager?.CurrentEnergy ?? 0,
                    MaxEnergy = _energyManager?.MaxEnergy ?? 0,
                    CanUseEnergy = CanUseEnergy(request.Amount),
                    RequestId = request.RequestId
                });
                break;
                
            case "add":
                AddEnergy(request.Amount, requester.ToString());
                SendMessageToSection(requester, new EnergyResponse
                {
                    Success = true,
                    CurrentEnergy = _energyManager?.CurrentEnergy ?? 0,
                    RequestId = request.RequestId
                });
                break;
        }
    }

    private void HandleGameActionEnergyCheck(GameActionRequest action, MainPageSectionType requester)
    {
        bool hasEnoughEnergy = CanUseEnergy(action.EnergyCost);
        
        SendMessageToSection(requester, new GameActionEnergyResponse
        {
            ActionId = action.ActionId,
            HasEnoughEnergy = hasEnoughEnergy,
            CurrentEnergy = _energyManager?.CurrentEnergy ?? 0,
            EnergyCost = action.EnergyCost
        });
    }
    #endregion

    #region Utility Methods
    private void UpdateUserData()
    {
        if (_userDataManager?.CurrentUser != null && _energyManager != null)
        {
            var userData = _userDataManager.CurrentUser;
            userData.CurrentEnergy = _energyManager.CurrentEnergy;
            userData.MaxEnergy = _energyManager.MaxEnergy;
            userData.LastEnergyRechargeTime = _recoverySystem?.LastRechargeTime ?? userData.LastEnergyRechargeTime;
            
            // Notify UserDataManager of changes
            _userDataManager.UpdateCurrentUser(userData);
        }
    }

    private void ShowEnergyNotification(string message, NotificationType type)
    {
        if (energyNotificationPrefab != null && notificationParent != null)
        {
            var notification = Instantiate(energyNotificationPrefab, notificationParent);
            var notificationComponent = notification.GetComponent<EnergyNotification>();
            notificationComponent?.Show(message, type);
        }
        else
        {
            Debug.Log($"[EnergySection] Notification: {message} ({type})");
        }
    }

    private T GetSetting<T>(string settingName, T defaultValue)
    {
        return _settingsManager != null ? _settingsManager.GetSetting<T>(settingName, defaultValue) : defaultValue;
    }
    #endregion

    #region Settings Override
    protected override void OnSettingUpdated(string settingName, object newValue)
    {
        if (settingName.StartsWith("Energy."))
        {
            // Reload energy configuration
            InitializeEnergyConfig();
            _energyManager?.UpdateConfig(_energyConfig);
            _recoverySystem?.UpdateConfig(_energyConfig);
            _economySystem?.UpdateConfig(_energyConfig);
            
            Debug.Log($"[EnergySection] Energy config updated: {settingName} = {newValue}");
        }
    }

    protected override void OnForceRefresh()
    {
        RefreshEnergyDisplay();
        _recoverySystem?.ForceUpdate();
        UpdateEnergyUI();
    }

    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        base.OnOfflineModeChanged(isOfflineMode);
        
        // Update economy system
        _economySystem?.SetOfflineMode(isOfflineMode);
        
        // Update UI
        if (energyUI != null)
        {
            energyUI.SetOfflineMode(isOfflineMode);
        }
    }
    #endregion
}