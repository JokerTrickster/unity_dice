using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// NetworkManager WebSocket 확장 기능
/// 기존 HTTP 기능을 유지하면서 WebSocket 기능을 추가하는 확장 메서드들
/// </summary>
public static class NetworkManagerExtensions
{
    #region WebSocket Management
    /// <summary>
    /// WebSocket 연결 초기화
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="config">WebSocket 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public static async Task<bool> InitializeWebSocketAsync(this NetworkManager networkManager, WebSocketConfig config)
    {
        if (networkManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] NetworkManager instance is null");
            return false;
        }

        if (config == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket config is null");
            return false;
        }

        // HybridNetworkManager를 통해 WebSocket 초기화
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            hybridManager = networkManager.gameObject.AddComponent<HybridNetworkManager>();
        }

        return await hybridManager.InitializeWebSocketAsync(config);
    }

    /// <summary>
    /// WebSocket 서버에 연결
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="serverUrl">서버 URL (선택적, 기본 설정 사용)</param>
    /// <returns>연결 성공 여부</returns>
    public static async Task<bool> ConnectWebSocketAsync(this NetworkManager networkManager, string serverUrl = null)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket not initialized. Call InitializeWebSocketAsync first.");
            return false;
        }

        return await hybridManager.ConnectWebSocketAsync(serverUrl);
    }

    /// <summary>
    /// WebSocket 연결 해제
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    public static async Task DisconnectWebSocketAsync(this NetworkManager networkManager)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager != null)
        {
            await hybridManager.DisconnectWebSocketAsync();
        }
    }

    /// <summary>
    /// WebSocket 연결 상태 확인
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <returns>연결된 상태인지 여부</returns>
    public static bool IsWebSocketConnected(this NetworkManager networkManager)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        return hybridManager?.IsWebSocketConnected ?? false;
    }
    #endregion

    #region Message Sending
    /// <summary>
    /// WebSocket 메시지 전송 (큐 사용)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="message">전송할 메시지</param>
    /// <param name="priority">메시지 우선순위</param>
    /// <returns>큐 추가 성공 여부</returns>
    public static bool SendWebSocketMessage(this NetworkManager networkManager, string message, MessagePriority priority = MessagePriority.Normal)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket not initialized");
            return false;
        }

        return hybridManager.SendWebSocketMessage(message, priority);
    }

    /// <summary>
    /// 즉시 WebSocket 메시지 전송 (큐 우회)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="message">전송할 메시지</param>
    /// <returns>전송 성공 여부</returns>
    public static async Task<bool> SendWebSocketMessageImmediateAsync(this NetworkManager networkManager, string message)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket not initialized");
            return false;
        }

        return await hybridManager.SendWebSocketMessageImmediateAsync(message);
    }

    /// <summary>
    /// 제네릭 객체를 JSON으로 직렬화해서 WebSocket으로 전송
    /// </summary>
    /// <typeparam name="T">전송할 객체 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="data">전송할 데이터</param>
    /// <param name="priority">메시지 우선순위</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendWebSocketData<T>(this NetworkManager networkManager, T data, MessagePriority priority = MessagePriority.Normal)
    {
        if (data == null)
        {
            Debug.LogError("[NetworkManagerExtensions] Cannot send null data");
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(data);
            return networkManager.SendWebSocketMessage(json, priority);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManagerExtensions] Failed to serialize data: {e.Message}");
            return false;
        }
    }
    #endregion

    #region Matching Protocol Integration
    /// <summary>
    /// 랜덤 매칭 요청 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendJoinQueueRequest(this NetworkManager networkManager, string playerId, int playerCount, string gameMode = "classic", int betAmount = 0)
    {
        string message = MatchingProtocol.CreateJoinQueueMessage(playerId, playerCount, gameMode, betAmount);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[NetworkManagerExtensions] Failed to create join queue message");
            return false;
        }

        return networkManager.SendWebSocketMessage(message, MessagePriority.High);
    }

    /// <summary>
    /// 방 생성 요청 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">최대 플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendRoomCreateRequest(this NetworkManager networkManager, string playerId, int playerCount, string gameMode = "classic", int betAmount = 0, bool isPrivate = false)
    {
        string message = MatchingProtocol.CreateRoomCreateMessage(playerId, playerCount, gameMode, betAmount, isPrivate);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[NetworkManagerExtensions] Failed to create room creation message");
            return false;
        }

        return networkManager.SendWebSocketMessage(message, MessagePriority.High);
    }

    /// <summary>
    /// 방 참가 요청 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="roomCode">방 코드</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendRoomJoinRequest(this NetworkManager networkManager, string playerId, string roomCode)
    {
        string message = MatchingProtocol.CreateRoomJoinMessage(playerId, roomCode);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[NetworkManagerExtensions] Failed to create room join message");
            return false;
        }

        return networkManager.SendWebSocketMessage(message, MessagePriority.High);
    }

    /// <summary>
    /// 매칭 취소 요청 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendMatchingCancelRequest(this NetworkManager networkManager, string playerId)
    {
        string message = MatchingProtocol.CreateCancelMessage(playerId);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[NetworkManagerExtensions] Failed to create cancel message");
            return false;
        }

        return networkManager.SendWebSocketMessage(message, MessagePriority.Normal);
    }

    /// <summary>
    /// 하트비트 메시지 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="playerId">플레이어 ID (선택적)</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendHeartbeat(this NetworkManager networkManager, string playerId = "")
    {
        string message = MatchingProtocol.CreateHeartbeatMessage(playerId);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[NetworkManagerExtensions] Failed to create heartbeat message");
            return false;
        }

        return networkManager.SendWebSocketMessage(message, MessagePriority.Low);
    }
    #endregion

    #region Event Subscription Helpers
    /// <summary>
    /// WebSocket 이벤트 구독 헬퍼
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="onMessage">메시지 수신 이벤트</param>
    /// <param name="onConnectionChanged">연결 상태 변경 이벤트</param>
    /// <param name="onError">에러 발생 이벤트</param>
    public static void SubscribeToWebSocketEvents(this NetworkManager networkManager,
        Action<string> onMessage = null,
        Action<bool> onConnectionChanged = null,
        Action<string> onError = null)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket not initialized");
            return;
        }

        hybridManager.SubscribeToWebSocketEvents(onMessage, onConnectionChanged, onError);
    }

    /// <summary>
    /// WebSocket 이벤트 구독 해제
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    public static void UnsubscribeFromWebSocketEvents(this NetworkManager networkManager)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager != null)
        {
            hybridManager.UnsubscribeFromWebSocketEvents();
        }
    }
    #endregion

    #region Configuration and Status
    /// <summary>
    /// WebSocket 설정 업데이트
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="config">새로운 설정</param>
    /// <returns>업데이트 성공 여부</returns>
    public static bool UpdateWebSocketConfig(this NetworkManager networkManager, WebSocketConfig config)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            Debug.LogError("[NetworkManagerExtensions] WebSocket not initialized");
            return false;
        }

        return hybridManager.UpdateWebSocketConfig(config);
    }

    /// <summary>
    /// WebSocket 상태 정보 가져오기
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <returns>상태 정보 딕셔너리</returns>
    public static Dictionary<string, object> GetWebSocketStatus(this NetworkManager networkManager)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            return new Dictionary<string, object>
            {
                {"initialized", false},
                {"connected", false},
                {"error", "WebSocket not initialized"}
            };
        }

        return hybridManager.GetWebSocketStatus();
    }

    /// <summary>
    /// WebSocket 연결 품질 정보 가져오기
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <returns>연결 품질 정보</returns>
    public static WebSocketConnectionQuality GetWebSocketConnectionQuality(this NetworkManager networkManager)
    {
        var hybridManager = networkManager.GetComponent<HybridNetworkManager>();
        if (hybridManager == null)
        {
            return new WebSocketConnectionQuality
            {
                IsConnected = false,
                QualityScore = 0f,
                Status = "Not initialized"
            };
        }

        return hybridManager.GetConnectionQuality();
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 네트워크 타입별 추천 통신 방법 가져오기
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="dataType">데이터 타입</param>
    /// <returns>추천 통신 방법</returns>
    public static NetworkCommunicationType GetRecommendedCommunicationType(this NetworkManager networkManager, NetworkDataType dataType)
    {
        // HTTP는 RESTful API, 인증, 파일 업로드 등에 적합
        // WebSocket은 실시간 매칭, 게임 상태 동기화 등에 적합
        return dataType switch
        {
            NetworkDataType.Authentication => NetworkCommunicationType.HTTP,
            NetworkDataType.UserProfile => NetworkCommunicationType.HTTP,
            NetworkDataType.GameHistory => NetworkCommunicationType.HTTP,
            NetworkDataType.FileUpload => NetworkCommunicationType.HTTP,
            NetworkDataType.Matching => NetworkCommunicationType.WebSocket,
            NetworkDataType.RealTimeGameState => NetworkCommunicationType.WebSocket,
            NetworkDataType.Chat => NetworkCommunicationType.WebSocket,
            NetworkDataType.Notification => NetworkCommunicationType.WebSocket,
            _ => NetworkCommunicationType.HTTP
        };
    }

    /// <summary>
    /// 최적의 통신 방법으로 데이터 전송
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="dataType">데이터 타입</param>
    /// <param name="endpoint">엔드포인트 (HTTP용)</param>
    /// <param name="data">전송할 데이터</param>
    /// <param name="callback">HTTP 응답 콜백</param>
    /// <param name="priority">WebSocket 메시지 우선순위</param>
    /// <returns>전송 성공 여부</returns>
    public static bool SendDataOptimally(this NetworkManager networkManager,
        NetworkDataType dataType,
        string endpoint,
        object data,
        Action<NetworkResponse> callback = null,
        MessagePriority priority = MessagePriority.Normal)
    {
        var recommendedType = networkManager.GetRecommendedCommunicationType(dataType);

        switch (recommendedType)
        {
            case NetworkCommunicationType.HTTP:
                if (data == null)
                {
                    networkManager.Get(endpoint, callback);
                }
                else
                {
                    networkManager.Post(endpoint, data, callback);
                }
                return true;

            case NetworkCommunicationType.WebSocket:
                return networkManager.SendWebSocketData(data, priority);

            default:
                Debug.LogWarning($"[NetworkManagerExtensions] Unknown communication type: {recommendedType}");
                return false;
        }
    }
    #endregion
}

#region Supporting Data Structures
/// <summary>
/// 네트워크 데이터 타입
/// </summary>
public enum NetworkDataType
{
    Authentication,
    UserProfile,
    GameHistory,
    FileUpload,
    Matching,
    RealTimeGameState,
    Chat,
    Notification
}

/// <summary>
/// 네트워크 통신 타입
/// </summary>
public enum NetworkCommunicationType
{
    HTTP,
    WebSocket
}

/// <summary>
/// WebSocket 연결 품질 정보
/// </summary>
public class WebSocketConnectionQuality
{
    public bool IsConnected { get; set; }
    public float QualityScore { get; set; } // 0.0 ~ 1.0
    public string Status { get; set; }
    public int ReconnectAttempts { get; set; }
    public int QueuedMessages { get; set; }
    public DateTime LastHeartbeat { get; set; }
}
#endregion