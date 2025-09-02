using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 에너지 관리 싱글톤
/// 피로도 시스템의 핵심 매니저로 에너지 상태, 자동 회복, 구매, 서버 동기화를 담당합니다.
/// </summary>
public class EnergyManager : MonoBehaviour
{
    #region Singleton
    private static EnergyManager _instance;
    public static EnergyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 기존 인스턴스 찾기
                _instance = FindObjectOfType<EnergyManager>();
                
                if (_instance == null)
                {
                    // 새 GameObject 생성 및 컴포넌트 추가
                    GameObject go = new GameObject("EnergyManager");
                    _instance = go.AddComponent<EnergyManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>에너지 변경 이벤트 (current, max)</summary>
    public static event Action<int, int> OnEnergyChanged;
    
    /// <summary>에너지 고갈 이벤트</summary>
    public static event Action OnEnergyDepleted;
    
    /// <summary>에너지 가득 참 이벤트</summary>
    public static event Action OnEnergyFull;
    
    /// <summary>에너지 부족 경고 이벤트</summary>
    public static event Action<bool> OnEnergyLowWarning; // isLow
    
    /// <summary>에너지 구매 완료 이벤트</summary>
    public static event Action<int, int> OnEnergyPurchased; // amount, totalCost
    
    /// <summary>에너지 회복 이벤트</summary>
    public static event Action<int> OnEnergyRecovered; // recoveredAmount
    #endregion

    #region Configuration
    [Header("Configuration")]
    [SerializeField] private EnergyConfig configOverride; // Inspector에서 설정 가능
    private EnergyConfig _config;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool simulateOfflineRecovery = true;
    #endregion

    #region Private Fields
    private EnergyData _energyData;
    private EnergyRecoverySystem _recoverySystem;
    private bool _isInitialized = false;
    private bool _isServerSyncing = false;
    private DateTime _lastSaveTime;
    private Coroutine _autoUpdateCoroutine;
    
    // PlayerPrefs Keys
    private const string ENERGY_DATA_KEY = "EnergyData";
    private const string LAST_SAVE_TIME_KEY = "LastEnergyUpdate";
    #endregion

    #region Properties
    /// <summary>현재 에너지</summary>
    public int CurrentEnergy => _energyData?.currentEnergy ?? 0;
    
    /// <summary>최대 에너지</summary>
    public int MaxEnergy => _energyData?.maxEnergy ?? 100;
    
    /// <summary>에너지 백분율 (0.0~1.0)</summary>
    public float EnergyPercentage => _energyData?.EnergyPercentage ?? 0f;
    
    /// <summary>에너지 부족 상태</summary>
    public bool IsEnergyLow => _energyData?.IsEnergyLow ?? false;
    
    /// <summary>에너지 가득 참 상태</summary>
    public bool IsEnergyFull => _energyData?.IsEnergyFull ?? false;
    
    /// <summary>에너지 사용 가능 상태</summary>
    public bool CanUseEnergy => _energyData?.CanUseEnergy ?? false;
    
    /// <summary>초기화 완료 상태</summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>서버 동기화 중 상태</summary>
    public bool IsServerSyncing => _isServerSyncing;
    
    /// <summary>현재 에너지 데이터 (읽기 전용)</summary>
    public EnergyData GetEnergyData() => _energyData?.Clone();
    
    /// <summary>회복 시스템 상태</summary>
    public bool IsRecoveryActive => _recoverySystem?.IsRecoveryActive ?? false;
    
    /// <summary>다음 회복까지 남은 시간</summary>
    public TimeSpan TimeUntilNextRecovery => _energyData?.TimeUntilNextRecovery(_config.RechargeIntervalMinutes) ?? TimeSpan.Zero;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton 패턴 구현
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else if (_instance != this)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[EnergyManager] Duplicate instance detected, destroying...");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (_instance == this)
        {
            StartEnergySystem();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!_isInitialized) return;
        
        if (hasFocus)
        {
            // 앱 포커스 시 오프라인 회복 처리
            ProcessOfflineRecovery();
        }
        else
        {
            // 앱 백그라운드 진입 시 데이터 저장
            SaveEnergyDataLocal();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!_isInitialized) return;
        
        if (!pauseStatus)
        {
            // 앱 재개 시 오프라인 회복 처리
            ProcessOfflineRecovery();
        }
        else
        {
            // 앱 일시정지 시 데이터 저장
            SaveEnergyDataLocal();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            StopAllCoroutines();
            SaveEnergyDataLocal();
            _recoverySystem?.Cleanup();
            
            // 이벤트 정리
            OnEnergyChanged = null;
            OnEnergyDepleted = null;
            OnEnergyFull = null;
            OnEnergyLowWarning = null;
            OnEnergyPurchased = null;
            OnEnergyRecovered = null;
            
            _instance = null;
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 매니저 초기화
    /// </summary>
    private void InitializeManager()
    {
        // 설정 로드
        _config = configOverride != null ? configOverride : EnergyConfig.Instance;
        
        if (_config == null)
        {
            Debug.LogError("[EnergyManager] EnergyConfig not found! Creating default config.");
            _config = ScriptableObject.CreateInstance<EnergyConfig>();
            _config.ResetToDefaults();
        }

        // 설정 유효성 검사
        if (!_config.ValidateConfig())
        {
            Debug.LogError("[EnergyManager] Invalid EnergyConfig detected! Using defaults.");
            _config.ResetToDefaults();
        }

        if (enableDebugLogs)
            Debug.Log("[EnergyManager] Manager initialized with config");
    }

    /// <summary>
    /// 에너지 시스템 시작
    /// </summary>
    private void StartEnergySystem()
    {
        // 로컬 데이터 로드
        LoadEnergyDataLocal();
        
        // 회복 시스템 초기화
        InitializeRecoverySystem();
        
        // 서버 동기화 시작
        StartCoroutine(InitializeServerData());
        
        // 자동 업데이트 시작
        StartAutoUpdate();
        
        _isInitialized = true;
        
        if (enableDebugLogs)
            Debug.Log($"[EnergyManager] System started - Energy: {CurrentEnergy}/{MaxEnergy}");
    }

    /// <summary>
    /// 회복 시스템 초기화
    /// </summary>
    private void InitializeRecoverySystem()
    {
        _recoverySystem = new EnergyRecoverySystem(_config);
        _recoverySystem.OnEnergyRecovered += HandleEnergyRecovered;
        
        if (enableDebugLogs)
            Debug.Log("[EnergyManager] Recovery system initialized");
    }

    /// <summary>
    /// 서버 데이터 초기화
    /// </summary>
    private IEnumerator InitializeServerData()
    {
        yield return StartCoroutine(LoadEnergyDataFromServer());
        
        if (enableDebugLogs)
            Debug.Log("[EnergyManager] Server data initialization complete");
    }

    /// <summary>
    /// 자동 업데이트 시작
    /// </summary>
    private void StartAutoUpdate()
    {
        if (_autoUpdateCoroutine != null)
            StopCoroutine(_autoUpdateCoroutine);
            
        _autoUpdateCoroutine = StartCoroutine(AutoUpdateCoroutine());
    }

    /// <summary>
    /// 자동 업데이트 코루틴
    /// </summary>
    private IEnumerator AutoUpdateCoroutine()
    {
        while (true)
        {
            // 1초마다 회복 체크
            yield return new WaitForSeconds(1f);
            
            if (_config.EnableEnergyRecovery && _recoverySystem != null)
            {
                _recoverySystem.TryRecoverEnergy();
            }
            
            // 5분마다 서버 동기화
            if (Time.time - _lastSaveTime.Ticks / TimeSpan.TicksPerSecond > 300f)
            {
                yield return StartCoroutine(SyncEnergyDataToServer());
            }
        }
    }
    #endregion

    #region Energy Operations
    /// <summary>
    /// 에너지 소비
    /// </summary>
    /// <param name="amount">소비할 에너지량</param>
    /// <returns>소비 성공 여부</returns>
    public bool ConsumeEnergy(int amount)
    {
        if (!_isInitialized || _energyData == null)
        {
            Debug.LogWarning("[EnergyManager] Manager not initialized");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[EnergyManager] Invalid energy amount: {amount}");
            return false;
        }

        if (!_energyData.ConsumeEnergy(amount))
        {
            Debug.LogWarning($"[EnergyManager] Insufficient energy: {CurrentEnergy} < {amount}");
            return false;
        }

        // 이벤트 발생
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
        
        if (CurrentEnergy == 0)
            OnEnergyDepleted?.Invoke();
            
        CheckEnergyLowWarning();
        
        // 로컬 저장
        SaveEnergyDataLocal();
        
        // 서버 동기화 (비동기)
        StartCoroutine(SyncEnergyDataToServer());

        if (enableDebugLogs)
            Debug.Log($"[EnergyManager] Consumed {amount} energy: {CurrentEnergy}/{MaxEnergy}");

        return true;
    }

    /// <summary>
    /// 에너지 추가
    /// </summary>
    /// <param name="amount">추가할 에너지량</param>
    /// <returns>실제 추가된 에너지량</returns>
    public int AddEnergy(int amount)
    {
        if (!_isInitialized || _energyData == null)
        {
            Debug.LogWarning("[EnergyManager] Manager not initialized");
            return 0;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[EnergyManager] Invalid energy amount: {amount}");
            return 0;
        }

        bool wasEnergyFull = IsEnergyFull;
        int addedAmount = _energyData.AddEnergy(amount);

        if (addedAmount > 0)
        {
            // 이벤트 발생
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
            
            if (!wasEnergyFull && IsEnergyFull)
                OnEnergyFull?.Invoke();
                
            CheckEnergyLowWarning();
            
            // 로컬 저장
            SaveEnergyDataLocal();

            if (enableDebugLogs)
                Debug.Log($"[EnergyManager] Added {addedAmount} energy: {CurrentEnergy}/{MaxEnergy}");
        }

        return addedAmount;
    }

    /// <summary>
    /// 게임 시작 가능 여부 확인
    /// </summary>
    public bool CanStartGame()
    {
        return CanUseEnergy;
    }

    /// <summary>
    /// 게임 시작 시 에너지 소비
    /// </summary>
    public bool OnGameStart(int energyCost = 1)
    {
        return ConsumeEnergy(energyCost);
    }
    #endregion

    #region Energy Purchase
    /// <summary>
    /// 에너지 구매 처리
    /// </summary>
    /// <param name="amount">구매할 에너지량</param>
    /// <param name="currencyType">화폐 타입</param>
    public void PurchaseEnergy(int amount, string currencyType = "coins")
    {
        StartCoroutine(PurchaseEnergyCoroutine(amount, currencyType));
    }

    /// <summary>
    /// 에너지 구매 코루틴
    /// </summary>
    private IEnumerator PurchaseEnergyCoroutine(int amount, string currencyType)
    {
        if (!_config.EnableEnergyPurchase)
        {
            Debug.LogWarning("[EnergyManager] Energy purchase is disabled");
            yield break;
        }

        if (amount <= 0 || amount > _config.MaxEnergyPurchaseAmount)
        {
            Debug.LogWarning($"[EnergyManager] Invalid purchase amount: {amount}");
            yield break;
        }

        _isServerSyncing = true;

        // 구매 요청 데이터 생성
        var purchaseRequest = new EnergyPurchaseRequest(amount, currencyType);
        
        // 서버에 구매 요청
        yield return StartCoroutine(SendPurchaseRequest(purchaseRequest));
        
        _isServerSyncing = false;
    }

    /// <summary>
    /// 서버에 구매 요청 전송
    /// </summary>
    private IEnumerator SendPurchaseRequest(EnergyPurchaseRequest request)
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[EnergyManager] NetworkManager not available");
            yield break;
        }

        bool requestCompleted = false;
        bool requestSuccess = false;
        int purchasedAmount = 0;
        int totalCost = 0;

        // 네트워크 요청
        NetworkManager.Instance.Post("/api/energy/purchase", request, (response) =>
        {
            requestCompleted = true;
            
            if (response.IsSuccess)
            {
                var result = response.GetData<EnergyPurchaseResponse>();
                if (result != null && result.success)
                {
                    requestSuccess = true;
                    purchasedAmount = result.purchasedAmount;
                    totalCost = result.totalCost;
                    
                    // 로컬 에너지 업데이트
                    AddEnergy(purchasedAmount);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[EnergyManager] Purchase successful: {purchasedAmount} energy for {totalCost} {request.currencyType}");
                }
                else
                {
                    Debug.LogError($"[EnergyManager] Purchase failed: {result?.errorMessage ?? "Unknown error"}");
                }
            }
            else
            {
                Debug.LogError($"[EnergyManager] Network error during purchase: {response.Error}");
            }
        });

        // 응답 대기
        float timeout = 10f;
        while (!requestCompleted && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!requestCompleted)
        {
            Debug.LogError("[EnergyManager] Purchase request timeout");
        }
        else if (requestSuccess)
        {
            OnEnergyPurchased?.Invoke(purchasedAmount, totalCost);
        }
    }
    #endregion

