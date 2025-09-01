using System;
using UnityEngine;

/// <summary>
/// 에너지 경제 시스템
/// 에너지 구매, 가격 계산, 경제적 유효성 검사를 담당합니다.
/// </summary>
public class EnergyEconomySystem
{
    #region Events
    public event Action<int, int> OnEnergyPurchased; // amount, cost
    public event Action<EnergyTransactionResult> OnTransactionCompleted;
    public event Action<string> OnTransactionFailed;
    #endregion

    #region Properties
    public bool IsOfflineMode { get; private set; }
    public int BasePurchaseCost => _config.EnergyPurchaseCost;
    public int MaxPurchaseAmount => _config.MaxEnergyPurchaseAmount;
    public bool IsPurchaseEnabled => !IsOfflineMode && _energyManager != null;
    #endregion

    #region Private Fields
    private EnergyConfig _config;
    private EnergyManager _energyManager;
    private UserDataManager _userDataManager;
    
    // Transaction tracking
    private int _totalPurchasedEnergy;
    private int _totalSpentCurrency;
    private int _transactionCount;
    private DateTime _lastPurchaseTime;
    #endregion

    #region Constructor
    public EnergyEconomySystem(EnergyConfig config, EnergyManager energyManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _energyManager = energyManager ?? throw new ArgumentNullException(nameof(energyManager));
        
        Initialize();
    }
    #endregion

    #region Initialization
    private void Initialize()
    {
        _userDataManager = UserDataManager.Instance;
        IsOfflineMode = false;
        _totalPurchasedEnergy = 0;
        _totalSpentCurrency = 0;
        _transactionCount = 0;
        _lastPurchaseTime = DateTime.MinValue;
        
        Debug.Log($"[EnergyEconomySystem] Initialized with base cost: {BasePurchaseCost}, max amount: {MaxPurchaseAmount}");
    }

    public void UpdateConfig(EnergyConfig newConfig)
    {
        if (newConfig == null) return;
        
        _config = newConfig;
        Debug.Log($"[EnergyEconomySystem] Config updated");
    }

    public void SetOfflineMode(bool isOffline)
    {
        IsOfflineMode = isOffline;
        Debug.Log($"[EnergyEconomySystem] Offline mode: {isOffline}");
    }
    #endregion

    #region Purchase Operations
    /// <summary>
    /// 에너지 구매
    /// </summary>
    public EnergyTransactionResult PurchaseEnergy(int amount)
    {
        var result = new EnergyTransactionResult
        {
            RequestedAmount = amount,
            ActualAmount = 0,
            Cost = 0,
            Success = false,
            TransactionId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.Now
        };

        // 기본 유효성 검사
        var validationResult = ValidatePurchaseRequest(amount);
        if (!validationResult.IsValid)
        {
            result.ErrorMessage = validationResult.ErrorMessage;
            OnTransactionCompleted?.Invoke(result);
            OnTransactionFailed?.Invoke(validationResult.ErrorMessage);
            return result;
        }

        // 실제 구매 가능한 양 계산
        int actualAmount = CalculateActualPurchaseAmount(amount);
        int totalCost = CalculatePurchaseCost(actualAmount);

        // 사용자 통화 확인 및 차감
        if (!ProcessPayment(totalCost))
        {
            result.ErrorMessage = "통화가 부족합니다.";
            OnTransactionCompleted?.Invoke(result);
            OnTransactionFailed?.Invoke(result.ErrorMessage);
            return result;
        }

        // 에너지 추가
        _energyManager.AddEnergy(actualAmount);

        // 트랜잭션 완료
        result.ActualAmount = actualAmount;
        result.Cost = totalCost;
        result.Success = true;
        result.NewEnergyAmount = _energyManager.CurrentEnergy;
        result.NewMaxEnergy = _energyManager.MaxEnergy;

        // 통계 업데이트
        UpdatePurchaseStats(actualAmount, totalCost);

        // 이벤트 발생
        OnEnergyPurchased?.Invoke(actualAmount, totalCost);
        OnTransactionCompleted?.Invoke(result);

        Debug.Log($"[EnergyEconomySystem] Purchase completed: {actualAmount} energy for {totalCost} currency");
        return result;
    }

    /// <summary>
    /// 에너지 구매 가능 여부 확인
    /// </summary>
    public bool CanPurchaseEnergy(int amount)
    {
        var validation = ValidatePurchaseRequest(amount);
        if (!validation.IsValid) return false;

        int actualAmount = CalculateActualPurchaseAmount(amount);
        int cost = CalculatePurchaseCost(actualAmount);
        
        return HasSufficientCurrency(cost);
    }

    /// <summary>
    /// 에너지 구매 견적 계산
    /// </summary>
    public EnergyPurchaseQuote GetPurchaseQuote(int amount)
    {
        var validation = ValidatePurchaseRequest(amount);
        
        var quote = new EnergyPurchaseQuote
        {
            RequestedAmount = amount,
            IsValid = validation.IsValid,
            ErrorMessage = validation.ErrorMessage
        };

        if (validation.IsValid)
        {
            quote.ActualAmount = CalculateActualPurchaseAmount(amount);
            quote.TotalCost = CalculatePurchaseCost(quote.ActualAmount);
            quote.CostPerUnit = quote.ActualAmount > 0 ? (float)quote.TotalCost / quote.ActualAmount : 0f;
            quote.HasSufficientCurrency = HasSufficientCurrency(quote.TotalCost);
            quote.EnergyAfterPurchase = _energyManager.CurrentEnergy + quote.ActualAmount;
        }

        return quote;
    }
    #endregion

