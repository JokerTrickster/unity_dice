# MatchingSection Implementation

## Overview
The `MatchingSection` is a comprehensive controller class that manages the game matching system for the Unity dice game. It inherits from `SectionBase` and integrates seamlessly with the existing MainPageManager architecture.

## Architecture

### Class Hierarchy
```
MonoBehaviour
└── SectionBase (Abstract)
    └── MatchingSection (Concrete Implementation)
```

### Key Components
- **MatchingSection.cs** - Main controller class
- **MatchingSectionUIEvents.cs** - UI event system and adapter
- **MatchingSectionTests.cs** - Comprehensive test suite

## Features

### Game Mode Support
- **Classic Mode** (1 energy, Level 1+): Standard 4-player dice game
- **Speed Mode** (2 energy, Level 5+): Fast-paced gameplay
- **Challenge Mode** (3 energy, Level 10+): High-difficulty matches
- **Ranked Mode** (2 energy, Level 15+): Competitive ranking matches

### Matching Types
1. **Quick Match**: Fast random matching
2. **Ranked Match**: Competitive matches with rating
3. **Room Creation**: Create custom rooms with 4-digit codes
4. **Room Joining**: Join existing rooms by code

### State Management
```
Idle → Searching → Found → Connecting → Ready
  ↑                 ↓
  ← ← ← ← Failed ← ← ←
```

## Integration Points

### Energy System Integration
```csharp
// Energy validation before starting match
if (!ValidateEnergyRequirement())
    return;

// Energy consumption
ConsumeEnergyForMatch();

// Energy refund on cancellation
RefundEnergyForCancelledMatch();
```

### MainPageManager Communication
```csharp
// Send energy requests
SendMessageToSection(MainPageSectionType.Energy, energyRequest);

// Broadcast match events
BroadcastToAllSections(matchReadyMessage);
```

### Network Integration
```csharp
// Uses NetworkManager for backend communication
_networkManager = NetworkManager.Instance;
_matchingManager = new MatchingManager(_matchingConfig, _networkManager);
```

## Usage

### Basic Setup
```csharp
// 1. Add MatchingSection component to GameObject
var matchingSection = gameObject.AddComponent<MatchingSection>();

// 2. Register with MainPageManager
MainPageManager.Instance.RegisterSection(MainPageSectionType.Matching, matchingSection);

// 3. Initialize
matchingSection.Initialize(MainPageManager.Instance);

// 4. Activate
matchingSection.Activate();
```

### Starting a Match
```csharp
// Quick match
matchingSection.StartQuickMatch(GameMode.Classic);

// Ranked match
matchingSection.StartRankedMatch(GameMode.Ranked);

// Create room
matchingSection.CreateRoom(GameMode.Speed, 4, false);

// Join room
matchingSection.JoinRoom("ABCD");
```

### Event Handling
```csharp
// Subscribe to matching events
MatchingSection.OnMatchingStateChanged += OnStateChanged;
MatchingSection.OnMatchFound += OnMatchFound;
MatchingSection.OnGameModeChanged += OnGameModeChanged;
MatchingSection.OnRoomCodeGenerated += OnRoomCreated;

private void OnStateChanged(MatchingState state)
{
    Debug.Log($"Matching state: {state}");
}

private void OnMatchFound(MatchFoundData matchData)
{
    Debug.Log($"Match found! Room: {matchData.RoomId}");
}
```

## Validation System

### Matching Conditions
- User must be online (not in offline mode)
- User must have sufficient level for game mode
- User must have sufficient energy
- No active matching session
- Valid user data available

### Energy Requirements
- Classic: 1 energy
- Speed: 2 energy  
- Challenge: 3 energy
- Ranked: 2 energy

### Level Requirements
- Classic: Level 1+
- Speed: Level 5+
- Challenge: Level 10+
- Ranked: Level 15+

## Error Handling

### Common Error Scenarios
1. **Insufficient Energy**: Shows error message, highlights energy section
2. **Offline Mode**: Disables matching, shows offline notification
3. **Low Level**: Prevents access to high-level modes
4. **Invalid Room Code**: Validates 4-digit codes
5. **Network Errors**: Handles connection failures gracefully

### Error Recovery
- Auto-retry for network failures
- State rollback on critical errors
- Energy refund on failed matches
- Graceful degradation in offline mode

## Testing

### Test Coverage
- **Unit Tests**: Individual method testing
- **Integration Tests**: Multi-component interaction testing
- **State Tests**: State transition validation
- **Validation Tests**: Input validation and error handling
- **UI Tests**: Event handling and UI integration
- **Energy Tests**: Energy system integration
- **Network Tests**: Network communication mocking

