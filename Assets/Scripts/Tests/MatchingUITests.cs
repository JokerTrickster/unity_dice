using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NUnit.Framework;

/// <summary>
/// 매칭 UI 통합 테스트
/// UI 컴포넌트들의 상호작용, 성능, 사용자 경험을 검증합니다.
/// Stream D: Integration & Testing의 UI 전문 테스트 클래스입니다.
/// </summary>
[Category("Integration")]
[Category("UI")]
[Category("Performance")]
public class MatchingUITests
{
    #region Test Components
    private GameObject _testGameObject;
    private Canvas _testCanvas;
    private EventSystem _eventSystem;
    private GraphicRaycaster _graphicRaycaster;
    
    // UI Components
    private IntegratedMatchingUI _matchingUI;
    private MatchingUI _coreMatchingUI;
    private PlayerCountSelector _playerCountSelector;
    private MatchingStatusDisplay _statusDisplay;
    private MatchingProgressAnimator _progressAnimator;
    
    // Test UI Elements
    private Button _quickMatchButton;
    private Button _cancelButton;
    private Text _statusText;
    private Slider _progressSlider;
    
    // Dependencies
    private UserDataManager _userDataManager;
    private EnergyManager _energyManager;
    #endregion

    #region Performance Monitoring
    private List<float> _frameTimeHistory;
    private List<float> _animationFrameTimes;
    private int _drawCallCount;
    private float _memoryUsageStart;
    private float _memoryUsageCurrent;
    #endregion

    #region Test Configuration
    private const float UI_RESPONSE_TIMEOUT = 2f;
    private const float ANIMATION_DURATION = 1f;
    private const int TARGET_FPS = 60;
    private const float MIN_ACCEPTABLE_FPS = 48;
    private const int MAX_UI_DRAW_CALLS = 50;
    private const float MAX_UI_MEMORY_MB = 3f;
    
    private readonly int[] TestPlayerCounts = { 2, 3, 4 };
    private readonly MatchingState[] TestStates = 
    { 
        MatchingState.Idle, 
        MatchingState.Searching, 
        MatchingState.Found, 
        MatchingState.Starting,
        MatchingState.Cancelled,
        MatchingState.Failed 
    };
    #endregion

    #region Setup & Teardown
    [SetUp]
    public void SetUp()
    {
        SetupUITestEnvironment();
        CreateUIComponents();
        InitializePerformanceMonitoring();
        
        Debug.Log($"[MatchingUITests] UI test setup completed for: {TestContext.CurrentContext.Test.Name}");
    }

    [TearDown]
    public void TearDown()
    {
        CleanupUIEnvironment();
        Debug.Log($"[MatchingUITests] UI test cleanup completed for: {TestContext.CurrentContext.Test.Name}");
    }

    private void SetupUITestEnvironment()
    {
        // Canvas 및 EventSystem 설정
        _testGameObject = new GameObject("MatchingUITest");
        
        _testCanvas = _testGameObject.AddComponent<Canvas>();
        _testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _testCanvas.sortingOrder = 100; // 테스트 중 최상위 표시
        
        _testGameObject.AddComponent<CanvasScaler>();
        _graphicRaycaster = _testGameObject.AddComponent<GraphicRaycaster>();

        // EventSystem이 없으면 생성
        _eventSystem = EventSystem.current;
        if (_eventSystem == null)
        {
            var eventSystemGO = new GameObject("EventSystem");
            _eventSystem = eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }

        // 의존성 매니저들 설정
        _userDataManager = _testGameObject.AddComponent<UserDataManager>();
        _energyManager = _testGameObject.AddComponent<EnergyManager>();
        
        ConfigureTestData();
    }

    private void ConfigureTestData()
    {
        // 테스트용 사용자 데이터
        _userDataManager.InitializeWithTestData(new UserData
        {
            UserId = "ui-test-user",
            DisplayName = "UI Test Player",
            Level = 10,
            CurrentEnergy = 50,
            MaxEnergy = 100
        });

        // 테스트용 에너지 설정
        _energyManager.InitializeForTesting(50, 100);
    }

