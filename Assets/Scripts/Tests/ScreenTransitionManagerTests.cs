using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// ScreenTransitionManager에 대한 단위 테스트
/// 화면 전환, 상태 통합, 애니메이션, 오류 처리 테스트를 포함
/// </summary>
public class ScreenTransitionManagerTests
{
    private ScreenTransitionManager _transitionManager;
    private GameObject _testGameObject;
    private Dictionary<ScreenType, Canvas> _testCanvases;
    
    [SetUp]
    public void SetUp()
    {
        // 기존 인스턴스가 있다면 제거
        if (ScreenTransitionManager.Instance != null)
        {
            Object.DestroyImmediate(ScreenTransitionManager.Instance.gameObject);
        }
        
        // 테스트용 TransitionManager 생성
        _testGameObject = new GameObject("TestScreenTransitionManager");
        _transitionManager = _testGameObject.AddComponent<ScreenTransitionManager>();
        
        // 테스트용 캔버스들 생성
        SetupTestCanvases();
        
        // 이벤트 구독 해제
        ScreenTransitionManager.OnScreenTransitionStarted = null;
        ScreenTransitionManager.OnScreenTransitionCompleted = null;
        ScreenTransitionManager.OnScreenTransitionFailed = null;
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // 테스트 캔버스들 정리
        CleanupTestCanvases();
        
        // 이벤트 구독 해제
        ScreenTransitionManager.OnScreenTransitionStarted = null;
        ScreenTransitionManager.OnScreenTransitionCompleted = null;
        ScreenTransitionManager.OnScreenTransitionFailed = null;
    }
    
    private void SetupTestCanvases()
    {
        _testCanvases = new Dictionary<ScreenType, Canvas>();
        
        // 각 화면 타입별로 테스트 캔버스 생성
        var screenTypes = new[] { ScreenType.Splash, ScreenType.Login, ScreenType.NicknameSetup, 
                                 ScreenType.MainMenu, ScreenType.Error, ScreenType.Loading };
        
        foreach (var screenType in screenTypes)
        {
            GameObject canvasGO = new GameObject($"{screenType}Screen");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            _testCanvases[screenType] = canvas;
            
            // TransitionManager에 등록
            _transitionManager.RegisterScreen(screenType, canvas);
        }
    }
    
    private void CleanupTestCanvases()
    {
        if (_testCanvases != null)
        {
            foreach (var canvas in _testCanvases.Values)
            {
                if (canvas != null)
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
            }
            _testCanvases.Clear();
        }
    }
    
