using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 네트워크 통신 관리자
/// RESTful API 통신, 자동 재시도, 오류 처리, 로깅을 포함한 HTTP 통신 래퍼
/// </summary>
public class NetworkManager : MonoBehaviour
{
    #region Singleton
    private static NetworkManager _instance;
    public static NetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("NetworkManager");
                _instance = go.AddComponent<NetworkManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// 네트워크 연결 상태가 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnNetworkStatusChanged;
    
    /// <summary>
    /// 요청이 시작될 때 발생하는 이벤트
    /// </summary>
    public static event Action<string> OnRequestStarted;
    
    /// <summary>
    /// 요청이 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action<string, bool> OnRequestCompleted;
    #endregion

    #region Configuration
    [Header("Network Settings")]
    [SerializeField] private string baseUrl = "https://api.unitydice.com/v1";
    [SerializeField] private float defaultTimeout = 30f;
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float baseRetryDelay = 1f;
    [SerializeField] private float maxRetryDelay = 16f;
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool enableDetailedLogging = false;

    [Header("Auth Settings")]  
    [SerializeField] private string authToken = "";
    [SerializeField] private bool useAuthToken = false;
    #endregion

    #region Private Fields
    private bool _isNetworkAvailable = true;
    private readonly Dictionary<string, Coroutine> _activeRequests = new();
    private readonly Queue<NetworkRequest> _requestQueue = new();
    private readonly NetworkLogger _logger = new();
    private bool _isInitialized = false;
    #endregion

    #region Properties
    /// <summary>
    /// 네트워크 연결 가능 상태
    /// </summary>
    public bool IsNetworkAvailable => _isNetworkAvailable;
    
    /// <summary>
    /// 활성 요청 수
    /// </summary>
    public int ActiveRequestCount => _activeRequests.Count;
    
    /// <summary>
    /// 대기 중인 요청 수
    /// </summary>
    public int QueuedRequestCount => _requestQueue.Count;
    
