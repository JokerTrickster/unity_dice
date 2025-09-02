using System;
using UnityEngine;

/// <summary>
/// 에너지 시스템 설정 관리
/// 게임의 에너지 관련 모든 설정값을 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "EnergyConfig", menuName = "Game Config/Energy Config")]
[System.Serializable]
public class EnergyConfig : ScriptableObject
{
    [Header("Basic Energy Settings")]
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int rechargeRate = 1;
    [SerializeField] private int rechargeIntervalMinutes = 5;
    
    [Header("Visual and Gameplay")]
    [SerializeField] [Range(0f, 1f)] private float lowEnergyThreshold = 0.2f;
    
    [Header("Economy Settings")]
    [SerializeField] private int maxEnergyPurchaseAmount = 50;
    [SerializeField] private int energyPurchaseCost = 100; // 코인 단위
    [SerializeField] private string defaultCurrency = "coins";
    
    [Header("Purchase Options")]
    [SerializeField] private EnergyPurchaseOption[] purchaseOptions = new EnergyPurchaseOption[]
    {
        new EnergyPurchaseOption { amount = 10, cost = 100, currency = "coins" },
        new EnergyPurchaseOption { amount = 25, cost = 200, currency = "coins" },
        new EnergyPurchaseOption { amount = 50, cost = 350, currency = "coins" }
    };
    
    [Header("Advanced Settings")]
    [SerializeField] private bool enableEnergyRecovery = true;
    [SerializeField] private bool enableEnergyPurchase = true;
    [SerializeField] private bool enableEnergyValidation = true;
    [SerializeField] private bool enableBackgroundRecovery = true;
    
    #region Properties
    /// <summary>최대 에너지</summary>
    public int MaxEnergy => maxEnergy;
    
    /// <summary>회복률 (분당 회복량)</summary>
    public int RechargeRate => rechargeRate;
    
    /// <summary>회복 간격 (분)</summary>
    public int RechargeIntervalMinutes => rechargeIntervalMinutes;
    
    /// <summary>회복 간격 (TimeSpan)</summary>
    public TimeSpan RechargeInterval => TimeSpan.FromMinutes(rechargeIntervalMinutes);
    
    /// <summary>에너지 부족 임계값 (0.0~1.0)</summary>
    public float LowEnergyThreshold => lowEnergyThreshold;
    
    /// <summary>최대 구매 가능 에너지량</summary>
    public int MaxEnergyPurchaseAmount => maxEnergyPurchaseAmount;
    
    /// <summary>에너지 구매 기본 비용</summary>
    public int EnergyPurchaseCost => energyPurchaseCost;
    
    /// <summary>기본 화폐 타입</summary>
    public string DefaultCurrency => defaultCurrency;
    
    /// <summary>구매 옵션들</summary>
    public EnergyPurchaseOption[] PurchaseOptions => purchaseOptions;
    
    /// <summary>자동 회복 활성화</summary>
    public bool EnableEnergyRecovery => enableEnergyRecovery;
    
    /// <summary>에너지 구매 활성화</summary>
    public bool EnableEnergyPurchase => enableEnergyPurchase;
    
    /// <summary>에너지 검증 활성화</summary>
    public bool EnableEnergyValidation => enableEnergyValidation;
    
    /// <summary>백그라운드 회복 활성화</summary>
    public bool EnableBackgroundRecovery => enableBackgroundRecovery;
    #endregion
    
    #region Static Instance (for ScriptableObject)
    private static EnergyConfig _instance;
    
