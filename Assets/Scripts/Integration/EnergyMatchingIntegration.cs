using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Energy-Matching 시스템 통합 컴포넌트
/// EnergyManager와 MatchingSystem 간의 연동을 담당하고 에너지 검증 로직을 제공합니다.
/// Integration & Testing Stream의 핵심 통합 컴포넌트입니다.
/// </summary>
public class EnergyMatchingIntegration : MonoBehaviour
{
    #region Events
    /// <summary>에너지 검증 완료 이벤트</summary>
    public event Action<bool, string> OnEnergyValidationComplete; // (isValid, message)
    
    /// <summary>에너지 소모 완료 이벤트</summary>
    public event Action<int, int> OnEnergyConsumed; // (previousEnergy, currentEnergy)
    
    /// <summary>에너지 복원 완료 이벤트</summary>
    public event Action<int, int> OnEnergyRestored; // (previousEnergy, currentEnergy)
    
    /// <summary>에너지 부족 경고 이벤트</summary>
    public event Action<int, int> OnEnergyWarning; // (currentEnergy, requiredEnergy)
    #endregion

    #region Dependencies
    private EnergyManager _energyManager;
    private MatchingNetworkHandler _matchingNetworkHandler;
    private IntegratedMatchingUI _matchingUI;
    private UserDataManager _userDataManager;
    #endregion

    #region Configuration
    [Header("Energy Configuration")]
    [SerializeField] private int _requiredEnergyPerGame = 1;
    [SerializeField] private bool _validateBeforeMatching = true;
    [SerializeField] private bool _consumeOnMatchStart = true;
    [SerializeField] private bool _restoreOnMatchCancel = true;
    
    [Header("Integration Settings")]
    [SerializeField] private bool _autoInitialize = true;
    [SerializeField] private float _validationTimeout = 5f;
    [SerializeField] private bool _enableLogging = true;
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private bool _isValidatingEnergy = false;
    private bool _hasConsumedEnergy = false;
    private int _energyBeforeMatching = 0;
    private Coroutine _validationTimeoutCoroutine;

    // Integration state tracking
    private MatchingState _lastMatchingState = MatchingState.Idle;
    private DateTime _matchingStartTime;
    private bool _isIntegrationActive = false;
    #endregion

    #region Properties
    /// <summary>초기화 상태</summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>에너지 검증 중 상태</summary>
    public bool IsValidatingEnergy => _isValidatingEnergy;
    
    /// <summary>에너지 소모 여부</summary>
    public bool HasConsumedEnergy => _hasConsumedEnergy;
    
    /// <summary>게임 시작에 필요한 에너지</summary>
    public int RequiredEnergyPerGame => _requiredEnergyPerGame;
    
    /// <summary>현재 에너지</summary>
    public int CurrentEnergy => _energyManager?.GetCurrentEnergy() ?? 0;
    
