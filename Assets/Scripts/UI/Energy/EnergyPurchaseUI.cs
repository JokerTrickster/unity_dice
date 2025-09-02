using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 에너지 구매 UI 컴포넌트
/// 에너지 구매 모달창과 확인 다이얼로그를 관리합니다.
/// EnergyManager와 연동하여 구매 처리 및 피드백을 제공합니다.
/// </summary>
public class EnergyPurchaseUI : MonoBehaviour
{
    #region Purchase Option Data
    [Serializable]
    public class PurchaseOption
    {
        [Header("Purchase Details")]
        public int energyAmount;
        public int cost;
        public string currencyType = "coins";
        
        [Header("Visual")]
        public string displayName;
        public string description;
        public bool isRecommended;
        public bool isSpecialOffer;
        
        [Header("Bonus")]
        public int bonusAmount = 0;
        public string bonusDescription;
    }
    #endregion

    #region UI References
    [Header("Modal Container")]
    [SerializeField] private GameObject modalContainer;
    [SerializeField] private CanvasGroup modalCanvasGroup;
    [SerializeField] private Button backgroundCloseButton;
    [SerializeField] private Button closeButton;
    
    [Header("Purchase Options")]
    [SerializeField] private Transform purchaseOptionsContainer;
    [SerializeField] private GameObject purchaseOptionPrefab;
    [SerializeField] private List<PurchaseOption> purchaseOptions = new List<PurchaseOption>();
    
    [Header("Confirmation Dialog")]
    [SerializeField] private GameObject confirmationDialog;
    [SerializeField] private Text confirmationTitle;
    [SerializeField] private Text confirmationMessage;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    
    [Header("User Info Display")]
    [SerializeField] private Text currentEnergyText;
    [SerializeField] private Text currentCurrencyText;
    [SerializeField] private Text maxEnergyText;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private Text statusMessage;
    [SerializeField] private Image successIcon;
    [SerializeField] private Image errorIcon;
    #endregion

    #region Private Fields
    private List<GameObject> _purchaseOptionInstances = new List<GameObject>();
    private PurchaseOption _selectedOption;
    private bool _isModalVisible = false;
    private bool _isProcessingPurchase = false;
    
    // Animation
    private Coroutine _showAnimationCoroutine;
    private Coroutine _hideAnimationCoroutine;
    private Coroutine _statusMessageCoroutine;
    
    // Managers
    private EnergyManager _energyManager;
    private EnergyEconomySystem _economySystem;
    #endregion

