using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 매칭 시스템용 재연결 관리자
/// WebSocket 연결이 끊어졌을 때 자동으로 재연결을 시도하고 상태를 복구
/// </summary>
public class MatchingReconnection : MonoBehaviour
{
    #region Events
    /// <summary>재연결 시작 이벤트</summary>
    public event Action OnReconnectionStarted;
    
    /// <summary>재연결 성공 이벤트</summary>
    public event Action OnReconnectionSuccess;
    
    /// <summary>재연결 실패 이벤트</summary>
    public event Action<int> OnReconnectionFailed; // 시도 횟수
    
    /// <summary>최대 재연결 시도 횟수 도달 이벤트</summary>
    public event Action OnMaxAttemptsReached;
    
    /// <summary>재연결 진행 상태 이벤트</summary>
    public event Action<int, int> OnReconnectionProgress; // (현재 시도, 최대 시도)
    
    /// <summary>상태 복구 시작 이벤트</summary>
    public event Action OnStateRecoveryStarted;
    
    /// <summary>상태 복구 완료 이벤트</summary>
    public event Action<bool> OnStateRecoveryCompleted; // 성공 여부
    #endregion

    #region Private Fields
    private NetworkManager _networkManager;
    private Coroutine _reconnectionCoroutine;
    
    // Reconnection state
    private bool _isReconnecting = false;
    private int _currentAttempt = 0;
    private DateTime _disconnectionTime;
    private DateTime _lastAttemptTime;
    
    // Saved state for recovery
    private ReconnectionState _savedState;
    
    // Configuration
    [SerializeField] private int _maxReconnectionAttempts = 5;
    [SerializeField] private float _initialRetryDelay = 2f;
    [SerializeField] private float _maxRetryDelay = 30f;
    [SerializeField] private float _backoffMultiplier = 2f;
    [SerializeField] private float _connectionTimeout = 10f;
    [SerializeField] private bool _enableExponentialBackoff = true;
    [SerializeField] private bool _enableStateRecovery = true;
    
    private bool _isDestroyed = false;
    #endregion

    #region Properties
    /// <summary>재연결 진행 중인지</summary>
    public bool IsReconnecting => _isReconnecting;
    
    /// <summary>현재 재연결 시도 횟수</summary>
    public int CurrentAttempt => _currentAttempt;
    
    /// <summary>최대 재연결 시도 횟수</summary>
    public int MaxAttempts => _maxReconnectionAttempts;
    
    /// <summary>연결이 끊어진 시간</summary>
    public TimeSpan DisconnectionDuration => _disconnectionTime != default 
        ? DateTime.UtcNow - _disconnectionTime 
        : TimeSpan.Zero;
    
    /// <summary>마지막 재연결 시도 시간</summary>
    public TimeSpan TimeSinceLastAttempt => _lastAttemptTime != default 
        ? DateTime.UtcNow - _lastAttemptTime 
        : TimeSpan.Zero;
    
    /// <summary>저장된 상태 존재 여부</summary>
    public bool HasSavedState => _savedState != null && _savedState.IsValid();
    
