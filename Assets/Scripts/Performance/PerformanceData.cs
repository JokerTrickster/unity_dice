using System;
using UnityEngine;

[Serializable]
public class PerformanceData
{
    [Header("Frame Performance")]
    public float fps;
    public float frameTime;
    public float deltaTime;
    
    [Header("Memory Usage")]
    public long totalMemory;
    public long usedMemory;
    public long gcMemory;
    
    [Header("System Resources")]
    public float cpuUsage;
    public int drawCalls;
    public int triangles;
    public int vertices;
    
    [Header("Game Specific")]
    public int activeDice;
    public int totalGameObjects;
    public Vector3 cameraPosition;
    
    public DateTime timestamp;
    
    public PerformanceData()
    {
        timestamp = DateTime.Now;
    }
    
    public void UpdateFrameMetrics()
    {
        fps = 1.0f / Time.unscaledDeltaTime;
        frameTime = Time.unscaledDeltaTime * 1000f;
        deltaTime = Time.deltaTime;
    }
    
    public void UpdateMemoryMetrics()
    {
        totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(0);
        usedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemory(0);
        gcMemory = GC.GetTotalMemory(false);
    }
    
    public void UpdateRenderMetrics()
    {
        drawCalls = UnityEngine.Rendering.DebugUI.Panel.children.Count;
        // Note: Getting exact draw calls requires Graphics API access
        // This is a simplified approach for monitoring
    }
    
    public void UpdateGameMetrics()
    {
        activeDice = GameObject.FindGameObjectsWithTag("Dice")?.Length ?? 0;
        totalGameObjects = FindObjectsOfType<GameObject>().Length;
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            cameraPosition = mainCam.transform.position;
        }
    }
    
    public string ToLogString()
    {
        return $"{timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
               $"{fps:F1},{frameTime:F2},{deltaTime:F4}," +
               $"{totalMemory},{usedMemory},{gcMemory}," +
               $"{cpuUsage:F1},{drawCalls},{triangles},{vertices}," +
               $"{activeDice},{totalGameObjects}," +
               $"{cameraPosition.x:F2},{cameraPosition.y:F2},{cameraPosition.z:F2}";
    }
    
    public static string GetLogHeader()
    {
        return "Timestamp,FPS,FrameTime(ms),DeltaTime,TotalMemory,UsedMemory,GCMemory," +
               "CPUUsage,DrawCalls,Triangles,Vertices,ActiveDice,TotalGameObjects," +
               "CamX,CamY,CamZ";
    }
}