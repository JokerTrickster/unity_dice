using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity 메인 스레드에서 작업을 실행하기 위한 디스패처
/// 백그라운드 스레드에서 UI 업데이트나 Unity API 호출이 필요할 때 사용
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    #region Singleton
    private static UnityMainThreadDispatcher _instance;
    private static readonly object _lock = new object();
    private static bool _applicationQuitting = false;

    /// <summary>
    /// 디스패처 인스턴스 (지연 초기화)
    /// </summary>
    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_applicationQuitting)
            {
                Debug.LogWarning("[UnityMainThreadDispatcher] Instance requested during application shutdown");
                return null;
            }

            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        CreateInstance();
                    }
                }
            }

            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private readonly Queue<Action> _executionQueue = new Queue<Action>();
    private readonly object _queueLock = new object();
    private bool _isDestroyed = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[UnityMainThreadDispatcher] Initialized");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Duplicate instance destroyed");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (_isDestroyed)
            return;

        // 메인 스레드에서 큐된 작업들을 실행
        lock (_queueLock)
        {
            while (_executionQueue.Count > 0)
            {
                var action = _executionQueue.Dequeue();
                
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UnityMainThreadDispatcher] Error executing queued action: {e.Message}\n{e.StackTrace}");
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    private void OnDestroy()
    {
        _isDestroyed = true;
        
        if (_instance == this)
        {
            _instance = null;
        }

        // 남은 작업들을 정리
        lock (_queueLock)
        {
            _executionQueue.Clear();
        }

        Debug.Log("[UnityMainThreadDispatcher] Destroyed");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 메인 스레드에서 실행할 작업을 큐에 추가
    /// 백그라운드 스레드에서 Unity API를 호출하거나 UI를 업데이트할 때 사용
    /// </summary>
    /// <param name="action">실행할 작업</param>
    public void Enqueue(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Null action cannot be enqueued");
            return;
        }

        if (_applicationQuitting || _isDestroyed)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Cannot enqueue action during shutdown");
            return;
        }

        lock (_queueLock)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// 지연된 실행을 위한 코루틴 시작
    /// </summary>
    /// <param name="action">실행할 작업</param>
    /// <param name="delaySeconds">지연 시간(초)</param>
    public void EnqueueDelayed(Action action, float delaySeconds)
    {
        if (action == null)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Null action cannot be enqueued");
            return;
        }

        if (_applicationQuitting || _isDestroyed)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Cannot enqueue delayed action during shutdown");
            return;
        }

        StartCoroutine(ExecuteDelayed(action, delaySeconds));
    }

    /// <summary>
    /// 다음 프레임에 실행할 작업을 큐에 추가
    /// </summary>
    /// <param name="action">실행할 작업</param>
    public void EnqueueNextFrame(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Null action cannot be enqueued");
            return;
        }

        if (_applicationQuitting || _isDestroyed)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Cannot enqueue next frame action during shutdown");
            return;
        }

        StartCoroutine(ExecuteNextFrame(action));
    }

    /// <summary>
    /// 현재 스레드가 메인 스레드인지 확인
    /// </summary>
    /// <returns>메인 스레드 여부</returns>
    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
    }

    /// <summary>
    /// 메인 스레드에서 안전하게 작업 실행
    /// 현재 스레드가 메인 스레드면 즉시 실행, 아니면 큐에 추가
    /// </summary>
    /// <param name="action">실행할 작업</param>
    public static void SafeExecute(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Null action cannot be executed");
            return;
        }

        if (IsMainThread())
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Error executing immediate action: {e.Message}\n{e.StackTrace}");
            }
        }
        else
        {
            Instance?.Enqueue(action);
        }
    }

    /// <summary>
    /// 큐에 대기 중인 작업 수
    /// </summary>
    public int QueuedActionCount
    {
        get
        {
            lock (_queueLock)
            {
                return _executionQueue.Count;
            }
        }
    }

    /// <summary>
    /// 큐의 모든 작업을 즉시 실행 (메인 스레드에서만 호출)
    /// </summary>
    public void FlushQueue()
    {
        if (!IsMainThread())
        {
            Debug.LogError("[UnityMainThreadDispatcher] FlushQueue can only be called from main thread");
            return;
        }

        lock (_queueLock)
        {
            while (_executionQueue.Count > 0)
            {
                var action = _executionQueue.Dequeue();
                
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UnityMainThreadDispatcher] Error executing flushed action: {e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 인스턴스 생성 (GameObject와 함께)
    /// </summary>
    private static void CreateInstance()
    {
        var go = new GameObject("UnityMainThreadDispatcher");
        _instance = go.AddComponent<UnityMainThreadDispatcher>();
    }

    /// <summary>
    /// 지연 실행 코루틴
    /// </summary>
    /// <param name="action">실행할 작업</param>
    /// <param name="delaySeconds">지연 시간</param>
    /// <returns>코루틴</returns>
    private IEnumerator ExecuteDelayed(Action action, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        
        if (!_applicationQuitting && !_isDestroyed)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Error executing delayed action: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 다음 프레임 실행 코루틴
    /// </summary>
    /// <param name="action">실행할 작업</param>
    /// <returns>코루틴</returns>
    private IEnumerator ExecuteNextFrame(Action action)
    {
        yield return null; // 다음 프레임까지 대기
        
        if (!_applicationQuitting && !_isDestroyed)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Error executing next frame action: {e.Message}\n{e.StackTrace}");
            }
        }
    }
    #endregion

    #region Debug Methods
    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    public void LogDebugInfo()
    {
        Debug.Log($"[UnityMainThreadDispatcher] Debug Info:\n" +
                 $"- Instance exists: {_instance != null}\n" +
                 $"- Application quitting: {_applicationQuitting}\n" +
                 $"- Is destroyed: {_isDestroyed}\n" +
                 $"- Queued actions: {QueuedActionCount}\n" +
                 $"- Is main thread: {IsMainThread()}");
    }
    #endregion
}

/// <summary>
/// UnityMainThreadDispatcher의 확장 메서드들
/// </summary>
public static class UnityMainThreadDispatcherExtensions
{
    /// <summary>
    /// UI 업데이트를 메인 스레드에서 안전하게 실행
    /// </summary>
    /// <param name="dispatcher">디스패처 인스턴스</param>
    /// <param name="uiUpdateAction">UI 업데이트 작업</param>
    public static void UpdateUI(this UnityMainThreadDispatcher dispatcher, Action uiUpdateAction)
    {
        if (uiUpdateAction == null)
            return;

        UnityMainThreadDispatcher.SafeExecute(uiUpdateAction);
    }

    /// <summary>
    /// 이벤트 호출을 메인 스레드에서 안전하게 실행
    /// </summary>
    /// <param name="dispatcher">디스패처 인스턴스</param>
    /// <param name="eventAction">이벤트 작업</param>
    public static void InvokeEvent(this UnityMainThreadDispatcher dispatcher, Action eventAction)
    {
        if (eventAction == null)
            return;

        UnityMainThreadDispatcher.SafeExecute(eventAction);
    }
}