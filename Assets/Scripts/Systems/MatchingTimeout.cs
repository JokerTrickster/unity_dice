using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 요청 타임아웃 관리 시스템
/// 매칭 요청의 타임아웃을 추적하고 관리하여 무한 대기 상황을 방지
/// </summary>
public class MatchingTimeout : MonoBehaviour
{
    #region Events
    /// <summary>요청 타임아웃 이벤트</summary>
    public event Action<string, string> OnRequestTimeout; // (requestId, playerId)
    
    /// <summary>타임아웃 경고 이벤트 (타임아웃 10초 전)</summary>
    public event Action<string, int> OnTimeoutWarning; // (requestId, remainingSeconds)
    
    /// <summary>타임아웃 취소 이벤트</summary>
    public event Action<string> OnTimeoutCancelled; // requestId
    #endregion

    #region Private Classes
    /// <summary>
    /// 타임아웃 정보를 담는 클래스
    /// </summary>
    private class TimeoutInfo
    {
        public string requestId;
        public string playerId;
        public DateTime startTime;
        public float timeoutDuration;
        public Coroutine timeoutCoroutine;
        public bool warningTriggered;
        
        public TimeoutInfo(string reqId, string pId, float duration)
        {
            requestId = reqId;
            playerId = pId;
            startTime = DateTime.UtcNow;
            timeoutDuration = duration;
            warningTriggered = false;
        }
        
        public float ElapsedTime => (float)(DateTime.UtcNow - startTime).TotalSeconds;
        public float RemainingTime => Mathf.Max(0f, timeoutDuration - ElapsedTime);
        public bool IsExpired => ElapsedTime >= timeoutDuration;
    }
    #endregion

    #region Private Fields
    private readonly Dictionary<string, TimeoutInfo> _activeTimeouts = new();
    private readonly object _lockObject = new();
    
    // Configuration
    private const float DEFAULT_TIMEOUT_DURATION = 60f; // 60초 기본 타임아웃
    private const float WARNING_TIME = 10f; // 타임아웃 10초 전 경고
    private const float CHECK_INTERVAL = 1f; // 1초마다 타임아웃 체크
    
    private bool _isDestroyed = false;
    #endregion

    #region Properties
    /// <summary>활성 타임아웃 수</summary>
    public int ActiveTimeoutCount => _activeTimeouts.Count;
    
    /// <summary>모든 활성 타임아웃 정보</summary>
    public Dictionary<string, float> ActiveTimeouts
    {
        get
        {
            var result = new Dictionary<string, float>();
            lock (_lockObject)
            {
                foreach (var kvp in _activeTimeouts)
                {
                    result[kvp.Key] = kvp.Value.RemainingTime;
                }
            }
            return result;
        }
    }
    
    /// <summary>전체 대기 중인 플레이어 목록</summary>
    public List<string> WaitingPlayers
    {
        get
        {
            var result = new List<string>();
            lock (_lockObject)
            {
                foreach (var timeout in _activeTimeouts.Values)
                {
                    if (!result.Contains(timeout.playerId))
                    {
                        result.Add(timeout.playerId);
                    }
                }
            }
            return result;
        }
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        _isDestroyed = true;
        StopAllTimeouts();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 매칭 요청 타임아웃 시작
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="timeoutDuration">타임아웃 시간(초), 기본값 60초</param>
    /// <returns>타임아웃 시작 성공 여부</returns>
    public bool StartRequestTimeout(string requestId, string playerId, float timeoutDuration = DEFAULT_TIMEOUT_DURATION)
    {
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("[MatchingTimeout] RequestId and PlayerId cannot be null or empty");
            return false;
        }

        if (timeoutDuration <= 0)
        {
            Debug.LogError($"[MatchingTimeout] Invalid timeout duration: {timeoutDuration}");
            return false;
        }

        if (_isDestroyed)
        {
            Debug.LogWarning("[MatchingTimeout] Cannot start timeout: Component is destroyed");
            return false;
        }

        try
        {
            lock (_lockObject)
            {
                // 이미 존재하는 요청이면 기존 것 취소 후 새로 시작
                if (_activeTimeouts.ContainsKey(requestId))
                {
                    Debug.LogWarning($"[MatchingTimeout] Request {requestId} already has timeout, replacing it");
                    CancelTimeoutInternal(requestId);
                }

                // 새 타임아웃 정보 생성
                var timeoutInfo = new TimeoutInfo(requestId, playerId, timeoutDuration);
                _activeTimeouts[requestId] = timeoutInfo;

                // 타임아웃 코루틴 시작
                timeoutInfo.timeoutCoroutine = StartCoroutine(TimeoutCoroutine(requestId));
                
                Debug.Log($"[MatchingTimeout] Started timeout for request {requestId}, player {playerId}, duration {timeoutDuration}s");
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingTimeout] Failed to start timeout: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 특정 요청의 타임아웃 취소
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <returns>취소 성공 여부</returns>
    public bool CancelTimeout(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            Debug.LogError("[MatchingTimeout] RequestId cannot be null or empty");
            return false;
        }

        lock (_lockObject)
        {
            return CancelTimeoutInternal(requestId);
        }
    }

    /// <summary>
    /// 특정 플레이어의 모든 타임아웃 취소
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>취소된 타임아웃 수</returns>
    public int CancelPlayerTimeouts(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("[MatchingTimeout] PlayerId cannot be null or empty");
            return 0;
        }

        int cancelledCount = 0;
        
        lock (_lockObject)
        {
            var requestsToCancel = new List<string>();
            
            foreach (var kvp in _activeTimeouts)
            {
                if (kvp.Value.playerId == playerId)
                {
                    requestsToCancel.Add(kvp.Key);
                }
            }
            
            foreach (var requestId in requestsToCancel)
            {
                if (CancelTimeoutInternal(requestId))
                {
                    cancelledCount++;
                }
            }
        }

        if (cancelledCount > 0)
        {
            Debug.Log($"[MatchingTimeout] Cancelled {cancelledCount} timeouts for player {playerId}");
        }

        return cancelledCount;
    }

