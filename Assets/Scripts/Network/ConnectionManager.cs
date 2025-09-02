using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// WebSocket 연결 관리자
/// 자동 재연결, 연결 상태 모니터링, 하트비트 관리를 담당
/// </summary>
public class ConnectionManager : IDisposable
{
    #region Events
    /// <summary>연결 상태 변경 이벤트</summary>
    public event Action<ConnectionState> OnConnectionStateChanged;
    
    /// <summary>재연결 시도 이벤트</summary>
    public event Action<int, int> OnReconnectionAttempt;
    
    /// <summary>재연결 성공 이벤트</summary>
    public event Action OnReconnected;
    
    /// <summary>재연결 실패 이벤트</summary>
    public event Action<string> OnReconnectionFailed;
    
    /// <summary>하트비트 상태 이벤트</summary>
    public event Action<bool> OnHeartbeatStatusChanged;
    #endregion

    #region Private Fields
    private readonly WebSocketConfig _config;
    private readonly object _lockObject = new();
    private volatile ConnectionState _connectionState = ConnectionState.Disconnected;
    private volatile bool _isDisposed = false;
    private volatile bool _shouldReconnect = false;
    private volatile int _currentReconnectAttempt = 0;
    
    private CancellationTokenSource _reconnectionCancellationToken;
    private CancellationTokenSource _heartbeatCancellationToken;
    private Task _reconnectionTask;
    private Task _heartbeatTask;
    
    private Func<Task<bool>> _connectFunc;
    private Func<Task> _disconnectFunc;
    private Func<string, Task<bool>> _sendMessageFunc;
    private Func<bool> _isConnectedFunc;
    
    private DateTime _lastHeartbeatSent;
    private DateTime _lastHeartbeatReceived;
    private volatile bool _heartbeatResponsePending = false;
    #endregion

    #region Properties
    /// <summary>현재 연결 상태</summary>
    public ConnectionState CurrentState => _connectionState;
    
    /// <summary>연결된 상태인지</summary>
    public bool IsConnected => _connectionState == ConnectionState.Connected;
    
    /// <summary>재연결 중인지</summary>
    public bool IsReconnecting => _connectionState == ConnectionState.Reconnecting;
    
    /// <summary>현재 재연결 시도 횟수</summary>
    public int CurrentReconnectAttempt => _currentReconnectAttempt;
    
    /// <summary>최대 재연결 시도 횟수</summary>
    public int MaxReconnectAttempts => _config.MaxReconnectAttempts;
    
    /// <summary>자동 재연결 활성화 여부</summary>
    public bool AutoReconnectEnabled => _shouldReconnect && _config.EnableAutoReconnect;
    #endregion

