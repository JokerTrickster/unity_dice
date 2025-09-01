using System.Collections;
using UnityEngine;

/// <summary>
/// 에너지 시스템 통합 예제
/// EnergySection과 MainPageManager의 연동을 보여주는 예제 스크립트
/// </summary>
public class EnergySystemExample : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnergySection energySection;
    [SerializeField] private MainPageManager mainPageManager;
    
    [Header("Test Settings")]
    [SerializeField] private bool autoRegisterOnStart = true;
    [SerializeField] private bool runDemoOnStart = false;
    [SerializeField] private float demoInterval = 5f;

    private void Start()
    {
        if (autoRegisterOnStart)
        {
            RegisterEnergySection();
        }
        
        if (runDemoOnStart)
        {
            StartCoroutine(RunEnergySystemDemo());
        }
    }

    /// <summary>
    /// EnergySection을 MainPageManager에 등록
    /// </summary>
    public void RegisterEnergySection()
    {
        if (energySection == null)
        {
            energySection = FindObjectOfType<EnergySection>();
            if (energySection == null)
            {
                Debug.LogError("[EnergySystemExample] EnergySection not found!");
                return;
            }
        }

        if (mainPageManager == null)
        {
            mainPageManager = MainPageManager.Instance;
            if (mainPageManager == null)
            {
                Debug.LogError("[EnergySystemExample] MainPageManager not found!");
                return;
            }
        }

        // EnergySection을 MainPageManager에 등록
        mainPageManager.RegisterSection(MainPageSectionType.Energy, energySection);
        
        // 이벤트 구독 예제
        SubscribeToEnergyEvents();
        
        Debug.Log("[EnergySystemExample] EnergySection registered successfully!");
    }

    /// <summary>
    /// 에너지 이벤트 구독
    /// </summary>
    private void SubscribeToEnergyEvents()
    {
        // EnergySection 이벤트 구독
        EnergySection.OnEnergyChanged += OnEnergyChanged;
        EnergySection.OnEnergyDepleted += OnEnergyDepleted;
        EnergySection.OnEnergyRestored += OnEnergyRestored;
        EnergySection.OnEnergyPurchased += OnEnergyPurchased;
        EnergySection.OnEnergyTransaction += OnEnergyTransaction;
    }

    /// <summary>
    /// 에너지 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEnergyEvents()
    {
        EnergySection.OnEnergyChanged -= OnEnergyChanged;
        EnergySection.OnEnergyDepleted -= OnEnergyDepleted;
        EnergySection.OnEnergyRestored -= OnEnergyRestored;
        EnergySection.OnEnergyPurchased -= OnEnergyPurchased;
        EnergySection.OnEnergyTransaction -= OnEnergyTransaction;
    }

    #region Event Handlers
    private void OnEnergyChanged(int currentEnergy, int maxEnergy)
    {
        Debug.Log($"[EnergySystemExample] Energy changed: {currentEnergy}/{maxEnergy} ({(float)currentEnergy / maxEnergy:P1})");
        
        // UI 업데이트나 다른 시스템에 알림 등을 여기서 처리
        if (currentEnergy <= maxEnergy * 0.1f) // 10% 이하
        {
            Debug.LogWarning("[EnergySystemExample] Energy critically low!");
        }
    }

    private void OnEnergyDepleted()
    {
        Debug.LogWarning("[EnergySystemExample] Energy completely depleted! Consider purchasing more energy.");
        
        // 에너지 부족 시 처리 로직
        // 예: 특정 기능 비활성화, 경고 메시지 표시 등
    }

    private void OnEnergyRestored()
    {
        Debug.Log("[EnergySystemExample] Energy restored! Full functionality available.");
        
        // 에너지 복구 시 처리 로직
        // 예: 비활성화된 기능 재활성화
    }

    private void OnEnergyPurchased(int amount)
    {
        Debug.Log($"[EnergySystemExample] Energy purchased: {amount} units");
        
        // 구매 완료 시 처리 로직
        // 예: 구매 성공 알림, 통계 업데이트 등
    }

    private void OnEnergyTransaction(EnergyTransactionResult result)
    {
        if (result.Success)
        {
            Debug.Log($"[EnergySystemExample] Transaction successful: {result.ActualAmount} energy for {result.Cost} currency");
        }
        else
        {
            Debug.LogError($"[EnergySystemExample] Transaction failed: {result.ErrorMessage}");
        }
        
        // 거래 결과에 따른 처리
        // 예: 거래 내역 로그, 사용자 피드백 등
    }
    #endregion

    #region Demo and Testing
    /// <summary>
    /// 에너지 시스템 데모 실행
    /// </summary>
    private IEnumerator RunEnergySystemDemo()
    {
        Debug.Log("[EnergySystemExample] Starting energy system demo...");
        
        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (energySection != null)
            {
                // 데모 액션들
                yield return StartCoroutine(DemoEnergyConsumption());
                yield return new WaitForSeconds(demoInterval);
                
                yield return StartCoroutine(DemoEnergyPurchase());
                yield return new WaitForSeconds(demoInterval);
                
                yield return StartCoroutine(DemoEnergyRecovery());
                yield return new WaitForSeconds(demoInterval);
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
        }
    }

    private IEnumerator DemoEnergyConsumption()
    {
        Debug.Log("[EnergySystemExample] Demo: Energy consumption");
        
        // 에너지 소비 테스트
        if (energySection.CanUseEnergy(25))
        {
            energySection.ConsumeEnergy(25, "demo_consumption");
        }
        
        yield return new WaitForSeconds(1f);
        
        // 게임 액션 시뮬레이션
        if (energySection.CanUseEnergy(10))
        {
            energySection.ConsumeEnergy(10, "demo_game_action");
        }
        
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator DemoEnergyPurchase()
    {
        Debug.Log("[EnergySystemExample] Demo: Energy purchase");
        
        // 에너지 구매 테스트
        if (energySection.CanPurchaseEnergy(20))
        {
            // 구매는 UI를 통해 수행되므로 여기서는 로그만
            Debug.Log("[EnergySystemExample] Energy purchase is available");
        }
        else
        {
            Debug.Log("[EnergySystemExample] Energy purchase not available");
        }
        
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator DemoEnergyRecovery()
    {
        Debug.Log("[EnergySystemExample] Demo: Force energy recovery");
        
        // 강제 에너지 회복
        energySection.ForceRecharge();
        
        yield return new WaitForSeconds(1f);
        
        // 에너지 상태 출력
        var status = energySection.GetEnergyStatus();
        if (status != null)
        {
            Debug.Log($"[EnergySystemExample] Current energy status: {status.CurrentEnergy}/{status.MaxEnergy} " +
                     $"({status.EnergyPercentage:P1}) - {status.GetStatusDescription()}");
        }
        
        yield return new WaitForSeconds(1f);
    }
    #endregion

    #region Public Interface for Testing
    /// <summary>
    /// 에너지 소비 테스트
    /// </summary>
    [ContextMenu("Test Energy Consumption")]
    public void TestEnergyConsumption()
    {
        if (energySection != null)
        {
            energySection.ConsumeEnergy(20, "manual_test");
        }
    }

    /// <summary>
    /// 에너지 추가 테스트
    /// </summary>
    [ContextMenu("Test Energy Addition")]
    public void TestEnergyAddition()
    {
        if (energySection != null)
        {
            energySection.AddEnergy(30, "manual_test");
        }
    }

    /// <summary>
    /// 강제 충전 테스트
    /// </summary>
    [ContextMenu("Test Force Recharge")]
    public void TestForceRecharge()
    {
        if (energySection != null)
        {
            energySection.ForceRecharge();
        }
    }

    /// <summary>
    /// 에너지 상태 로그 출력
    /// </summary>
    [ContextMenu("Log Energy Status")]
    public void LogEnergyStatus()
    {
        if (energySection != null)
        {
            var status = energySection.GetEnergyStatus();
            if (status != null)
            {
                Debug.Log($"Energy Status: {status.CurrentEnergy}/{status.MaxEnergy} " +
                         $"({status.EnergyPercentage:P1}) - {status.GetStatusDescription()}");
                Debug.Log($"Can Use Energy: {status.CanUseEnergy}, Is Low: {status.IsEnergyLow}, " +
                         $"Is Full: {status.IsEnergyFull}");
                Debug.Log($"Time Until Next Recharge: {status.TimeUntilNextRecharge}");
            }
        }
    }

    /// <summary>
    /// 섹션 간 메시지 테스트
    /// </summary>
    [ContextMenu("Test Section Message")]
    public void TestSectionMessage()
    {
        if (mainPageManager != null)
        {
            // 다른 섹션에서 에너지 요청하는 시뮬레이션
            var energyRequest = new EnergyRequest
            {
                RequestType = "consume",
                Amount = 15,
                RequesterSection = MainPageSectionType.Matching
            };
            
            mainPageManager.SendMessageToSection(
                MainPageSectionType.Matching, 
                MainPageSectionType.Energy, 
                energyRequest
            );
        }
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        UnsubscribeFromEnergyEvents();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 앱 일시정지 시 에너지 데이터 저장
            Debug.Log("[EnergySystemExample] Application paused, energy data should be saved");
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // 앱 포커스 잃을 시 에너지 데이터 저장
            Debug.Log("[EnergySystemExample] Application lost focus, energy data should be saved");
        }
        else
        {
            // 앱 포커스 복구 시 에너지 회복 처리 (백그라운드 시간 계산)
            Debug.Log("[EnergySystemExample] Application gained focus, processing background energy recovery");
        }
    }
    #endregion
}