### Running Tests
```csharp
// In Unity Test Runner
// 1. Open Window > General > Test Runner
// 2. Select PlayMode or EditMode
// 3. Run MatchingSectionTests
```

## Configuration

### Settings
```csharp
// Matching settings (via SettingsManager)
Matching.MaxWaitTimeMinutes: 5.0
Matching.PlayerCountUpdateInterval: 30.0
Matching.EnableRoomCreation: true
Matching.EnableRandomMatching: true
Matching.MaxPlayersPerRoom: 4
Matching.MinPlayersPerRoom: 2
```

### Game Mode Configuration
Each game mode has configurable:
- Display name
- Energy cost
- Estimated wait time
- Minimum player level
- Player count limits
- Description

## Performance Considerations

### Memory Management
- Proper cleanup in OnCleanup()
- Coroutine lifecycle management
- Event subscription cleanup
- Manager instance disposal

### Network Efficiency
- Request batching where possible
- Connection reuse through NetworkManager
- Timeout handling
- Retry logic with exponential backoff

### UI Performance
- UI update throttling
- Safe UI update methods
- Background processing for heavy operations
- Efficient state change handling

## Future Enhancements

### Planned Features
1. **WebSocket Integration**: Real-time matching updates
2. **Friend System**: Friend invites and private matches
3. **Tournament Mode**: Bracket-style competitions
4. **Spectator Mode**: Watch ongoing matches
5. **Match History**: Previous match tracking
6. **Statistics**: Win/loss records and performance metrics

### Extension Points
- Custom game modes through configuration
- Plugin system for match types
- AI opponent integration
- Cross-platform matching
- Regional matching preferences

## Troubleshooting

### Common Issues

**Issue**: Matching doesn't start
- Check offline mode status
- Verify energy and level requirements
- Ensure NetworkManager is initialized

**Issue**: UI events not working
- Verify MatchingSectionUIAdapter is present
- Check event subscriptions in UI code
- Ensure proper component references

**Issue**: Energy not consumed
- Check EnergySection integration
- Verify message sending to energy section
- Review energy request/response flow

**Issue**: Room codes not working
- Validate 4-digit code format
- Check network connectivity
- Verify room manager integration

### Debug Commands
```csharp
// Get current matching status
var status = matchingSection.GetMatchingStatus();
Debug.Log($"State: {status.CurrentState}, Mode: {status.SelectedGameMode}");

// Check energy validation
bool canMatch = matchingSection.CanStartMatching(GameMode.Classic);
Debug.Log($"Can start Classic match: {canMatch}");

// Force refresh
matchingSection.ForceRefresh();
```

## API Reference

### Public Methods
```csharp
// Core Operations
void StartQuickMatch(GameMode gameMode);
void StartRankedMatch(GameMode gameMode);
void CreateRoom(GameMode gameMode, int maxPlayers, bool isPrivate);
void JoinRoom(string roomCode);
void CancelMatching();

// Status Queries
MatchingState GetCurrentState();
GameMode GetSelectedGameMode();
string GetCurrentRoomCode();
MatchConfig GetGameModeConfig(GameMode gameMode);
bool CanStartMatching(GameMode gameMode);
bool CanCreateRoom();
MatchingStatusInfo GetMatchingStatus();

// Event Handlers (Public for UI integration)
void HandleMatchingRequest(MatchingRequest request);
void HandleRoomCreationRequest(RoomCreationRequest request);
void HandleRoomJoinRequest(RoomJoinRequest request);
void HandleMatchCancelRequest();
void HandleGameModeSelection(GameMode gameMode);
```

### Events
```csharp
static event Action<MatchingState> OnMatchingStateChanged;
static event Action<GameMode> OnGameModeChanged;
static event Action<string> OnRoomCodeGenerated;
static event Action<MatchFoundData> OnMatchFound;
static event Action<string> OnMatchingError;
static event Action<int> OnPlayerCountUpdated;
```

### Data Structures
```csharp
public enum MatchingState { Idle, Searching, Found, Connecting, Ready, Failed }
public enum GameMode { Classic, Speed, Challenge, Ranked }
public enum MatchType { Quick, Ranked, Custom, Friend }
public class MatchConfig { /* Configuration data */ }
public class MatchingStatusInfo { /* Status information */ }
```

---

*This implementation provides a robust, scalable, and well-tested foundation for the game's matching system while maintaining clean architecture and following Unity best practices.*