    /// <summary>통합 활성 상태</summary>
    public bool IsIntegrationActive => _isIntegrationActive;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_autoInitialize)
        {
            StartCoroutine(InitializeWhenReady());
        }
    }

    private void Start()
    {
        if (!_isInitialized && _autoInitialize)
        {
            InitializeIntegration();
        }
    }

    private void OnDestroy()
    {
        CleanupIntegration();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 의존성이 준비될 때까지 기다리는 코루틴
    /// </summary>
    private IEnumerator InitializeWhenReady()
    {
        float waitTime = 0f;
        const float maxWaitTime = 10f;

        while (!AreAllDependenciesReady() && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (AreAllDependenciesReady())
        {
            InitializeIntegration();
        }
        else
        {
            Debug.LogError("[EnergyMatchingIntegration] Failed to initialize: Dependencies not ready after timeout");
        }
    }

    /// <summary>
    /// 모든 의존성이 준비되었는지 확인
    /// </summary>
    private bool AreAllDependenciesReady()
    {
        return FindDependencies();
    }

    /// <summary>
    /// 의존성 컴포넌트들을 찾아서 설정
    /// </summary>
    private bool FindDependencies()
    {
        _energyManager = EnergyManager.Instance;
        _userDataManager = UserDataManager.Instance;
        
        if (_matchingNetworkHandler == null)
            _matchingNetworkHandler = FindObjectOfType<MatchingNetworkHandler>();
            
        if (_matchingUI == null)
            _matchingUI = FindObjectOfType<IntegratedMatchingUI>();

        bool allFound = _energyManager != null && 
                       _userDataManager != null && 
                       _matchingNetworkHandler != null && 
                       _matchingUI != null;

        if (_enableLogging)
        {
            Debug.Log($"[EnergyMatchingIntegration] Dependencies check: " +
                     $"EnergyManager={_energyManager != null}, " +
                     $"UserDataManager={_userDataManager != null}, " +
                     $"MatchingNetworkHandler={_matchingNetworkHandler != null}, " +
                     $"IntegratedMatchingUI={_matchingUI != null}");
        }

        return allFound;
    }

    /// <summary>
    /// 통합 시스템 초기화
    /// </summary>
    public bool InitializeIntegration()
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[EnergyMatchingIntegration] Already initialized");
            return true;
        }

        if (!FindDependencies())
        {
            Debug.LogError("[EnergyMatchingIntegration] Cannot initialize: Missing dependencies");
            return false;
        }

        try
        {
            // 이벤트 구독
            SubscribeToEvents();

            _isInitialized = true;
            _isIntegrationActive = true;
            
            if (_enableLogging)
                Debug.Log("[EnergyMatchingIntegration] Integration initialized successfully");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyMatchingIntegration] Initialization failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // Matching UI 이벤트 구독
        if (_matchingUI != null)
        {
            _matchingUI.OnMatchingRequested += OnMatchingRequested;
            _matchingUI.OnMatchCancelRequested += OnMatchingCancelled;
        }

        // EnergyManager 이벤트 구독
        if (_energyManager != null)
        {
            _energyManager.OnEnergyChanged += OnEnergyChanged;
            _energyManager.OnEnergyInsufficient += OnEnergyInsufficient;
        }
    }

    /// <summary>
    /// 통합 시스템 정리
    /// </summary>
    private void CleanupIntegration()
    {
        UnsubscribeFromEvents();
        
        if (_validationTimeoutCoroutine != null)
        {
            StopCoroutine(_validationTimeoutCoroutine);
            _validationTimeoutCoroutine = null;
        }

        _isInitialized = false;
        _isIntegrationActive = false;
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_matchingUI != null)
        {
            _matchingUI.OnMatchingRequested -= OnMatchingRequested;
            _matchingUI.OnMatchCancelRequested -= OnMatchingCancelled;
        }

        if (_energyManager != null)
        {
            _energyManager.OnEnergyChanged -= OnEnergyChanged;
            _energyManager.OnEnergyInsufficient -= OnEnergyInsufficient;
        }
    }
    #endregion

    #region Energy Validation API
    /// <summary>
    /// 매칭 시작 전 에너지 검증
    /// </summary>
    public async Task<bool> ValidateEnergyForMatchingAsync(int requiredEnergy = -1)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[EnergyMatchingIntegration] Cannot validate: Not initialized");
            return false;
        }

        if (_isValidatingEnergy)
        {
            Debug.LogWarning("[EnergyMatchingIntegration] Energy validation already in progress");
            return false;
        }

        _isValidatingEnergy = true;
        
        try
        {
            if (requiredEnergy < 0)
                requiredEnergy = _requiredEnergyPerGame;

            int currentEnergy = _energyManager.GetCurrentEnergy();
            
            if (_enableLogging)
                Debug.Log($"[EnergyMatchingIntegration] Validating energy: {currentEnergy}/{requiredEnergy}");

            // 에너지 부족 확인
            if (currentEnergy < requiredEnergy)
            {
                OnEnergyWarning?.Invoke(currentEnergy, requiredEnergy);
                OnEnergyValidationComplete?.Invoke(false, $"에너지가 부족합니다. ({currentEnergy}/{requiredEnergy})");
                return false;
            }

            // 에너지 검증 성공
            _energyBeforeMatching = currentEnergy;
            OnEnergyValidationComplete?.Invoke(true, "에너지 검증 완료");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyMatchingIntegration] Energy validation failed: {e.Message}");
            OnEnergyValidationComplete?.Invoke(false, $"검증 오류: {e.Message}");
            return false;
        }
        finally
        {
            _isValidatingEnergy = false;
        }
    }

    /// <summary>
    /// 매칭 시작 시 에너지 소모
    /// </summary>
    public bool ConsumeEnergyForMatching(int energyAmount = -1)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[EnergyMatchingIntegration] Cannot consume: Not initialized");
            return false;
        }

        if (energyAmount < 0)
            energyAmount = _requiredEnergyPerGame;

        try
        {
            int previousEnergy = _energyManager.GetCurrentEnergy();
            bool consumed = _energyManager.ConsumeEnergy(energyAmount);
            
            if (consumed)
            {
                _hasConsumedEnergy = true;
                int currentEnergy = _energyManager.GetCurrentEnergy();
                OnEnergyConsumed?.Invoke(previousEnergy, currentEnergy);
                
                if (_enableLogging)
                    Debug.Log($"[EnergyMatchingIntegration] Energy consumed: {previousEnergy} → {currentEnergy}");
            }
            else
            {
                Debug.LogWarning($"[EnergyMatchingIntegration] Failed to consume energy: {energyAmount}");
            }

            return consumed;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyMatchingIntegration] Energy consumption failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 매칭 취소 시 에너지 복원
    /// </summary>
    public bool RestoreEnergyAfterCancel(int energyAmount = -1)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[EnergyMatchingIntegration] Cannot restore: Not initialized");
            return false;
        }

        if (!_hasConsumedEnergy)
        {
            if (_enableLogging)
                Debug.Log("[EnergyMatchingIntegration] No energy to restore");
            return true;
        }

        if (energyAmount < 0)
            energyAmount = _requiredEnergyPerGame;

        try
        {
            int previousEnergy = _energyManager.GetCurrentEnergy();
            bool restored = _energyManager.AddEnergy(energyAmount);
            
            if (restored)
            {
                _hasConsumedEnergy = false;
                int currentEnergy = _energyManager.GetCurrentEnergy();
                OnEnergyRestored?.Invoke(previousEnergy, currentEnergy);
                
                if (_enableLogging)
                    Debug.Log($"[EnergyMatchingIntegration] Energy restored: {previousEnergy} → {currentEnergy}");
            }
            else
            {
                Debug.LogWarning($"[EnergyMatchingIntegration] Failed to restore energy: {energyAmount}");
            }

            return restored;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyMatchingIntegration] Energy restoration failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 현재 에너지 상태 확인
    /// </summary>
    public EnergyMatchingStatus GetEnergyMatchingStatus()
    {
        if (!_isInitialized)
        {
            return new EnergyMatchingStatus
            {
                IsValid = false,
                CurrentEnergy = 0,
                RequiredEnergy = _requiredEnergyPerGame,
                CanStartMatching = false,
                Message = "Integration not initialized"
            };
        }

        int currentEnergy = _energyManager.GetCurrentEnergy();
        bool canStart = currentEnergy >= _requiredEnergyPerGame;

        return new EnergyMatchingStatus
        {
            IsValid = true,
            CurrentEnergy = currentEnergy,
            RequiredEnergy = _requiredEnergyPerGame,
            CanStartMatching = canStart,
            HasConsumedEnergy = _hasConsumedEnergy,
            Message = canStart ? "Ready to start matching" : "Insufficient energy"
        };
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 매칭 요청 처리
    /// </summary>
    private async void OnMatchingRequested(MatchingRequest request)
    {
        if (!_validateBeforeMatching)
        {
            if (_enableLogging)
                Debug.Log("[EnergyMatchingIntegration] Energy validation disabled, proceeding with matching");
            return;
        }

        // 에너지 검증
        bool isValid = await ValidateEnergyForMatchingAsync();
        if (!isValid)
        {
            if (_enableLogging)
                Debug.Log("[EnergyMatchingIntegration] Matching blocked due to insufficient energy");
            
            // 매칭 UI에게 에너지 부족 상태 알림
            _matchingUI?.ShowMessage("에너지가 부족합니다.", MessageType.Warning);
            return;
        }

        // 에너지 소모 (설정에 따라)
        if (_consumeOnMatchStart)
        {
            bool consumed = ConsumeEnergyForMatching();
            if (!consumed)
            {
                Debug.LogError("[EnergyMatchingIntegration] Failed to consume energy for matching");
                _matchingUI?.ShowMessage("에너지 소모에 실패했습니다.", MessageType.Error);
                return;
            }
        }

        _matchingStartTime = DateTime.UtcNow;
        
        if (_enableLogging)
            Debug.Log($"[EnergyMatchingIntegration] Matching approved for player {request.playerId}");
    }

    /// <summary>
    /// 매칭 취소 처리
    /// </summary>
    private void OnMatchingCancelled()
    {
        if (_restoreOnMatchCancel && _hasConsumedEnergy)
        {
            bool restored = RestoreEnergyAfterCancel();
            if (restored)
            {
                if (_enableLogging)
                    Debug.Log("[EnergyMatchingIntegration] Energy restored after matching cancel");
            }
        }
    }

    /// <summary>
    /// 에너지 변경 처리
    /// </summary>
    private void OnEnergyChanged(int previousEnergy, int currentEnergy)
    {
        if (_enableLogging)
            Debug.Log($"[EnergyMatchingIntegration] Energy changed: {previousEnergy} → {currentEnergy}");

        // UI 업데이트
        RefreshMatchingAvailability();
    }

    /// <summary>
    /// 에너지 부족 처리
    /// </summary>
    private void OnEnergyInsufficient(int currentEnergy, int requiredEnergy)
    {
        OnEnergyWarning?.Invoke(currentEnergy, requiredEnergy);
        
        if (_enableLogging)
            Debug.LogWarning($"[EnergyMatchingIntegration] Energy insufficient: {currentEnergy}/{requiredEnergy}");

        // 매칭 중이라면 취소
        if (_lastMatchingState == MatchingState.Searching)
        {
            _matchingUI?.ShowMessage("에너지 부족으로 매칭이 취소됩니다.", MessageType.Warning);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 매칭 가능 상태 새로고침
    /// </summary>
    private void RefreshMatchingAvailability()
    {
        if (!_isInitialized) return;

        var status = GetEnergyMatchingStatus();
        // UI에 상태 업데이트 요청할 수 있음
    }

    /// <summary>
    /// 검증 타임아웃 코루틴
    /// </summary>
    private IEnumerator ValidationTimeoutCoroutine()
    {
        yield return new WaitForSeconds(_validationTimeout);
        
        if (_isValidatingEnergy)
        {
            _isValidatingEnergy = false;
            OnEnergyValidationComplete?.Invoke(false, "에너지 검증 타임아웃");
            Debug.LogWarning("[EnergyMatchingIntegration] Energy validation timed out");
        }
    }
    #endregion

    #region Public Configuration
    /// <summary>
    /// 게임 시작에 필요한 에너지 설정
    /// </summary>
    public void SetRequiredEnergy(int requiredEnergy)
    {
        _requiredEnergyPerGame = Mathf.Max(0, requiredEnergy);
        if (_enableLogging)
            Debug.Log($"[EnergyMatchingIntegration] Required energy set to: {_requiredEnergyPerGame}");
    }

    /// <summary>
    /// 통합 설정 업데이트
    /// </summary>
    public void UpdateIntegrationSettings(bool validateBeforeMatching, bool consumeOnStart, bool restoreOnCancel)
    {
        _validateBeforeMatching = validateBeforeMatching;
        _consumeOnMatchStart = consumeOnStart;
        _restoreOnMatchCancel = restoreOnCancel;
        
        if (_enableLogging)
            Debug.Log($"[EnergyMatchingIntegration] Settings updated: Validate={validateBeforeMatching}, Consume={consumeOnStart}, Restore={restoreOnCancel}");
    }

    /// <summary>
    /// 로깅 활성화/비활성화
    /// </summary>
    public void SetLogging(bool enabled)
    {
        _enableLogging = enabled;
    }
    #endregion
}

/// <summary>
/// 에너지-매칭 상태 정보
/// </summary>
[System.Serializable]
public class EnergyMatchingStatus
{
    public bool IsValid;
    public int CurrentEnergy;
    public int RequiredEnergy;
    public bool CanStartMatching;
    public bool HasConsumedEnergy;
    public string Message;

    public override string ToString()
    {
        return $"EnergyMatchingStatus(Energy: {CurrentEnergy}/{RequiredEnergy}, CanStart: {CanStartMatching}, Message: {Message})";
    }
}