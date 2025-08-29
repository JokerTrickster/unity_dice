using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 앱 시작 시 인증 처리기
/// 앱 부팅 과정에서 자동 로그인, 인증 상태 복원, 초기화 시퀀스를 관리합니다.
/// </summary>
public class StartupAuthenticator : MonoBehaviour
{
    #region Singleton
    private static StartupAuthenticator _instance;
    public static StartupAuthenticator Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("StartupAuthenticator");
                _instance = go.AddComponent<StartupAuthenticator>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Startup Configuration")]
    [SerializeField] private bool enableStartupAuth = true;
    [SerializeField] private float startupTimeout = 30f;
    [SerializeField] private bool showStartupProgress = true;
    [SerializeField] private bool bypassOnDebug = true;

    [Header("Initialization Order")]
    [SerializeField] private bool initializeSecureStorage = true;
    [SerializeField] private bool initializeTokenManager = true;
    [SerializeField] private bool initializeAuthenticationManager = true;
    [SerializeField] private bool initializeAutoLogin = true;

    [Header("Fallback Behavior")]
    [SerializeField] private bool fallbackToOfflineMode = false;
    [SerializeField] private float minInitializationTime = 2f; // 최소 스플래시 표시 시간
    #endregion

    #region Events
    /// <summary>
    /// 시작 인증이 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action OnStartupAuthBegin;
    
    /// <summary>
    /// 시작 인증이 완료될 때 발생하는 이벤트 (성공/실패, 메시지)
    /// </summary>
    public static event Action<bool, string> OnStartupAuthCompleted;
    
    /// <summary>
    /// 시작 인증 진행 상황 업데이트 이벤트
    /// </summary>
    public static event Action<string, float> OnStartupProgress;

    /// <summary>
    /// 초기화 단계 완료 이벤트
    /// </summary>
    public static event Action<StartupPhase> OnPhaseCompleted;

    /// <summary>
    /// 오프라인 모드로 전환 이벤트
    /// </summary>
    public static event Action OnFallbackToOfflineMode;
    #endregion

    #region Private Fields
    private bool _isStartupCompleted = false;
    private bool _isStartupInProgress = false;
    private DateTime _startupBeginTime;
    private StartupResult _lastResult = StartupResult.Unknown;
    private StartupPhase _currentPhase = StartupPhase.NotStarted;
    #endregion

    #region Properties
    /// <summary>
    /// 시작 인증 활성화 여부
    /// </summary>
    public bool EnableStartupAuth
    {
        get => enableStartupAuth && (!Debug.isDebugBuild || !bypassOnDebug);
    }
    
    /// <summary>
    /// 시작 인증 진행 중 여부
    /// </summary>
    public bool IsStartupInProgress => _isStartupInProgress;
    
    /// <summary>
    /// 시작 인증 완료 여부
    /// </summary>
    public bool IsStartupCompleted => _isStartupCompleted;
    
    /// <summary>
    /// 현재 단계
    /// </summary>
    public StartupPhase CurrentPhase => _currentPhase;
    
    /// <summary>
    /// 마지막 결과
    /// </summary>
    public StartupResult LastResult => _lastResult;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 자동으로 시작 인증 실행
        if (EnableStartupAuth && !_isStartupCompleted && !_isStartupInProgress)
        {
            StartCoroutine(BeginStartupAuthentication());
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        OnStartupAuthBegin = null;
        OnStartupAuthCompleted = null;
        OnStartupProgress = null;
        OnPhaseCompleted = null;
        OnFallbackToOfflineMode = null;
    }
    #endregion

    #region Startup Authentication Flow
    /// <summary>
    /// 시작 인증 프로세스 시작
    /// </summary>
    public void BeginStartup()
    {
        if (_isStartupInProgress || _isStartupCompleted)
        {
            Debug.LogWarning("[StartupAuthenticator] Startup already in progress or completed");
            return;
        }

        StartCoroutine(BeginStartupAuthentication());
    }

