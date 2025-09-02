using System;
using UnityEngine;

/// <summary>
/// WebSocket 클라이언트 설정 클래스
/// WebSocket 연결, 재연결, 메시지 처리에 관련된 모든 설정을 관리
/// </summary>
[Serializable]
public class WebSocketConfig
{
    #region Connection Settings
    [Header("Connection Settings")]
    [SerializeField] private string serverUrl = "wss://api.unitydice.com/matching";
    [SerializeField] private int connectionTimeout = 10000; // 10초
    [SerializeField] private int messageTimeout = 30000; // 30초
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool enableDetailedLogging = false;
    #endregion

    #region Reconnection Settings
    [Header("Reconnection Settings")]
    [SerializeField] private bool enableAutoReconnect = true;
    [SerializeField] private int maxReconnectAttempts = 5;
    [SerializeField] private int[] retryDelays = { 1000, 2000, 5000, 10000, 30000 }; // 밀리초
    #endregion

    #region Message Queue Settings
    [Header("Message Queue Settings")]
    [SerializeField] private int maxMessageQueueSize = 100;
    [SerializeField] private int maxMessageSize = 1024 * 1024; // 1MB
    #endregion

    #region Heartbeat Settings
    [Header("Heartbeat Settings")]
    [SerializeField] private bool enableHeartbeat = true;
    [SerializeField] private int heartbeatInterval = 30000; // 30초
    [SerializeField] private int heartbeatTimeout = 60000; // 60초
    #endregion

    #region Properties
    /// <summary>WebSocket 서버 URL</summary>
    public string ServerUrl 
    { 
        get => serverUrl; 
        set => serverUrl = value; 
    }

    /// <summary>연결 타임아웃 (밀리초)</summary>
    public int ConnectionTimeout 
    { 
        get => connectionTimeout; 
        set => connectionTimeout = Mathf.Max(1000, value); 
    }

    /// <summary>메시지 타임아웃 (밀리초)</summary>
    public int MessageTimeout 
    { 
        get => messageTimeout; 
        set => messageTimeout = Mathf.Max(1000, value); 
    }

    /// <summary>로깅 활성화</summary>
    public bool EnableLogging 
    { 
        get => enableLogging; 
        set => enableLogging = value; 
    }

    /// <summary>상세 로깅 활성화</summary>
    public bool EnableDetailedLogging 
    { 
        get => enableDetailedLogging; 
        set => enableDetailedLogging = value; 
    }

    /// <summary>자동 재연결 활성화</summary>
    public bool EnableAutoReconnect 
    { 
        get => enableAutoReconnect; 
        set => enableAutoReconnect = value; 
    }

    /// <summary>최대 재연결 시도 횟수</summary>
    public int MaxReconnectAttempts 
    { 
        get => maxReconnectAttempts; 
        set => maxReconnectAttempts = Mathf.Max(0, value); 
    }

    /// <summary>재시도 지연 배열</summary>
    public int[] RetryDelays 
    { 
        get => retryDelays; 
        set => retryDelays = value ?? new int[] { 1000, 2000, 5000 }; 
    }

    /// <summary>최대 메시지 큐 크기</summary>
    public int MaxMessageQueueSize 
    { 
        get => maxMessageQueueSize; 
        set => maxMessageQueueSize = Mathf.Max(10, value); 
    }

    /// <summary>최대 메시지 크기 (바이트)</summary>
    public int MaxMessageSize 
    { 
        get => maxMessageSize; 
        set => maxMessageSize = Mathf.Max(1024, value); 
    }

    /// <summary>하트비트 활성화</summary>
    public bool EnableHeartbeat 
    { 
        get => enableHeartbeat; 
        set => enableHeartbeat = value; 
    }

    /// <summary>하트비트 간격 (밀리초)</summary>
    public int HeartbeatInterval 
    { 
        get => heartbeatInterval; 
        set => heartbeatInterval = Mathf.Max(5000, value); 
    }

    /// <summary>하트비트 타임아웃 (밀리초)</summary>
    public int HeartbeatTimeout 
    { 
        get => heartbeatTimeout; 
        set => heartbeatTimeout = Mathf.Max(heartbeatInterval * 2, value); 
    }
    #endregion

    #region Constructors
    /// <summary>
    /// 기본 설정으로 초기화
    /// </summary>
    public WebSocketConfig()
    {
        // 기본값은 필드 초기화에서 설정됨
    }

    /// <summary>
    /// 서버 URL을 지정하여 초기화
    /// </summary>
    /// <param name="serverUrl">WebSocket 서버 URL</param>
    public WebSocketConfig(string serverUrl)
    {
        this.serverUrl = serverUrl;
    }

