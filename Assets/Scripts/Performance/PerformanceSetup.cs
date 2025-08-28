using UnityEngine;

[CreateAssetMenu(fileName = "PerformanceSetup", menuName = "Unity Dice/Performance Setup")]
public class PerformanceSetup : MonoBehaviour
{
    [Header("Quick Setup")]
    [SerializeField] private bool setupOnAwake = true;
    [SerializeField] private bool createPrefab = true;
    
    [Header("Components to Add")]
    [SerializeField] private bool addPerformanceMonitor = true;
    [SerializeField] private bool addPerformanceUI = true;
    [SerializeField] private bool addSystemResourceTracker = true;
    [SerializeField] private bool addPerformanceLogger = true;
    
    private void Awake()
    {
        if (setupOnAwake)
        {
            SetupPerformanceMonitoring();
        }
    }
    
    [ContextMenu("Setup Performance Monitoring")]
    public void SetupPerformanceMonitoring()
    {
        // Find or create performance monitor object
        GameObject monitorObject = GameObject.Find("PerformanceMonitor");
        
        if (monitorObject == null)
        {
            monitorObject = new GameObject("PerformanceMonitor");
            DontDestroyOnLoad(monitorObject);
        }
        
        // Add components
        if (addPerformanceMonitor && monitorObject.GetComponent<PerformanceMonitor>() == null)
        {
            monitorObject.AddComponent<PerformanceMonitor>();
            Debug.Log("[PerformanceSetup] Added PerformanceMonitor component");
        }
        
        if (addPerformanceUI && monitorObject.GetComponent<PerformanceUI>() == null)
        {
            monitorObject.AddComponent<PerformanceUI>();
            Debug.Log("[PerformanceSetup] Added PerformanceUI component");
        }
        
        if (addSystemResourceTracker && monitorObject.GetComponent<SystemResourceTracker>() == null)
        {
            monitorObject.AddComponent<SystemResourceTracker>();
            Debug.Log("[PerformanceSetup] Added SystemResourceTracker component");
        }
        
        if (addPerformanceLogger && monitorObject.GetComponent<PerformanceLogger>() == null)
        {
            monitorObject.AddComponent<PerformanceLogger>();
            Debug.Log("[PerformanceSetup] Added PerformanceLogger component");
        }
        
        Debug.Log("[PerformanceSetup] Performance monitoring setup completed!");
        
        if (createPrefab)
        {
            CreatePerformanceMonitorPrefab(monitorObject);
        }
    }
    
    private void CreatePerformanceMonitorPrefab(GameObject monitorObject)
    {
#if UNITY_EDITOR
        try
        {
            string prefabPath = "Assets/Prefabs/PerformanceMonitor.prefab";
            
            // Create directory if it doesn't exist
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            // Create prefab
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(monitorObject, prefabPath);
            Debug.Log($"[PerformanceSetup] Prefab created at: {prefabPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PerformanceSetup] Failed to create prefab: {e.Message}");
        }
#endif
    }
    
    [ContextMenu("Remove Performance Monitoring")]
    public void RemovePerformanceMonitoring()
    {
        GameObject monitorObject = GameObject.Find("PerformanceMonitor");
        if (monitorObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(monitorObject);
            }
            else
            {
                DestroyImmediate(monitorObject);
            }
            Debug.Log("[PerformanceSetup] Performance monitoring removed");
        }
    }
}