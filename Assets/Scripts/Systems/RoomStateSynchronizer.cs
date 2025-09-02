using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 방 상태 실시간 동기화 시스템
/// RoomManager와 RoomNetworkHandler를 연결하여 실시간 방 상태 동기화 관리
/// 상태 충돌 해결, 동기화 검증, 성능 최적화를 담당
/// </summary>
public class RoomStateSynchronizer : MonoBehaviour
{
    #region Events
    /// <summary>동기화 상태 변경 이벤트</summary>
    public event Action<bool> OnSyncStatusChanged; // isSynchronized
    
    /// <summary>동기화 충돌 발생 이벤트</summary>
    public event Action<string, RoomConflictType> OnSyncConflict; // roomCode, conflictType
    
    /// <summary>동기화 성능 경고 이벤트</summary>
    public event Action<string, float> OnSyncPerformanceWarning; // operation, actualTime
    #endregion

    #region Private Fields
    private RoomManager _roomManager;
    private RoomNetworkHandler _networkHandler;
    
    private bool _isInitialized = false;
    private bool _isSynchronized = true;
    private bool _isSyncInProgress = false;
    
    // 상태 동기화 관리
    private RoomData _localRoomState;
    private RoomProtocolExtension.RoomStateSyncData _lastServerSync;
    private Dictionary<string, int> _playerVersions = new(); // playerId -> version
    
    // 성능 모니터링
    private readonly Dictionary<string, DateTime> _operationStartTimes = new();
    private const float TARGET_SYNC_TIME = 1.0f; // 1초 동기화 목표
    private const float SYNC_INTERVAL = 5.0f; // 5초마다 동기화 상태 확인
    private const int MAX_SYNC_RETRIES = 3;
    
    // 충돌 해결
    private readonly Queue<PendingSyncOperation> _pendingOperations = new();
    private readonly Dictionary<string, DateTime> _conflictResolvedTimes = new();
    private Coroutine _syncMonitorCoroutine;
    #endregion

    #region Properties
    public bool IsInitialized => _isInitialized;
    public bool IsSynchronized => _isSynchronized;
    public bool IsSyncInProgress => _isSyncInProgress;
    public RoomData LocalRoomState => _localRoomState;
    public int PendingOperationCount => _pendingOperations.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // 컴포넌트 찾기
        _roomManager = FindObjectOfType<RoomManager>();
        _networkHandler = FindObjectOfType<RoomNetworkHandler>();
        
        if (_roomManager == null)
        {
            Debug.LogError("[RoomStateSynchronizer] RoomManager not found");
            return;
        }
        
        if (_networkHandler == null)
        {
            Debug.LogError("[RoomStateSynchronizer] RoomNetworkHandler not found");
            return;
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    private void OnDestroy()
    {
        Cleanup();
    }
    #endregion

    #region Initialization
    private IEnumerator InitializeAfterFrame()
    {
        yield return null; // 다른 컴포넌트들이 초기화될 때까지 대기
        
        if (_roomManager != null && _networkHandler != null)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[RoomStateSynchronizer] Already initialized");
            return;
        }

        try
        {
            // RoomManager 이벤트 구독
            SubscribeToRoomManagerEvents();
            
            // NetworkHandler 이벤트 구독
            SubscribeToNetworkEvents();
            
            // 동기화 모니터링 시작
            StartSyncMonitoring();
            
            _isInitialized = true;
            Debug.Log("[RoomStateSynchronizer] Initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomStateSynchronizer] Initialization failed: {e.Message}");
        }
    }

    private void SubscribeToRoomManagerEvents()
    {
        // Room lifecycle events
        RoomManager.OnRoomCreated += HandleRoomCreated;
        RoomManager.OnRoomJoined += HandleRoomJoined;
        RoomManager.OnRoomUpdated += HandleRoomUpdated;
        RoomManager.OnRoomLeft += HandleRoomLeft;
        RoomManager.OnRoomClosed += HandleRoomClosed;
        
        // Player events
        RoomManager.OnPlayerJoined += HandlePlayerJoined;
        RoomManager.OnPlayerLeft += HandlePlayerLeft;
        RoomManager.OnPlayerUpdated += HandlePlayerUpdated;
        RoomManager.OnHostChanged += HandleHostChanged;
    }

