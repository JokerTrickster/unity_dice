using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// 메모리 누수 감지 및 메모리 사용량 프로파일링을 위한 클래스
/// 50회 반복 테스트, 30MB 메모리 임계값 검증, 가비지 컬렉션 모니터링을 지원합니다.
/// </summary>
public class MemoryProfiler : MonoBehaviour
{
    #region Configuration
    
    [SerializeField]
    private float _samplingInterval = 1f;
    
    [SerializeField]
    private int _maxSamples = 1000;
    
    [SerializeField]
    private long _memoryThresholdBytes = 30 * 1024 * 1024; // 30MB
    
    [SerializeField]
    private int _defaultIterationCount = 50;
    
    [SerializeField]
    private bool _enableDetailedLogging = false;
    
    [SerializeField]
    private bool _enableAutoGC = true;
    
    #endregion
    
    #region State
    
    private bool _isProfileActive = false;
    private long _baselineMemory = 0;
    private long _currentMemory = 0;
    private long _peakMemory = 0;
    private long _minimumMemory = long.MaxValue;
    
    private List<MemorySnapshot> _memorySnapshots;
    private Dictionary<string, long> _typeMemoryUsage;
    private Coroutine _profilingCoroutine;
    
    private DateTime _profilingStartTime;
    private float _totalProfilingTime = 0f;
    private int _gcCollectionsBefore = 0;
    private int _gcCollectionsAfter = 0;
    
    #endregion
    
    #region Properties
    
    /// <summary>현재 메모리 사용량 (바이트)</summary>
    public long CurrentMemoryUsage => _currentMemory;
    
    /// <summary>현재 메모리 사용량 (MB)</summary>
    public float CurrentMemoryUsageMB => _currentMemory / (1024f * 1024f);
    
    /// <summary>베이스라인 대비 메모리 증가량 (바이트)</summary>
    public long MemoryIncrease => _currentMemory - _baselineMemory;
    
    /// <summary>베이스라인 대비 메모리 증가량 (MB)</summary>
    public float MemoryIncreaseMB => MemoryIncrease / (1024f * 1024f);
    
    /// <summary>피크 메모리 사용량 (MB)</summary>
    public float PeakMemoryUsageMB => _peakMemory / (1024f * 1024f);
    
    /// <summary>최소 메모리 사용량 (MB)</summary>
    public float MinimumMemoryUsageMB => _minimumMemory == long.MaxValue ? 0f : _minimumMemory / (1024f * 1024f);
    
    /// <summary>프로파일링 활성 여부</summary>
    public bool IsProfilingActive => _isProfileActive;
    
    /// <summary>메모리 누수 의심 여부</summary>
    public bool IsSuspectedMemoryLeak => MemoryIncrease > _memoryThresholdBytes;
    
    /// <summary>총 GC 발생 횟수</summary>
    public int TotalGCCollections => _gcCollectionsAfter - _gcCollectionsBefore;
    
    #endregion
    
    #region Events
    
    public event Action<MemorySnapshot> OnMemorySnapshotTaken;
    public event Action<MemoryLeakReport> OnMemoryLeakDetected;
    public event Action<string> OnMemoryAlert;
    public event Action OnGarbageCollectionTriggered;
    
    #endregion
    
    #region Lifecycle
    
    private void Awake()
    {
        InitializeCollections();
    }
    
    private void OnDestroy()
    {
        StopProfiling();
    }
    
