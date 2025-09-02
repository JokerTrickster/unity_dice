using System;
using UnityEngine;

/// <summary>
/// WebSocket 클라이언트 설정 관리
/// Unity Inspector에서 설정 가능하며 런타임에 동적 변경도 지원
/// </summary>
[CreateAssetMenu(fileName = "WebSocketConfig", menuName = "Config/WebSocket Config")]
[Serializable]
public class WebSocketConfig : ScriptableObject
{
    #region Connection Settings
    [Header("Connection Settings")]
    [SerializeField] private string serverUrl = "wss://game-api.unitydice.com/matching";
    [SerializeField] private int connectionTimeout = 10000; // milliseconds
    [SerializeField] private bool enableSsl = true;
    
    [Header("Reconnection Settings")]
    [SerializeField] private int maxReconnectAttempts = 5;
    [SerializeField] private int[] retryDelays = { 1000, 2000, 5000, 10000, 30000 }; // milliseconds
    [SerializeField] private bool enableAutoReconnect = true;
    
    [Header("Message Settings")]
    [SerializeField] private int maxMessageQueueSize = 100;
    [SerializeField] private int messageTimeout = 5000; // milliseconds
    [SerializeField] private int maxMessageSize = 1048576; // 1MB
    
    [Header("Heartbeat Settings")]
    [SerializeField] private bool enableHeartbeat = true;
    [SerializeField] private int heartbeatInterval = 30000; // milliseconds
    [SerializeField] private int heartbeatTimeout = 10000; // milliseconds
    
    [Header("Logging Settings")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool enableDetailedLogging = false;
    #endregion

    #region Properties
    /// <summary>WebSocket 서버 URL</summary>
    public string ServerUrl => serverUrl;
    
    /// <summary>연결 타임아웃 (밀리초)</summary>
    public int ConnectionTimeout => connectionTimeout;
    
    /// <summary>SSL 사용 여부</summary>
    public bool EnableSsl => enableSsl;
    
    /// <summary>최대 재연결 시도 횟수</summary>
    public int MaxReconnectAttempts => maxReconnectAttempts;
    
    /// <summary>재시도 지연 시간 배열 (밀리초)</summary>
    public int[] RetryDelays => retryDelays;
    
    /// <summary>자동 재연결 사용 여부</summary>
    public bool EnableAutoReconnect => enableAutoReconnect;
    
    /// <summary>최대 메시지 큐 크기</summary>
    public int MaxMessageQueueSize => maxMessageQueueSize;
    
    /// <summary>메시지 타임아웃 (밀리초)</summary>
    public int MessageTimeout => messageTimeout;
    
    /// <summary>최대 메시지 크기 (바이트)</summary>
    public int MaxMessageSize => maxMessageSize;
    
    /// <summary>하트비트 사용 여부</summary>
    public bool EnableHeartbeat => enableHeartbeat;
    
    /// <summary>하트비트 간격 (밀리초)</summary>
    public int HeartbeatInterval => heartbeatInterval;
    
    /// <summary>하트비트 타임아웃 (밀리초)</summary>
    public int HeartbeatTimeout => heartbeatTimeout;
    
    /// <summary>로깅 사용 여부</summary>
    public bool EnableLogging => enableLogging;
    
    /// <summary>상세 로깅 사용 여부</summary>
    public bool EnableDetailedLogging => enableDetailedLogging;
    #endregion

    #region Runtime Configuration
    /// <summary>
    /// 런타임에 서버 URL 변경
    /// </summary>
    public void SetServerUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[WebSocketConfig] Server URL cannot be null or empty");
            return;
        }
        
        if (!url.StartsWith("ws://") && !url.StartsWith("wss://"))
        {
            Debug.LogError("[WebSocketConfig] Invalid WebSocket URL format. Must start with ws:// or wss://");
            return;
        }
        
