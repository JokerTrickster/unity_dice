using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 테스트용 Mock WebSocket 서버
/// 실제 WebSocket 연결 없이 다양한 네트워크 시나리오를 시뮬레이션합니다.
/// </summary>
public class MockWebSocketServer : MonoBehaviour
{
    #region Server State
    
    public enum MockServerState
    {
        Disconnected,
        Connecting,
        Connected,
        Unstable,
        Failed
    }
    
    [SerializeField]
    private MockServerState _currentState = MockServerState.Disconnected;
    
    [SerializeField]
    private float _baseLatency = 0.1f;
    
    [SerializeField]
    private float _latencyVariation = 0.05f;
    
    [SerializeField]
    private bool _simulateNetworkInstability = false;
    
    [SerializeField]
    private float _instabilityChance = 0.1f;
    
    [SerializeField]
    private float _timeoutDuration = 30f;
    
    private Dictionary<string, Func<string, object>> _messageHandlers;
    private Queue<MockMessage> _messageQueue;
    private Coroutine _connectionCoroutine;
    private bool _isProcessingMessages = false;
    
    public MockServerState CurrentState => _currentState;
    public bool IsConnected => _currentState == MockServerState.Connected;
    public float BaseLatency => _baseLatency;
    
    #endregion
    
    #region Events
    
    public static event Action<MockServerState> OnStateChanged;
    public static event Action<string> OnMessageReceived;
    public static event Action<string> OnMessageSent;
    public static event Action OnConnectionTimeout;
    public static event Action<string> OnError;
    
    #endregion
    
    #region Initialization
    
    private void Awake()
    {
        InitializeMessageHandlers();
        _messageQueue = new Queue<MockMessage>();
    }
    
    private void OnDestroy()
    {
        if (_connectionCoroutine != null)
        {
            StopCoroutine(_connectionCoroutine);
        }
        
        ClearAllEvents();
    }
    
    private void InitializeMessageHandlers()
    {
        _messageHandlers = new Dictionary<string, Func<string, object>>
        {
            { "matching_request", HandleMatchingRequest },
            { "room_create", HandleRoomCreate },
            { "room_join", HandleRoomJoin },
            { "room_leave", HandleRoomLeave },
            { "game_start", HandleGameStart },
            { "energy_purchase", HandleEnergyPurchase },
            { "profile_update", HandleProfileUpdate },
            { "ping", HandlePing }
        };
    }
    
    private void ClearAllEvents()
    {
        OnStateChanged = null;
        OnMessageReceived = null;
        OnMessageSent = null;
        OnConnectionTimeout = null;
        OnError = null;
    }
    
    #endregion
    
    #region Connection Management
    
    /// <summary>
    /// Mock 서버 연결을 시뮬레이션합니다.
    /// </summary>
    /// <param name="connectionDelay">연결 지연 시간</param>
    /// <param name="shouldSucceed">연결 성공 여부</param>
    /// <returns>연결 시뮬레이션 코루틴</returns>
    public IEnumerator SimulateConnection(float connectionDelay = 1f, bool shouldSucceed = true)
    {
        ChangeState(MockServerState.Connecting);
        
        yield return new WaitForSeconds(connectionDelay);
        
        if (shouldSucceed && !_simulateNetworkInstability)
        {
            ChangeState(MockServerState.Connected);
            StartMessageProcessing();
        }
        else
        {
            ChangeState(MockServerState.Failed);
            OnError?.Invoke("Connection failed");
        }
    }
    
    /// <summary>
    /// Mock 서버 연결 해제를 시뮬레이션합니다.
    /// </summary>
    /// <param name="disconnectionDelay">연결 해제 지연 시간</param>
    /// <returns>연결 해제 시뮬레이션 코루틴</returns>
    public IEnumerator SimulateDisconnection(float disconnectionDelay = 0.1f)
    {
        yield return new WaitForSeconds(disconnectionDelay);
        
        ChangeState(MockServerState.Disconnected);
        StopMessageProcessing();
    }
    
    /// <summary>
    /// 네트워크 불안정 상황을 시뮬레이션합니다.
    /// </summary>
    /// <param name="enable">불안정 상황 활성화 여부</param>
    /// <param name="instabilityChance">불안정 발생 확률 (0-1)</param>
    public void SimulateNetworkInstability(bool enable, float instabilityChance = 0.1f)
    {
        _simulateNetworkInstability = enable;
        _instabilityChance = Mathf.Clamp01(instabilityChance);
        
        if (enable && _currentState == MockServerState.Connected)
        {
            ChangeState(MockServerState.Unstable);
        }
        else if (!enable && _currentState == MockServerState.Unstable)
        {
            ChangeState(MockServerState.Connected);
        }
    }
    