    private void SubscribeToNetworkEvents()
    {
        _networkHandler.OnRoomResponse += HandleNetworkRoomResponse;
        _networkHandler.OnRoomStateSync += HandleNetworkRoomStateSync;
        _networkHandler.OnPlayerJoined += HandleNetworkPlayerJoined;
        _networkHandler.OnPlayerLeft += HandleNetworkPlayerLeft;
        _networkHandler.OnHostChanged += HandleNetworkHostChanged;
        _networkHandler.OnConnectionStateChanged += HandleNetworkConnectionChanged;
    }

    private void StartSyncMonitoring()
    {
        if (_syncMonitorCoroutine != null)
        {
            StopCoroutine(_syncMonitorCoroutine);
        }
        
        _syncMonitorCoroutine = StartCoroutine(SyncMonitorCoroutine());
    }

    private void Cleanup()
    {
        // RoomManager 이벤트 구독 해제
        if (RoomManager.Instance != null)
        {
            RoomManager.OnRoomCreated -= HandleRoomCreated;
            RoomManager.OnRoomJoined -= HandleRoomJoined;
            RoomManager.OnRoomUpdated -= HandleRoomUpdated;
            RoomManager.OnRoomLeft -= HandleRoomLeft;
            RoomManager.OnRoomClosed -= HandleRoomClosed;
            RoomManager.OnPlayerJoined -= HandlePlayerJoined;
            RoomManager.OnPlayerLeft -= HandlePlayerLeft;
            RoomManager.OnPlayerUpdated -= HandlePlayerUpdated;
            RoomManager.OnHostChanged -= HandleHostChanged;
        }
        
        // NetworkHandler 이벤트 구독 해제
        if (_networkHandler != null)
        {
            _networkHandler.OnRoomResponse -= HandleNetworkRoomResponse;
            _networkHandler.OnRoomStateSync -= HandleNetworkRoomStateSync;
            _networkHandler.OnPlayerJoined -= HandleNetworkPlayerJoined;
            _networkHandler.OnPlayerLeft -= HandleNetworkPlayerLeft;
            _networkHandler.OnHostChanged -= HandleNetworkHostChanged;
            _networkHandler.OnConnectionStateChanged -= HandleNetworkConnectionChanged;
        }
        
        if (_syncMonitorCoroutine != null)
        {
            StopCoroutine(_syncMonitorCoroutine);
            _syncMonitorCoroutine = null;
        }
        
        _pendingOperations.Clear();
        _conflictResolvedTimes.Clear();
        _playerVersions.Clear();
        
        Debug.Log("[RoomStateSynchronizer] Cleaned up");
    }
    #endregion

    #region RoomManager Event Handlers
    private void HandleRoomCreated(RoomData roomData)
    {
        StartSyncOperation("room_created");
        
        _localRoomState = roomData.DeepCopy();
        InitializePlayerVersions(roomData.Players);
        
        SetSyncStatus(true);
        CompleteSyncOperation("room_created");
        
        Debug.Log($"[RoomStateSynchronizer] Room created synchronized: {roomData.RoomCode}");
    }

    private void HandleRoomJoined(RoomData roomData)
    {
        StartSyncOperation("room_joined");
        
        _localRoomState = roomData.DeepCopy();
        InitializePlayerVersions(roomData.Players);
        
        // 서버에서 최신 상태 요청
        RequestServerStateSync(roomData.RoomCode);
        
        Debug.Log($"[RoomStateSynchronizer] Room joined, requesting server sync: {roomData.RoomCode}");
    }

    private void HandleRoomUpdated(RoomData roomData)
    {
        StartSyncOperation("room_updated");
        
        if (DetectConflict(roomData))
        {
            ResolveStateConflict(roomData);
        }
        else
        {
            _localRoomState = roomData.DeepCopy();
            CompleteSyncOperation("room_updated");
        }
        
        Debug.Log($"[RoomStateSynchronizer] Room updated synchronized: {roomData.RoomCode}");
    }

