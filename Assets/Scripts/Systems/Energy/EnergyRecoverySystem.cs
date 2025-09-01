using System;
using UnityEngine;

/// <summary>
/// 에너지 자동 회복 시스템
/// 시간 기반 에너지 충전 로직을 관리합니다.
/// </summary>
public class EnergyRecoverySystem
{
    #region Events
    public event Action<int> OnEnergyRecovered; // amount recovered
    public event Action OnRecoveryTick;
    #endregion

    #region Properties
    public DateTime LastRechargeTime { get; private set; }
    public TimeSpan RechargeInterval => _config.RechargeInterval;
    public int RechargeAmount => _config.RechargeRate;
    public bool CanRechargeNow => TimeUntilNextRecharge.TotalSeconds <= 0 && !_energyManager.IsEnergyFull;
    public TimeSpan TimeUntilNextRecharge
    {
        get
        {
            var timeSinceLastRecharge = DateTime.Now - LastRechargeTime;
            var timeToNext = RechargeInterval - timeSinceLastRecharge;
            return timeToNext.TotalSeconds > 0 ? timeToNext : TimeSpan.Zero;
        }
    }
    public bool IsRecoveryActive { get; private set; }
    #endregion

    #region Private Fields
    private EnergyConfig _config;
    private EnergyManager _energyManager;
    private DateTime _lastUpdateTime;
    private int _totalRecoveredAmount;
    private int _recoveryTickCount;
    #endregion

    #region Constructor
    public EnergyRecoverySystem(EnergyConfig config, EnergyManager energyManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _energyManager = energyManager ?? throw new ArgumentNullException(nameof(energyManager));
        
        Initialize();
    }
    #endregion

    #region Initialization
    private void Initialize()
    {
        LastRechargeTime = DateTime.Now;
        _lastUpdateTime = DateTime.Now;
        IsRecoveryActive = true;
        _totalRecoveredAmount = 0;
        _recoveryTickCount = 0;
        
        Debug.Log($"[EnergyRecoverySystem] Initialized with interval: {RechargeInterval}, amount: {RechargeAmount}");
    }

    public void UpdateConfig(EnergyConfig newConfig)
    {
        if (newConfig == null) return;
        
        _config = newConfig;
        Debug.Log($"[EnergyRecoverySystem] Config updated");
    }
    #endregion

