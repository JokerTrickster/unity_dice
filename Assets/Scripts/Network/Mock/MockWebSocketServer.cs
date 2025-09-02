using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Mock WebSocket 서버 - 개발 및 테스트용
/// 실제 서버 없이 WebSocket 클라이언트 기능을 테스트하고 개발할 수 있는 환경 제공
/// </summary>
public class MockWebSocketServer : IDisposable
{
    #region Events
    /// <summary>클라이언트 연결 이벤트</summary>
    public event Action<string> OnClientConnected;
    
    /// <summary>클라이언트 연결 해제 이벤트</summary>
    public event Action<string> OnClientDisconnected;
    
    /// <summary>메시지 수신 이벤트</summary>
    public event Action<string, string> OnMessageReceived; // clientId, message
    
    /// <summary>서버 상태 변경 이벤트</summary>
    public event Action<MockServerState> OnServerStateChanged;
    
    /// <summary>에러 발생 이벤트</summary>
    public event Action<string> OnError;
    #endregion

    #region Private Fields
    private readonly object _lockObject = new();
    private readonly ConcurrentDictionary<string, MockWebSocketClient> _connectedClients = new();
    private readonly ConcurrentQueue<MockMessage> _messageQueue = new();
    private readonly MockServerConfig _config;
    
    private volatile MockServerState _serverState = MockServerState.Stopped;
    private volatile bool _isDisposed = false;
    
    private CancellationTokenSource _cancellationTokenSource;
    private Task _messageProcessingTask;
    private Task _simulationTask;
    
    private readonly Random _random = new();
    private readonly Dictionary<string, object> _gameRooms = new(); // roomId -> room data
    private readonly Dictionary<string, List<string>> _matchingQueues = new(); // gameMode -> player list
    #endregion

    #region Properties
    /// <summary>서버 상태</summary>
    public MockServerState State => _serverState;
    
    /// <summary>실행 중인지</summary>
    public bool IsRunning => _serverState == MockServerState.Running;
    
    /// <summary>연결된 클라이언트 수</summary>
    public int ConnectedClientCount => _connectedClients.Count;
    
    /// <summary>서버 설정</summary>
    public MockServerConfig Config => _config;
    
    /// <summary>통계 정보</summary>
    public MockServerStats Stats { get; private set; } = new();
    #endregion

    #region Constructor
    /// <summary>
    /// Mock WebSocket 서버 초기화
    /// </summary>
    /// <param name="config">서버 설정</param>
    public MockWebSocketServer(MockServerConfig config = null)
    {
        _config = config ?? MockServerConfig.Default();
        
        if (_config.EnableLogging)
        {
            Debug.Log("[MockWebSocketServer] Initialized");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 서버 시작
    /// </summary>
    public async Task<bool> StartAsync()
    {
        if (_isDisposed)
        {
            Debug.LogError("[MockWebSocketServer] Cannot start: Server is disposed");
            return false;
        }

        lock (_lockObject)
        {
            if (_serverState == MockServerState.Running)
            {
                if (_config.EnableLogging)
                {
                    Debug.LogWarning("[MockWebSocketServer] Server is already running");
                }
                return true;
            }

            if (_serverState == MockServerState.Starting)
            {
                if (_config.EnableLogging)
                {
                    Debug.LogWarning("[MockWebSocketServer] Server is already starting");
                }
                return false;
            }

            SetServerState(MockServerState.Starting);
        }

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start message processing
            _messageProcessingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);
            
            // Start simulation task if enabled
            if (_config.EnableRealisticSimulation)
            {
                _simulationTask = RunSimulationAsync(_cancellationTokenSource.Token);
            }
            
            SetServerState(MockServerState.Running);
            
            if (_config.EnableLogging)
            {
                Debug.Log($"[MockWebSocketServer] Started on port {_config.Port}");
            }
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Failed to start: {e.Message}");
            SetServerState(MockServerState.Error);
            return false;
        }
    }