    #region Events
    public static event Action<PurchaseOption> OnPurchaseRequested;
    public static event Action<PurchaseOption> OnPurchaseCompleted;
    public static event Action<PurchaseOption, string> OnPurchaseFailed;
    public static event Action OnModalOpened;
    public static event Action OnModalClosed;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        SetupEventListeners();
        CreateDefaultPurchaseOptions();
    }

    private void Start()
    {
        InitializeManagers();
        HideModal();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
        CleanupPurchaseOptions();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Validate required components
        if (modalContainer == null)
        {
            Debug.LogError("[EnergyPurchaseUI] Modal container is missing!");
            return;
        }

        if (modalCanvasGroup == null)
        {
            modalCanvasGroup = modalContainer.GetComponent<CanvasGroup>();
            if (modalCanvasGroup == null)
            {
                modalCanvasGroup = modalContainer.AddComponent<CanvasGroup>();
            }
        }

        if (purchaseOptionsContainer == null)
        {
            Debug.LogError("[EnergyPurchaseUI] Purchase options container is missing!");
        }

        if (purchaseOptionPrefab == null)
        {
            Debug.LogWarning("[EnergyPurchaseUI] Purchase option prefab is not assigned!");
        }
    }

    private void SetupEventListeners()
    {
        if (backgroundCloseButton != null)
        {
            backgroundCloseButton.onClick.AddListener(HideModal);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideModal);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmPurchase);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelPurchase);
        }
    }

    private void CleanupEventListeners()
    {
        if (backgroundCloseButton != null)
        {
            backgroundCloseButton.onClick.RemoveListener(HideModal);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideModal);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmPurchase);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelPurchase);
        }
    }

    private void InitializeManagers()
    {
        _energyManager = EnergyManager.Instance;
        _economySystem = EnergyEconomySystem.Instance; // Assuming Stream A provides this

        if (_energyManager == null)
        {
            Debug.LogError("[EnergyPurchaseUI] EnergyManager.Instance is null!");
        }

        if (_economySystem == null)
        {
            Debug.LogWarning("[EnergyPurchaseUI] EnergyEconomySystem.Instance is not available!");
        }
    }

    private void CreateDefaultPurchaseOptions()
    {
        if (purchaseOptions.Count == 0)
        {
            // Create default purchase options
            purchaseOptions.Add(new PurchaseOption
            {
                energyAmount = 10,
                cost = 100,
                currencyType = "coins",
                displayName = "작은 에너지",
                description = "에너지 10개",
                isRecommended = false
            });

            purchaseOptions.Add(new PurchaseOption
            {
                energyAmount = 25,
                cost = 200,
                currencyType = "coins", 
                displayName = "보통 에너지",
                description = "에너지 25개",
                bonusAmount = 5,
                bonusDescription = "+5 보너스!",
                isRecommended = true
            });

            purchaseOptions.Add(new PurchaseOption
            {
                energyAmount = 50,
                cost = 350,
                currencyType = "coins",
                displayName = "대형 에너지",
                description = "에너지 50개",
                bonusAmount = 15,
                bonusDescription = "+15 보너스!",
                isSpecialOffer = true
            });
        }
    }
    #endregion

    #region Modal Management
    /// <summary>
    /// 구매 모달 표시
    /// </summary>
    public void ShowPurchaseModal()
    {
        if (_isModalVisible) return;

        Debug.Log("[EnergyPurchaseUI] Showing purchase modal");

        UpdateUserInfo();
        GeneratePurchaseOptions();
        ShowModal();
        
        OnModalOpened?.Invoke();
    }

    /// <summary>
    /// 모달 표시 (애니메이션 포함)
    /// </summary>
    private void ShowModal()
    {
        if (_hideAnimationCoroutine != null)
        {
            StopCoroutine(_hideAnimationCoroutine);
            _hideAnimationCoroutine = null;
        }

        modalContainer.SetActive(true);
        _isModalVisible = true;

        _showAnimationCoroutine = StartCoroutine(ShowModalCoroutine());
    }

    /// <summary>
    /// 모달 숨김
    /// </summary>
    public void HideModal()
    {
        if (!_isModalVisible) return;

        Debug.Log("[EnergyPurchaseUI] Hiding purchase modal");

        if (_showAnimationCoroutine != null)
        {
            StopCoroutine(_showAnimationCoroutine);
            _showAnimationCoroutine = null;
        }

        _hideAnimationCoroutine = StartCoroutine(HideModalCoroutine());
        
        OnModalClosed?.Invoke();
    }

    private IEnumerator ShowModalCoroutine()
    {
        if (modalCanvasGroup == null) yield break;

        modalCanvasGroup.alpha = 0f;
        modalCanvasGroup.interactable = false;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            modalCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            
            yield return null;
        }

        modalCanvasGroup.alpha = 1f;
        modalCanvasGroup.interactable = true;
        
        _showAnimationCoroutine = null;
    }

    private IEnumerator HideModalCoroutine()
    {
        if (modalCanvasGroup == null) yield break;

        modalCanvasGroup.interactable = false;

        float duration = 0.2f;
        float elapsed = 0f;
        float startAlpha = modalCanvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            modalCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            
            yield return null;
        }

        modalCanvasGroup.alpha = 0f;
        modalContainer.SetActive(false);
        _isModalVisible = false;
        
        _hideAnimationCoroutine = null;
    }
    #endregion

    #region Purchase Options Management
    private void GeneratePurchaseOptions()
    {
        CleanupPurchaseOptions();

        if (purchaseOptionsContainer == null || purchaseOptionPrefab == null) return;

        foreach (var option in purchaseOptions)
        {
            CreatePurchaseOptionButton(option);
        }
    }

    private void CreatePurchaseOptionButton(PurchaseOption option)
    {
        GameObject optionInstance = Instantiate(purchaseOptionPrefab, purchaseOptionsContainer);
        _purchaseOptionInstances.Add(optionInstance);

        // Setup option UI
        SetupPurchaseOptionUI(optionInstance, option);

        // Add click listener
        Button optionButton = optionInstance.GetComponent<Button>();
        if (optionButton != null)
        {
            optionButton.onClick.AddListener(() => OnPurchaseOptionSelected(option));
        }
    }

    private void SetupPurchaseOptionUI(GameObject optionInstance, PurchaseOption option)
    {
        // Find and setup UI components
        Text nameText = optionInstance.transform.Find("NameText")?.GetComponent<Text>();
        Text descriptionText = optionInstance.transform.Find("DescriptionText")?.GetComponent<Text>();
        Text costText = optionInstance.transform.Find("CostText")?.GetComponent<Text>();
        GameObject recommendedTag = optionInstance.transform.Find("RecommendedTag")?.gameObject;
        GameObject specialOfferTag = optionInstance.transform.Find("SpecialOfferTag")?.gameObject;

        if (nameText != null)
        {
            nameText.text = option.displayName;
        }

        if (descriptionText != null)
        {
            string description = option.description;
            if (option.bonusAmount > 0)
            {
                description += $"\n{option.bonusDescription}";
            }
            descriptionText.text = description;
        }

        if (costText != null)
        {
            costText.text = $"{option.cost} {GetCurrencySymbol(option.currencyType)}";
        }

        if (recommendedTag != null)
        {
            recommendedTag.SetActive(option.isRecommended);
        }

        if (specialOfferTag != null)
        {
            specialOfferTag.SetActive(option.isSpecialOffer);
        }
    }

    private void CleanupPurchaseOptions()
    {
        foreach (var instance in _purchaseOptionInstances)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        _purchaseOptionInstances.Clear();
    }
    #endregion

    #region User Info Display
    private void UpdateUserInfo()
    {
        if (_energyManager == null) return;

        var stateInfo = _energyManager.GetStateInfo();

        if (currentEnergyText != null)
        {
            currentEnergyText.text = stateInfo.CurrentEnergy.ToString();
        }

        if (maxEnergyText != null)
        {
            maxEnergyText.text = stateInfo.MaxEnergy.ToString();
        }

        if (currentCurrencyText != null)
        {
            // Get currency from currency manager (assuming it exists)
            int currentCoins = GetCurrentCurrency("coins");
            currentCurrencyText.text = $"{currentCoins} {GetCurrencySymbol("coins")}";
        }
    }
    #endregion

    #region Purchase Processing
    private void OnPurchaseOptionSelected(PurchaseOption option)
    {
        Debug.Log($"[EnergyPurchaseUI] Purchase option selected: {option.displayName}");

        _selectedOption = option;
        ShowConfirmationDialog(option);
    }

    private void ShowConfirmationDialog(PurchaseOption option)
    {
        if (confirmationDialog == null) return;

        confirmationDialog.SetActive(true);

        if (confirmationTitle != null)
        {
            confirmationTitle.text = "구매 확인";
        }

        if (confirmationMessage != null)
        {
            int totalEnergy = option.energyAmount + option.bonusAmount;
            confirmationMessage.text = $"{option.displayName}을(를) {option.cost} {GetCurrencySymbol(option.currencyType)}에 구매하시겠습니까?\n\n" +
                                     $"획득 에너지: {totalEnergy}개";
        }
    }

    private void OnConfirmPurchase()
    {
        if (_selectedOption == null || _isProcessingPurchase) return;

        Debug.Log($"[EnergyPurchaseUI] Processing purchase: {_selectedOption.displayName}");

        HideConfirmationDialog();
        StartCoroutine(ProcessPurchaseCoroutine(_selectedOption));
    }

    private void OnCancelPurchase()
    {
        Debug.Log("[EnergyPurchaseUI] Purchase cancelled");
        HideConfirmationDialog();
        _selectedOption = null;
    }

    private void HideConfirmationDialog()
    {
        if (confirmationDialog != null)
        {
            confirmationDialog.SetActive(false);
        }
    }

    private IEnumerator ProcessPurchaseCoroutine(PurchaseOption option)
    {
        _isProcessingPurchase = true;
        ShowLoadingState(true);

        OnPurchaseRequested?.Invoke(option);

        // Check if user has enough currency
        if (!CanAffordPurchase(option))
        {
            ShowStatusMessage("재화가 부족합니다!", false);
            OnPurchaseFailed?.Invoke(option, "Insufficient currency");
            _isProcessingPurchase = false;
            ShowLoadingState(false);
            yield break;
        }

        // Simulate network delay
        yield return new WaitForSeconds(1f);

        // Process purchase through economy system
        bool purchaseSuccess = false;
        string errorMessage = "";

        if (_economySystem != null)
        {
            try
            {
                int totalEnergyToAdd = option.energyAmount + option.bonusAmount;
                var result = _economySystem.PurchaseEnergy(totalEnergyToAdd);
                purchaseSuccess = result.Success;
                errorMessage = result.ErrorMessage;
            }
            catch (System.Exception e)
            {
                purchaseSuccess = false;
                errorMessage = e.Message;
            }
        }
        else
        {
            // Fallback direct purchase
            purchaseSuccess = ProcessDirectPurchase(option);
        }

        ShowLoadingState(false);

        if (purchaseSuccess)
        {
            ShowStatusMessage("구매 완료!", true);
            OnPurchaseCompleted?.Invoke(option);
            
            // Update user info display
            UpdateUserInfo();
            
            // Auto-close modal after success
            yield return new WaitForSeconds(1.5f);
            HideModal();
        }
        else
        {
            ShowStatusMessage($"구매 실패: {errorMessage}", false);
            OnPurchaseFailed?.Invoke(option, errorMessage);
        }

        _isProcessingPurchase = false;
        _selectedOption = null;
    }

    private bool ProcessDirectPurchase(PurchaseOption option)
    {
        try
        {
            // Deduct currency
            if (!DeductCurrency(option.currencyType, option.cost))
            {
                return false;
            }

            // Add energy
            int totalEnergyToAdd = option.energyAmount + option.bonusAmount;
            _energyManager.AddEnergy(totalEnergyToAdd);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EnergyPurchaseUI] Direct purchase failed: {e.Message}");
            return false;
        }
    }
    #endregion

    #region UI Feedback
    private void ShowLoadingState(bool show)
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(show);
        }
    }

    private void ShowStatusMessage(string message, bool isSuccess)
    {
        if (statusMessage == null) return;

        if (_statusMessageCoroutine != null)
        {
            StopCoroutine(_statusMessageCoroutine);
        }

        _statusMessageCoroutine = StartCoroutine(ShowStatusMessageCoroutine(message, isSuccess));
    }

    private IEnumerator ShowStatusMessageCoroutine(string message, bool isSuccess)
    {
        statusMessage.text = message;
        statusMessage.gameObject.SetActive(true);

        if (successIcon != null && errorIcon != null)
        {
            successIcon.gameObject.SetActive(isSuccess);
            errorIcon.gameObject.SetActive(!isSuccess);
        }

        yield return new WaitForSeconds(2f);

        statusMessage.gameObject.SetActive(false);
        
        if (successIcon != null)
            successIcon.gameObject.SetActive(false);
        if (errorIcon != null)
            errorIcon.gameObject.SetActive(false);

        _statusMessageCoroutine = null;
    }
    #endregion

    #region Currency Integration
    private bool CanAffordPurchase(PurchaseOption option)
    {
        int currentCurrency = GetCurrentCurrency(option.currencyType);
        return currentCurrency >= option.cost;
    }

    private int GetCurrentCurrency(string currencyType)
    {
        // Integration point with currency system
        // For now, return a mock value
        return 1000; // Mock value
    }

    private bool DeductCurrency(string currencyType, int amount)
    {
        // Integration point with currency system
        // For now, simulate successful deduction
        return true; // Mock implementation
    }

    private string GetCurrencySymbol(string currencyType)
    {
        switch (currencyType.ToLower())
        {
            case "coins":
                return "💰";
            case "gems":
                return "💎";
            default:
                return "🪙";
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 구매 옵션 추가
    /// </summary>
    public void AddPurchaseOption(PurchaseOption option)
    {
        if (option != null)
        {
            purchaseOptions.Add(option);
        }
    }

    /// <summary>
    /// 모달 표시 상태 확인
    /// </summary>
    public bool IsModalVisible()
    {
        return _isModalVisible;
    }

    /// <summary>
    /// 구매 처리 중 상태 확인
    /// </summary>
    public bool IsProcessingPurchase()
    {
        return _isProcessingPurchase;
    }
    #endregion

    #region Editor Methods
    #if UNITY_EDITOR
    [ContextMenu("Test Show Modal")]
    private void TestShowModal()
    {
        ShowPurchaseModal();
    }

    [ContextMenu("Test Hide Modal")]
    private void TestHideModal()
    {
        HideModal();
    }

    [ContextMenu("Test Purchase Success")]
    private void TestPurchaseSuccess()
    {
        ShowStatusMessage("구매 완료!", true);
    }

    [ContextMenu("Test Purchase Failure")]
    private void TestPurchaseFailure()
    {
        ShowStatusMessage("구매 실패!", false);
    }
    #endif
    #endregion
}