using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 반응형 레이아웃 헬퍼 클래스
/// MainPageScreen과 연동하여 다양한 화면 크기에 대응하는 레이아웃 기능을 제공합니다.
/// </summary>
public static class ResponsiveLayoutHelper
{
    #region Constants
    public const float MIN_TOUCH_SIZE = 44f;
    public const float MOBILE_BREAKPOINT = 768f;
    public const float TABLET_BREAKPOINT = 1024f;
    public const float DESKTOP_BREAKPOINT = 1920f;
    
    // Layout proportions for landscape mode (as specified in task requirements)
    public const float PROFILE_SECTION_WIDTH = 0.25f;  // 25%
    public const float ENERGY_SECTION_WIDTH = 0.25f;   // 25%
    public const float MATCHING_SECTION_WIDTH = 0.5f;  // 50%
    public const float SETTINGS_SECTION_HEIGHT = 0.2f; // Footer 20%
    
    // Reference resolutions for different device types
    public static readonly Vector2 MOBILE_RESOLUTION = new Vector2(720f, 1280f);
    public static readonly Vector2 TABLET_RESOLUTION = new Vector2(1024f, 768f);
    public static readonly Vector2 DESKTOP_RESOLUTION = new Vector2(1920f, 1080f);
    #endregion

    #region Device Type Detection
    public enum DeviceCategory
    {
        Mobile,
        Tablet,
        Desktop
    }

    public static DeviceCategory GetCurrentDeviceCategory()
    {
        float screenWidth = Screen.width;
        
        if (screenWidth <= MOBILE_BREAKPOINT)
            return DeviceCategory.Mobile;
        else if (screenWidth <= TABLET_BREAKPOINT)
            return DeviceCategory.Tablet;
        else
            return DeviceCategory.Desktop;
    }

    public static bool IsPortraitMode()
    {
        return Screen.height > Screen.width;
    }

    public static bool IsLandscapeMode()
    {
        return Screen.width > Screen.height;
    }
    #endregion

    #region Canvas Setup
    public static void SetupCanvasScaler(CanvasScaler scaler, DeviceCategory deviceCategory)
    {
        if (scaler == null) return;
        
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        
        switch (deviceCategory)
        {
            case DeviceCategory.Mobile:
                scaler.referenceResolution = MOBILE_RESOLUTION;
                scaler.matchWidthOrHeight = IsPortraitMode() ? 0.0f : 1.0f;
                break;
                
            case DeviceCategory.Tablet:
                scaler.referenceResolution = TABLET_RESOLUTION;
                scaler.matchWidthOrHeight = 0.5f;
                break;
                
            case DeviceCategory.Desktop:
                scaler.referenceResolution = DESKTOP_RESOLUTION;
                scaler.matchWidthOrHeight = 0.5f;
                break;
        }
    }

    public static Vector2 GetReferenceResolution(DeviceCategory deviceCategory)
    {
        return deviceCategory switch
        {
            DeviceCategory.Mobile => MOBILE_RESOLUTION,
            DeviceCategory.Tablet => TABLET_RESOLUTION,
            DeviceCategory.Desktop => DESKTOP_RESOLUTION,
            _ => DESKTOP_RESOLUTION
        };
    }
    #endregion

    #region Layout Application
    public static void ApplyMainPageLayout(
        RectTransform profileContainer,
        RectTransform energyContainer,
        RectTransform matchingContainer,
        RectTransform settingsContainer,
        bool isPortraitMode = false)
    {
        if (isPortraitMode)
        {
            ApplyPortraitLayout(profileContainer, energyContainer, matchingContainer, settingsContainer);
        }
        else
        {
            ApplyLandscapeLayout(profileContainer, energyContainer, matchingContainer, settingsContainer);
        }
    }

