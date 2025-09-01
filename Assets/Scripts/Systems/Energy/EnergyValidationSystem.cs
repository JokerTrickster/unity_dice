using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 유효성 검사 시스템
/// 게임 액션 및 에너지 소비에 대한 검증과 제약 조건을 관리합니다.
/// </summary>
public class EnergyValidationSystem
{
    #region Events
    public event Action<EnergyValidationResult> OnValidationCompleted;
    public event Action<string> OnValidationFailed;
    #endregion

    #region Properties
    public bool IsValidationEnabled { get; private set; }
    public int TotalValidationCount { get; private set; }
    public int SuccessfulValidationCount { get; private set; }
    public int FailedValidationCount { get; private set; }
    public float ValidationSuccessRate => TotalValidationCount > 0 ? (float)SuccessfulValidationCount / TotalValidationCount : 0f;
    #endregion

    #region Private Fields
    private EnergyManager _energyManager;
    private Dictionary<string, EnergyValidationRule> _validationRules;
    private List<EnergyValidationResult> _validationHistory;
    private const int MAX_HISTORY_SIZE = 100;
    #endregion

    #region Constructor
    public EnergyValidationSystem(EnergyManager energyManager)
    {
        _energyManager = energyManager ?? throw new ArgumentNullException(nameof(energyManager));
        Initialize();
    }
    #endregion

    #region Initialization
    private void Initialize()
    {
        IsValidationEnabled = true;
        TotalValidationCount = 0;
        SuccessfulValidationCount = 0;
        FailedValidationCount = 0;
        
        _validationRules = new Dictionary<string, EnergyValidationRule>();
        _validationHistory = new List<EnergyValidationResult>();
        
        SetupDefaultValidationRules();
        
        Debug.Log("[EnergyValidationSystem] Initialized with default validation rules");
    }

    private void SetupDefaultValidationRules()
    {
        // 기본 에너지 소비 검증 규칙
        AddValidationRule("basic_energy_consumption", new EnergyValidationRule
        {
            RuleName = "Basic Energy Consumption",
            ValidateFunction = (amount, context) => ValidateBasicEnergyConsumption(amount, context),
            ErrorMessage = "에너지가 부족합니다.",
            IsEnabled = true,
            Priority = 1
        });

        // 게임 액션 에너지 소비 검증 규칙
        AddValidationRule("game_action_energy", new EnergyValidationRule
        {
            RuleName = "Game Action Energy",
            ValidateFunction = (amount, context) => ValidateGameActionEnergy(amount, context),
            ErrorMessage = "게임 액션을 위한 에너지가 부족합니다.",
            IsEnabled = true,
            Priority = 2
        });

        // 최소 에너지 보존 규칙
        AddValidationRule("minimum_energy_preservation", new EnergyValidationRule
        {
            RuleName = "Minimum Energy Preservation",
            ValidateFunction = (amount, context) => ValidateMinimumEnergyPreservation(amount, context),
            ErrorMessage = "최소 에너지를 보존해야 합니다.",
            IsEnabled = false, // 기본적으로 비활성화
            Priority = 3
        });

        // 일일 에너지 소비 제한
        AddValidationRule("daily_consumption_limit", new EnergyValidationRule
        {
            RuleName = "Daily Consumption Limit",
            ValidateFunction = (amount, context) => ValidateDailyConsumptionLimit(amount, context),
            ErrorMessage = "일일 에너지 소비 한도를 초과했습니다.",
            IsEnabled = false, // 필요시 활성화
            Priority = 4
        });
    }
    #endregion

    #region Validation Operations
    /// <summary>
    /// 에너지 사용 가능 여부 검증
    /// </summary>
    public bool CanUseEnergy(int amount, EnergyValidationContext context = null)
    {
        var result = ValidateEnergyUsage(amount, context);
        return result.IsValid;
    }

