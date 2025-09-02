using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우편함 동기화 시스템
/// MailboxManager와 MailboxNetworkHandler 간의 데이터 동기화를 담당
/// 오프라인/온라인 전환, 에러 처리, 동기화 상태 관리 포함
/// </summary>
public class MailboxSynchronizer : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 동기화 시작 이벤트
    /// </summary>
    public static event Action<SyncOperation> OnSyncStarted;
    
    /// <summary>
    /// 동기화 완료 이벤트
    /// </summary>
    public static event Action<SyncOperation, bool> OnSyncCompleted; // operation, success
    
    /// <summary>
    /// 동기화 진행률 업데이트 이벤트
    /// </summary>
    public static event Action<SyncOperation, float> OnSyncProgressUpdated; // operation, progress (0-1)
    
    /// <summary>
    /// 연결 상태 변경 이벤트
    /// </summary>
    public static event Action<bool> OnConnectionStateChanged; // isOnline
    
    /// <summary>
    /// 동기화 오류 이벤트
    /// </summary>
    public static event Action<SyncOperation, string> OnSyncError; // operation, errorMessage
    #endregion

    #region Singleton
    private static MailboxSynchronizer _instance;
    public static MailboxSynchronizer Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MailboxSynchronizer");
                _instance = go.AddComponent<MailboxSynchronizer>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private MailboxManager _mailboxManager;
    private MailboxNetworkHandler _networkHandler;
    private bool _isInitialized = false;
    
    // 동기화 상태 관리
    private readonly Dictionary<SyncOperation, bool> _activeSyncOperations = new();
    private readonly Queue<SyncRequest> _pendingSyncQueue = new();
    private bool _isOnline = false;
    private bool _isPaused = false;
    
    // 자동 동기화 설정
    private Coroutine _autoSyncCoroutine;
    private const float AUTO_SYNC_INTERVAL = 300f; // 5분
    private const float RETRY_SYNC_INTERVAL = 60f;  // 1분 (실패 시)
    
    // 오프라인 모드 처리
    private readonly Queue<PendingAction> _offlineActions = new();
    private DateTime _lastSuccessfulSync = DateTime.MinValue;
    private const int MAX_OFFLINE_ACTIONS = 100;
    
    // 성능 모니터링
    private DateTime _syncStartTime;
    private const float SYNC_TIMEOUT = 10f;
    private const float PERFORMANCE_TARGET = 3f; // 3초 이내 목표
    #endregion

    #region Properties
    /// <summary>
    /// 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 온라인 상태
    /// </summary>
    public bool IsOnline => _isOnline;
    
    /// <summary>
    /// 동기화 일시정지 상태
    /// </summary>
    public bool IsPaused => _isPaused;
    
    /// <summary>
    /// 활성 동기화 작업 수
    /// </summary>
    public int ActiveSyncCount => _activeSyncOperations.Count;
    
    /// <summary>
    /// 대기 중인 오프라인 액션 수
    /// </summary>
    public int PendingOfflineActionCount => _offlineActions.Count;
    
    /// <summary>
    /// 마지막 성공한 동기화 시간
    /// </summary>
    public DateTime LastSuccessfulSync => _lastSuccessfulSync;
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
        StartCoroutine(InitializeAsync());
    }

    private void OnDestroy()
    {
        Cleanup();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 비동기 초기화
    /// </summary>
    private IEnumerator InitializeAsync()
    {
        try
        {
            Debug.Log("[MailboxSynchronizer] Starting initialization");
            
            // MailboxManager 대기
            float timeout = 5f;
            float elapsed = 0f;
            while (_mailboxManager == null && elapsed < timeout)
            {
                _mailboxManager = MailboxManager.Instance;
                if (_mailboxManager == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }
            }

            if (_mailboxManager == null)
            {
                Debug.LogError("[MailboxSynchronizer] Failed to get MailboxManager instance");
                yield break;
            }

            // NetworkHandler 대기
            elapsed = 0f;
            while (_networkHandler == null && elapsed < timeout)
            {
                _networkHandler = MailboxNetworkHandler.Instance;
                if (_networkHandler == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }
            }

            if (_networkHandler == null)
            {
                Debug.LogError("[MailboxSynchronizer] Failed to get MailboxNetworkHandler instance");
                yield break;
            }

            // 이벤트 구독
            SubscribeToEvents();
            
            // 네트워크 상태 초기화
            _isOnline = _networkHandler.IsNetworkAvailable;
            OnConnectionStateChanged?.Invoke(_isOnline);
            
            // 자동 동기화 시작
            StartAutoSync();
            
            _isInitialized = true;
            Debug.Log("[MailboxSynchronizer] Initialization completed successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MailboxSynchronizer] Initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        // MailboxNetworkHandler 이벤트 구독
        MailboxNetworkHandler.OnMessagesLoaded += OnNetworkMessagesLoaded;
        MailboxNetworkHandler.OnMessageReadStatusUpdated += OnNetworkMessageReadStatusUpdated;
        MailboxNetworkHandler.OnMessageDeleted += OnNetworkMessageDeleted;
        MailboxNetworkHandler.OnAttachmentClaimed += OnNetworkAttachmentClaimed;
        MailboxNetworkHandler.OnNetworkError += OnNetworkError;

        // NetworkManager 네트워크 상태 변경 구독
        NetworkManager.OnNetworkStatusChanged += OnNetworkStatusChanged;
    }
    #endregion

    #region Public API
    /// <summary>
    /// 수동 동기화 시작
    /// </summary>
    /// <param name="forceRefresh">강제 새로고침</param>
    public void SynchronizeMessages(bool forceRefresh = false)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[MailboxSynchronizer] Not initialized");
            return;
        }

        if (_activeSyncOperations.ContainsKey(SyncOperation.LoadMessages))
        {
            Debug.LogWarning("[MailboxSynchronizer] Message sync already in progress");
            return;
        }

        StartSyncOperation(SyncOperation.LoadMessages, forceRefresh);
    }

    /// <summary>
    /// 메시지 읽음 동기화
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    public void SynchronizeMessageRead(string messageId)
    {
        if (!_isInitialized || string.IsNullOrEmpty(messageId))
            return;

        if (_isOnline)
        {
            StartSyncOperation(SyncOperation.MarkAsRead, messageId);
        }
        else
        {
            // 오프라인 액션으로 저장
            EnqueueOfflineAction(new PendingAction
            {
                Type = SyncOperation.MarkAsRead,
                MessageId = messageId,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 메시지 삭제 동기화
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    public void SynchronizeMessageDelete(string messageId)
    {
        if (!_isInitialized || string.IsNullOrEmpty(messageId))
            return;

        if (_isOnline)
        {
            StartSyncOperation(SyncOperation.DeleteMessage, messageId);
        }
        else
        {
            // 오프라인 액션으로 저장
            EnqueueOfflineAction(new PendingAction
            {
                Type = SyncOperation.DeleteMessage,
                MessageId = messageId,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 첨부 파일 수령 동기화
    /// </summary>
    /// <param name="messageId">메시지 ID</param>
    /// <param name="giftId">선물 ID</param>
    public void SynchronizeAttachmentClaim(string messageId, string giftId = null)
    {
        if (!_isInitialized || string.IsNullOrEmpty(messageId))
            return;

        if (_isOnline)
        {
            StartSyncOperation(SyncOperation.ClaimAttachment, messageId, giftId);
        }
        else
        {
            // 오프라인 액션으로 저장
            EnqueueOfflineAction(new PendingAction
            {
                Type = SyncOperation.ClaimAttachment,
                MessageId = messageId,
                GiftId = giftId,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 동기화 일시정지/재개
    /// </summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        Debug.Log($"[MailboxSynchronizer] Sync {(paused ? "paused" : "resumed")}");
        
        if (!paused && _isOnline)
        {
            ProcessPendingOfflineActions();
        }
    }

    /// <summary>
    /// 오프라인 액션 큐 클리어
    /// </summary>
    public void ClearOfflineActions()
    {
        _offlineActions.Clear();
        Debug.Log("[MailboxSynchronizer] Offline actions cleared");
    }

    /// <summary>
    /// 동기화 통계 정보
    /// </summary>
    public SynchronizerStats GetStats()
    {
        return new SynchronizerStats
        {
            IsOnline = _isOnline,
            IsPaused = _isPaused,
            ActiveSyncCount = ActiveSyncCount,
            PendingOfflineActionCount = PendingOfflineActionCount,
            LastSuccessfulSync = _lastSuccessfulSync,
            AutoSyncEnabled = _autoSyncCoroutine != null
        };
    }
    #endregion

    #region Sync Operations
    /// <summary>
    /// 동기화 작업 시작
    /// </summary>
    private void StartSyncOperation(SyncOperation operation, params object[] parameters)
    {
        if (_isPaused)
        {
            Debug.Log($"[MailboxSynchronizer] Sync paused, queuing operation: {operation}");
            // TODO: 일시정지 중 큐에 저장하는 로직 추가 가능
            return;
        }

        if (_activeSyncOperations.ContainsKey(operation))
        {
            Debug.LogWarning($"[MailboxSynchronizer] Operation already active: {operation}");
            return;
        }

        _activeSyncOperations[operation] = true;
        _syncStartTime = DateTime.UtcNow;
        
        OnSyncStarted?.Invoke(operation);
        Debug.Log($"[MailboxSynchronizer] Starting sync operation: {operation}");

        // 타임아웃 처리
        StartCoroutine(SyncTimeoutHandler(operation));

        // 작업별 처리
        switch (operation)
        {
            case SyncOperation.LoadMessages:
                bool forceRefresh = parameters.Length > 0 && (bool)parameters[0];
                _networkHandler.LoadMessages(forceRefresh);
                break;
                
            case SyncOperation.MarkAsRead:
                string messageId = parameters[0].ToString();
                _networkHandler.MarkMessageAsRead(messageId);
                break;
                
            case SyncOperation.DeleteMessage:
                string deleteMessageId = parameters[0].ToString();
                _networkHandler.DeleteMessage(deleteMessageId);
                break;
                
            case SyncOperation.ClaimAttachment:
                string claimMessageId = parameters[0].ToString();
                string giftId = parameters.Length > 1 ? parameters[1]?.ToString() : null;
                _networkHandler.ClaimAttachment(claimMessageId, giftId);
                break;
        }
    }

    /// <summary>
    /// 동기화 작업 완료 처리
    /// </summary>
    private void CompleteSyncOperation(SyncOperation operation, bool success)
    {
        if (!_activeSyncOperations.ContainsKey(operation))
            return;

        _activeSyncOperations.Remove(operation);
        
        float elapsedTime = (float)(DateTime.UtcNow - _syncStartTime).TotalSeconds;
        
        if (success)
        {
            _lastSuccessfulSync = DateTime.UtcNow;
            
            // 성능 모니터링
            if (operation == SyncOperation.LoadMessages && elapsedTime > PERFORMANCE_TARGET)
            {
                Debug.LogWarning($"[MailboxSynchronizer] LoadMessages took {elapsedTime:F2}s (target: {PERFORMANCE_TARGET}s)");
            }
        }

        OnSyncCompleted?.Invoke(operation, success);
        Debug.Log($"[MailboxSynchronizer] Sync operation completed: {operation}, success: {success}, time: {elapsedTime:F2}s");
    }

    /// <summary>
    /// 동기화 타임아웃 처리
    /// </summary>
    private IEnumerator SyncTimeoutHandler(SyncOperation operation)
    {
        yield return new WaitForSeconds(SYNC_TIMEOUT);
        
        if (_activeSyncOperations.ContainsKey(operation))
        {
            Debug.LogError($"[MailboxSynchronizer] Sync operation timed out: {operation}");
            CompleteSyncOperation(operation, false);
            OnSyncError?.Invoke(operation, "Operation timed out");
        }
    }
    #endregion

    #region Network Event Handlers
    /// <summary>
    /// 네트워크 메시지 로드 완료 처리
    /// </summary>
    private void OnNetworkMessagesLoaded(MailboxData data, bool success)
    {
        if (success && data != null)
        {
            // MailboxManager와 동기화
            _mailboxManager.SyncWithServerData(data);
        }
        
        CompleteSyncOperation(SyncOperation.LoadMessages, success);
    }

    /// <summary>
    /// 네트워크 메시지 읽음 상태 업데이트 완료 처리
    /// </summary>
    private void OnNetworkMessageReadStatusUpdated(string messageId, bool success)
    {
        CompleteSyncOperation(SyncOperation.MarkAsRead, success);
    }

    /// <summary>
    /// 네트워크 메시지 삭제 완료 처리
    /// </summary>
    private void OnNetworkMessageDeleted(string messageId, bool success)
    {
        CompleteSyncOperation(SyncOperation.DeleteMessage, success);
    }

    /// <summary>
    /// 네트워크 첨부 파일 수령 완료 처리
    /// </summary>
    private void OnNetworkAttachmentClaimed(string messageId, bool success, object result)
    {
        if (success && result is ClaimResult claimResult)
        {
            // 수령 결과를 MailboxManager에 전달 (예: 에너지 추가)
            _mailboxManager.ProcessClaimResult(messageId, claimResult);
        }
        
        CompleteSyncOperation(SyncOperation.ClaimAttachment, success);
    }

    /// <summary>
    /// 네트워크 오류 처리
    /// </summary>
    private void OnNetworkError(string operation, string errorMessage)
    {
        Debug.LogError($"[MailboxSynchronizer] Network error in {operation}: {errorMessage}");
        
        // 작업 타입에 따른 에러 처리
        if (Enum.TryParse<SyncOperation>(operation, out SyncOperation syncOp))
        {
            OnSyncError?.Invoke(syncOp, errorMessage);
        }
    }

    /// <summary>
    /// 네트워크 상태 변경 처리
    /// </summary>
    private void OnNetworkStatusChanged(bool isOnline)
    {
        bool previousState = _isOnline;
        _isOnline = isOnline;
        
        if (previousState != _isOnline)
        {
            OnConnectionStateChanged?.Invoke(_isOnline);
            Debug.Log($"[MailboxSynchronizer] Network status changed: {_isOnline}");
            
            if (_isOnline && !_isPaused)
            {
                // 온라인 복구 시 오프라인 액션 처리
                StartCoroutine(OnlineRecoveryCoroutine());
            }
        }
    }
    #endregion

    #region Offline Mode Handling
    /// <summary>
    /// 오프라인 액션 큐에 추가
    /// </summary>
    private void EnqueueOfflineAction(PendingAction action)
    {
        if (_offlineActions.Count >= MAX_OFFLINE_ACTIONS)
        {
            _offlineActions.Dequeue(); // 가장 오래된 액션 제거
            Debug.LogWarning("[MailboxSynchronizer] Offline action queue full, removing oldest action");
        }

        _offlineActions.Enqueue(action);
        Debug.Log($"[MailboxSynchronizer] Offline action queued: {action.Type} for {action.MessageId}");
    }

    /// <summary>
    /// 온라인 복구 시 처리
    /// </summary>
    private IEnumerator OnlineRecoveryCoroutine()
    {
        yield return new WaitForSeconds(1f); // 네트워크 안정화 대기
        
        Debug.Log("[MailboxSynchronizer] Starting online recovery");
        
        // 1. 서버에서 최신 데이터 동기화
        SynchronizeMessages(true);
        
        // 2. 동기화 완료 대기
        yield return new WaitUntil(() => !_activeSyncOperations.ContainsKey(SyncOperation.LoadMessages));
        
        // 3. 오프라인 액션 처리
        ProcessPendingOfflineActions();
    }

    /// <summary>
    /// 대기 중인 오프라인 액션 처리
    /// </summary>
    private void ProcessPendingOfflineActions()
    {
        if (_offlineActions.Count == 0)
            return;

        Debug.Log($"[MailboxSynchronizer] Processing {_offlineActions.Count} offline actions");
        StartCoroutine(ProcessOfflineActionsCoroutine());
    }

    /// <summary>
    /// 오프라인 액션 처리 코루틴
    /// </summary>
    private IEnumerator ProcessOfflineActionsCoroutine()
    {
        while (_offlineActions.Count > 0 && _isOnline)
        {
            var action = _offlineActions.Dequeue();
            
            // 액션 처리
            switch (action.Type)
            {
                case SyncOperation.MarkAsRead:
                    SynchronizeMessageRead(action.MessageId);
                    break;
                case SyncOperation.DeleteMessage:
                    SynchronizeMessageDelete(action.MessageId);
                    break;
                case SyncOperation.ClaimAttachment:
                    SynchronizeAttachmentClaim(action.MessageId, action.GiftId);
                    break;
            }

            // 다음 액션 처리 전 대기
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[MailboxSynchronizer] Offline actions processing completed");
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
        Debug.Log("[MailboxSynchronizer] Auto sync started");
    }

    /// <summary>
    /// 자동 동기화 중지
    /// </summary>
    public void StopAutoSync()
    {
        if (_autoSyncCoroutine != null)
        {
            StopCoroutine(_autoSyncCoroutine);
            _autoSyncCoroutine = null;
            Debug.Log("[MailboxSynchronizer] Auto sync stopped");
        }
    }

    /// <summary>
    /// 자동 동기화 코루틴
    /// </summary>
    private IEnumerator AutoSyncCoroutine()
    {
        while (_isInitialized)
        {
            float interval = _lastSuccessfulSync == DateTime.MinValue ? RETRY_SYNC_INTERVAL : AUTO_SYNC_INTERVAL;
            yield return new WaitForSeconds(interval);
            
            if (!_isPaused && _isOnline && !_activeSyncOperations.ContainsKey(SyncOperation.LoadMessages))
            {
                Debug.Log("[MailboxSynchronizer] Performing auto sync");
                SynchronizeMessages();
            }
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 정리 작업
    /// </summary>
    private void Cleanup()
    {
        StopAutoSync();
        
        // 이벤트 구독 해제
        if (_networkHandler != null)
        {
            MailboxNetworkHandler.OnMessagesLoaded -= OnNetworkMessagesLoaded;
            MailboxNetworkHandler.OnMessageReadStatusUpdated -= OnNetworkMessageReadStatusUpdated;
            MailboxNetworkHandler.OnMessageDeleted -= OnNetworkMessageDeleted;
            MailboxNetworkHandler.OnAttachmentClaimed -= OnNetworkAttachmentClaimed;
            MailboxNetworkHandler.OnNetworkError -= OnNetworkError;
        }

        NetworkManager.OnNetworkStatusChanged -= OnNetworkStatusChanged;

        // 이벤트 클리어
        OnSyncStarted = null;
        OnSyncCompleted = null;
        OnSyncProgressUpdated = null;
        OnConnectionStateChanged = null;
        OnSyncError = null;
    }
    #endregion
}

#region Data Structures
/// <summary>
/// 동기화 작업 타입
/// </summary>
public enum SyncOperation
{
    LoadMessages,
    MarkAsRead,
    DeleteMessage,
    ClaimAttachment
}

/// <summary>
/// 동기화 요청
/// </summary>
[Serializable]
public class SyncRequest
{
    public SyncOperation Operation;
    public object[] Parameters;
    public DateTime RequestTime;
    public int Priority;
}

/// <summary>
/// 대기 중인 오프라인 액션
/// </summary>
[Serializable]
public class PendingAction
{
    public SyncOperation Type;
    public string MessageId;
    public string GiftId;
    public DateTime Timestamp;
    public int RetryCount;
}

/// <summary>
/// 동기화 시스템 통계
/// </summary>
[Serializable]
public class SynchronizerStats
{
    public bool IsOnline;
    public bool IsPaused;
    public int ActiveSyncCount;
    public int PendingOfflineActionCount;
    public DateTime LastSuccessfulSync;
    public bool AutoSyncEnabled;
}
#endregion