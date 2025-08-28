using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 반응형 레이아웃 관리자
/// 다양한 화면 크기와 해상도에 대응하는 UI 레이아웃을 제공합니다.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveLayout : MonoBehaviour
{
    [Header("Screen Size Breakpoints")]
    [SerializeField] private float mobileWidth = 768f;
    [SerializeField] private float tabletWidth = 1024f;
    [SerializeField] private float desktopWidth = 1920f;
    
    [Header("Layout Settings")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private float mobileMaxWidth = 400f;
    [SerializeField] private float tabletMaxWidth = 600f;
    [SerializeField] private float desktopMaxWidth = 800f;
    
    [Header("Font Scaling")]
    [SerializeField] private Text[] scalableTexts;
    [SerializeField] private float mobileFontScale = 0.8f;
    [SerializeField] private float tabletFontScale = 1.0f;
    [SerializeField] private float desktopFontScale = 1.2f;
    
    [Header("Button Scaling")]
    [SerializeField] private Button[] scalableButtons;
    [SerializeField] private Vector2 mobileButtonSize = new(300f, 60f);
    [SerializeField] private Vector2 tabletButtonSize = new(350f, 70f);
    [SerializeField] private Vector2 desktopButtonSize = new(400f, 80f);
    
    private CanvasScaler _canvasScaler;
    private DeviceType _currentDeviceType;
    private Vector2 _lastScreenSize;
    
    public enum DeviceType
    {
        Mobile,
        Tablet,
        Desktop
    }
    
    private void Awake()
    {
        _canvasScaler = GetComponent<CanvasScaler>();
        _lastScreenSize = new Vector2(Screen.width, Screen.height);
    }
    
    private void Start()
    {
        ApplyResponsiveLayout();
    }
    
    private void Update()
    {
        // 화면 크기 변경 감지
        Vector2 currentScreenSize = new(Screen.width, Screen.height);
        if (_lastScreenSize != currentScreenSize)
        {
            _lastScreenSize = currentScreenSize;
            ApplyResponsiveLayout();
        }
    }
    
    /// <summary>
    /// 반응형 레이아웃 적용
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        DeviceType deviceType = GetDeviceType();
        
        if (_currentDeviceType != deviceType)
        {
            _currentDeviceType = deviceType;
            
            SetupCanvasScaler(deviceType);
            SetupContentLayout(deviceType);
            SetupFontScaling(deviceType);
            SetupButtonScaling(deviceType);
            
            Debug.Log($"[ResponsiveLayout] Applied layout for {deviceType} device (Screen: {Screen.width}x{Screen.height})");
        }
    }
    
    /// <summary>
    /// 현재 화면 크기에 따른 디바이스 타입 결정
    /// </summary>
    private DeviceType GetDeviceType()
    {
        float screenWidth = Screen.width;
        
        if (screenWidth <= mobileWidth)
            return DeviceType.Mobile;
        else if (screenWidth <= tabletWidth)
            return DeviceType.Tablet;
        else
            return DeviceType.Desktop;
    }
    
    /// <summary>
    /// Canvas Scaler 설정
    /// </summary>
    private void SetupCanvasScaler(DeviceType deviceType)
    {
        if (_canvasScaler == null) return;
        
        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        
        switch (deviceType)
        {
            case DeviceType.Mobile:
                _canvasScaler.referenceResolution = new Vector2(720f, 1280f);
                _canvasScaler.matchWidthOrHeight = 0.5f;
                break;
                
            case DeviceType.Tablet:
                _canvasScaler.referenceResolution = new Vector2(1024f, 768f);
                _canvasScaler.matchWidthOrHeight = 0.5f;
                break;
                
            case DeviceType.Desktop:
                _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
                _canvasScaler.matchWidthOrHeight = 0.5f;
                break;
        }
    }
    
    /// <summary>
    /// 컨텐츠 레이아웃 설정
    /// </summary>
    private void SetupContentLayout(DeviceType deviceType)
    {
        if (contentContainer == null) return;
        
        float maxWidth = deviceType switch
        {
            DeviceType.Mobile => mobileMaxWidth,
            DeviceType.Tablet => tabletMaxWidth,
            DeviceType.Desktop => desktopMaxWidth,
            _ => tabletMaxWidth
        };
        
        // 최대 너비 제한
        var layoutElement = contentContainer.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = contentContainer.gameObject.AddComponent<LayoutElement>();
        
        layoutElement.preferredWidth = maxWidth;
        
        // 중앙 정렬을 위한 설정
        contentContainer.anchorMin = new Vector2(0.5f, 0.5f);
        contentContainer.anchorMax = new Vector2(0.5f, 0.5f);
        contentContainer.pivot = new Vector2(0.5f, 0.5f);
    }
    
    /// <summary>
    /// 폰트 크기 조정
    /// </summary>
    private void SetupFontScaling(DeviceType deviceType)
    {
        if (scalableTexts == null) return;
        
        float fontScale = deviceType switch
        {
            DeviceType.Mobile => mobileFontScale,
            DeviceType.Tablet => tabletFontScale,
            DeviceType.Desktop => desktopFontScale,
            _ => 1.0f
        };
        
        foreach (var text in scalableTexts)
        {
            if (text != null)
            {
                // 원본 폰트 크기를 저장해두고 스케일 적용
                var originalFontSize = text.GetComponent<TextOriginalSize>();
                if (originalFontSize == null)
                {
                    originalFontSize = text.gameObject.AddComponent<TextOriginalSize>();
                    originalFontSize.originalSize = text.fontSize;
                }
                
                text.fontSize = Mathf.RoundToInt(originalFontSize.originalSize * fontScale);
            }
        }
    }
    
    /// <summary>
    /// 버튼 크기 조정
    /// </summary>
    private void SetupButtonScaling(DeviceType deviceType)
    {
        if (scalableButtons == null) return;
        
        Vector2 buttonSize = deviceType switch
        {
            DeviceType.Mobile => mobileButtonSize,
            DeviceType.Tablet => tabletButtonSize,
            DeviceType.Desktop => desktopButtonSize,
            _ => tabletButtonSize
        };
        
        foreach (var button in scalableButtons)
        {
            if (button != null)
            {
                var rectTransform = button.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = buttonSize;
                }
            }
        }
    }
    
    /// <summary>
    /// 수동으로 레이아웃 새로고침
    /// </summary>
    public void RefreshLayout()
    {
        _currentDeviceType = (DeviceType)(-1); // 강제로 다시 적용하기 위해
        ApplyResponsiveLayout();
    }
    
    /// <summary>
    /// 현재 디바이스 타입 반환
    /// </summary>
    public DeviceType GetCurrentDeviceType()
    {
        return _currentDeviceType;
    }
}

/// <summary>
/// 텍스트의 원본 폰트 크기를 기억하는 컴포넌트
/// </summary>
public class TextOriginalSize : MonoBehaviour
{
    public int originalSize;
}