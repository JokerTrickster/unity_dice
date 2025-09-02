using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 매칭 시스템용 WebSocket 네트워크 핸들러
/// WebSocket 통신을 담당하고 MatchingManager와 연동하여 매칭 요청/응답을 처리
/// </summary>
public class MatchingNetworkHandler : MonoBehaviour
{
    #region Events
    /// <summary>매칭 응답 수신 이벤트</summary>
    public event Action<MatchingResponse> OnMatchingResponse;
    
    /// <summary>연결 상태 변경 이벤트</summary>
    public event Action<bool> OnConnectionStateChanged;
    
    /// <summary>네트워크 에러 이벤트</summary>
    public event Action<string, string> OnNetworkError; // (errorCode, errorMessage)
    
    /// <summary>매칭 취소됨 이벤트</summary>
    public event Action<string> OnMatchingCancelled; // playerId
    
    /// <summary>하트비트 응답 이벤트</summary>
    public event Action OnHeartbeatReceived;
    #endregion

    #region Private Fields
    private NetworkManager _networkManager;
    private MatchingTimeout _timeoutManager;
    private MatchingReconnection _reconnectionManager;
    
    private bool _isInitialized = false;
    private bool _isProcessingMessage = false;
    
    private readonly Dictionary<string, DateTime> _pendingRequests = new();
    private readonly object _lockObject = new();
    
    // Configuration
    private const int MAX_RECONNECT_ATTEMPTS = 3;
    private const float HEARTBEAT_INTERVAL = 30f;
    private const float CONNECTION_TIMEOUT = 10f;
    #endregion

    #region Properties
    /// <summary>초기화 상태</summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>WebSocket 연결 상태</summary>
    public bool IsConnected => _networkManager?.IsWebSocketConnected() ?? false;
    
    /// <summary>보류 중인 요청 수</summary>
    public int PendingRequestCount => _pendingRequests.Count;
    
    /// <summary>타임아웃 매니저</summary>
    public MatchingTimeout TimeoutManager => _timeoutManager;
    
    /// <summary>재연결 매니저</summary>
    public MatchingReconnection ReconnectionManager => _reconnectionManager;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // 타임아웃 및 재연결 매니저 초기화
        _timeoutManager = gameObject.AddComponent<MatchingTimeout>();
        _reconnectionManager = gameObject.AddComponent<MatchingReconnection>();
        
        SetupTimeoutEvents();
        SetupReconnectionEvents();
        
