using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 테스트 인프라 컴포넌트들의 기본 동작을 검증하는 테스트
/// Stream A 구현이 올바르게 동작하는지 확인합니다.
/// </summary>
public class TestInfrastructureValidationTests
{
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        _testGameObject = new GameObject("TestInfrastructureValidation");
    }
    
    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
    }
    
    #region TestUtilities Tests
    
    [UnityTest]
    public IEnumerator TestUtilities_WaitForCondition_Should_Wait_Until_Condition_Is_Met()
    {
        bool conditionMet = false;
        float startTime = Time.time;
        
        // Start a coroutine that sets the condition after 1 second
        StartCoroutineHelper(() => conditionMet = true, 1f);
        
        // Wait for condition with 3 second timeout
        yield return TestUtilities.WaitForCondition(() => conditionMet, 3f);
        
        float elapsedTime = Time.time - startTime;
        
        Assert.IsTrue(conditionMet, "Condition should be met");
        Assert.IsTrue(elapsedTime >= 1f && elapsedTime < 2f, $"Should wait approximately 1 second, actual: {elapsedTime:F1}s");
    }
    
    [Test]
    public void TestUtilities_SimulateButtonClick_Should_Trigger_Button_Event()
    {
        // Arrange
        Button testButton = TestUtilities.CreateTestButton();
        bool buttonClicked = false;
        testButton.onClick.AddListener(() => buttonClicked = true);
        
        // Act
        TestUtilities.SimulateButtonClick(testButton);
        
        // Assert
        Assert.IsTrue(buttonClicked, "Button click should be triggered");
        
        // Cleanup
        TestUtilities.SafeDestroy(testButton.gameObject);
    }
    
    [Test]
    public void TestUtilities_AssertGameObjectActive_Should_Pass_For_Active_Object()
    {
        // Arrange
        GameObject activeObject = new GameObject("ActiveObject");
        activeObject.SetActive(true);
        
        // Act & Assert - Should not throw
        TestUtilities.AssertGameObjectActive(activeObject);
        
        // Cleanup
        TestUtilities.SafeDestroy(activeObject);
    }
    
    [Test]
    public void TestUtilities_MeasureExecutionTime_Should_Return_Positive_Time()
    {
        // Arrange
        System.Action testAction = () => {
            // Simulate some work
            for (int i = 0; i < 1000; i++)
            {
                _ = Mathf.Sin(i * 0.01f);
            }
        };
        
        // Act
        double executionTime = TestUtilities.MeasureExecutionTime(testAction);
        
        // Assert
        Assert.IsTrue(executionTime > 0, $"Execution time should be positive, actual: {executionTime}ms");
    }
    
    #endregion
    
    #region MockWebSocketServer Tests
    
    [UnityTest]
    public IEnumerator MockWebSocketServer_Should_Initialize_Properly()
    {
        // Arrange
        MockWebSocketServer mockServer = _testGameObject.AddComponent<MockWebSocketServer>();
        
        // Act
        yield return null; // Wait one frame for Awake to be called
        
        // Assert
        Assert.IsNotNull(mockServer, "MockWebSocketServer should be created");
        Assert.AreEqual(MockWebSocketServer.MockServerState.Disconnected, mockServer.CurrentState, "Initial state should be Disconnected");
        Assert.IsFalse(mockServer.IsConnected, "Should not be connected initially");
    }
    
    [UnityTest]
    public IEnumerator MockWebSocketServer_Connection_Simulation_Should_Work()
    {
        // Arrange
        MockWebSocketServer mockServer = _testGameObject.AddComponent<MockWebSocketServer>();
        yield return null; // Wait for Awake
        
        bool stateChanged = false;
        MockWebSocketServer.OnStateChanged += (state) => {
            if (state == MockWebSocketServer.MockServerState.Connected)
                stateChanged = true;
        };
        
        // Act
        yield return mockServer.SimulateConnection(0.1f, true);
        
        // Assert
        Assert.IsTrue(mockServer.IsConnected, "Should be connected after simulation");
        Assert.IsTrue(stateChanged, "State change event should be triggered");
        
        // Cleanup
        MockWebSocketServer.OnStateChanged = null;
    }
    
    [UnityTest]
    public IEnumerator MockWebSocketServer_Network_Instability_Should_Change_State()
    {
        // Arrange
        MockWebSocketServer mockServer = _testGameObject.AddComponent<MockWebSocketServer>();
        yield return null;
        yield return mockServer.SimulateConnection(0.1f, true);
        
        // Act
        mockServer.SimulateNetworkInstability(true, 0.5f);
        yield return null;
        
        // Assert
        Assert.AreEqual(MockWebSocketServer.MockServerState.Unstable, mockServer.CurrentState, "State should change to Unstable");
        
        // Act
        mockServer.SimulateNetworkInstability(false);
        yield return null;
        
        // Assert
        Assert.AreEqual(MockWebSocketServer.MockServerState.Connected, mockServer.CurrentState, "State should return to Connected");
    }
    
    #endregion
    
    #region FPSCounter Tests
    
    [UnityTest]
    public IEnumerator FPSCounter_Should_Start_And_Stop_Monitoring()
    {
        // Arrange
        FPSCounter fpsCounter = _testGameObject.AddComponent<FPSCounter>();
        yield return null;
        
        // Act
        fpsCounter.StartMonitoring();
        yield return new WaitForSeconds(1f);
        
        // Assert
        Assert.IsTrue(fpsCounter.IsMonitoring, "Should be monitoring");
        Assert.IsTrue(fpsCounter.CurrentFPS > 0, $"Current FPS should be positive: {fpsCounter.CurrentFPS}");
        Assert.IsTrue(fpsCounter.TotalFrames > 0, "Should have counted frames");
        
        // Act
        fpsCounter.StopMonitoring();
        
        // Assert
        Assert.IsFalse(fpsCounter.IsMonitoring, "Should stop monitoring");
    }
    
    [UnityTest]
    public IEnumerator FPSCounter_Performance_Report_Should_Contain_Valid_Data()
    {
        // Arrange
        FPSCounter fpsCounter = _testGameObject.AddComponent<FPSCounter>();
        yield return null;
        
        // Act
        fpsCounter.StartMonitoring();
        yield return new WaitForSeconds(1f);
        fpsCounter.StopMonitoring();
        
        string report = fpsCounter.GetPerformanceReport();
        var statistics = fpsCounter.GetDetailedStatistics();
        
        // Assert
        Assert.IsNotNull(report, "Performance report should not be null");
        Assert.IsTrue(report.Contains("FPS Performance Report"), "Report should contain header");
        Assert.IsTrue(statistics.TotalFrames > 0, "Statistics should show counted frames");
        Assert.IsTrue(statistics.MonitoringDuration > 0, "Should have positive monitoring duration");
    }
    
    #endregion
    
    #region MemoryProfiler Tests
    
    [UnityTest]
    public IEnumerator MemoryProfiler_Should_Establish_Baseline_And_Profile()
    {
        // Arrange
        MemoryProfiler memoryProfiler = _testGameObject.AddComponent<MemoryProfiler>();
        yield return null;
        
        // Act
        memoryProfiler.StartProfiling(true);
        yield return new WaitForSeconds(0.5f);
        
        long initialMemory = memoryProfiler.CurrentMemoryUsage;
        
        // Create some objects to increase memory usage
        List<GameObject> tempObjects = new List<GameObject>();
        for (int i = 0; i < 100; i++)
        {
            tempObjects.Add(new GameObject($"TempObject_{i}"));
        }
        
        yield return new WaitForSeconds(0.5f);
        memoryProfiler.StopProfiling();
        
        // Assert
        Assert.IsTrue(memoryProfiler.CurrentMemoryUsage > 0, "Should have positive memory usage");
        Assert.IsTrue(memoryProfiler.CurrentMemoryUsageMB >= 0, "Memory usage in MB should be valid");
        
        // Cleanup
        foreach (var obj in tempObjects)
        {
            TestUtilities.SafeDestroy(obj);
        }
    }
    
    [UnityTest]
    public IEnumerator MemoryProfiler_Memory_Report_Should_Contain_Valid_Data()
    {
        // Arrange
        MemoryProfiler memoryProfiler = _testGameObject.AddComponent<MemoryProfiler>();
        yield return null;
        
        // Act
        memoryProfiler.StartProfiling(true);
        yield return new WaitForSeconds(0.5f);
        memoryProfiler.StopProfiling();
        
        var report = memoryProfiler.GenerateMemoryReport();
        string reportString = memoryProfiler.GetMemoryReportString();
        
        // Assert
        Assert.IsNotNull(report, "Memory report should not be null");
        Assert.IsNotNull(reportString, "Memory report string should not be null");
        Assert.IsTrue(report.ProfilingDuration > 0, "Should have positive profiling duration");
        Assert.IsTrue(report.BaselineMemoryMB > 0, "Should have positive baseline memory");
        Assert.IsTrue(reportString.Contains("Memory Profiling Report"), "Report string should contain header");
    }
    
    [UnityTest]
    public IEnumerator MemoryProfiler_Memory_Leak_Test_Should_Run_Iterations()
    {
        // Arrange
        MemoryProfiler memoryProfiler = _testGameObject.AddComponent<MemoryProfiler>();
        yield return null;
        
        int iterationCount = 0;
        System.Action testAction = () => {
            iterationCount++;
            // Create and immediately destroy object to avoid accumulation
            var tempObj = new GameObject($"TestIteration_{iterationCount}");
            TestUtilities.SafeDestroy(tempObj);
        };
        
        // Act
        yield return memoryProfiler.RunMemoryLeakTest(testAction, 10);
        
        // Assert
        Assert.AreEqual(10, iterationCount, "Should run exactly 10 iterations");
        Assert.IsFalse(memoryProfiler.IsProfilingActive, "Profiling should be stopped after test");
    }
    
    #endregion
    
    #region Integration Tests
    
    [UnityTest]
    public IEnumerator Test_Infrastructure_Components_Should_Work_Together()
    {
        // Arrange
        var fpsCounter = _testGameObject.AddComponent<FPSCounter>();
        var memoryProfiler = _testGameObject.AddComponent<MemoryProfiler>();
        var mockServer = _testGameObject.AddComponent<MockWebSocketServer>();
        
        yield return null;
        
        // Act - Start all monitoring systems
        fpsCounter.StartMonitoring();
        memoryProfiler.StartProfiling(true);
        yield return mockServer.SimulateConnection(0.1f, true);
        
        // Simulate some workload
        yield return new WaitForSeconds(1f);
        
        // Stop all systems
        fpsCounter.StopMonitoring();
        memoryProfiler.StopProfiling();
        yield return mockServer.SimulateDisconnection(0.1f);
        
        // Assert
        Assert.IsTrue(fpsCounter.TotalFrames > 0, "FPS counter should have measured frames");
        Assert.IsTrue(memoryProfiler.CurrentMemoryUsage > 0, "Memory profiler should have measured memory");
        Assert.IsFalse(mockServer.IsConnected, "Mock server should be disconnected");
        
        // Verify reports can be generated
        string fpsReport = fpsCounter.GetPerformanceReport();
        string memoryReport = memoryProfiler.GetMemoryReportString();
        
        Assert.IsNotNull(fpsReport, "FPS report should be generated");
        Assert.IsNotNull(memoryReport, "Memory report should be generated");
    }
    
    #endregion
    
    #region Helper Methods
    
    private void StartCoroutineHelper(System.Action action, float delay)
    {
        _testGameObject.AddComponent<CoroutineHelper>().StartDelayedAction(action, delay);
    }
    
    private class CoroutineHelper : MonoBehaviour
    {
        public void StartDelayedAction(System.Action action, float delay)
        {
            StartCoroutine(DelayedActionCoroutine(action, delay));
        }
        
        private IEnumerator DelayedActionCoroutine(System.Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }
    }
    
    #endregion
}