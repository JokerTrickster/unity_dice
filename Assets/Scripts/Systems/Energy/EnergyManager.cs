using System;
using UnityEngine;

/// <summary>
/// 에너지 상태 관리자
/// 에너지의 현재 값, 최대값, 소비/충전 로직을 담당합니다.
/// </summary>
public class EnergyManager
{
    #region Events
    public event Action<int, int> OnEnergyChanged; // current, max
    public event Action OnEnergyDepleted;
    public event Action OnEnergyFull;
    #endregion

    #region Properties
    public int CurrentEnergy { get; private set; }
    public int MaxEnergy { get; private set; }
    public float EnergyPercentage => MaxEnergy > 0 ? (float)CurrentEnergy / MaxEnergy : 0f;
    public bool IsEnergyLow => EnergyPercentage <= _config.LowEnergyThreshold;
    public bool IsEnergyFull => CurrentEnergy >= MaxEnergy;
    public bool CanUseEnergy => CurrentEnergy > 0;
    public bool IsInitialized { get; private set; }
    #endregion

    #region Private Fields
    private EnergyConfig _config;
    private int _previousEnergy;
    #endregion

    #region Constructor
    public EnergyManager(EnergyConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        InitializeEnergy();
    }
    #endregion

    #region Initialization
    private void InitializeEnergy()
    {
        MaxEnergy = _config.MaxEnergy;
        CurrentEnergy = MaxEnergy; // Start with full energy
        _previousEnergy = CurrentEnergy;
        IsInitialized = true;
        
        Debug.Log($"[EnergyManager] Initialized with {CurrentEnergy}/{MaxEnergy} energy");
    }

    public void UpdateFromUserData(UserData userData)
    {
        if (userData == null) return;
        
        var wasEnergyFull = IsEnergyFull;
        var wasEnergyDepleted = CurrentEnergy == 0;
        
        CurrentEnergy = Mathf.Clamp(userData.CurrentEnergy, 0, userData.MaxEnergy);
        MaxEnergy = Mathf.Max(1, userData.MaxEnergy);
        
        // Trigger events if state changed
        if (CurrentEnergy != _previousEnergy || MaxEnergy != userData.MaxEnergy)
        {
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
            
            // Check for state transitions
            if (!wasEnergyDepleted && CurrentEnergy == 0)
            {
                OnEnergyDepleted?.Invoke();
            }
            else if (!wasEnergyFull && IsEnergyFull)
            {
                OnEnergyFull?.Invoke();
            }
        }
        
        _previousEnergy = CurrentEnergy;
        
        Debug.Log($"[EnergyManager] Updated from UserData: {CurrentEnergy}/{MaxEnergy}");
    }

    public void UpdateConfig(EnergyConfig newConfig)
    {
        if (newConfig == null) return;
        
        _config = newConfig;
        
        // Adjust max energy if needed
        if (MaxEnergy != _config.MaxEnergy)
        {
            SetMaxEnergy(_config.MaxEnergy);
        }
        
        Debug.Log($"[EnergyManager] Config updated");
    }
    #endregion

    #region Energy Operations
    /// <summary>
    /// 에너지 소비
    /// </summary>
    public bool ConsumeEnergy(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[EnergyManager] Invalid energy amount to consume: {amount}");
            return false;
        }
        
        if (CurrentEnergy < amount)
        {
            Debug.LogWarning($"[EnergyManager] Insufficient energy: {CurrentEnergy} < {amount}");
            return false;
        }
        
        var wasEnergyFull = IsEnergyFull;
        CurrentEnergy -= amount;
        CurrentEnergy = Mathf.Max(0, CurrentEnergy);
        
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (CurrentEnergy == 0)
        {
            OnEnergyDepleted?.Invoke();
        }
        
