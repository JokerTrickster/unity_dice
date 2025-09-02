using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우편함 네트워크 핸들러
/// NetworkManager를 활용한 우편함 관련 HTTP API 통신 담당
/// 기존 NetworkManager 인프라를 완전 재사용하여 서버와의 모든 우편함 통신 처리
/// </summary>
public class MailboxNetworkHandler : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 메시지 로드 완료 이벤트
    /// </summary>
    public static event Action<MailboxData, bool> OnMessagesLoaded; // data, isSuccess
    
    /// <summary>
    /// 메시지 읽음 처리 완료 이벤트
    /// </summary>
    public static event Action<string, bool> OnMessageReadStatusUpdated; // messageId, isSuccess
    
    /// <summary>
    /// 메시지 삭제 완료 이벤트
    /// </summary>
    public static event Action<string, bool> OnMessageDeleted; // messageId, isSuccess
    
    /// <summary>
    /// 첨부 파일 수령 완료 이벤트
    /// </summary>
    public static event Action<string, bool, object> OnAttachmentClaimed; // messageId, isSuccess, result
    
    /// <summary>
    /// 네트워크 오류 발생 이벤트
    /// </summary>
    public static event Action<string, string> OnNetworkError; // operation, errorMessage
    #endregion

    #region Singleton
    private static MailboxNetworkHandler _instance;
    public static MailboxNetworkHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MailboxNetworkHandler");
                _instance = go.AddComponent<MailboxNetworkHandler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private bool _isInitialized = false;
    private NetworkManager _networkManager;
    private readonly Dictionary<string, Coroutine> _activeRequests = new();
    
    // API 엔드포인트 상수
    private const string API_MESSAGES_ENDPOINT = "/api/mailbox/messages";
    private const string API_READ_ENDPOINT = "/api/mailbox/read";
    private const string API_DELETE_ENDPOINT = "/api/mailbox/messages";
    private const string API_CLAIM_ENDPOINT = "/api/mailbox/claim";
    
    // 타임아웃 설정 (3초 이내 로딩 성능 목표)
    private const float LOAD_MESSAGES_TIMEOUT = 3f;
    private const float STANDARD_REQUEST_TIMEOUT = 5f;
    
    // 재시도 설정
    private const int MAX_RETRY_COUNT = 3;
    private const float RETRY_BASE_DELAY = 0.5f;
    #endregion

    #region Properties
    /// <summary>
    /// 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 활성 요청 수
    /// </summary>
    public int ActiveRequestCount => _activeRequests.Count;
    
    /// <summary>
    /// 네트워크 가용 상태
    /// </summary>
    public bool IsNetworkAvailable => _networkManager?.IsNetworkAvailable ?? false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        CancelAllRequests();
        ClearEvents();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 네트워크 핸들러 초기화
    /// </summary>
    private void Initialize()
    {
        try
        {
            _networkManager = NetworkManager.Instance;
            if (_networkManager == null)
            {
                Debug.LogError("[MailboxNetworkHandler] NetworkManager instance not found");
                return;
            }

            // NetworkManager 이벤트 구독
            NetworkManager.OnNetworkStatusChanged += OnNetworkStatusChanged;
            NetworkManager.OnRequestStarted += OnRequestStarted;
            NetworkManager.OnRequestCompleted += OnRequestCompleted;

            _isInitialized = true;
            Debug.Log("[MailboxNetworkHandler] Initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxNetworkHandler] Initialization failed: {e.Message}");
        }
    }
    #endregion

    #region Public API Methods
    /// <summary>
    /// 서버에서 메시지 목록 로드
    /// </summary>
    /// <param name="forceRefresh">캐시 무시 강제 새로고침</param>
    public void LoadMessages(bool forceRefresh = false)
    {
        if (!ValidateNetworkState("LoadMessages"))
            return;

        string requestId = $"load_messages_{DateTime.UtcNow.Ticks}";
        StartCoroutine(LoadMessagesCoroutine(requestId, forceRefresh));
    }

    /// <summary>
    /// 메시지를 읽음 상태로 표시
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    public void MarkMessageAsRead(string messageId)
    {
        if (!ValidateNetworkState("MarkAsRead") || string.IsNullOrEmpty(messageId))
        {
            OnMessageReadStatusUpdated?.Invoke(messageId, false);
            return;
        }

        string requestId = $"mark_read_{messageId}_{DateTime.UtcNow.Ticks}";
        StartCoroutine(MarkAsReadCoroutine(requestId, messageId));
    }

    /// <summary>
    /// 메시지 삭제
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    public void DeleteMessage(string messageId)
    {
        if (!ValidateNetworkState("DeleteMessage") || string.IsNullOrEmpty(messageId))
        {
            OnMessageDeleted?.Invoke(messageId, false);
            return;
        }

        string requestId = $"delete_message_{messageId}_{DateTime.UtcNow.Ticks}";
        StartCoroutine(DeleteMessageCoroutine(requestId, messageId));
    }

    /// <summary>
    /// 첨부 파일(선물) 수령
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    /// <param name="giftId">선물 ID</param>
    public void ClaimAttachment(string messageId, string giftId = null)
    {
        if (!ValidateNetworkState("ClaimAttachment") || string.IsNullOrEmpty(messageId))
        {
            OnAttachmentClaimed?.Invoke(messageId, false, null);
            return;
        }

        string requestId = $"claim_attachment_{messageId}_{DateTime.UtcNow.Ticks}";
        StartCoroutine(ClaimAttachmentCoroutine(requestId, messageId, giftId));
    }

    /// <summary>
    /// 모든 활성 요청 취소
    /// </summary>
    public void CancelAllRequests()
    {
        foreach (var request in _activeRequests.Values)
        {
            if (request != null)
            {
                StopCoroutine(request);
            }
        }
        _activeRequests.Clear();
        Debug.Log("[MailboxNetworkHandler] All requests cancelled");
    }

    /// <summary>
    /// 특정 요청 취소
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    public void CancelRequest(string requestId)
    {
        if (_activeRequests.TryGetValue(requestId, out Coroutine request))
        {
            StopCoroutine(request);
            _activeRequests.Remove(requestId);
            Debug.Log($"[MailboxNetworkHandler] Request cancelled: {requestId}");
        }
    }
    #endregion

    #region Network Request Coroutines
    /// <summary>
    /// 메시지 로드 코루틴
    /// </summary>
    private IEnumerator LoadMessagesCoroutine(string requestId, bool forceRefresh)
    {
        _activeRequests[requestId] = null; // 플래그로 사용
        
        Debug.Log($"[MailboxNetworkHandler] Starting LoadMessages request: {requestId}");
        
        // 쿼리 파라미터 추가
        string endpoint = API_MESSAGES_ENDPOINT;
        if (forceRefresh)
        {
            endpoint += "?force_refresh=true";
        }

        // 재시도 로직 포함
        int retryCount = 0;
        bool success = false;
        MailboxData resultData = null;

        while (retryCount <= MAX_RETRY_COUNT && !success)
        {
            if (retryCount > 0)
            {
                float delay = RETRY_BASE_DELAY * Mathf.Pow(2, retryCount - 1);
                Debug.Log($"[MailboxNetworkHandler] Retry {retryCount}/{MAX_RETRY_COUNT} after {delay}s");
                yield return new WaitForSeconds(delay);
            }

            bool requestCompleted = false;
            string errorMessage = "";

            _networkManager.Get(endpoint, (response) =>
            {
                requestCompleted = true;
                
                if (response.IsSuccess)
                {
                    try
                    {
                        // MailboxData로 직접 파싱
                        resultData = response.GetData<MailboxData>();
                        if (resultData != null && resultData.IsValid())
                        {
                            success = true;
                            Debug.Log($"[MailboxNetworkHandler] LoadMessages successful: {resultData.messages?.Count ?? 0} messages");
                        }
                        else
                        {
                            errorMessage = "Invalid server response data";
                            Debug.LogWarning($"[MailboxNetworkHandler] Invalid data from server");
                        }
                    }
                    catch (Exception e)
                    {
                        errorMessage = $"Failed to parse response: {e.Message}";
                        Debug.LogError($"[MailboxNetworkHandler] Parse error: {e.Message}");
                    }
                }
                else
                {
                    errorMessage = response.Error;
                    Debug.LogError($"[MailboxNetworkHandler] LoadMessages failed: {response.Error}");
                }
            }, LOAD_MESSAGES_TIMEOUT);

            // 응답 대기
            yield return new WaitUntil(() => requestCompleted);
            
            retryCount++;
        }

        // 요청 완료 처리
        _activeRequests.Remove(requestId);
        
        if (success && resultData != null)
        {
            OnMessagesLoaded?.Invoke(resultData, true);
        }
        else
        {
            OnMessagesLoaded?.Invoke(null, false);
            OnNetworkError?.Invoke("LoadMessages", $"Failed after {MAX_RETRY_COUNT} retries: {errorMessage}");
        }
    }

    /// <summary>
    /// 읽음 처리 코루틴
    /// </summary>
    private IEnumerator MarkAsReadCoroutine(string requestId, string messageId)
    {
        _activeRequests[requestId] = null;
        
        Debug.Log($"[MailboxNetworkHandler] Marking message as read: {messageId}");
        
        var requestData = new { messageId = messageId };
        bool requestCompleted = false;
        bool success = false;

        _networkManager.Post(API_READ_ENDPOINT, requestData, (response) =>
        {
            requestCompleted = true;
            success = response.IsSuccess;
            
            if (!success)
            {
                Debug.LogError($"[MailboxNetworkHandler] MarkAsRead failed: {response.Error}");
                OnNetworkError?.Invoke("MarkAsRead", response.Error);
            }
            else
            {
                Debug.Log($"[MailboxNetworkHandler] Message marked as read successfully: {messageId}");
            }
        }, STANDARD_REQUEST_TIMEOUT);

        yield return new WaitUntil(() => requestCompleted);
        
        _activeRequests.Remove(requestId);
        OnMessageReadStatusUpdated?.Invoke(messageId, success);
    }

    /// <summary>
    /// 메시지 삭제 코루틴
    /// </summary>
    private IEnumerator DeleteMessageCoroutine(string requestId, string messageId)
    {
        _activeRequests[requestId] = null;
        
        Debug.Log($"[MailboxNetworkHandler] Deleting message: {messageId}");
        
        bool requestCompleted = false;
        bool success = false;

        _networkManager.Delete($"{API_DELETE_ENDPOINT}/{messageId}", (response) =>
        {
            requestCompleted = true;
            success = response.IsSuccess;
            
            if (!success)
            {
                Debug.LogError($"[MailboxNetworkHandler] DeleteMessage failed: {response.Error}");
                OnNetworkError?.Invoke("DeleteMessage", response.Error);
            }
            else
            {
                Debug.Log($"[MailboxNetworkHandler] Message deleted successfully: {messageId}");
            }
        }, STANDARD_REQUEST_TIMEOUT);

        yield return new WaitUntil(() => requestCompleted);
        
        _activeRequests.Remove(requestId);
        OnMessageDeleted?.Invoke(messageId, success);
    }

    /// <summary>
    /// 첨부 파일 수령 코루틴
    /// </summary>
    private IEnumerator ClaimAttachmentCoroutine(string requestId, string messageId, string giftId)
    {
        _activeRequests[requestId] = null;
        
        Debug.Log($"[MailboxNetworkHandler] Claiming attachment for message: {messageId}");
        
        // 요청 데이터 구성
        var requestData = new { messageId = messageId };
        if (!string.IsNullOrEmpty(giftId))
        {
            requestData = new { messageId = messageId, giftId = giftId };
        }

        bool requestCompleted = false;
        bool success = false;
        object result = null;

        _networkManager.Post(API_CLAIM_ENDPOINT, requestData, (response) =>
        {
            requestCompleted = true;
            success = response.IsSuccess;
            
            if (success)
            {
                try
                {
                    // 응답 데이터 파싱 (예: 에너지 수량, 아이템 정보 등)
                    if (!string.IsNullOrEmpty(response.RawData))
                    {
                        result = JsonUtility.FromJson<ClaimResult>(response.RawData);
                    }
                    Debug.Log($"[MailboxNetworkHandler] Attachment claimed successfully: {messageId}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MailboxNetworkHandler] Failed to parse claim result: {e.Message}");
                    result = new ClaimResult { success = true, message = "Claimed successfully" };
                }
            }
            else
            {
                Debug.LogError($"[MailboxNetworkHandler] ClaimAttachment failed: {response.Error}");
                OnNetworkError?.Invoke("ClaimAttachment", response.Error);
            }
        }, STANDARD_REQUEST_TIMEOUT);

        yield return new WaitUntil(() => requestCompleted);
        
        _activeRequests.Remove(requestId);
        OnAttachmentClaimed?.Invoke(messageId, success, result);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 네트워크 상태 검증
    /// </summary>
    private bool ValidateNetworkState(string operation)
    {
        if (!_isInitialized)
        {
            Debug.LogError($"[MailboxNetworkHandler] {operation}: Handler not initialized");
            OnNetworkError?.Invoke(operation, "Network handler not initialized");
            return false;
        }

        if (_networkManager == null)
        {
            Debug.LogError($"[MailboxNetworkHandler] {operation}: NetworkManager not available");
            OnNetworkError?.Invoke(operation, "NetworkManager not available");
            return false;
        }

        if (!_networkManager.IsNetworkAvailable)
        {
            Debug.LogWarning($"[MailboxNetworkHandler] {operation}: Network not available");
            OnNetworkError?.Invoke(operation, "Network connection not available");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void ClearEvents()
    {
        OnMessagesLoaded = null;
        OnMessageReadStatusUpdated = null;
        OnMessageDeleted = null;
        OnAttachmentClaimed = null;
        OnNetworkError = null;

        // NetworkManager 이벤트 구독 해제
        if (_networkManager != null)
        {
            NetworkManager.OnNetworkStatusChanged -= OnNetworkStatusChanged;
            NetworkManager.OnRequestStarted -= OnRequestStarted;
            NetworkManager.OnRequestCompleted -= OnRequestCompleted;
        }
    }
    #endregion

    #region NetworkManager Event Handlers
    /// <summary>
    /// 네트워크 상태 변경 처리
    /// </summary>
    private void OnNetworkStatusChanged(bool isConnected)
    {
        Debug.Log($"[MailboxNetworkHandler] Network status changed: {isConnected}");
        
        if (!isConnected)
        {
            // 오프라인 상태에서는 모든 활성 요청 취소
            CancelAllRequests();
        }
    }

    /// <summary>
    /// 요청 시작 처리
    /// </summary>
    private void OnRequestStarted(string requestId)
    {
        // NetworkManager의 요청 시작 로깅
        if (Debug.isDebugBuild)
        {
            Debug.Log($"[MailboxNetworkHandler] Network request started: {requestId}");
        }
    }

    /// <summary>
    /// 요청 완료 처리
    /// </summary>
    private void OnRequestCompleted(string requestId, bool success)
    {
        // NetworkManager의 요청 완료 로깅
        if (Debug.isDebugBuild)
        {
            Debug.Log($"[MailboxNetworkHandler] Network request completed: {requestId}, success: {success}");
        }
    }
    #endregion
}

#region Data Transfer Objects
/// <summary>
/// 첨부 파일 수령 결과
/// </summary>
[Serializable]
public class ClaimResult
{
    public bool success;
    public string message;
    public int energyReceived;
    public string itemReceived;
    public Dictionary<string, object> additionalData;

    public ClaimResult()
    {
        success = false;
        message = "";
        energyReceived = 0;
        itemReceived = "";
        additionalData = new Dictionary<string, object>();
    }
}
#endregion