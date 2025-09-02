# Stream B Integration Guide: Settings UI

## Overview
Stream A has completed all backend logic and integration layers. Stream B needs to implement the UI components and connect them to the provided integration points.

## Core Integration Point: SettingsIntegration

### Single Entry Point
```csharp
// Get the integration instance
var settingsIntegration = SettingsIntegration.Instance;

// Check if system is ready
if (settingsIntegration.IsInitialized)
{
    // Safe to proceed with UI integration
}
```

## Required UI Components

### 1. Audio Toggle Buttons
```csharp
[Header("Audio Settings")]
public Toggle musicToggle;
public Toggle soundToggle;

private void Start()
{
    // Subscribe to backend events
    SettingsIntegration.OnSettingChanged += OnSettingChanged;
    
    // Initialize UI state
    var currentSettings = SettingsIntegration.Instance.GetCurrentSettings();
    musicToggle.isOn = currentSettings.MusicEnabled;
    soundToggle.isOn = currentSettings.SoundEnabled;
    
    // Connect UI events
    musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
    soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
}

private void OnMusicToggleChanged(bool isEnabled)
{
    SettingsIntegration.Instance.ToggleMusic(isEnabled);
}

private void OnSoundToggleChanged(bool isEnabled)
{
    SettingsIntegration.Instance.ToggleSound(isEnabled);
}

private void OnSettingChanged(string key, object value)
{
    switch (key)
    {
        case "MusicEnabled":
            musicToggle.isOn = (bool)value;
            break;
        case "SoundEnabled":
            soundToggle.isOn = (bool)value;
            break;
    }
}
```

### 2. Logout Button
```csharp
[Header("Actions")]
public Button logoutButton;

private void Start()
{
    logoutButton.onClick.AddListener(OnLogoutClicked);
    
    // Monitor logout progress
    SettingsIntegration.OnSettingChanged += OnLogoutProgress;
}

private void OnLogoutClicked()
{
    SettingsIntegration.Instance.InitiateLogout();
}

private void OnLogoutProgress(string key, object value)
{
    if (key == "LogoutProgress")
    {
        var progress = value as dynamic;
        // Update logout progress UI
        // ShowLogoutProgress(progress.Message, progress.Progress);
    }
    else if (key == "LogoutStatus")
    {
        var status = value as dynamic;
        if (status.Success)
        {
            // Logout completed successfully
        }
        else
        {
            // Show error message: status.Message
        }
    }
}
```

### 3. Terms & Conditions Button
```csharp
public Button termsButton;

private void Start()
{
    termsButton.onClick.AddListener(OnTermsClicked);
}

private void OnTermsClicked()
{
    SettingsIntegration.Instance.ShowTermsAndConditions();
    // UI will be handled automatically by TermsHandler based on platform
}
```

### 4. Mailbox Button
```csharp
public Button mailboxButton;

private void Start()
{
    mailboxButton.onClick.AddListener(OnMailboxClicked);
}

private void OnMailboxClicked()
{
    SettingsIntegration.Instance.OpenMailbox();
}
```

## UI Layout Recommendations

### Quick Settings Panel (Always Visible)
```
┌─────────────────────────────────────┐
│ 🎵 Music    [Toggle ON ]           │
│ 🔊 Sound    [Toggle ON ]           │
└─────────────────────────────────────┘
```

### Action Buttons Panel
```
┌─────────────────────────────────────┐
│ [📬 Mailbox] [📄 Terms] [🚪 Logout] │
└─────────────────────────────────────┘
```

## Event Handling Best Practices

### 1. Subscribe to All Relevant Events
```csharp
private void SubscribeToEvents()
{
    SettingsIntegration.OnIntegrationInitialized += OnIntegrationReady;
    SettingsIntegration.OnSettingChanged += OnSettingChanged;
    SettingsIntegration.OnSystemStatusChanged += OnSystemStatusChanged;
}

private void UnsubscribeFromEvents()
{
    SettingsIntegration.OnIntegrationInitialized -= OnIntegrationReady;
    SettingsIntegration.OnSettingChanged -= OnSettingChanged;
    SettingsIntegration.OnSystemStatusChanged -= OnSystemStatusChanged;
}
```

### 2. Handle System Status Changes
```csharp
private void OnSystemStatusChanged(SystemStatus status)
{
    // Update UI availability based on system status
    UpdateUIAvailability(status);
}

private void UpdateUIAvailability(SystemStatus status)
{
    logoutButton.interactable = status.HasAuthenticationManager;
    mailboxButton.interactable = status.HasMailboxManager;
    
    // Show status indicators if needed
    if (!status.IsSettingsReady)
    {
        ShowSettingsNotReady();
    }
}
```

## Error Handling

### 1. Graceful Degradation
```csharp
private void SafeExecuteAction(System.Action action, string actionName)
{
    try
    {
        if (SettingsIntegration.Instance.IsInitialized)
        {
            action();
        }
        else
        {
            ShowMessage($"{actionName} not available - system not ready");
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Failed to execute {actionName}: {ex.Message}");
        ShowErrorMessage($"{actionName} failed. Please try again.");
    }
}
```

### 2. UI Feedback
```csharp
private void ShowLogoutInProgress()
{
    logoutButton.interactable = false;
    logoutButton.GetComponentInChildren<Text>().text = "Logging out...";
}

private void ShowSettingsApplying()
{
    // Briefly disable toggles to show immediate response
    musicToggle.interactable = false;
    soundToggle.interactable = false;
    
    // Re-enable after short delay
    StartCoroutine(ReEnableToggles(0.1f));
}
```

## Performance Considerations

### 1. Immediate UI Response (0.1s Target)
- Toggles should respond immediately to user input
- Audio changes should be audible within 0.1 seconds
- Use the provided event system for instant feedback

### 2. Logout Animation (5s Target)
- Show progress bar for logout process
- Stream A guarantees completion within 5 seconds
- Provide clear feedback at each stage

## Testing Integration

### 1. Test All Events
```csharp
// Test event firing
SettingsIntegration.Instance.ToggleMusic(false);
// Verify UI updates immediately

// Test system status
var status = SettingsIntegration.Instance.GetCurrentSettings();
// Verify UI reflects current state
```

### 2. Test Error Scenarios
```csharp
// Test with components missing
// Test with network disconnected
// Test with invalid states
```

## Dependencies
Stream A provides all backend logic. Stream B only needs to:
1. Create UI components
2. Connect to SettingsIntegration events
3. Call SettingsIntegration methods
4. Handle UI state updates

## Files to Create in Stream B
- `SettingsSectionUI.cs` - Main UI controller
- `SettingsToggleComponent.cs` - Reusable toggle component
- `LogoutProgressDialog.cs` - Logout progress UI
- UI Prefabs and Scenes

## Success Criteria
- ✅ Music toggle responds within 0.1s
- ✅ Sound toggle responds within 0.1s  
- ✅ Logout completes within 5s with progress feedback
- ✅ Terms display works on all platforms
- ✅ Mailbox integration works seamlessly
- ✅ All error states handled gracefully
- ✅ UI reflects system status accurately