    #region Recovery Logic
    /// <summary>
    /// 에너지 회복 시도
    /// </summary>
    public bool TryRecoverEnergy()
    {
        if (!IsRecoveryActive || _energyManager.IsEnergyFull)
        {
            return false;
        }
        
        if (!CanRechargeNow)
        {
            return false;
        }
        
        // 에너지 회복 실행
        int amountToRecover = CalculateRecoveryAmount();
        if (amountToRecover > 0)
        {
            _energyManager.AddEnergy(amountToRecover);
            LastRechargeTime = DateTime.Now;
            _totalRecoveredAmount += amountToRecover;
            _recoveryTickCount++;
            
            OnEnergyRecovered?.Invoke(amountToRecover);
            OnRecoveryTick?.Invoke();
            
            Debug.Log($"[EnergyRecoverySystem] Recovered {amountToRecover} energy. Total recovered: {_totalRecoveredAmount}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 여러 회복 기회 처리 (긴 시간 후 복귀 시)
    /// </summary>
    public int ProcessPendingRecoveries()
    {
        if (!IsRecoveryActive || _energyManager.IsEnergyFull)
        {
            return 0;
        }
        
        var now = DateTime.Now;
        var timeSinceLastRecharge = now - LastRechargeTime;
        
        // 여러 회복 기회가 있는지 확인
        int pendingRecoveries = (int)(timeSinceLastRecharge.TotalSeconds / RechargeInterval.TotalSeconds);
        
        if (pendingRecoveries > 0)
        {
            int totalRecoveryAmount = 0;
            int maxRecoveries = Math.Min(pendingRecoveries, GetMaxPossibleRecoveries());
            
            for (int i = 0; i < maxRecoveries; i++)
            {
                if (_energyManager.IsEnergyFull) break;
                
                int recoveryAmount = CalculateRecoveryAmount();
                if (recoveryAmount > 0)
                {
                    _energyManager.AddEnergy(recoveryAmount);
                    totalRecoveryAmount += recoveryAmount;
                    _recoveryTickCount++;
                }
            }
            
            // 마지막 회복 시간 업데이트
            LastRechargeTime = now - TimeSpan.FromSeconds(timeSinceLastRecharge.TotalSeconds % RechargeInterval.TotalSeconds);
            _totalRecoveredAmount += totalRecoveryAmount;
            
            if (totalRecoveryAmount > 0)
            {
                OnEnergyRecovered?.Invoke(totalRecoveryAmount);
                Debug.Log($"[EnergyRecoverySystem] Processed {maxRecoveries} pending recoveries, recovered {totalRecoveryAmount} energy");
            }
            
            return totalRecoveryAmount;
        }
        
        return 0;
    }

    /// <summary>
    /// 강제 에너지 충전
    /// </summary>
    public bool ForceRecharge()
    {
        if (!IsRecoveryActive || _energyManager.IsEnergyFull)
        {
            return false;
        }
        
        int amountToRecover = CalculateRecoveryAmount();
        if (amountToRecover > 0)
        {
            _energyManager.AddEnergy(amountToRecover);
            LastRechargeTime = DateTime.Now;
            _totalRecoveredAmount += amountToRecover;
            _recoveryTickCount++;
            
            OnEnergyRecovered?.Invoke(amountToRecover);
            OnRecoveryTick?.Invoke();
            
            Debug.Log($"[EnergyRecoverySystem] Force recharge: {amountToRecover} energy");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 전체 에너지 즉시 충전
    /// </summary>
    public void FullInstantRecharge()
    {
        if (!IsRecoveryActive) return;
        
        int energyDeficit = _energyManager.MaxEnergy - _energyManager.CurrentEnergy;
        if (energyDeficit > 0)
        {
            _energyManager.AddEnergy(energyDeficit);
            LastRechargeTime = DateTime.Now;
            _totalRecoveredAmount += energyDeficit;
            
            OnEnergyRecovered?.Invoke(energyDeficit);
            Debug.Log($"[EnergyRecoverySystem] Full instant recharge: {energyDeficit} energy");
        }
    }
    #endregion

    #region Recovery Control
    /// <summary>
    /// 에너지 회복 활성화/비활성화
    /// </summary>
    public void SetRecoveryActive(bool active)
    {
        if (IsRecoveryActive != active)
        {
            IsRecoveryActive = active;
            Debug.Log($"[EnergyRecoverySystem] Recovery {(active ? "activated" : "deactivated")}");
        }
    }

    /// <summary>
    /// 회복 시간 재설정
    /// </summary>
    public void ResetRecoveryTime()
    {
        LastRechargeTime = DateTime.Now;
        Debug.Log("[EnergyRecoverySystem] Recovery time reset");
    }

    /// <summary>
    /// 강제 업데이트
    /// </summary>
    public void ForceUpdate()
    {
        _lastUpdateTime = DateTime.Now;
        ProcessPendingRecoveries();
    }
    #endregion

    #region Calculation Helpers
    private int CalculateRecoveryAmount()
    {
        // 기본 회복량
        int baseAmount = RechargeAmount;
        
        // 최대 에너지를 초과하지 않도록 제한
        int availableSpace = _energyManager.MaxEnergy - _energyManager.CurrentEnergy;
        return Math.Min(baseAmount, availableSpace);
    }

    private int GetMaxPossibleRecoveries()
    {
        // 현재 에너지 상태에서 최대 몇 번 회복할 수 있는지 계산
        int energyDeficit = _energyManager.MaxEnergy - _energyManager.CurrentEnergy;
        return (int)Math.Ceiling((float)energyDeficit / RechargeAmount);
    }
    #endregion

    #region Status and Statistics
    /// <summary>
    /// 회복 시스템 상태 정보
    /// </summary>
    public EnergyRecoveryStatus GetStatus()
    {
        return new EnergyRecoveryStatus
        {
            IsActive = IsRecoveryActive,
            LastRechargeTime = LastRechargeTime,
            TimeUntilNextRecharge = TimeUntilNextRecharge,
            CanRechargeNow = CanRechargeNow,
            RechargeAmount = RechargeAmount,
            RechargeInterval = RechargeInterval,
            TotalRecoveredAmount = _totalRecoveredAmount,
            RecoveryTickCount = _recoveryTickCount
        };
    }

    /// <summary>
    /// 회복 통계 정보
    /// </summary>
    public EnergyRecoveryStats GetStats()
    {
        var uptime = DateTime.Now - _lastUpdateTime;
        return new EnergyRecoveryStats
        {
            TotalRecoveredAmount = _totalRecoveredAmount,
            RecoveryTickCount = _recoveryTickCount,
            AverageRecoveryPerTick = _recoveryTickCount > 0 ? (float)_totalRecoveredAmount / _recoveryTickCount : 0f,
            SystemUptime = uptime
        };
    }
    #endregion

    #region Cleanup
    public void Cleanup()
    {
        IsRecoveryActive = false;
        OnEnergyRecovered = null;
        OnRecoveryTick = null;
        
        Debug.Log("[EnergyRecoverySystem] Cleaned up");
    }
    #endregion
}