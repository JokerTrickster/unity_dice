using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class PerformanceUI : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private Vector2 windowSize = new Vector2(350, 500);
    [SerializeField] private Vector2 windowPosition = new Vector2(10, 10);
    
    [Header("UI Colors")]
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.8f);
    
    private Canvas performanceCanvas;
    private GameObject performancePanel;
    private Dictionary<string, TextMeshProUGUI> labels = new Dictionary<string, TextMeshProUGUI>();
    private bool isVisible = true;
    private PerformanceMonitor monitor;
    
    // Graph components
    private LineRenderer fpsGraph;
    private List<float> fpsHistory = new List<float>();
    private int maxGraphPoints = 50;
    
    private void Start()
    {
        monitor = PerformanceMonitor.Instance;
        if (monitor == null)
        {
            Debug.LogError("[PerformanceUI] PerformanceMonitor instance not found!");
            return;
        }
        
        CreatePerformanceUI();
        isVisible = showOnStart;
        performancePanel.SetActive(isVisible);
        
        // Subscribe to performance updates
        PerformanceMonitor.OnPerformanceUpdate += UpdateUI;
        PerformanceMonitor.OnPerformanceWarning += ShowWarning;
    }
    
    private void CreatePerformanceUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("PerformanceCanvas");
        performanceCanvas = canvasObj.AddComponent<Canvas>();
        performanceCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        performanceCanvas.sortingOrder = 1000;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create main panel
        GameObject panelObj = new GameObject("PerformancePanel");
        panelObj.transform.SetParent(performanceCanvas.transform, false);
        performancePanel = panelObj;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = backgroundColor;
        
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = windowPosition;
        panelRect.sizeDelta = windowSize;
        
        // Add vertical layout group
        VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        // Create header
        CreateLabel("Header", "Performance Monitor", 16, FontStyles.Bold);
        
        // Create performance metrics labels
        CreateLabel("FPS", "FPS: --", 14);
        CreateLabel("FrameTime", "Frame Time: -- ms", 14);
        CreateLabel("Memory", "Memory: -- MB", 14);
        CreateLabel("GCMemory", "GC Memory: -- MB", 14);
        CreateLabel("DrawCalls", "Draw Calls: --", 14);
        CreateLabel("GameObjects", "Game Objects: --", 14);
        CreateLabel("ActiveDice", "Active Dice: --", 14);
        CreateLabel("Camera", "Camera: (--,--,--)", 14);
        
        // Create separator
        CreateSeparator();
        
        // Create stats section
        CreateLabel("StatsHeader", "Statistics", 14, FontStyles.Bold);
        CreateLabel("AvgFPS", "Avg FPS: --", 12);
        CreateLabel("MinFPS", "Min FPS: --", 12);
        CreateLabel("MaxFPS", "Max FPS: --", 12);
        CreateLabel("AvgMemory", "Avg Memory: -- MB", 12);
        
        // Create warning area
        CreateSeparator();
        CreateLabel("Warning", "", 12, FontStyles.Bold, criticalColor);
        
        // Create FPS graph
        CreateFPSGraph();
        
        DontDestroyOnLoad(canvasObj);
    }
    
    private void CreateLabel(string key, string text, int fontSize, FontStyles style = FontStyles.Normal, Color? color = null)
    {
        GameObject labelObj = new GameObject($"Label_{key}");
        labelObj.transform.SetParent(performancePanel.transform, false);
        
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color ?? Color.white;
        label.enableAutoSizing = false;
        
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 0);
        labelRect.pivot = new Vector2(0, 0);
        
        ContentSizeFitter fitter = labelObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        labels[key] = label;
    }
    
    private void CreateSeparator()
    {
        GameObject separatorObj = new GameObject("Separator");
        separatorObj.transform.SetParent(performancePanel.transform, false);
        
        Image separator = separatorObj.AddComponent<Image>();
        separator.color = Color.gray;
        
        RectTransform sepRect = separatorObj.GetComponent<RectTransform>();
        sepRect.sizeDelta = new Vector2(0, 2);
        
        LayoutElement element = separatorObj.AddComponent<LayoutElement>();
        element.minHeight = 2;
        element.preferredHeight = 2;
    }
    
    private void CreateFPSGraph()
    {
        GameObject graphObj = new GameObject("FPSGraph");
        graphObj.transform.SetParent(performancePanel.transform, false);
        
        fpsGraph = graphObj.AddComponent<LineRenderer>();
        fpsGraph.material = new Material(Shader.Find("Sprites/Default"));
        fpsGraph.color = goodColor;
        fpsGraph.startWidth = 2f;
        fpsGraph.endWidth = 2f;
        fpsGraph.useWorldSpace = false;
        fpsGraph.sortingOrder = 2;
        
        RectTransform graphRect = graphObj.GetComponent<RectTransform>();
        graphRect.sizeDelta = new Vector2(0, 100);
        
        LayoutElement element = graphObj.AddComponent<LayoutElement>();
        element.minHeight = 100;
        element.preferredHeight = 100;
    }
    
    private void UpdateUI(PerformanceData data)
    {
        if (!isVisible || labels.Count == 0) return;
        
        // Update FPS with color coding
        Color fpsColor = GetPerformanceColor(data.fps, 60f, 30f);
        labels["FPS"].text = $"FPS: {data.fps:F1}";
        labels["FPS"].color = fpsColor;
        
        // Update frame time
        Color frameTimeColor = GetPerformanceColor(33.33f - data.frameTime, 16.67f, 0f);
        labels["FrameTime"].text = $"Frame Time: {data.frameTime:F2} ms";
        labels["FrameTime"].color = frameTimeColor;
        
        // Update memory
        float memoryMB = data.usedMemory / (1024f * 1024f);
        Color memoryColor = GetPerformanceColor(512f - memoryMB, 256f, 0f);
        labels["Memory"].text = $"Memory: {memoryMB:F1} MB";
        labels["Memory"].color = memoryColor;
        
        // Update GC memory
        float gcMemoryMB = data.gcMemory / (1024f * 1024f);
        labels["GCMemory"].text = $"GC Memory: {gcMemoryMB:F1} MB";
        
        // Update other metrics
        labels["DrawCalls"].text = $"Draw Calls: {data.drawCalls}";
        labels["GameObjects"].text = $"Game Objects: {data.totalGameObjects}";
        labels["ActiveDice"].text = $"Active Dice: {data.activeDice}";
        labels["Camera"].text = $"Camera: ({data.cameraPosition.x:F1},{data.cameraPosition.y:F1},{data.cameraPosition.z:F1})";
        
        // Update statistics
        if (monitor != null)
        {
            var stats = monitor.GetStats();
            if (stats.sampleCount > 0)
            {
                labels["AvgFPS"].text = $"Avg FPS: {stats.avgFps:F1}";
                labels["MinFPS"].text = $"Min FPS: {stats.minFps:F1}";
                labels["MaxFPS"].text = $"Max FPS: {stats.maxFps:F1}";
                labels["AvgMemory"].text = $"Avg Memory: {stats.avgMemory:F1} MB";
            }
        }
        
        // Update FPS graph
        UpdateFPSGraph(data.fps);
    }
    
    private void UpdateFPSGraph(float fps)
    {
        fpsHistory.Add(fps);
        if (fpsHistory.Count > maxGraphPoints)
        {
            fpsHistory.RemoveAt(0);
        }
        
        if (fpsGraph != null && fpsHistory.Count > 1)
        {
            fpsGraph.positionCount = fpsHistory.Count;
            Vector3[] points = new Vector3[fpsHistory.Count];
            
            float width = 300f; // Graph width
            float height = 80f;  // Graph height
            float maxFps = 120f; // Max FPS for scaling
            
            for (int i = 0; i < fpsHistory.Count; i++)
            {
                float x = (i / (float)(maxGraphPoints - 1)) * width - width / 2f;
                float y = (fpsHistory[i] / maxFps) * height - height / 2f;
                points[i] = new Vector3(x, y, 0);
            }
            
            fpsGraph.SetPositions(points);
        }
    }
    
    private Color GetPerformanceColor(float value, float good, float bad)
    {
        if (value >= good) return goodColor;
        if (value <= bad) return criticalColor;
        return warningColor;
    }
    
    private void ShowWarning(string message)
    {
        if (labels.ContainsKey("Warning"))
        {
            labels["Warning"].text = $"⚠ {message}";
            // Clear warning after 3 seconds
            Invoke(nameof(ClearWarning), 3f);
        }
    }
    
    private void ClearWarning()
    {
        if (labels.ContainsKey("Warning"))
        {
            labels["Warning"].text = "";
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI();
        }
    }
    
    public void ToggleUI()
    {
        isVisible = !isVisible;
        if (performancePanel != null)
        {
            performancePanel.SetActive(isVisible);
        }
    }
    
    public void SetPosition(Vector2 position)
    {
        windowPosition = position;
        if (performancePanel != null)
        {
            performancePanel.GetComponent<RectTransform>().anchoredPosition = position;
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        PerformanceMonitor.OnPerformanceUpdate -= UpdateUI;
        PerformanceMonitor.OnPerformanceWarning -= ShowWarning;
    }
}