    private void HandleRoomLeft(RoomData roomData)
    {
        StartSyncOperation("room_left");
        
        CleanupRoomState();
        SetSyncStatus(true);
        
        CompleteSyncOperation("room_left");
        Debug.Log($"[RoomStateSynchronizer] Room left synchronized: {roomData.RoomCode}");
    }

    private void HandleRoomClosed(RoomData roomData)
    {
        StartSyncOperation("room_closed");
        
        CleanupRoomState();
        SetSyncStatus(true);
        
        CompleteSyncOperation("room_closed");
        Debug.Log($"[RoomStateSynchronizer] Room closed synchronized: {roomData.RoomCode}");
    }

    private void HandlePlayerJoined(RoomData roomData, PlayerInfo playerInfo)
    {
        StartSyncOperation("player_joined");
        
        if (_localRoomState != null)
        {
            UpdateLocalPlayerState(playerInfo, PlayerUpdateType.Added);
            UpdatePlayerVersion(playerInfo.PlayerId);
        }
        
        CompleteSyncOperation("player_joined");
        Debug.Log($"[RoomStateSynchronizer] Player joined synchronized: {playerInfo.Nickname}");
    }

    private void HandlePlayerLeft(RoomData roomData, PlayerInfo playerInfo)
    {
        StartSyncOperation("player_left");
        
        if (_localRoomState != null)
        {
            UpdateLocalPlayerState(playerInfo, PlayerUpdateType.Removed);
            RemovePlayerVersion(playerInfo.PlayerId);
        }
        
        CompleteSyncOperation("player_left");
        Debug.Log($"[RoomStateSynchronizer] Player left synchronized: {playerInfo.Nickname}");
    }

    private void HandlePlayerUpdated(RoomData roomData, PlayerInfo playerInfo)
    {
        StartSyncOperation("player_updated");
        
        if (_localRoomState != null)
        {
            UpdateLocalPlayerState(playerInfo, PlayerUpdateType.Modified);
            UpdatePlayerVersion(playerInfo.PlayerId);
        }
        
        CompleteSyncOperation("player_updated");
    }

    private void HandleHostChanged(string newHostId)
    {
        StartSyncOperation("host_changed");
        
        if (_localRoomState != null)
        {
            UpdateLocalHostState(newHostId);
        }
        
        CompleteSyncOperation("host_changed");
        Debug.Log($"[RoomStateSynchronizer] Host changed synchronized: {newHostId}");
    }
    #endregion

    #region Network Event Handlers
    private void HandleNetworkRoomResponse(RoomProtocolExtension.RoomResponse response)
    {
        if (!response.success)
        {
            HandleSyncError(response.errorCode, response.errorMessage);
            return;
        }

        StartSyncOperation("network_response");
        
        if (response.roomData != null)
        {
            SynchronizeWithServerData(response.roomData);
        }
        
        CompleteSyncOperation("network_response");
    }

    private void HandleNetworkRoomStateSync(RoomProtocolExtension.RoomStateSyncData syncData)
    {
        StartSyncOperation("state_sync");
        
        if (ValidateServerSync(syncData))
        {
            _lastServerSync = syncData;
            SynchronizeWithServerState(syncData);
            SetSyncStatus(true);
            
            Debug.Log($"[RoomStateSynchronizer] Server state synchronized: {syncData.roomCode} v{syncData.syncVersion}");
        }
        else
        {
            Debug.LogWarning($"[RoomStateSynchronizer] Invalid server sync data: {syncData.roomCode}");
            OnSyncConflict?.Invoke(syncData.roomCode, RoomConflictType.InvalidServerData);
        }
        
        CompleteSyncOperation("state_sync");
    }

    private void HandleNetworkPlayerJoined(string roomCode, PlayerInfo playerInfo)
    {
        if (_localRoomState?.RoomCode != roomCode) return;
        
        StartSyncOperation("network_player_joined");
        
        // 로컬 상태와 비교하여 중복 확인
        if (!_localRoomState.HasPlayer(playerInfo.PlayerId))
        {
            UpdateLocalPlayerState(playerInfo, PlayerUpdateType.Added);
            UpdatePlayerVersion(playerInfo.PlayerId);
        }
        
        CompleteSyncOperation("network_player_joined");
    }

