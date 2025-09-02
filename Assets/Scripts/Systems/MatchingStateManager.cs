using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 상태 관리자
/// 매칭 상태 전환, 검증, 타임아웃, 지속성을 관리합니다.
/// </summary>
public class MatchingStateManager : MonoBehaviour
{
    #region Events
    /// <summary>상태가 변경될 때 발생하는 이벤트</summary>
    public static event Action<MatchingState, MatchingState> OnStateChanged; // previousState, newState
    
    /// <summary>상태 전환이 실패했을 때 발생하는 이벤트</summary>
    public static event Action<MatchingState, MatchingState, string> OnStateTransitionFailed; // from, to, reason
    
    /// <summary>타임아웃이 발생했을 때 발생하는 이벤트</summary>
    public static event Action<MatchingState> OnStateTimeout;
    
    /// <summary>상태가 저장될 때 발생하는 이벤트</summary>
    public static event Action<MatchingStateInfo> OnStateSaved;
    
    /// <summary>상태가 복원될 때 발생하는 이벤트</summary>
    public static event Action<MatchingStateInfo> OnStateRestored;
    #endregion

    #region Private Fields
    private MatchingStateInfo _currentStateInfo;
    private MatchingConfig _config;
    private Dictionary<MatchingState, HashSet<MatchingState>> _validTransitions;
    private Dictionary<MatchingState, float> _stateTimeouts;
    private Coroutine _timeoutCoroutine;
    private bool _isInitialized = false;
    
    // Persistence keys
    private const string STATE_PERSISTENCE_KEY = "MatchingState";
    private const string LAST_STATE_TIME_KEY = "LastStateTime";
    #endregion

    #region Properties
    /// <summary>현재 상태 정보</summary>
    public MatchingStateInfo CurrentState => _currentStateInfo;
    
    /// <summary>현재 상태</summary>
    public MatchingState State => _currentStateInfo?.currentState ?? MatchingState.Idle;
    
    /// <summary>초기화 완료 여부</summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>상태 변경 가능 여부</summary>
    public bool CanChangeState { get; private set; } = true;
    #endregion