    public static void ApplyLandscapeLayout(
        RectTransform profileContainer,
        RectTransform energyContainer,
        RectTransform matchingContainer,
        RectTransform settingsContainer)
    {
        // Profile Section (25% width, top area)
        if (profileContainer != null)
        {
            profileContainer.anchorMin = new Vector2(0, SETTINGS_SECTION_HEIGHT);
            profileContainer.anchorMax = new Vector2(PROFILE_SECTION_WIDTH, 1);
            profileContainer.offsetMin = Vector2.zero;
            profileContainer.offsetMax = Vector2.zero;
        }

        // Energy Section (25% width, top area)
        if (energyContainer != null)
        {
            float leftEdge = PROFILE_SECTION_WIDTH;
            float rightEdge = PROFILE_SECTION_WIDTH + ENERGY_SECTION_WIDTH;
            energyContainer.anchorMin = new Vector2(leftEdge, SETTINGS_SECTION_HEIGHT);
            energyContainer.anchorMax = new Vector2(rightEdge, 1);
            energyContainer.offsetMin = Vector2.zero;
            energyContainer.offsetMax = Vector2.zero;
        }

        // Matching Section (50% width, top area)
        if (matchingContainer != null)
        {
            float leftEdge = PROFILE_SECTION_WIDTH + ENERGY_SECTION_WIDTH;
            matchingContainer.anchorMin = new Vector2(leftEdge, SETTINGS_SECTION_HEIGHT);
            matchingContainer.anchorMax = new Vector2(1, 1);
            matchingContainer.offsetMin = Vector2.zero;
            matchingContainer.offsetMax = Vector2.zero;
        }

        // Settings Section (full width, footer)
        if (settingsContainer != null)
        {
            settingsContainer.anchorMin = new Vector2(0, 0);
            settingsContainer.anchorMax = new Vector2(1, SETTINGS_SECTION_HEIGHT);
            settingsContainer.offsetMin = Vector2.zero;
            settingsContainer.offsetMax = Vector2.zero;
        }
    }

    public static void ApplyPortraitLayout(
        RectTransform profileContainer,
        RectTransform energyContainer,
        RectTransform matchingContainer,
        RectTransform settingsContainer)
    {
        // Portrait mode: Stack sections vertically
        float sectionHeight = (1f - SETTINGS_SECTION_HEIGHT) / 3f; // Divide remaining space equally

        // Profile Section (top)
        if (profileContainer != null)
        {
            float topEdge = 1f - sectionHeight;
            profileContainer.anchorMin = new Vector2(0, topEdge);
            profileContainer.anchorMax = new Vector2(1, 1);
            profileContainer.offsetMin = Vector2.zero;
            profileContainer.offsetMax = Vector2.zero;
        }

        // Energy Section (middle-top)
        if (energyContainer != null)
        {
            float topEdge = 1f - sectionHeight * 2f;
            float bottomEdge = 1f - sectionHeight;
            energyContainer.anchorMin = new Vector2(0, topEdge);
            energyContainer.anchorMax = new Vector2(1, bottomEdge);
            energyContainer.offsetMin = Vector2.zero;
            energyContainer.offsetMax = Vector2.zero;
        }

        // Matching Section (middle)
        if (matchingContainer != null)
        {
            float topEdge = SETTINGS_SECTION_HEIGHT + sectionHeight;
            float bottomEdge = 1f - sectionHeight * 2f;
            matchingContainer.anchorMin = new Vector2(0, bottomEdge);
            matchingContainer.anchorMax = new Vector2(1, topEdge);
            matchingContainer.offsetMin = Vector2.zero;
            matchingContainer.offsetMax = Vector2.zero;
        }

        // Settings Section (footer)
        if (settingsContainer != null)
        {
            settingsContainer.anchorMin = new Vector2(0, 0);
            settingsContainer.anchorMax = new Vector2(1, SETTINGS_SECTION_HEIGHT);
            settingsContainer.offsetMin = Vector2.zero;
            settingsContainer.offsetMax = Vector2.zero;
        }
    }
    #endregion

    #region Touch-Friendly Design
    public static void EnsureTouchFriendlySize(RectTransform rectTransform, float minSize = MIN_TOUCH_SIZE)
    {
        if (rectTransform == null) return;

        Vector2 sizeDelta = rectTransform.sizeDelta;
        sizeDelta.x = Mathf.Max(sizeDelta.x, minSize);
        sizeDelta.y = Mathf.Max(sizeDelta.y, minSize);
        rectTransform.sizeDelta = sizeDelta;
    }

    public static void EnsureTouchFriendlyButtons(IEnumerable<Button> buttons, float minSize = MIN_TOUCH_SIZE)
    {
        if (buttons == null) return;

        foreach (var button in buttons)
        {
            if (button != null)
            {
                EnsureTouchFriendlySize(button.GetComponent<RectTransform>(), minSize);
            }
        }
    }

    public static void AddTouchFeedback(Button button, float scaleOnPress = 0.95f)
    {
        if (button == null) return;

        var buttonTransform = button.transform;
        var originalScale = buttonTransform.localScale;

        // Add event listeners for touch feedback
        button.onClick.AddListener(() =>
        {
            // Simple immediate scale feedback (can be enhanced with animations)
            buttonTransform.localScale = originalScale * scaleOnPress;
            
            // Reset scale after a short delay
            button.StartCoroutine(ResetScaleCoroutine(buttonTransform, originalScale));
        });
    }