    private void HandleNetworkPlayerLeft(string roomCode, PlayerInfo playerInfo)
    {
        if (_localRoomState?.RoomCode != roomCode) return;
        
        StartSyncOperation("network_player_left");
        
        if (_localRoomState.HasPlayer(playerInfo.PlayerId))
        {
            UpdateLocalPlayerState(playerInfo, PlayerUpdateType.Removed);
            RemovePlayerVersion(playerInfo.PlayerId);
        }
        
        CompleteSyncOperation("network_player_left");
    }

    private void HandleNetworkHostChanged(string roomCode, string newHostId)
    {
        if (_localRoomState?.RoomCode != roomCode) return;
        
        StartSyncOperation("network_host_changed");
        
        UpdateLocalHostState(newHostId);
        
        CompleteSyncOperation("network_host_changed");
    }

    private void HandleNetworkConnectionChanged(bool isConnected)
    {
        if (!isConnected)
        {
            SetSyncStatus(false);
            Debug.LogWarning("[RoomStateSynchronizer] Lost network connection, sync disabled");
        }
        else
        {
            // 재연결 시 상태 동기화 요청
            if (_localRoomState != null)
            {
                StartCoroutine(RequestSyncAfterReconnection());
            }
        }
    }
    #endregion

    #region State Synchronization
    private void SynchronizeWithServerData(RoomData serverData)
    {
        if (_localRoomState == null)
        {
            _localRoomState = serverData.DeepCopy();
            InitializePlayerVersions(serverData.Players);
            return;
        }

        // 상태 비교 및 동기화
        if (DetectConflict(serverData))
        {
            ResolveStateConflict(serverData);
        }
        else
        {
            _localRoomState = serverData.DeepCopy();
            InitializePlayerVersions(serverData.Players);
        }
    }

    private void SynchronizeWithServerState(RoomProtocolExtension.RoomStateSyncData syncData)
    {
        if (_localRoomState == null)
        {
            _localRoomState = syncData.roomData.DeepCopy();
            InitializePlayerVersions(syncData.players);
            return;
        }

        // 버전 기반 동기화
        if (syncData.syncVersion > (_lastServerSync?.syncVersion ?? 0))
        {
            _localRoomState = syncData.roomData.DeepCopy();
            SyncPlayerList(syncData.players);
            _lastServerSync = syncData;
        }
        else
        {
            Debug.LogWarning($"[RoomStateSynchronizer] Older sync data received: v{syncData.syncVersion}");
        }
    }

    private void SyncPlayerList(List<PlayerInfo> serverPlayers)
    {
        if (_localRoomState == null) return;

        // 서버 플레이어 목록으로 로컬 상태 업데이트
        _localRoomState.ClearPlayers();
        foreach (var player in serverPlayers)
        {
            _localRoomState.AddPlayer(player);
        }

        // 플레이어 버전 재설정
        InitializePlayerVersions(serverPlayers);
    }

    private bool DetectConflict(RoomData newData)
    {
        if (_localRoomState == null) return false;

        // 기본 충돌 검사
        if (_localRoomState.RoomCode != newData.RoomCode) return true;
        if (_localRoomState.Status != newData.Status && IsConflictingStatus(_localRoomState.Status, newData.Status)) return true;
        if (_localRoomState.HostPlayerId != newData.HostPlayerId) return true;

        return false;
    }

    private void ResolveStateConflict(RoomData conflictData)
    {
        Debug.LogWarning($"[RoomStateSynchronizer] State conflict detected in room {conflictData.RoomCode}");
        
        var conflictType = DetermineConflictType(conflictData);
        OnSyncConflict?.Invoke(conflictData.RoomCode, conflictType);
        
        // 서버 상태 요청으로 충돌 해결
        RequestServerStateSync(conflictData.RoomCode);
        
        // 충돌 해결 시간 기록
        _conflictResolvedTimes[conflictData.RoomCode] = DateTime.UtcNow;
    }