    #region Validation
    private EnergyPurchaseValidation ValidatePurchaseRequest(int amount)
    {
        var validation = new EnergyPurchaseValidation { IsValid = false };

        if (!IsPurchaseEnabled)
        {
            validation.ErrorMessage = "에너지 구매가 비활성화되어 있습니다.";
            return validation;
        }

        if (amount <= 0)
        {
            validation.ErrorMessage = "구매 수량은 0보다 커야 합니다.";
            return validation;
        }

        if (amount > MaxPurchaseAmount)
        {
            validation.ErrorMessage = $"최대 구매 가능 수량은 {MaxPurchaseAmount}개입니다.";
            return validation;
        }

        if (_energyManager.IsEnergyFull)
        {
            validation.ErrorMessage = "에너지가 이미 가득 찼습니다.";
            return validation;
        }

        validation.IsValid = true;
        return validation;
    }
    #endregion

    #region Cost Calculation
    /// <summary>
    /// 구매 비용 계산
    /// </summary>
    public int CalculatePurchaseCost(int amount)
    {
        if (amount <= 0) return 0;

        // 기본 비용 계산
        int baseCost = amount * BasePurchaseCost;

        // 대량 구매 할인 적용
        float discountMultiplier = CalculateBulkDiscountMultiplier(amount);
        
        // 동적 가격 조정 (에너지 부족 정도에 따라)
        float demandMultiplier = CalculateDemandMultiplier();

        int finalCost = Mathf.RoundToInt(baseCost * discountMultiplier * demandMultiplier);
        return Mathf.Max(1, finalCost); // 최소 1 통화
    }

    private float CalculateBulkDiscountMultiplier(int amount)
    {
        // 대량 구매 할인 (10개 이상 구매 시)
        if (amount >= 10)
        {
            return 0.9f; // 10% 할인
        }
        else if (amount >= 25)
        {
            return 0.8f; // 20% 할인
        }
        else if (amount >= 50)
        {
            return 0.7f; // 30% 할인
        }
        
        return 1.0f; // 할인 없음
    }

    private float CalculateDemandMultiplier()
    {
        // 에너지가 부족할수록 가격 상승
        float energyPercentage = _energyManager.EnergyPercentage;
        
        if (energyPercentage <= 0.1f) // 10% 이하
        {
            return 1.5f; // 50% 프리미엄
        }
        else if (energyPercentage <= 0.25f) // 25% 이하
        {
            return 1.2f; // 20% 프리미엄
        }
        
        return 1.0f; // 정상 가격
    }

    private int CalculateActualPurchaseAmount(int requestedAmount)
    {
        // 현재 에너지 여유 공간 계산
        int availableSpace = _energyManager.MaxEnergy - _energyManager.CurrentEnergy;
        
        // 요청량과 여유 공간 중 작은 값 선택
        return Math.Min(requestedAmount, availableSpace);
    }
    #endregion

    #region Currency Operations
    private bool ProcessPayment(int cost)
    {
        // TODO: 실제 통화 시스템과 연동
        // 지금은 임시로 항상 성공으로 처리
        if (_userDataManager?.CurrentUser != null)
        {
            // 가정: UserData에 Currency 필드가 있다고 가정
            // var userData = _userDataManager.CurrentUser;
            // if (userData.Currency >= cost)
            // {
            //     userData.Currency -= cost;
            //     return true;
            // }
            // return false;
            
            // 임시로 항상 성공 처리
            Debug.Log($"[EnergyEconomySystem] Payment processed: {cost} currency (temporary implementation)");
            return true;
        }
        
        return false;
    }

    private bool HasSufficientCurrency(int cost)
    {
        // TODO: 실제 통화 시스템과 연동
        // 지금은 임시로 항상 충분하다고 처리
        return true;
    }
    #endregion

    #region Statistics
    private void UpdatePurchaseStats(int amount, int cost)
    {
        _totalPurchasedEnergy += amount;
        _totalSpentCurrency += cost;
        _transactionCount++;
        _lastPurchaseTime = DateTime.Now;
    }

    public EnergyEconomyStats GetEconomyStats()
    {
        return new EnergyEconomyStats
        {
            TotalPurchasedEnergy = _totalPurchasedEnergy,
            TotalSpentCurrency = _totalSpentCurrency,
            TransactionCount = _transactionCount,
            LastPurchaseTime = _lastPurchaseTime,
            AveragePurchaseAmount = _transactionCount > 0 ? (float)_totalPurchasedEnergy / _transactionCount : 0f,
            AverageCostPerEnergy = _totalPurchasedEnergy > 0 ? (float)_totalSpentCurrency / _totalPurchasedEnergy : 0f
        };
    }
    #endregion

    #region Cleanup
    public void Cleanup()
    {
        OnEnergyPurchased = null;
        OnTransactionCompleted = null;
        OnTransactionFailed = null;
        
        Debug.Log("[EnergyEconomySystem] Cleaned up");
    }
    #endregion
}