    /// <summary>
    /// 기본 설정 인스턴스 가져오기
    /// Resources/Config/EnergyConfig.asset에서 로드
    /// </summary>
    public static EnergyConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<EnergyConfig>("Config/EnergyConfig");
                if (_instance == null)
                {
                    // 기본 설정으로 생성
                    _instance = CreateInstance<EnergyConfig>();
                    _instance.ResetToDefaults();
                    Debug.LogWarning("[EnergyConfig] Config asset not found, using default settings");
                }
            }
            return _instance;
        }
    }
    #endregion
    
    #region Configuration Methods
    /// <summary>
    /// 기본값으로 재설정
    /// </summary>
    public void ResetToDefaults()
    {
        maxEnergy = 100;
        rechargeRate = 1;
        rechargeIntervalMinutes = 5;
        lowEnergyThreshold = 0.2f;
        maxEnergyPurchaseAmount = 50;
        energyPurchaseCost = 100;
        defaultCurrency = "coins";
        enableEnergyRecovery = true;
        enableEnergyPurchase = true;
        enableEnergyValidation = true;
        enableBackgroundRecovery = true;
        
        purchaseOptions = new EnergyPurchaseOption[]
        {
            new EnergyPurchaseOption { amount = 10, cost = 100, currency = "coins" },
            new EnergyPurchaseOption { amount = 25, cost = 200, currency = "coins" },
            new EnergyPurchaseOption { amount = 50, cost = 350, currency = "coins" }
        };
    }
    
    /// <summary>
    /// 설정 유효성 검증
    /// </summary>
    public bool ValidateConfig()
    {
        bool isValid = true;
        
        if (maxEnergy <= 0)
        {
            Debug.LogError("[EnergyConfig] MaxEnergy must be greater than 0");
            isValid = false;
        }
        
        if (rechargeRate <= 0)
        {
            Debug.LogError("[EnergyConfig] RechargeRate must be greater than 0");
            isValid = false;
        }
        
        if (rechargeIntervalMinutes <= 0)
        {
            Debug.LogError("[EnergyConfig] RechargeIntervalMinutes must be greater than 0");
            isValid = false;
        }
        
        if (lowEnergyThreshold < 0f || lowEnergyThreshold > 1f)
        {
            Debug.LogError("[EnergyConfig] LowEnergyThreshold must be between 0 and 1");
            isValid = false;
        }
        
        return isValid;
    }
    
    /// <summary>
    /// 구매 옵션 검색
    /// </summary>
    public EnergyPurchaseOption GetPurchaseOption(int amount)
    {
        foreach (var option in purchaseOptions)
        {
            if (option.amount == amount)
                return option;
        }
        return null;
    }
    
    /// <summary>
    /// 에너지량에 따른 총 비용 계산
    /// </summary>
    public int CalculatePurchaseCost(int amount, string currency = null)
    {
        currency = currency ?? defaultCurrency;
        
        // 사전 정의된 옵션 중에서 찾기
        var option = GetPurchaseOption(amount);
        if (option != null && option.currency == currency)
        {
            return option.cost;
        }
        
        // 기본 비율로 계산
        return amount * energyPurchaseCost;
    }
    
    /// <summary>
    /// 설정을 다른 EnergyConfig로 복사
    /// </summary>
    public EnergyConfig Clone()
    {
        var clone = CreateInstance<EnergyConfig>();
        clone.maxEnergy = this.maxEnergy;
        clone.rechargeRate = this.rechargeRate;
        clone.rechargeIntervalMinutes = this.rechargeIntervalMinutes;
        clone.lowEnergyThreshold = this.lowEnergyThreshold;
        clone.maxEnergyPurchaseAmount = this.maxEnergyPurchaseAmount;
        clone.energyPurchaseCost = this.energyPurchaseCost;
        clone.defaultCurrency = this.defaultCurrency;
        clone.enableEnergyRecovery = this.enableEnergyRecovery;
        clone.enableEnergyPurchase = this.enableEnergyPurchase;
        clone.enableEnergyValidation = this.enableEnergyValidation;
        clone.enableBackgroundRecovery = this.enableBackgroundRecovery;
        
        // 구매 옵션 복사
        clone.purchaseOptions = new EnergyPurchaseOption[this.purchaseOptions.Length];
        for (int i = 0; i < this.purchaseOptions.Length; i++)
        {
            clone.purchaseOptions[i] = new EnergyPurchaseOption
            {
                amount = this.purchaseOptions[i].amount,
                cost = this.purchaseOptions[i].cost,
                currency = this.purchaseOptions[i].currency
            };
        }
        
        return clone;
    }
    #endregion
    
    #region Unity Editor
    #if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값 변경 시 유효성 검증
    /// </summary>
    private void OnValidate()
    {
        maxEnergy = Mathf.Max(1, maxEnergy);
        rechargeRate = Mathf.Max(1, rechargeRate);
        rechargeIntervalMinutes = Mathf.Max(1, rechargeIntervalMinutes);
        lowEnergyThreshold = Mathf.Clamp01(lowEnergyThreshold);
        maxEnergyPurchaseAmount = Mathf.Max(1, maxEnergyPurchaseAmount);
        energyPurchaseCost = Mathf.Max(1, energyPurchaseCost);
        
        if (string.IsNullOrEmpty(defaultCurrency))
            defaultCurrency = "coins";
    }
    #endif
    #endregion
}

/// <summary>
/// 에너지 구매 옵션 데이터
/// </summary>
[System.Serializable]
public class EnergyPurchaseOption
{
    [Header("Purchase Details")]
    public int amount;          // 구매할 에너지량
    public int cost;           // 필요 비용
    public string currency;    // 화폐 타입 ("coins", "gems" 등)
    
    [Header("Display")]
    public string displayName;  // UI 표시명
    public string description;  // 설명
    
    /// <summary>
    /// 구매 옵션 유효성 검증
    /// </summary>
    public bool IsValid()
    {
        return amount > 0 && cost > 0 && !string.IsNullOrEmpty(currency);
    }
    
    /// <summary>
    /// 에너지당 비용 계산
    /// </summary>
    public float CostPerEnergy => amount > 0 ? (float)cost / amount : 0f;
    
    /// <summary>
    /// 표시명 (자동 생성)
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(displayName))
            return displayName;
            
        return $"{amount} 에너지";
    }
    
    /// <summary>
    /// 설명 (자동 생성)
    /// </summary>
    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(description))
            return description;
            
        return $"{cost} {currency}로 {amount} 에너지 구매";
    }
}