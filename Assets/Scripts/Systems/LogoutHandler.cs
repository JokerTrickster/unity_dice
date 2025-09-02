using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LogoutHandler - 안전한 로그아웃 처리 시스템
/// AuthenticationManager와 연동하여 로그아웃 플로우를 관리하며,
/// 매칭 중 로그아웃 시 안전한 상태 정리를 보장합니다.
/// 5초 이내 로그아웃 완료를 목표로 합니다.
/// </summary>
public class LogoutHandler : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 로그아웃 시작 시 발생하는 이벤트
    /// </summary>
    public static event Action OnLogoutStarted;
    
    /// <summary>
    /// 로그아웃 진행 상태 이벤트
    /// </summary>
    public static event Action<string, float> OnLogoutProgress;
    
    /// <summary>
    /// 로그아웃 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action<bool, string> OnLogoutCompleted;
    #endregion

    #region Private Fields
    private bool _isLogoutInProgress = false;
    private Coroutine _logoutCoroutine = null;
    
    // 로그아웃 타임아웃 설정
    private const float LOGOUT_TIMEOUT_SECONDS = 5f;
    private const float MATCHING_CANCEL_TIMEOUT = 2f;
    private const float WEBSOCKET_DISCONNECT_TIMEOUT = 1f;
    private const string LOGIN_SCENE_NAME = "LoginScene";
    #endregion

    #region Properties
    /// <summary>
    /// 로그아웃 진행 중 여부
    /// </summary>
    public bool IsLogoutInProgress => _isLogoutInProgress;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        if (_logoutCoroutine != null)
        {
            StopCoroutine(_logoutCoroutine);
        }
        
        // 이벤트 정리
        OnLogoutStarted = null;
        OnLogoutProgress = null;
        OnLogoutCompleted = null;
    }
    #endregion

    #region Public API
    /// <summary>
    /// 로그아웃 시작 (확인 다이얼로그 표시)
    /// </summary>
    public void InitiateLogout()
    {
        if (_isLogoutInProgress)
        {
            Debug.LogWarning("[LogoutHandler] Logout already in progress");
            return;
        }

        ShowLogoutConfirmDialog();
    }

    /// <summary>
    /// 즉시 로그아웃 (확인 없이)
    /// </summary>
    public void ForceLogout()
    {
        if (_isLogoutInProgress)
        {
            Debug.LogWarning("[LogoutHandler] Logout already in progress, forcing stop");
            if (_logoutCoroutine != null)
            {
                StopCoroutine(_logoutCoroutine);
            }
        }

        _logoutCoroutine = StartCoroutine(LogoutSequence(false));
    }
    #endregion

    #region Logout Flow
    /// <summary>
    /// 로그아웃 확인 다이얼로그 표시
    /// </summary>
    private void ShowLogoutConfirmDialog()
    {
        try
        {
            // UIManager를 통한 확인 다이얼로그 표시
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirmDialog(
                    title: "로그아웃",
                    message: "정말 로그아웃하시겠습니까?",
                    onConfirm: () => StartLogoutProcess(true),
                    onCancel: null
                );
            }
            else
            {
                // UIManager가 없는 경우 즉시 로그아웃
                Debug.LogWarning("[LogoutHandler] UIManager not available, proceeding with logout");
                StartLogoutProcess(true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to show confirm dialog: {ex.Message}");
            StartLogoutProcess(true);
        }
    }

    /// <summary>
    /// 로그아웃 프로세스 시작
    /// </summary>
    /// <param name="showProgress">진행상황 표시 여부</param>
    private void StartLogoutProcess(bool showProgress)
    {
        _logoutCoroutine = StartCoroutine(LogoutSequence(showProgress));
    }

    /// <summary>
    /// 로그아웃 시퀀스 실행
    /// </summary>
    /// <param name="showProgress">진행상황 표시 여부</param>
    private IEnumerator LogoutSequence(bool showProgress)
    {
        _isLogoutInProgress = true;
        float startTime = Time.time;
        
        try
        {
            Debug.Log("[LogoutHandler] Starting logout sequence...");
            OnLogoutStarted?.Invoke();
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("로그아웃을 시작합니다...", 0.1f);
            }

            // 1. 진행 중인 매칭 취소 (2초 타임아웃)
            yield return StartCoroutine(CancelMatchingWithTimeout());
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("매칭을 취소하고 있습니다...", 0.3f);
            }

            // 2. WebSocket 연결 해제 (1초 타임아웃)
            yield return StartCoroutine(DisconnectWebSocketWithTimeout());
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("연결을 종료하고 있습니다...", 0.6f);
            }

            // 3. 로컬 데이터 정리
            yield return StartCoroutine(ClearLocalDataSafely());
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("사용자 데이터를 정리하고 있습니다...", 0.8f);
            }

            // 4. AuthenticationManager 로그아웃
            yield return StartCoroutine(ExecuteAuthenticationLogout());
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("로그아웃 완료 중...", 0.9f);
            }

            // 5. 로그인 화면으로 전환
            yield return StartCoroutine(NavigateToLoginScene());

            float elapsedTime = Time.time - startTime;
            Debug.Log($"[LogoutHandler] Logout completed successfully in {elapsedTime:F2}s");
            
            if (showProgress)
            {
                OnLogoutProgress?.Invoke("로그아웃 완료", 1.0f);
            }
            
            OnLogoutCompleted?.Invoke(true, "로그아웃이 성공적으로 완료되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Logout failed: {ex.Message}");
            OnLogoutCompleted?.Invoke(false, $"로그아웃 중 오류가 발생했습니다: {ex.Message}");
            
            // 실패 시에도 로그인 화면으로 이동
            yield return StartCoroutine(NavigateToLoginScene());
        }
        finally
        {
            _isLogoutInProgress = false;
            _logoutCoroutine = null;
        }
    }

    /// <summary>
    /// 타임아웃이 있는 매칭 취소
    /// </summary>
    private IEnumerator CancelMatchingWithTimeout()
    {
        try
        {
            // MatchingManager를 통한 매칭 취소
            if (MatchingManager.Instance != null && MatchingManager.Instance.CurrentState != MatchingState.Idle)
            {
                Debug.Log("[LogoutHandler] Cancelling active matching...");
                
                bool cancelled = false;
                float timeout = MATCHING_CANCEL_TIMEOUT;
                
                // 매칭 취소 시도
                MatchingManager.Instance.CancelMatching("User logout");
                
                // 매칭 상태가 Idle이 될 때까지 대기 (타임아웃 포함)
                while (timeout > 0 && MatchingManager.Instance.CurrentState != MatchingState.Idle)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }
                
                if (MatchingManager.Instance.CurrentState == MatchingState.Idle)
                {
                    cancelled = true;
                    Debug.Log("[LogoutHandler] Matching cancelled successfully");
                }
                else
                {
                    Debug.LogWarning("[LogoutHandler] Matching cancel timeout, forcing disconnect");
                }
            }
            else
            {
                Debug.Log("[LogoutHandler] No active matching to cancel");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to cancel matching: {ex.Message}");
        }
    }

    /// <summary>
    /// 타임아웃이 있는 WebSocket 연결 해제
    /// </summary>
    private IEnumerator DisconnectWebSocketWithTimeout()
    {
        try
        {
            // NetworkManager를 통한 WebSocket 연결 해제
            if (NetworkManager.Instance != null)
            {
                Debug.Log("[LogoutHandler] Disconnecting WebSocket...");
                
                NetworkManager.Instance.DisconnectWebSocket();
                
                // 연결 해제 완료까지 대기
                float timeout = WEBSOCKET_DISCONNECT_TIMEOUT;
                while (timeout > 0 && NetworkManager.Instance.IsWebSocketConnected)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }
                
                Debug.Log("[LogoutHandler] WebSocket disconnected");
            }
            else
            {
                Debug.Log("[LogoutHandler] NetworkManager not available");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to disconnect WebSocket: {ex.Message}");
        }
    }

    /// <summary>
    /// 안전한 로컬 데이터 정리
    /// </summary>
    private IEnumerator ClearLocalDataSafely()
    {
        try
        {
            Debug.Log("[LogoutHandler] Clearing local user data...");
            
            // 우편함 캐시 정리
            if (MailboxManager.Instance != null)
            {
                MailboxManager.Instance.ClearCache();
            }
            
            // 민감한 사용자 데이터만 정리 (기본 설정은 유지)
            ClearSensitiveUserData();
            
            yield return new WaitForSeconds(0.2f); // 안전한 처리 시간 확보
            
            Debug.Log("[LogoutHandler] Local data cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to clear local data: {ex.Message}");
        }
    }

    /// <summary>
    /// AuthenticationManager를 통한 로그아웃 실행
    /// </summary>
    private IEnumerator ExecuteAuthenticationLogout()
    {
        try
        {
            Debug.Log("[LogoutHandler] Executing authentication logout...");
            
            if (AuthenticationManager.Instance != null)
            {
                // 로그아웃 완료 이벤트 구독
                bool logoutCompleted = false;
                Action logoutHandler = () => logoutCompleted = true;
                
                AuthenticationManager.OnLogoutCompleted += logoutHandler;
                
                try
                {
                    // AuthenticationManager 로그아웃 실행
                    AuthenticationManager.Instance.Logout();
                    
                    // 로그아웃 완료까지 대기 (최대 2초)
                    float timeout = 2f;
                    while (timeout > 0 && !logoutCompleted)
                    {
                        yield return new WaitForSeconds(0.1f);
                        timeout -= 0.1f;
                    }
                    
                    if (logoutCompleted)
                    {
                        Debug.Log("[LogoutHandler] Authentication logout completed");
                    }
                    else
                    {
                        Debug.LogWarning("[LogoutHandler] Authentication logout timeout");
                    }
                }
                finally
                {
                    AuthenticationManager.OnLogoutCompleted -= logoutHandler;
                }
            }
            else
            {
                Debug.LogWarning("[LogoutHandler] AuthenticationManager not available");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Authentication logout failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그인 화면으로 이동
    /// </summary>
    private IEnumerator NavigateToLoginScene()
    {
        try
        {
            Debug.Log("[LogoutHandler] Navigating to login scene...");
            
            // 씬 전환
            SceneManager.LoadScene(LOGIN_SCENE_NAME);
            
            yield return null; // 씬 로드 시작 대기
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to navigate to login scene: {ex.Message}");
            
            // 씬 로드 실패 시 Application.Quit() 또는 대체 처리
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
    #endregion

    #region Data Cleanup
    /// <summary>
    /// 민감한 사용자 데이터 정리
    /// </summary>
    private void ClearSensitiveUserData()
    {
        try
        {
            // 인증 토큰 및 개인정보 정리
            PlayerPrefs.DeleteKey("AuthToken");
            PlayerPrefs.DeleteKey("RefreshToken");
            PlayerPrefs.DeleteKey("UserId");
            PlayerPrefs.DeleteKey("UserEmail");
            PlayerPrefs.DeleteKey("LastLoginTime");
            
            // 임시 세션 데이터 정리
            PlayerPrefs.DeleteKey("SessionId");
            PlayerPrefs.DeleteKey("MatchingSessionId");
            PlayerPrefs.DeleteKey("CurrentGameRoom");
            
            // 기본 설정은 유지 (음악, 효과음 등)
            // PlayerPrefs.DeleteKey("MusicEnabled"); // 유지
            // PlayerPrefs.DeleteKey("SoundEnabled"); // 유지
            
            PlayerPrefs.Save();
            
            Debug.Log("[LogoutHandler] Sensitive user data cleared");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to clear sensitive data: {ex.Message}");
        }
    }
    #endregion

    #region Error Handling
    /// <summary>
    /// 로그아웃 실패 시 에러 다이얼로그 표시
    /// </summary>
    private void ShowLogoutErrorDialog()
    {
        try
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowErrorDialog(
                    title: "로그아웃 실패",
                    message: "로그아웃 중 문제가 발생했습니다. 다시 시도하시겠습니까?",
                    onRetry: () => StartLogoutProcess(true),
                    onCancel: null
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LogoutHandler] Failed to show error dialog: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그아웃 상태 정보 반환
    /// </summary>
    public LogoutStatus GetStatus()
    {
        return new LogoutStatus
        {
            IsInProgress = _isLogoutInProgress,
            IsAuthenticationManagerAvailable = AuthenticationManager.Instance != null,
            IsNetworkManagerAvailable = NetworkManager.Instance != null,
            IsMatchingManagerAvailable = MatchingManager.Instance != null,
            CurrentMatchingState = MatchingManager.Instance?.CurrentState ?? MatchingState.Idle
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 로그아웃 상태 정보
/// </summary>
[Serializable]
public class LogoutStatus
{
    public bool IsInProgress { get; set; }
    public bool IsAuthenticationManagerAvailable { get; set; }
    public bool IsNetworkManagerAvailable { get; set; }
    public bool IsMatchingManagerAvailable { get; set; }
    public MatchingState CurrentMatchingState { get; set; }
}

/// <summary>
/// 매칭 상태 열거형 (MatchingManager에서 정의된 것과 동일)
/// </summary>
public enum MatchingState
{
    Idle,
    Searching,
    Found,
    Starting,
    Cancelled,
    Failed
}
#endregion