    #region Data Persistence
    /// <summary>
    /// 로컬 데이터 로드
    /// </summary>
    private void LoadEnergyDataLocal()
    {
        try
        {
            if (PlayerPrefs.HasKey(ENERGY_DATA_KEY))
            {
                string jsonData = PlayerPrefs.GetString(ENERGY_DATA_KEY);
                _energyData = JsonUtility.FromJson<EnergyData>(jsonData);
                
                if (_energyData == null || !_energyData.IsValid())
                {
                    CreateDefaultEnergyData();
                }
                else
                {
                    _energyData.Normalize();
                    
                    // 마지막 저장 시간 로드
                    if (PlayerPrefs.HasKey(LAST_SAVE_TIME_KEY))
                    {
                        string lastSaveStr = PlayerPrefs.GetString(LAST_SAVE_TIME_KEY);
                        if (DateTime.TryParse(lastSaveStr, out DateTime lastSave))
                        {
                            _lastSaveTime = lastSave;
                        }
                    }
                }
            }
            else
            {
                CreateDefaultEnergyData();
            }

            if (enableDebugLogs)
                Debug.Log($"[EnergyManager] Local data loaded: {_energyData}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyManager] Error loading local data: {e.Message}");
            CreateDefaultEnergyData();
        }
    }

    /// <summary>
    /// 로컬 데이터 저장
    /// </summary>
    private void SaveEnergyDataLocal()
    {
        if (_energyData == null) return;

        try
        {
            _energyData.UpdateRecoveryTime();
            string jsonData = JsonUtility.ToJson(_energyData);
            PlayerPrefs.SetString(ENERGY_DATA_KEY, jsonData);
            
            _lastSaveTime = DateTime.Now;
            PlayerPrefs.SetString(LAST_SAVE_TIME_KEY, _lastSaveTime.ToString());
            
            PlayerPrefs.Save();

            if (enableDebugLogs)
                Debug.Log("[EnergyManager] Local data saved");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyManager] Error saving local data: {e.Message}");
        }
    }

    /// <summary>
    /// 기본 에너지 데이터 생성
    /// </summary>
    private void CreateDefaultEnergyData()
    {
        _energyData = new EnergyData(_config.MaxEnergy, _config.MaxEnergy, _config.RechargeRate, _config.EnergyPurchaseCost);
        _lastSaveTime = DateTime.Now;
        
        if (enableDebugLogs)
            Debug.Log("[EnergyManager] Created default energy data");
    }

    /// <summary>
    /// 서버에서 에너지 데이터 로드
    /// </summary>
    private IEnumerator LoadEnergyDataFromServer()
    {
        if (NetworkManager.Instance == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[EnergyManager] NetworkManager not available, using local data");
            yield break;
        }

        _isServerSyncing = true;
        bool requestCompleted = false;

        NetworkManager.Instance.Get("/api/user/energy", (response) =>
        {
            requestCompleted = true;
            
            if (response.IsSuccess)
            {
                var serverData = response.GetData<EnergyData>();
                if (serverData != null && serverData.IsValid())
                {
                    _energyData = serverData;
                    _energyData.Normalize();
                    
                    // 로컬에도 저장
                    SaveEnergyDataLocal();
                    
                    // 이벤트 발생
                    OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[EnergyManager] Server data loaded: {_energyData}");
                }
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[EnergyManager] Failed to load server data: {response.Error}");
            }
        });

        // 응답 대기 (타임아웃 5초)
        float timeout = 5f;
        while (!requestCompleted && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        _isServerSyncing = false;
    }

    /// <summary>
    /// 서버에 에너지 데이터 동기화
    /// </summary>
    private IEnumerator SyncEnergyDataToServer()
    {
        if (NetworkManager.Instance == null || _energyData == null || _isServerSyncing)
            yield break;

        _isServerSyncing = true;
        bool requestCompleted = false;

        NetworkManager.Instance.Put("/api/user/energy", _energyData, (response) =>
        {
            requestCompleted = true;
            
            if (response.IsSuccess)
            {
                if (enableDebugLogs)
                    Debug.Log("[EnergyManager] Server sync successful");
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[EnergyManager] Server sync failed: {response.Error}");
            }
        });

        // 응답 대기 (타임아웃 3초)
        float timeout = 3f;
        while (!requestCompleted && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        _isServerSyncing = false;
    }
    #endregion

    #region Offline Recovery
    /// <summary>
    /// 오프라인 회복 처리
    /// </summary>
    private void ProcessOfflineRecovery()
    {
        if (!_config.EnableBackgroundRecovery || _recoverySystem == null || simulateOfflineRecovery)
            return;

        int recoveredAmount = _recoverySystem.ProcessPendingRecoveries();
        
        if (recoveredAmount > 0)
        {
            OnEnergyRecovered?.Invoke(recoveredAmount);
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
            
            if (IsEnergyFull)
                OnEnergyFull?.Invoke();
                
            CheckEnergyLowWarning();
            
            SaveEnergyDataLocal();
            
            if (enableDebugLogs)
                Debug.Log($"[EnergyManager] Offline recovery: {recoveredAmount} energy");
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 에너지 회복 처리
    /// </summary>
    private void HandleEnergyRecovered(int amount)
    {
        OnEnergyRecovered?.Invoke(amount);
        
        if (enableDebugLogs)
            Debug.Log($"[EnergyManager] Energy recovered: {amount}");
    }

    /// <summary>
    /// 에너지 부족 경고 체크
    /// </summary>
    private void CheckEnergyLowWarning()
    {
        static bool wasEnergyLow = false;
        bool isEnergyLow = IsEnergyLow;
        
        if (wasEnergyLow != isEnergyLow)
        {
            OnEnergyLowWarning?.Invoke(isEnergyLow);
            wasEnergyLow = isEnergyLow;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 에너지 구매 비용 계산
    /// </summary>
    public int CalculatePurchaseCost(int amount, string currency = "coins")
    {
        return _config.CalculatePurchaseCost(amount, currency);
    }

    /// <summary>
    /// 에너지 상태 정보 반환
    /// </summary>
    public EnergyStateInfo GetStateInfo()
    {
        if (_energyData == null)
            return null;

        return new EnergyStateInfo
        {
            CurrentEnergy = CurrentEnergy,
            MaxEnergy = MaxEnergy,
            EnergyPercentage = EnergyPercentage,
            IsEnergyLow = IsEnergyLow,
            IsEnergyFull = IsEnergyFull,
            CanUseEnergy = CanUseEnergy,
            EnergyDeficit = _energyData.EnergyDeficit,
            Timestamp = DateTime.Now
        };
    }
    #endregion
}

#region Response Data Structures
/// <summary>
/// 에너지 구매 응답 데이터
/// </summary>
[System.Serializable]
public class EnergyPurchaseResponse
{
    public bool success;
    public int purchasedAmount;
    public int totalCost;
    public int newEnergyAmount;
    public string errorMessage;
}