    /// <summary>
    /// 기본 서버 URL
    /// </summary>
    public string BaseUrl => baseUrl;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeNetworkManager();
        StartCoroutine(NetworkStatusChecker());
    }

    private void OnDestroy()
    {
        StopAllRequests();
        OnNetworkStatusChanged = null;
        OnRequestStarted = null;
        OnRequestCompleted = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 네트워크 매니저 초기화
    /// </summary>
    private void InitializeNetworkManager()
    {
        _logger.Initialize(enableLogging, enableDetailedLogging);
        _logger.Log("NetworkManager initialized", LogLevel.Info);
        _isInitialized = true;
    }
    #endregion

    #region Network Status
    /// <summary>
    /// 네트워크 상태 체커 코루틴
    /// </summary>
    private IEnumerator NetworkStatusChecker()
    {
        while (true)
        {
            bool previousStatus = _isNetworkAvailable;
            _isNetworkAvailable = Application.internetReachability != NetworkReachability.NotReachable;
            
            if (previousStatus != _isNetworkAvailable)
            {
                _logger.Log($"Network status changed: {_isNetworkAvailable}", LogLevel.Info);
                OnNetworkStatusChanged?.Invoke(_isNetworkAvailable);
                
                if (_isNetworkAvailable)
                {
                    ProcessRequestQueue();
                }
            }
            
            yield return new WaitForSeconds(5f); // 5초마다 체크
        }
    }
    #endregion

    #region HTTP Methods
    /// <summary>
    /// GET 요청
    /// </summary>
    public void Get(string endpoint, Action<NetworkResponse> callback, float timeout = 0f)
    {
        var request = new NetworkRequest
        {
            Method = HttpMethod.GET,
            Endpoint = endpoint,
            Callback = callback,
            Timeout = timeout > 0 ? timeout : defaultTimeout
        };
        
        ExecuteRequest(request);
    }

    /// <summary>
    /// POST 요청
    /// </summary>
    public void Post(string endpoint, object data, Action<NetworkResponse> callback, float timeout = 0f)
    {
        var request = new NetworkRequest
        {
            Method = HttpMethod.POST,
            Endpoint = endpoint,
            Data = data,
            Callback = callback,
            Timeout = timeout > 0 ? timeout : defaultTimeout
        };
        
        ExecuteRequest(request);
    }

    /// <summary>
    /// PUT 요청
    /// </summary>
    public void Put(string endpoint, object data, Action<NetworkResponse> callback, float timeout = 0f)
    {
        var request = new NetworkRequest
        {
            Method = HttpMethod.PUT,
            Endpoint = endpoint,
            Data = data,
            Callback = callback,
            Timeout = timeout > 0 ? timeout : defaultTimeout
        };
        
        ExecuteRequest(request);
    }

    /// <summary>
    /// DELETE 요청
    /// </summary>
    public void Delete(string endpoint, Action<NetworkResponse> callback, float timeout = 0f)
    {
        var request = new NetworkRequest
        {
            Method = HttpMethod.DELETE,
            Endpoint = endpoint,
            Callback = callback,
            Timeout = timeout > 0 ? timeout : defaultTimeout
        };
        
        ExecuteRequest(request);
    }

    /// <summary>
    /// PATCH 요청
    /// </summary>
    public void Patch(string endpoint, object data, Action<NetworkResponse> callback, float timeout = 0f)
    {
        var request = new NetworkRequest
        {
            Method = HttpMethod.PATCH,
            Endpoint = endpoint,
            Data = data,
            Callback = callback,
            Timeout = timeout > 0 ? timeout : defaultTimeout
        };
        
        ExecuteRequest(request);
    }
    #endregion

    #region Request Execution
    /// <summary>
    /// 요청 실행
    /// </summary>
    private void ExecuteRequest(NetworkRequest request)
    {
        if (!_isInitialized)
        {
            _logger.Log("NetworkManager not initialized", LogLevel.Error);
            request.Callback?.Invoke(new NetworkResponse { IsSuccess = false, Error = "NetworkManager not initialized" });
            return;
        }

        if (!_isNetworkAvailable)
        {
            _logger.Log($"Network unavailable, queueing request: {request.Endpoint}", LogLevel.Warning);
            _requestQueue.Enqueue(request);
            return;
        }

        string requestId = Guid.NewGuid().ToString();
        request.Id = requestId;
        
        _logger.Log($"Starting request [{requestId}]: {request.Method} {request.Endpoint}", LogLevel.Info);
        OnRequestStarted?.Invoke(requestId);
        
        Coroutine requestCoroutine = StartCoroutine(ExecuteRequestCoroutine(request));
        _activeRequests[requestId] = requestCoroutine;
    }

    /// <summary>
    /// 요청 실행 코루틴
    /// </summary>
    private IEnumerator ExecuteRequestCoroutine(NetworkRequest request)
    {
        int attemptCount = 0;
        float retryDelay = baseRetryDelay;

        while (attemptCount <= maxRetryAttempts)
        {
            attemptCount++;
            
            if (attemptCount > 1)
            {
                _logger.Log($"Retry attempt {attemptCount}/{maxRetryAttempts} for [{request.Id}]", LogLevel.Info);
                yield return new WaitForSeconds(retryDelay);
                retryDelay = Mathf.Min(retryDelay * 2f, maxRetryDelay); // 지수 백오프
            }

            UnityWebRequest webRequest = CreateWebRequest(request);
            
            if (webRequest == null)
            {
                CompleteRequest(request, new NetworkResponse { IsSuccess = false, Error = "Failed to create web request" });
                yield break;
            }

            yield return webRequest.SendWebRequest();

            NetworkResponse response = ProcessWebResponse(webRequest, request);
            
            // 성공하거나 재시도 불가능한 오류인 경우 완료
            if (response.IsSuccess || !ShouldRetry(webRequest.result, response.StatusCode))
            {
                CompleteRequest(request, response);
                yield break;
            }

            webRequest.Dispose();
        }

        // 최대 재시도 횟수 초과
        CompleteRequest(request, new NetworkResponse 
        { 
            IsSuccess = false, 
            Error = $"Max retry attempts ({maxRetryAttempts}) exceeded",
            StatusCode = 0
        });
    }

    /// <summary>
    /// UnityWebRequest 생성
    /// </summary>
    private UnityWebRequest CreateWebRequest(NetworkRequest request)
    {
        try
        {
            string fullUrl = GetFullUrl(request.Endpoint);
            UnityWebRequest webRequest;

            switch (request.Method)
            {
                case HttpMethod.GET:
                    webRequest = UnityWebRequest.Get(fullUrl);
                    break;
                    
                case HttpMethod.POST:
                    webRequest = CreateRequestWithBody(fullUrl, "POST", request.Data);
                    break;
                    
                case HttpMethod.PUT:
                    webRequest = CreateRequestWithBody(fullUrl, "PUT", request.Data);
                    break;
                    
                case HttpMethod.DELETE:
                    webRequest = UnityWebRequest.Delete(fullUrl);
                    break;
                    
                case HttpMethod.PATCH:
                    webRequest = CreateRequestWithBody(fullUrl, "PATCH", request.Data);
                    break;
                    
                default:
                    _logger.Log($"Unsupported HTTP method: {request.Method}", LogLevel.Error);
                    return null;
            }

            webRequest.timeout = (int)request.Timeout;
            SetRequestHeaders(webRequest);
            
            return webRequest;
        }
        catch (Exception e)
        {
            _logger.Log($"Error creating web request: {e.Message}", LogLevel.Error);
            return null;
        }
    }

    /// <summary>
    /// Body가 있는 요청 생성
    /// </summary>
    private UnityWebRequest CreateRequestWithBody(string url, string method, object data)
    {
        UnityWebRequest request = new(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        
        if (data != null)
        {
            string jsonData = JsonUtility.ToJson(data);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
            
            if (enableDetailedLogging)
            {
                _logger.Log($"Request body: {jsonData}", LogLevel.Debug);
            }
        }
        
        return request;
    }

    /// <summary>
    /// 요청 헤더 설정
    /// </summary>
    private void SetRequestHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("User-Agent", $"UnityDice/{Application.version}");
        
        if (useAuthToken && !string.IsNullOrEmpty(authToken))
        {
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
        }
    }

    /// <summary>
    /// Web 응답 처리
    /// </summary>
    private NetworkResponse ProcessWebResponse(UnityWebRequest webRequest, NetworkRequest request)
    {
        var response = new NetworkResponse
        {
            StatusCode = webRequest.responseCode,
            RawData = webRequest.downloadHandler?.text ?? "",
            Headers = []
        };

        // 헤더 복사
        foreach (var header in webRequest.GetResponseHeaders() ?? new Dictionary<string, string>())
        {
            response.Headers[header.Key] = header.Value;
        }

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            response.IsSuccess = true;
            _logger.Log($"Request [{request.Id}] completed successfully", LogLevel.Info);
            
            if (enableDetailedLogging)
            {
                _logger.Log($"Response: {response.RawData}", LogLevel.Debug);
            }
        }
        else
        {
            response.IsSuccess = false;
            response.Error = webRequest.error ?? "Unknown error";
            
            _logger.Log($"Request [{request.Id}] failed: {response.Error} (Status: {response.StatusCode})", LogLevel.Error);
        }

        webRequest.Dispose();
        return response;
    }

    /// <summary>
    /// 재시도 여부 판단
    /// </summary>
    private bool ShouldRetry(UnityWebRequest.Result result, long statusCode)
    {
        // 네트워크 오류나 서버 오류의 경우 재시도
        return result switch
        {
            UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError => true,
            UnityWebRequest.Result.ProtocolError => statusCode >= 500 && statusCode < 600,
            _ => false
        };
    }

    /// <summary>
    /// 요청 완료 처리
    /// </summary>
    private void CompleteRequest(NetworkRequest request, NetworkResponse response)
    {
        _activeRequests.Remove(request.Id);
        OnRequestCompleted?.Invoke(request.Id, response.IsSuccess);
        
        try
        {
            request.Callback?.Invoke(response);
        }
        catch (Exception e)
        {
            _logger.Log($"Error in request callback: {e.Message}", LogLevel.Error);
        }
    }
    #endregion

    #region Request Queue
    /// <summary>
    /// 요청 큐 처리
    /// </summary>
    private void ProcessRequestQueue()
    {
        while (_requestQueue.Count > 0 && _isNetworkAvailable)
        {
            NetworkRequest request = _requestQueue.Dequeue();
            ExecuteRequest(request);
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 전체 URL 생성
    /// </summary>
    private string GetFullUrl(string endpoint)
    {
        if (endpoint.StartsWith("http://") || endpoint.StartsWith("https://"))
        {
            return endpoint;
        }
        
        string cleanEndpoint = endpoint.TrimStart('/');
        return $"{baseUrl.TrimEnd('/')}/{cleanEndpoint}";
    }

    /// <summary>
    /// 모든 요청 중지
    /// </summary>
    public void StopAllRequests()
    {
        foreach (var request in _activeRequests.Values)
        {
            if (request != null)
            {
                StopCoroutine(request);
            }
        }
        
        _activeRequests.Clear();
        _requestQueue.Clear();
        _logger.Log("All requests stopped", LogLevel.Info);
    }

    /// <summary>
    /// 특정 요청 취소
    /// </summary>
    public void CancelRequest(string requestId)
    {
        if (_activeRequests.TryGetValue(requestId, out Coroutine request))
        {
            StopCoroutine(request);
            _activeRequests.Remove(requestId);
            _logger.Log($"Request [{requestId}] cancelled", LogLevel.Info);
        }
    }
    #endregion

    #region Configuration
    /// <summary>
    /// 기본 URL 설정
    /// </summary>
    public void SetBaseUrl(string url)
    {
        baseUrl = url;
        _logger.Log($"Base URL set to: {baseUrl}", LogLevel.Info);
    }

    /// <summary>
    /// 인증 토큰 설정
    /// </summary>
    public void SetAuthToken(string token)
    {
        authToken = token;
        useAuthToken = !string.IsNullOrEmpty(token);
        _logger.Log($"Auth token {(string.IsNullOrEmpty(token) ? "cleared" : "set")}", LogLevel.Info);
    }

    /// <summary>
    /// 로깅 설정
    /// </summary>
    public void SetLogging(bool enabled, bool detailed = false)
    {
        enableLogging = enabled;
        enableDetailedLogging = detailed;
        _logger.Initialize(enableLogging, enableDetailedLogging);
    }
    #endregion
}

#region Data Structures
/// <summary>
/// HTTP 메서드 열거형
/// </summary>
public enum HttpMethod
{
    GET,
    POST,
    PUT,
    DELETE,
    PATCH
}

/// <summary>
/// 네트워크 요청 데이터
/// </summary>
public class NetworkRequest
{
    public string Id { get; set; }
    public HttpMethod Method { get; set; }
    public string Endpoint { get; set; }
    public object Data { get; set; }
    public Action<NetworkResponse> Callback { get; set; }
    public float Timeout { get; set; }
}

/// <summary>
/// 네트워크 응답 데이터
/// </summary>
public class NetworkResponse
{
    public bool IsSuccess { get; set; }
    public long StatusCode { get; set; }
    public string RawData { get; set; } = "";
    public string Error { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    
    /// <summary>
    /// JSON 데이터를 객체로 변환
    /// </summary>
    public T GetData<T>() where T : class
    {
        if (!IsSuccess || string.IsNullOrEmpty(RawData))
            return null;
            
        try
        {
            return JsonUtility.FromJson<T>(RawData);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse response data: {e.Message}");
            return null;
        }
    }
}

/// <summary>
/// 네트워크 로거
/// </summary>
public class NetworkLogger
{
    private bool _loggingEnabled = true;
    private bool _detailedLogging = false;
    
    public void Initialize(bool enabled, bool detailed)
    {
        _loggingEnabled = enabled;
        _detailedLogging = detailed;
    }
    
    public void Log(string message, LogLevel level)
    {
        if (!_loggingEnabled) return;
        
        if (level == LogLevel.Debug && !_detailedLogging) return;
        
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logMessage = $"[{timestamp}] [Network] {message}";
        
        switch (level)
        {
            case LogLevel.Info:
                Debug.Log(logMessage);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(logMessage);
                break;
            case LogLevel.Error:
                Debug.LogError(logMessage);
                break;
            case LogLevel.Debug:
                Debug.Log($"<color=cyan>{logMessage}</color>");
                break;
        }
    }
}

/// <summary>
/// 로그 레벨
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
#endregion