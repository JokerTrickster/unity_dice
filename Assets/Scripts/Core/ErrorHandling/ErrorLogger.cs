using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 오류 로깅 시스템
/// 오류 정보를 파일과 콘솔에 기록하고 분석할 수 있는 형태로 저장합니다.
/// </summary>
public class ErrorLogger
{
    #region Configuration
    private bool _loggingEnabled = true;
    private string _logFilePath;
    private readonly int _maxLogFileSize = 10 * 1024 * 1024; // 10MB
    private readonly int _maxLogFiles = 5;
    private readonly object _lockObject = new object();
    #endregion

    #region Properties
    /// <summary>
    /// 로깅 활성화 여부
    /// </summary>
    public bool IsLoggingEnabled => _loggingEnabled;
    
    /// <summary>
    /// 로그 파일 경로
    /// </summary>
    public string LogFilePath => _logFilePath;
    #endregion

    #region Initialization
    /// <summary>
    /// 로거 초기화
    /// </summary>
    public void Initialize(bool enabled)
    {
        _loggingEnabled = enabled;
        
        if (_loggingEnabled)
        {
            SetupLogFile();
            LogMessage("=== Error Logger Initialized ===", LogLevel.Info);
        }
    }

    /// <summary>
    /// 로그 파일 설정
    /// </summary>
    private void SetupLogFile()
    {
        try
        {
            string logDir = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd");
            _logFilePath = Path.Combine(logDir, $"error_log_{timestamp}.txt");
            
            // 로그 파일 크기 체크 및 로테이션
            CheckLogRotation();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to setup log file: {ex.Message}");
            _loggingEnabled = false;
        }
    }
    #endregion