    /// <summary>
    /// 모든 타임아웃 취소
    /// </summary>
    /// <returns>취소된 타임아웃 수</returns>
    public int CancelAllTimeouts()
    {
        int cancelledCount = 0;
        
        lock (_lockObject)
        {
            var requestIds = new List<string>(_activeTimeouts.Keys);
            
            foreach (var requestId in requestIds)
            {
                if (CancelTimeoutInternal(requestId))
                {
                    cancelledCount++;
                }
            }
        }

        if (cancelledCount > 0)
        {
            Debug.Log($"[MatchingTimeout] Cancelled all {cancelledCount} active timeouts");
        }

        return cancelledCount;
    }

    /// <summary>
    /// 모든 타임아웃 강제 중지 (컴포넌트 정리용)
    /// </summary>
    public void StopAllTimeouts()
    {
        lock (_lockObject)
        {
            foreach (var timeout in _activeTimeouts.Values)
            {
                if (timeout.timeoutCoroutine != null)
                {
                    try
                    {
                        StopCoroutine(timeout.timeoutCoroutine);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[MatchingTimeout] Error stopping coroutine: {e.Message}");
                    }
                }
            }
            
            _activeTimeouts.Clear();
        }
        
        Debug.Log("[MatchingTimeout] All timeouts stopped");
    }

    /// <summary>
    /// 특정 요청의 남은 시간 조회
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <returns>남은 시간(초), 없으면 -1</returns>
    public float GetRemainingTime(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return -1f;

        lock (_lockObject)
        {
            if (_activeTimeouts.TryGetValue(requestId, out var timeoutInfo))
            {
                return timeoutInfo.RemainingTime;
            }
        }

        return -1f;
    }

    /// <summary>
    /// 특정 플레이어의 총 대기 시간 조회
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>총 대기 시간(초)</returns>
    public float GetPlayerWaitTime(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return 0f;

        float totalWaitTime = 0f;
        
        lock (_lockObject)
        {
            foreach (var timeout in _activeTimeouts.Values)
            {
                if (timeout.playerId == playerId)
                {
                    totalWaitTime = Mathf.Max(totalWaitTime, timeout.ElapsedTime);
                }
            }
        }

        return totalWaitTime;
    }

    /// <summary>
    /// 타임아웃 통계 정보 조회
    /// </summary>
    /// <returns>통계 정보</returns>
    public TimeoutStats GetTimeoutStats()
    {
        var stats = new TimeoutStats();
        
        lock (_lockObject)
        {
            stats.ActiveTimeouts = _activeTimeouts.Count;
            stats.UniquePlayersWaiting = WaitingPlayers.Count;
            
            float totalElapsed = 0f;
            float maxElapsed = 0f;
            int expiredCount = 0;
            
            foreach (var timeout in _activeTimeouts.Values)
            {
                float elapsed = timeout.ElapsedTime;
                totalElapsed += elapsed;
                maxElapsed = Mathf.Max(maxElapsed, elapsed);
                
                if (timeout.IsExpired)
                    expiredCount++;
            }
            
            stats.AverageWaitTime = _activeTimeouts.Count > 0 ? totalElapsed / _activeTimeouts.Count : 0f;
            stats.MaxWaitTime = maxElapsed;
            stats.ExpiredTimeouts = expiredCount;
        }

        return stats;
    }

