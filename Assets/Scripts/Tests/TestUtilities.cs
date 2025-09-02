using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// 통합 테스트를 위한 공통 유틸리티 클래스
/// 모든 테스트 스트림에서 사용할 수 있는 헬퍼 메서드들을 제공합니다.
/// </summary>
public static class TestUtilities
{
    #region Wait Utilities
    
    /// <summary>
    /// 지정된 조건이 만족될 때까지 대기합니다.
    /// </summary>
    /// <param name="condition">확인할 조건</param>
    /// <param name="timeout">타임아웃 시간 (초)</param>
    /// <param name="customErrorMessage">실패 시 표시할 사용자 정의 에러 메시지</param>
    /// <returns>조건이 만족될 때까지 대기하는 코루틴</returns>
    public static IEnumerator WaitForCondition(Func<bool> condition, float timeout = 10f, string customErrorMessage = null)
    {
        float timer = 0f;
        while (timer < timeout && !condition())
        {
            yield return null;
            timer += Time.deltaTime;
        }
        
        string errorMessage = customErrorMessage ?? $"Condition not met within {timeout}s";
        Assert.IsTrue(condition(), errorMessage);
    }
    
    /// <summary>
    /// 지정된 시간만큼 대기합니다.
    /// </summary>
    /// <param name="seconds">대기 시간 (초)</param>
    /// <returns>대기 코루틴</returns>
    public static IEnumerator WaitForSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }
    
    /// <summary>
    /// 한 프레임 대기합니다.
    /// </summary>
    /// <returns>한 프레임 대기 코루틴</returns>
    public static IEnumerator WaitForFrame()
    {
        yield return null;
    }
    
    /// <summary>
    /// 여러 프레임 대기합니다.
    /// </summary>
    /// <param name="frames">대기할 프레임 수</param>
    /// <returns>지정된 프레임 수만큼 대기하는 코루틴</returns>
    public static IEnumerator WaitForFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }
    
    #endregion
    
    #region UI Simulation Utilities
    
    /// <summary>
    /// 버튼 클릭을 시뮬레이션합니다.
    /// </summary>
    /// <param name="button">클릭할 버튼</param>
    public static void SimulateButtonClick(Button button)
    {
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
        }
        else
        {
            throw new ArgumentException($"Button is null or not interactable: {button?.name ?? "null"}");
        }
    }
    
    /// <summary>
    /// 입력 필드에 텍스트를 입력합니다.
    /// </summary>
    /// <param name="inputField">입력 필드</param>
    /// <param name="text">입력할 텍스트</param>
    public static void SimulateTextInput(InputField inputField, string text)
    {
        if (inputField != null && inputField.interactable)
        {
            inputField.text = text;
            inputField.onEndEdit.Invoke(text);
        }
        else
        {
            throw new ArgumentException($"InputField is null or not interactable: {inputField?.name ?? "null"}");
        }
    }
    
    /// <summary>
    /// 토글을 시뮬레이션합니다.
    /// </summary>
    /// <param name="toggle">토글 컴포넌트</param>
    /// <param name="value">설정할 값</param>
    public static void SimulateToggle(Toggle toggle, bool value)
    {
        if (toggle != null && toggle.interactable)
        {
            toggle.isOn = value;
            toggle.onValueChanged.Invoke(value);
        }
        else
        {
            throw new ArgumentException($"Toggle is null or not interactable: {toggle?.name ?? "null"}");
        }
    }
    
    /// <summary>
    /// 슬라이더 값을 시뮬레이션합니다.
    /// </summary>
    /// <param name="slider">슬라이더 컴포넌트</param>
    /// <param name="value">설정할 값</param>
    public static void SimulateSlider(Slider slider, float value)
    {
        if (slider != null && slider.interactable)
        {
            slider.value = value;
            slider.onValueChanged.Invoke(value);
        }
        else
        {
            throw new ArgumentException($"Slider is null or not interactable: {slider?.name ?? "null"}");
        }
    }
    
    #endregion
    
    #region Network Simulation Utilities
    
    /// <summary>
    /// 네트워크 지연을 시뮬레이션합니다.
    /// </summary>
    /// <param name="seconds">지연 시간 (초)</param>
    /// <returns>지연 코루틴</returns>
    public static IEnumerator SimulateNetworkDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }
    
    /// <summary>
    /// 랜덤한 네트워크 지연을 시뮬레이션합니다.
    /// </summary>
    /// <param name="minSeconds">최소 지연 시간</param>
    /// <param name="maxSeconds">최대 지연 시간</param>
    /// <returns>랜덤 지연 코루틴</returns>
    public static IEnumerator SimulateRandomNetworkDelay(float minSeconds, float maxSeconds)
    {
        float delay = UnityEngine.Random.Range(minSeconds, maxSeconds);
        yield return new WaitForSeconds(delay);
    }
    
    #endregion
    
    #region Assertion Helpers
    
    /// <summary>
    /// 게임오브젝트가 활성화 상태인지 확인합니다.
    /// </summary>
    /// <param name="gameObject">확인할 게임오브젝트</param>
    /// <param name="message">실패 시 메시지</param>
    public static void AssertGameObjectActive(GameObject gameObject, string message = null)
    {
        Assert.IsNotNull(gameObject, "GameObject is null");
        Assert.IsTrue(gameObject.activeInHierarchy, 
            message ?? $"GameObject '{gameObject.name}' should be active");
    }
    
    /// <summary>
    /// 게임오브젝트가 비활성화 상태인지 확인합니다.
    /// </summary>
    /// <param name="gameObject">확인할 게임오브젝트</param>
    /// <param name="message">실패 시 메시지</param>
    public static void AssertGameObjectInactive(GameObject gameObject, string message = null)
    {
        Assert.IsNotNull(gameObject, "GameObject is null");
        Assert.IsFalse(gameObject.activeInHierarchy, 
            message ?? $"GameObject '{gameObject.name}' should be inactive");
    }
    
    /// <summary>
    /// 컴포넌트가 존재하는지 확인합니다.
    /// </summary>
    /// <typeparam name="T">컴포넌트 타입</typeparam>
    /// <param name="gameObject">확인할 게임오브젝트</param>
    /// <param name="message">실패 시 메시지</param>
    /// <returns>찾은 컴포넌트</returns>
    public static T AssertComponentExists<T>(GameObject gameObject, string message = null) where T : Component
    {
        Assert.IsNotNull(gameObject, "GameObject is null");
        T component = gameObject.GetComponent<T>();
        Assert.IsNotNull(component, 
            message ?? $"Component '{typeof(T).Name}' not found on '{gameObject.name}'");
        return component;
    }
    
    /// <summary>
    /// UI 컴포넌트가 상호작용 가능한지 확인합니다.
    /// </summary>
    /// <param name="selectable">확인할 UI 컴포넌트</param>
    /// <param name="message">실패 시 메시지</param>
    public static void AssertUIInteractable(Selectable selectable, string message = null)
    {
        Assert.IsNotNull(selectable, "Selectable component is null");
        Assert.IsTrue(selectable.interactable, 
            message ?? $"UI component '{selectable.name}' should be interactable");
    }
    
    /// <summary>
    /// 값이 지정된 범위 내에 있는지 확인합니다.
    /// </summary>
    /// <param name="value">확인할 값</param>
    /// <param name="min">최소값</param>
    /// <param name="max">최대값</param>
    /// <param name="message">실패 시 메시지</param>
    public static void AssertInRange(float value, float min, float max, string message = null)
    {
        Assert.IsTrue(value >= min && value <= max, 
            message ?? $"Value {value} is not in range [{min}, {max}]");
    }
    
    #endregion
    
    #region Test Object Factory
    
    /// <summary>
    /// 테스트용 게임오브젝트를 생성합니다.
    /// </summary>
    /// <param name="name">게임오브젝트 이름</param>
    /// <returns>생성된 게임오브젝트</returns>
    public static GameObject CreateTestGameObject(string name = "TestObject")
    {
        GameObject testObject = new GameObject(name);
        return testObject;
    }
    
    /// <summary>
    /// 컴포넌트가 포함된 테스트용 게임오브젝트를 생성합니다.
    /// </summary>
    /// <typeparam name="T">추가할 컴포넌트 타입</typeparam>
    /// <param name="name">게임오브젝트 이름</param>
    /// <returns>컴포넌트가 추가된 게임오브젝트</returns>
    public static T CreateTestGameObjectWithComponent<T>(string name = null) where T : Component
    {
        string objectName = name ?? $"Test{typeof(T).Name}";
        GameObject testObject = new GameObject(objectName);
        return testObject.AddComponent<T>();
    }
    
    /// <summary>
    /// 테스트용 UI 캔버스를 생성합니다.
    /// </summary>
    /// <returns>생성된 캔버스 게임오브젝트</returns>
    public static GameObject CreateTestCanvas()
    {
        GameObject canvasObject = new GameObject("TestCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }
    
    /// <summary>
    /// 테스트용 버튼을 생성합니다.
    /// </summary>
    /// <param name="parent">부모 오브젝트</param>
    /// <param name="name">버튼 이름</param>
    /// <returns>생성된 버튼</returns>
    public static Button CreateTestButton(Transform parent = null, string name = "TestButton")
    {
        GameObject buttonObject = new GameObject(name);
        if (parent != null)
        {
            buttonObject.transform.SetParent(parent);
        }
        
        Button button = buttonObject.AddComponent<Button>();
        Image image = buttonObject.AddComponent<Image>();
        
        return button;
    }
    
    #endregion
    
    #region Cleanup Utilities
    
    /// <summary>
    /// 게임오브젝트를 안전하게 제거합니다.
    /// </summary>
    /// <param name="gameObject">제거할 게임오브젝트</param>
    public static void SafeDestroy(GameObject gameObject)
    {
        if (gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
    
    /// <summary>
    /// 컴포넌트를 안전하게 제거합니다.
    /// </summary>
    /// <param name="component">제거할 컴포넌트</param>
    public static void SafeDestroy(Component component)
    {
        if (component != null)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }
    
    /// <summary>
    /// 여러 게임오브젝트를 안전하게 제거합니다.
    /// </summary>
    /// <param name="gameObjects">제거할 게임오브젝트 배열</param>
    public static void SafeDestroyMultiple(params GameObject[] gameObjects)
    {
        foreach (GameObject obj in gameObjects)
        {
            SafeDestroy(obj);
        }
    }
    
    /// <summary>
    /// 이벤트 핸들러를 안전하게 제거합니다.
    /// </summary>
    /// <param name="action">제거할 액션</param>
    public static void SafeClearAction(ref Action action)
    {
        action = null;
    }
    
    /// <summary>
    /// 제네릭 이벤트 핸들러를 안전하게 제거합니다.
    /// </summary>
    /// <typeparam name="T">이벤트 파라미터 타입</typeparam>
    /// <param name="action">제거할 액션</param>
    public static void SafeClearAction<T>(ref Action<T> action)
    {
        action = null;
    }
    
    #endregion
    
    #region Performance Measurement Utilities
    
    /// <summary>
    /// 코드 실행 시간을 측정합니다.
    /// </summary>
    /// <param name="action">측정할 액션</param>
    /// <returns>실행 시간 (밀리초)</returns>
    public static double MeasureExecutionTime(Action action)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        action.Invoke();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
    
    /// <summary>
    /// 코루틴 실행 시간을 측정합니다.
    /// </summary>
    /// <param name="coroutine">측정할 코루틴</param>
    /// <returns>실행 시간 측정 코루틴</returns>
    public static IEnumerator MeasureCoroutineExecutionTime(IEnumerator coroutine, System.Action<double> onComplete)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        yield return coroutine;
        stopwatch.Stop();
        onComplete?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
    }
    
    /// <summary>
    /// 가비지 컬렉션을 강제 실행합니다.
    /// </summary>
    public static void ForceGarbageCollection()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }
    
    /// <summary>
    /// 현재 메모리 사용량을 반환합니다.
    /// </summary>
    /// <returns>메모리 사용량 (바이트)</returns>
    public static long GetCurrentMemoryUsage()
    {
        return System.GC.GetTotalMemory(false);
    }
    
    /// <summary>
    /// 가비지 컬렉션 후 메모리 사용량을 반환합니다.
    /// </summary>
    /// <returns>메모리 사용량 (바이트)</returns>
    public static long GetMemoryUsageAfterGC()
    {
        return System.GC.GetTotalMemory(true);
    }
    
    #endregion
    
    #region Validation Utilities
    
    /// <summary>
    /// 성능 요구사항을 검증합니다.
    /// </summary>
    /// <param name="fps">측정된 FPS</param>
    /// <param name="minimumFPS">최소 요구 FPS (기본값: 55)</param>
    /// <param name="message">실패 시 메시지</param>
    public static void ValidatePerformanceRequirements(float fps, float minimumFPS = 55f, string message = null)
    {
        Assert.IsTrue(fps >= minimumFPS, 
            message ?? $"FPS requirement not met: {fps:F1} < {minimumFPS}");
    }
    
    /// <summary>
    /// 메모리 사용량 요구사항을 검증합니다.
    /// </summary>
    /// <param name="memoryUsageMB">메모리 사용량 (MB)</param>
    /// <param name="maxMemoryMB">최대 허용 메모리 (기본값: 30MB)</param>
    /// <param name="message">실패 시 메시지</param>
    public static void ValidateMemoryRequirements(float memoryUsageMB, float maxMemoryMB = 30f, string message = null)
    {
        Assert.IsTrue(memoryUsageMB <= maxMemoryMB, 
            message ?? $"Memory usage requirement not met: {memoryUsageMB:F1}MB > {maxMemoryMB}MB");
    }
    
    /// <summary>
    /// 로딩 시간 요구사항을 검증합니다.
    /// </summary>
    /// <param name="loadingTimeSeconds">로딩 시간 (초)</param>
    /// <param name="maxLoadingTime">최대 허용 로딩 시간 (기본값: 3초)</param>
    /// <param name="message">실패 시 메시지</param>
    public static void ValidateLoadingTimeRequirements(float loadingTimeSeconds, float maxLoadingTime = 3f, string message = null)
    {
        Assert.IsTrue(loadingTimeSeconds <= maxLoadingTime, 
            message ?? $"Loading time requirement not met: {loadingTimeSeconds:F1}s > {maxLoadingTime}s");
    }
    
    #endregion
}