        serverUrl = url;
        Debug.Log($"[WebSocketConfig] Server URL updated to: {serverUrl}");
    }
    
    /// <summary>
    /// 런타임에 연결 타임아웃 변경
    /// </summary>
    public void SetConnectionTimeout(int timeoutMs)
    {
        if (timeoutMs <= 0)
        {
            Debug.LogError("[WebSocketConfig] Connection timeout must be greater than 0");
            return;
        }
        
        connectionTimeout = timeoutMs;
        Debug.Log($"[WebSocketConfig] Connection timeout updated to: {connectionTimeout}ms");
    }
    
    /// <summary>
    /// 런타임에 재연결 설정 변경
    /// </summary>
    public void SetReconnectionSettings(int maxAttempts, int[] delays)
    {
        if (maxAttempts < 0)
        {
            Debug.LogError("[WebSocketConfig] Max reconnect attempts cannot be negative");
            return;
        }
        
        if (delays == null || delays.Length == 0)
        {
            Debug.LogError("[WebSocketConfig] Retry delays array cannot be null or empty");
            return;
        }
        
        maxReconnectAttempts = maxAttempts;
        retryDelays = delays;
        Debug.Log($"[WebSocketConfig] Reconnection settings updated: {maxAttempts} attempts, {delays.Length} delay steps");
    }
    
    /// <summary>
    /// 런타임에 로깅 설정 변경
    /// </summary>
    public void SetLoggingSettings(bool enabled, bool detailed = false)
    {
        enableLogging = enabled;
        enableDetailedLogging = detailed;
        Debug.Log($"[WebSocketConfig] Logging settings updated: enabled={enabled}, detailed={detailed}");
    }
    #endregion

    #region Validation
    /// <summary>
    /// 설정 유효성 검사
    /// </summary>
    public bool ValidateConfiguration()
    {
        bool isValid = true;
        
        // URL 검증
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError("[WebSocketConfig] Server URL is not set");
            isValid = false;
        }
        else if (!serverUrl.StartsWith("ws://") && !serverUrl.StartsWith("wss://"))
        {
            Debug.LogError("[WebSocketConfig] Invalid WebSocket URL format");
            isValid = false;
        }
        
        // 타임아웃 검증
        if (connectionTimeout <= 0)
        {
            Debug.LogError("[WebSocketConfig] Connection timeout must be greater than 0");
            isValid = false;
        }
        
        // 재연결 설정 검증
        if (maxReconnectAttempts < 0)
        {
            Debug.LogError("[WebSocketConfig] Max reconnect attempts cannot be negative");
            isValid = false;
        }
        
        if (retryDelays == null || retryDelays.Length == 0)
        {
            Debug.LogError("[WebSocketConfig] Retry delays array cannot be null or empty");
            isValid = false;
        }
        else
        {
            for (int i = 0; i < retryDelays.Length; i++)
            {
                if (retryDelays[i] <= 0)
                {
                    Debug.LogError($"[WebSocketConfig] Retry delay at index {i} must be greater than 0");
                    isValid = false;
                }
            }
        }
        
        // 메시지 설정 검증
        if (maxMessageQueueSize <= 0)
        {
            Debug.LogError("[WebSocketConfig] Max message queue size must be greater than 0");
            isValid = false;
        }
        
        if (messageTimeout <= 0)
        {
            Debug.LogError("[WebSocketConfig] Message timeout must be greater than 0");
            isValid = false;
        }
        
        if (maxMessageSize <= 0)
        {
            Debug.LogError("[WebSocketConfig] Max message size must be greater than 0");
            isValid = false;
        }
        
        // 하트비트 설정 검증
        if (enableHeartbeat)
        {
            if (heartbeatInterval <= 0)
            {
                Debug.LogError("[WebSocketConfig] Heartbeat interval must be greater than 0");
                isValid = false;
            }
            
            if (heartbeatTimeout <= 0)
            {
                Debug.LogError("[WebSocketConfig] Heartbeat timeout must be greater than 0");
                isValid = false;
            }
            
            if (heartbeatTimeout >= heartbeatInterval)
            {
                Debug.LogError("[WebSocketConfig] Heartbeat timeout should be less than heartbeat interval");
                isValid = false;
            }
        }
        
        if (isValid)
        {
            Debug.Log("[WebSocketConfig] Configuration validation passed");
        }
        
        return isValid;
    }
    
    /// <summary>
    /// 재시도 지연 시간 가져오기 (안전한 인덱스 처리)
    /// </summary>
    public int GetRetryDelay(int attemptIndex)
    {
        if (retryDelays == null || retryDelays.Length == 0)
        {
            return 5000; // 기본값 5초
        }
        
        // 마지막 지연 시간을 반복 사용
        int index = Math.Min(attemptIndex, retryDelays.Length - 1);
        return retryDelays[index];
    }
    #endregion

    #region Unity Events
    private void OnValidate()
    {
        // Inspector에서 값 변경 시 실시간 유효성 검사
        ValidateConfiguration();
    }
    #endregion
}