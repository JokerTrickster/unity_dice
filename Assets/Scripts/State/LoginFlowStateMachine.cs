using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그인 플로우 상태 머신
/// 로그인 프로세스의 모든 상태와 전환을 관리합니다.
/// </summary>
public class LoginFlowStateMachine : MonoBehaviour
{
    #region Singleton
    private static LoginFlowStateMachine _instance;
    public static LoginFlowStateMachine Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("LoginFlowStateMachine");
                _instance = go.AddComponent<LoginFlowStateMachine>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// 상태가 변경될 때 발생하는 이벤트
    /// </summary>
    public static event Action<LoginState, LoginState> OnStateChanged;
    
    /// <summary>
    /// 상태 전환이 실패할 때 발생하는 이벤트
    /// </summary>
    public static event Action<LoginState, LoginState, string> OnTransitionFailed;
    
    /// <summary>
    /// 오류 상태에 진입할 때 발생하는 이벤트
    /// </summary>
    public static event Action<LoginState, string> OnErrorStateEntered;
    
    /// <summary>
    /// 상태가 복원될 때 발생하는 이벤트
    /// </summary>
    public static event Action<LoginState> OnStateRestored;
    #endregion

    #region Properties
    private LoginState _currentState = LoginState.NotInitialized;
    private LoginState _previousState = LoginState.NotInitialized;
    private readonly Dictionary<LoginState, HashSet<LoginState>> _validTransitions = new();
    private readonly StateManager _stateManager = new();
    private bool _isInitialized = false;
    
    /// <summary>
    /// 현재 로그인 상태
    /// </summary>
    public LoginState CurrentState => _currentState;
    
    /// <summary>
    /// 이전 로그인 상태
    /// </summary>
    public LoginState PreviousState => _previousState;
    