    /// <summary>
    /// 시작 인증 코루틴
    /// </summary>
    private IEnumerator BeginStartupAuthentication()
    {
        _isStartupInProgress = true;
        _startupBeginTime = DateTime.UtcNow;
        _currentPhase = StartupPhase.Initializing;

        OnStartupAuthBegin?.Invoke();
        UpdateProgress("앱을 시작하고 있습니다...", 0.0f);

        Debug.Log("[StartupAuthenticator] Starting startup authentication sequence");

        try
        {
            // 비동기 초기화 작업 시작
            var startupTask = PerformStartupSequenceAsync();
            
            // 최소 초기화 시간과 실제 작업 완료를 모두 기다림
            float minTimeElapsed = 0f;
            
            while (!startupTask.IsCompleted || minTimeElapsed < minInitializationTime)
            {
                minTimeElapsed += Time.deltaTime;
                
                // 타임아웃 체크
                if (minTimeElapsed > startupTimeout)
                {
                    Debug.LogError("[StartupAuthenticator] Startup timeout exceeded");
                    _lastResult = StartupResult.Timeout;
                    break;
                }
                
                yield return null;
            }

            // 결과 처리
            if (startupTask.IsCompleted)
            {
                _lastResult = startupTask.Result;
            }
            
            HandleStartupResult(_lastResult);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StartupAuthenticator] Startup failed with exception: {ex.Message}");
            _lastResult = StartupResult.Failed;
            HandleStartupResult(_lastResult);
        }
        finally
        {
            _isStartupInProgress = false;
            _isStartupCompleted = true;
        }
    }

    /// <summary>
    /// 시작 시퀀스 수행 (비동기)
    /// </summary>
    private async Task<StartupResult> PerformStartupSequenceAsync()
    {
        try
        {
            // Phase 1: Core Initialization
            _currentPhase = StartupPhase.CoreInitialization;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("핵심 시스템을 초기화하고 있습니다...", 0.1f);
            
            var coreResult = await InitializeCoreSystemsAsync();
            if (!coreResult.Success)
            {
                Debug.LogError($"[StartupAuthenticator] Core initialization failed: {coreResult.ErrorMessage}");
                return StartupResult.InitializationFailed;
            }

            // Phase 2: Security & Storage
            _currentPhase = StartupPhase.SecurityInitialization;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("보안 스토리지를 초기화하고 있습니다...", 0.2f);
            
            var securityResult = await InitializeSecuritySystemsAsync();
            if (!securityResult.Success)
            {
                Debug.LogError($"[StartupAuthenticator] Security initialization failed: {securityResult.ErrorMessage}");
                return StartupResult.SecurityInitializationFailed;
            }

            // Phase 3: Token Management
            _currentPhase = StartupPhase.TokenInitialization;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("토큰 관리자를 초기화하고 있습니다...", 0.3f);
            
            var tokenResult = await InitializeTokenSystemsAsync();
            if (!tokenResult.Success)
            {
                Debug.LogError($"[StartupAuthenticator] Token initialization failed: {tokenResult.ErrorMessage}");
                return StartupResult.TokenInitializationFailed;
            }

            // Phase 4: Authentication Manager
            _currentPhase = StartupPhase.AuthenticationInitialization;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("인증 시스템을 초기화하고 있습니다...", 0.4f);
            
            var authResult = await InitializeAuthenticationSystemsAsync();
            if (!authResult.Success)
            {
                Debug.LogError($"[StartupAuthenticator] Authentication initialization failed: {authResult.ErrorMessage}");
                return StartupResult.AuthenticationInitializationFailed;
            }

            // Phase 5: Auto-Login Preparation
            _currentPhase = StartupPhase.AutoLoginPreparation;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("자동 로그인을 준비하고 있습니다...", 0.6f);
            
            var autoLoginPrepResult = await PrepareAutoLoginAsync();
            if (!autoLoginPrepResult.Success)
            {
                Debug.LogWarning($"[StartupAuthenticator] Auto-login preparation failed: {autoLoginPrepResult.ErrorMessage}");
                // 자동 로그인 준비 실패는 치명적이지 않음
            }

            // Phase 6: Auto-Login Attempt
            _currentPhase = StartupPhase.AutoLoginAttempt;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("자동 로그인을 시도하고 있습니다...", 0.8f);
            
            var autoLoginResult = await AttemptAutoLoginAsync();
            
            // Phase 7: Completion
            _currentPhase = StartupPhase.Completed;
            OnPhaseCompleted?.Invoke(_currentPhase);
            UpdateProgress("시작 과정을 완료하고 있습니다...", 1.0f);

            // 자동 로그인 결과에 따른 최종 상태 결정
            return autoLoginResult.Success ? StartupResult.Success : StartupResult.AutoLoginFailed;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StartupAuthenticator] Startup sequence failed: {ex.Message}");
            return StartupResult.Failed;
        }
    }
    #endregion

    #region Initialization Phases
    /// <summary>
    /// 핵심 시스템 초기화
    /// </summary>
    private async Task<StartupOperationResult> InitializeCoreSystemsAsync()
    {
        try
        {
            // Unity 시스템 준비 확인
            await Task.Delay(100);
            
            // 기본 게임 오브젝트 및 매니저 초기화
            if (FindObjectOfType<AuthenticationManager>() == null && initializeAuthenticationManager)
            {
                var authManager = AuthenticationManager.Instance; // 싱글톤 생성
                await Task.Delay(200); // 초기화 대기
            }

            Debug.Log("[StartupAuthenticator] Core systems initialized");
            return new StartupOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 보안 시스템 초기화
    /// </summary>
    private async Task<StartupOperationResult> InitializeSecuritySystemsAsync()
    {
        try
        {
            if (initializeSecureStorage)
            {
                if (!SecureStorage.IsInitialized)
                {
                    SecureStorage.Initialize();
                    await Task.Delay(100);
                }

                if (!SecureStorage.IsInitialized)
                {
                    throw new InvalidOperationException("SecureStorage initialization failed");
                }
            }

            Debug.Log("[StartupAuthenticator] Security systems initialized");
            return new StartupOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 토큰 시스템 초기화
    /// </summary>
    private async Task<StartupOperationResult> InitializeTokenSystemsAsync()
    {
        try
        {
            if (initializeTokenManager)
            {
                var tokenManager = TokenManager.Instance; // 싱글톤 생성 및 초기화
                
                // TokenManager 초기화 대기
                int waitCount = 0;
                while (!tokenManager.IsInitialized && waitCount < 50) // 최대 5초
                {
                    await Task.Delay(100);
                    waitCount++;
                }

                if (!tokenManager.IsInitialized)
                {
                    throw new InvalidOperationException("TokenManager initialization failed or timeout");
                }
            }

            Debug.Log("[StartupAuthenticator] Token systems initialized");
            return new StartupOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 인증 시스템 초기화
    /// </summary>
    private async Task<StartupOperationResult> InitializeAuthenticationSystemsAsync()
    {
        try
        {
            if (initializeAuthenticationManager && AuthenticationManager.Instance != null)
            {
                // AuthenticationManager 초기화 대기
                int waitCount = 0;
                while (!AuthenticationManager.Instance.IsInitialized && waitCount < 50)
                {
                    await Task.Delay(100);
                    waitCount++;
                }

                if (!AuthenticationManager.Instance.IsInitialized)
                {
                    Debug.LogWarning("[StartupAuthenticator] AuthenticationManager initialization timeout, continuing...");
                }
            }

            Debug.Log("[StartupAuthenticator] Authentication systems initialized");
            return new StartupOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 자동 로그인 준비
    /// </summary>
    private async Task<StartupOperationResult> PrepareAutoLoginAsync()
    {
        try
        {
            if (initializeAutoLogin)
            {
                var autoLoginManager = AutoLoginManager.Instance; // 싱글톤 생성
                
                // AutoLoginManager 초기화 대기
                int waitCount = 0;
                while (!autoLoginManager.IsInitialized && waitCount < 30) // 최대 3초
                {
                    await Task.Delay(100);
                    waitCount++;
                }

                if (!autoLoginManager.IsInitialized)
                {
                    throw new InvalidOperationException("AutoLoginManager initialization failed or timeout");
                }
            }

            Debug.Log("[StartupAuthenticator] Auto-login preparation completed");
            return new StartupOperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 자동 로그인 시도
    /// </summary>
    private async Task<StartupOperationResult> AttemptAutoLoginAsync()
    {
        try
        {
            var autoLoginManager = AutoLoginManager.Instance;
            if (autoLoginManager == null || !autoLoginManager.IsInitialized)
            {
                return new StartupOperationResult
                {
                    Success = false,
                    ErrorMessage = "AutoLoginManager not available"
                };
            }

            if (!autoLoginManager.CanAttemptAutoLogin)
            {
                Debug.Log("[StartupAuthenticator] Auto-login conditions not met, skipping");
                return new StartupOperationResult
                {
                    Success = true,
                    Message = "Auto-login skipped - conditions not met"
                };
            }

            // 자동 로그인 시도
            var result = await autoLoginManager.TryAutoLoginAsync();
            
            if (result == AutoLoginResult.Success)
            {
                Debug.Log("[StartupAuthenticator] Auto-login succeeded during startup");
                return new StartupOperationResult { Success = true };
            }
            else
            {
                Debug.LogWarning($"[StartupAuthenticator] Auto-login failed during startup: {result}");
                return new StartupOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Auto-login failed: {result}"
                };
            }
        }
        catch (Exception ex)
        {
            return new StartupOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    #endregion

    #region Result Handling
    /// <summary>
    /// 시작 결과 처리
    /// </summary>
    private void HandleStartupResult(StartupResult result)
    {
        string message = GetResultMessage(result);
        bool success = result == StartupResult.Success || result == StartupResult.AutoLoginFailed;
        
        Debug.Log($"[StartupAuthenticator] Startup completed with result: {result} - {message}");
        
        OnStartupAuthCompleted?.Invoke(success, message);

        // 오프라인 모드 전환 처리
        if (!success && fallbackToOfflineMode)
        {
            Debug.Log("[StartupAuthenticator] Falling back to offline mode");
            OnFallbackToOfflineMode?.Invoke();
        }
    }

    /// <summary>
    /// 결과 메시지 반환
    /// </summary>
    private string GetResultMessage(StartupResult result)
    {
        return result switch
        {
            StartupResult.Success => "앱이 성공적으로 시작되었습니다.",
            StartupResult.AutoLoginFailed => "앱이 시작되었지만 자동 로그인에 실패했습니다.",
            StartupResult.InitializationFailed => "초기화 중 오류가 발생했습니다.",
            StartupResult.SecurityInitializationFailed => "보안 시스템 초기화에 실패했습니다.",
            StartupResult.TokenInitializationFailed => "토큰 시스템 초기화에 실패했습니다.",
            StartupResult.AuthenticationInitializationFailed => "인증 시스템 초기화에 실패했습니다.",
            StartupResult.Timeout => "시작 과정이 시간 초과되었습니다.",
            StartupResult.Failed => "시작 과정에서 오류가 발생했습니다.",
            _ => "알 수 없는 상태입니다."
        };
    }
    #endregion

    #region Progress Updates
    /// <summary>
    /// 진행 상황 업데이트
    /// </summary>
    private void UpdateProgress(string message, float progress)
    {
        if (showStartupProgress)
        {
            Debug.Log($"[StartupAuthenticator] {message} ({progress * 100:F0}%)");
            OnStartupProgress?.Invoke(message, progress);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 시작 상태 정보 반환
    /// </summary>
    public StartupStatus GetStatus()
    {
        return new StartupStatus
        {
            IsEnabled = EnableStartupAuth,
            IsInProgress = _isStartupInProgress,
            IsCompleted = _isStartupCompleted,
            CurrentPhase = _currentPhase,
            LastResult = _lastResult,
            StartupBeginTime = _startupBeginTime,
            ElapsedTime = _isStartupInProgress ? DateTime.UtcNow - _startupBeginTime : TimeSpan.Zero
        };
    }

    /// <summary>
    /// 강제 재시작
    /// </summary>
    public void ForceRestart()
    {
        if (_isStartupInProgress)
        {
            Debug.LogWarning("[StartupAuthenticator] Cannot restart while startup is in progress");
            return;
        }

        _isStartupCompleted = false;
        _currentPhase = StartupPhase.NotStarted;
        _lastResult = StartupResult.Unknown;
        
        BeginStartup();
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 시작 단계
/// </summary>
public enum StartupPhase
{
    NotStarted,
    Initializing,
    CoreInitialization,
    SecurityInitialization,
    TokenInitialization,
    AuthenticationInitialization,
    AutoLoginPreparation,
    AutoLoginAttempt,
    Completed
}

/// <summary>
/// 시작 결과
/// </summary>
public enum StartupResult
{
    Success,
    AutoLoginFailed,
    InitializationFailed,
    SecurityInitializationFailed,
    TokenInitializationFailed,
    AuthenticationInitializationFailed,
    Timeout,
    Failed,
    Unknown
}

/// <summary>
/// 시작 작업 결과
/// </summary>
public class StartupOperationResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// 시작 상태 정보
/// </summary>
[Serializable]
public class StartupStatus
{
    public bool IsEnabled;
    public bool IsInProgress;
    public bool IsCompleted;
    public StartupPhase CurrentPhase;
    public StartupResult LastResult;
    public DateTime StartupBeginTime;
    public TimeSpan ElapsedTime;
}
#endregion