    private void InitializeCollections()
    {
        _memorySnapshots = new List<MemorySnapshot>();
        _typeMemoryUsage = new Dictionary<string, long>();
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// 메모리 프로파일링을 시작합니다.
    /// </summary>
    /// <param name="establishBaseline">베이스라인 설정 여부</param>
    public void StartProfiling(bool establishBaseline = true)
    {
        if (_isProfileActive) return;
        
        _isProfileActive = true;
        _profilingStartTime = DateTime.UtcNow;
        _gcCollectionsBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        
        if (establishBaseline)
        {
            EstablishMemoryBaseline();
        }
        
        ResetStatistics();
        _profilingCoroutine = StartCoroutine(ProfilingCoroutine());
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"Memory profiling started. Baseline: {_baselineMemory / (1024f * 1024f):F1}MB");
        }
    }
    
    /// <summary>
    /// 메모리 프로파일링을 중단합니다.
    /// </summary>
    public void StopProfiling()
    {
        if (!_isProfileActive) return;
        
        _isProfileActive = false;
        _totalProfilingTime = (float)(DateTime.UtcNow - _profilingStartTime).TotalSeconds;
        _gcCollectionsAfter = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        
        if (_profilingCoroutine != null)
        {
            StopCoroutine(_profilingCoroutine);
            _profilingCoroutine = null;
        }
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"Memory profiling stopped. Final usage: {CurrentMemoryUsageMB:F1}MB (+{MemoryIncreaseMB:F1}MB)");
        }
    }
    
    /// <summary>
    /// 메모리 베이스라인을 설정합니다.
    /// </summary>
    public void EstablishMemoryBaseline()
    {
        if (_enableAutoGC)
        {
            ForceGarbageCollection();
        }
        
        _baselineMemory = GC.GetTotalMemory(false);
        _currentMemory = _baselineMemory;
        _peakMemory = _baselineMemory;
        _minimumMemory = _baselineMemory;
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"Memory baseline established: {_baselineMemory / (1024f * 1024f):F1}MB");
        }
    }
    
    /// <summary>
    /// 메모리 스냅샷을 즉시 생성합니다.
    /// </summary>
    /// <returns>생성된 메모리 스냅샷</returns>
    public MemorySnapshot TakeMemorySnapshot()
    {
        long totalMemory = GC.GetTotalMemory(false);
        long managedMemory = Profiler.GetTotalAllocatedMemory(0);
        long unityMemory = Profiler.GetTotalReservedMemory(0);
        
        var snapshot = new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            TotalMemory = totalMemory,
            ManagedMemory = managedMemory,
            UnityMemory = unityMemory,
            MemoryIncrease = totalMemory - _baselineMemory,
            GCGeneration0 = GC.CollectionCount(0),
            GCGeneration1 = GC.CollectionCount(1),
            GCGeneration2 = GC.CollectionCount(2)
        };
        
        _memorySnapshots.Add(snapshot);
        
        // 최대 샘플 수 제한
        if (_memorySnapshots.Count > _maxSamples)
        {
            _memorySnapshots.RemoveAt(0);
        }
        
        UpdateMemoryStatistics(totalMemory);
        OnMemorySnapshotTaken?.Invoke(snapshot);
        
        return snapshot;
    }
    
    /// <summary>
    /// 메모리 누수 테스트를 실행합니다.
    /// </summary>
    /// <param name="testAction">테스트할 액션</param>
    /// <param name="iterations">반복 횟수</param>
    /// <returns>메모리 누수 테스트 코루틴</returns>
    public IEnumerator RunMemoryLeakTest(System.Action testAction, int iterations = -1)
    {
        if (iterations <= 0) iterations = _defaultIterationCount;
        
        StartProfiling(true);
        
        var initialSnapshot = TakeMemorySnapshot();
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                testAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in memory leak test iteration {i}: {ex.Message}");
            }
            
            // 정기적으로 스냅샷 생성
            if (i % 10 == 0)
            {
                TakeMemorySnapshot();
            }
            
            yield return null;
        }
        
        // 테스트 완료 후 GC 실행 및 최종 측정
        if (_enableAutoGC)
        {
            ForceGarbageCollection();
            yield return new WaitForSeconds(0.5f);
        }
        
        var finalSnapshot = TakeMemorySnapshot();
        StopProfiling();
        
        // 메모리 누수 검사
        CheckForMemoryLeak(initialSnapshot, finalSnapshot, iterations);
    }
    
    /// <summary>
    /// 스트레스 테스트를 실행합니다.
    /// </summary>
    /// <param name="stressAction">스트레스를 가할 액션</param>
    /// <param name="duration">테스트 기간 (초)</param>
    /// <returns>스트레스 테스트 코루틴</returns>
    public IEnumerator RunMemoryStressTest(System.Action stressAction, float duration)
    {
        StartProfiling(true);
        
        float startTime = Time.time;
        int iterations = 0;
        
        while (Time.time - startTime < duration)
        {
            try
            {
                stressAction?.Invoke();
                iterations++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in memory stress test: {ex.Message}");
            }
            
            yield return null;
        }
        
        if (_enableAutoGC)
        {
            ForceGarbageCollection();
            yield return new WaitForSeconds(0.5f);
        }
        
        TakeMemorySnapshot();
        StopProfiling();
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"Memory stress test completed: {iterations} iterations in {duration}s");
        }
    }
    
    /// <summary>
    /// 상세한 메모리 보고서를 생성합니다.
    /// </summary>
    /// <returns>메모리 보고서</returns>
    public MemoryReport GenerateMemoryReport()
    {
        var report = new MemoryReport
        {
            ProfilingDuration = _totalProfilingTime,
            BaselineMemoryMB = _baselineMemory / (1024f * 1024f),
            CurrentMemoryMB = CurrentMemoryUsageMB,
            PeakMemoryMB = PeakMemoryUsageMB,
            MinimumMemoryMB = MinimumMemoryUsageMB,
            MemoryIncreaseMB = MemoryIncreaseMB,
            TotalGCCollections = TotalGCCollections,
            SnapshotCount = _memorySnapshots.Count,
            IsSuspectedMemoryLeak = IsSuspectedMemoryLeak,
            IsWithinThreshold = CurrentMemoryUsageMB <= (_memoryThresholdBytes / (1024f * 1024f)),
            Snapshots = new List<MemorySnapshot>(_memorySnapshots)
        };
        
        if (_memorySnapshots.Count > 1)
        {
            report.AverageMemoryUsageMB = _memorySnapshots.Average(s => s.TotalMemory) / (1024f * 1024f);
            report.MemoryStandardDeviation = CalculateMemoryStandardDeviation();
        }
        
        return report;
    }
    
    /// <summary>
    /// 메모리 보고서를 문자열로 반환합니다.
    /// </summary>
    /// <returns>메모리 보고서 문자열</returns>
    public string GetMemoryReportString()
    {
        var report = GenerateMemoryReport();
        
        return $"=== Memory Profiling Report ===\n" +
               $"Profiling Duration: {report.ProfilingDuration:F1}s\n" +
               $"Baseline Memory: {report.BaselineMemoryMB:F1}MB\n" +
               $"Current Memory: {report.CurrentMemoryMB:F1}MB\n" +
               $"Peak Memory: {report.PeakMemoryMB:F1}MB\n" +
               $"Memory Increase: {report.MemoryIncreaseMB:F1}MB\n" +
               $"Average Memory: {report.AverageMemoryUsageMB:F1}MB\n" +
               $"GC Collections: {report.TotalGCCollections}\n" +
               $"Snapshots Taken: {report.SnapshotCount}\n" +
               $"Memory Leak Suspected: {(report.IsSuspectedMemoryLeak ? "YES" : "NO")}\n" +
               $"Within Threshold: {(report.IsWithinThreshold ? "YES" : "NO")}\n" +
               $"Standard Deviation: {report.MemoryStandardDeviation:F2}MB";
    }
    
    #endregion
    
    #region Profiling Coroutine
    
    private IEnumerator ProfilingCoroutine()
    {
        while (_isProfileActive)
        {
            TakeMemorySnapshot();
            CheckMemoryThresholds();
            
            yield return new WaitForSeconds(_samplingInterval);
        }
    }
    
    private void UpdateMemoryStatistics(long currentMemory)
    {
        _currentMemory = currentMemory;
        _peakMemory = Math.Max(_peakMemory, currentMemory);
        _minimumMemory = Math.Min(_minimumMemory, currentMemory);
    }
    
    private void CheckMemoryThresholds()
    {
        if (CurrentMemoryUsageMB > (_memoryThresholdBytes / (1024f * 1024f)))
        {
            OnMemoryAlert?.Invoke($"Memory usage exceeded threshold: {CurrentMemoryUsageMB:F1}MB");
        }
        
        if (MemoryIncreaseMB > (_memoryThresholdBytes / (1024f * 1024f)) * 0.5f)
        {
            OnMemoryAlert?.Invoke($"Significant memory increase detected: +{MemoryIncreaseMB:F1}MB");
        }
    }
    
    private void CheckForMemoryLeak(MemorySnapshot initial, MemorySnapshot final, int iterations)
    {
        long memoryIncrease = final.TotalMemory - initial.TotalMemory;
        float memoryIncreaseMB = memoryIncrease / (1024f * 1024f);
        
        bool isMemoryLeak = memoryIncrease > _memoryThresholdBytes;
        
        var leakReport = new MemoryLeakReport
        {
            InitialSnapshot = initial,
            FinalSnapshot = final,
            Iterations = iterations,
            MemoryIncrease = memoryIncrease,
            MemoryIncreaseMB = memoryIncreaseMB,
            IsMemoryLeakDetected = isMemoryLeak,
            LeakRate = memoryIncreaseMB / iterations,
            GCIncreaseCount = final.GCGeneration0 - initial.GCGeneration0 +
                             final.GCGeneration1 - initial.GCGeneration1 +
                             final.GCGeneration2 - initial.GCGeneration2
        };
        
        if (isMemoryLeak)
        {
            OnMemoryLeakDetected?.Invoke(leakReport);
            
            if (_enableDetailedLogging)
            {
                Debug.LogWarning($"Memory leak detected: +{memoryIncreaseMB:F1}MB after {iterations} iterations " +
                               $"(Rate: {leakReport.LeakRate:F3}MB/iteration)");
            }
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// 가비지 컬렉션을 강제 실행합니다.
    /// </summary>
    public void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        OnGarbageCollectionTriggered?.Invoke();
        
        if (_enableDetailedLogging)
        {
            Debug.Log($"Forced GC completed. Memory: {GC.GetTotalMemory(false) / (1024f * 1024f):F1}MB");
        }
    }
    
    /// <summary>
    /// 통계를 리셋합니다.
    /// </summary>
    private void ResetStatistics()
    {
        _memorySnapshots.Clear();
        _typeMemoryUsage.Clear();
        _peakMemory = _currentMemory;
        _minimumMemory = _currentMemory;
    }
    
    /// <summary>
    /// 메모리 사용량의 표준편차를 계산합니다.
    /// </summary>
    /// <returns>표준편차 (MB)</returns>
    private float CalculateMemoryStandardDeviation()
    {
        if (_memorySnapshots.Count < 2) return 0f;
        
        float average = _memorySnapshots.Average(s => s.TotalMemory) / (1024f * 1024f);
        float sumOfSquares = _memorySnapshots.Sum(s => 
        {
            float value = s.TotalMemory / (1024f * 1024f);
            return (value - average) * (value - average);
        });
        
        return Mathf.Sqrt(sumOfSquares / _memorySnapshots.Count);
    }
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// 메모리 프로파일러를 구성합니다.
    /// </summary>
    /// <param name="samplingInterval">샘플링 간격</param>
    /// <param name="maxSamples">최대 샘플 수</param>
    /// <param name="memoryThresholdMB">메모리 임계값 (MB)</param>
    /// <param name="defaultIterations">기본 반복 횟수</param>
    public void Configure(float samplingInterval = 1f, int maxSamples = 1000, 
                         float memoryThresholdMB = 30f, int defaultIterations = 50)
    {
        _samplingInterval = Mathf.Max(0.1f, samplingInterval);
        _maxSamples = Mathf.Max(10, maxSamples);
        _memoryThresholdBytes = (long)(memoryThresholdMB * 1024 * 1024);
        _defaultIterationCount = Mathf.Max(1, defaultIterations);
    }
    
    /// <summary>
    /// 상세 로깅을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enable">활성화 여부</param>
    public void EnableDetailedLogging(bool enable)
    {
        _enableDetailedLogging = enable;
    }
    
    /// <summary>
    /// 자동 가비지 컬렉션을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enable">활성화 여부</param>
    public void EnableAutoGarbageCollection(bool enable)
    {
        _enableAutoGC = enable;
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// 메모리 스냅샷 데이터
    /// </summary>
    [Serializable]
    public class MemorySnapshot
    {
        public DateTime Timestamp;
        public long TotalMemory;
        public long ManagedMemory;
        public long UnityMemory;
        public long MemoryIncrease;
        public int GCGeneration0;
        public int GCGeneration1;
        public int GCGeneration2;
        
        public float TotalMemoryMB => TotalMemory / (1024f * 1024f);
        public float ManagedMemoryMB => ManagedMemory / (1024f * 1024f);
        public float UnityMemoryMB => UnityMemory / (1024f * 1024f);
        public float MemoryIncreaseMB => MemoryIncrease / (1024f * 1024f);
    }
    
    /// <summary>
    /// 메모리 누수 보고서
    /// </summary>
    [Serializable]
    public class MemoryLeakReport
    {
        public MemorySnapshot InitialSnapshot;
        public MemorySnapshot FinalSnapshot;
        public int Iterations;
        public long MemoryIncrease;
        public float MemoryIncreaseMB;
        public bool IsMemoryLeakDetected;
        public float LeakRate; // MB per iteration
        public int GCIncreaseCount;
    }
    
    /// <summary>
    /// 종합 메모리 보고서
    /// </summary>
    [Serializable]
    public class MemoryReport
    {
        public float ProfilingDuration;
        public float BaselineMemoryMB;
        public float CurrentMemoryMB;
        public float PeakMemoryMB;
        public float MinimumMemoryMB;
        public float AverageMemoryUsageMB;
        public float MemoryIncreaseMB;
        public float MemoryStandardDeviation;
        public int TotalGCCollections;
        public int SnapshotCount;
        public bool IsSuspectedMemoryLeak;
        public bool IsWithinThreshold;
        public List<MemorySnapshot> Snapshots;
    }
    
    #endregion
}