    #region Singleton Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = ScreenTransitionManager.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is ScreenTransitionManager);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = ScreenTransitionManager.Instance;
        var instance2 = ScreenTransitionManager.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region Screen Management Tests
    [Test]
    public void ShowScreen_ValidScreen_ShouldActivateCanvas()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.AreEqual(ScreenType.Login, _transitionManager.CurrentScreen);
        Assert.IsTrue(_testCanvases[ScreenType.Login].gameObject.activeInHierarchy);
    }
    
    [Test]
    public void ShowScreen_DeactivatesPreviousScreen()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.ShowScreen(ScreenType.Splash, false);
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.IsFalse(_testCanvases[ScreenType.Splash].gameObject.activeInHierarchy);
        Assert.IsTrue(_testCanvases[ScreenType.Login].gameObject.activeInHierarchy);
    }
    
    [Test]
    public void ShowScreen_UpdatesPreviousScreen()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.ShowScreen(ScreenType.Splash, false);
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.AreEqual(ScreenType.Splash, _transitionManager.PreviousScreen);
        Assert.AreEqual(ScreenType.Login, _transitionManager.CurrentScreen);
    }
    
    [Test]
    public void ShowPreviousScreen_ShouldReturnToPreviousScreen()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.ShowScreen(ScreenType.Splash, false);
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Act
        _transitionManager.ShowPreviousScreen();
        
        // Assert
        Assert.AreEqual(ScreenType.Splash, _transitionManager.CurrentScreen);
    }
    
    [Test]
    public void IsScreenActive_ActiveScreen_ShouldReturnTrue()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Act & Assert
        Assert.IsTrue(_transitionManager.IsScreenActive(ScreenType.Login));
        Assert.IsFalse(_transitionManager.IsScreenActive(ScreenType.Splash));
    }
    
    [Test]
    public void GetScreenCanvas_ValidScreen_ShouldReturnCanvas()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act
        Canvas canvas = _transitionManager.GetScreenCanvas(ScreenType.Login);
        
        // Assert
        Assert.IsNotNull(canvas);
        Assert.AreSame(_testCanvases[ScreenType.Login], canvas);
    }
    
    [Test]
    public void GetScreenCanvas_InvalidScreen_ShouldReturnNull()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act
        Canvas canvas = _transitionManager.GetScreenCanvas((ScreenType)999); // 존재하지 않는 화면
        
        // Assert
        Assert.IsNull(canvas);
    }
    #endregion
    
    #region Registration Tests
    [Test]
    public void RegisterScreen_NewScreen_ShouldAddToCollection()
    {
        // Arrange
        _transitionManager.Start();
        GameObject newCanvasGO = new GameObject("NewScreen");
        Canvas newCanvas = newCanvasGO.AddComponent<Canvas>();
        
        // Act
        _transitionManager.RegisterScreen((ScreenType)999, newCanvas);
        
        // Assert
        Canvas retrievedCanvas = _transitionManager.GetScreenCanvas((ScreenType)999);
        Assert.IsNotNull(retrievedCanvas);
        Assert.AreSame(newCanvas, retrievedCanvas);
        
        // Cleanup
        Object.DestroyImmediate(newCanvasGO);
    }
    
    [Test]
    public void UnregisterScreen_ExistingScreen_ShouldRemoveFromCollection()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act
        _transitionManager.UnregisterScreen(ScreenType.Login);
        
        // Assert
        Canvas canvas = _transitionManager.GetScreenCanvas(ScreenType.Login);
        Assert.IsNull(canvas);
    }
    
    [Test]
    public void RegisterScreen_NullCanvas_ShouldNotCrash()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act & Assert - 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _transitionManager.RegisterScreen(ScreenType.Loading, null);
        });
    }
    #endregion
    
    #region Event System Tests
    [Test]
    public void ShowScreen_ShouldFireTransitionCompletedEvent()
    {
        // Arrange
        _transitionManager.Start();
        ScreenType completedScreen = ScreenType.None;
        bool eventFired = false;
        
        ScreenTransitionManager.OnScreenTransitionCompleted += (screen) =>
        {
            completedScreen = screen;
            eventFired = true;
        };
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.AreEqual(ScreenType.Login, completedScreen);
    }
    
    [UnityTest]
    public IEnumerator ShowScreen_WithAnimation_ShouldFireStartAndCompleteEvents()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.UpdateTransitionSettings(true, 0.1f); // 빠른 애니메이션
        
        bool startEventFired = false;
        bool completeEventFired = false;
        ScreenType fromScreen = ScreenType.None;
        ScreenType toScreen = ScreenType.None;
        
        ScreenTransitionManager.OnScreenTransitionStarted += (from, to) =>
        {
            fromScreen = from;
            toScreen = to;
            startEventFired = true;
        };
        
        ScreenTransitionManager.OnScreenTransitionCompleted += (screen) =>
        {
            completeEventFired = true;
        };
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, true);
        
        // Wait for animation to complete
        yield return new WaitForSeconds(0.2f);
        
        // Assert
        Assert.IsTrue(startEventFired);
        Assert.IsTrue(completeEventFired);
        Assert.AreEqual(ScreenType.Login, toScreen);
    }
    
    [Test]
    public void ShowScreen_UnknownScreen_ShouldFireFailedEvent()
    {
        // Arrange
        _transitionManager.Start();
        ScreenType failedScreen = ScreenType.None;
        string errorMessage = "";
        bool eventFired = false;
        
        ScreenTransitionManager.OnScreenTransitionFailed += (screen, error) =>
        {
            failedScreen = screen;
            errorMessage = error;
            eventFired = true;
        };
        
        // Act
        _transitionManager.ShowScreen((ScreenType)999, false); // 등록되지 않은 화면
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.AreEqual((ScreenType)999, failedScreen);
        Assert.IsNotEmpty(errorMessage);
    }
    #endregion
    
    #region Animation Tests
    [Test]
    public void UpdateTransitionSettings_ShouldUpdateProperties()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act
        _transitionManager.UpdateTransitionSettings(false, 0.5f);
        
        // Assert - 설정이 업데이트되었는지는 내부 동작으로만 확인 가능
        // 애니메이션 없이 즉시 전환되는지 확인
        _transitionManager.ShowScreen(ScreenType.Login, true);
        Assert.IsFalse(_transitionManager.IsTransitioning); // 즉시 완료되어야 함
    }
    
    [UnityTest]
    public IEnumerator ShowScreen_WithAnimation_ShouldSetTransitioningFlag()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.UpdateTransitionSettings(true, 0.1f);
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, true);
        
        // Assert - 전환 중일 때
        Assert.IsTrue(_transitionManager.IsTransitioning);
        
        // Wait for completion
        yield return new WaitForSeconds(0.2f);
        
        // Assert - 전환 완료 후
        Assert.IsFalse(_transitionManager.IsTransitioning);
    }
    
    [Test]
    public void ShowScreen_DuringTransition_ShouldBeIgnored()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.UpdateTransitionSettings(true, 1f); // 긴 애니메이션
        _transitionManager.ShowScreen(ScreenType.Login, true);
        
        // Act
        _transitionManager.ShowScreen(ScreenType.MainMenu, true);
        
        // Assert - 두 번째 요청은 무시되어야 함
        Assert.AreNotEqual(ScreenType.MainMenu, _transitionManager.CurrentScreen);
    }
    #endregion
    
    #region Integration Tests
    [Test]
    public void Integration_WithLoginStateMachine_ShouldRespondToStateChanges()
    {
        // Arrange
        _transitionManager.Start();
        
        // LoginFlowStateMachine 인스턴스 생성
        var stateMachineGO = new GameObject("TestStateMachine");
        var stateMachine = stateMachineGO.AddComponent<LoginFlowStateMachine>();
        stateMachine.Start();
        
        // Act - 상태 변경
        stateMachine.ChangeState(LoginState.Ready);
        
        // Assert - 해당하는 화면으로 전환되었는지 확인
        Assert.AreEqual(ScreenType.Login, _transitionManager.CurrentScreen);
        
        // Cleanup
        Object.DestroyImmediate(stateMachineGO);
    }
    
    [UnityTest]
    public IEnumerator FullScreenFlow_ShouldTransitionCorrectly()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act & Assert - 전체 화면 플로우 시뮬레이션
        _transitionManager.ShowScreen(ScreenType.Splash, false);
        Assert.AreEqual(ScreenType.Splash, _transitionManager.CurrentScreen);
        
        yield return new WaitForSeconds(0.1f);
        
        _transitionManager.ShowScreen(ScreenType.Login, false);
        Assert.AreEqual(ScreenType.Login, _transitionManager.CurrentScreen);
        
        _transitionManager.ShowScreen(ScreenType.Loading, false);
        Assert.AreEqual(ScreenType.Loading, _transitionManager.CurrentScreen);
        
        _transitionManager.ShowScreen(ScreenType.NicknameSetup, false);
        Assert.AreEqual(ScreenType.NicknameSetup, _transitionManager.CurrentScreen);
        
        _transitionManager.ShowScreen(ScreenType.MainMenu, false);
        Assert.AreEqual(ScreenType.MainMenu, _transitionManager.CurrentScreen);
    }
    #endregion
    
    #region Edge Cases
    [Test]
    public void ShowPreviousScreen_NoPreviousScreen_ShouldNotCrash()
    {
        // Arrange
        _transitionManager.Start();
        
        // Act & Assert - 이전 화면이 없을 때 예외가 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            _transitionManager.ShowPreviousScreen();
        });
    }
    
    [Test]
    public void ShowScreen_SameScreen_ShouldStillWork()
    {
        // Arrange
        _transitionManager.Start();
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.AreEqual(ScreenType.Login, _transitionManager.CurrentScreen);
    }
    
    [Test]
    public void MultipleEventHandlers_ShouldAllBeInvoked()
    {
        // Arrange
        _transitionManager.Start();
        int eventCount = 0;
        
        ScreenTransitionManager.OnScreenTransitionCompleted += (screen) => eventCount++;
        ScreenTransitionManager.OnScreenTransitionCompleted += (screen) => eventCount++;
        ScreenTransitionManager.OnScreenTransitionCompleted += (screen) => eventCount++;
        
        // Act
        _transitionManager.ShowScreen(ScreenType.Login, false);
        
        // Assert
        Assert.AreEqual(3, eventCount);
    }
    
    [Test]
    public void ScreenTransition_WithoutInitialization_ShouldStillWork()
    {
        // Arrange - Start()를 호출하지 않음
        
        // Act & Assert - 기본적인 기능은 동작해야 함
        Assert.DoesNotThrow(() =>
        {
            _transitionManager.ShowScreen(ScreenType.Login, false);
        });
    }
    #endregion
}