    private RoomConflictType DetermineConflictType(RoomData conflictData)
    {
        if (_localRoomState.HostPlayerId != conflictData.HostPlayerId)
            return RoomConflictType.HostConflict;
        
        if (_localRoomState.Status != conflictData.Status)
            return RoomConflictType.StatusConflict;
        
        if (_localRoomState.CurrentPlayerCount != conflictData.CurrentPlayerCount)
            return RoomConflictType.PlayerCountConflict;
        
        return RoomConflictType.GeneralConflict;
    }

    private bool IsConflictingStatus(RoomStatus local, RoomStatus remote)
    {
        // 특정 상태 전환은 충돌로 간주하지 않음
        if (local == RoomStatus.Waiting && remote == RoomStatus.Starting) return false;
        if (local == RoomStatus.Starting && remote == RoomStatus.InGame) return false;
        
        return local != remote;
    }
    #endregion

    #region Player State Management
    private void UpdateLocalPlayerState(PlayerInfo playerInfo, PlayerUpdateType updateType)
    {
        if (_localRoomState == null) return;

        switch (updateType)
        {
            case PlayerUpdateType.Added:
                if (!_localRoomState.HasPlayer(playerInfo.PlayerId))
                {
                    _localRoomState.AddPlayer(playerInfo);
                }
                break;
                
            case PlayerUpdateType.Modified:
                _localRoomState.UpdatePlayer(playerInfo);
                break;
                
            case PlayerUpdateType.Removed:
                _localRoomState.RemovePlayer(playerInfo.PlayerId);
                break;
        }
    }

    private void UpdateLocalHostState(string newHostId)
    {
        if (_localRoomState == null) return;

        // 기존 방장 해제
        foreach (var player in _localRoomState.Players)
        {
            player.IsHost = player.PlayerId == newHostId;
        }

        _localRoomState.HostPlayerId = newHostId;
    }

    private void InitializePlayerVersions(List<PlayerInfo> players)
    {
        _playerVersions.Clear();
        foreach (var player in players)
        {
            _playerVersions[player.PlayerId] = 1;
        }
    }

    private void UpdatePlayerVersion(string playerId)
    {
        if (_playerVersions.ContainsKey(playerId))
        {
            _playerVersions[playerId]++;
        }
        else
        {
            _playerVersions[playerId] = 1;
        }
    }

    private void RemovePlayerVersion(string playerId)
    {
        _playerVersions.Remove(playerId);
    }
    #endregion

    #region Sync Monitoring
    private IEnumerator SyncMonitorCoroutine()
    {
        while (_isInitialized)
        {
            yield return new WaitForSeconds(SYNC_INTERVAL);
            
            if (_localRoomState != null && _networkHandler.IsConnected)
            {
                CheckSyncHealth();
                
                // 주기적으로 서버와 동기화 상태 확인
                if (!_isSyncInProgress)
                {
                    RequestServerStateSync(_localRoomState.RoomCode);
                }
            }
        }
    }

    private void CheckSyncHealth()
    {
        // 대기 중인 작업이 너무 많은지 확인
        if (_pendingOperations.Count > 10)
        {
            Debug.LogWarning($"[RoomStateSynchronizer] Too many pending operations: {_pendingOperations.Count}");
        }

        // 오래된 충돌 해결 기록 정리
        CleanupOldConflictRecords();
    }