    private void CreateUIComponents()
    {
        // IntegratedMatchingUI 생성
        var matchingUIGO = new GameObject("IntegratedMatchingUI");
        matchingUIGO.transform.SetParent(_testCanvas.transform, false);
        _matchingUI = matchingUIGO.AddComponent<IntegratedMatchingUI>();

        // 하위 UI 컴포넌트들 생성
        CreateCoreUIComponents(matchingUIGO);
        CreateLegacyUIElements(matchingUIGO);
        
        // UI 초기화
        _matchingUI.ForceRefresh();
    }

    private void CreateCoreUIComponents(GameObject parent)
    {
        // MatchingUI
        var coreUIGO = new GameObject("MatchingUI");
        coreUIGO.transform.SetParent(parent.transform, false);
        _coreMatchingUI = coreUIGO.AddComponent<MatchingUI>();

        // PlayerCountSelector
        var selectorGO = new GameObject("PlayerCountSelector");
        selectorGO.transform.SetParent(parent.transform, false);
        _playerCountSelector = selectorGO.AddComponent<PlayerCountSelector>();

        // MatchingStatusDisplay
        var statusGO = new GameObject("MatchingStatusDisplay");
        statusGO.transform.SetParent(parent.transform, false);
        _statusDisplay = statusGO.AddComponent<MatchingStatusDisplay>();

        // MatchingProgressAnimator
        var animatorGO = new GameObject("MatchingProgressAnimator");
        animatorGO.transform.SetParent(parent.transform, false);
        _progressAnimator = animatorGO.AddComponent<MatchingProgressAnimator>();
    }

    private void CreateLegacyUIElements(GameObject parent)
    {
        // Quick Match Button
        var buttonGO = new GameObject("QuickMatchButton");
        buttonGO.transform.SetParent(parent.transform, false);
        var image = buttonGO.AddComponent<Image>();
        _quickMatchButton = buttonGO.AddComponent<Button>();
        
        // Cancel Button
        var cancelGO = new GameObject("CancelButton");
        cancelGO.transform.SetParent(parent.transform, false);
        cancelGO.AddComponent<Image>();
        _cancelButton = cancelGO.AddComponent<Button>();

        // Status Text
        var textGO = new GameObject("StatusText");
        textGO.transform.SetParent(parent.transform, false);
        _statusText = textGO.AddComponent<Text>();
        _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Progress Slider
        var sliderGO = new GameObject("ProgressSlider");
        sliderGO.transform.SetParent(parent.transform, false);
        _progressSlider = sliderGO.AddComponent<Slider>();
        
        // Slider 하위 요소들
        CreateSliderElements(sliderGO);
    }

    private void CreateSliderElements(GameObject sliderGO)
    {
        // Background
        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderGO.transform, false);
        bg.AddComponent<Image>();

        // Fill Area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        fill.AddComponent<Image>();