        if (FindObjectOfType<NetworkManager>() is NetworkManager networkManager)
        {
            InitializeAsync(networkManager);
        }
        else
        {
            Debug.LogError("[MatchingNetworkHandler] NetworkManager not found in scene");
        }
    }

    private void OnDestroy()
    {
        CleanupEvents();
        
        if (_timeoutManager != null)
        {
            _timeoutManager.StopAllTimeouts();
        }
        
        if (_reconnectionManager != null)
        {
            _reconnectionManager.StopReconnection();
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 네트워크 핸들러 초기화
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    public async Task InitializeAsync(NetworkManager networkManager)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[MatchingNetworkHandler] Already initialized");
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError("[MatchingNetworkHandler] NetworkManager cannot be null");
            return;
        }

        try
        {
            _networkManager = networkManager;
            
            // WebSocket 초기화 및 연결
            var config = Resources.Load<WebSocketConfig>("DefaultWebSocketConfig");
            if (config == null)
            {
                Debug.LogError("[MatchingNetworkHandler] WebSocket config not found in Resources");
                return;
            }

            bool initialized = await _networkManager.InitializeWebSocketAsync(config);
            if (!initialized)
            {
                Debug.LogError("[MatchingNetworkHandler] Failed to initialize WebSocket");
                return;
            }

            // 이벤트 구독
            SetupNetworkEvents();
            
            // WebSocket 연결
            bool connected = await _networkManager.ConnectWebSocketAsync();
            if (!connected)
            {
                Debug.LogWarning("[MatchingNetworkHandler] Failed to connect WebSocket, will retry via reconnection manager");
                _reconnectionManager.StartReconnection();
            }

            _isInitialized = true;
            Debug.Log("[MatchingNetworkHandler] Initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// 네트워크 이벤트 설정
    /// </summary>
    private void SetupNetworkEvents()
    {
        if (_networkManager == null) return;

        _networkManager.SubscribeToWebSocketEvents(
            onMessage: HandleWebSocketMessage,
            onConnectionChanged: HandleConnectionChanged,
            onError: HandleWebSocketError
        );
    }

    /// <summary>
    /// 타임아웃 이벤트 설정
    /// </summary>
    private void SetupTimeoutEvents()
    {
        if (_timeoutManager == null) return;

        _timeoutManager.OnRequestTimeout += HandleRequestTimeout;
        _timeoutManager.OnTimeoutWarning += HandleTimeoutWarning;
    }

    /// <summary>
    /// 재연결 이벤트 설정
    /// </summary>
    private void SetupReconnectionEvents()
    {
        if (_reconnectionManager == null) return;

        _reconnectionManager.OnReconnectionStarted += HandleReconnectionStarted;
        _reconnectionManager.OnReconnectionSuccess += HandleReconnectionSuccess;
        _reconnectionManager.OnReconnectionFailed += HandleReconnectionFailed;
        _reconnectionManager.OnMaxAttemptsReached += HandleMaxReconnectionAttempts;
    }

    /// <summary>
    /// 이벤트 정리
    /// </summary>
    private void CleanupEvents()
    {
        if (_networkManager != null)
        {
            _networkManager.UnsubscribeFromWebSocketEvents();
        }

        if (_timeoutManager != null)
        {
            _timeoutManager.OnRequestTimeout -= HandleRequestTimeout;
            _timeoutManager.OnTimeoutWarning -= HandleTimeoutWarning;
        }

        if (_reconnectionManager != null)
        {
            _reconnectionManager.OnReconnectionStarted -= HandleReconnectionStarted;
            _reconnectionManager.OnReconnectionSuccess -= HandleReconnectionSuccess;
            _reconnectionManager.OnReconnectionFailed -= HandleReconnectionFailed;
            _reconnectionManager.OnMaxAttemptsReached -= HandleMaxReconnectionAttempts;
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 랜덤 매칭 요청
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <returns>요청 성공 여부</returns>
    public async Task<bool> SendJoinQueueRequestAsync(string playerId, int playerCount, string gameMode = "classic", int betAmount = 0)
    {
        if (!ValidateConnection("join queue"))
            return false;

        try
        {
            // 요청 생성
            var request = new MatchingRequest(playerId, playerCount, gameMode, betAmount);
            if (!request.IsValid())
            {
                Debug.LogError("[MatchingNetworkHandler] Invalid join queue request");
                return false;
            }

            // 타임아웃 시작
            _timeoutManager.StartRequestTimeout(request.requestTime, playerId);
            
            // 요청 추적
            lock (_lockObject)
            {
                _pendingRequests[request.requestTime] = DateTime.UtcNow;
            }

            // WebSocket으로 전송
            bool sent = _networkManager.SendJoinQueueRequest(playerId, playerCount, gameMode, betAmount);
            
            if (sent)
            {
                Debug.Log($"[MatchingNetworkHandler] Join queue request sent for player {playerId}");
            }
            else
            {
                // 전송 실패 시 타임아웃 정리
                _timeoutManager.CancelTimeout(request.requestTime);
                RemovePendingRequest(request.requestTime);
                Debug.LogError("[MatchingNetworkHandler] Failed to send join queue request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Join queue request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 방 생성 요청
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">최대 플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <returns>요청 성공 여부</returns>
    public async Task<bool> SendRoomCreateRequestAsync(string playerId, int playerCount, string gameMode = "classic", int betAmount = 0, bool isPrivate = false)
    {
        if (!ValidateConnection("create room"))
            return false;

        try
        {
            var request = MatchingRequest.CreateRoom(playerId, playerCount, gameMode, betAmount, isPrivate);
            if (!request.IsValid())
            {
                Debug.LogError("[MatchingNetworkHandler] Invalid room create request");
                return false;
            }

            _timeoutManager.StartRequestTimeout(request.requestTime, playerId);
            
            lock (_lockObject)
            {
                _pendingRequests[request.requestTime] = DateTime.UtcNow;
            }

            bool sent = _networkManager.SendRoomCreateRequest(playerId, playerCount, gameMode, betAmount, isPrivate);
            
            if (sent)
            {
                Debug.Log($"[MatchingNetworkHandler] Room create request sent for player {playerId}");
            }
            else
            {
                _timeoutManager.CancelTimeout(request.requestTime);
                RemovePendingRequest(request.requestTime);
                Debug.LogError("[MatchingNetworkHandler] Failed to send room create request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Room create request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 방 참가 요청
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="roomCode">방 코드</param>
    /// <returns>요청 성공 여부</returns>
    public async Task<bool> SendRoomJoinRequestAsync(string playerId, string roomCode)
    {
        if (!ValidateConnection("join room"))
            return false;

        try
        {
            var request = new MatchingRequest(playerId, roomCode);
            if (!request.IsValid())
            {
                Debug.LogError("[MatchingNetworkHandler] Invalid room join request");
                return false;
            }

            _timeoutManager.StartRequestTimeout(request.requestTime, playerId);
            
            lock (_lockObject)
            {
                _pendingRequests[request.requestTime] = DateTime.UtcNow;
            }

            bool sent = _networkManager.SendRoomJoinRequest(playerId, roomCode);
            
            if (sent)
            {
                Debug.Log($"[MatchingNetworkHandler] Room join request sent for player {playerId}");
            }
            else
            {
                _timeoutManager.CancelTimeout(request.requestTime);
                RemovePendingRequest(request.requestTime);
                Debug.LogError("[MatchingNetworkHandler] Failed to send room join request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Room join request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 매칭 취소 요청
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>요청 성공 여부</returns>
    public async Task<bool> SendMatchingCancelRequestAsync(string playerId)
    {
        if (!ValidateConnection("cancel matching"))
            return false;

        try
        {
            bool sent = _networkManager.SendMatchingCancelRequest(playerId);
            
            if (sent)
            {
                // 모든 대기 중인 요청 타임아웃 취소
                _timeoutManager.CancelAllTimeouts();
                ClearPendingRequests();
                
                Debug.Log($"[MatchingNetworkHandler] Matching cancel request sent for player {playerId}");
            }
            else
            {
                Debug.LogError("[MatchingNetworkHandler] Failed to send matching cancel request");
            }

            return sent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Matching cancel request failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 하트비트 전송
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>전송 성공 여부</returns>
    public bool SendHeartbeat(string playerId = "")
    {
        if (!IsConnected)
        {
            return false; // 연결 끊어진 상태에서는 하트비트 무시
        }

        try
        {
            return _networkManager.SendHeartbeat(playerId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Heartbeat failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 강제 재연결 시작
    /// </summary>
    public void ForceReconnect()
    {
        if (_reconnectionManager != null)
        {
            _reconnectionManager.StartReconnection();
        }
    }

    /// <summary>
    /// 연결 상태 확인
    /// </summary>
    /// <returns>연결 품질 정보</returns>
    public WebSocketConnectionQuality GetConnectionQuality()
    {
        return _networkManager?.GetWebSocketConnectionQuality() ?? new WebSocketConnectionQuality
        {
            IsConnected = false,
            QualityScore = 0f,
            Status = "Not initialized"
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 연결 유효성 검증
    /// </summary>
    /// <param name="operation">수행하려는 작업</param>
    /// <returns>유효한 연결인지 여부</returns>
    private bool ValidateConnection(string operation)
    {
        if (!_isInitialized)
        {
            Debug.LogError($"[MatchingNetworkHandler] Cannot {operation}: Not initialized");
            return false;
        }

        if (!IsConnected)
        {
            Debug.LogWarning($"[MatchingNetworkHandler] Cannot {operation}: Not connected to server");
            
            // 자동 재연결 시도
            if (_reconnectionManager != null && !_reconnectionManager.IsReconnecting)
            {
                _reconnectionManager.StartReconnection();
            }
            
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대기 중인 요청 제거
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    private void RemovePendingRequest(string requestId)
    {
        lock (_lockObject)
        {
            _pendingRequests.Remove(requestId);
        }
    }

    /// <summary>
    /// 모든 대기 중인 요청 제거
    /// </summary>
    private void ClearPendingRequests()
    {
        lock (_lockObject)
        {
            _pendingRequests.Clear();
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// WebSocket 메시지 처리
    /// </summary>
    /// <param name="message">수신된 메시지</param>
    private void HandleWebSocketMessage(string message)
    {
        if (_isProcessingMessage)
        {
            Debug.LogWarning("[MatchingNetworkHandler] Already processing a message, queuing might be needed");
            return;
        }

        _isProcessingMessage = true;

        try
        {
            // 메시지 파싱
            var matchingMessage = MatchingProtocol.DeserializeMessage(message);
            if (matchingMessage == null)
            {
                Debug.LogError($"[MatchingNetworkHandler] Failed to parse message: {message}");
                return;
            }

            Debug.Log($"[MatchingNetworkHandler] Received message type: {matchingMessage.type}");

            switch (matchingMessage.type.ToLower())
            {
                case "queue_status":
                case "match_found":
                case "room_created":
                case "room_joined":
                case "match_cancelled":
                case "match_error":
                    ProcessMatchingResponse(matchingMessage);
                    break;

                case "heartbeat":
                    ProcessHeartbeat(matchingMessage);
                    break;

                case "pong":
                    OnHeartbeatReceived?.Invoke();
                    break;

                default:
                    Debug.LogWarning($"[MatchingNetworkHandler] Unknown message type: {matchingMessage.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Message processing failed: {e.Message}");
        }
        finally
        {
            _isProcessingMessage = false;
        }
    }

    /// <summary>
    /// 매칭 응답 처리
    /// </summary>
    /// <param name="message">매칭 메시지</param>
    private void ProcessMatchingResponse(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response == null || !response.IsValid())
        {
            Debug.LogError("[MatchingNetworkHandler] Invalid matching response received");
            return;
        }

        // 타임아웃 취소 (응답 받음)
        _timeoutManager.CancelAllTimeouts();
        ClearPendingRequests();

        // 응답 이벤트 발생
        OnMatchingResponse?.Invoke(response);

        Debug.Log($"[MatchingNetworkHandler] Processed matching response: {response.GetSummary()}");
    }

    /// <summary>
    /// 하트비트 처리
    /// </summary>
    /// <param name="message">하트비트 메시지</param>
    private void ProcessHeartbeat(MatchingMessage message)
    {
        try
        {
            // 하트비트 응답 전송
            var payload = message.GetPayload<dynamic>();
            string playerId = payload?.playerId ?? "";
            
            _networkManager?.SendWebSocketMessage(
                MatchingProtocol.CreatePongMessage(playerId), 
                MessagePriority.Low
            );

            Debug.Log("[MatchingNetworkHandler] Heartbeat acknowledged");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingNetworkHandler] Heartbeat processing failed: {e.Message}");
        }
    }

    /// <summary>
    /// 연결 상태 변경 처리
    /// </summary>
    /// <param name="isConnected">연결 상태</param>
    private void HandleConnectionChanged(bool isConnected)
    {
        Debug.Log($"[MatchingNetworkHandler] Connection state changed: {isConnected}");
        
        if (isConnected)
        {
            // 재연결 성공 시 재연결 매니저 정지
            if (_reconnectionManager != null && _reconnectionManager.IsReconnecting)
            {
                _reconnectionManager.StopReconnection();
            }
        }
        else
        {
            // 연결 끊어짐 시 자동 재연결 시작
            if (_reconnectionManager != null)
            {
                _reconnectionManager.StartReconnection();
            }
        }

        OnConnectionStateChanged?.Invoke(isConnected);
    }

    /// <summary>
    /// WebSocket 에러 처리
    /// </summary>
    /// <param name="error">에러 메시지</param>
    private void HandleWebSocketError(string error)
    {
        Debug.LogError($"[MatchingNetworkHandler] WebSocket error: {error}");
        OnNetworkError?.Invoke("WEBSOCKET_ERROR", error);
        
        // 에러 발생 시 재연결 시도
        if (_reconnectionManager != null)
        {
            _reconnectionManager.StartReconnection();
        }
    }

    /// <summary>
    /// 요청 타임아웃 처리
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <param name="playerId">플레이어 ID</param>
    private void HandleRequestTimeout(string requestId, string playerId)
    {
        Debug.LogWarning($"[MatchingNetworkHandler] Request timeout: {requestId} for player {playerId}");
        
        RemovePendingRequest(requestId);
        OnNetworkError?.Invoke("REQUEST_TIMEOUT", $"Request timed out for player {playerId}");
        
        // 타임아웃 시 매칭 취소 이벤트 발생
        OnMatchingCancelled?.Invoke(playerId);
    }

    /// <summary>
    /// 타임아웃 경고 처리
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <param name="remainingSeconds">남은 시간(초)</param>
    private void HandleTimeoutWarning(string requestId, int remainingSeconds)
    {
        Debug.Log($"[MatchingNetworkHandler] Timeout warning: {remainingSeconds}s remaining for request {requestId}");
    }

    /// <summary>
    /// 재연결 시작 처리
    /// </summary>
    private void HandleReconnectionStarted()
    {
        Debug.Log("[MatchingNetworkHandler] Reconnection started");
        OnNetworkError?.Invoke("RECONNECTING", "Attempting to reconnect to server");
    }

    /// <summary>
    /// 재연결 성공 처리
    /// </summary>
    private void HandleReconnectionSuccess()
    {
        Debug.Log("[MatchingNetworkHandler] Reconnection successful");
    }

    /// <summary>
    /// 재연결 실패 처리
    /// </summary>
    /// <param name="attemptCount">시도 횟수</param>
    private void HandleReconnectionFailed(int attemptCount)
    {
        Debug.LogWarning($"[MatchingNetworkHandler] Reconnection failed (attempt {attemptCount})");
    }

    /// <summary>
    /// 최대 재연결 시도 횟수 도달 처리
    /// </summary>
    private void HandleMaxReconnectionAttempts()
    {
        Debug.LogError("[MatchingNetworkHandler] Maximum reconnection attempts reached");
        OnNetworkError?.Invoke("CONNECTION_FAILED", "Unable to reconnect to server after maximum attempts");
    }
    #endregion
}