    /// <summary>상태 복구 활성화 여부</summary>
    public bool StateRecoveryEnabled
    {
        get => _enableStateRecovery;
        set => _enableStateRecovery = value;
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // NetworkManager 찾기
        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("[MatchingReconnection] NetworkManager not found in scene");
        }
    }

    private void OnDestroy()
    {
        _isDestroyed = true;
        StopReconnection();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _isReconnecting)
        {
            // 앱이 다시 활성화되면 즉시 재연결 시도
            Debug.Log("[MatchingReconnection] App resumed, attempting immediate reconnection");
            StartReconnection();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _isReconnecting)
        {
            // 포커스 복구 시 재연결 시도
            Debug.Log("[MatchingReconnection] App focused, attempting immediate reconnection");
            StartReconnection();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 재연결 시작
    /// </summary>
    /// <param name="saveCurrentState">현재 상태 저장 여부</param>
    public void StartReconnection(bool saveCurrentState = true)
    {
        if (_isDestroyed)
        {
            Debug.LogWarning("[MatchingReconnection] Cannot start reconnection: Component is destroyed");
            return;
        }

        if (_networkManager == null)
        {
            Debug.LogError("[MatchingReconnection] Cannot start reconnection: NetworkManager not available");
            return;
        }

        // 이미 연결된 상태라면 재연결 불필요
        if (_networkManager.IsWebSocketConnected())
        {
            Debug.Log("[MatchingReconnection] Already connected, stopping reconnection");
            StopReconnection();
            return;
        }

        // 이미 재연결 중이라면 새로운 시도로 리셋
        if (_isReconnecting)
        {
            Debug.Log("[MatchingReconnection] Already reconnecting, restarting process");
            StopReconnection();
        }

        try
        {
            // 현재 상태 저장 (선택적)
            if (saveCurrentState && _enableStateRecovery)
            {
                SaveCurrentState();
            }

            // 재연결 상태 초기화
            _isReconnecting = true;
            _currentAttempt = 0;
            _disconnectionTime = DateTime.UtcNow;
            _lastAttemptTime = default;

            // 재연결 코루틴 시작
            _reconnectionCoroutine = StartCoroutine(ReconnectionCoroutine());
            
            OnReconnectionStarted?.Invoke();
            Debug.Log("[MatchingReconnection] Reconnection process started");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingReconnection] Failed to start reconnection: {e.Message}");
            _isReconnecting = false;
        }
    }

    /// <summary>
    /// 재연결 중지
    /// </summary>
    public void StopReconnection()
    {
        if (_reconnectionCoroutine != null)
        {
            try
            {
                StopCoroutine(_reconnectionCoroutine);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchingReconnection] Error stopping reconnection coroutine: {e.Message}");
            }
            finally
            {
                _reconnectionCoroutine = null;
            }
        }

        if (_isReconnecting)
        {
            _isReconnecting = false;
            Debug.Log("[MatchingReconnection] Reconnection process stopped");
        }
    }

    /// <summary>
    /// 즉시 재연결 시도 (현재 대기 시간 무시)
    /// </summary>
    /// <returns>즉시 시도 성공 여부</returns>
    public bool AttemptImmediateReconnection()
    {
        if (!_isReconnecting)
        {
            StartReconnection();
            return true;
        }

        // 이미 재연결 중이라면 현재 시도를 중단하고 즉시 시도
        Debug.Log("[MatchingReconnection] Forcing immediate reconnection attempt");
        StopReconnection();
        StartReconnection(false); // 상태 중복 저장 방지
        
        return true;
    }

    /// <summary>
    /// 재연결 설정 업데이트
    /// </summary>
    /// <param name="maxAttempts">최대 시도 횟수</param>
    /// <param name="initialDelay">초기 대기 시간</param>
    /// <param name="maxDelay">최대 대기 시간</param>
    /// <param name="backoffMultiplier">백오프 승수</param>
    public void UpdateReconnectionConfig(int maxAttempts = -1, float initialDelay = -1f, 
        float maxDelay = -1f, float backoffMultiplier = -1f)
    {
        if (maxAttempts > 0)
            _maxReconnectionAttempts = maxAttempts;
        
        if (initialDelay > 0)
            _initialRetryDelay = initialDelay;
            
        if (maxDelay > 0)
            _maxRetryDelay = maxDelay;
            
        if (backoffMultiplier > 0)
            _backoffMultiplier = backoffMultiplier;

        Debug.Log($"[MatchingReconnection] Configuration updated - Max attempts: {_maxReconnectionAttempts}, " +
                  $"Initial delay: {_initialRetryDelay}s, Max delay: {_maxRetryDelay}s");
    }

    /// <summary>
    /// 저장된 상태 강제 복구
    /// </summary>
    /// <returns>복구 성공 여부</returns>
    public async System.Threading.Tasks.Task<bool> ForceStateRecovery()
    {
        if (!_enableStateRecovery || !HasSavedState)
        {
            Debug.LogWarning("[MatchingReconnection] No saved state available for recovery");
            return false;
        }

        return await RecoverSavedState();
    }

    /// <summary>
    /// 저장된 상태 정리
    /// </summary>
    public void ClearSavedState()
    {
        _savedState = null;
        Debug.Log("[MatchingReconnection] Saved state cleared");
    }

    /// <summary>
    /// 재연결 통계 정보
    /// </summary>
    /// <returns>통계 정보</returns>
    public ReconnectionStats GetReconnectionStats()
    {
        return new ReconnectionStats
        {
            IsReconnecting = _isReconnecting,
            CurrentAttempt = _currentAttempt,
            MaxAttempts = _maxReconnectionAttempts,
            DisconnectionDuration = DisconnectionDuration,
            TimeSinceLastAttempt = TimeSinceLastAttempt,
            HasSavedState = HasSavedState,
            StateRecoveryEnabled = _enableStateRecovery
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 현재 상태 저장
    /// </summary>
    private void SaveCurrentState()
    {
        try
        {
            _savedState = new ReconnectionState
            {
                SaveTime = DateTime.UtcNow,
                WasConnected = _networkManager?.IsWebSocketConnected() ?? false,
                // 추후 MatchingManager와 연동 시 매칭 상태 저장 추가
                ConnectionQuality = _networkManager?.GetWebSocketConnectionQuality()
            };

            Debug.Log("[MatchingReconnection] Current state saved for recovery");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingReconnection] Failed to save current state: {e.Message}");
        }
    }

    /// <summary>
    /// 저장된 상태 복구
    /// </summary>
    /// <returns>복구 성공 여부</returns>
    private async System.Threading.Tasks.Task<bool> RecoverSavedState()
    {
        if (!HasSavedState)
            return false;

        try
        {
            OnStateRecoveryStarted?.Invoke();
            Debug.Log("[MatchingReconnection] Starting state recovery");

            // 기본적으로 상태 복구는 성공으로 간주
            // 추후 MatchingManager와 연동 시 실제 매칭 상태 복구 로직 추가
            bool recoverySuccess = true;

            // 상태 복구 완료 후 저장된 상태 정리
            if (recoverySuccess)
            {
                ClearSavedState();
                Debug.Log("[MatchingReconnection] State recovery completed successfully");
            }
            else
            {
                Debug.LogWarning("[MatchingReconnection] State recovery failed");
            }

            OnStateRecoveryCompleted?.Invoke(recoverySuccess);
            return recoverySuccess;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingReconnection] State recovery failed: {e.Message}");
            OnStateRecoveryCompleted?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    /// 다음 재연결 시도까지의 대기 시간 계산
    /// </summary>
    /// <param name="attemptNumber">시도 횟수</param>
    /// <returns>대기 시간(초)</returns>
    private float CalculateRetryDelay(int attemptNumber)
    {
        if (!_enableExponentialBackoff)
        {
            return _initialRetryDelay;
        }

        float delay = _initialRetryDelay * Mathf.Pow(_backoffMultiplier, attemptNumber - 1);
        return Mathf.Min(delay, _maxRetryDelay);
    }

    /// <summary>
    /// 단일 재연결 시도
    /// </summary>
    /// <returns>연결 성공 여부</returns>
    private async System.Threading.Tasks.Task<bool> AttemptSingleReconnection()
    {
        try
        {
            Debug.Log($"[MatchingReconnection] Attempting reconnection (attempt {_currentAttempt}/{_maxReconnectionAttempts})");
            _lastAttemptTime = DateTime.UtcNow;

            // WebSocket 재연결 시도
            bool connected = await _networkManager.ConnectWebSocketAsync();
            
            if (connected)
            {
                Debug.Log("[MatchingReconnection] WebSocket reconnection successful");
                
                // 상태 복구 시도
                if (_enableStateRecovery && HasSavedState)
                {
                    bool stateRecovered = await RecoverSavedState();
                    Debug.Log($"[MatchingReconnection] State recovery: {(stateRecovered ? "successful" : "failed")}");
                }

                return true;
            }
            else
            {
                Debug.LogWarning($"[MatchingReconnection] Reconnection attempt {_currentAttempt} failed");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingReconnection] Reconnection attempt {_currentAttempt} failed with exception: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 재연결 코루틴
    /// </summary>
    private IEnumerator ReconnectionCoroutine()
    {
        while (_isReconnecting && !_isDestroyed && _currentAttempt < _maxReconnectionAttempts)
        {
            _currentAttempt++;
            OnReconnectionProgress?.Invoke(_currentAttempt, _maxReconnectionAttempts);

            // 첫 번째 시도가 아니라면 대기
            if (_currentAttempt > 1)
            {
                float delay = CalculateRetryDelay(_currentAttempt);
                Debug.Log($"[MatchingReconnection] Waiting {delay:F1}s before attempt {_currentAttempt}");
                
                yield return new WaitForSeconds(delay);
                
                // 대기 중에 재연결이 중단되었는지 확인
                if (!_isReconnecting || _isDestroyed)
                    yield break;
            }

            // 재연결 시도
            bool reconnectionTask = false;
            
            // Unity 코루틴에서 async 메서드 호출을 위한 처리
            StartCoroutine(PerformReconnectionAttempt((success) => reconnectionTask = success));
            
            // 재연결 시도 완료까지 대기
            yield return new WaitUntil(() => reconnectionTask || !_isReconnecting);
            
            if (_isDestroyed || !_isReconnecting)
                yield break;

            if (reconnectionTask)
            {
                // 재연결 성공
                _isReconnecting = false;
                OnReconnectionSuccess?.Invoke();
                Debug.Log($"[MatchingReconnection] Reconnection successful after {_currentAttempt} attempts");
                yield break;
            }
            else
            {
                // 재연결 실패
                OnReconnectionFailed?.Invoke(_currentAttempt);
                Debug.LogWarning($"[MatchingReconnection] Reconnection attempt {_currentAttempt} failed");
            }
        }

        // 최대 시도 횟수 도달
        if (_currentAttempt >= _maxReconnectionAttempts)
        {
            _isReconnecting = false;
            OnMaxAttemptsReached?.Invoke();
            Debug.LogError($"[MatchingReconnection] Maximum reconnection attempts ({_maxReconnectionAttempts}) reached");
        }
    }

    /// <summary>
    /// 재연결 시도 수행 (코루틴용)
    /// </summary>
    /// <param name="callback">결과 콜백</param>
    private IEnumerator PerformReconnectionAttempt(Action<bool> callback)
    {
        bool success = false;
        bool completed = false;

        // async 메서드를 별도 스레드에서 실행
        System.Threading.Tasks.Task.Run(async () => {
            try
            {
                success = await AttemptSingleReconnection();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MatchingReconnection] Async reconnection failed: {e.Message}");
                success = false;
            }
            finally
            {
                completed = true;
            }
        });

        // 완료 대기 (타임아웃 포함)
        float timeout = _connectionTimeout;
        while (!completed && timeout > 0)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        if (!completed)
        {
            Debug.LogWarning("[MatchingReconnection] Reconnection attempt timed out");
            success = false;
        }

        callback?.Invoke(success);
    }
    #endregion
}

/// <summary>
/// 재연결을 위한 상태 정보
/// </summary>
[Serializable]
public class ReconnectionState
{
    public DateTime SaveTime { get; set; }
    public bool WasConnected { get; set; }
    public WebSocketConnectionQuality ConnectionQuality { get; set; }
    
    // 추후 MatchingManager와 연동 시 추가될 필드들
    // public MatchingState MatchingState { get; set; }
    // public string CurrentRoomId { get; set; }
    // public List<PlayerInfo> CurrentPlayers { get; set; }

    /// <summary>저장된 상태가 유효한지 확인</summary>
    public bool IsValid()
    {
        // 상태가 너무 오래된 경우 (5분 이상) 무효로 처리
        return DateTime.UtcNow - SaveTime < TimeSpan.FromMinutes(5);
    }
}

/// <summary>
/// 재연결 통계 정보
/// </summary>
public class ReconnectionStats
{
    public bool IsReconnecting { get; set; }
    public int CurrentAttempt { get; set; }
    public int MaxAttempts { get; set; }
    public TimeSpan DisconnectionDuration { get; set; }
    public TimeSpan TimeSinceLastAttempt { get; set; }
    public bool HasSavedState { get; set; }
    public bool StateRecoveryEnabled { get; set; }
    
    public override string ToString()
    {
        return $"Reconnecting: {IsReconnecting}, Attempt: {CurrentAttempt}/{MaxAttempts}, " +
               $"Disconnected: {DisconnectionDuration.TotalSeconds:F1}s, " +
               $"Last Attempt: {TimeSinceLastAttempt.TotalSeconds:F1}s ago, " +
               $"Saved State: {HasSavedState}";
    }
}