    private void CleanupOldConflictRecords()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);
        var expiredEntries = _conflictResolvedTimes.Where(kvp => kvp.Value < cutoffTime).ToList();
        
        foreach (var entry in expiredEntries)
        {
            _conflictResolvedTimes.Remove(entry.Key);
        }
    }
    #endregion

    #region Performance Tracking
    private void StartSyncOperation(string operation)
    {
        _operationStartTimes[operation] = DateTime.UtcNow;
        _isSyncInProgress = true;
    }

    private void CompleteSyncOperation(string operation)
    {
        _isSyncInProgress = false;
        
        if (_operationStartTimes.TryGetValue(operation, out var startTime))
        {
            var duration = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            _operationStartTimes.Remove(operation);
            
            if (duration > TARGET_SYNC_TIME)
            {
                Debug.LogWarning($"[RoomStateSynchronizer] Slow sync operation: {operation} took {duration:F2}s");
                OnSyncPerformanceWarning?.Invoke(operation, duration);
            }
        }
    }

    private void HandleSyncError(string errorCode, string errorMessage)
    {
        Debug.LogError($"[RoomStateSynchronizer] Sync error: {errorCode} - {errorMessage}");
        SetSyncStatus(false);
        
        // 에러 후 재동기화 시도
        if (_localRoomState != null)
        {
            StartCoroutine(RetrySyncAfterError());
        }
    }

    private IEnumerator RetrySyncAfterError()
    {
        yield return new WaitForSeconds(2f);
        
        if (_localRoomState != null && _networkHandler.IsConnected)
        {
            RequestServerStateSync(_localRoomState.RoomCode);
        }
    }

    private IEnumerator RequestSyncAfterReconnection()
    {
        yield return new WaitForSeconds(1f);
        
        if (_localRoomState != null && _networkHandler.IsConnected)
        {
            RequestServerStateSync(_localRoomState.RoomCode);
            SetSyncStatus(true);
            Debug.Log("[RoomStateSynchronizer] Requesting sync after reconnection");
        }
    }
    #endregion

    #region Utility Methods
    private void RequestServerStateSync(string roomCode)
    {
        if (!_networkHandler.IsConnected)
        {
            Debug.LogWarning("[RoomStateSynchronizer] Cannot request sync: not connected");
            return;
        }

        _networkHandler.RequestRoomStateSync(roomCode);
    }

    private bool ValidateServerSync(RoomProtocolExtension.RoomStateSyncData syncData)
    {
        return RoomProtocolExtension.ValidateSyncData(syncData);
    }

    private void SetSyncStatus(bool isSynchronized)
    {
        if (_isSynchronized != isSynchronized)
        {
            _isSynchronized = isSynchronized;
            OnSyncStatusChanged?.Invoke(isSynchronized);
        }
    }

    private void CleanupRoomState()
    {
        _localRoomState = null;
        _lastServerSync = null;
        _playerVersions.Clear();
        _pendingOperations.Clear();
        _conflictResolvedTimes.Clear();
    }

    /// <summary>동기화 상태 정보</summary>
    public Dictionary<string, object> GetSyncStatus()
    {
        return new Dictionary<string, object>
        {
            {"initialized", _isInitialized},
            {"synchronized", _isSynchronized},
            {"syncInProgress", _isSyncInProgress},
            {"localRoomCode", _localRoomState?.RoomCode ?? ""},
            {"playerCount", _localRoomState?.CurrentPlayerCount ?? 0},
            {"playerVersions", _playerVersions.Count},
            {"pendingOperations", _pendingOperations.Count},
            {"lastSyncVersion", _lastServerSync?.syncVersion ?? 0}
        };
    }

    /// <summary>강제 동기화 요청</summary>
    public void ForceSyncRequest()
    {
        if (_localRoomState != null && _networkHandler.IsConnected)
        {
            RequestServerStateSync(_localRoomState.RoomCode);
            Debug.Log("[RoomStateSynchronizer] Force sync requested");
        }
    }
    #endregion
}

#region Supporting Data Structures
/// <summary>플레이어 업데이트 타입</summary>
public enum PlayerUpdateType
{
    Added,
    Modified,
    Removed
}

/// <summary>방 충돌 타입</summary>
public enum RoomConflictType
{
    GeneralConflict,
    HostConflict,
    StatusConflict,
    PlayerCountConflict,
    InvalidServerData
}

/// <summary>대기 중인 동기화 작업</summary>
public class PendingSyncOperation
{
    public string OperationType { get; set; }
    public string RoomCode { get; set; }
    public DateTime Timestamp { get; set; }
    public object Data { get; set; }
    public int RetryCount { get; set; } = 0;
}
#endregion