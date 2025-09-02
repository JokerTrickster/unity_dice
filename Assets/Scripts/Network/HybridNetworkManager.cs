using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 하이브리드 네트워크 매니저
/// NetworkManager의 HTTP 기능과 WebSocket 기능을 통합 관리
/// 기존 HTTP 기능은 완전히 보존하면서 WebSocket 기능을 추가
/// </summary>
public class HybridNetworkManager : MonoBehaviour
{
    #region Events
    /// <summary>WebSocket 메시지 수신 이벤트</summary>
    public event Action<string> OnWebSocketMessage;
    
    /// <summary>WebSocket 연결 상태 변경 이벤트</summary>
    public event Action<bool> OnWebSocketConnectionChanged;
    
    /// <summary>WebSocket 에러 발생 이벤트</summary>
    public event Action<string> OnWebSocketError;
    
    /// <summary>WebSocket 연결 끊김 이벤트</summary>
    public event Action<System.Net.WebSockets.WebSocketCloseStatus?, string> OnWebSocketClosed;
    
    /// <summary>재연결 시도 이벤트</summary>
    public event Action<int, int> OnReconnectionAttempt;
    
    /// <summary>재연결 성공 이벤트</summary>
    public event Action OnReconnected;
    
    /// <summary>재연결 실패 이벤트</summary>
    public event Action<string> OnReconnectionFailed;
    #endregion

    #region Private Fields
    private NetworkManager _networkManager;
    private WebSocketClient _webSocketClient;
    private WebSocketConfig _webSocketConfig;
    private bool _isInitialized = false;
    private readonly Dictionary<string, Action<string>> _messageHandlers = new();
    private readonly Dictionary<string, DateTime> _lastMessageTimes = new();
    #endregion

    #region Properties
    /// <summary>WebSocket 초기화 여부</summary>
    public bool IsWebSocketInitialized => _isInitialized && _webSocketClient != null;
    
    /// <summary>WebSocket 연결 상태</summary>
    public bool IsWebSocketConnected => _webSocketClient?.IsConnected ?? false;
    
    /// <summary>WebSocket 연결 중 상태</summary>
    public bool IsWebSocketConnecting => _webSocketClient?.IsConnecting ?? false;
    
    /// <summary>현재 WebSocket 상태</summary>
    public System.Net.WebSockets.WebSocketState? WebSocketState => _webSocketClient?.State;
    
    /// <summary>연결된 NetworkManager 인스턴스</summary>
    public NetworkManager NetworkManager => _networkManager;
    