    private static System.Collections.IEnumerator ResetScaleCoroutine(Transform transform, Vector3 originalScale)
    {
        yield return new WaitForSeconds(0.1f);
        transform.localScale = originalScale;
    }
    #endregion

    #region Safe Area Support
    public static void ApplySafeArea(RectTransform safeAreaRect)
    {
        if (safeAreaRect == null) return;

        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;

        safeAreaRect.anchorMin = anchorMin;
        safeAreaRect.anchorMax = anchorMax;
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
    }

    public static bool IsSafeAreaSupported()
    {
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        
        return safeArea.x > 0 || safeArea.y > 0 || 
               safeArea.width < screenSize.x || safeArea.height < screenSize.y;
    }
    #endregion

    #region Layout Validation
    public static bool ValidateMainPageLayout(
        RectTransform profileContainer,
        RectTransform energyContainer,
        RectTransform matchingContainer,
        RectTransform settingsContainer)
    {
        var containers = new RectTransform[] 
        { 
            profileContainer, energyContainer, matchingContainer, settingsContainer 
        };

        foreach (var container in containers)
        {
            if (container == null)
            {
                Debug.LogError("[ResponsiveLayoutHelper] Required container is null");
                return false;
            }

            if (container.anchorMin.x < 0 || container.anchorMin.y < 0 ||
                container.anchorMax.x > 1 || container.anchorMax.y > 1)
            {
                Debug.LogError($"[ResponsiveLayoutHelper] Invalid anchors for {container.name}");
                return false;
            }
        }

        return true;
    }

    public static LayoutValidationResult ValidateLayoutMetrics()
    {
        var result = new LayoutValidationResult
        {
            ScreenSize = new Vector2(Screen.width, Screen.height),
            DeviceCategory = GetCurrentDeviceCategory(),
            IsPortraitMode = IsPortraitMode(),
            SafeArea = Screen.safeArea,
            IsSafeAreaSupported = IsSafeAreaSupported()
        };

        // Add validation logic
        result.IsValid = result.ScreenSize.x > 0 && result.ScreenSize.y > 0;

        if (result.SafeArea.width <= 0 || result.SafeArea.height <= 0)
        {
            result.Issues.Add("Invalid safe area dimensions");
            result.IsValid = false;
        }

        return result;
    }
    #endregion

    #region Utility Methods
    public static float GetOptimalFontScale(DeviceCategory deviceCategory)
    {
        return deviceCategory switch
        {
            DeviceCategory.Mobile => 0.8f,
            DeviceCategory.Tablet => 1.0f,
            DeviceCategory.Desktop => 1.2f,
            _ => 1.0f
        };
    }

    public static Vector2 GetOptimalButtonSize(DeviceCategory deviceCategory)
    {
        return deviceCategory switch
        {
            DeviceCategory.Mobile => new Vector2(280f, 50f),
            DeviceCategory.Tablet => new Vector2(320f, 60f),
            DeviceCategory.Desktop => new Vector2(360f, 70f),
            _ => new Vector2(320f, 60f)
        };
    }

    public static float GetOptimalSpacing(DeviceCategory deviceCategory)
    {
        return deviceCategory switch
        {
            DeviceCategory.Mobile => 8f,
            DeviceCategory.Tablet => 12f,
            DeviceCategory.Desktop => 16f,
            _ => 12f
        };
    }

    public static Color GetDeviceOptimalBackgroundColor(DeviceCategory deviceCategory)
    {
        return deviceCategory switch
        {
            DeviceCategory.Mobile => new Color(0.1f, 0.1f, 0.1f, 0.9f),
            DeviceCategory.Tablet => new Color(0.05f, 0.05f, 0.05f, 0.95f),
            DeviceCategory.Desktop => new Color(0.0f, 0.0f, 0.0f, 1.0f),
            _ => new Color(0.05f, 0.05f, 0.05f, 0.95f)
        };
    }

    public static void LogLayoutInfo()
    {
        var validation = ValidateLayoutMetrics();
        Debug.Log($"[ResponsiveLayoutHelper] Layout Info: " +
                  $"Screen={validation.ScreenSize}, " +
                  $"Device={validation.DeviceCategory}, " +
                  $"Portrait={validation.IsPortraitMode}, " +
                  $"SafeArea={validation.SafeArea}");
    }
    #endregion
}

#region Data Classes
[System.Serializable]
public class LayoutValidationResult
{
    public bool IsValid = true;
    public Vector2 ScreenSize;
    public ResponsiveLayoutHelper.DeviceCategory DeviceCategory;
    public bool IsPortraitMode;
    public Rect SafeArea;
    public bool IsSafeAreaSupported;
    public List<string> Issues = new List<string>();
}
#endregion