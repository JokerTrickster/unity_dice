using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// WebSocketClient 사용법 예제
/// Stream C (NetworkManager Extensions)에서 참조할 수 있는 통합 패턴 데모
/// </summary>
public class WebSocketClientExample : MonoBehaviour
{
    [Header("WebSocket Configuration")]
    [SerializeField] private WebSocketConfig webSocketConfig;
    [SerializeField] private string testServerUrl = "wss://echo.websocket.org";
    [SerializeField] private bool autoConnect = false;

    [Header("Test Messages")]
    [SerializeField] private string[] testMessages = {
        "Hello WebSocket Server!",
        "This is a test message",
        "Testing message priority",
        "Final test message"
    };

    private WebSocketClient _webSocketClient;
    private bool _isConnected = false;

    #region Unity Lifecycle
    private void Start()
    {
        InitializeWebSocketClient();
        
        if (autoConnect)
        {
            _ = ConnectToServerAsync();
        }
    }

    private void OnDestroy()
    {
        CleanupWebSocketClient();
    }
    #endregion

    #region WebSocket Initialization
    /// <summary>
    /// WebSocket 클라이언트 초기화
    /// </summary>
    private void InitializeWebSocketClient()
    {
        try
        {
            // 설정이 없으면 런타임에 생성
            if (webSocketConfig == null)
            {
                webSocketConfig = CreateRuntimeConfig();
            }

            // WebSocket 클라이언트 생성
            _webSocketClient = new WebSocketClient(webSocketConfig);
            
            // 이벤트 핸들러 등록
            SetupEventHandlers();
            
            Debug.Log("[WebSocketExample] WebSocket client initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketExample] Failed to initialize WebSocket client: {e.Message}");
        }
    }

    /// <summary>
    /// 런타임 설정 생성
    /// </summary>
    private WebSocketConfig CreateRuntimeConfig()
    {
        var config = ScriptableObject.CreateInstance<WebSocketConfig>();
        config.SetServerUrl(testServerUrl);
        config.SetConnectionTimeout(10000);
        config.SetReconnectionSettings(3, new int[] { 1000, 2000, 5000 });
        config.SetLoggingSettings(true, true);
        
        Debug.Log("[WebSocketExample] Created runtime WebSocket configuration");
        return config;
    }

    /// <summary>
    /// 이벤트 핸들러 설정
    /// </summary>
    private void SetupEventHandlers()
    {
        _webSocketClient.OnConnectionChanged += OnConnectionChanged;
        _webSocketClient.OnMessage += OnMessageReceived;
        _webSocketClient.OnError += OnErrorOccurred;
        _webSocketClient.OnClosed += OnConnectionClosed;
        
        Debug.Log("[WebSocketExample] Event handlers configured");
    }
    #endregion