        _previousEnergy = CurrentEnergy;
        Debug.Log($"[EnergyManager] Consumed {amount} energy: {CurrentEnergy}/{MaxEnergy}");
        return true;
    }

    /// <summary>
    /// 에너지 추가
    /// </summary>
    public void AddEnergy(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[EnergyManager] Invalid energy amount to add: {amount}");
            return;
        }
        
        var wasEnergyDepleted = CurrentEnergy == 0;
        CurrentEnergy += amount;
        CurrentEnergy = Mathf.Min(MaxEnergy, CurrentEnergy);
        
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (IsEnergyFull && _previousEnergy < MaxEnergy)
        {
            OnEnergyFull?.Invoke();
        }
        
        _previousEnergy = CurrentEnergy;
        Debug.Log($"[EnergyManager] Added {amount} energy: {CurrentEnergy}/{MaxEnergy}");
    }

    /// <summary>
    /// 최대 에너지 설정
    /// </summary>
    public void SetMaxEnergy(int maxEnergy)
    {
        if (maxEnergy <= 0)
        {
            Debug.LogWarning($"[EnergyManager] Invalid max energy: {maxEnergy}");
            return;
        }
        
        var oldMaxEnergy = MaxEnergy;
        MaxEnergy = maxEnergy;
        
        // 현재 에너지가 새로운 최대치를 초과하면 조정
        if (CurrentEnergy > MaxEnergy)
        {
            CurrentEnergy = MaxEnergy;
        }
        
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (IsEnergyFull && !wasEnergyFull(oldMaxEnergy))
        {
            OnEnergyFull?.Invoke();
        }
        
        _previousEnergy = CurrentEnergy;
        Debug.Log($"[EnergyManager] Max energy changed: {oldMaxEnergy} -> {MaxEnergy}");
    }

    /// <summary>
    /// 에너지 완전 충전
    /// </summary>
    public void FullRecharge()
    {
        var wasEnergyFull = IsEnergyFull;
        CurrentEnergy = MaxEnergy;
        
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (!wasEnergyFull)
        {
            OnEnergyFull?.Invoke();
        }
        
        _previousEnergy = CurrentEnergy;
        Debug.Log($"[EnergyManager] Full recharge: {CurrentEnergy}/{MaxEnergy}");
    }

    /// <summary>
    /// 에너지 완전 소모
    /// </summary>
    public void DepleteEnergy()
    {
        var wasEnergyDepleted = CurrentEnergy == 0;
        CurrentEnergy = 0;
        
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (!wasEnergyDepleted)
        {
            OnEnergyDepleted?.Invoke();
        }
        
        _previousEnergy = CurrentEnergy;
        Debug.Log($"[EnergyManager] Energy depleted: {CurrentEnergy}/{MaxEnergy}");
    }
    #endregion

    #region Validation
    /// <summary>
    /// 에너지 사용 가능 여부 확인
    /// </summary>
    public bool CanConsumeEnergy(int amount)
    {
        return CurrentEnergy >= amount && amount > 0;
    }

    /// <summary>
    /// 에너지 추가 가능 여부 확인
    /// </summary>
    public bool CanAddEnergy(int amount)
    {
        return CurrentEnergy < MaxEnergy && amount > 0;
    }

    /// <summary>
    /// 현재 에너지 상태 정보 반환
    /// </summary>
    public EnergyStateInfo GetStateInfo()
    {
        return new EnergyStateInfo
        {
            CurrentEnergy = CurrentEnergy,
            MaxEnergy = MaxEnergy,
            EnergyPercentage = EnergyPercentage,
            IsEnergyLow = IsEnergyLow,
            IsEnergyFull = IsEnergyFull,
            CanUseEnergy = CanUseEnergy,
            EnergyDeficit = MaxEnergy - CurrentEnergy,
            Timestamp = DateTime.Now
        };
    }
    #endregion

    #region Cleanup
    public void Cleanup()
    {
        OnEnergyChanged = null;
        OnEnergyDepleted = null;
        OnEnergyFull = null;
        
        Debug.Log("[EnergyManager] Cleaned up");
    }
    #endregion

    #region Private Helpers
    private bool wasEnergyFull(int previousMaxEnergy)
    {
        return _previousEnergy >= previousMaxEnergy;
    }
    #endregion
}