    /// <summary>
    /// 서버 중지
    /// </summary>
    public async Task StopAsync()
    {
        if (_isDisposed)
            return;

        lock (_lockObject)
        {
            if (_serverState == MockServerState.Stopped || _serverState == MockServerState.Stopping)
                return;

            SetServerState(MockServerState.Stopping);
        }

        try
        {
            // Cancel all operations
            _cancellationTokenSource?.Cancel();
            
            // Disconnect all clients
            var clients = new List<MockWebSocketClient>(_connectedClients.Values);
            foreach (var client in clients)
            {
                await DisconnectClientAsync(client.ClientId, "Server stopping");
            }
            
            // Wait for tasks to complete
            if (_messageProcessingTask != null)
            {
                await _messageProcessingTask;
            }
            
            if (_simulationTask != null)
            {
                await _simulationTask;
            }
            
            SetServerState(MockServerState.Stopped);
            
            if (_config.EnableLogging)
            {
                Debug.Log("[MockWebSocketServer] Stopped");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Error during stop: {e.Message}");
            SetServerState(MockServerState.Error);
        }
    }

    /// <summary>
    /// 클라이언트 연결 시뮬레이션
    /// </summary>
    /// <param name="clientId">클라이언트 ID</param>
    /// <returns>연결 성공 여부</returns>
    public async Task<bool> SimulateClientConnectAsync(string clientId)
    {
        if (_isDisposed || !IsRunning)
        {
            Debug.LogError("[MockWebSocketServer] Cannot connect client: Server not running");
            return false;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("[MockWebSocketServer] Cannot connect: Invalid client ID");
            return false;
        }

        if (_connectedClients.ContainsKey(clientId))
        {
            if (_config.EnableLogging)
            {
                Debug.LogWarning($"[MockWebSocketServer] Client {clientId} is already connected");
            }
            return true;
        }

        try
        {
            // Simulate connection delay
            if (_config.ConnectionDelay > 0)
            {
                await Task.Delay(_config.ConnectionDelay);
            }

            // Simulate connection failure rate
            if (_config.ConnectionFailureRate > 0 && _random.NextDouble() < _config.ConnectionFailureRate)
            {
                if (_config.EnableLogging)
                {
                    Debug.Log($"[MockWebSocketServer] Simulated connection failure for {clientId}");
                }
                return false;
            }

            var client = new MockWebSocketClient
            {
                ClientId = clientId,
                ConnectedAt = DateTime.UtcNow,
                IsConnected = true,
                LastActivity = DateTime.UtcNow
            };

            _connectedClients[clientId] = client;
            Stats.TotalConnections++;
            Stats.CurrentConnections = _connectedClients.Count;

            OnClientConnected?.Invoke(clientId);

            if (_config.EnableLogging)
            {
                Debug.Log($"[MockWebSocketServer] Client {clientId} connected");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Error connecting client {clientId}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 클라이언트 연결 해제 시뮬레이션
    /// </summary>
    /// <param name="clientId">클라이언트 ID</param>
    /// <param name="reason">연결 해제 사유</param>
    public async Task DisconnectClientAsync(string clientId, string reason = "Client disconnected")
    {
        if (!_connectedClients.TryRemove(clientId, out var client))
        {
            return;
        }

        client.IsConnected = false;
        Stats.CurrentConnections = _connectedClients.Count;
        Stats.TotalDisconnections++;

        OnClientDisconnected?.Invoke(clientId);

        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Client {clientId} disconnected: {reason}");
        }

        // Remove from matching queues
        RemovePlayerFromAllQueues(clientId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 클라이언트에게 메시지 전송 시뮬레이션
    /// </summary>
    /// <param name="clientId">클라이언트 ID</param>
    /// <param name="message">메시지</param>
    public async Task<bool> SendToClientAsync(string clientId, string message)
    {
        if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsConnected)
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.LogWarning($"[MockWebSocketServer] Cannot send to {clientId}: Client not connected");
            }
            return false;
        }

        try
        {
            // Simulate message delay
            if (_config.MessageDelay > 0)
            {
                await Task.Delay(_random.Next(0, _config.MessageDelay));
            }

            // Simulate message failure rate
            if (_config.MessageFailureRate > 0 && _random.NextDouble() < _config.MessageFailureRate)
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[MockWebSocketServer] Simulated message failure to {clientId}");
                }
                return false;
            }

            client.LastActivity = DateTime.UtcNow;
            Stats.MessagesSent++;

            // In a real implementation, this would send via WebSocket
            // Here we just log or trigger events for testing
            if (_config.EnableDetailedLogging)
            {
                string shortMessage = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
                Debug.Log($"[MockWebSocketServer] Sent to {clientId}: {shortMessage}");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Error sending to {clientId}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 모든 클라이언트에게 브로드캐스트
    /// </summary>
    /// <param name="message">메시지</param>
    public async Task<int> BroadcastAsync(string message)
    {
        int successCount = 0;
        var clients = new List<string>(_connectedClients.Keys);

        foreach (var clientId in clients)
        {
            if (await SendToClientAsync(clientId, message))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// 클라이언트로부터 메시지 수신 시뮬레이션
    /// </summary>
    /// <param name="clientId">클라이언트 ID</param>
    /// <param name="message">메시지</param>
    public void SimulateMessageReceived(string clientId, string message)
    {
        if (!_connectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogWarning($"[MockWebSocketServer] Message from unknown client: {clientId}");
            return;
        }

        client.LastActivity = DateTime.UtcNow;
        Stats.MessagesReceived++;

        var mockMessage = new MockMessage
        {
            ClientId = clientId,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        _messageQueue.Enqueue(mockMessage);
        OnMessageReceived?.Invoke(clientId, message);

        if (_config.EnableDetailedLogging)
        {
            string shortMessage = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
            Debug.Log($"[MockWebSocketServer] Received from {clientId}: {shortMessage}");
        }
    }

    /// <summary>
    /// 서버 통계 리셋
    /// </summary>
    public void ResetStats()
    {
        Stats = new MockServerStats
        {
            CurrentConnections = _connectedClients.Count
        };
    }

    /// <summary>
    /// 연결된 클라이언트 목록 반환
    /// </summary>
    public List<string> GetConnectedClients()
    {
        return new List<string>(_connectedClients.Keys);
    }

    /// <summary>
    /// 특정 클라이언트 정보 반환
    /// </summary>
    public MockWebSocketClient GetClientInfo(string clientId)
    {
        return _connectedClients.TryGetValue(clientId, out var client) ? client : null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 서버 상태 설정
    /// </summary>
    private void SetServerState(MockServerState newState)
    {
        if (_serverState == newState)
            return;

        var oldState = _serverState;
        _serverState = newState;

        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] State changed: {oldState} -> {newState}");
        }

        OnServerStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// 메시지 처리 루프
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_messageQueue.TryDequeue(out var mockMessage))
                {
                    await ProcessMessage(mockMessage);
                }

                await Task.Delay(10, cancellationToken); // Small delay to prevent busy waiting
            }
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[MockWebSocketServer] Message processing cancelled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Message processing error: {e.Message}");
            OnError?.Invoke($"Message processing error: {e.Message}");
        }
    }

    /// <summary>
    /// 개별 메시지 처리
    /// </summary>
    private async Task ProcessMessage(MockMessage mockMessage)
    {
        try
        {
            var message = MatchingProtocol.DeserializeMessage(mockMessage.Message);
            if (message == null)
            {
                await SendToClientAsync(mockMessage.ClientId, 
                    MatchingProtocol.CreateProtocolErrorMessage("INVALID_MESSAGE", "Failed to parse message"));
                return;
            }

            switch (message.type.ToLower())
            {
                case "join_queue":
                    await HandleJoinQueue(mockMessage.ClientId, message);
                    break;
                    
                case "leave_queue":
                    await HandleLeaveQueue(mockMessage.ClientId, message);
                    break;
                    
                case "room_create":
                    await HandleRoomCreate(mockMessage.ClientId, message);
                    break;
                    
                case "room_join":
                    await HandleRoomJoin(mockMessage.ClientId, message);
                    break;
                    
                case "room_leave":
                    await HandleRoomLeave(mockMessage.ClientId, message);
                    break;
                    
                case "matching_cancel":
                    await HandleMatchingCancel(mockMessage.ClientId, message);
                    break;
                    
                case "heartbeat":
                    await HandleHeartbeat(mockMessage.ClientId, message);
                    break;
                    
                case "pong":
                    // Handle pong response - just update activity
                    if (_connectedClients.TryGetValue(mockMessage.ClientId, out var client))
                    {
                        client.LastActivity = DateTime.UtcNow;
                    }
                    break;
                    
                default:
                    await SendToClientAsync(mockMessage.ClientId,
                        MatchingProtocol.CreateInvalidMessageTypeError(message.type));
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Error processing message from {mockMessage.ClientId}: {e.Message}");
            await SendToClientAsync(mockMessage.ClientId,
                MatchingProtocol.CreateProtocolErrorMessage("PROCESSING_ERROR", e.Message));
        }
    }

    /// <summary>
    /// 매칭 대기열 참가 처리
    /// </summary>
    private async Task HandleJoinQueue(string clientId, MatchingMessage message)
    {
        var request = message.GetPayload<MatchingRequest>();
        if (request == null || !request.IsValid())
        {
            await SendToClientAsync(clientId, 
                MatchingProtocol.CreateProtocolErrorMessage("INVALID_REQUEST", "Invalid matching request"));
            return;
        }

        var gameMode = request.gameMode ?? "classic";
        
        if (!_matchingQueues.ContainsKey(gameMode))
        {
            _matchingQueues[gameMode] = new List<string>();
        }

        var queue = _matchingQueues[gameMode];
        
        // Remove from other queues first
        RemovePlayerFromAllQueues(clientId);
        
        // Add to requested queue
        queue.Add(clientId);
        
        // Send queue status
        var queueStatus = MatchingResponse.QueueStatus(queue.Count, request.playerCount);
        string statusJson = MatchingProtocol.SerializeResponse(queueStatus);
        await SendToClientAsync(clientId, statusJson);

        // Check if we can form a match
        if (queue.Count >= request.playerCount)
        {
            await CreateMatch(gameMode, request.playerCount, request.betAmount);
        }

        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Player {clientId} joined {gameMode} queue ({queue.Count} players)");
        }
    }

    /// <summary>
    /// 매칭 대기열 나가기 처리
    /// </summary>
    private async Task HandleLeaveQueue(string clientId, MatchingMessage message)
    {
        RemovePlayerFromAllQueues(clientId);
        
        var response = MatchingResponse.Success("", new PlayerInfo[0]);
        response.message = "Left matching queue";
        string responseJson = MatchingProtocol.SerializeResponse(response);
        await SendToClientAsync(clientId, responseJson);
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Player {clientId} left matching queue");
        }
    }

    /// <summary>
    /// 방 생성 처리
    /// </summary>
    private async Task HandleRoomCreate(string clientId, MatchingMessage message)
    {
        var request = message.GetPayload<MatchingRequest>();
        if (request == null || !request.IsValid())
        {
            await SendToClientAsync(clientId, 
                MatchingProtocol.CreateProtocolErrorMessage("INVALID_REQUEST", "Invalid room creation request"));
            return;
        }

        string roomId = GenerateRoomId();
        var roomData = new
        {
            roomId = roomId,
            hostId = clientId,
            maxPlayers = request.playerCount,
            gameMode = request.gameMode,
            betAmount = request.betAmount,
            players = new List<PlayerInfo> { new PlayerInfo(clientId, $"Player{clientId}", 1200) },
            isPrivate = request.isPrivate,
            createdAt = DateTime.UtcNow
        };
        
        _gameRooms[roomId] = roomData;
        
        var response = MatchingResponse.RoomCreated(roomId, new[] { new PlayerInfo(clientId, $"Player{clientId}", 1200) });
        string responseJson = MatchingProtocol.SerializeResponse(response);
        await SendToClientAsync(clientId, responseJson);
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Room {roomId} created by {clientId}");
        }
    }

