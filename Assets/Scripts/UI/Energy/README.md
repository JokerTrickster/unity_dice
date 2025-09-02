# Energy UI Components

## Overview
This package contains the UI components for the energy system, implementing real-time energy display, visual feedback, and purchase functionality as specified in Issue #16.

## Components

### EnergyDisplayUI.cs
The main coordinator for all energy UI elements.

**Features:**
- Real-time updates every 1 second as specified
- Integration with EnergyManager.Instance from Stream A
- Visual feedback system (low energy warnings, full energy effects)
- Recovery timer display with proper time formatting
- Event-driven updates for responsive UI

**Required UI Elements:**
- `energyBar`: EnergyBar component for visual progress
- `currentEnergyText`: Text showing current energy value
- `maxEnergyText`: Text showing maximum energy value
- `energyRatioText`: Text showing "current/max" format
- `recoveryTimerText`: Text showing time until next recovery
- `purchaseButton`: Button to open purchase modal
- `lowEnergyWarning`: GameObject for low energy warning
- `fullEnergyEffect`: GameObject for full energy visual effect

### EnergyBar.cs
Visual progress bar with smooth animations and color transitions.

**Features:**
- Smooth animations with configurable animation curves
- Color gradients based on energy level (red → yellow → green)
- Visual effects: glow, pulse, particles for different states
- Depletion and full energy effect triggers
- Shake effects for dramatic moments

**Required UI Elements:**
- `progressSlider`: Unity Slider component (configured as display-only)
- `fillImage`: Image component for the slider fill
- `glowEffect`: Image component for glow effects
- `fullEnergyParticles`: Particle system for full energy celebration
- `pulseEffect`: GameObject for pulse animation when energy is low

### EnergyPurchaseUI.cs
Purchase modal system with confirmation dialogs.

**Features:**
- Modal with configurable purchase options
- Confirmation dialogs with detailed purchase information
- Currency integration points for future expansion
- Loading states and visual feedback
- Success/failure indicators with status messages

**Required UI Elements:**
- `modalContainer`: Main modal GameObject
- `purchaseOptionsContainer`: Transform to hold purchase option buttons
- `purchaseOptionPrefab`: Prefab template for purchase options
- `confirmationDialog`: GameObject for purchase confirmation
- `loadingOverlay`: GameObject shown during purchase processing

## Integration Points

### Stream A Dependencies
These components require the following from Stream A (Core Energy Management):
- `EnergyManager.Instance`: Singleton for energy state management
- `EnergyRecoverySystem.Instance`: For recovery timing information
- `EnergyEconomySystem.Instance`: For purchase processing

### Event System
The components use Unity's event system for loose coupling:
- `EnergyDisplayUI.OnEnergyDisplayUpdated`
- `EnergyBar.OnEnergyBarUpdated`
- `EnergyPurchaseUI.OnPurchaseRequested`

## Setup Instructions

### 1. EnergySection Prefab Setup
1. Create a GameObject with EnergyDisplayUI component
2. Add child objects for:
   - Current Energy Text (with Text component)
   - Max Energy Text (with Text component)
   - Energy Ratio Text (with Text component)
   - Recovery Timer Text (with Text component)
   - Energy Bar (with EnergyBar component and Slider)
   - Purchase Button (with Button component)
   - Low Energy Warning (GameObject with visual indicators)
   - Full Energy Effect (GameObject with particle systems)

### 2. EnergyBar Setup
1. Add a Slider component configured as:
   - Min Value: 0
   - Max Value: 1
   - Interactable: false (display only)
2. Configure the Fill Rect with an Image component
3. Add optional visual effects:
   - Glow Image for full energy effect
   - Particle System for celebration effects
   - Pulse GameObject for low energy warning

### 3. EnergyPurchaseModal Prefab Setup
1. Create a full-screen modal container with CanvasGroup
2. Add purchase options container (vertical layout recommended)
3. Create confirmation dialog as child object
4. Add status message area for feedback
5. Configure loading overlay for purchase processing states

## Usage Example

```csharp
// Get reference to energy display
var energyDisplay = FindObjectOfType<EnergyDisplayUI>();

// The component automatically connects to EnergyManager.Instance
// and starts real-time updates

// Manually trigger update if needed
energyDisplay.TriggerUpdate();

// Show purchase modal
var purchaseUI = FindObjectOfType<EnergyPurchaseUI>();
purchaseUI.ShowPurchaseModal();
```

## Configuration

### Real-time Update Interval
The update interval is set to 1 second as specified in the requirements:
```csharp
private const float UPDATE_INTERVAL = 1.0f; // 1-second interval
```

### Animation Settings
Energy bar animations can be configured:
```csharp
[SerializeField] private float animationDuration = 0.8f;
[SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
```

### Purchase Options
Purchase options are configurable through the inspector:
```csharp
[Serializable]
public class PurchaseOption
{
    public int energyAmount;
    public int cost;
    public string currencyType = "coins";
    public string displayName;
    public bool isRecommended;
    public int bonusAmount = 0;
}
```

## Testing

Each component includes editor context menu methods for testing:
- `[ContextMenu("Test Low Energy")]`
- `[ContextMenu("Test Full Energy")]`
- `[ContextMenu("Test Animation")]`
- `[ContextMenu("Test Show Modal")]`

## Performance Considerations

- Real-time updates use coroutines with WaitForSeconds to avoid constant polling
- Visual updates are throttled to prevent unnecessary redraws
- Animations use Unity's built-in tweening system for optimal performance
- UI elements are properly pooled and cleaned up on destroy

## Future Enhancements

- Integration with analytics system for purchase tracking
- Localization support for all text elements
- Accessibility features (screen reader support, high contrast mode)
- Advanced visual effects and animations
- Currency system integration beyond basic coin support