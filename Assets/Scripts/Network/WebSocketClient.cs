using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Unity용 WebSocket 클라이언트
/// System.Net.WebSockets 기반으로 구현된 안정적인 WebSocket 클라이언트
/// </summary>
public class WebSocketClient : IDisposable
{
    #region Events
    /// <summary>연결 상태 변경 이벤트</summary>
    public event Action<bool> OnConnectionChanged;
    
    /// <summary>메시지 수신 이벤트</summary>
    public event Action<string> OnMessage;
    
    /// <summary>에러 발생 이벤트</summary>
    public event Action<string> OnError;
    
    /// <summary>연결 종료 이벤트</summary>
    public event Action<WebSocketCloseStatus?, string> OnClosed;
    #endregion

    #region Private Fields
    private readonly WebSocketConfig _config;
    private readonly ConnectionManager _connectionManager;
    private readonly MessageQueue _messageQueue;
    private readonly object _lockObject = new();
    
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _receiveTask;
    private volatile bool _isDisposed = false;
    private volatile bool _isConnecting = false;
    
    private readonly Dictionary<string, string> _customHeaders = new();
    private string _authToken;
    #endregion

    #region Properties
    /// <summary>연결된 상태인지</summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    
    /// <summary>연결 중인지</summary>
    public bool IsConnecting => _isConnecting || _webSocket?.State == WebSocketState.Connecting;
    
    /// <summary>연결 상태</summary>
    public WebSocketState? State => _webSocket?.State;
    
    /// <summary>현재 설정</summary>
    public WebSocketConfig Config => _config;
    
    /// <summary>연결 관리자</summary>
    public ConnectionManager ConnectionManager => _connectionManager;
    
    /// <summary>메시지 큐</summary>
    public MessageQueue MessageQueue => _messageQueue;
    #endregion

    #region Constructor
    /// <summary>
    /// WebSocket 클라이언트 초기화
    /// </summary>
    /// <param name="config">WebSocket 설정</param>
    public WebSocketClient(WebSocketConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (!_config.ValidateConfiguration())
        {
            throw new ArgumentException("Invalid WebSocket configuration");
        }

        _connectionManager = new ConnectionManager(_config);
        _messageQueue = new MessageQueue(_config);
        
        // 연결 관리자에 함수들 설정
        _connectionManager.SetConnectionFunctions(
            ConnectInternalAsync,
            DisconnectInternalAsync,
            SendInternalAsync,
            () => IsConnected
        );
        
        // 메시지 큐에 전송 함수 설정
        _messageQueue.SetSendMessageFunction(SendInternalAsync);
        
        // 이벤트 구독
        SetupEventHandlers();
        
        if (_config.EnableLogging)
        {
            Debug.Log("[WebSocketClient] Initialized");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// WebSocket 서버에 연결
    /// </summary>
    /// <returns>연결 성공 여부</returns>
    public async Task<bool> ConnectAsync()
    {
        if (_isDisposed)
        {
            Debug.LogError("[WebSocketClient] Cannot connect: Client is disposed");
            return false;
        }

        return await _connectionManager.ConnectAsync();
    }

    /// <summary>
    /// WebSocket 연결 종료
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_isDisposed)
            return;

        await _connectionManager.DisconnectAsync();
    }

    /// <summary>
    /// 메시지 전송 (큐 사용)
    /// </summary>
    /// <param name="message">전송할 메시지</param>
    /// <param name="priority">메시지 우선순위</param>
    /// <returns>큐 추가 성공 여부</returns>
    public bool SendMessage(string message, MessagePriority priority = MessagePriority.Normal)
    {
        if (_isDisposed)
        {
            Debug.LogError("[WebSocketClient] Cannot send message: Client is disposed");
            return false;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[WebSocketClient] Cannot send null or empty message");
            return false;
        }

        return _messageQueue.EnqueueMessage(message, priority);
    }

    /// <summary>
    /// 즉시 메시지 전송 (큐 우회)
    /// </summary>
    /// <param name="message">전송할 메시지</param>
    /// <returns>전송 성공 여부</returns>
    public async Task<bool> SendMessageImmediateAsync(string message)
    {
        if (_isDisposed)
        {
            Debug.LogError("[WebSocketClient] Cannot send message: Client is disposed");
            return false;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[WebSocketClient] Cannot send null or empty message");
            return false;
        }

        return await SendInternalAsync(message);
    }

    /// <summary>
    /// 인증 토큰 설정
    /// </summary>
    /// <param name="token">Bearer 토큰</param>
    public void SetAuthToken(string token)
    {
        _authToken = token;
        
        if (!string.IsNullOrEmpty(token))
        {
            _customHeaders["Authorization"] = $"Bearer {token}";
        }
        else
        {
            _customHeaders.Remove("Authorization");
        }
        
        if (_config.EnableLogging)
        {
            Debug.Log($"[WebSocketClient] Auth token {(string.IsNullOrEmpty(token) ? "cleared" : "set")}");
        }
    }