    /// <summary>
    /// 방 참가 처리
    /// </summary>
    private async Task HandleRoomJoin(string clientId, MatchingMessage message)
    {
        var request = message.GetPayload<MatchingRequest>();
        if (request == null || string.IsNullOrEmpty(request.roomCode))
        {
            await SendToClientAsync(clientId, 
                MatchingProtocol.CreateProtocolErrorMessage("INVALID_REQUEST", "Invalid room join request"));
            return;
        }

        if (!_gameRooms.TryGetValue(request.roomCode, out var roomObj))
        {
            var errorResponse = MatchingResponse.Error("Room not found");
            string errorJson = MatchingProtocol.SerializeResponse(errorResponse);
            await SendToClientAsync(clientId, errorJson);
            return;
        }

        // In a real implementation, you'd properly manage room data structure
        // For this mock, we'll simulate successful join
        var players = new[]
        {
            new PlayerInfo(clientId, $"Player{clientId}", 1200),
            new PlayerInfo("host", "PlayerHost", 1300)
        };
        
        var response = MatchingResponse.RoomJoined(request.roomCode, players);
        string responseJson = MatchingProtocol.SerializeResponse(response);
        await SendToClientAsync(clientId, responseJson);
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Player {clientId} joined room {request.roomCode}");
        }
    }

    /// <summary>
    /// 방 나가기 처리
    /// </summary>
    private async Task HandleRoomLeave(string clientId, MatchingMessage message)
    {
        // Simple implementation - just confirm leave
        var response = MatchingResponse.Success("", new PlayerInfo[0]);
        response.message = "Left room";
        string responseJson = MatchingProtocol.SerializeResponse(response);
        await SendToClientAsync(clientId, responseJson);
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Player {clientId} left room");
        }
    }

    /// <summary>
    /// 매칭 취소 처리
    /// </summary>
    private async Task HandleMatchingCancel(string clientId, MatchingMessage message)
    {
        RemovePlayerFromAllQueues(clientId);
        
        var response = MatchingResponse.Success("", new PlayerInfo[0]);
        response.message = "Matching cancelled";
        string responseJson = MatchingProtocol.SerializeResponse(response);
        await SendToClientAsync(clientId, responseJson);
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Matching cancelled for {clientId}");
        }
    }

    /// <summary>
    /// 하트비트 처리
    /// </summary>
    private async Task HandleHeartbeat(string clientId, MatchingMessage message)
    {
        string pongResponse = MatchingProtocol.CreatePongMessage(clientId);
        await SendToClientAsync(clientId, pongResponse);
        
        if (_config.EnableDetailedLogging)
        {
            Debug.Log($"[MockWebSocketServer] Heartbeat from {clientId}, sent pong");
        }
    }

    /// <summary>
    /// 매치 생성 시뮬레이션
    /// </summary>
    private async Task CreateMatch(string gameMode, int playerCount, int betAmount)
    {
        if (!_matchingQueues.TryGetValue(gameMode, out var queue) || queue.Count < playerCount)
            return;

        // Take required number of players from queue
        var matchPlayers = queue.Take(playerCount).ToArray();
        for (int i = 0; i < playerCount; i++)
        {
            queue.RemoveAt(0);
        }

        string roomId = GenerateRoomId();
        var players = matchPlayers.Select((playerId, index) => 
            new PlayerInfo(playerId, $"Player{index + 1}", 1000 + _random.Next(500))).ToArray();

        var matchResponse = MatchingResponse.MatchFound(roomId, players);
        string responseJson = MatchingProtocol.SerializeResponse(matchResponse);

        // Send match found to all players
        foreach (var playerId in matchPlayers)
        {
            await SendToClientAsync(playerId, responseJson);
        }

        if (_config.EnableLogging)
        {
            Debug.Log($"[MockWebSocketServer] Match created: {roomId} with {playerCount} players in {gameMode}");
        }
    }

    /// <summary>
    /// 플레이어를 모든 대기열에서 제거
    /// </summary>
    private void RemovePlayerFromAllQueues(string playerId)
    {
        foreach (var queue in _matchingQueues.Values)
        {
            queue.RemoveAll(id => id == playerId);
        }
    }

    /// <summary>
    /// 방 ID 생성
    /// </summary>
    private string GenerateRoomId()
    {
        return $"ROOM{_random.Next(1000, 9999)}";
    }

    /// <summary>
    /// 시뮬레이션 루프 (연결 끊김, 지연 등)
    /// </summary>
    private async Task RunSimulationAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken); // Check every 5 seconds
                
                // Simulate random disconnections
                if (_config.RandomDisconnectionRate > 0)
                {
                    var clients = new List<string>(_connectedClients.Keys);
                    foreach (var clientId in clients)
                    {
                        if (_random.NextDouble() < _config.RandomDisconnectionRate)
                        {
                            await DisconnectClientAsync(clientId, "Simulated random disconnection");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[MockWebSocketServer] Simulation cancelled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MockWebSocketServer] Simulation error: {e.Message}");
        }
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

        try
        {
            StopAsync().Wait(5000); // Wait up to 5 seconds for graceful shutdown
        }
        catch
        {
            // Ignore disposal errors
        }

        _cancellationTokenSource?.Dispose();

        OnClientConnected = null;
        OnClientDisconnected = null;
        OnMessageReceived = null;
        OnServerStateChanged = null;
        OnError = null;

        if (_config != null && _config.EnableLogging)
        {
            Debug.Log("[MockWebSocketServer] Disposed");
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// Mock 서버 상태
/// </summary>
public enum MockServerState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Mock 웹소켓 클라이언트 정보
/// </summary>
public class MockWebSocketClient
{
    public string ClientId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsConnected { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Mock 메시지
/// </summary>
public class MockMessage
{
    public string ClientId { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Mock 서버 설정
/// </summary>
[System.Serializable]
public class MockServerConfig
{
    [Header("Server Settings")]
    public int Port = 8080;
    public bool EnableLogging = true;
    public bool EnableDetailedLogging = false;
    
    [Header("Simulation Settings")]
    public bool EnableRealisticSimulation = true;
    public int ConnectionDelay = 100; // ms
    public int MessageDelay = 50; // ms
    
    [Header("Failure Simulation")]
    [Range(0f, 1f)]
    public float ConnectionFailureRate = 0.05f; // 5% failure rate
    [Range(0f, 1f)]
    public float MessageFailureRate = 0.02f; // 2% failure rate
    [Range(0f, 0.1f)]
    public float RandomDisconnectionRate = 0.001f; // 0.1% per check
    
    [Header("Performance")]
    public int MaxConnectedClients = 1000;
    public int MessageQueueSize = 10000;

    public static MockServerConfig Default()
    {
        return new MockServerConfig();
    }

    public bool ValidateConfiguration()
    {
        return Port > 0 && Port < 65536 && 
               ConnectionFailureRate >= 0 && ConnectionFailureRate <= 1 &&
               MessageFailureRate >= 0 && MessageFailureRate <= 1 &&
               RandomDisconnectionRate >= 0 && RandomDisconnectionRate <= 1 &&
               MaxConnectedClients > 0 &&
               MessageQueueSize > 0;
    }
}

/// <summary>
/// Mock 서버 통계
/// </summary>
public class MockServerStats
{
    public int TotalConnections { get; set; }
    public int TotalDisconnections { get; set; }
    public int CurrentConnections { get; set; }
    public int MessagesReceived { get; set; }
    public int MessagesSent { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;
    public double MessagesPerSecond => Uptime.TotalSeconds > 0 ? (MessagesReceived + MessagesSent) / Uptime.TotalSeconds : 0;
    
    public override string ToString()
    {
        return $"Connections: {CurrentConnections}/{TotalConnections}, " +
               $"Messages: {MessagesReceived} in/{MessagesSent} out, " +
               $"Rate: {MessagesPerSecond:F2} msg/s, " +
               $"Uptime: {Uptime:hh\\:mm\\:ss}";
    }
}
#endregion