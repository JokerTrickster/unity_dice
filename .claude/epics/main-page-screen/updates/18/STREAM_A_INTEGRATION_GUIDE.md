# Stream A Integration Guide: Core Matching System

## Overview
Stream A has completed the core matching system implementation. This guide provides integration points for Stream B (UI) and Stream C (WebSocket).

## Components Delivered

### 1. MatchingManager (Singleton)
- **File**: `Assets/Scripts/Managers/MatchingManager.cs`
- **Purpose**: Main entry point for all matching operations
- **Dependencies**: EnergyManager, UserDataManager, NetworkManager

### 2. MatchingStateManager
- **File**: `Assets/Scripts/Systems/MatchingStateManager.cs`
- **Purpose**: Handles state transitions, validation, and persistence
- **Key Features**: Timeout management, error recovery, app lifecycle handling

### 3. MatchingConfig (ScriptableObject)
- **File**: `Assets/Scripts/Config/MatchingConfig.cs`
- **Purpose**: Centralized configuration for all matching settings
- **Creation**: Right-click → Create → Unity Dice → Matching Config

### 4. Data Structures
- **File**: `Assets/Scripts/Data/MatchingState.cs`
- **Contains**: Enums, request/response classes, state info classes

## Public API for Stream B (UI Integration)

### MatchingManager Events
```csharp
// Subscribe to these events for UI updates
MatchingManager.OnStateChanged += (state) => { /* Update UI based on state */ };
MatchingManager.OnMatchFound += (players) => { /* Show matched players */ };
MatchingManager.OnGameStarting += (gameData) => { /* Show game start countdown */ };
MatchingManager.OnMatchingCancelled += (reason) => { /* Show cancellation message */ };
MatchingManager.OnMatchingFailed += (error) => { /* Show error dialog */ };
MatchingManager.OnInsufficientEnergy += (required, current) => { /* Show energy purchase UI */ };
MatchingManager.OnMatchingProgressUpdate += (elapsedSeconds) => { /* Update progress bar */ };
```

### Key Methods for UI
```csharp
// Check if matching can start
bool canMatch = MatchingManager.Instance.CanStartMatching;
bool canMatchMode = MatchingManager.Instance.CanMatchWithGameMode(GameMode.Classic);

// Start matching
await MatchingManager.Instance.StartRandomMatchingAsync(GameMode.Classic, 2);

// Cancel matching
MatchingManager.Instance.CancelMatching("User cancelled");

// Get current state info
var info = MatchingManager.Instance.GetCurrentMatchingInfo();
MatchingState currentState = MatchingManager.Instance.CurrentState;
bool isMatching = MatchingManager.Instance.IsMatching;
```

### State Management for UI
```csharp
public enum MatchingState
{
    Idle,           // Show matching options
    Searching,      // Show searching animation + cancel button
    Found,          // Show matched players list
    Starting,       // Show countdown timer
    Cancelled,      // Show cancellation message → back to Idle
    Failed          // Show error message → back to Idle with retry option
}
```

## Integration Points for Stream C (WebSocket)

### IWebSocketClient Interface
Stream A defines the WebSocket interface that Stream C should implement:

```csharp
public interface IWebSocketClient
{
    Task<bool> SendAsync(string message);
    bool IsConnected { get; }
    event Action<string> OnMessageReceived;
    event Action<bool> OnConnectionStatusChanged;
}
```

### Message Handling Integration
Stream C should call this method when WebSocket messages are received:

```csharp
// In your WebSocket message handler:
void OnWebSocketMessage(string message)
{
    var response = JsonUtility.FromJson<MatchingResponse>(message);
    MatchingManager.Instance.HandleMatchingResponse(response);
}
```

### Message Types Stream C Should Handle
```csharp
// Outgoing to server (Stream C sends these)
MatchingRequest request = new MatchingRequest
{
    playerId = "user123",
    playerCount = 2,
    gameMode = GameMode.Classic,
    matchType = MatchType.Random
};

// Incoming from server (Stream C receives and forwards to MatchingManager)
MatchingResponse response = new MatchingResponse
{
    type = "matching_found", // or "matching_cancelled", "matching_failed", "game_starting"
    success = true,
    roomId = "room123",
    players = [...],
    gameMode = GameMode.Classic
};
```

## Configuration and Setup

### 1. Create MatchingConfig Asset
1. Right-click in Project → Create → Unity Dice → Matching Config
2. Name it "DefaultMatchingConfig"
3. Configure timeouts, player counts, game modes as needed
4. Assign to MatchingManager in Inspector or let it use defaults

### 2. Initialize Dependencies
Make sure these exist before using MatchingManager:
- EnergyManager.Instance
- UserDataManager.Instance  
- NetworkManager.Instance

### 3. Error Handling
```csharp
try {
    MatchingManager.Instance.Initialize();
} catch (System.Exception e) {
    Debug.LogError($"MatchingManager initialization failed: {e.Message}");
}
```

## Testing and Validation

### Unit Tests Available
- `MatchingManagerTests.cs` - 16 test methods
- `MatchingStateManagerTests.cs` - 20 test methods  
- `MatchingConfigTests.cs` - 12 test methods

### Manual Testing Scenarios
1. **Basic Matching Flow**
   - Start matching → State = Searching
   - Simulate match found → State = Found
   - Simulate game start → State = Starting
   
2. **Cancellation Flow**
   - Start matching → Cancel → State = Cancelled → Auto return to Idle

3. **Error Handling**
   - Start matching without energy → InsufficientEnergy event
   - Network timeout → State = Failed with retry option

4. **State Persistence**
   - Start matching → Close app → Reopen → State should reset to Idle

## Integration Checklist for Other Streams

### Stream B (UI) TODO:
- [ ] Subscribe to MatchingManager events
- [ ] Create UI states for each MatchingState
- [ ] Implement player count selection (2, 3, 4)
- [ ] Implement game mode selection (Classic, Speed, Challenge, Ranked)
- [ ] Add matching progress animation
- [ ] Add cancel button during searching
- [ ] Show matched players list when found
- [ ] Implement game start countdown
- [ ] Handle insufficient energy UI flow
- [ ] Test UI with all state transitions

### Stream C (WebSocket) TODO:
- [ ] Implement IWebSocketClient interface
- [ ] Set up WebSocket connection management
- [ ] Implement message sending for MatchingRequest
- [ ] Parse incoming MatchingResponse messages
- [ ] Forward parsed responses to MatchingManager.HandleMatchingResponse()
- [ ] Handle connection status events
- [ ] Implement reconnection logic
- [ ] Test message flow with MatchingManager

## Contact and Coordination

Stream A implementation is complete. For integration questions:
1. Check existing test files for usage examples
2. Review MatchingManager public API documentation
3. Use the event system for loose coupling between streams
4. Maintain single responsibility: Stream A = Core Logic, Stream B = UI, Stream C = Network

The core matching system is fully functional and ready for UI and WebSocket integration.