    /// <summary>
    /// 서버 타임아웃을 강제로 발생시킵니다.
    /// </summary>
    /// <param name="enable">타임아웃 시뮬레이션 활성화</param>
    public void SimulateServerTimeout(bool enable)
    {
        if (enable)
        {
            StartCoroutine(TriggerTimeoutAfterDelay(_timeoutDuration));
        }
    }
    
    private IEnumerator TriggerTimeoutAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        OnConnectionTimeout?.Invoke();
        ChangeState(MockServerState.Failed);
    }
    
    private void ChangeState(MockServerState newState)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
    
    #endregion
    
    #region Message Handling
    
    /// <summary>
    /// 메시지 처리를 시작합니다.
    /// </summary>
    private void StartMessageProcessing()
    {
        if (!_isProcessingMessages)
        {
            _isProcessingMessages = true;
            StartCoroutine(ProcessMessageQueue());
        }
    }
    
    /// <summary>
    /// 메시지 처리를 중단합니다.
    /// </summary>
    private void StopMessageProcessing()
    {
        _isProcessingMessages = false;
        _messageQueue.Clear();
    }
    
    /// <summary>
    /// 메시지 큐를 처리하는 코루틴입니다.
    /// </summary>
    /// <returns>메시지 처리 코루틴</returns>
    private IEnumerator ProcessMessageQueue()
    {
        while (_isProcessingMessages)
        {
            if (_messageQueue.Count > 0 && IsConnected)
            {
                MockMessage message = _messageQueue.Dequeue();
                yield return ProcessMessage(message);
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// 개별 메시지를 처리합니다.
    /// </summary>
    /// <param name="message">처리할 메시지</param>
    /// <returns>메시지 처리 코루틴</returns>
    private IEnumerator ProcessMessage(MockMessage message)
    {
        // 네트워크 지연 시뮬레이션
        float actualLatency = CalculateActualLatency();
        yield return new WaitForSeconds(actualLatency);
        
        // 불안정 상황에서 메시지 손실 시뮬레이션
        if (ShouldSimulateMessageLoss())
        {
            OnError?.Invoke($"Message lost due to network instability: {message.Type}");
            yield break;
        }
        
        // 메시지 처리
        try
        {
            if (_messageHandlers.ContainsKey(message.Type))
            {
                object response = _messageHandlers[message.Type](message.Data);
                string responseJson = JsonConvert.SerializeObject(response);
                OnMessageReceived?.Invoke(responseJson);
            }
            else
            {
                OnError?.Invoke($"Unknown message type: {message.Type}");
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error processing message: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 실제 지연 시간을 계산합니다.
    /// </summary>
    /// <returns>계산된 지연 시간</returns>
    private float CalculateActualLatency()
    {
        float variation = UnityEngine.Random.Range(-_latencyVariation, _latencyVariation);
        float latency = _baseLatency + variation;
        
        // 불안정 상황에서 추가 지연
        if (_currentState == MockServerState.Unstable)
        {
            latency *= UnityEngine.Random.Range(2f, 5f);
        }
        
        return Mathf.Max(0f, latency);
    }
    
    /// <summary>
    /// 메시지 손실 시뮬레이션 여부를 결정합니다.
    /// </summary>
    /// <returns>메시지 손실 여부</returns>
    private bool ShouldSimulateMessageLoss()
    {
        return _simulateNetworkInstability && UnityEngine.Random.Range(0f, 1f) < _instabilityChance;
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Mock 서버에 메시지를 전송합니다.
    /// </summary>
    /// <param name="messageType">메시지 타입</param>
    /// <param name="data">메시지 데이터</param>
    public void SendMessage(string messageType, string data)
    {
        if (!IsConnected)
        {
            OnError?.Invoke("Cannot send message: Server not connected");
            return;
        }
        
        MockMessage message = new MockMessage(messageType, data);
        _messageQueue.Enqueue(message);
        
        OnMessageSent?.Invoke($"{{\"type\":\"{messageType}\",\"data\":{data}}}");
    }
    
    /// <summary>
    /// 네트워크 설정을 구성합니다.
    /// </summary>
    /// <param name="baseLatency">기본 지연 시간</param>
    /// <param name="latencyVariation">지연 시간 변동</param>
    /// <param name="timeoutDuration">타임아웃 시간</param>
    public void ConfigureNetwork(float baseLatency, float latencyVariation, float timeoutDuration)
    {
        _baseLatency = Mathf.Max(0f, baseLatency);
        _latencyVariation = Mathf.Max(0f, latencyVariation);
        _timeoutDuration = Mathf.Max(1f, timeoutDuration);
    }
    
    /// <summary>
    /// Mock 서버를 재시작합니다.
    /// </summary>
    /// <returns>재시작 코루틴</returns>
    public IEnumerator RestartServer()
    {
        yield return SimulateDisconnection(0.1f);
        yield return new WaitForSeconds(0.5f);
        yield return SimulateConnection(1f, true);
    }
    
    #endregion
    
    #region Message Handlers
    
    private object HandleMatchingRequest(string data)
    {
        var request = JsonConvert.DeserializeObject<MatchingRequest>(data);
        
        return new MatchingResponse
        {
            success = true,
            roomId = $"room_{UnityEngine.Random.Range(1000, 9999)}",
            playerCount = request.playerCount,
            estimatedWaitTime = UnityEngine.Random.Range(10, 120)
        };
    }
    
    private object HandleRoomCreate(string data)
    {
        var request = JsonConvert.DeserializeObject<RoomCreateRequest>(data);
        
        return new RoomCreateResponse
        {
            success = true,
            roomId = $"room_{UnityEngine.Random.Range(1000, 9999)}",
            roomCode = UnityEngine.Random.Range(1000, 9999).ToString(),
            maxPlayers = request.maxPlayers
        };
    }
    
    private object HandleRoomJoin(string data)
    {
        var request = JsonConvert.DeserializeObject<RoomJoinRequest>(data);
        
        return new RoomJoinResponse
        {
            success = true,
            roomId = request.roomCode,
            playerCount = UnityEngine.Random.Range(1, 4),
            players = new List<string> { "TestPlayer1", "TestPlayer2" }
        };
    }
    
    private object HandleRoomLeave(string data)
    {
        return new { success = true };
    }
    
    private object HandleGameStart(string data)
    {
        return new GameStartResponse
        {
            success = true,
            gameId = $"game_{UnityEngine.Random.Range(10000, 99999)}",
            startTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }
    
    private object HandleEnergyPurchase(string data)
    {
        var request = JsonConvert.DeserializeObject<EnergyPurchaseRequest>(data);
        
        return new EnergyPurchaseResponse
        {
            success = true,
            newEnergyAmount = request.amount + UnityEngine.Random.Range(80, 100),
            transactionId = $"txn_{UnityEngine.Random.Range(100000, 999999)}"
        };
    }
    
    private object HandleProfileUpdate(string data)
    {
        return new { success = true, updated = DateTime.UtcNow };
    }
    
    private object HandlePing(string data)
    {
        return new { pong = DateTime.UtcNow.Ticks };
    }
    
    #endregion
    
    #region Data Structures
    
    private class MockMessage
    {
        public string Type { get; }
        public string Data { get; }
        public DateTime Timestamp { get; }
        
        public MockMessage(string type, string data)
        {
            Type = type;
            Data = data;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    [Serializable]
    public class MatchingRequest
    {
        public int playerCount;
        public string gameMode;
    }
    
    [Serializable]
    public class MatchingResponse
    {
        public bool success;
        public string roomId;
        public int playerCount;
        public int estimatedWaitTime;
        public string error;
    }
    
    [Serializable]
    public class RoomCreateRequest
    {
        public int maxPlayers;
        public string gameMode;
        public bool isPrivate;
    }
    
    [Serializable]
    public class RoomCreateResponse
    {
        public bool success;
        public string roomId;
        public string roomCode;
        public int maxPlayers;
        public string error;
    }
    
    [Serializable]
    public class RoomJoinRequest
    {
        public string roomCode;
        public string playerId;
    }
    
    [Serializable]
    public class RoomJoinResponse
    {
        public bool success;
        public string roomId;
        public int playerCount;
        public List<string> players;
        public string error;
    }
    
    [Serializable]
    public class GameStartResponse
    {
        public bool success;
        public string gameId;
        public string startTime;
        public string error;
    }
    
    [Serializable]
    public class EnergyPurchaseRequest
    {
        public int amount;
        public string paymentMethod;
        public string currency;
    }
    
    [Serializable]
    public class EnergyPurchaseResponse
    {
        public bool success;
        public int newEnergyAmount;
        public string transactionId;
        public string error;
    }
    
    #endregion
}