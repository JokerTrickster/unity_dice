using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;

public class PerformanceLogger : MonoBehaviour
{
    [Header("Logging Settings")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool logToFile = true;
    [SerializeField] private bool logToConsole = false;
    [SerializeField] private string logDirectory = "PerformanceLogs";
    [SerializeField] private int maxLogFiles = 10;
    [SerializeField] private long maxLogFileSize = 10 * 1024 * 1024; // 10MB
    
    [Header("Log Formats")]
    [SerializeField] private LogFormat logFormat = LogFormat.CSV;
    [SerializeField] private bool includeTimestamp = true;
    [SerializeField] private bool separateResourceLog = true;
    
    private string currentLogFilePath;
    private string currentResourceLogFilePath;
    private Queue<string> logBuffer = new Queue<string>();
    private Queue<string> resourceLogBuffer = new Queue<string>();
    private bool isLogging = false;
    private SystemResourceTracker resourceTracker;
    
    // Log rotation
    private int currentLogFileIndex = 1;
    private long currentLogFileSize = 0;
    
    public enum LogFormat
    {
        CSV,
        JSON,
        XML,
        PlainText
    }
    
    // Events
    public static event Action<string> OnLogEntry;
    public static event Action<string> OnLogError;
    
    private void Start()
    {
        if (!enableLogging) return;
        
        resourceTracker = GetComponent<SystemResourceTracker>();
        if (resourceTracker == null)
        {
            resourceTracker = gameObject.AddComponent<SystemResourceTracker>();
        }
        
        InitializeLogging();
        
        // Subscribe to resource updates if separate logging is enabled
        if (separateResourceLog)
        {
            SystemResourceTracker.OnCpuUsageUpdate += LogCpuUsage;
            SystemResourceTracker.OnMemoryUsageUpdate += LogMemoryUsage;
        }
    }
    
    private void InitializeLogging()
    {
        try
        {
            // Create log directory
            string logDir = Path.Combine(Application.persistentDataPath, logDirectory);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            
            // Generate log file names with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionId = Guid.NewGuid().ToString("N")[..8];
            
            currentLogFilePath = Path.Combine(logDir, $"performance_{timestamp}_{sessionId}.{GetFileExtension()}");
            
            if (separateResourceLog)
            {
                currentResourceLogFilePath = Path.Combine(logDir, $"resources_{timestamp}_{sessionId}.{GetFileExtension()}");
            }
            
            // Write headers
            if (logToFile)
            {
                WriteLogHeader(currentLogFilePath, PerformanceData.GetLogHeader());
                
                if (separateResourceLog)
                {
                    WriteLogHeader(currentResourceLogFilePath, SystemResourceData.GetLogHeader());
                }
            }
            
            // Clean up old log files
            CleanupOldLogFiles(logDir);
            
            isLogging = true;
            Debug.Log($"[PerformanceLogger] Logging initialized. Files: {currentLogFilePath}");
            
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to initialize logging: {e.Message}");
            OnLogError?.Invoke($"Logging initialization failed: {e.Message}");
        }
    }
    
    private string GetFileExtension()
    {
        return logFormat switch
        {
            LogFormat.CSV => "csv",
            LogFormat.JSON => "json",
            LogFormat.XML => "xml",
            LogFormat.PlainText => "txt",
            _ => "csv"
        };
    }
    
    private void WriteLogHeader(string filePath, string header)
    {
        try
        {
            if (logFormat == LogFormat.CSV)
            {
                File.WriteAllText(filePath, header + Environment.NewLine);
            }
            else if (logFormat == LogFormat.JSON)
            {
                File.WriteAllText(filePath, "[\n");
            }
            else if (logFormat == LogFormat.XML)
            {
                File.WriteAllText(filePath, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<PerformanceLog>\n");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to write header: {e.Message}");
        }
    }
    
    public void LogPerformanceData(PerformanceData data)
    {
        if (!isLogging || !enableLogging) return;
        
        try
        {
            string logEntry = FormatLogEntry(data);
            
            if (logToConsole)
            {
                Debug.Log($"[Performance] {logEntry}");
            }
            
            if (logToFile)
            {
                logBuffer.Enqueue(logEntry);
                ProcessLogBuffer();
            }
            
            OnLogEntry?.Invoke(logEntry);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to log performance data: {e.Message}");
            OnLogError?.Invoke($"Performance logging failed: {e.Message}");
        }
    }
    
    private void LogCpuUsage(float cpuUsage)
    {
        if (!separateResourceLog || !isLogging) return;
        
        var resourceData = resourceTracker?.GetCurrentResourceData() ?? new SystemResourceData();
        string logEntry = FormatResourceLogEntry(resourceData);
        
        if (logToFile)
        {
            resourceLogBuffer.Enqueue(logEntry);
            ProcessResourceLogBuffer();
        }
    }
    
    private void LogMemoryUsage(long used, long total, float percentage)
    {
        // Memory logging is handled in LogCpuUsage to avoid duplicate entries
    }
    
    private string FormatLogEntry(PerformanceData data)
    {
        return logFormat switch
        {
            LogFormat.CSV => data.ToLogString(),
            LogFormat.JSON => FormatAsJson(data),
            LogFormat.XML => FormatAsXml(data),
            LogFormat.PlainText => FormatAsPlainText(data),
            _ => data.ToLogString()
        };
    }
    
    private string FormatResourceLogEntry(SystemResourceData data)
    {
        return logFormat switch
        {
            LogFormat.CSV => data.ToLogString(),
            LogFormat.JSON => FormatResourceAsJson(data),
            LogFormat.XML => FormatResourceAsXml(data),
            LogFormat.PlainText => FormatResourceAsPlainText(data),
            _ => data.ToLogString()
        };
    }
    
    private string FormatAsJson(PerformanceData data)
    {
        var json = new StringBuilder();
        json.Append("  {\n");
        json.Append($"    \"timestamp\": \"{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}\",\n");
        json.Append($"    \"fps\": {data.fps:F1},\n");
        json.Append($"    \"frameTime\": {data.frameTime:F2},\n");
        json.Append($"    \"deltaTime\": {data.deltaTime:F4},\n");
        json.Append($"    \"totalMemory\": {data.totalMemory},\n");
        json.Append($"    \"usedMemory\": {data.usedMemory},\n");
        json.Append($"    \"gcMemory\": {data.gcMemory},\n");
        json.Append($"    \"cpuUsage\": {data.cpuUsage:F1},\n");
        json.Append($"    \"drawCalls\": {data.drawCalls},\n");
        json.Append($"    \"triangles\": {data.triangles},\n");
        json.Append($"    \"vertices\": {data.vertices},\n");
        json.Append($"    \"activeDice\": {data.activeDice},\n");
        json.Append($"    \"totalGameObjects\": {data.totalGameObjects},\n");
        json.Append($"    \"cameraPosition\": {{\n");
        json.Append($"      \"x\": {data.cameraPosition.x:F2},\n");
        json.Append($"      \"y\": {data.cameraPosition.y:F2},\n");
        json.Append($"      \"z\": {data.cameraPosition.z:F2}\n");
        json.Append($"    }}\n");
        json.Append("  }");
        return json.ToString();
    }
    
    private string FormatAsXml(PerformanceData data)
    {
        var xml = new StringBuilder();
        xml.Append("  <Entry>\n");
        xml.Append($"    <Timestamp>{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}</Timestamp>\n");
        xml.Append($"    <FPS>{data.fps:F1}</FPS>\n");
        xml.Append($"    <FrameTime>{data.frameTime:F2}</FrameTime>\n");
        xml.Append($"    <DeltaTime>{data.deltaTime:F4}</DeltaTime>\n");
        xml.Append($"    <TotalMemory>{data.totalMemory}</TotalMemory>\n");
        xml.Append($"    <UsedMemory>{data.usedMemory}</UsedMemory>\n");
        xml.Append($"    <GCMemory>{data.gcMemory}</GCMemory>\n");
        xml.Append($"    <CPUUsage>{data.cpuUsage:F1}</CPUUsage>\n");
        xml.Append($"    <DrawCalls>{data.drawCalls}</DrawCalls>\n");
        xml.Append($"    <Triangles>{data.triangles}</Triangles>\n");
        xml.Append($"    <Vertices>{data.vertices}</Vertices>\n");
        xml.Append($"    <ActiveDice>{data.activeDice}</ActiveDice>\n");
        xml.Append($"    <TotalGameObjects>{data.totalGameObjects}</TotalGameObjects>\n");
        xml.Append($"    <Camera x=\"{data.cameraPosition.x:F2}\" y=\"{data.cameraPosition.y:F2}\" z=\"{data.cameraPosition.z:F2}\" />\n");
        xml.Append("  </Entry>\n");
        return xml.ToString();
    }
    
    private string FormatAsPlainText(PerformanceData data)
    {
        return $"[{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}] FPS: {data.fps:F1}, " +
               $"Frame: {data.frameTime:F2}ms, Memory: {data.usedMemory / (1024 * 1024):F1}MB, " +
               $"CPU: {data.cpuUsage:F1}%, Dice: {data.activeDice}, Objects: {data.totalGameObjects}";
    }
    
    private string FormatResourceAsJson(SystemResourceData data)
    {
        var json = new StringBuilder();
        json.Append("  {\n");
        json.Append($"    \"timestamp\": \"{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}\",\n");
        json.Append($"    \"cpuUsage\": {data.cpuUsage:F2},\n");
        json.Append($"    \"systemMemoryUsed\": {data.systemMemoryUsed},\n");
        json.Append($"    \"systemMemoryTotal\": {data.systemMemoryTotal},\n");
        json.Append($"    \"systemMemoryPercentage\": {data.systemMemoryPercentage:F2},\n");
        json.Append($"    \"gcCollectionCount\": {data.gcCollectionCount},\n");
        json.Append($"    \"processId\": {data.processId}\n");
        json.Append("  }");
        return json.ToString();
    }
    
    private string FormatResourceAsXml(SystemResourceData data)
    {
        var xml = new StringBuilder();
        xml.Append("  <ResourceEntry>\n");
        xml.Append($"    <Timestamp>{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}</Timestamp>\n");
        xml.Append($"    <CPUUsage>{data.cpuUsage:F2}</CPUUsage>\n");
        xml.Append($"    <SystemMemoryUsed>{data.systemMemoryUsed}</SystemMemoryUsed>\n");
        xml.Append($"    <SystemMemoryTotal>{data.systemMemoryTotal}</SystemMemoryTotal>\n");
        xml.Append($"    <SystemMemoryPercentage>{data.systemMemoryPercentage:F2}</SystemMemoryPercentage>\n");
        xml.Append($"    <GCCollectionCount>{data.gcCollectionCount}</GCCollectionCount>\n");
        xml.Append($"    <ProcessID>{data.processId}</ProcessID>\n");
        xml.Append("  </ResourceEntry>\n");
        return xml.ToString();
    }
    
    private string FormatResourceAsPlainText(SystemResourceData data)
    {
        return $"[{data.timestamp:yyyy-MM-dd HH:mm:ss.fff}] CPU: {data.cpuUsage:F2}%, " +
               $"System Memory: {data.systemMemoryPercentage:F2}% ({data.systemMemoryUsed / (1024 * 1024):F1}MB), " +
               $"GC: {data.gcCollectionCount}, PID: {data.processId}";
    }
    
    private async void ProcessLogBuffer()
    {
        if (logBuffer.Count == 0) return;
        
        try
        {
            var entries = new List<string>();
            while (logBuffer.Count > 0 && entries.Count < 50) // Process in batches
            {
                entries.Add(logBuffer.Dequeue());
            }
            
            await WriteLogEntriesAsync(currentLogFilePath, entries);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to process log buffer: {e.Message}");
        }
    }
    
    private async void ProcessResourceLogBuffer()
    {
        if (resourceLogBuffer.Count == 0 || !separateResourceLog) return;
        
        try
        {
            var entries = new List<string>();
            while (resourceLogBuffer.Count > 0 && entries.Count < 50)
            {
                entries.Add(resourceLogBuffer.Dequeue());
            }
            
            await WriteLogEntriesAsync(currentResourceLogFilePath, entries);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to process resource log buffer: {e.Message}");
        }
    }
    
    private async Task WriteLogEntriesAsync(string filePath, List<string> entries)
    {
        if (entries.Count == 0) return;
        
        try
        {
            await Task.Run(() =>
            {
                var content = new StringBuilder();
                
                if (logFormat == LogFormat.JSON && currentLogFileSize > 0)
                {
                    content.Append(",\n");
                }
                
                for (int i = 0; i < entries.Count; i++)
                {
                    content.Append(entries[i]);
                    if (logFormat == LogFormat.JSON && i < entries.Count - 1)
                    {
                        content.Append(",");
                    }
                    content.Append("\n");
                }
                
                File.AppendAllText(filePath, content.ToString());
                currentLogFileSize += content.Length;
                
                // Check for log rotation
                if (currentLogFileSize > maxLogFileSize)
                {
                    RotateLogFile(filePath);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to write log entries: {e.Message}");
        }
    }
    
    private void RotateLogFile(string filePath)
    {
        try
        {
            // Close current file properly for JSON/XML
            if (logFormat == LogFormat.JSON)
            {
                File.AppendAllText(filePath, "\n]");
            }
            else if (logFormat == LogFormat.XML)
            {
                File.AppendAllText(filePath, "</PerformanceLog>");
            }
            
            // Create new log file
            currentLogFileIndex++;
            string directory = Path.GetDirectoryName(filePath);
            string filename = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            
            string newFilePath = Path.Combine(directory, $"{filename}_{currentLogFileIndex:D3}{extension}");
            
            if (filePath == currentLogFilePath)
            {
                currentLogFilePath = newFilePath;
            }
            else if (filePath == currentResourceLogFilePath)
            {
                currentResourceLogFilePath = newFilePath;
            }
            
            WriteLogHeader(newFilePath, filePath == currentLogFilePath ? 
                          PerformanceData.GetLogHeader() : 
                          SystemResourceData.GetLogHeader());
            
            currentLogFileSize = 0;
            
            Debug.Log($"[PerformanceLogger] Log file rotated to: {newFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to rotate log file: {e.Message}");
        }
    }
    
    private void CleanupOldLogFiles(string logDirectory)
    {
        try
        {
            var logFiles = Directory.GetFiles(logDirectory, "*.csv")
                          .Concat(Directory.GetFiles(logDirectory, "*.json"))
                          .Concat(Directory.GetFiles(logDirectory, "*.xml"))
                          .Concat(Directory.GetFiles(logDirectory, "*.txt"))
                          .Select(f => new FileInfo(f))
                          .OrderByDescending(f => f.CreationTime)
                          .ToList();
            
            if (logFiles.Count > maxLogFiles)
            {
                for (int i = maxLogFiles; i < logFiles.Count; i++)
                {
                    logFiles[i].Delete();
                    Debug.Log($"[PerformanceLogger] Deleted old log file: {logFiles[i].Name}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Failed to cleanup old log files: {e.Message}");
        }
    }
    
    public void FlushLogs()
    {
        ProcessLogBuffer();
        ProcessResourceLogBuffer();
    }
    
    public void SetLogLevel(LogLevel level)
    {
        // Implementation for different log levels if needed
    }
    
    public string GetCurrentLogFilePath()
    {
        return currentLogFilePath;
    }
    
    public string GetCurrentResourceLogFilePath()
    {
        return currentResourceLogFilePath;
    }
    
    private void OnDestroy()
    {
        try
        {
            // Unsubscribe from events
            SystemResourceTracker.OnCpuUsageUpdate -= LogCpuUsage;
            SystemResourceTracker.OnMemoryUsageUpdate -= LogMemoryUsage;
            
            // Flush remaining logs
            FlushLogs();
            
            // Close files properly
            if (logFormat == LogFormat.JSON && currentLogFilePath != null)
            {
                File.AppendAllText(currentLogFilePath, "\n]");
                if (separateResourceLog && currentResourceLogFilePath != null)
                {
                    File.AppendAllText(currentResourceLogFilePath, "\n]");
                }
            }
            else if (logFormat == LogFormat.XML && currentLogFilePath != null)
            {
                File.AppendAllText(currentLogFilePath, "</PerformanceLog>");
                if (separateResourceLog && currentResourceLogFilePath != null)
                {
                    File.AppendAllText(currentResourceLogFilePath, "</PerformanceLog>");
                }
            }
            
            isLogging = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PerformanceLogger] Cleanup failed: {e.Message}");
        }
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}