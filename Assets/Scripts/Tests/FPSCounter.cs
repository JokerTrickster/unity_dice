using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 성능 테스트를 위한 FPS 카운터 클래스
/// 실시간 FPS 측정, 통계 분석, 성능 임계값 검증을 제공합니다.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    #region Configuration
    
    [SerializeField]
    private float _updateInterval = 0.5f;
    
    [SerializeField]
    private int _maxSamples = 100;
    
    [SerializeField]
    private float _targetFPS = 60f;
    
    [SerializeField]
    private float _minimumAcceptableFPS = 55f;
    
    [SerializeField]
    private bool _enableDetailedLogging = false;
    
    #endregion
    
    #region State
    
    private bool _isMonitoring = false;
    private float _currentFPS = 0f;
    private float _averageFPS = 0f;
    private float _minimumFPS = float.MaxValue;
    private float _maximumFPS = float.MinValue;
    private float _deltaTime = 0f;
    
    private List<float> _fpsSamples;
    private List<float> _frameTimeSamples;
    private Queue<FPSDataPoint> _recentFrames;
    
    private Coroutine _monitoringCoroutine;
    private DateTime _monitoringStartTime;
    private int _totalFrames = 0;
    private float _totalTime = 0f;
    
    #endregion
    
    #region Properties
    
    /// <summary>현재 FPS</summary>
    public float CurrentFPS => _currentFPS;
    
    /// <summary>평균 FPS</summary>
    public float AverageFPS => _averageFPS;
    
    /// <summary>최소 FPS</summary>
    public float MinimumFPS => _minimumFPS == float.MaxValue ? 0f : _minimumFPS;
    
    /// <summary>최대 FPS</summary>
    public float MaximumFPS => _maximumFPS == float.MinValue ? 0f : _maximumFPS;
    
    /// <summary>FPS 표준편차</summary>
    public float FPSStandardDeviation => CalculateStandardDeviation(_fpsSamples);
    
    /// <summary>모니터링 중 여부</summary>
    public bool IsMonitoring => _isMonitoring;
    
    /// <summary>모니터링 시간 (초)</summary>
    public float MonitoringTime => _totalTime;
    
    /// <summary>총 프레임 수</summary>
    public int TotalFrames => _totalFrames;
    
    /// <summary>성능 요구사항 충족 여부</summary>
    public bool IsPerformanceAcceptable => _averageFPS >= _minimumAcceptableFPS && MinimumFPS >= _minimumAcceptableFPS * 0.9f;
    
    #endregion
    
    #region Events
    
    public event Action<float> OnFPSUpdated;
    public event Action<FPSStatistics> OnStatisticsUpdated;
    public event Action<string> OnPerformanceAlert;
    
    #endregion
    
    #region Lifecycle
    
    private void Awake()
    {
        InitializeCollections();
    }
    
    private void OnDestroy()
    {
        StopMonitoring();
    }
    
    private void InitializeCollections()
    {
        _fpsSamples = new List<float>();
        _frameTimeSamples = new List<float>();
        _recentFrames = new Queue<FPSDataPoint>();
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// FPS 모니터링을 시작합니다.
    /// </summary>
    public void StartMonitoring()
    {
        if (_isMonitoring) return;
        
        _isMonitoring = true;
        _monitoringStartTime = DateTime.UtcNow;
        ResetStatistics();
        
        _monitoringCoroutine = StartCoroutine(MonitoringCoroutine());
        
        if (_enableDetailedLogging)
        {
            Debug.Log("FPS monitoring started");
        }
    }
    
    /// <summary>
    /// FPS 모니터링을 중단합니다.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring) return;
        
        _isMonitoring = false;
        
        if (_monitoringCoroutine != null)
        {
            StopCoroutine(_monitoringCoroutine);
            _monitoringCoroutine = null;
        }
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"FPS monitoring stopped. Final stats: Avg={_averageFPS:F1}, Min={MinimumFPS:F1}, Max={MaximumFPS:F1}");
        }
    }
    
    /// <summary>
    /// 현재 통계를 리셋합니다.
    /// </summary>
    public void ResetStatistics()
    {
        _currentFPS = 0f;
        _averageFPS = 0f;
        _minimumFPS = float.MaxValue;
        _maximumFPS = float.MinValue;
        _totalFrames = 0;
        _totalTime = 0f;
        
        _fpsSamples.Clear();
        _frameTimeSamples.Clear();
        _recentFrames.Clear();
    }
    
    /// <summary>
    /// 상세한 성능 통계를 반환합니다.
    /// </summary>
    /// <returns>성능 통계</returns>
    public FPSStatistics GetDetailedStatistics()
    {
        return new FPSStatistics
        {
            CurrentFPS = _currentFPS,
            AverageFPS = _averageFPS,
            MinimumFPS = MinimumFPS,
            MaximumFPS = MaximumFPS,
            StandardDeviation = FPSStandardDeviation,
            TotalFrames = _totalFrames,
            MonitoringDuration = _totalTime,
            Percentile95 = CalculatePercentile(_fpsSamples, 0.95f),
            Percentile99 = CalculatePercentile(_fpsSamples, 0.99f),
            FrameTimeAverage = _frameTimeSamples.Count > 0 ? _frameTimeSamples.Average() : 0f,
            FrameTimeMax = _frameTimeSamples.Count > 0 ? _frameTimeSamples.Max() : 0f,
            IsPerformanceAcceptable = IsPerformanceAcceptable,
            PerformanceGrade = CalculatePerformanceGrade()
        };
    }
    
    /// <summary>
    /// 성능 보고서를 문자열로 반환합니다.
    /// </summary>
    /// <returns>성능 보고서</returns>
    public string GetPerformanceReport()
    {
        var stats = GetDetailedStatistics();
        
        return $"=== FPS Performance Report ===\n" +
               $"Monitoring Duration: {stats.MonitoringDuration:F1}s ({stats.TotalFrames} frames)\n" +
               $"Current FPS: {stats.CurrentFPS:F1}\n" +
               $"Average FPS: {stats.AverageFPS:F1}\n" +
               $"Min/Max FPS: {stats.MinimumFPS:F1} / {stats.MaximumFPS:F1}\n" +
               $"95th Percentile: {stats.Percentile95:F1}\n" +
               $"99th Percentile: {stats.Percentile99:F1}\n" +
               $"Standard Deviation: {stats.StandardDeviation:F2}\n" +
               $"Frame Time (Avg/Max): {stats.FrameTimeAverage * 1000:F1}ms / {stats.FrameTimeMax * 1000:F1}ms\n" +
               $"Performance Grade: {stats.PerformanceGrade}\n" +
               $"Requirements Met: {(stats.IsPerformanceAcceptable ? "YES" : "NO")}";
    }
    
    /// <summary>
    /// 특정 기간 동안의 평균 FPS를 측정합니다.
    /// </summary>
    /// <param name="duration">측정 기간 (초)</param>
    /// <returns>평균 FPS 측정 코루틴</returns>
    public IEnumerator MeasureAverageFPSOverTime(float duration, System.Action<float> onComplete = null)
    {
        List<float> samples = new List<float>();
        float timer = 0f;
        
        while (timer < duration)
        {
            float fps = 1f / Time.deltaTime;
            samples.Add(fps);
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        float averageFPS = samples.Count > 0 ? samples.Average() : 0f;
        onComplete?.Invoke(averageFPS);
    }
    
    /// <summary>
    /// 성능 스트레스 테스트를 실행합니다.
    /// </summary>
    /// <param name="duration">테스트 기간</param>
    /// <returns>스트레스 테스트 코루틴</returns>
    public IEnumerator RunPerformanceStressTest(float duration)
    {
        StartMonitoring();
        
        float startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            // CPU 스트레스 시뮬레이션
            for (int i = 0; i < 1000; i++)
            {
                _ = Mathf.Sin(i * 0.01f);
            }
            
            yield return null;
        }
        
        yield return new WaitForSeconds(1f); // 안정화 대기
        StopMonitoring();
    }
    
    #endregion
    
    #region Monitoring Coroutine
    
    private IEnumerator MonitoringCoroutine()
    {
        while (_isMonitoring)
        {
            UpdateFPSMeasurements();
            
            yield return new WaitForSeconds(_updateInterval);
        }
    }
    
    private void UpdateFPSMeasurements()
    {
        _deltaTime += (Time.deltaTime - _deltaTime) * 0.1f;
        _currentFPS = 1.0f / _deltaTime;
        _totalFrames++;
        _totalTime += Time.deltaTime;
        
        // 샘플 추가 및 크기 제한
        AddSample(_currentFPS);
        AddFrameTimeSample(Time.deltaTime);
        
        // 통계 업데이트
        UpdateStatistics();
        
        // 최근 프레임 데이터 추가
        _recentFrames.Enqueue(new FPSDataPoint
        {
            FPS = _currentFPS,
            FrameTime = Time.deltaTime,
            Timestamp = DateTime.UtcNow
        });
        
        // 큐 크기 제한
        while (_recentFrames.Count > _maxSamples)
        {
            _recentFrames.Dequeue();
        }
        
        // 이벤트 발생
        OnFPSUpdated?.Invoke(_currentFPS);
        OnStatisticsUpdated?.Invoke(GetDetailedStatistics());
        
        // 성능 경고 확인
        CheckPerformanceThresholds();
    }
    
    private void AddSample(float fps)
    {
        _fpsSamples.Add(fps);
        
        if (_fpsSamples.Count > _maxSamples)
        {
            _fpsSamples.RemoveAt(0);
        }
    }
    
    private void AddFrameTimeSample(float frameTime)
    {
        _frameTimeSamples.Add(frameTime);
        
        if (_frameTimeSamples.Count > _maxSamples)
        {
            _frameTimeSamples.RemoveAt(0);
        }
    }
    
    private void UpdateStatistics()
    {
        if (_fpsSamples.Count == 0) return;
        
        _averageFPS = _fpsSamples.Average();
        _minimumFPS = Mathf.Min(_minimumFPS, _currentFPS);
        _maximumFPS = Mathf.Max(_maximumFPS, _currentFPS);
    }
    
    private void CheckPerformanceThresholds()
    {
        if (_currentFPS < _minimumAcceptableFPS)
        {
            OnPerformanceAlert?.Invoke($"FPS dropped below threshold: {_currentFPS:F1} < {_minimumAcceptableFPS}");
        }
        
        if (_averageFPS < _minimumAcceptableFPS && _fpsSamples.Count >= 10)
        {
            OnPerformanceAlert?.Invoke($"Average FPS below threshold: {_averageFPS:F1} < {_minimumAcceptableFPS}");
        }
    }
    
    #endregion
    
    #region Statistical Calculations
    
    private float CalculateStandardDeviation(List<float> values)
    {
        if (values.Count == 0) return 0f;
        
        float average = values.Average();
        float sumOfSquares = values.Sum(val => (val - average) * (val - average));
        return Mathf.Sqrt(sumOfSquares / values.Count);
    }
    
    private float CalculatePercentile(List<float> values, float percentile)
    {
        if (values.Count == 0) return 0f;
        
        var sortedValues = values.OrderBy(x => x).ToList();
        int index = Mathf.FloorToInt(percentile * (sortedValues.Count - 1));
        return sortedValues[Mathf.Clamp(index, 0, sortedValues.Count - 1)];
    }
    
    private PerformanceGrade CalculatePerformanceGrade()
    {
        if (_averageFPS >= _targetFPS && MinimumFPS >= _targetFPS * 0.9f)
            return PerformanceGrade.Excellent;
        else if (_averageFPS >= _minimumAcceptableFPS && MinimumFPS >= _minimumAcceptableFPS * 0.9f)
            return PerformanceGrade.Good;
        else if (_averageFPS >= _minimumAcceptableFPS * 0.9f)
            return PerformanceGrade.Acceptable;
        else if (_averageFPS >= _minimumAcceptableFPS * 0.7f)
            return PerformanceGrade.Poor;
        else
            return PerformanceGrade.Unacceptable;
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// FPS 통계 데이터
    /// </summary>
    [Serializable]
    public class FPSStatistics
    {
        public float CurrentFPS;
        public float AverageFPS;
        public float MinimumFPS;
        public float MaximumFPS;
        public float StandardDeviation;
        public int TotalFrames;
        public float MonitoringDuration;
        public float Percentile95;
        public float Percentile99;
        public float FrameTimeAverage;
        public float FrameTimeMax;
        public bool IsPerformanceAcceptable;
        public PerformanceGrade PerformanceGrade;
    }
    
    /// <summary>
    /// 개별 프레임 데이터 포인트
    /// </summary>
    [Serializable]
    public class FPSDataPoint
    {
        public float FPS;
        public float FrameTime;
        public DateTime Timestamp;
    }
    
    /// <summary>
    /// 성능 등급
    /// </summary>
    public enum PerformanceGrade
    {
        Excellent,   // >= 60 FPS 안정적
        Good,        // >= 55 FPS 안정적
        Acceptable,  // >= 50 FPS 대부분
        Poor,        // >= 40 FPS 가끔 드롭
        Unacceptable // < 40 FPS 자주 드롭
    }
    
    #endregion
    
    #region Configuration Methods
    
    /// <summary>
    /// FPS 카운터를 구성합니다.
    /// </summary>
    /// <param name="updateInterval">업데이트 간격</param>
    /// <param name="maxSamples">최대 샘플 수</param>
    /// <param name="targetFPS">목표 FPS</param>
    /// <param name="minimumAcceptableFPS">최소 허용 FPS</param>
    public void Configure(float updateInterval = 0.5f, int maxSamples = 100, float targetFPS = 60f, float minimumAcceptableFPS = 55f)
    {
        _updateInterval = Mathf.Max(0.1f, updateInterval);
        _maxSamples = Mathf.Max(10, maxSamples);
        _targetFPS = Mathf.Max(1f, targetFPS);
        _minimumAcceptableFPS = Mathf.Max(1f, minimumAcceptableFPS);
    }
    
    /// <summary>
    /// 상세 로깅을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enable">활성화 여부</param>
    public void EnableDetailedLogging(bool enable)
    {
        _enableDetailedLogging = enable;
    }
    
    #endregion
}