    /// <summary>
    /// 주요 설정을 지정하여 초기화
    /// </summary>
    /// <param name="serverUrl">WebSocket 서버 URL</param>
    /// <param name="enableAutoReconnect">자동 재연결 활성화</param>
    /// <param name="maxReconnectAttempts">최대 재연결 시도 횟수</param>
    public WebSocketConfig(string serverUrl, bool enableAutoReconnect, int maxReconnectAttempts)
    {
        this.serverUrl = serverUrl;
        this.enableAutoReconnect = enableAutoReconnect;
        this.maxReconnectAttempts = maxReconnectAttempts;
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// 설정 유효성 검증
    /// </summary>
    /// <returns>설정이 유효한지 여부</returns>
    public bool ValidateConfiguration()
    {
        // URL 검증
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError("[WebSocketConfig] Server URL is required");
            return false;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            Debug.LogError("[WebSocketConfig] Invalid server URL format");
            return false;
        }

        if (uri.Scheme != "ws" && uri.Scheme != "wss")
        {
            Debug.LogError("[WebSocketConfig] Server URL must use ws:// or wss:// scheme");
            return false;
        }

        // 타임아웃 검증
        if (connectionTimeout < 1000)
        {
            Debug.LogWarning("[WebSocketConfig] Connection timeout is too short, minimum 1000ms recommended");
        }

        if (messageTimeout < connectionTimeout)
        {
            Debug.LogWarning("[WebSocketConfig] Message timeout should be longer than connection timeout");
        }

        // 재연결 설정 검증
        if (enableAutoReconnect && maxReconnectAttempts <= 0)
        {
            Debug.LogError("[WebSocketConfig] Max reconnect attempts must be greater than 0 when auto-reconnect is enabled");
            return false;
        }

        if (retryDelays == null || retryDelays.Length == 0)
        {
            Debug.LogWarning("[WebSocketConfig] Retry delays array is empty, using default delays");
            retryDelays = new int[] { 1000, 2000, 5000, 10000, 30000 };
        }

        // 큐 설정 검증
        if (maxMessageQueueSize < 10)
        {
            Debug.LogWarning("[WebSocketConfig] Message queue size is very small, minimum 10 recommended");
        }

        // 하트비트 설정 검증
        if (enableHeartbeat)
        {
            if (heartbeatInterval < 5000)
            {
                Debug.LogWarning("[WebSocketConfig] Heartbeat interval is very short, minimum 5000ms recommended");
            }

            if (heartbeatTimeout <= heartbeatInterval)
            {
                Debug.LogWarning("[WebSocketConfig] Heartbeat timeout should be longer than heartbeat interval");
                heartbeatTimeout = heartbeatInterval * 2;
            }
        }

        return true;
    }

    /// <summary>
    /// 개발 환경용 설정으로 업데이트
    /// </summary>
    public void SetDevelopmentMode()
    {
        enableLogging = true;
        enableDetailedLogging = true;
        connectionTimeout = 5000; // 더 짧은 타임아웃
        maxReconnectAttempts = 3; // 적은 재시도 횟수
        retryDelays = new int[] { 1000, 2000, 5000 }; // 더 짧은 지연
        
        Debug.Log("[WebSocketConfig] Development mode enabled");
    }

    /// <summary>
    /// 프로덕션 환경용 설정으로 업데이트
    /// </summary>
    public void SetProductionMode()
    {
        enableLogging = false;
        enableDetailedLogging = false;
        connectionTimeout = 10000; // 안정적인 타임아웃
        maxReconnectAttempts = 5; // 충분한 재시도 횟수
        retryDelays = new int[] { 1000, 2000, 5000, 10000, 30000 }; // 점진적 백오프
        
        Debug.Log("[WebSocketConfig] Production mode enabled");
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 재시도 지연 시간 가져오기
    /// </summary>
    /// <param name="attemptIndex">시도 인덱스 (0부터 시작)</param>
    /// <returns>지연 시간 (밀리초)</returns>
    public int GetRetryDelay(int attemptIndex)
    {
        if (retryDelays == null || retryDelays.Length == 0)
            return 5000; // 기본값 5초

        int index = Mathf.Min(attemptIndex, retryDelays.Length - 1);
        return retryDelays[index];
    }

    /// <summary>
    /// 설정을 JSON으로 직렬화
    /// </summary>
    /// <returns>JSON 문자열</returns>
    public string ToJson()
    {
        try
        {
            return JsonUtility.ToJson(this, true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketConfig] Failed to serialize to JSON: {e.Message}");
            return "{}";
        }
    }

    /// <summary>
    /// JSON에서 설정 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>WebSocketConfig 객체</returns>
    public static WebSocketConfig FromJson(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[WebSocketConfig] Empty JSON, using default configuration");
                return new WebSocketConfig();
            }

            var config = JsonUtility.FromJson<WebSocketConfig>(json);
            if (config != null && config.ValidateConfiguration())
            {
                return config;
            }

            Debug.LogError("[WebSocketConfig] Failed to deserialize or validate configuration");
            return new WebSocketConfig();
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocketConfig] Failed to deserialize from JSON: {e.Message}");
            return new WebSocketConfig();
        }
    }

    /// <summary>
    /// 설정 요약 정보
    /// </summary>
    /// <returns>요약 문자열</returns>
    public string GetSummary()
    {
        return $"WebSocketConfig: {serverUrl}, " +
               $"Timeout: {connectionTimeout}ms, " +
               $"AutoReconnect: {enableAutoReconnect}, " +
               $"MaxRetries: {maxReconnectAttempts}, " +
               $"QueueSize: {maxMessageQueueSize}, " +
               $"Heartbeat: {enableHeartbeat}({heartbeatInterval}ms)";
    }

    /// <summary>
    /// 설정 복사본 생성
    /// </summary>
    /// <returns>복사된 설정 객체</returns>
    public WebSocketConfig Clone()
    {
        var clone = new WebSocketConfig();
        clone.serverUrl = this.serverUrl;
        clone.connectionTimeout = this.connectionTimeout;
        clone.messageTimeout = this.messageTimeout;
        clone.enableLogging = this.enableLogging;
        clone.enableDetailedLogging = this.enableDetailedLogging;
        clone.enableAutoReconnect = this.enableAutoReconnect;
        clone.maxReconnectAttempts = this.maxReconnectAttempts;
        clone.retryDelays = (int[])this.retryDelays?.Clone();
        clone.maxMessageQueueSize = this.maxMessageQueueSize;
        clone.maxMessageSize = this.maxMessageSize;
        clone.enableHeartbeat = this.enableHeartbeat;
        clone.heartbeatInterval = this.heartbeatInterval;
        clone.heartbeatTimeout = this.heartbeatTimeout;
        return clone;
    }
    #endregion
}