    /// <summary>
    /// 에너지 사용 검증 (상세 결과 반환)
    /// </summary>
    public EnergyValidationResult ValidateEnergyUsage(int amount, EnergyValidationContext context = null)
    {
        if (!IsValidationEnabled)
        {
            return new EnergyValidationResult
            {
                IsValid = true,
                ValidatedAmount = amount,
                ValidationContext = context,
                Message = "Validation disabled"
            };
        }

        var result = new EnergyValidationResult
        {
            RequestedAmount = amount,
            ValidatedAmount = 0,
            ValidationContext = context ?? new EnergyValidationContext(),
            Timestamp = DateTime.Now
        };

        try
        {
            // 활성화된 검증 규칙들을 우선순위 순으로 실행
            var sortedRules = GetSortedValidationRules();
            
            foreach (var rule in sortedRules)
            {
                if (!rule.IsEnabled) continue;
                
                var ruleResult = rule.ValidateFunction(amount, result.ValidationContext);
                
                if (!ruleResult.IsValid)
                {
                    result.IsValid = false;
                    result.FailedRule = rule;
                    result.Message = rule.ErrorMessage;
                    result.ErrorCode = ruleResult.ErrorCode;
                    
                    RecordValidationResult(result);
                    OnValidationCompleted?.Invoke(result);
                    OnValidationFailed?.Invoke(rule.ErrorMessage);
                    
                    return result;
                }
            }
            
            // 모든 검증 통과
            result.IsValid = true;
            result.ValidatedAmount = amount;
            result.Message = "Validation successful";
            
            RecordValidationResult(result);
            OnValidationCompleted?.Invoke(result);
            
            return result;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Validation error: {ex.Message}";
            result.ErrorCode = "VALIDATION_EXCEPTION";
            
            RecordValidationResult(result);
            OnValidationFailed?.Invoke(result.Message);
            
            Debug.LogError($"[EnergyValidationSystem] Validation exception: {ex}");
            return result;
        }
    }

    /// <summary>
    /// 게임 액션 에너지 요구사항 검증
    /// </summary>
    public bool ValidateGameAction(string actionName, int energyCost, object actionData = null)
    {
        var context = new EnergyValidationContext
        {
            ActionName = actionName,
            ActionData = actionData,
            RequestSource = "game_action",
            Timestamp = DateTime.Now
        };
        
        return CanUseEnergy(energyCost, context);
    }

    /// <summary>
    /// 배치 에너지 소비 검증
    /// </summary>
    public EnergyBatchValidationResult ValidateBatchEnergyUsage(IEnumerable<EnergyUsageRequest> requests)
    {
        var batchResult = new EnergyBatchValidationResult
        {
            TotalRequests = 0,
            ValidRequests = new List<EnergyValidationResult>(),
            InvalidRequests = new List<EnergyValidationResult>()
        };
        
        int totalEnergyRequired = 0;
        
        foreach (var request in requests)
        {
            batchResult.TotalRequests++;
            var validationResult = ValidateEnergyUsage(request.Amount, request.Context);
            
            if (validationResult.IsValid)
            {
                batchResult.ValidRequests.Add(validationResult);
                totalEnergyRequired += request.Amount;
            }
            else
            {
                batchResult.InvalidRequests.Add(validationResult);
            }
        }
        
        // 전체 배치에 대한 에너지 충족 여부 확인
        batchResult.HasSufficientEnergyForAll = _energyManager.CanConsumeEnergy(totalEnergyRequired);
        batchResult.TotalEnergyRequired = totalEnergyRequired;
        batchResult.AvailableEnergy = _energyManager.CurrentEnergy;
        
        return batchResult;
    }
    #endregion

    #region Validation Rules Management
    /// <summary>
    /// 검증 규칙 추가
    /// </summary>
    public void AddValidationRule(string ruleId, EnergyValidationRule rule)
    {
        if (string.IsNullOrEmpty(ruleId) || rule == null)
        {
            Debug.LogWarning("[EnergyValidationSystem] Invalid rule parameters");
            return;
        }
        
        _validationRules[ruleId] = rule;
        Debug.Log($"[EnergyValidationSystem] Added validation rule: {ruleId}");
    }

