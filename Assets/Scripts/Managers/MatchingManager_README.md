# MatchingManager Integration Guide

## Stream A (Core Matching System) - COMPLETED ✅

The core matching system implementation is complete and provides the following components:

### Components Delivered
1. **MatchingManager.cs** - Main singleton for matching operations
2. **MatchingStateManager.cs** - State transition and persistence management  
3. **MatchingConfig.cs** - ScriptableObject-based configuration system
4. **MatchingState.cs** - Data structures and enums
5. **Comprehensive unit tests** - 48 test methods across all components

### Public API for UI Integration (Stream B)

#### Key Events
```csharp
MatchingManager.OnStateChanged += (state) => { /* Update UI */ };
MatchingManager.OnMatchFound += (players) => { /* Show players */ };
MatchingManager.OnGameStarting += (data) => { /* Show countdown */ };
MatchingManager.OnMatchingFailed += (error) => { /* Show error */ };
MatchingManager.OnInsufficientEnergy += (req, curr) => { /* Energy UI */ };
```

#### Key Methods
```csharp
// Check state
bool canStart = MatchingManager.Instance.CanStartMatching;
MatchingState state = MatchingManager.Instance.CurrentState;

// Start/Cancel matching  
await MatchingManager.Instance.StartRandomMatchingAsync(GameMode.Classic, 2);
MatchingManager.Instance.CancelMatching("User cancelled");
```

### WebSocket Integration Points (Stream C)

#### Interface to Implement
```csharp
public interface IWebSocketClient
{
    Task<bool> SendAsync(string message);
    bool IsConnected { get; }
    event Action<string> OnMessageReceived;
    event Action<bool> OnConnectionStatusChanged;
}
```

#### Message Handling
```csharp
// Forward WebSocket messages to MatchingManager
void OnWebSocketMessage(string message) {
    var response = JsonUtility.FromJson<MatchingResponse>(message);
    MatchingManager.Instance.HandleMatchingResponse(response);
}
```

### Dependencies
- ✅ EnergyManager integration complete
- ✅ UserDataManager integration complete  
- ✅ NetworkManager integration complete
- 🔄 WebSocket client (Stream C dependency)

### State Flow
```
Idle → Searching → Found → Starting → Idle
  ↑      ↓         ↓        ↓
  ← Cancelled ←  Failed ← ←  ←
```

### Configuration
Create MatchingConfig asset via: Create → Unity Dice → Matching Config

### Testing
All components have comprehensive unit test coverage:
- State transitions and validation
- Energy integration scenarios  
- Timeout and error handling
- Configuration validation
- Persistence and app lifecycle

**Status**: Ready for Stream B/C integration with well-defined interfaces and comprehensive documentation.