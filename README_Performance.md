# Unity Dice Performance Monitoring System

Complete performance monitoring solution for Unity dice game with real-time metrics, system resource tracking, and comprehensive logging.

## Features

### 📊 Real-time Performance Metrics
- **FPS Monitoring**: Frame rate with color-coded warnings
- **Frame Time**: Millisecond precision timing
- **Memory Usage**: Unity and system memory tracking  
- **CPU Usage**: Real-time processor utilization
- **Render Statistics**: Draw calls, triangles, vertices
- **Game Metrics**: Active dice count, total GameObjects
- **Camera Tracking**: Real-time camera position

### 🎮 Interactive UI Display
- **Toggle with F1**: Show/hide performance overlay
- **Color-coded Metrics**: Green (good), Yellow (warning), Red (critical)
- **Live FPS Graph**: Visual performance trends
- **Statistics Panel**: Average, min, max values
- **Warning System**: Real-time performance alerts

### 📝 Comprehensive Logging
- **Multiple Formats**: CSV, JSON, XML, Plain Text
- **Automatic Log Rotation**: 10MB file size limit
- **Separate Resource Logs**: CPU/Memory in dedicated files
- **Log Cleanup**: Maintains max 10 log files
- **Async Writing**: Non-blocking performance

### 🔧 System Resource Tracking
- **Cross-platform CPU Usage**: Windows, macOS, Linux support
- **System Memory Monitoring**: Total and used memory
- **Garbage Collection Stats**: GC events and memory delta
- **Process Monitoring**: Process ID and resource usage

## Quick Setup

### Automatic Setup
1. Add `PerformanceSetup` component to any GameObject
2. Enable "Setup On Awake" in inspector
3. Performance monitoring starts automatically

### Manual Setup
```csharp
// Add to any GameObject in scene
var monitor = gameObject.AddComponent<PerformanceMonitor>();
var ui = gameObject.AddComponent<PerformanceUI>();
var tracker = gameObject.AddComponent<SystemResourceTracker>();
var logger = gameObject.AddComponent<PerformanceLogger>();
```

## File Structure

```
Assets/Scripts/Performance/
├── PerformanceData.cs          # Core data structures
├── PerformanceMonitor.cs       # Main monitoring controller  
├── PerformanceUI.cs           # Real-time UI overlay
├── SystemResourceTracker.cs   # CPU/Memory tracking
├── PerformanceLogger.cs       # File logging system
└── PerformanceSetup.cs        # Easy setup utility
```

## Usage

### Basic Monitoring
```csharp
// Get current performance data
var data = PerformanceMonitor.Instance.GetCurrentData();
Debug.Log($"FPS: {data.fps}, Memory: {data.usedMemory / 1024 / 1024} MB");

// Get performance statistics  
var stats = PerformanceMonitor.Instance.GetStats();
Debug.Log($"Average FPS: {stats.avgFps}");
```

### Custom Thresholds
```csharp
// Configure performance thresholds in inspector
[SerializeField] private float lowFpsThreshold = 30f;
[SerializeField] private float highMemoryThreshold = 512f; // MB
[SerializeField] private float highFrameTimeThreshold = 33.33f; // ms
```

### Event Subscription
```csharp
// Subscribe to performance events
PerformanceMonitor.OnPerformanceWarning += (message) => {
    Debug.LogWarning($"Performance Issue: {message}");
};

PerformanceMonitor.OnPerformanceUpdate += (data) => {
    // Handle real-time performance data
};
```

## Controls

- **F1**: Toggle performance UI visibility
- **Inspector Settings**: Configure all monitoring parameters
- **Runtime API**: Start/stop monitoring programmatically

## Log Files

Logs are saved to: `Application.persistentDataPath/PerformanceLogs/`

### Log Formats
- **CSV**: Easy Excel/analysis import
- **JSON**: Structured data for processing  
- **XML**: Hierarchical data format
- **Plain Text**: Human-readable format

### Sample CSV Output
```csv
Timestamp,FPS,FrameTime(ms),DeltaTime,TotalMemory,UsedMemory,GCMemory,CPUUsage,DrawCalls,Triangles,Vertices,ActiveDice,TotalGameObjects,CamX,CamY,CamZ
2024-08-28 13:52:18.123,60.1,16.65,0.0166,1048576,524288,262144,15.2,45,1200,800,6,234,0.00,5.00,-10.00
```

## Configuration Options

### PerformanceMonitor Settings
- **Update Interval**: Monitoring frequency (0.1s default)
- **Max Samples**: History buffer size (100 default)  
- **Enable Monitoring**: Master on/off switch
- **Show UI**: Display performance overlay
- **Log to File**: Enable file logging

### UI Settings  
- **Window Size**: Overlay dimensions (350x500 default)
- **Window Position**: Screen position (10,10 default)
- **Toggle Key**: Show/hide key (F1 default)
- **Colors**: Good/Warning/Critical color coding

### Logger Settings
- **Log Format**: CSV/JSON/XML/PlainText
- **Max Log Files**: Cleanup threshold (10 default)
- **Max File Size**: Rotation threshold (10MB default)
- **Separate Resource Log**: CPU/Memory in separate files

## Performance Impact

- **CPU Overhead**: ~0.1-0.5% typical
- **Memory Overhead**: ~2-5MB for buffers
- **File I/O**: Async writing, minimal frame impact
- **UI Overhead**: ~0.1ms render time when visible

## Platform Support

- ✅ **Windows**: Full CPU/Memory tracking
- ✅ **macOS**: Limited system memory detection  
- ✅ **Linux**: Limited system memory detection
- ✅ **Unity Editor**: Full functionality
- ✅ **Built Applications**: Full functionality

## Troubleshooting

### Common Issues
1. **No Performance Data**: Check if PerformanceMonitor.Instance exists
2. **UI Not Showing**: Press F1 or check "Show UI" setting
3. **Log Files Missing**: Verify write permissions to persistentDataPath
4. **High CPU Usage**: Increase update interval in settings

### Debug Commands
```csharp
// Check system status
Debug.Log($"Monitor Active: {PerformanceMonitor.Instance != null}");
Debug.Log($"Log Path: {Application.persistentDataPath}/PerformanceLogs/");

// Reset statistics
PerformanceMonitor.Instance.ResetStats();
```

## Integration Examples

### Dice Game Specific Monitoring
```csharp
public class DicePerformanceTracker : MonoBehaviour 
{
    private void Update() 
    {
        // Monitor dice-specific performance
        int activeDice = GameObject.FindGameObjectsWithTag("Dice").Length;
        if (activeDice > 10) 
        {
            Debug.LogWarning("High dice count may impact performance");
        }
    }
}
```

### Performance-based Quality Adjustment
```csharp
private void OnPerformanceWarning(string message)
{
    if (message.Contains("Low FPS"))
    {
        // Reduce visual quality
        QualitySettings.DecreaseLevel();
    }
}
```