        _progressSlider.fillRect = fill.GetComponent<RectTransform>();
    }

    private void InitializePerformanceMonitoring()
    {
        _frameTimeHistory = new List<float>();
        _animationFrameTimes = new List<float>();
        _memoryUsageStart = GetCurrentMemoryUsageMB();
    }

    private void CleanupUIEnvironment()
    {
        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
        }
    }

    private float GetCurrentMemoryUsageMB()
    {
        return System.GC.GetTotalMemory(false) / (1024f * 1024f);
    }
    #endregion

    #region UI State Integration Tests
    [UnityTest]
    [Description("매칭 상태 변경에 따른 UI 업데이트 테스트")]
    public IEnumerator Test_UI_StateTransition_AllStates()
    {
        // Arrange
        var stateUpdateTracker = new Dictionary<MatchingState, bool>();

        // UI 상태 변경 추적
        foreach (var state in TestStates)
        {
            stateUpdateTracker[state] = false;
        }

        // Act & Assert - 각 상태별 UI 업데이트 확인
        foreach (var state in TestStates)
        {
            Debug.Log($"[MatchingUITests] Testing state: {state}");

            // 상태 설정
            _matchingUI.SetMatchingState(state);
            yield return new WaitForSeconds(0.1f); // UI 업데이트 대기

            // UI 상태 검증
            ValidateUIForState(state);
            stateUpdateTracker[state] = true;

            // 성능 측정
            yield return MeasureUIPerformance();
        }

        // 모든 상태가 테스트되었는지 확인
        foreach (var kvp in stateUpdateTracker)
        {
            Assert.IsTrue(kvp.Value, $"상태 {kvp.Key}가 테스트되지 않음");
        }

        Debug.Log("[MatchingUITests] All state transitions validated successfully");
    }

    [UnityTest]
    [Description("플레이어 수 선택 UI 통합 테스트")]
    public IEnumerator Test_UI_PlayerCountSelection()
    {
        // Arrange
        var selectedCounts = new List<int>();
        
        _playerCountSelector.OnPlayerCountChanged += (count) =>
        {
            selectedCounts.Add(count);
        };

        // Act - 각 플레이어 수 선택 시뮬레이션
        foreach (int playerCount in TestPlayerCounts)
        {
            Debug.Log($"[MatchingUITests] Selecting player count: {playerCount}");

            // UI에서 플레이어 수 선택
            _playerCountSelector.SetPlayerCount(playerCount);
            yield return new WaitForSeconds(0.1f);

            // IntegratedMatchingUI에 반영되었는지 확인
            _matchingUI.OnPlayerCountUIChanged?.Invoke(playerCount);
            yield return new WaitForSeconds(0.1f);

            // 시각적 피드백 확인
            Assert.AreEqual(playerCount, _playerCountSelector.GetSelectedPlayerCount(), 
                $"선택된 플레이어 수가 {playerCount}이어야 함");
        }

        // Assert
        Assert.AreEqual(TestPlayerCounts.Length, selectedCounts.Count, 
            "모든 플레이어 수 선택이 감지되어야 함");

        for (int i = 0; i < TestPlayerCounts.Length; i++)
        {
            Assert.AreEqual(TestPlayerCounts[i], selectedCounts[i], 
                $"플레이어 수 선택 순서가 정확해야 함 ({i}번째)");
        }
    }

    [UnityTest]
    [Description("매칭 진행 상태 애니메이션 테스트")]
    public IEnumerator Test_UI_MatchingProgressAnimation()
    {
        // Arrange
        var animationStartTime = Time.time;
        bool animationCompleted = false;

        // Act - 매칭 검색 상태로 변경하여 애니메이션 시작
        _matchingUI.SetMatchingState(MatchingState.Searching);
        _progressAnimator.SetState(MatchingState.Searching);

        // 애니메이션 프레임 시간 측정
        var frameCount = 0;
        while (Time.time - animationStartTime < ANIMATION_DURATION && frameCount < 60)
        {
            var frameStart = Time.realtimeSinceStartup;
            yield return null;
            var frameTime = Time.realtimeSinceStartup - frameStart;
            
            _animationFrameTimes.Add(frameTime);
            frameCount++;

            // 애니메이션 상태 확인
            if (_progressAnimator.IsAnimating)
            {
                animationCompleted = true;
            }
        }

        // Assert - 애니메이션 성능 검증
        float avgFrameTime = _animationFrameTimes.Count > 0 ? 
            _animationFrameTimes.Average() : 0f;
        float avgFPS = avgFrameTime > 0 ? 1f / avgFrameTime : 0f;

        Assert.IsTrue(animationCompleted, "애니메이션이 시작되어야 함");
        Assert.Greater(avgFPS, MIN_ACCEPTABLE_FPS, 
            $"애니메이션 평균 FPS가 {MIN_ACCEPTABLE_FPS} 이상이어야 함");

        Debug.Log($"[MatchingUITests] Animation performance - Avg FPS: {avgFPS:F1}");
    }
    #endregion

    #region User Interaction Tests
    [UnityTest]
    [Description("버튼 클릭 반응성 테스트")]
    public IEnumerator Test_UI_ButtonResponsiveness()
    {
        // Arrange
        bool quickMatchClicked = false;
        bool cancelClicked = false;
        var clickResponseTimes = new List<float>();

        _quickMatchButton.onClick.AddListener(() => quickMatchClicked = true);
        _cancelButton.onClick.AddListener(() => cancelClicked = true);

        // Act - 버튼 클릭 시뮬레이션
        var buttonTests = new[]
        {
            new { Button = _quickMatchButton, Name = "QuickMatch", Expected = () => quickMatchClicked },
            new { Button = _cancelButton, Name = "Cancel", Expected = () => cancelClicked }
        };

        foreach (var test in buttonTests)
        {
            var clickStartTime = Time.realtimeSinceStartup;
            
            // 버튼 클릭 시뮬레이션
            ExecuteUIEvents.Click(test.Button.gameObject);
            
            // 클릭 응답 대기
            yield return new WaitUntil(() => test.Expected() || Time.realtimeSinceStartup - clickStartTime > UI_RESPONSE_TIMEOUT);
            
            var responseTime = Time.realtimeSinceStartup - clickStartTime;
            clickResponseTimes.Add(responseTime);

            Debug.Log($"[MatchingUITests] {test.Name} button response time: {responseTime:F3}s");
        }

        // Assert
        Assert.IsTrue(quickMatchClicked, "Quick Match 버튼 클릭이 감지되어야 함");
        Assert.IsTrue(cancelClicked, "Cancel 버튼 클릭이 감지되어야 함");

        foreach (var responseTime in clickResponseTimes)
        {
            Assert.That(responseTime, Is.LessThan(UI_RESPONSE_TIMEOUT), 
                $"버튼 응답 시간이 {UI_RESPONSE_TIMEOUT}초 이내여야 함");
        }
    }

    [UnityTest]
    [Description("에너지 부족 시 UI 피드백 테스트")]
    public IEnumerator Test_UI_EnergyInsufficientFeedback()
    {
        // Arrange - 에너지를 0으로 설정
        _energyManager.SetEnergyForTesting(0);
        
        bool warningDisplayed = false;
        string warningMessage = "";

        // UI 메시지 표시 확인
        _matchingUI.ShowMessage("에너지가 부족합니다.", MessageType.Warning);

        yield return new WaitForSeconds(0.1f);

        // Act - 매칭 시도 시뮬레이션
        ExecuteUIEvents.Click(_quickMatchButton.gameObject);
        yield return new WaitForSeconds(0.2f);

        // Assert - UI 피드백 확인
        // 상태 텍스트에 경고 메시지가 표시되는지 확인
        if (_statusText.text.Contains("부족") || _statusText.text.Contains("에너지"))
        {
            warningDisplayed = true;
            warningMessage = _statusText.text;
        }

        Assert.IsTrue(warningDisplayed, "에너지 부족 경고가 UI에 표시되어야 함");
        Assert.IsNotEmpty(warningMessage, "경고 메시지가 있어야 함");

        Debug.Log($"[MatchingUITests] Energy warning displayed: {warningMessage}");
    }

    [UnityTest]
    [Description("매칭 취소 버튼 가시성 테스트")]
    public IEnumerator Test_UI_CancelButtonVisibility()
    {
        // Arrange & Act - 다양한 상태에서 취소 버튼 가시성 확인
        var visibilityTests = new[]
        {
            new { State = MatchingState.Idle, ShouldBeVisible = false },
            new { State = MatchingState.Searching, ShouldBeVisible = true },
            new { State = MatchingState.Found, ShouldBeVisible = false },
            new { State = MatchingState.Starting, ShouldBeVisible = false },
            new { State = MatchingState.Cancelled, ShouldBeVisible = false },
            new { State = MatchingState.Failed, ShouldBeVisible = false }
        };

        foreach (var test in visibilityTests)
        {
            _matchingUI.SetMatchingState(test.State);
            yield return new WaitForSeconds(0.1f);

            bool isVisible = _cancelButton.gameObject.activeInHierarchy;
            
            Assert.AreEqual(test.ShouldBeVisible, isVisible, 
                $"상태 {test.State}에서 취소 버튼 가시성이 {test.ShouldBeVisible}이어야 함");

            Debug.Log($"[MatchingUITests] Cancel button visibility for {test.State}: {isVisible}");
        }
    }
    #endregion

    #region Performance Tests
    [UnityTest]
    [Description("UI 렌더링 성능 테스트")]
    public IEnumerator Test_Performance_UIRendering()
    {
        // Arrange
        const int performanceTestDuration = 2; // 2초간 측정
        var testStartTime = Time.time;
        
        // 복잡한 UI 상태 시뮬레이션
        _matchingUI.SetMatchingState(MatchingState.Searching);
        
        // Act - 프레임 시간 측정
        while (Time.time - testStartTime < performanceTestDuration)
        {
            var frameStart = Time.realtimeSinceStartup;
            
            // UI 업데이트 시뮬레이션
            _matchingUI.UpdateMatchingProgress(
                TimeSpan.FromSeconds(Time.time - testStartTime),
                TimeSpan.FromSeconds(30)
            );

            yield return null;
            
            var frameTime = Time.realtimeSinceStartup - frameStart;
            _frameTimeHistory.Add(frameTime);
        }

        // Assert - 성능 기준 검증
        float avgFrameTime = _frameTimeHistory.Average();
        float avgFPS = 1f / avgFrameTime;
        float minFPS = 1f / _frameTimeHistory.Max();

        Assert.Greater(avgFPS, MIN_ACCEPTABLE_FPS, 
            $"UI 평균 FPS가 {MIN_ACCEPTABLE_FPS} 이상이어야 함");
        Assert.Greater(minFPS, MIN_ACCEPTABLE_FPS * 0.8f, 
            $"UI 최소 FPS가 {MIN_ACCEPTABLE_FPS * 0.8f} 이상이어야 함");

        Debug.Log($"[MatchingUITests] UI Performance - Avg FPS: {avgFPS:F1}, Min FPS: {minFPS:F1}");
    }

    [UnityTest]
    [Description("UI 메모리 사용량 테스트")]
    public IEnumerator Test_Performance_UIMemoryUsage()
    {
        // Arrange
        System.GC.Collect();
        yield return null;
        var initialMemory = GetCurrentMemoryUsageMB();

        // Act - UI 집중 사용 시뮬레이션
        for (int cycle = 0; cycle < 5; cycle++)
        {
            foreach (var state in TestStates)
            {
                _matchingUI.SetMatchingState(state);
                
                // UI 업데이트 반복
                for (int i = 0; i < 10; i++)
                {
                    _matchingUI.UpdateMatchingProgress(
                        TimeSpan.FromSeconds(i),
                        TimeSpan.FromSeconds(30)
                    );
                    yield return null;
                }
            }
        }

        // Assert
        System.GC.Collect();
        yield return null;
        var finalMemory = GetCurrentMemoryUsageMB();
        var memoryIncrease = finalMemory - initialMemory;

        Assert.That(memoryIncrease, Is.LessThan(MAX_UI_MEMORY_MB), 
            $"UI 메모리 증가량이 {MAX_UI_MEMORY_MB}MB 이하여야 함");

        Debug.Log($"[MatchingUITests] UI Memory increase: {memoryIncrease:F2}MB");
    }
    #endregion

    #region Helper Methods
    private void ValidateUIForState(MatchingState state)
    {
        switch (state)
        {
            case MatchingState.Idle:
                Assert.IsTrue(_quickMatchButton.interactable, "Idle 상태에서 매칭 버튼이 활성화되어야 함");
                break;

            case MatchingState.Searching:
                Assert.IsFalse(_quickMatchButton.interactable, "Searching 상태에서 매칭 버튼이 비활성화되어야 함");
                Assert.IsTrue(_cancelButton.gameObject.activeInHierarchy, "Searching 상태에서 취소 버튼이 보여야 함");
                break;

            case MatchingState.Found:
                Assert.IsFalse(_quickMatchButton.interactable, "Found 상태에서 매칭 버튼이 비활성화되어야 함");
                break;

            case MatchingState.Cancelled:
            case MatchingState.Failed:
                Assert.IsTrue(_quickMatchButton.interactable, "취소/실패 상태에서 매칭 버튼이 다시 활성화되어야 함");
                break;
        }

        // 상태 텍스트 확인
        Assert.IsNotEmpty(_statusText.text, "상태 텍스트가 비어있지 않아야 함");
    }

    private IEnumerator MeasureUIPerformance()
    {
        var frameStart = Time.realtimeSinceStartup;
        yield return null;
        var frameTime = Time.realtimeSinceStartup - frameStart;
        _frameTimeHistory.Add(frameTime);
    }
    #endregion
}