    #region Initialization
    /// <summary>
    /// 상태 관리자 초기화
    /// </summary>
    /// <param name="config">매칭 설정</param>
    public void Initialize(MatchingConfig config)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[MatchingStateManager] Already initialized");
            return;
        }
        
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _currentStateInfo = new MatchingStateInfo();
        
        SetupValidTransitions();
        SetupStateTimeouts();
        RestoreStateFromPersistence();
        
        _isInitialized = true;
        
        if (_config.EnableDebugLogs)
        {
            Debug.Log("[MatchingStateManager] Initialized successfully");
        }
    }
    
    /// <summary>
    /// 유효한 상태 전환 설정
    /// </summary>
    private void SetupValidTransitions()
    {
        _validTransitions = new Dictionary<MatchingState, HashSet<MatchingState>>
        {
            [MatchingState.Idle] = new HashSet<MatchingState> 
            { 
                MatchingState.Searching 
            },
            
            [MatchingState.Searching] = new HashSet<MatchingState> 
            { 
                MatchingState.Found, 
                MatchingState.Cancelled, 
                MatchingState.Failed 
            },
            
            [MatchingState.Found] = new HashSet<MatchingState> 
            { 
                MatchingState.Starting, 
                MatchingState.Cancelled, 
                MatchingState.Failed 
            },
            
            [MatchingState.Starting] = new HashSet<MatchingState> 
            { 
                MatchingState.Idle, // 게임 시작 완료 후 대기 상태로
                MatchingState.Failed 
            },
            
            [MatchingState.Cancelled] = new HashSet<MatchingState> 
            { 
                MatchingState.Idle 
            },
            
            [MatchingState.Failed] = new HashSet<MatchingState> 
            { 
                MatchingState.Idle, 
                MatchingState.Searching // 재시도 가능
            }
        };
    }
    
    /// <summary>
    /// 상태별 타임아웃 설정
    /// </summary>
    private void SetupStateTimeouts()
    {
        _stateTimeouts = new Dictionary<MatchingState, float>
        {
            [MatchingState.Searching] = _config.MaxWaitTimeSeconds,
            [MatchingState.Found] = 30f, // 매칭 완료 후 30초 내에 게임 시작 확인
            [MatchingState.Starting] = 15f // 게임 시작 과정 15초 제한
        };
    }
    #endregion

    #region State Management
    /// <summary>
    /// 상태 변경
    /// </summary>
    /// <param name="newState">새로운 상태</param>
    /// <param name="reason">변경 이유 (디버깅용)</param>
    /// <returns>변경 성공 여부</returns>
    public bool ChangeState(MatchingState newState, string reason = "")
    {
        if (!_isInitialized)
        {
            Debug.LogError("[MatchingStateManager] Not initialized");
            return false;
        }
        
        if (!CanChangeState)
        {
            Debug.LogWarning("[MatchingStateManager] State changes are currently disabled");
            return false;
        }
        
        MatchingState currentState = _currentStateInfo.currentState;
        
        // 같은 상태로의 전환은 무시
        if (currentState == newState)
        {
            if (_config.EnableDebugLogs)
            {
                Debug.Log($"[MatchingStateManager] Already in state {newState}");
            }
            return true;
        }
        
        // 유효한 전환인지 검사
        if (!IsValidTransition(currentState, newState))
        {
            string error = $"Invalid state transition from {currentState} to {newState}";
            Debug.LogError($"[MatchingStateManager] {error}");
            OnStateTransitionFailed?.Invoke(currentState, newState, error);
            return false;
        }
        
        // 상태 전환 실행
        MatchingState previousState = currentState;
        _currentStateInfo.currentState = newState;
        
        // 상태별 추가 처리
        ProcessStateEntry(newState, reason);
        
        // 타임아웃 관리
        UpdateTimeoutCoroutine(newState);
        
        // 상태 지속성 저장
        SaveStateToPersistence();
        
        // 이벤트 발생
        OnStateChanged?.Invoke(previousState, newState);
        
        if (_config.EnableDebugLogs)
        {
            Debug.Log($"[MatchingStateManager] State changed: {previousState} → {newState}" + 
                      (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
        }
        
        return true;
    }
    
    /// <summary>
    /// 유효한 상태 전환인지 확인
    /// </summary>
    /// <param name="from">현재 상태</param>
    /// <param name="to">목표 상태</param>
    /// <returns>유효한 전환 여부</returns>
    public bool IsValidTransition(MatchingState from, MatchingState to)
    {
        return _validTransitions.ContainsKey(from) && _validTransitions[from].Contains(to);
    }
    
    /// <summary>
    /// 특정 상태로 전환 가능한지 확인
    /// </summary>
    /// <param name="targetState">목표 상태</param>
    /// <returns>전환 가능 여부</returns>
    public bool CanTransitionTo(MatchingState targetState)
    {
        return IsValidTransition(_currentStateInfo.currentState, targetState);
    }
    
    /// <summary>
    /// 현재 상태에서 가능한 다음 상태들 가져오기
    /// </summary>
    /// <returns>가능한 다음 상태 목록</returns>
    public HashSet<MatchingState> GetPossibleNextStates()
    {
        var currentState = _currentStateInfo.currentState;
        return _validTransitions.ContainsKey(currentState) 
            ? new HashSet<MatchingState>(_validTransitions[currentState])
            : new HashSet<MatchingState>();
    }
    #endregion

    #region State Processing
    /// <summary>
    /// 상태 진입 시 처리
    /// </summary>
    /// <param name="state">진입한 상태</param>
    /// <param name="reason">진입 이유</param>
    private void ProcessStateEntry(MatchingState state, string reason)
    {
        switch (state)
        {
            case MatchingState.Idle:
                ProcessIdleEntry();
                break;
                
            case MatchingState.Searching:
                ProcessSearchingEntry();
                break;
                
            case MatchingState.Found:
                ProcessFoundEntry();
                break;
                
            case MatchingState.Starting:
                ProcessStartingEntry();
                break;
                
            case MatchingState.Cancelled:
                ProcessCancelledEntry(reason);
                break;
                
            case MatchingState.Failed:
                ProcessFailedEntry(reason);
                break;
        }
    }
    
    /// <summary>
    /// 대기 상태 진입 처리
    /// </summary>
    private void ProcessIdleEntry()
    {
        _currentStateInfo.searchStartTime = 0f;
        _currentStateInfo.estimatedWaitTime = 0f;
        _currentStateInfo.currentRoomCode = "";
        _currentStateInfo.matchedPlayers.Clear();
        _currentStateInfo.lastErrorMessage = "";
    }
    
    /// <summary>
    /// 검색 중 상태 진입 처리
    /// </summary>
    private void ProcessSearchingEntry()
    {
        _currentStateInfo.searchStartTime = Time.time;
        _currentStateInfo.estimatedWaitTime = GetEstimatedWaitTime();
        _currentStateInfo.matchedPlayers.Clear();
    }
    
    /// <summary>
    /// 매칭 완료 상태 진입 처리
    /// </summary>
    private void ProcessFoundEntry()
    {
        // 매칭된 플레이어 정보는 외부에서 설정됨
        if (_config.EnableDebugLogs)
        {
            Debug.Log($"[MatchingStateManager] Match found with {_currentStateInfo.matchedPlayers.Count} players");
        }
    }
    
    /// <summary>
    /// 게임 시작 중 상태 진입 처리
    /// </summary>
    private void ProcessStartingEntry()
    {
        if (_config.EnableDebugLogs)
        {
            Debug.Log("[MatchingStateManager] Game is starting...");
        }
    }
    
    /// <summary>
    /// 취소됨 상태 진입 처리
    /// </summary>
    /// <param name="reason">취소 이유</param>
    private void ProcessCancelledEntry(string reason)
    {
        _currentStateInfo.lastErrorMessage = $"Matching cancelled: {reason}";
        
        if (_config.EnableDebugLogs)
        {
            Debug.Log($"[MatchingStateManager] Matching cancelled: {reason}");
        }
    }
    
    /// <summary>
    /// 실패 상태 진입 처리
    /// </summary>
    /// <param name="reason">실패 이유</param>
    private void ProcessFailedEntry(string reason)
    {
        _currentStateInfo.lastErrorMessage = $"Matching failed: {reason}";
        
        Debug.LogWarning($"[MatchingStateManager] Matching failed: {reason}");
    }
    #endregion

    #region Timeout Management
    /// <summary>
    /// 타임아웃 코루틴 업데이트
    /// </summary>
    /// <param name="state">새로운 상태</param>
    private void UpdateTimeoutCoroutine(MatchingState state)
    {
        // 기존 타임아웃 코루틴 정리
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
        
        // 새 상태에 타임아웃이 있으면 코루틴 시작
        if (_stateTimeouts.ContainsKey(state))
        {
            _timeoutCoroutine = StartCoroutine(TimeoutCoroutine(state, _stateTimeouts[state]));
        }
    }
    
    /// <summary>
    /// 타임아웃 코루틴
    /// </summary>
    /// <param name="state">타임아웃을 감시할 상태</param>
    /// <param name="timeoutSeconds">타임아웃 시간</param>
    /// <returns></returns>
    private IEnumerator TimeoutCoroutine(MatchingState state, float timeoutSeconds)
    {
        yield return new WaitForSeconds(timeoutSeconds);
        
        // 여전히 같은 상태에 있다면 타임아웃 처리
        if (_currentStateInfo.currentState == state)
        {
            HandleStateTimeout(state);
        }
    }
    
    /// <summary>
    /// 상태 타임아웃 처리
    /// </summary>
    /// <param name="state">타임아웃된 상태</param>
    private void HandleStateTimeout(MatchingState state)
    {
        OnStateTimeout?.Invoke(state);
        
        switch (state)
        {
            case MatchingState.Searching:
                ChangeState(MatchingState.Failed, "Search timeout");
                break;
                
            case MatchingState.Found:
                ChangeState(MatchingState.Failed, "Found timeout - no game start confirmation");
                break;
                
            case MatchingState.Starting:
                ChangeState(MatchingState.Failed, "Start timeout - game failed to start");
                break;
        }
    }
    #endregion

    #region State Information Updates
    /// <summary>
    /// 매칭된 플레이어 정보 설정
    /// </summary>
    /// <param name="players">플레이어 목록</param>
    public void SetMatchedPlayers(List<PlayerInfo> players)
    {
        _currentStateInfo.matchedPlayers.Clear();
        if (players != null)
        {
            _currentStateInfo.matchedPlayers.AddRange(players);
        }
    }
    
    /// <summary>
    /// 현재 방 코드 설정
    /// </summary>
    /// <param name="roomCode">방 코드</param>
    public void SetCurrentRoomCode(string roomCode)
    {
        _currentStateInfo.currentRoomCode = roomCode ?? "";
    }
    
    /// <summary>
    /// 선택된 게임 모드 설정
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    public void SetSelectedGameMode(GameMode gameMode)
    {
        _currentStateInfo.selectedGameMode = gameMode;
        _currentStateInfo.estimatedWaitTime = GetEstimatedWaitTime();
    }
    
    /// <summary>
    /// 매칭 타입 설정
    /// </summary>
    /// <param name="matchType">매칭 타입</param>
    public void SetMatchType(MatchType matchType)
    {
        _currentStateInfo.matchType = matchType;
    }
    
    /// <summary>
    /// 선택된 플레이어 수 설정
    /// </summary>
    /// <param name="playerCount">플레이어 수</param>
    public void SetSelectedPlayerCount(int playerCount)
    {
        _currentStateInfo.selectedPlayerCount = Mathf.Clamp(playerCount, 
            _config.MinPlayersPerMatch, _config.MaxPlayersPerMatch);
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 예상 대기 시간 계산
    /// </summary>
    /// <returns>예상 대기 시간 (초)</returns>
    private float GetEstimatedWaitTime()
    {
        if (_config == null) return 30f; // 기본값
        
        var gameModeConfig = _config.GetGameModeConfig(_currentStateInfo.selectedGameMode);
        return gameModeConfig.estimatedWaitTimeSeconds;
    }
    
    /// <summary>
    /// 상태 변경 가능 여부 설정
    /// </summary>
    /// <param name="canChange">변경 가능 여부</param>
    public void SetCanChangeState(bool canChange)
    {
        CanChangeState = canChange;
        
        if (_config.EnableDebugLogs)
        {
            Debug.Log($"[MatchingStateManager] State changes {(canChange ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// 상태 강제 초기화
    /// </summary>
    public void ForceReset()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
        
        _currentStateInfo = new MatchingStateInfo();
        SaveStateToPersistence();
        
        if (_config.EnableDebugLogs)
        {
            Debug.Log("[MatchingStateManager] State forcefully reset to Idle");
        }
    }
    #endregion

    #region Persistence
    /// <summary>
    /// 상태를 지속성 저장소에 저장
    /// </summary>
    private void SaveStateToPersistence()
    {
        try
        {
            string json = JsonUtility.ToJson(_currentStateInfo);
            PlayerPrefs.SetString(STATE_PERSISTENCE_KEY, json);
            PlayerPrefs.SetString(LAST_STATE_TIME_KEY, DateTime.Now.ToBinary().ToString());
            PlayerPrefs.Save();
            
            OnStateSaved?.Invoke(_currentStateInfo);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingStateManager] Failed to save state: {e.Message}");
        }
    }
    
    /// <summary>
    /// 지속성 저장소에서 상태 복원
    /// </summary>
    private void RestoreStateFromPersistence()
    {
        try
        {
            string json = PlayerPrefs.GetString(STATE_PERSISTENCE_KEY, "");
            if (string.IsNullOrEmpty(json)) return;
            
            var restoredState = JsonUtility.FromJson<MatchingStateInfo>(json);
            if (restoredState == null) return;
            
            // 복원된 상태가 검색 중이거나 임시 상태라면 초기화
            if (restoredState.currentState == MatchingState.Searching ||
                restoredState.currentState == MatchingState.Found ||
                restoredState.currentState == MatchingState.Starting)
            {
                restoredState.currentState = MatchingState.Idle;
                restoredState.searchStartTime = 0f;
                restoredState.matchedPlayers.Clear();
                
                if (_config.EnableDebugLogs)
                {
                    Debug.Log("[MatchingStateManager] Reset transient state to Idle on restoration");
                }
            }
            
            _currentStateInfo = restoredState;
            OnStateRestored?.Invoke(_currentStateInfo);
            
            if (_config.EnableDebugLogs)
            {
                Debug.Log($"[MatchingStateManager] State restored: {_currentStateInfo.currentState}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingStateManager] Failed to restore state: {e.Message}");
            _currentStateInfo = new MatchingStateInfo(); // 기본 상태로 초기화
        }
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
        }
        
        // 이벤트 정리
        OnStateChanged = null;
        OnStateTransitionFailed = null;
        OnStateTimeout = null;
        OnStateSaved = null;
        OnStateRestored = null;
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) // 앱이 다시 활성화될 때
        {
            // 검색 중이었던 상태를 초기화
            if (_currentStateInfo?.currentState == MatchingState.Searching)
            {
                ChangeState(MatchingState.Idle, "App resumed from pause");
            }
        }
    }
    #endregion
}