    #region Connection Management
    /// <summary>
    /// 서버 연결
    /// </summary>
    public async Task<bool> ConnectToServerAsync()
    {
        if (_webSocketClient == null)
        {
            Debug.LogError("[WebSocketExample] WebSocket client not initialized");
            return false;
        }

        try
        {
            Debug.Log($"[WebSocketExample] Connecting to {webSocketConfig.ServerUrl}...");
            bool connected = await _webSocketClient.ConnectAsync();
            
            if (connected)
            {
                Debug.Log("[WebSocketExample] Connected successfully!");
                
                // 인증 토큰 설정 (예제)
                _webSocketClient.SetAuthToken("example-jwt-token-12345");
                
                return true;
            }
            else
            {
                Debug.LogWarning("[WebSocketExample] Connection failed");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketExample] Connection error: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 서버 연결 해제
    /// </summary>
    public async Task DisconnectFromServerAsync()
    {
        if (_webSocketClient != null && _webSocketClient.IsConnected)
        {
            Debug.Log("[WebSocketExample] Disconnecting from server...");
            await _webSocketClient.DisconnectAsync();
        }
    }
    #endregion

    #region Message Handling
    /// <summary>
    /// 테스트 메시지 전송
    /// </summary>
    [ContextMenu("Send Test Messages")]
    public void SendTestMessages()
    {
        if (_webSocketClient == null || !_webSocketClient.IsConnected)
        {
            Debug.LogWarning("[WebSocketExample] Not connected to server");
            return;
        }

        foreach (var message in testMessages)
        {
            var priority = UnityEngine.Random.value > 0.7f ? MessagePriority.High : MessagePriority.Normal;
            bool queued = _webSocketClient.SendMessage(message, priority);
            
            Debug.Log($"[WebSocketExample] Message queued ({priority}): {message} - Success: {queued}");
        }
    }

    /// <summary>
    /// 매칭 요청 메시지 전송 (게임 로직 예제)
    /// </summary>
    [ContextMenu("Send Matching Request")]
    public void SendMatchingRequest()
    {
        if (_webSocketClient == null || !_webSocketClient.IsConnected)
        {
            Debug.LogWarning("[WebSocketExample] Not connected to server");
            return;
        }

        var matchingRequest = new MatchingRequestMessage
        {
            type = "matching_request",
            payload = new MatchingRequestData
            {
                playerCount = 2,
                matchType = "random",
                playerId = SystemInfo.deviceUniqueIdentifier
            },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        string jsonMessage = JsonUtility.ToJson(matchingRequest);
        bool sent = _webSocketClient.SendMessage(jsonMessage, MessagePriority.High);
        
        Debug.Log($"[WebSocketExample] Matching request sent: {sent}");
    }

    /// <summary>
    /// 즉시 메시지 전송 (큐 우회)
    /// </summary>
    public async Task SendImmediateMessage(string message)
    {
        if (_webSocketClient == null || !_webSocketClient.IsConnected)
        {
            Debug.LogWarning("[WebSocketExample] Not connected to server");
            return;
        }

        bool sent = await _webSocketClient.SendMessageImmediateAsync(message);
        Debug.Log($"[WebSocketExample] Immediate message sent: {sent} - {message}");
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 연결 상태 변경 핸들러
    /// </summary>
    private void OnConnectionChanged(bool connected)
    {
        _isConnected = connected;
        Debug.Log($"[WebSocketExample] Connection status changed: {connected}");
        
        if (connected)
        {
            // 연결 성공 시 테스트 메시지 자동 전송 (옵션)
            if (autoConnect && testMessages.Length > 0)
            {
                Invoke(nameof(SendTestMessages), 2f); // 2초 후 테스트 메시지 전송
            }
        }
    }

    /// <summary>
    /// 메시지 수신 핸들러
    /// </summary>
    private void OnMessageReceived(string message)
    {
        Debug.Log($"[WebSocketExample] Message received: {message}");
        
        // 간단한 메시지 타입 처리 예제
        try
        {
            if (message.Contains("\"type\":\"matching_response\""))
            {
                ProcessMatchingResponse(message);
            }
            else if (message.Contains("\"type\":\"heartbeat\""))
            {
                Debug.Log("[WebSocketExample] Heartbeat received");
                _webSocketClient.HandleHeartbeatResponse();
            }
            else
            {
                Debug.Log($"[WebSocketExample] General message: {message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketExample] Error processing message: {e.Message}");
        }
    }

    /// <summary>
    /// 매칭 응답 처리
    /// </summary>
    private void ProcessMatchingResponse(string jsonMessage)
    {
        try
        {
            // 실제 구현에서는 더 복잡한 JSON 파싱 필요
            Debug.Log($"[WebSocketExample] Processing matching response: {jsonMessage}");
            
            // UI 업데이트나 게임 상태 변경 로직이 여기에 들어감
            // 예: SceneManager.LoadScene("GameScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketExample] Error processing matching response: {e.Message}");
        }
    }

    /// <summary>
    /// 에러 발생 핸들러
    /// </summary>
    private void OnErrorOccurred(string error)
    {
        Debug.LogError($"[WebSocketExample] WebSocket error: {error}");
        
        // 에러에 따른 UI 업데이트나 복구 로직
        // 예: 사용자에게 연결 문제 알림 표시
    }

    /// <summary>
    /// 연결 종료 핸들러
    /// </summary>
    private void OnConnectionClosed(System.Net.WebSockets.WebSocketCloseStatus? status, string description)
    {
        Debug.Log($"[WebSocketExample] Connection closed: {status} - {description}");
        _isConnected = false;
        
        // 필요시 자동 재연결 시작
        if (status != System.Net.WebSockets.WebSocketCloseStatus.NormalClosure)
        {
            Debug.Log("[WebSocketExample] Abnormal closure, starting reconnection...");
            _webSocketClient?.StartReconnection();
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// WebSocket 클라이언트 정리
    /// </summary>
    private void CleanupWebSocketClient()
    {
        try
        {
            if (_webSocketClient != null)
            {
                // 이벤트 핸들러 해제
                _webSocketClient.OnConnectionChanged -= OnConnectionChanged;
                _webSocketClient.OnMessage -= OnMessageReceived;
                _webSocketClient.OnError -= OnErrorOccurred;
                _webSocketClient.OnClosed -= OnConnectionClosed;
                
                // 리소스 해제
                _webSocketClient.Dispose();
                _webSocketClient = null;
                
                Debug.Log("[WebSocketExample] WebSocket client cleaned up");
            }

            if (webSocketConfig != null && !Application.isPlaying)
            {
                DestroyImmediate(webSocketConfig);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketExample] Error during cleanup: {e.Message}");
        }
    }
    #endregion

    #region Public Interface for NetworkManager Integration
    /// <summary>
    /// NetworkManager가 사용할 수 있는 공개 인터페이스
    /// </summary>
    
    public bool IsWebSocketConnected => _isConnected;
    
    public WebSocketClient GetWebSocketClient() => _webSocketClient;
    
    public async Task<bool> ConnectWebSocketAsync(string url, string authToken = null)
    {
        if (_webSocketClient != null)
        {
            webSocketConfig.SetServerUrl(url);
            
            if (!string.IsNullOrEmpty(authToken))
            {
                _webSocketClient.SetAuthToken(authToken);
            }
            
            return await _webSocketClient.ConnectAsync();
        }
        return false;
    }
    
    public async Task DisconnectWebSocketAsync()
    {
        if (_webSocketClient != null)
        {
            await _webSocketClient.DisconnectAsync();
        }
    }
    
    public bool SendWebSocketMessage(string message, MessagePriority priority = MessagePriority.Normal)
    {
        return _webSocketClient?.SendMessage(message, priority) ?? false;
    }
    #endregion
}

#region Data Structures for Example
/// <summary>
/// 매칭 요청 메시지 구조체
/// </summary>
[Serializable]
public class MatchingRequestMessage
{
    public string type;
    public MatchingRequestData payload;
    public string timestamp;
}

/// <summary>
/// 매칭 요청 데이터
/// </summary>
[Serializable]
public class MatchingRequestData
{
    public int playerCount;
    public string matchType;
    public string playerId;
    public string roomCode; // 방 참여 시에만 사용
}
#endregion