    #region Error Logging
    /// <summary>
    /// 오류 로깅
    /// </summary>
    public void LogError(ErrorInfo errorInfo)
    {
        if (!_loggingEnabled) return;

        try
        {
            string logEntry = FormatErrorLogEntry(errorInfo);
            WriteToFile(logEntry);
            WriteToConsole(errorInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to log error: {ex.Message}");
        }
    }

    /// <summary>
    /// 오류 복구 로깅
    /// </summary>
    public void LogRecovery(ErrorInfo errorInfo)
    {
        if (!_loggingEnabled) return;

        try
        {
            string logEntry = FormatRecoveryLogEntry(errorInfo);
            WriteToFile(logEntry);
            Debug.Log($"[ErrorLogger] RECOVERY: {errorInfo.Type}:{errorInfo.Code} - {errorInfo.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to log recovery: {ex.Message}");
        }
    }

    /// <summary>
    /// 일반 메시지 로깅
    /// </summary>
    public void LogMessage(string message, LogLevel level)
    {
        if (!_loggingEnabled) return;

        try
        {
            string logEntry = FormatMessageLogEntry(message, level);
            WriteToFile(logEntry);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to log message: {ex.Message}");
        }
    }
    #endregion

    #region Log Formatting
    /// <summary>
    /// 오류 로그 엔트리 포맷팅
    /// </summary>
    private string FormatErrorLogEntry(ErrorInfo errorInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR");
        sb.AppendLine($"Type: {errorInfo.Type}");
        sb.AppendLine($"Code: {errorInfo.Code}");
        sb.AppendLine($"Severity: {errorInfo.Severity}");
        sb.AppendLine($"Message: {errorInfo.Message}");
        sb.AppendLine($"Context: {errorInfo.Context}");
        sb.AppendLine($"Retry Count: {errorInfo.RetryCount}");
        
        if (!string.IsNullOrEmpty(errorInfo.UserMessage))
        {
            sb.AppendLine($"User Message: {errorInfo.UserMessage}");
        }
        
        if (!string.IsNullOrEmpty(errorInfo.StackTrace))
        {
            sb.AppendLine($"Stack Trace: {errorInfo.StackTrace}");
        }
        
        if (errorInfo.Metadata?.Count > 0)
        {
            sb.AppendLine("Metadata:");
            foreach (var kvp in errorInfo.Metadata)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        sb.AppendLine("----------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// 복구 로그 엔트리 포맷팅
    /// </summary>
    private string FormatRecoveryLogEntry(ErrorInfo errorInfo)
    {
        return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] RECOVERY - {errorInfo.Type}:{errorInfo.Code} - {errorInfo.Message}\n";
    }

    /// <summary>
    /// 메시지 로그 엔트리 포맷팅
    /// </summary>
    private string FormatMessageLogEntry(string message, LogLevel level)
    {
        return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level.ToString().ToUpper()} - {message}\n";
    }
    #endregion

    #region File Operations
    /// <summary>
    /// 파일에 쓰기
    /// </summary>
    private void WriteToFile(string content)
    {
        if (string.IsNullOrEmpty(_logFilePath)) return;

        lock (_lockObject)
        {
            try
            {
                File.AppendAllText(_logFilePath, content);
                
                // 파일 크기 체크
                CheckLogRotation();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ErrorLogger] Failed to write to file: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 콘솔에 쓰기
    /// </summary>
    private void WriteToConsole(ErrorInfo errorInfo)
    {
        string message = $"[{errorInfo.Type}:{errorInfo.Code}] {errorInfo.Message}";
        
        switch (errorInfo.Severity)
        {
            case ErrorSeverity.Critical:
                Debug.LogError($"<color=red><b>{message}</b></color>");
                break;
            case ErrorSeverity.High:
                Debug.LogError(message);
                break;
            case ErrorSeverity.Medium:
                Debug.LogWarning(message);
                break;
            case ErrorSeverity.Low:
                Debug.Log($"<color=yellow>{message}</color>");
                break;
        }
    }

    /// <summary>
    /// 로그 로테이션 체크
    /// </summary>
    private void CheckLogRotation()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;

            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length > _maxLogFileSize)
            {
                RotateLogFiles();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to check log rotation: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그 파일 로테이션
    /// </summary>
    private void RotateLogFiles()
    {
        try
        {
            string logDir = Path.GetDirectoryName(_logFilePath);
            string baseFileName = Path.GetFileNameWithoutExtension(_logFilePath);
            string extension = Path.GetExtension(_logFilePath);

            // 기존 파일들을 번호를 증가시켜 이동
            for (int i = _maxLogFiles - 1; i >= 1; i--)
            {
                string oldFile = Path.Combine(logDir, $"{baseFileName}.{i}{extension}");
                string newFile = Path.Combine(logDir, $"{baseFileName}.{i + 1}{extension}");
                
                if (File.Exists(oldFile))
                {
                    if (File.Exists(newFile))
                        File.Delete(newFile);
                    File.Move(oldFile, newFile);
                }
            }

            // 현재 파일을 .1으로 이동
            string archivedFile = Path.Combine(logDir, $"{baseFileName}.1{extension}");
            if (File.Exists(archivedFile))
                File.Delete(archivedFile);
            File.Move(_logFilePath, archivedFile);

            // 최대 개수 초과 파일 삭제
            string oldestFile = Path.Combine(logDir, $"{baseFileName}.{_maxLogFiles + 1}{extension}");
            if (File.Exists(oldestFile))
                File.Delete(oldestFile);

            Debug.Log($"[ErrorLogger] Log files rotated. Current: {_logFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to rotate log files: {ex.Message}");
        }
    }
    #endregion

    #region Analysis and Reporting
    /// <summary>
    /// 오류 리포트 생성
    /// </summary>
    public string GenerateErrorReport(DateTime? startDate = null, DateTime? endDate = null)
    {
        if (!_loggingEnabled) return "Logging is disabled";

        try
        {
            var report = new StringBuilder();
            report.AppendLine("=== Error Report ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            if (startDate.HasValue && endDate.HasValue)
            {
                report.AppendLine($"Period: {startDate.Value:yyyy-MM-dd} to {endDate.Value:yyyy-MM-dd}");
            }
            
            report.AppendLine();

            // 로그 파일에서 오류 통계 분석
            var errorStats = AnalyzeErrorLogs(startDate, endDate);
            
            report.AppendLine("Error Statistics:");
            foreach (var kvp in errorStats)
            {
                report.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            return report.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to generate error report: {ex.Message}";
        }
    }

    /// <summary>
    /// 오류 로그 분석
    /// </summary>
    private Dictionary<string, int> AnalyzeErrorLogs(DateTime? startDate, DateTime? endDate)
    {
        var stats = new Dictionary<string, int>();
        
        try
        {
            if (!File.Exists(_logFilePath)) return stats;

            string[] lines = File.ReadAllLines(_logFilePath);
            
            foreach (string line in lines)
            {
                if (line.Contains("ERROR"))
                {
                    // 간단한 통계 추출 (실제로는 더 복잡한 파싱이 필요)
                    if (line.Contains("Type: "))
                    {
                        string type = ExtractValueFromLine(line, "Type: ");
                        stats[type] = stats.GetValueOrDefault(type, 0) + 1;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to analyze error logs: {ex.Message}");
        }

        return stats;
    }

    /// <summary>
    /// 라인에서 값 추출
    /// </summary>
    private string ExtractValueFromLine(string line, string prefix)
    {
        int startIndex = line.IndexOf(prefix);
        if (startIndex >= 0)
        {
            startIndex += prefix.Length;
            int endIndex = line.IndexOf('\n', startIndex);
            if (endIndex < 0) endIndex = line.Length;
            return line.Substring(startIndex, endIndex - startIndex).Trim();
        }
        return "Unknown";
    }

    /// <summary>
    /// 로그 파일 목록 반환
    /// </summary>
    public List<string> GetLogFiles()
    {
        var logFiles = new List<string>();
        
        try
        {
            string logDir = Path.GetDirectoryName(_logFilePath);
            if (Directory.Exists(logDir))
            {
                string pattern = "error_log_*.txt";
                logFiles.AddRange(Directory.GetFiles(logDir, pattern));
                logFiles.Sort((x, y) => File.GetLastWriteTime(y).CompareTo(File.GetLastWriteTime(x)));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ErrorLogger] Failed to get log files: {ex.Message}");
        }

        return logFiles;
    }

    /// <summary>
    /// 로그 파일 내용 읽기
    /// </summary>
    public string ReadLogFile(string filePath, int maxLines = 1000)
    {
        try
        {
            if (!File.Exists(filePath)) return "File not found";

            string[] lines = File.ReadAllLines(filePath);
            int startIndex = Math.Max(0, lines.Length - maxLines);
            
            var sb = new StringBuilder();
            for (int i = startIndex; i < lines.Length; i++)
            {
                sb.AppendLine(lines[i]);
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read log file: {ex.Message}";
        }
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Cleanup()
    {
        if (_loggingEnabled)
        {
            LogMessage("=== Error Logger Shutdown ===", LogLevel.Info);
        }
        _loggingEnabled = false;
    }
    #endregion
}

/// <summary>
/// 로그 레벨 (ErrorHandling 용)
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}