    /// <summary>
    /// 검증 규칙 제거
    /// </summary>
    public bool RemoveValidationRule(string ruleId)
    {
        if (_validationRules.Remove(ruleId))
        {
            Debug.Log($"[EnergyValidationSystem] Removed validation rule: {ruleId}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 검증 규칙 활성화/비활성화
    /// </summary>
    public void SetRuleEnabled(string ruleId, bool enabled)
    {
        if (_validationRules.TryGetValue(ruleId, out var rule))
        {
            rule.IsEnabled = enabled;
            Debug.Log($"[EnergyValidationSystem] Rule {ruleId} {(enabled ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// 검증 시스템 활성화/비활성화
    /// </summary>
    public void SetValidationEnabled(bool enabled)
    {
        IsValidationEnabled = enabled;
        Debug.Log($"[EnergyValidationSystem] Validation {(enabled ? "enabled" : "disabled")}");
    }
    #endregion

    #region Default Validation Rules
    private ValidationRuleResult ValidateBasicEnergyConsumption(int amount, EnergyValidationContext context)
    {
        return new ValidationRuleResult
        {
            IsValid = _energyManager.CanConsumeEnergy(amount),
            ErrorCode = _energyManager.CanConsumeEnergy(amount) ? null : "INSUFFICIENT_ENERGY"
        };
    }

    private ValidationRuleResult ValidateGameActionEnergy(int amount, EnergyValidationContext context)
    {
        // 게임 액션별 특별한 검증 로직
        if (context?.ActionName != null)
        {
            // 특정 액션에 대한 추가 검증
            switch (context.ActionName.ToLower())
            {
                case "dice_roll":
                    return new ValidationRuleResult
                    {
                        IsValid = _energyManager.CurrentEnergy >= amount && amount <= 5, // 주사위 굴리기는 최대 5에너지
                        ErrorCode = amount > 5 ? "EXCESSIVE_ENERGY_FOR_DICE_ROLL" : "INSUFFICIENT_ENERGY"
                    };
                    
                case "special_action":
                    return new ValidationRuleResult
                    {
                        IsValid = _energyManager.CurrentEnergy >= amount && _energyManager.EnergyPercentage >= 0.5f,
                        ErrorCode = _energyManager.EnergyPercentage < 0.5f ? "INSUFFICIENT_ENERGY_PERCENTAGE" : "INSUFFICIENT_ENERGY"
                    };
            }
        }
        
        return ValidateBasicEnergyConsumption(amount, context);
    }

    private ValidationRuleResult ValidateMinimumEnergyPreservation(int amount, EnergyValidationContext context)
    {
        int minimumEnergyToPreserve = Mathf.RoundToInt(_energyManager.MaxEnergy * 0.1f); // 최대 에너지의 10% 보존
        int energyAfterConsumption = _energyManager.CurrentEnergy - amount;
        
        return new ValidationRuleResult
        {
            IsValid = energyAfterConsumption >= minimumEnergyToPreserve,
            ErrorCode = "MINIMUM_ENERGY_PRESERVATION_REQUIRED"
        };
    }

    private ValidationRuleResult ValidateDailyConsumptionLimit(int amount, EnergyValidationContext context)
    {
        // TODO: 일일 에너지 소비 한도 검증 구현
        // 현재는 임시로 항상 통과
        return new ValidationRuleResult { IsValid = true };
    }
    #endregion

    #region Utility Methods
    private List<EnergyValidationRule> GetSortedValidationRules()
    {
        var rules = new List<EnergyValidationRule>(_validationRules.Values);
        rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return rules;
    }

    private void RecordValidationResult(EnergyValidationResult result)
    {
        TotalValidationCount++;
        
        if (result.IsValid)
        {
            SuccessfulValidationCount++;
        }
        else
        {
            FailedValidationCount++;
        }
        
        // 기록 히스토리 관리
        _validationHistory.Add(result);
        
        if (_validationHistory.Count > MAX_HISTORY_SIZE)
        {
            _validationHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// 검증 통계 정보 반환
    /// </summary>
    public EnergyValidationStats GetValidationStats()
    {
        return new EnergyValidationStats
        {
            TotalValidationCount = TotalValidationCount,
            SuccessfulValidationCount = SuccessfulValidationCount,
            FailedValidationCount = FailedValidationCount,
            ValidationSuccessRate = ValidationSuccessRate,
            ActiveRuleCount = _validationRules.Values.Count(r => r.IsEnabled),
            TotalRuleCount = _validationRules.Count,
            RecentValidationHistory = new List<EnergyValidationResult>(_validationHistory.TakeLast(10))
        };
    }

    /// <summary>
    /// 검증 히스토리 조회
    /// </summary>
    public List<EnergyValidationResult> GetValidationHistory(int maxCount = 50)
    {
        int count = Math.Min(maxCount, _validationHistory.Count);
        return _validationHistory.TakeLast(count).ToList();
    }
    #endregion

    #region Cleanup
    public void Cleanup()
    {
        OnValidationCompleted = null;
        OnValidationFailed = null;
        _validationRules.Clear();
        _validationHistory.Clear();
        
        Debug.Log("[EnergyValidationSystem] Cleaned up");
    }
    #endregion
}