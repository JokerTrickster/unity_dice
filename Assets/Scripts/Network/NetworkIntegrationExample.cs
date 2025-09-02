using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// NetworkManager와 WebSocket 통합 사용 예제
/// HTTP와 WebSocket을 동시에 사용하는 하이브리드 네트워크 통신 데모
/// </summary>
public class NetworkIntegrationExample : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string webSocketUrl = "wss://api.unitydice.com/matching";
    [SerializeField] private bool enableAutoStart = false;
    
    private NetworkManager networkManager;
    private string playerId;
    
    #region Unity Lifecycle
    private async void Start()
    {
        // NetworkManager 인스턴스 가져오기
        networkManager = NetworkManager.Instance;
        playerId = SystemInfo.deviceUniqueIdentifier;
        
        if (enableAutoStart)
        {
            await DemonstrateIntegration();
        }
    }

    private void OnDestroy()
    {
        // WebSocket 이벤트 구독 해제
        if (networkManager != null)
        {
            networkManager.UnsubscribeFromWebSocketEvents();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 네트워크 통합 데모 실행
    /// </summary>
    [ContextMenu("Run Integration Demo")]
    public async Task DemonstrateIntegration()
    {
        Debug.Log("[NetworkIntegrationExample] Starting network integration demo...");

        // 1. HTTP 기능 테스트 (기존 기능)
        await DemonstrateHttpFunctionality();

        // 2. WebSocket 초기화 및 연결
        await DemonstrateWebSocketInitialization();

        // 3. 하이브리드 사용 시나리오
        await DemonstrateHybridUsage();

        Debug.Log("[NetworkIntegrationExample] Integration demo completed!");
    }

    /// <summary>
    /// WebSocket만 테스트
    /// </summary>
    [ContextMenu("Test WebSocket Only")]
    public async Task TestWebSocketOnly()
    {
        await DemonstrateWebSocketInitialization();
        await TestMatchingFunctionality();
    }
    #endregion

    #region HTTP 기능 데모
    /// <summary>
    /// 기존 HTTP 기능 데모 (완전히 보존됨을 확인)
    /// </summary>
    private async Task DemonstrateHttpFunctionality()
    {
        Debug.Log("[NetworkIntegrationExample] === HTTP 기능 테스트 ===");

        // GET 요청 테스트
        networkManager.Get("/user/profile", (response) =>
        {
            if (response.IsSuccess)
            {
                Debug.Log($"HTTP GET 성공: {response.RawData}");
            }
            else
            {
                Debug.LogWarning($"HTTP GET 실패: {response.Error}");
            }
        });

        // POST 요청 테스트
        var loginData = new { username = "testuser", password = "testpass" };
        networkManager.Post("/auth/login", loginData, (response) =>
        {
            if (response.IsSuccess)
            {
                Debug.Log($"HTTP POST 성공: {response.RawData}");
                
                // 인증 토큰 설정 (WebSocket에서도 사용됨)
                var loginResponse = response.GetData<LoginResponse>();
                if (loginResponse?.token != null)
                {
                    networkManager.SetAuthToken(loginResponse.token);
                }
            }
            else
            {
                Debug.LogError($"HTTP POST 실패: {response.Error}");
            }
        });

        // Async POST 요청 테스트
        try
        {
            var asyncResponse = await networkManager.PostAsync<ApiResponse>("/test/async", new { data = "test" });
            if (asyncResponse.IsSuccess)
            {
                Debug.Log($"HTTP Async POST 성공: {asyncResponse.data?.message}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"HTTP Async POST 예외: {e.Message}");
        }

        await Task.Delay(2000); // HTTP 요청들이 완료될 시간 확보
    }
    #endregion

    #region WebSocket 기능 데모
    /// <summary>
    /// WebSocket 초기화 및 연결 데모
    /// </summary>
    private async Task DemonstrateWebSocketInitialization()
    {
        Debug.Log("[NetworkIntegrationExample] === WebSocket 초기화 및 연결 ===");

        // WebSocket 설정 생성
        var wsConfig = new WebSocketConfig(webSocketUrl)
        {
            EnableAutoReconnect = true,
            MaxReconnectAttempts = 5,
            EnableHeartbeat = true,
            HeartbeatInterval = 30000, // 30초
            EnableLogging = true,
            EnableDetailedLogging = false
        };

        // WebSocket 초기화
        bool initialized = await networkManager.InitializeWebSocketAsync(wsConfig);
        if (!initialized)
        {
            Debug.LogError("WebSocket 초기화 실패!");
            return;
        }

        // WebSocket 이벤트 구독
        networkManager.SubscribeToWebSocketEvents(
            onMessage: OnWebSocketMessage,
            onConnectionChanged: OnWebSocketConnectionChanged,
            onError: OnWebSocketError
        );

        // WebSocket 연결
        bool connected = await networkManager.ConnectWebSocketAsync();
        if (connected)
        {
            Debug.Log("WebSocket 연결 성공!");
        }
        else
        {
            Debug.LogError("WebSocket 연결 실패!");
        }
    }

    /// <summary>
    /// 매칭 기능 테스트
    /// </summary>
    private async Task TestMatchingFunctionality()
    {
        if (!networkManager.IsWebSocketConnected())
        {
            Debug.LogWarning("WebSocket이 연결되지 않음");
            return;
        }

        Debug.Log("[NetworkIntegrationExample] === 매칭 기능 테스트 ===");

        // 랜덤 매칭 요청
        bool sent = networkManager.SendJoinQueueRequest(playerId, 4, "classic", 1000);
        Debug.Log($"랜덤 매칭 요청 전송: {sent}");

        await Task.Delay(3000);

        // 매칭 취소
        networkManager.SendMatchingCancelRequest(playerId);
        Debug.Log("매칭 취소 요청 전송");

        await Task.Delay(1000);

        // 방 생성 요청
        networkManager.SendRoomCreateRequest(playerId, 4, "classic", 1000, false);
        Debug.Log("방 생성 요청 전송");
    }
    #endregion

    #region 하이브리드 사용 데모
    /// <summary>
    /// HTTP와 WebSocket을 함께 사용하는 시나리오
    /// </summary>
    private async Task DemonstrateHybridUsage()
    {
        Debug.Log("[NetworkIntegrationExample] === 하이브리드 사용 시나리오 ===");

        // 1. HTTP로 사용자 프로필 가져오기
        networkManager.Get("/user/profile", (response) =>
        {
            if (response.IsSuccess)
            {
                Debug.Log("HTTP: 사용자 프로필 로드 완료");
                
                // 2. 프로필 로드 후 WebSocket으로 매칭 시작
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    if (networkManager.IsWebSocketConnected())
                    {
                        networkManager.SendJoinQueueRequest(playerId, 2);
                        Debug.Log("WebSocket: 매칭 요청 전송");
                    }
                });
            }
        });

        // 3. 데이터 타입에 따른 최적 통신 방법 사용
        Debug.Log("=== 최적 통신 방법 선택 ===");
        
        // 인증은 HTTP
        var authType = networkManager.GetRecommendedCommunicationType(NetworkDataType.Authentication);
        Debug.Log($"인증 데이터 권장 방법: {authType}");

        // 매칭은 WebSocket
        var matchingType = networkManager.GetRecommendedCommunicationType(NetworkDataType.Matching);
        Debug.Log($"매칭 데이터 권장 방법: {matchingType}");

        // 최적 방법으로 전송
        networkManager.SendDataOptimally(
            NetworkDataType.Authentication,
            "/auth/refresh",
            new { token = networkManager.GetAuthToken() },
            callback: (response) => Debug.Log($"인증 갱신 결과: {response.IsSuccess}")
        );

        await Task.Delay(2000);
    }
    #endregion

    #region WebSocket 이벤트 핸들러
    private void OnWebSocketMessage(string message)
    {
        Debug.Log($"[WebSocket] 메시지 수신: {message.Substring(0, Mathf.Min(100, message.Length))}...");

        // 매칭 프로토콜 메시지 파싱
        var matchingMessage = MatchingProtocol.DeserializeMessage(message);
        if (matchingMessage != null)
        {
            switch (matchingMessage.type.ToLower())
            {
                case "match_found":
                    Debug.Log("🎉 매칭 성공!");
                    HandleMatchFound(matchingMessage);
                    break;
                case "queue_status":
                    Debug.Log("📊 대기열 상태 업데이트");
                    break;
                case "match_error":
                    Debug.LogError("❌ 매칭 오류 발생");
                    break;
            }
        }
    }

    private void OnWebSocketConnectionChanged(bool isConnected)
    {
        Debug.Log($"[WebSocket] 연결 상태 변경: {(isConnected ? "연결됨" : "연결 끊김")}");
        
        if (isConnected)
        {
            // 연결 시 하트비트 전송
            networkManager.SendHeartbeat(playerId);
        }
    }

    private void OnWebSocketError(string error)
    {
        Debug.LogError($"[WebSocket] 에러 발생: {error}");
    }

    private void HandleMatchFound(MatchingMessage message)
    {
        var response = message.GetPayload<MatchingResponse>();
        if (response != null)
        {
            Debug.Log($"매칭된 방 ID: {response.roomId}");
            Debug.Log($"플레이어 수: {response.players?.Count ?? 0}");
            
            // 매칭 성공 시 HTTP로 게임 세션 정보 가져오기
            networkManager.Get($"/game/session/{response.roomId}", (gameResponse) =>
            {
                if (gameResponse.IsSuccess)
                {
                    Debug.Log("HTTP: 게임 세션 정보 로드 완료");
                }
            });
        }
    }
    #endregion

    #region 상태 조회 메서드
    /// <summary>
    /// 네트워크 상태 정보 출력
    /// </summary>
    [ContextMenu("Show Network Status")]
    public void ShowNetworkStatus()
    {
        Debug.Log("=== 네트워크 상태 정보 ===");
        
        // HTTP 상태
        Debug.Log($"HTTP 사용 가능: {networkManager.IsNetworkAvailable}");
        Debug.Log($"활성 HTTP 요청: {networkManager.ActiveRequestCount}");
        Debug.Log($"HTTP 대기열: {networkManager.QueuedRequestCount}");
        
        // WebSocket 상태
        var wsStatus = networkManager.GetWebSocketStatus();
        Debug.Log($"WebSocket 초기화: {wsStatus["initialized"]}");
        Debug.Log($"WebSocket 연결: {wsStatus["connected"]}");
        Debug.Log($"WebSocket 상태: {wsStatus["state"]}");
        Debug.Log($"큐 메시지: {wsStatus["queuedMessages"]}");
        
        // 연결 품질
        var quality = networkManager.GetWebSocketConnectionQuality();
        Debug.Log($"연결 품질: {quality.Status} ({quality.QualityScore:F2})");
        Debug.Log($"재연결 시도: {quality.ReconnectAttempts}");
    }
    #endregion
}

#region 응답 데이터 구조
[System.Serializable]
public class LoginResponse
{
    public string token;
    public string userId;
    public string username;
}

[System.Serializable]
public class ApiResponse
{
    public bool success;
    public string message;
    public object data;
}
#endregion