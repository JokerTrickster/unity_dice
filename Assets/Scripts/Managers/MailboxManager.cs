using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 우편함 관리자 Singleton 클래스
/// 우편함 시스템의 핵심 로직 및 데이터 관리를 담당
/// NetworkManager 활용, 로컬 캐싱, 읽음/안읽음 상태 관리
/// </summary>
public class MailboxManager : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 우편함 데이터가 로드될 때 발생
    /// </summary>
    public static event Action<MailboxData> OnMailboxLoaded;
    
    /// <summary>
    /// 우편함 데이터가 업데이트될 때 발생
    /// </summary>
    public static event Action<MailboxData> OnMailboxUpdated;
    
    /// <summary>
    /// 읽지 않은 메시지 수가 변경될 때 발생
    /// </summary>
    public static event Action<int> OnUnreadCountChanged;
    
    /// <summary>
    /// 새 메시지가 도착했을 때 발생
    /// </summary>
    public static event Action<MailboxMessage> OnNewMessageReceived;
    
    /// <summary>
    /// 메시지가 읽음 상태로 변경될 때 발생
    /// </summary>
    public static event Action<string> OnMessageRead;
    
    /// <summary>
    /// 메시지가 삭제될 때 발생
    /// </summary>
    public static event Action<string> OnMessageDeleted;
    
    /// <summary>
    /// 동기화 상태 변경 시 발생
    /// </summary>
    public static event Action<bool, string> OnSyncStatusChanged; // success, message
    
    /// <summary>
    /// 에러 발생 시 발생
    /// </summary>
    public static event Action<string> OnError;
    #endregion
    
    #region Singleton
    private static MailboxManager _instance;
    public static MailboxManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MailboxManager");
                _instance = go.AddComponent<MailboxManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion
    
    #region Private Fields
    private MailboxData _mailboxData;
    private bool _isInitialized = false;
    private bool _isLoading = false;
    private bool _isSyncing = false;
    private string _currentUserId;
    
    // 자동 동기화
    private Coroutine _autoSyncCoroutine;
    private const float AUTO_SYNC_INTERVAL = 300f; // 5분
    
    // 재시도 로직
    private const int MAX_RETRY_COUNT = 3;
    private const float RETRY_DELAY_BASE = 2f;
    
    // API 엔드포인트
    private const string API_MESSAGES_ENDPOINT = "/api/mailbox/messages";
    private const string API_READ_ENDPOINT = "/api/mailbox/read";
    private const string API_DELETE_ENDPOINT = "/api/mailbox/messages";
    private const string API_CLAIM_ENDPOINT = "/api/mailbox/claim";
    
    // 메시지 타입 핸들러 매핑
    private Dictionary<MailMessageType, IMailboxMessageHandler> _messageHandlers;
    #endregion
    
    #region Properties
    /// <summary>
    /// 현재 우편함 데이터
    /// </summary>
    public MailboxData MailboxData => _mailboxData;
    
    /// <summary>
    /// 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 로딩 중인지 여부
    /// </summary>
    public bool IsLoading => _isLoading;
    
    /// <summary>
    /// 동기화 중인지 여부
    /// </summary>
    public bool IsSyncing => _isSyncing;
    
    /// <summary>
    /// 읽지 않은 메시지 수
    /// </summary>
    public int UnreadCount => _mailboxData?.unreadCount ?? 0;
    
    /// <summary>
    /// 총 메시지 수
    /// </summary>
    public int TotalMessageCount => _mailboxData?.messages?.Count ?? 0;
    
    /// <summary>
    /// 현재 사용자 ID
    /// </summary>
    public string CurrentUserId => _currentUserId;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMessageHandlers();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
        ClearEvents();
    }
    #endregion
    
    #region Initialization
    /// <summary>
    /// 우편함 매니저 초기화
    /// </summary>
    /// <param name="userId">현재 사용자 ID</param>
    public void Initialize(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[MailboxManager] Cannot initialize without user ID");
            OnError?.Invoke("사용자 ID가 필요합니다");
            return;
        }
        
        _currentUserId = userId;
        _isInitialized = false;
        
        Debug.Log($"[MailboxManager] Initializing for user: {userId}");
        StartCoroutine(InitializeAsync());
    }
    
    /// <summary>
    /// 비동기 초기화
    /// </summary>
    private IEnumerator InitializeAsync()
    {
        try
        {
            // 1. 캐시에서 데이터 로드 시도
            _mailboxData = MailboxCache.LoadFromCache(_currentUserId);
            
            if (_mailboxData != null)
            {
                Debug.Log($"[MailboxManager] Loaded {_mailboxData.messages?.Count ?? 0} messages from cache");
                OnMailboxLoaded?.Invoke(_mailboxData);
                OnUnreadCountChanged?.Invoke(_mailboxData.unreadCount);
            }
            else
            {
                // 캐시가 없으면 빈 데이터로 초기화
                _mailboxData = new MailboxData();
                Debug.Log("[MailboxManager] No cache found, initialized with empty data");
            }
            
            // 2. 서버에서 최신 데이터 동기화
            yield return StartCoroutine(SyncWithServer());
            
            // 3. 자동 동기화 시작
            StartAutoSync();
            
            _isInitialized = true;
            Debug.Log("[MailboxManager] Initialization completed");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxManager] Initialization failed: {e.Message}");
            OnError?.Invoke("우편함 초기화에 실패했습니다");
        }
    }
    
    /// <summary>
    /// 메시지 타입 핸들러 초기화
    /// </summary>
    private void InitializeMessageHandlers()
    {
        _messageHandlers = new Dictionary<MailMessageType, IMailboxMessageHandler>
        {
            [MailMessageType.EnergyGift] = new EnergyGiftHandler()
            // 다른 핸들러들은 필요에 따라 추가
        };
    }
    #endregion
    
    #region Message Management
    /// <summary>
    /// 새 메시지 추가
    /// </summary>
    public bool AddMessage(MailboxMessage message)
    {
        if (message == null || _mailboxData == null)
            return false;
        
        bool added = _mailboxData.AddMessage(message);
        if (added)
        {
            OnNewMessageReceived?.Invoke(message);
            OnMailboxUpdated?.Invoke(_mailboxData);
            OnUnreadCountChanged?.Invoke(_mailboxData.unreadCount);
            
            // 캐시 업데이트
            MailboxCache.SaveToCache(_mailboxData, _currentUserId);
            
            Debug.Log($"[MailboxManager] New message added: {message.title}");
        }
        
        return added;
    }
    
    /// <summary>
    /// 메시지를 읽음 상태로 표시
    /// </summary>
    public void MarkMessageAsRead(string messageId, bool syncToServer = true)
    {
        if (string.IsNullOrEmpty(messageId) || _mailboxData == null)
            return;
        
        bool marked = _mailboxData.MarkMessageAsRead(messageId);
        if (marked)
        {
            OnMessageRead?.Invoke(messageId);
            OnMailboxUpdated?.Invoke(_mailboxData);
            OnUnreadCountChanged?.Invoke(_mailboxData.unreadCount);
            
            // 캐시 업데이트
            MailboxCache.SaveToCache(_mailboxData, _currentUserId);
            
            // 서버 동기화
            if (syncToServer)
            {
                StartCoroutine(MarkAsReadOnServer(messageId));
            }
            
            Debug.Log($"[MailboxManager] Message marked as read: {messageId}");
        }
    }
    
    /// <summary>
    /// 모든 메시지를 읽음 상태로 표시
    /// </summary>
    public void MarkAllMessagesAsRead()
    {
        if (_mailboxData == null || _mailboxData.unreadCount == 0)
            return;
        
        var unreadMessages = _mailboxData.GetUnreadMessages();
        _mailboxData.MarkAllAsRead();
        
        OnMailboxUpdated?.Invoke(_mailboxData);
        OnUnreadCountChanged?.Invoke(0);
        
        // 캐시 업데이트
        MailboxCache.SaveToCache(_mailboxData, _currentUserId);
        
        // 서버에 일괄 읽음 처리 요청
        foreach (var message in unreadMessages)
        {
            StartCoroutine(MarkAsReadOnServer(message.messageId));
        }
        
        Debug.Log($"[MailboxManager] Marked {unreadMessages.Count} messages as read");
    }
    
    /// <summary>
    /// 메시지 삭제
    /// </summary>
    public void DeleteMessage(string messageId, bool syncToServer = true)
    {
        if (string.IsNullOrEmpty(messageId) || _mailboxData == null)
            return;
        
        bool removed = _mailboxData.RemoveMessage(messageId);
        if (removed)
        {
            OnMessageDeleted?.Invoke(messageId);
            OnMailboxUpdated?.Invoke(_mailboxData);
            OnUnreadCountChanged?.Invoke(_mailboxData.unreadCount);
            
            // 캐시 업데이트
            MailboxCache.SaveToCache(_mailboxData, _currentUserId);
            
            // 서버 동기화
            if (syncToServer)
            {
                StartCoroutine(DeleteMessageOnServer(messageId));
            }
            
            Debug.Log($"[MailboxManager] Message deleted: {messageId}");
        }
    }
    
    /// <summary>
    /// 메시지 가져오기
    /// </summary>
    public MailboxMessage GetMessage(string messageId)
    {
        return _mailboxData?.GetMessage(messageId);
    }
    
    /// <summary>
    /// 타입별 메시지 가져오기
    /// </summary>
    public List<MailboxMessage> GetMessagesByType(MailMessageType type)
    {
        return _mailboxData?.GetMessagesByType(type) ?? new List<MailboxMessage>();
    }
    
    /// <summary>
    /// 읽지 않은 메시지 가져오기
    /// </summary>
    public List<MailboxMessage> GetUnreadMessages()
    {
        return _mailboxData?.GetUnreadMessages() ?? new List<MailboxMessage>();
    }
    #endregion
    
    #region Server Synchronization
    /// <summary>
    /// 서버와 동기화
    /// </summary>
    public void RefreshFromServer()
    {
        if (_isSyncing)
        {
            Debug.LogWarning("[MailboxManager] Already syncing");
            return;
        }
        
        StartCoroutine(SyncWithServer());
    }
    
    /// <summary>
    /// 서버와 동기화 (내부)
    /// </summary>
    private IEnumerator SyncWithServer()
    {
        _isSyncing = true;
        OnSyncStatusChanged?.Invoke(false, "동기화 중...");
        
        Debug.Log("[MailboxManager] Starting server sync");
        
        int retryCount = 0;
        bool success = false;
        
        while (retryCount < MAX_RETRY_COUNT && !success)
        {
            if (retryCount > 0)
            {
                float delay = RETRY_DELAY_BASE * Mathf.Pow(2, retryCount - 1);
                Debug.Log($"[MailboxManager] Retry {retryCount}/{MAX_RETRY_COUNT} after {delay}s");
                yield return new WaitForSeconds(delay);
            }
            
            yield return StartCoroutine(LoadMessagesFromServer());
            
            // NetworkManager를 통해 결과 확인 (실제 구현 시 NetworkResponse 확인)
            success = true; // 임시로 성공으로 설정, 실제로는 NetworkResponse.IsSuccess 확인
            retryCount++;
        }
        
        _isSyncing = false;
        
        if (success)
        {
            OnSyncStatusChanged?.Invoke(true, "동기화 완료");
            Debug.Log("[MailboxManager] Server sync completed successfully");
        }
        else
        {
            OnSyncStatusChanged?.Invoke(false, "동기화 실패");
            Debug.LogError("[MailboxManager] Server sync failed after all retries");
        }
    }
    
    /// <summary>
    /// 서버에서 메시지 로드
    /// </summary>
    private IEnumerator LoadMessagesFromServer()
    {
        bool requestCompleted = false;
        MailboxData serverData = null;
        
        NetworkManager.Instance.Get(API_MESSAGES_ENDPOINT, (response) =>
        {
            requestCompleted = true;
            
            if (response.IsSuccess)
            {
                try
                {
                    serverData = response.GetData<MailboxData>();
                    Debug.Log($"[MailboxManager] Loaded {serverData?.messages?.Count ?? 0} messages from server");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MailboxManager] Failed to parse server data: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[MailboxManager] Server request failed: {response.Error}");
            }
        });
        
        // 응답 대기
        yield return new WaitUntil(() => requestCompleted);
        
        if (serverData != null && serverData.IsValid())
        {
            // 서버 데이터로 업데이트
            var oldUnreadCount = _mailboxData?.unreadCount ?? 0;
            _mailboxData = serverData;
            
            OnMailboxLoaded?.Invoke(_mailboxData);
            OnMailboxUpdated?.Invoke(_mailboxData);
            
            if (oldUnreadCount != _mailboxData.unreadCount)
            {
                OnUnreadCountChanged?.Invoke(_mailboxData.unreadCount);
            }
            
            // 캐시 업데이트
            MailboxCache.SaveToCache(_mailboxData, _currentUserId);
        }
    }
    
    /// <summary>
    /// 서버에 읽음 상태 업데이트
    /// </summary>
    private IEnumerator MarkAsReadOnServer(string messageId)
    {
        var requestData = new { messageId = messageId };
        bool requestCompleted = false;
        
        NetworkManager.Instance.Post(API_READ_ENDPOINT, requestData, (response) =>
        {
            requestCompleted = true;
            
            if (!response.IsSuccess)
            {
                Debug.LogError($"[MailboxManager] Failed to mark message as read on server: {response.Error}");
            }
        });
        
        yield return new WaitUntil(() => requestCompleted);
    }
    
    /// <summary>
    /// 서버에서 메시지 삭제
    /// </summary>
    private IEnumerator DeleteMessageOnServer(string messageId)
    {
        bool requestCompleted = false;
        
        NetworkManager.Instance.Delete($"{API_DELETE_ENDPOINT}/{messageId}", (response) =>
        {
            requestCompleted = true;
            
            if (!response.IsSuccess)
            {
                Debug.LogError($"[MailboxManager] Failed to delete message on server: {response.Error}");
            }
        });
        
        yield return new WaitUntil(() => requestCompleted);
    }
    #endregion
    
    #region Message Processing
    /// <summary>
    /// 메시지 처리 (타입별 핸들러 호출)
    /// </summary>
    public void ProcessMessage(string messageId)
    {
        var message = GetMessage(messageId);
        if (message == null)
        {
            Debug.LogWarning($"[MailboxManager] Message not found: {messageId}");
            return;
        }
        
        // 메시지 타입별 처리
        if (_messageHandlers.TryGetValue(message.type, out var handler))
        {
            try
            {
                handler.HandleMessage(message, this);
                Debug.Log($"[MailboxManager] Processed {message.type} message: {messageId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MailboxManager] Failed to process message: {e.Message}");
                OnError?.Invoke($"메시지 처리 중 오류가 발생했습니다: {message.title}");
            }
        }
        
        // 메시지를 읽음으로 표시
        if (!message.isRead)
        {
            MarkMessageAsRead(messageId);
        }
    }
    #endregion
    
    #region Auto Sync
    /// <summary>
    /// 자동 동기화 시작
    /// </summary>
    private void StartAutoSync()
    {
        if (_autoSyncCoroutine != null)
        {
            StopCoroutine(_autoSyncCoroutine);
        }
        
        _autoSyncCoroutine = StartCoroutine(AutoSyncCoroutine());
    }
    
    /// <summary>
    /// 자동 동기화 코루틴
    /// </summary>
    private IEnumerator AutoSyncCoroutine()
    {
        while (_isInitialized)
        {
            yield return new WaitForSeconds(AUTO_SYNC_INTERVAL);
            
            if (!_isSyncing && NetworkManager.Instance.IsNetworkAvailable)
            {
                yield return StartCoroutine(SyncWithServer());
            }
        }
    }
    #endregion
    
    #region Utility Methods
    /// <summary>
    /// 캐시 클리어
    /// </summary>
    public void ClearCache()
    {
        MailboxCache.ClearCache();
        Debug.Log("[MailboxManager] Cache cleared");
    }
    
    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void ClearEvents()
    {
        OnMailboxLoaded = null;
        OnMailboxUpdated = null;
        OnUnreadCountChanged = null;
        OnNewMessageReceived = null;
        OnMessageRead = null;
        OnMessageDeleted = null;
        OnSyncStatusChanged = null;
        OnError = null;
    }
    
    /// <summary>
    /// 상태 정보 가져오기
    /// </summary>
    public MailboxManagerStatus GetStatus()
    {
        return new MailboxManagerStatus
        {
            IsInitialized = _isInitialized,
            IsLoading = _isLoading,
            IsSyncing = _isSyncing,
            CurrentUserId = _currentUserId,
            TotalMessages = TotalMessageCount,
            UnreadMessages = UnreadCount,
            CacheInfo = MailboxCache.GetCacheInfo()
        };
    }
    #endregion
}

#region Message Handler Interface
/// <summary>
/// 메시지 핸들러 인터페이스
/// </summary>
public interface IMailboxMessageHandler
{
    void HandleMessage(MailboxMessage message, MailboxManager manager);
}
#endregion

#region Status Classes
/// <summary>
/// 우편함 매니저 상태 정보
/// </summary>
[Serializable]
public class MailboxManagerStatus
{
    public bool IsInitialized;
    public bool IsLoading;
    public bool IsSyncing;
    public string CurrentUserId;
    public int TotalMessages;
    public int UnreadMessages;
    public MailboxCacheInfo CacheInfo;
}
#endregion