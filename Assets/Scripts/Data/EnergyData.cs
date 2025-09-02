using System;
using UnityEngine;

/// <summary>
/// 피로도(에너지) 데이터 모델
/// 현재/최대 에너지, 회복 시간 정보를 담는 핵심 데이터 구조
/// </summary>
[System.Serializable]
public class EnergyData
{
    [Header("Energy Status")]
    public int currentEnergy;
    public int maxEnergy;
    
    [Header("Recovery System")]
    public DateTime lastRecoveryTime;
    public int recoveryRate; // 분당 회복량
    
    [Header("Economy")]
    public int costPerPurchase; // 구매당 가격 (게임 내 재화)
    
    /// <summary>
    /// 기본 생성자 - 초기 에너지 값 설정
    /// </summary>
    public EnergyData()
    {
        currentEnergy = 100;
        maxEnergy = 100;
        lastRecoveryTime = DateTime.Now;
        recoveryRate = 1;
        costPerPurchase = 100;
    }
    
    /// <summary>
    /// 매개변수 생성자
    /// </summary>
    public EnergyData(int current, int max, int recoveryRate = 1, int purchaseCost = 100)
    {
        this.currentEnergy = Mathf.Clamp(current, 0, max);
        this.maxEnergy = Mathf.Max(1, max);
        this.lastRecoveryTime = DateTime.Now;
        this.recoveryRate = Mathf.Max(1, recoveryRate);
        this.costPerPurchase = Mathf.Max(1, purchaseCost);
    }
    
    /// <summary>
    /// 에너지 백분율 계산 (0.0 ~ 1.0)
    /// </summary>
    public float EnergyPercentage => maxEnergy > 0 ? (float)currentEnergy / maxEnergy : 0f;
    
    /// <summary>
    /// 에너지 부족 상태 확인 (20% 이하)
    /// </summary>
    public bool IsEnergyLow => EnergyPercentage <= 0.2f;
    
    /// <summary>
    /// 에너지 가득 찬 상태 확인
    /// </summary>
    public bool IsEnergyFull => currentEnergy >= maxEnergy;
    
    /// <summary>
    /// 에너지 사용 가능 상태 확인
    /// </summary>
    public bool CanUseEnergy => currentEnergy > 0;
    
    /// <summary>
    /// 에너지 부족량 계산
    /// </summary>
    public int EnergyDeficit => Mathf.Max(0, maxEnergy - currentEnergy);
    
    /// <summary>
    /// 다음 회복까지 남은 시간 계산
    /// </summary>
    /// <param name="recoveryIntervalMinutes">회복 간격(분)</param>
    public TimeSpan TimeUntilNextRecovery(int recoveryIntervalMinutes = 5)
    {
        var timeSinceLastRecovery = DateTime.Now - lastRecoveryTime;
        var recoveryInterval = TimeSpan.FromMinutes(recoveryIntervalMinutes);
        var timeUntilNext = recoveryInterval - timeSinceLastRecovery;
        return timeUntilNext.TotalSeconds > 0 ? timeUntilNext : TimeSpan.Zero;
    }
    
    /// <summary>
    /// 에너지 소비
    /// </summary>
    /// <param name="amount">소비할 에너지량</param>
    /// <returns>소비 성공 여부</returns>
    public bool ConsumeEnergy(int amount)
    {
        if (amount <= 0 || currentEnergy < amount)
            return false;
            
        currentEnergy = Mathf.Max(0, currentEnergy - amount);
        return true;
    }
    
    /// <summary>
    /// 에너지 추가
    /// </summary>
    /// <param name="amount">추가할 에너지량</param>
    /// <returns>실제 추가된 에너지량</returns>
    public int AddEnergy(int amount)
    {
        if (amount <= 0)
            return 0;
            
        int oldEnergy = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        return currentEnergy - oldEnergy;
    }
    
    /// <summary>
    /// 회복 시간 업데이트
    /// </summary>
    public void UpdateRecoveryTime()
    {
        lastRecoveryTime = DateTime.Now;
    }
    
    /// <summary>
    /// 데이터 유효성 검증
    /// </summary>
    public bool IsValid()
    {
        return maxEnergy > 0 && 
               currentEnergy >= 0 && 
               currentEnergy <= maxEnergy &&
               recoveryRate > 0 &&
               costPerPurchase > 0;
    }
    
    /// <summary>
    /// 데이터 정규화 (잘못된 값 수정)
    /// </summary>
    public void Normalize()
    {
        maxEnergy = Mathf.Max(1, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
        recoveryRate = Mathf.Max(1, recoveryRate);
        costPerPurchase = Mathf.Max(1, costPerPurchase);
        
        // 타임스탬프가 미래인 경우 현재 시간으로 설정
        if (lastRecoveryTime > DateTime.Now)
        {
            lastRecoveryTime = DateTime.Now;
        }
    }
    
    /// <summary>
    /// 데이터를 JSON으로 직렬화하기 위한 복사
    /// </summary>
    public EnergyData Clone()
    {
        return new EnergyData(currentEnergy, maxEnergy, recoveryRate, costPerPurchase)
        {
            lastRecoveryTime = this.lastRecoveryTime
        };
    }
    
    /// <summary>
    /// 디버그용 문자열 표현
    /// </summary>
    public override string ToString()
    {
        return $"EnergyData[{currentEnergy}/{maxEnergy}, Recovery: {recoveryRate}/min, Cost: {costPerPurchase}]";
    }
}

/// <summary>
/// 에너지 구매 요청 데이터
/// 서버 통신용 데이터 구조
/// </summary>
[System.Serializable]
public class EnergyPurchaseRequest
{
    public int amount;
    public string currencyType; // "coins", "gems" 등
    public DateTime timestamp;
    
    public EnergyPurchaseRequest()
    {
        timestamp = DateTime.Now;
    }
    
    public EnergyPurchaseRequest(int amount, string currencyType)
    {
        this.amount = amount;
        this.currencyType = currencyType ?? "coins";
        this.timestamp = DateTime.Now;
    }
    
    /// <summary>
    /// 요청 데이터 유효성 검증
    /// </summary>
    public bool IsValid()
    {
        return amount > 0 && !string.IsNullOrEmpty(currencyType);
    }
}