    #region Constructor
    /// <summary>
    /// 연결 관리자 초기화
    /// </summary>
    /// <param name="config">WebSocket 설정</param>
    public ConnectionManager(WebSocketConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (_config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Initialized");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 연결 함수들 설정
    /// </summary>
    public void SetConnectionFunctions(
        Func<Task<bool>> connectFunc,
        Func<Task> disconnectFunc,
        Func<string, Task<bool>> sendMessageFunc,
        Func<bool> isConnectedFunc)
    {
        _connectFunc = connectFunc ?? throw new ArgumentNullException(nameof(connectFunc));
        _disconnectFunc = disconnectFunc ?? throw new ArgumentNullException(nameof(disconnectFunc));
        _sendMessageFunc = sendMessageFunc ?? throw new ArgumentNullException(nameof(sendMessageFunc));
        _isConnectedFunc = isConnectedFunc ?? throw new ArgumentNullException(nameof(isConnectedFunc));
        
        if (_config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Connection functions set");
        }
    }

    /// <summary>
    /// 연결 시작
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (_isDisposed)
        {
            Debug.LogError("[ConnectionManager] Cannot connect: ConnectionManager is disposed");
            return false;
        }

        if (_connectFunc == null)
        {
            Debug.LogError("[ConnectionManager] Cannot connect: Connection functions not set");
            return false;
        }

        lock (_lockObject)
        {
            if (_connectionState == ConnectionState.Connecting || _connectionState == ConnectionState.Connected)
            {
                if (_config.EnableLogging)
                {
                    Debug.Log($"[ConnectionManager] Already connecting or connected: {_connectionState}");
                }
                return _connectionState == ConnectionState.Connected;
            }

            SetConnectionState(ConnectionState.Connecting);
        }

        try
        {
            bool success = await _connectFunc();
            
            if (success)
            {
                SetConnectionState(ConnectionState.Connected);
                _currentReconnectAttempt = 0;
                _shouldReconnect = true;
                
                StartHeartbeat();
                
                if (_config.EnableLogging)
                {
                    Debug.Log("[ConnectionManager] Connected successfully");
                }
                
                return true;
            }
            else
            {
                SetConnectionState(ConnectionState.Disconnected);
                
                if (_config.EnableLogging)
                {
                    Debug.LogWarning("[ConnectionManager] Initial connection failed");
                }
                
                return false;
            }
        }
        catch (Exception e)
        {
            SetConnectionState(ConnectionState.Disconnected);
            Debug.LogError($"[ConnectionManager] Connection error: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 연결 종료
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_isDisposed)
            return;

        lock (_lockObject)
        {
            _shouldReconnect = false;
            StopReconnection();
            StopHeartbeat();
        }

        if (_disconnectFunc != null)
        {
            try
            {
                await _disconnectFunc();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConnectionManager] Disconnect error: {e.Message}");
            }
        }

        SetConnectionState(ConnectionState.Disconnected);
        
        if (_config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Disconnected");
        }
    }

    /// <summary>
    /// 연결 끊김 처리 (외부에서 호출)
    /// </summary>
    public void HandleConnectionLost()
    {
        if (_isDisposed || _connectionState == ConnectionState.Disconnected)
            return;

        SetConnectionState(ConnectionState.Disconnected);
        StopHeartbeat();

        if (_shouldReconnect && _config.EnableAutoReconnect)
        {
            StartReconnection();
        }
        
        if (_config.EnableLogging)
        {
            Debug.LogWarning("[ConnectionManager] Connection lost, auto-reconnect: " + AutoReconnectEnabled);
        }
    }

    /// <summary>
    /// 수동 재연결 시작
    /// </summary>
    public void StartManualReconnection()
    {
        if (_isDisposed)
            return;

        lock (_lockObject)
        {
            _shouldReconnect = true;
            _currentReconnectAttempt = 0;
        }

        StartReconnection();
        
        if (_config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Manual reconnection started");
        }
    }

    /// <summary>
    /// 재연결 중지
    /// </summary>
    public void StopReconnection()
    {
        lock (_lockObject)
        {
            _shouldReconnect = false;
            _reconnectionCancellationToken?.Cancel();
        }
        
        if (_config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Reconnection stopped");
        }
    }

    /// <summary>
    /// 하트비트 응답 처리 (외부에서 호출)
    /// </summary>
    public void HandleHeartbeatResponse()
    {
        _lastHeartbeatReceived = DateTime.UtcNow;
        _heartbeatResponsePending = false;
        
        if (_config.EnableDetailedLogging)
        {
            Debug.Log("[ConnectionManager] Heartbeat response received");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 연결 상태 설정
    /// </summary>
    private void SetConnectionState(ConnectionState newState)
    {
        if (_connectionState == newState)
            return;

        var oldState = _connectionState;
        _connectionState = newState;
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[ConnectionManager] State changed: {oldState} -> {newState}");
        }
        
        // Unity 메인 스레드에서 이벤트 발생
        UnityMainThreadDispatcher.Instance.Enqueue(() => {
            OnConnectionStateChanged?.Invoke(newState);
        });
    }

    /// <summary>
    /// 재연결 시작
    /// </summary>
    private void StartReconnection()
    {
        lock (_lockObject)
        {
            if (_connectionState == ConnectionState.Reconnecting || 
                _connectionState == ConnectionState.Connected)
                return;

            StopReconnection(); // 기존 재연결 작업 중지
            
            SetConnectionState(ConnectionState.Reconnecting);
            _reconnectionCancellationToken = new CancellationTokenSource();
            _reconnectionTask = ReconnectionLoop(_reconnectionCancellationToken.Token);
        }
    }

    /// <summary>
    /// 재연결 루프
    /// </summary>
    private async Task ReconnectionLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && 
                   _shouldReconnect && 
                   _currentReconnectAttempt < _config.MaxReconnectAttempts)
            {
                _currentReconnectAttempt++;
                
                // 재연결 시도 이벤트 발생
                UnityMainThreadDispatcher.Instance.Enqueue(() => {
                    OnReconnectionAttempt?.Invoke(_currentReconnectAttempt, _config.MaxReconnectAttempts);
                });

                if (_config.EnableLogging)
                {
                    Debug.Log($"[ConnectionManager] Reconnection attempt {_currentReconnectAttempt}/{_config.MaxReconnectAttempts}");
                }

                try
                {
                    bool success = await _connectFunc();
                    
                    if (success)
                    {
                        SetConnectionState(ConnectionState.Connected);
                        _currentReconnectAttempt = 0;
                        StartHeartbeat();
                        
                        UnityMainThreadDispatcher.Instance.Enqueue(() => {
                            OnReconnected?.Invoke();
                        });
                        
                        if (_config.EnableLogging)
                        {
                            Debug.Log("[ConnectionManager] Reconnected successfully");
                        }
                        
                        return; // 재연결 성공
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ConnectionManager] Reconnection attempt {_currentReconnectAttempt} failed: {e.Message}");
                }

                // 재시도 지연
                int delay = _config.GetRetryDelay(_currentReconnectAttempt - 1);
                await Task.Delay(delay, cancellationToken);
            }

            // 모든 재연결 시도 실패
            SetConnectionState(ConnectionState.Disconnected);
            string errorMessage = _currentReconnectAttempt >= _config.MaxReconnectAttempts 
                ? "Max reconnection attempts exceeded" 
                : "Reconnection cancelled";
            
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                OnReconnectionFailed?.Invoke(errorMessage);
            });
            
            Debug.LogError($"[ConnectionManager] {errorMessage}");
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[ConnectionManager] Reconnection cancelled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConnectionManager] Reconnection loop error: {e.Message}");
            SetConnectionState(ConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// 하트비트 시작
    /// </summary>
    private void StartHeartbeat()
    {
        if (!_config.EnableHeartbeat)
            return;

        lock (_lockObject)
        {
            StopHeartbeat();
            _heartbeatCancellationToken = new CancellationTokenSource();
            _heartbeatTask = HeartbeatLoop(_heartbeatCancellationToken.Token);
        }
    }

    /// <summary>
    /// 하트비트 중지
    /// </summary>
    private void StopHeartbeat()
    {
        _heartbeatCancellationToken?.Cancel();
    }

    /// <summary>
    /// 하트비트 루프
    /// </summary>
    private async Task HeartbeatLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                await Task.Delay(_config.HeartbeatInterval, cancellationToken);
                
                if (!IsConnected)
                    break;

                // 이전 하트비트 응답 대기 중인지 확인
                if (_heartbeatResponsePending)
                {
                    var timeSinceLastSent = DateTime.UtcNow - _lastHeartbeatSent;
                    if (timeSinceLastSent.TotalMilliseconds > _config.HeartbeatTimeout)
                    {
                        Debug.LogWarning("[ConnectionManager] Heartbeat timeout, connection may be lost");
                        UnityMainThreadDispatcher.Instance.Enqueue(() => {
                            OnHeartbeatStatusChanged?.Invoke(false);
                        });
                        HandleConnectionLost();
                        break;
                    }
                }

                try
                {
                    string heartbeatMessage = CreateHeartbeatMessage();
                    bool sent = await _sendMessageFunc(heartbeatMessage);
                    
                    if (sent)
                    {
                        _lastHeartbeatSent = DateTime.UtcNow;
                        _heartbeatResponsePending = true;
                        
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log("[ConnectionManager] Heartbeat sent");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[ConnectionManager] Failed to send heartbeat");
                        HandleConnectionLost();
                        break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ConnectionManager] Heartbeat send error: {e.Message}");
                    HandleConnectionLost();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[ConnectionManager] Heartbeat cancelled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConnectionManager] Heartbeat loop error: {e.Message}");
        }
    }

    /// <summary>
    /// 하트비트 메시지 생성
    /// </summary>
    private string CreateHeartbeatMessage()
    {
        return JsonUtility.ToJson(new { type = "heartbeat", timestamp = DateTime.UtcNow.ToString("O") });
    }
    #endregion

    #region IDisposable
    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _shouldReconnect = false;

        StopReconnection();
        StopHeartbeat();

        _reconnectionCancellationToken?.Dispose();
        _heartbeatCancellationToken?.Dispose();
        _reconnectionTask?.Dispose();
        _heartbeatTask?.Dispose();

        OnConnectionStateChanged = null;
        OnReconnectionAttempt = null;
        OnReconnected = null;
        OnReconnectionFailed = null;
        OnHeartbeatStatusChanged = null;

        if (_config != null && _config.EnableLogging)
        {
            Debug.Log("[ConnectionManager] Disposed");
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// 연결 상태
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
#endregion

#region Unity Main Thread Dispatcher
/// <summary>
/// Unity 메인 스레드에서 액션 실행을 위한 디스패처
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

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
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue()?.Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;
        
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
#endregion