    /// <summary>
    /// 커스텀 헤더 추가
    /// </summary>
    /// <param name="key">헤더 키</param>
    /// <param name="value">헤더 값</param>
    public void AddCustomHeader(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[WebSocketClient] Header key cannot be null or empty");
            return;
        }

        _customHeaders[key] = value;
        
        if (_config.EnableDetailedLogging)
        {
            Debug.Log($"[WebSocketClient] Custom header added: {key}");
        }
    }

    /// <summary>
    /// 커스텀 헤더 제거
    /// </summary>
    /// <param name="key">헤더 키</param>
    public void RemoveCustomHeader(string key)
    {
        if (_customHeaders.Remove(key) && _config.EnableDetailedLogging)
        {
            Debug.Log($"[WebSocketClient] Custom header removed: {key}");
        }
    }

    /// <summary>
    /// 수동 재연결 시작
    /// </summary>
    public void StartReconnection()
    {
        _connectionManager.StartManualReconnection();
    }

    /// <summary>
    /// 재연결 중지
    /// </summary>
    public void StopReconnection()
    {
        _connectionManager.StopReconnection();
    }

    /// <summary>
    /// 하트비트 응답 처리
    /// </summary>
    public void HandleHeartbeatResponse()
    {
        _connectionManager.HandleHeartbeatResponse();
    }
    #endregion

    #region Private Connection Methods
    /// <summary>
    /// 내부 연결 메서드
    /// </summary>
    private async Task<bool> ConnectInternalAsync()
    {
        lock (_lockObject)
        {
            if (_isConnecting || IsConnected)
                return IsConnected;

            _isConnecting = true;
        }

        try
        {
            await CleanupWebSocket();
            
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            ConfigureWebSocket();
            
            var connectUri = new Uri(_config.ServerUrl);
            var timeout = TimeSpan.FromMilliseconds(_config.ConnectionTimeout);
            
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token, timeoutCts.Token);
            
            await _webSocket.ConnectAsync(connectUri, linkedCts.Token);
            
            if (_webSocket.State == WebSocketState.Open)
            {
                StartReceiving();
                _messageQueue.StartProcessing();
                
                // 연결 이벤트 발생 (메인 스레드에서)
                UnityMainThreadDispatcher.Instance.Enqueue(() => {
                    OnConnectionChanged?.Invoke(true);
                });
                
                if (_config.EnableLogging)
                {
                    Debug.Log($"[WebSocketClient] Connected to {_config.ServerUrl}");
                }
                
                return true;
            }
            
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketClient] Connection failed: {e.Message}");
            
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                OnError?.Invoke($"Connection failed: {e.Message}");
            });
            
            return false;
        }
        finally
        {
            _isConnecting = false;
        }
    }

    /// <summary>
    /// 내부 연결 종료 메서드
    /// </summary>
    private async Task DisconnectInternalAsync()
    {
        _messageQueue.StopProcessing();
        
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var timeout = TimeSpan.FromSeconds(5);
                using var timeoutCts = new CancellationTokenSource(timeout);
                
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Client disconnecting", 
                    timeoutCts.Token);
                
                if (_config.EnableLogging)
                {
                    Debug.Log("[WebSocketClient] Disconnected gracefully");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocketClient] Error during graceful disconnect: {e.Message}");
            }
        }
        
        await CleanupWebSocket();
        
        // 연결 해제 이벤트 발생 (메인 스레드에서)
        UnityMainThreadDispatcher.Instance.Enqueue(() => {
            OnConnectionChanged?.Invoke(false);
        });
    }

    /// <summary>
    /// WebSocket 설정
    /// </summary>
    private void ConfigureWebSocket()
    {
        // 커스텀 헤더 추가
        foreach (var header in _customHeaders)
        {
            try
            {
                _webSocket.Options.SetRequestHeader(header.Key, header.Value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocketClient] Failed to set header {header.Key}: {e.Message}");
            }
        }

        // User-Agent 설정
        try
        {
            _webSocket.Options.SetRequestHeader("User-Agent", $"UnityDice/{Application.version}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocketClient] Failed to set User-Agent: {e.Message}");
        }

        // Keep-Alive 설정
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    }
    #endregion

    #region Private Message Methods
    /// <summary>
    /// 내부 메시지 전송 메서드
    /// </summary>
    private async Task<bool> SendInternalAsync(string message)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.LogWarning("[WebSocketClient] Cannot send message: WebSocket not connected");
            }
            return false;
        }

        try
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);
            
            var timeout = TimeSpan.FromMilliseconds(_config.MessageTimeout);
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource?.Token ?? CancellationToken.None, timeoutCts.Token);
            
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, linkedCts.Token);
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"[WebSocketClient] Message sent: {message.Substring(0, Math.Min(100, message.Length))}...");
            }
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketClient] Send failed: {e.Message}");
            
            // 연결 오류인 경우 연결 끊김 처리
            if (e is WebSocketException || e is InvalidOperationException)
            {
                _connectionManager.HandleConnectionLost();
            }
            
            return false;
        }
    }

    /// <summary>
    /// 메시지 수신 시작
    /// </summary>
    private void StartReceiving()
    {
        if (_cancellationTokenSource == null || _webSocket == null)
            return;

        _receiveTask = ReceiveLoop(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// 메시지 수신 루프
    /// </summary>
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4]; // 4KB 버퍼
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && 
                   _webSocket.State == WebSocketState.Open)
            {
                using var memoryStream = new MemoryStream();
                WebSocketReceiveResult result;
                
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        var closeStatus = result.CloseStatus;
                        var closeDescription = result.CloseStatusDescription ?? "No description";
                        
                        if (_config.EnableLogging)
                        {
                            Debug.Log($"[WebSocketClient] Connection closed: {closeStatus} - {closeDescription}");
                        }
                        
                        UnityMainThreadDispatcher.Instance.Enqueue(() => {
                            OnClosed?.Invoke(closeStatus, closeDescription);
                        });
                        
                        _connectionManager.HandleConnectionLost();
                        return;
                    }
                    
                    memoryStream.Write(buffer, 0, result.Count);
                    
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(memoryStream.ToArray());
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[WebSocketClient] Message received: {message.Substring(0, Math.Min(100, message.Length))}...");
                    }
                    
                    ProcessReceivedMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[WebSocketClient] Binary message received: {memoryStream.Length} bytes");
                    }
                    
                    // 바이너리 메시지는 Base64로 인코딩해서 전달
                    var binaryData = Convert.ToBase64String(memoryStream.ToArray());
                    ProcessReceivedMessage($"{{\"type\":\"binary\",\"data\":\"{binaryData}\"}}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[WebSocketClient] Receive loop cancelled");
            }
        }
        catch (WebSocketException e)
        {
            Debug.LogError($"[WebSocketClient] WebSocket error: {e.Message}");
            
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                OnError?.Invoke($"WebSocket error: {e.Message}");
            });
            
            _connectionManager.HandleConnectionLost();
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketClient] Receive error: {e.Message}");
            
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                OnError?.Invoke($"Receive error: {e.Message}");
            });
        }
    }

    /// <summary>
    /// 수신된 메시지 처리
    /// </summary>
    private void ProcessReceivedMessage(string message)
    {
        try
        {
            // 하트비트 응답 체크
            if (IsHeartbeatResponse(message))
            {
                HandleHeartbeatResponse();
                return;
            }
            
            // 메인 스레드에서 메시지 이벤트 발생
            UnityMainThreadDispatcher.Instance.Enqueue(() => {
                OnMessage?.Invoke(message);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketClient] Error processing message: {e.Message}");
        }
    }

    /// <summary>
    /// 하트비트 응답 메시지인지 확인
    /// </summary>
    private bool IsHeartbeatResponse(string message)
    {
        try
        {
            // 간단한 JSON 파싱으로 하트비트 메시지 확인
            return message.Contains("\"type\":\"heartbeat\"") || message.Contains("\"type\":\"pong\"");
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Private Helper Methods
    /// <summary>
    /// 이벤트 핸들러 설정
    /// </summary>
    private void SetupEventHandlers()
    {
        _connectionManager.OnConnectionStateChanged += (state) => {
            if (_config.EnableLogging)
            {
                Debug.Log($"[WebSocketClient] Connection state: {state}");
            }
        };

        _messageQueue.OnMessageFailed += (messageId, error) => {
            Debug.LogWarning($"[WebSocketClient] Message failed [{messageId}]: {error}");
        };

        _messageQueue.OnQueueOverflow += (queueSize) => {
            Debug.LogWarning($"[WebSocketClient] Message queue overflow: {queueSize}");
        };
    }

    /// <summary>
    /// WebSocket 정리
    /// </summary>
    private async Task CleanupWebSocket()
    {
        _cancellationTokenSource?.Cancel();
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // 정리 중 발생하는 예외는 무시
            }
            finally
            {
                _receiveTask = null;
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
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
            DisconnectAsync().Wait(5000); // 최대 5초 대기
        }
        catch
        {
            // 종료 중 발생하는 예외는 무시
        }

        CleanupWebSocket().Wait(2000); // 최대 2초 대기
        
        _connectionManager?.Dispose();
        _messageQueue?.Dispose();

        OnConnectionChanged = null;
        OnMessage = null;
        OnError = null;
        OnClosed = null;

        if (_config != null && _config.EnableLogging)
        {
            Debug.Log("[WebSocketClient] Disposed");
        }
    }
    #endregion
}