    /// <summary>
    /// 상태 머신 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;
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
        InitializeStateMachine();
    }

    private void OnDestroy()
    {
        SaveCurrentState();
        OnStateChanged = null;
        OnTransitionFailed = null;
        OnErrorStateEntered = null;
        OnStateRestored = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 상태 머신 초기화
    /// </summary>
    private void InitializeStateMachine()
    {
        SetupValidTransitions();
        RestoreStateIfExists();
        
        if (_currentState == LoginState.NotInitialized)
        {
            ChangeState(LoginState.Initializing);
        }
        
        _isInitialized = true;
        Debug.Log($"[LoginFlowStateMachine] Initialized with state: {_currentState}");
    }

    /// <summary>
    /// 유효한 상태 전환 규칙 설정
    /// </summary>
    private void SetupValidTransitions()
    {
        _validTransitions.Clear();
        
        // NotInitialized -> Initializing
        AddValidTransition(LoginState.NotInitialized, LoginState.Initializing);
        
        // Initializing -> Ready, Error
        AddValidTransition(LoginState.Initializing, LoginState.Ready);
        AddValidTransition(LoginState.Initializing, LoginState.Error);
        
        // Ready -> Authenticating
        AddValidTransition(LoginState.Ready, LoginState.Authenticating);
        
        // Authenticating -> Success, Failed, Error
        AddValidTransition(LoginState.Authenticating, LoginState.Success);
        AddValidTransition(LoginState.Authenticating, LoginState.Failed);
        AddValidTransition(LoginState.Authenticating, LoginState.Error);
        
        // Failed -> Ready, Authenticating
        AddValidTransition(LoginState.Failed, LoginState.Ready);
        AddValidTransition(LoginState.Failed, LoginState.Authenticating);
        
        // Success -> Ready (for logout), NicknameSetup
        AddValidTransition(LoginState.Success, LoginState.Ready);
        AddValidTransition(LoginState.Success, LoginState.NicknameSetup);
        AddValidTransition(LoginState.Success, LoginState.Complete);
        
        // NicknameSetup -> Success, Failed, Complete
        AddValidTransition(LoginState.NicknameSetup, LoginState.Success);
        AddValidTransition(LoginState.NicknameSetup, LoginState.Failed);
        AddValidTransition(LoginState.NicknameSetup, LoginState.Complete);
        
        // Complete -> Ready (for logout)
        AddValidTransition(LoginState.Complete, LoginState.Ready);
        
        // Error -> Ready, Initializing (recovery)
        AddValidTransition(LoginState.Error, LoginState.Ready);
        AddValidTransition(LoginState.Error, LoginState.Initializing);
        
        Debug.Log($"[LoginFlowStateMachine] Setup {_validTransitions.Count} state transition rules");
    }

    /// <summary>
    /// 유효한 전환 추가
    /// </summary>
    private void AddValidTransition(LoginState from, LoginState to)
    {
        if (!_validTransitions.ContainsKey(from))
        {
            _validTransitions[from] = new HashSet<LoginState>();
        }
        _validTransitions[from].Add(to);
    }
    #endregion

    #region State Management
    /// <summary>
    /// 상태 변경
    /// </summary>
    public bool ChangeState(LoginState newState, string context = "")
    {
        if (!_isInitialized && newState != LoginState.Initializing)
        {
            Debug.LogWarning($"[LoginFlowStateMachine] Attempted to change state before initialization: {newState}");
            return false;
        }
        
        if (!IsValidTransition(_currentState, newState))
        {
            string errorMessage = $"Invalid transition from {_currentState} to {newState}";
            Debug.LogError($"[LoginFlowStateMachine] {errorMessage}");
            OnTransitionFailed?.Invoke(_currentState, newState, errorMessage);
            return false;
        }

        LoginState oldState = _currentState;
        _previousState = _currentState;
        _currentState = newState;
        
        Debug.Log($"[LoginFlowStateMachine] State changed: {oldState} -> {newState}" + 
                 (!string.IsNullOrEmpty(context) ? $" ({context})" : ""));
        
        // 상태별 특수 처리
        HandleStateSpecificLogic(newState, context);
        
        // 상태 저장
        SaveCurrentState();
        
        // 이벤트 발생
        OnStateChanged?.Invoke(oldState, newState);
        
        return true;
    }

    /// <summary>
    /// 유효한 전환인지 확인
    /// </summary>
    public bool IsValidTransition(LoginState from, LoginState to)
    {
        return _validTransitions.ContainsKey(from) && _validTransitions[from].Contains(to);
    }

    /// <summary>
    /// 상태별 특수 로직 처리
    /// </summary>
    private void HandleStateSpecificLogic(LoginState state, string context)
    {
        switch (state)
        {
            case LoginState.Error:
                OnErrorStateEntered?.Invoke(state, context);
                break;
                
            case LoginState.Success:
                // 성공 시 자동 진행 로직
                if (ShouldProceedToNicknameSetup())
                {
                    ChangeState(LoginState.NicknameSetup, "Auto-transition to nickname setup");
                }
                else
                {
                    ChangeState(LoginState.Complete, "Authentication complete");
                }
                break;
                
            case LoginState.Complete:
                // 완료 상태 처리
                ClearErrorHistory();
                break;
        }
    }

    /// <summary>
    /// 닉네임 설정 단계로 진행해야 하는지 확인
    /// </summary>
    private bool ShouldProceedToNicknameSetup()
    {
        // 사용자 데이터를 확인하여 닉네임이 필요한지 판단
        var userDataManager = UserDataManager.Instance;
        if (userDataManager == null) return true;
        
        var userData = userDataManager.GetUserData();
        return userData == null || string.IsNullOrEmpty(userData.Nickname);
    }

    /// <summary>
    /// 오류 이력 정리
    /// </summary>
    private void ClearErrorHistory()
    {
        _stateManager.ClearErrorHistory();
    }
    #endregion

    #region State Persistence
    /// <summary>
    /// 현재 상태 저장
    /// </summary>
    private void SaveCurrentState()
    {
        _stateManager.SaveState(_currentState, _previousState);
    }

    /// <summary>
    /// 저장된 상태가 있다면 복원
    /// </summary>
    private void RestoreStateIfExists()
    {
        var savedState = _stateManager.LoadState();
        if (savedState.HasValue && IsValidStateForRestore(savedState.Value))
        {
            _currentState = savedState.Value;
            Debug.Log($"[LoginFlowStateMachine] State restored: {_currentState}");
            OnStateRestored?.Invoke(_currentState);
        }
    }

    /// <summary>
    /// 복원 가능한 상태인지 확인
    /// </summary>
    private bool IsValidStateForRestore(LoginState state)
    {
        // 일시적 상태들은 복원하지 않음
        return state != LoginState.Authenticating && 
               state != LoginState.NotInitialized &&
               state != LoginState.Initializing;
    }

    /// <summary>
    /// 수동 상태 복원
    /// </summary>
    public void RestoreState()
    {
        RestoreStateIfExists();
    }

    /// <summary>
    /// 상태 정보 클리어
    /// </summary>
    public void ClearSavedState()
    {
        _stateManager.ClearSavedState();
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 현재 상태에서 가능한 다음 상태들 반환
    /// </summary>
    public HashSet<LoginState> GetValidNextStates()
    {
        return _validTransitions.ContainsKey(_currentState) 
            ? new HashSet<LoginState>(_validTransitions[_currentState]) 
            : new HashSet<LoginState>();
    }

    /// <summary>
    /// 오류 상태로 전환
    /// </summary>
    public bool TransitionToError(string errorMessage)
    {
        return ChangeState(LoginState.Error, errorMessage);
    }

    /// <summary>
    /// 준비 상태로 리셋
    /// </summary>
    public bool ResetToReady()
    {
        if (!IsValidTransition(_currentState, LoginState.Ready))
        {
            // 오류 상태를 거쳐서 Ready로 전환
            if (ChangeState(LoginState.Error, "Reset requested"))
            {
                return ChangeState(LoginState.Ready, "Reset to ready");
            }
            return false;
        }
        
        return ChangeState(LoginState.Ready, "Reset to ready");
    }

    /// <summary>
    /// 현재 상태 설명 반환
    /// </summary>
    public string GetStateDescription()
    {
        return _currentState switch
        {
            LoginState.NotInitialized => "시스템이 초기화되지 않음",
            LoginState.Initializing => "시스템 초기화 중",
            LoginState.Ready => "로그인 준비 완료",
            LoginState.Authenticating => "인증 진행 중",
            LoginState.Success => "인증 성공",
            LoginState.Failed => "인증 실패",
            LoginState.NicknameSetup => "닉네임 설정 중",
            LoginState.Complete => "로그인 완료",
            LoginState.Error => "오류 상태",
            _ => "알 수 없는 상태"
        };
    }
    #endregion
}

#region Enums and Data Classes
/// <summary>
/// 로그인 플로우 상태
/// </summary>
public enum LoginState
{
    /// <summary>초기화되지 않음</summary>
    NotInitialized,
    
    /// <summary>시스템 초기화 중</summary>
    Initializing,
    
    /// <summary>로그인 준비 완료</summary>
    Ready,
    
    /// <summary>인증 진행 중</summary>
    Authenticating,
    
    /// <summary>인증 성공</summary>
    Success,
    
    /// <summary>인증 실패</summary>
    Failed,
    
    /// <summary>닉네임 설정</summary>
    NicknameSetup,
    
    /// <summary>로그인 완료</summary>
    Complete,
    
    /// <summary>오류 상태</summary>
    Error
}

/// <summary>
/// 상태 관리 헬퍼 클래스
/// </summary>
public class StateManager
{
    private const string STATE_KEY = "LoginFlowState";
    private const string PREVIOUS_STATE_KEY = "LoginFlowPreviousState";
    private const string ERROR_HISTORY_KEY = "LoginFlowErrorHistory";
    
    /// <summary>
    /// 상태 저장
    /// </summary>
    public void SaveState(LoginState currentState, LoginState previousState)
    {
        PlayerPrefs.SetInt(STATE_KEY, (int)currentState);
        PlayerPrefs.SetInt(PREVIOUS_STATE_KEY, (int)previousState);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 상태 로드
    /// </summary>
    public LoginState? LoadState()
    {
        if (PlayerPrefs.HasKey(STATE_KEY))
        {
            return (LoginState)PlayerPrefs.GetInt(STATE_KEY);
        }
        return null;
    }
    
    /// <summary>
    /// 저장된 상태 클리어
    /// </summary>
    public void ClearSavedState()
    {
        PlayerPrefs.DeleteKey(STATE_KEY);
        PlayerPrefs.DeleteKey(PREVIOUS_STATE_KEY);
        PlayerPrefs.DeleteKey(ERROR_HISTORY_KEY);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 오류 이력 클리어
    /// </summary>
    public void ClearErrorHistory()
    {
        PlayerPrefs.DeleteKey(ERROR_HISTORY_KEY);
        PlayerPrefs.Save();
    }
}
#endregion