    /// <summary>
    /// 특정 요청의 타임아웃 시간 연장
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <param name="additionalSeconds">추가 시간(초)</param>
    /// <returns>연장 성공 여부</returns>
    public bool ExtendTimeout(string requestId, float additionalSeconds)
    {
        if (string.IsNullOrEmpty(requestId) || additionalSeconds <= 0)
        {
            Debug.LogError("[MatchingTimeout] Invalid extend parameters");
            return false;
        }

        lock (_lockObject)
        {
            if (_activeTimeouts.TryGetValue(requestId, out var timeoutInfo))
            {
                timeoutInfo.timeoutDuration += additionalSeconds;
                Debug.Log($"[MatchingTimeout] Extended timeout for {requestId} by {additionalSeconds}s");
                return true;
            }
        }

        Debug.LogWarning($"[MatchingTimeout] Cannot extend timeout: Request {requestId} not found");
        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 내부 타임아웃 취소 메서드 (락 없음)
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    /// <returns>취소 성공 여부</returns>
    private bool CancelTimeoutInternal(string requestId)
    {
        if (_activeTimeouts.TryGetValue(requestId, out var timeoutInfo))
        {
            if (timeoutInfo.timeoutCoroutine != null)
            {
                try
                {
                    StopCoroutine(timeoutInfo.timeoutCoroutine);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MatchingTimeout] Error stopping coroutine for {requestId}: {e.Message}");
                }
            }
            
            _activeTimeouts.Remove(requestId);
            OnTimeoutCancelled?.Invoke(requestId);
            
            Debug.Log($"[MatchingTimeout] Cancelled timeout for request {requestId}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 타임아웃 처리 코루틴
    /// </summary>
    /// <param name="requestId">요청 ID</param>
    private IEnumerator TimeoutCoroutine(string requestId)
    {
        TimeoutInfo timeoutInfo;
        
        // 타임아웃 정보 가져오기
        lock (_lockObject)
        {
            if (!_activeTimeouts.TryGetValue(requestId, out timeoutInfo))
            {
                Debug.LogError($"[MatchingTimeout] Timeout info not found for request {requestId}");
                yield break;
            }
        }

        float checkInterval = CHECK_INTERVAL;
        
        while (!_isDestroyed)
        {
            yield return new WaitForSeconds(checkInterval);
            
            // 타임아웃 정보 재확인 (중간에 취소될 수 있음)
            lock (_lockObject)
            {
                if (!_activeTimeouts.TryGetValue(requestId, out timeoutInfo))
                {
                    // 타임아웃이 취소됨
                    yield break;
                }
            }

            float remainingTime = timeoutInfo.RemainingTime;
            
            // 경고 시간 체크
            if (!timeoutInfo.warningTriggered && remainingTime <= WARNING_TIME && remainingTime > 0)
            {
                timeoutInfo.warningTriggered = true;
                OnTimeoutWarning?.Invoke(requestId, (int)remainingTime);
                Debug.Log($"[MatchingTimeout] Timeout warning for {requestId}: {remainingTime:F1}s remaining");
            }
            
            // 타임아웃 체크
            if (timeoutInfo.IsExpired)
            {
                Debug.LogWarning($"[MatchingTimeout] Request {requestId} timed out after {timeoutInfo.timeoutDuration}s");
                
                string playerId = timeoutInfo.playerId;
                
                // 타임아웃 정보 제거
                lock (_lockObject)
                {
                    _activeTimeouts.Remove(requestId);
                }
                
                // 타임아웃 이벤트 발생
                OnRequestTimeout?.Invoke(requestId, playerId);
                yield break;
            }
        }
    }
    #endregion
}

/// <summary>
/// 타임아웃 통계 정보
/// </summary>
public class TimeoutStats
{
    public int ActiveTimeouts { get; set; }
    public int UniquePlayersWaiting { get; set; }
    public float AverageWaitTime { get; set; }
    public float MaxWaitTime { get; set; }
    public int ExpiredTimeouts { get; set; }
    
    public override string ToString()
    {
        return $"Active: {ActiveTimeouts}, Players: {UniquePlayersWaiting}, " +
               $"Avg Wait: {AverageWaitTime:F1}s, Max Wait: {MaxWaitTime:F1}s, Expired: {ExpiredTimeouts}";
    }
}