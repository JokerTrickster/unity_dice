using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class PerformanceMonitor : MonoBehaviour
{
    [Header("Monitoring Settings")]
    [SerializeField] private bool enableMonitoring = true;
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private int maxSamples = 100;
    [SerializeField] private bool logToFile = true;
    [SerializeField] private bool showUI = true;
    
    [Header("Performance Thresholds")]
    [SerializeField] private float lowFpsThreshold = 30f;
    [SerializeField] private float highMemoryThreshold = 512f; // MB
    [SerializeField] private float highFrameTimeThreshold = 33.33f; // ms
    
    private List<PerformanceData> samples = new List<PerformanceData>();
    private PerformanceData currentData = new PerformanceData();
    private PerformanceLogger logger;
    private PerformanceUI performanceUI;
    private Coroutine monitoringCoroutine;
    
    // Events for performance warnings
    public static event Action<string> OnPerformanceWarning;
    public static event Action<PerformanceData> OnPerformanceUpdate;
    
    // Singleton pattern
    public static PerformanceMonitor Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMonitor();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeMonitor()
    {
        logger = GetComponent<PerformanceLogger>();
        if (logger == null && logToFile)
        {
            logger = gameObject.AddComponent<PerformanceLogger>();
        }
        
        performanceUI = GetComponent<PerformanceUI>();
        if (performanceUI == null && showUI)
        {
            performanceUI = gameObject.AddComponent<PerformanceUI>();
        }
        
        if (enableMonitoring)
        {
            StartMonitoring();
        }
    }
    
    public void StartMonitoring()
    {
        if (monitoringCoroutine == null)
        {
            monitoringCoroutine = StartCoroutine(MonitorPerformance());
            Debug.Log("[PerformanceMonitor] Monitoring started");
        }
    }
    
    public void StopMonitoring()
    {
        if (monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
            monitoringCoroutine = null;
            Debug.Log("[PerformanceMonitor] Monitoring stopped");
        }
    }
    
    private IEnumerator MonitorPerformance()
    {
        while (enableMonitoring)
        {
            CollectPerformanceData();
            CheckPerformanceThresholds();
            OnPerformanceUpdate?.Invoke(currentData);
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    private void CollectPerformanceData()
    {
        currentData = new PerformanceData();
        
        // Update all metrics
        currentData.UpdateFrameMetrics();
        currentData.UpdateMemoryMetrics();
        currentData.UpdateRenderMetrics();
        currentData.UpdateGameMetrics();
        
        // Add to samples
        samples.Add(currentData);
        
        // Maintain sample limit
        if (samples.Count > maxSamples)
        {
            samples.RemoveAt(0);
        }
        
        // Log if enabled
        if (logger != null && logToFile)
        {
            logger.LogPerformanceData(currentData);
        }
    }
    
    private void CheckPerformanceThresholds()
    {
        // Check FPS threshold
        if (currentData.fps < lowFpsThreshold)
        {
            OnPerformanceWarning?.Invoke($"Low FPS detected: {currentData.fps:F1}");
        }
        
        // Check memory threshold
        float memoryMB = currentData.usedMemory / (1024f * 1024f);
        if (memoryMB > highMemoryThreshold)
        {
            OnPerformanceWarning?.Invoke($"High memory usage: {memoryMB:F1} MB");
        }
        
        // Check frame time threshold
        if (currentData.frameTime > highFrameTimeThreshold)
        {
            OnPerformanceWarning?.Invoke($"High frame time: {currentData.frameTime:F2} ms");
        }
    }
    
    public PerformanceData GetCurrentData()
    {
        return currentData;
    }
    
    public List<PerformanceData> GetSamples()
    {
        return new List<PerformanceData>(samples);
    }
    
    public PerformanceStats GetStats()
    {
        if (samples.Count == 0) return new PerformanceStats();
        
        var fps = samples.Select(s => s.fps).ToList();
        var frameTime = samples.Select(s => s.frameTime).ToList();
        var memory = samples.Select(s => s.usedMemory / (1024f * 1024f)).ToList();
        
        return new PerformanceStats
        {
            avgFps = fps.Average(),
            minFps = fps.Min(),
            maxFps = fps.Max(),
            avgFrameTime = frameTime.Average(),
            minFrameTime = frameTime.Min(),
            maxFrameTime = frameTime.Max(),
            avgMemory = memory.Average(),
            minMemory = memory.Min(),
            maxMemory = memory.Max(),
            sampleCount = samples.Count
        };
    }
    
    public void ResetStats()
    {
        samples.Clear();
        Debug.Log("[PerformanceMonitor] Statistics reset");
    }
    
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.01f, interval);
    }
    
    public void ToggleMonitoring()
    {
        enableMonitoring = !enableMonitoring;
        if (enableMonitoring)
        {
            StartMonitoring();
        }
        else
        {
            StopMonitoring();
        }
    }
    
    private void OnDestroy()
    {
        StopMonitoring();
    }
}

[Serializable]
public struct PerformanceStats
{
    public float avgFps;
    public float minFps;
    public float maxFps;
    public float avgFrameTime;
    public float minFrameTime;
    public float maxFrameTime;
    public float avgMemory;
    public float minMemory;
    public float maxMemory;
    public int sampleCount;
}