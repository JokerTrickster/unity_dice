using System;
using System.Collections.Generic;
using UnityEngine;

#region Configuration
/// <summary>
/// 에너지 시스템 설정
/// </summary>
[Serializable]
public class EnergyConfig
{
    [Header("Basic Energy Settings")]
    public int MaxEnergy = 100;
    public int RechargeRate = 1;
    public TimeSpan RechargeInterval = TimeSpan.FromMinutes(10);
    
    [Header("Visual and Gameplay")]
    [Range(0f, 1f)]
    public float LowEnergyThreshold = 0.2f;
    
    [Header("Economy Settings")]
    public int MaxEnergyPurchaseAmount = 50;
    public int EnergyPurchaseCost = 2; // cost per energy unit
    
    [Header("Advanced Settings")]
    public bool EnableEnergyRecovery = true;
    public bool EnableEnergyPurchase = true;
    public bool EnableEnergyValidation = true;
    
    public EnergyConfig()
    {
        // Default values already set above
    }
    
    public EnergyConfig Clone()
    {
        return new EnergyConfig
        {
            MaxEnergy = this.MaxEnergy,
            RechargeRate = this.RechargeRate,
            RechargeInterval = this.RechargeInterval,
            LowEnergyThreshold = this.LowEnergyThreshold,
            MaxEnergyPurchaseAmount = this.MaxEnergyPurchaseAmount,
            EnergyPurchaseCost = this.EnergyPurchaseCost,
            EnableEnergyRecovery = this.EnableEnergyRecovery,
            EnableEnergyPurchase = this.EnableEnergyPurchase,
            EnableEnergyValidation = this.EnableEnergyValidation
        };
    }
}
#endregion

#region Status and State
/// <summary>
/// 에너지 상태 정보
/// </summary>
[Serializable]
public class EnergyStatus
{
    public int CurrentEnergy;
    public int MaxEnergy;
    public float EnergyPercentage;
    public bool IsEnergyLow;
    public bool IsEnergyFull;
    public bool CanUseEnergy;
    public TimeSpan TimeUntilNextRecharge;
    public bool CanRechargeNow;
    public DateTime Timestamp;