    /// <summary>현재 WebSocket 설정</summary>
    public WebSocketConfig Config => _webSocketConfig;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("[HybridNetworkManager] NetworkManager component not found");
            enabled = false;
            return;
        }
        
        Debug.Log("[HybridNetworkManager] Initialized");
    }

    private void OnDestroy()
    {
        CleanupWebSocket();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsWebSocketConnected)
        {
            // 앱이 백그라운드로 갈 때 WebSocket 연결 일시 중단
            Debug.Log("[HybridNetworkManager] App paused, maintaining WebSocket connection");
        }
        else if (!pauseStatus && !IsWebSocketConnected && _webSocketConfig != null)
        {
            // 앱이 포그라운드로 복귀할 때 재연결 시도
            Debug.Log("[HybridNetworkManager] App resumed, attempting WebSocket reconnection");
            _ = Task.Run(async () => await ConnectWebSocketAsync());
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !IsWebSocketConnected && _webSocketConfig != null)
        {
            // 포커스 복귀 시 연결 상태 확인 및 재연결
            Debug.Log("[HybridNetworkManager] App focused, checking WebSocket connection");
            _ = Task.Run(async () => await ConnectWebSocketAsync());
        }
    }
    #endregion

    #region WebSocket Initialization
    /// <summary>
    /// WebSocket 클라이언트 초기화
    /// </summary>
    /// <param name="config">WebSocket 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public async Task<bool> InitializeWebSocketAsync(WebSocketConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[HybridNetworkManager] WebSocket config is null");
            return false;
        }

        if (!config.ValidateConfiguration())
        {
            Debug.LogError("[HybridNetworkManager] Invalid WebSocket configuration");
            return false;
        }

        try
        {
            // 기존 연결 정리
            CleanupWebSocket();

            _webSocketConfig = config.Clone(); // 설정 복사본 생성
            _webSocketClient = new WebSocketClient(_webSocketConfig);

            // 이벤트 구독
            SetupWebSocketEvents();

            _isInitialized = true;

            if (_webSocketConfig.EnableLogging)
            {
                Debug.Log($"[HybridNetworkManager] WebSocket initialized: {_webSocketConfig.GetSummary()}");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridNetworkManager] WebSocket initialization failed: {e.Message}");
            CleanupWebSocket();
            return false;
        }
    }

    /// <summary>
    /// WebSocket 이벤트 설정
    /// </summary>
    private void SetupWebSocketEvents()
    {
        if (_webSocketClient == null) return;

        _webSocketClient.OnMessage += HandleWebSocketMessage;
        _webSocketClient.OnConnectionChanged += HandleConnectionChanged;
        _webSocketClient.OnError += HandleWebSocketError;
        _webSocketClient.OnClosed += HandleWebSocketClosed;

        // 연결 관리자 이벤트
        _webSocketClient.ConnectionManager.OnReconnectionAttempt += (attempt, maxAttempts) =>
        {
            OnReconnectionAttempt?.Invoke(attempt, maxAttempts);
        };

        _webSocketClient.ConnectionManager.OnReconnected += () =>
        {
            OnReconnected?.Invoke();
        };

        _webSocketClient.ConnectionManager.OnReconnectionFailed += (error) =>
        {
            OnReconnectionFailed?.Invoke(error);
        };
    }
    #endregion

    #region WebSocket Connection Management
    /// <summary>
    /// WebSocket 서버에 연결
    /// </summary>
    /// <param name="serverUrl">서버 URL (선택적)</param>
    /// <returns>연결 성공 여부</returns>
    public async Task<bool> ConnectWebSocketAsync(string serverUrl = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[HybridNetworkManager] WebSocket not initialized");
            return false;
        }

        // URL이 제공된 경우 설정 업데이트
        if (!string.IsNullOrEmpty(serverUrl))
        {
            _webSocketConfig.ServerUrl = serverUrl;
        }

        // NetworkManager의 인증 토큰 가져와서 WebSocket에 설정
        var authToken = GetNetworkManagerAuthToken();
        if (!string.IsNullOrEmpty(authToken))
        {
            _webSocketClient.SetAuthToken(authToken);
        }

        try
        {
            bool connected = await _webSocketClient.ConnectAsync();
            
            if (connected && _webSocketConfig.EnableLogging)
            {
                Debug.Log($"[HybridNetworkManager] Connected to WebSocket server: {_webSocketConfig.ServerUrl}");
            }
            
            return connected;
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridNetworkManager] WebSocket connection failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// WebSocket 연결 해제
    /// </summary>
    public async Task DisconnectWebSocketAsync()
    {
        if (_webSocketClient != null)
        {
            try
            {
                await _webSocketClient.DisconnectAsync();
                
                if (_webSocketConfig?.EnableLogging == true)
                {
                    Debug.Log("[HybridNetworkManager] WebSocket disconnected");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HybridNetworkManager] WebSocket disconnect error: {e.Message}");
            }
        }
    }
    #endregion

    #region Message Handling
    /// <summary>
    /// WebSocket 메시지 전송 (큐 사용)
    /// </summary>
    /// <param name="message">전송할 메시지</param>
    /// <param name="priority">메시지 우선순위</param>
    /// <returns>큐 추가 성공 여부</returns>
    public bool SendWebSocketMessage(string message, MessagePriority priority = MessagePriority.Normal)
    {
        if (_webSocketClient == null)
        {
            Debug.LogError("[HybridNetworkManager] WebSocket client not initialized");
            return false;
        }

        return _webSocketClient.SendMessage(message, priority);
    }

    /// <summary>
    /// 즉시 WebSocket 메시지 전송 (큐 우회)
    /// </summary>
    /// <param name="message">전송할 메시지</param>
    /// <returns>전송 성공 여부</returns>
    public async Task<bool> SendWebSocketMessageImmediateAsync(string message)
    {
        if (_webSocketClient == null)
        {
            Debug.LogError("[HybridNetworkManager] WebSocket client not initialized");
            return false;
        }

        return await _webSocketClient.SendMessageImmediateAsync(message);
    }

    /// <summary>
    /// WebSocket 메시지 수신 처리
    /// </summary>
    /// <param name="message">수신된 메시지</param>
    private void HandleWebSocketMessage(string message)
    {
        try
        {
            if (_webSocketConfig?.EnableDetailedLogging == true)
            {
                Debug.Log($"[HybridNetworkManager] Received WebSocket message: {message.Substring(0, Math.Min(100, message.Length))}...");
            }

            // 매칭 프로토콜 메시지 파싱 시도
            var matchingMessage = MatchingProtocol.DeserializeMessage(message);
            if (matchingMessage != null)
            {
                HandleMatchingMessage(matchingMessage);
            }

            // 등록된 핸들러 호출
            ProcessMessageHandlers(message);

            // 전역 이벤트 발생
            OnWebSocketMessage?.Invoke(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridNetworkManager] Error processing WebSocket message: {e.Message}");
            OnWebSocketError?.Invoke($"Message processing error: {e.Message}");
        }
    }

    /// <summary>
    /// 매칭 메시지 처리
    /// </summary>
    /// <param name="message">매칭 메시지</param>
    private void HandleMatchingMessage(MatchingMessage message)
    {
        if (message == null) return;

        _lastMessageTimes[message.type] = DateTime.UtcNow;

        switch (message.type.ToLower())
        {
            case "match_found":
                HandleMatchFound(message);
                break;
            case "queue_status":
                HandleQueueStatus(message);
                break;
            case "room_created":
                HandleRoomCreated(message);
                break;
            case "room_joined":
                HandleRoomJoined(message);
                break;
            case "match_error":
                HandleMatchError(message);
                break;
            case "heartbeat":
                HandleHeartbeatMessage(message);
                break;
            case "pong":
                _webSocketClient.HandleHeartbeatResponse();
                break;
            default:
                if (_webSocketConfig?.EnableDetailedLogging == true)
                {
                    Debug.Log($"[HybridNetworkManager] Unhandled message type: {message.type}");
                }
                break;
        }
    }

    /// <summary>
    /// 메시지 핸들러 처리
    /// </summary>
    /// <param name="message">원본 메시지</param>
    private void ProcessMessageHandlers(string message)
    {
        foreach (var handler in _messageHandlers.Values)
        {
            try
            {
                handler?.Invoke(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HybridNetworkManager] Error in message handler: {e.Message}");
            }
        }
    }
    #endregion

    #region Specific Message Handlers
    private void HandleMatchFound(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response != null)
        {
            Debug.Log($"[HybridNetworkManager] Match found! Room: {response.roomId}, Players: {response.players?.Count ?? 0}");
        }
    }

    private void HandleQueueStatus(MatchingMessage message)
    {
        if (_webSocketConfig?.EnableDetailedLogging == true)
        {
            Debug.Log($"[HybridNetworkManager] Queue status update received");
        }
    }

    private void HandleRoomCreated(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response != null)
        {
            Debug.Log($"[HybridNetworkManager] Room created! Code: {response.roomCode}");
        }
    }

    private void HandleRoomJoined(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response != null)
        {
            Debug.Log($"[HybridNetworkManager] Joined room: {response.roomId}");
        }
    }

    private void HandleMatchError(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response != null)
        {
            Debug.LogError($"[HybridNetworkManager] Match error: {response.error} (Code: {response.errorCode})");
            OnWebSocketError?.Invoke($"Match error: {response.error}");
        }
    }

    private void HandleHeartbeatMessage(MatchingMessage message)
    {
        // 하트비트 수신 시 응답 전송
        string pongMessage = MatchingProtocol.CreatePongMessage();
        if (!string.IsNullOrEmpty(pongMessage))
        {
            _webSocketClient.SendMessage(pongMessage, MessagePriority.High);
        }
    }
    #endregion

    #region Event Handlers
    private void HandleConnectionChanged(bool isConnected)
    {
        if (_webSocketConfig?.EnableLogging == true)
        {
            Debug.Log($"[HybridNetworkManager] WebSocket connection changed: {isConnected}");
        }

        OnWebSocketConnectionChanged?.Invoke(isConnected);
    }

    private void HandleWebSocketError(string error)
    {
        Debug.LogError($"[HybridNetworkManager] WebSocket error: {error}");
        OnWebSocketError?.Invoke(error);
    }

    private void HandleWebSocketClosed(System.Net.WebSockets.WebSocketCloseStatus? closeStatus, string closeDescription)
    {
        if (_webSocketConfig?.EnableLogging == true)
        {
            Debug.Log($"[HybridNetworkManager] WebSocket closed: {closeStatus} - {closeDescription}");
        }

        OnWebSocketClosed?.Invoke(closeStatus, closeDescription);
    }
    #endregion

    #region Public API
    /// <summary>
    /// WebSocket 이벤트 구독
    /// </summary>
    public void SubscribeToWebSocketEvents(Action<string> onMessage = null, Action<bool> onConnectionChanged = null, Action<string> onError = null)
    {
        if (onMessage != null)
            OnWebSocketMessage += onMessage;
        if (onConnectionChanged != null)
            OnWebSocketConnectionChanged += onConnectionChanged;
        if (onError != null)
            OnWebSocketError += onError;
    }

    /// <summary>
    /// WebSocket 이벤트 구독 해제
    /// </summary>
    public void UnsubscribeFromWebSocketEvents()
    {
        OnWebSocketMessage = null;
        OnWebSocketConnectionChanged = null;
        OnWebSocketError = null;
        OnWebSocketClosed = null;
    }

    /// <summary>
    /// 메시지 핸들러 등록
    /// </summary>
    /// <param name="handlerId">핸들러 ID</param>
    /// <param name="handler">메시지 핸들러</param>
    public void RegisterMessageHandler(string handlerId, Action<string> handler)
    {
        if (string.IsNullOrEmpty(handlerId) || handler == null)
            return;

        _messageHandlers[handlerId] = handler;
        
        if (_webSocketConfig?.EnableDetailedLogging == true)
        {
            Debug.Log($"[HybridNetworkManager] Message handler registered: {handlerId}");
        }
    }

    /// <summary>
    /// 메시지 핸들러 등록 해제
    /// </summary>
    /// <param name="handlerId">핸들러 ID</param>
    public void UnregisterMessageHandler(string handlerId)
    {
        if (_messageHandlers.Remove(handlerId) && _webSocketConfig?.EnableDetailedLogging == true)
        {
            Debug.Log($"[HybridNetworkManager] Message handler unregistered: {handlerId}");
        }
    }

    /// <summary>
    /// WebSocket 설정 업데이트
    /// </summary>
    /// <param name="config">새로운 설정</param>
    /// <returns>업데이트 성공 여부</returns>
    public bool UpdateWebSocketConfig(WebSocketConfig config)
    {
        if (config == null || !config.ValidateConfiguration())
        {
            Debug.LogError("[HybridNetworkManager] Invalid WebSocket configuration");
            return false;
        }

        bool wasConnected = IsWebSocketConnected;
        
        _webSocketConfig = config.Clone();
        
        if (wasConnected)
        {
            // 연결된 상태라면 재연결 필요
            Debug.Log("[HybridNetworkManager] Configuration updated, reconnecting...");
            _ = Task.Run(async () =>
            {
                await DisconnectWebSocketAsync();
                await Task.Delay(1000); // 잠시 대기
                await ConnectWebSocketAsync();
            });
        }

        return true;
    }

    /// <summary>
    /// WebSocket 상태 정보 가져오기
    /// </summary>
    /// <returns>상태 정보</returns>
    public Dictionary<string, object> GetWebSocketStatus()
    {
        var status = new Dictionary<string, object>
        {
            {"initialized", _isInitialized},
            {"connected", IsWebSocketConnected},
            {"connecting", IsWebSocketConnecting},
            {"state", WebSocketState?.ToString() ?? "Unknown"},
            {"queuedMessages", _webSocketClient?.MessageQueue?.QueuedCount ?? 0},
            {"reconnectAttempt", _webSocketClient?.ConnectionManager?.CurrentReconnectAttempt ?? 0},
            {"maxReconnectAttempts", _webSocketClient?.ConnectionManager?.MaxReconnectAttempts ?? 0}
        };

        return status;
    }

    /// <summary>
    /// 연결 품질 정보 가져오기
    /// </summary>
    /// <returns>연결 품질 정보</returns>
    public WebSocketConnectionQuality GetConnectionQuality()
    {
        var quality = new WebSocketConnectionQuality
        {
            IsConnected = IsWebSocketConnected,
            QualityScore = CalculateConnectionQuality(),
            ReconnectAttempts = _webSocketClient?.ConnectionManager?.CurrentReconnectAttempt ?? 0,
            QueuedMessages = _webSocketClient?.MessageQueue?.QueuedCount ?? 0,
            LastHeartbeat = _lastMessageTimes.GetValueOrDefault("heartbeat", DateTime.MinValue)
        };

        quality.Status = quality.QualityScore switch
        {
            >= 0.8f => "Excellent",
            >= 0.6f => "Good",
            >= 0.4f => "Fair",
            >= 0.2f => "Poor",
            _ => "Very Poor"
        };

        return quality;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// NetworkManager에서 인증 토큰 가져오기
    /// </summary>
    /// <returns>인증 토큰</returns>
    private string GetNetworkManagerAuthToken()
    {
        return _networkManager?.GetAuthToken() ?? "";
    }

    /// <summary>
    /// 연결 품질 점수 계산
    /// </summary>
    /// <returns>품질 점수 (0.0 ~ 1.0)</returns>
    private float CalculateConnectionQuality()
    {
        if (!IsWebSocketConnected)
            return 0f;

        float score = 1.0f;

        // 재연결 시도 횟수에 따른 점수 감점
        int reconnectAttempts = _webSocketClient?.ConnectionManager?.CurrentReconnectAttempt ?? 0;
        if (reconnectAttempts > 0)
        {
            score -= Math.Min(0.3f, reconnectAttempts * 0.1f);
        }

        // 큐에 쌓인 메시지 수에 따른 점수 감점
        int queuedMessages = _webSocketClient?.MessageQueue?.QueuedCount ?? 0;
        if (queuedMessages > 0)
        {
            score -= Math.Min(0.2f, queuedMessages * 0.01f);
        }

        // 마지막 하트비트 시간에 따른 점수 감점
        if (_lastMessageTimes.TryGetValue("heartbeat", out DateTime lastHeartbeat))
        {
            var timeSinceHeartbeat = DateTime.UtcNow - lastHeartbeat;
            if (timeSinceHeartbeat.TotalSeconds > 60) // 1분 이상
            {
                score -= 0.3f;
            }
            else if (timeSinceHeartbeat.TotalSeconds > 30) // 30초 이상
            {
                score -= 0.1f;
            }
        }

        return Math.Max(0f, score);
    }

    /// <summary>
    /// WebSocket 정리
    /// </summary>
    private void CleanupWebSocket()
    {
        try
        {
            if (_webSocketClient != null)
            {
                _webSocketClient.OnMessage -= HandleWebSocketMessage;
                _webSocketClient.OnConnectionChanged -= HandleConnectionChanged;
                _webSocketClient.OnError -= HandleWebSocketError;
                _webSocketClient.OnClosed -= HandleWebSocketClosed;
                
                _webSocketClient.Dispose();
                _webSocketClient = null;
            }

            _messageHandlers.Clear();
            _lastMessageTimes.Clear();
            _isInitialized = false;

            if (_webSocketConfig?.EnableLogging == true)
            {
                Debug.Log("[HybridNetworkManager] WebSocket cleaned up");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridNetworkManager] Error during WebSocket cleanup: {e.Message}");
        }
    }
    #endregion
}