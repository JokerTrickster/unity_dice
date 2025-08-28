using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

public class SystemResourceTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    [SerializeField] private bool trackCpuUsage = true;
    [SerializeField] private bool trackSystemMemory = true;
    [SerializeField] private float updateInterval = 1.0f;
    
    // Platform-specific CPU tracking
    private PerformanceCounter cpuCounter;
    private Process currentProcess;
    private DateTime lastCpuTime;
    private TimeSpan lastTotalProcessorTime;
    
    // Memory tracking
    private long lastGcMemory;
    private int gcCollectionCount;
    
    // Public properties
    public float CurrentCpuUsage { get; private set; }
    public long SystemMemoryUsed { get; private set; }
    public long SystemMemoryTotal { get; private set; }
    public float SystemMemoryPercentage { get; private set; }
    public int GcCollectionCount { get; private set; }
    
    // Events
    public static event Action<float> OnCpuUsageUpdate;
    public static event Action<long, long, float> OnMemoryUsageUpdate;
    
    private void Start()
    {
        InitializeTracking();
        InvokeRepeating(nameof(UpdateResourceUsage), 0f, updateInterval);
    }
    
    private void InitializeTracking()
    {
        try
        {
            currentProcess = Process.GetCurrentProcess();
            lastCpuTime = DateTime.UtcNow;
            lastTotalProcessorTime = currentProcess.TotalProcessorTime;
            
            // Initialize CPU counter for Windows
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    cpuCounter = new PerformanceCounter("Process", "% Processor Time", currentProcess.ProcessName);
                    cpuCounter.NextValue(); // First call returns 0, need to call twice
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Failed to initialize CPU counter: {e.Message}");
                    cpuCounter = null;
                }
            }
            
            lastGcMemory = GC.GetTotalMemory(false);
            gcCollectionCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[SystemResourceTracker] Failed to initialize: {e.Message}");
        }
    }
    
    private void UpdateResourceUsage()
    {
        if (trackCpuUsage)
        {
            UpdateCpuUsage();
        }
        
        if (trackSystemMemory)
        {
            UpdateMemoryUsage();
        }
        
        UpdateGarbageCollectionStats();
    }
    
    private void UpdateCpuUsage()
    {
        try
        {
            float cpuUsage = 0f;
            
            // Platform-specific CPU usage calculation
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                if (cpuCounter != null)
                {
                    cpuUsage = cpuCounter.NextValue();
                }
                else
                {
                    cpuUsage = CalculateCpuUsageGeneric();
                }
            }
            else
            {
                cpuUsage = CalculateCpuUsageGeneric();
            }
            
            CurrentCpuUsage = Mathf.Clamp(cpuUsage, 0f, 100f);
            OnCpuUsageUpdate?.Invoke(CurrentCpuUsage);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] CPU usage update failed: {e.Message}");
        }
    }
    
    private float CalculateCpuUsageGeneric()
    {
        try
        {
            if (currentProcess != null)
            {
                DateTime currentTime = DateTime.UtcNow;
                TimeSpan currentTotalProcessorTime = currentProcess.TotalProcessorTime;
                
                double cpuUsedMs = (currentTotalProcessorTime - lastTotalProcessorTime).TotalMilliseconds;
                double totalMsPassed = (currentTime - lastCpuTime).TotalMilliseconds;
                double cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                lastCpuTime = currentTime;
                lastTotalProcessorTime = currentTotalProcessorTime;
                
                return (float)(cpuUsageTotal * 100);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Generic CPU calculation failed: {e.Message}");
        }
        
        return 0f;
    }
    
    private void UpdateMemoryUsage()
    {
        try
        {
            // Unity-specific memory tracking
            long unityTotalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(0);
            long unityReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemory(0);
            
            // System memory (if available)
            if (currentProcess != null)
            {
                SystemMemoryUsed = currentProcess.WorkingSet64;
                
                // Get total system memory (platform-specific)
                SystemMemoryTotal = GetTotalSystemMemory();
                
                if (SystemMemoryTotal > 0)
                {
                    SystemMemoryPercentage = (float)SystemMemoryUsed / SystemMemoryTotal * 100f;
                }
            }
            
            OnMemoryUsageUpdate?.Invoke(SystemMemoryUsed, SystemMemoryTotal, SystemMemoryPercentage);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Memory usage update failed: {e.Message}");
        }
    }
    
    private long GetTotalSystemMemory()
    {
        try
        {
            // Platform-specific memory detection
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                return GetWindowsSystemMemory();
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer || 
                     Application.platform == RuntimePlatform.OSXEditor)
            {
                return GetMacSystemMemory();
            }
            else if (Application.platform == RuntimePlatform.LinuxPlayer || 
                     Application.platform == RuntimePlatform.LinuxEditor)
            {
                return GetLinuxSystemMemory();
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Failed to get system memory: {e.Message}");
        }
        
        return 0;
    }
    
    // Windows API
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }
    
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    
    private long GetWindowsSystemMemory()
    {
        try
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return (long)memStatus.ullTotalPhys;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Windows memory detection failed: {e.Message}");
        }
        return 0;
    }
    
    private long GetMacSystemMemory()
    {
        // macOS memory detection would require native plugins or system calls
        // For now, return 0 and rely on process memory
        return 0;
    }
    
    private long GetLinuxSystemMemory()
    {
        // Linux memory detection would require reading /proc/meminfo
        // For now, return 0 and rely on process memory
        return 0;
    }
    
    private void UpdateGarbageCollectionStats()
    {
        try
        {
            int currentGcCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            GcCollectionCount = currentGcCount - gcCollectionCount;
            gcCollectionCount = currentGcCount;
            
            long currentGcMemory = GC.GetTotalMemory(false);
            long gcMemoryDelta = currentGcMemory - lastGcMemory;
            lastGcMemory = currentGcMemory;
            
            // Log significant GC events
            if (GcCollectionCount > 0)
            {
                UnityEngine.Debug.Log($"[SystemResourceTracker] GC occurred {GcCollectionCount} times, memory delta: {gcMemoryDelta / 1024f:F1} KB");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] GC stats update failed: {e.Message}");
        }
    }
    
    public SystemResourceData GetCurrentResourceData()
    {
        return new SystemResourceData
        {
            cpuUsage = CurrentCpuUsage,
            systemMemoryUsed = SystemMemoryUsed,
            systemMemoryTotal = SystemMemoryTotal,
            systemMemoryPercentage = SystemMemoryPercentage,
            gcCollectionCount = GcCollectionCount,
            processId = currentProcess?.Id ?? 0,
            timestamp = DateTime.Now
        };
    }
    
    private void OnDestroy()
    {
        try
        {
            cpuCounter?.Dispose();
            currentProcess?.Dispose();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SystemResourceTracker] Cleanup failed: {e.Message}");
        }
    }
}

[Serializable]
public struct SystemResourceData
{
    public float cpuUsage;
    public long systemMemoryUsed;
    public long systemMemoryTotal;
    public float systemMemoryPercentage;
    public int gcCollectionCount;
    public int processId;
    public DateTime timestamp;
    
    public string ToLogString()
    {
        return $"{timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
               $"{cpuUsage:F2},{systemMemoryUsed},{systemMemoryTotal}," +
               $"{systemMemoryPercentage:F2},{gcCollectionCount},{processId}";
    }
    
    public static string GetLogHeader()
    {
        return "Timestamp,CPUUsage,SystemMemoryUsed,SystemMemoryTotal,SystemMemoryPercentage,GCCount,ProcessID";
    }
}