    public EnergyStatus()
    {
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 상태 세부 정보
/// </summary>
[Serializable]
public class EnergyStateInfo
{
    public int CurrentEnergy;
    public int MaxEnergy;
    public float EnergyPercentage;
    public bool IsEnergyLow;
    public bool IsEnergyFull;
    public bool CanUseEnergy;
    public int EnergyDeficit; // MaxEnergy - CurrentEnergy
    public DateTime Timestamp;
}

/// <summary>
/// 에너지 회복 시스템 상태
/// </summary>
[Serializable]
public class EnergyRecoveryStatus
{
    public bool IsActive;
    public DateTime LastRechargeTime;
    public TimeSpan TimeUntilNextRecharge;
    public bool CanRechargeNow;
    public int RechargeAmount;
    public TimeSpan RechargeInterval;
    public int TotalRecoveredAmount;
    public int RecoveryTickCount;
}

/// <summary>
/// 에너지 회복 통계
/// </summary>
[Serializable]
public class EnergyRecoveryStats
{
    public int TotalRecoveredAmount;
    public int RecoveryTickCount;
    public float AverageRecoveryPerTick;
    public TimeSpan SystemUptime;
}

/// <summary>
/// 에너지 경제 통계
/// </summary>
[Serializable]
public class EnergyEconomyStats
{
    public int TotalPurchasedEnergy;
    public int TotalSpentCurrency;
    public int TransactionCount;
    public DateTime LastPurchaseTime;
    public float AveragePurchaseAmount;
    public float AverageCostPerEnergy;
}
#endregion

#region Action and Request Types
/// <summary>
/// 에너지 액션 타입
/// </summary>
public enum EnergyActionType
{
    Consume,      // 에너지 소비
    Restore,      // 에너지 회복
    SetMax,       // 최대 에너지 설정
    ForceRecharge // 강제 충전
}

/// <summary>
/// 에너지 액션 요청
/// </summary>
[Serializable]
public class EnergyActionRequest
{
    public EnergyActionType ActionType;
    public int Amount;
    public string Source;
    public DateTime Timestamp;
    
    public EnergyActionRequest()
    {
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 구매 요청
/// </summary>
[Serializable]
public class EnergyPurchaseRequest
{
    public int Amount;
    public string PaymentMethod;
    public object PaymentData;
    public DateTime Timestamp;
    
    public EnergyPurchaseRequest()
    {
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 요청 (섹션 간 통신용)
/// </summary>
[Serializable]
public class EnergyRequest
{
    public string RequestType; // "consume", "check", "add"
    public int Amount;
    public MainPageSectionType RequesterSection;
    public string RequestId;
    public DateTime Timestamp;
    
    public EnergyRequest()
    {
        RequestId = Guid.NewGuid().ToString();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 응답 (섹션 간 통신용)
/// </summary>
[Serializable]
public class EnergyResponse
{
    public bool Success;
    public int CurrentEnergy;
    public int MaxEnergy;
    public bool CanUseEnergy;
    public string RequestId;
    public string ErrorMessage;
    public DateTime Timestamp;
    
    public EnergyResponse()
    {
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 게임 액션 요청
/// </summary>
[Serializable]
public class GameActionRequest
{
    public string ActionId;
    public string ActionName;
    public int EnergyCost;
    public bool RequiresEnergy;
    public object ActionData;
    public DateTime Timestamp;
    
    public GameActionRequest()
    {
        ActionId = Guid.NewGuid().ToString();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 게임 액션 에너지 응답
/// </summary>
[Serializable]
public class GameActionEnergyResponse
{
    public string ActionId;
    public bool HasEnoughEnergy;
    public int CurrentEnergy;
    public int EnergyCost;
    public DateTime Timestamp;
    
    public GameActionEnergyResponse()
    {
        Timestamp = DateTime.Now;
    }
}
#endregion

#region Transaction and Economy
/// <summary>
/// 에너지 거래 결과
/// </summary>
[Serializable]
public class EnergyTransactionResult
{
    public string TransactionId;
    public bool Success;
    public int RequestedAmount;
    public int ActualAmount;
    public int Cost;
    public int NewEnergyAmount;
    public int NewMaxEnergy;
    public string ErrorMessage;
    public DateTime Timestamp;
    
    public EnergyTransactionResult()
    {
        TransactionId = Guid.NewGuid().ToString();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 구매 견적
/// </summary>
[Serializable]
public class EnergyPurchaseQuote
{
    public int RequestedAmount;
    public int ActualAmount;
    public int TotalCost;
    public float CostPerUnit;
    public bool IsValid;
    public bool HasSufficientCurrency;
    public int EnergyAfterPurchase;
    public string ErrorMessage;
    public DateTime Timestamp;
    
    public EnergyPurchaseQuote()
    {
        Timestamp = DateTime.Now;
    }
}
#endregion

#region Validation
/// <summary>
/// 에너지 유효성 검사 컨텍스트
/// </summary>
[Serializable]
public class EnergyValidationContext
{
    public string ActionName;
    public object ActionData;
    public string RequestSource;
    public Dictionary<string, object> AdditionalData;
    public DateTime Timestamp;
    
    public EnergyValidationContext()
    {
        AdditionalData = new Dictionary<string, object>();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 유효성 검사 결과
/// </summary>
[Serializable]
public class EnergyValidationResult
{
    public bool IsValid;
    public int RequestedAmount;
    public int ValidatedAmount;
    public EnergyValidationContext ValidationContext;
    public EnergyValidationRule FailedRule;
    public string Message;
    public string ErrorCode;
    public DateTime Timestamp;
    
    public EnergyValidationResult()
    {
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 유효성 검사 규칙
/// </summary>
[Serializable]
public class EnergyValidationRule
{
    public string RuleName;
    public Func<int, EnergyValidationContext, ValidationRuleResult> ValidateFunction;
    public string ErrorMessage;
    public bool IsEnabled;
    public int Priority; // Lower numbers = higher priority
    
    public EnergyValidationRule()
    {
        IsEnabled = true;
        Priority = 1;
    }
}

/// <summary>
/// 검증 규칙 결과
/// </summary>
[Serializable]
public class ValidationRuleResult
{
    public bool IsValid;
    public string ErrorCode;
    public string Message;
    public object AdditionalData;
}

/// <summary>
/// 에너지 구매 유효성 검사
/// </summary>
[Serializable]
public class EnergyPurchaseValidation
{
    public bool IsValid;
    public string ErrorMessage;
    public string ErrorCode;
}

/// <summary>
/// 에너지 사용 요청
/// </summary>
[Serializable]
public class EnergyUsageRequest
{
    public int Amount;
    public EnergyValidationContext Context;
    public string RequestId;
    public DateTime Timestamp;
    
    public EnergyUsageRequest()
    {
        RequestId = Guid.NewGuid().ToString();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 배치 에너지 검증 결과
/// </summary>
[Serializable]
public class EnergyBatchValidationResult
{
    public int TotalRequests;
    public List<EnergyValidationResult> ValidRequests;
    public List<EnergyValidationResult> InvalidRequests;
    public bool HasSufficientEnergyForAll;
    public int TotalEnergyRequired;
    public int AvailableEnergy;
    public DateTime Timestamp;
    
    public EnergyBatchValidationResult()
    {
        ValidRequests = new List<EnergyValidationResult>();
        InvalidRequests = new List<EnergyValidationResult>();
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 에너지 검증 통계
/// </summary>
[Serializable]
public class EnergyValidationStats
{
    public int TotalValidationCount;
    public int SuccessfulValidationCount;
    public int FailedValidationCount;
    public float ValidationSuccessRate;
    public int ActiveRuleCount;
    public int TotalRuleCount;
    public List<EnergyValidationResult> RecentValidationHistory;
}
#endregion

#region Notifications
/// <summary>
/// 알림 타입
/// </summary>
public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success,
    EnergyGain,
    EnergyConsume,
    EnergyPurchase,
    EnergyFull,
    EnergyDepleted
}

/// <summary>
/// 에너지 변경 메시지
/// </summary>
[Serializable]
public class EnergyChangedMessage
{
    public int CurrentEnergy;
    public int MaxEnergy;
    public int ChangeAmount;
    public string ChangeReason;
    public DateTime Timestamp;
    
    public EnergyChangedMessage()
    {
        Timestamp = DateTime.Now;
    }
}
#endregion

#region Extensions
public static class EnergyDataExtensions
{
    public static bool IsEmpty(this EnergyStatus status)
    {
        return status.CurrentEnergy == 0;
    }
    
    public static bool IsCritical(this EnergyStatus status)
    {
        return status.IsEnergyLow && status.CurrentEnergy <= status.MaxEnergy * 0.1f;
    }
    
    public static string GetStatusDescription(this EnergyStatus status)
    {
        if (status.IsEnergyFull) return "에너지 가득참";
        if (status.IsEnergyLow) return "에너지 부족";
        if (status.EnergyPercentage >= 0.75f) return "에너지 충분";
        if (status.EnergyPercentage >= 0.5f) return "에너지 보통";
        return "에너지 부족 주의";
    }
    
    public static Color GetStatusColor(this EnergyStatus status)
    {
        if (status.IsEnergyFull) return Color.green;
        if (status.IsEnergyLow) return Color.red;
        if (status.EnergyPercentage >= 0.5f) return Color.yellow;
        return Color.white;
    }
}
#endregion