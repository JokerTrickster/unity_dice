using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// LoginFlowStateMachine에 대한 단위 테스트
/// 상태 전환, 이벤트 시스템, 지속성, 오류 처리 테스트를 포함
/// </summary>
public class LoginFlowStateMachineTests
{
    private LoginFlowStateMachine _stateMachine;
    private GameObject _testGameObject;
    
    [SetUp]
    public void SetUp()
    {
        // 기존 인스턴스가 있다면 제거
        if (LoginFlowStateMachine.Instance != null)
        {
            Object.DestroyImmediate(LoginFlowStateMachine.Instance.gameObject);
        }
        
        // 테스트용 StateMachine 생성
        _testGameObject = new GameObject("TestLoginFlowStateMachine");
        _stateMachine = _testGameObject.AddComponent<LoginFlowStateMachine>();
        
        // 이벤트 구독 해제
        LoginFlowStateMachine.OnStateChanged = null;
        LoginFlowStateMachine.OnTransitionFailed = null;
        LoginFlowStateMachine.OnErrorStateEntered = null;
        LoginFlowStateMachine.OnStateRestored = null;
        
        // PlayerPrefs 클리어
        PlayerPrefs.DeleteAll();
    }
    
    [TearDown]
    public void TearDown()
    {
        // 테스트 후 정리
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
        }
        
        // 이벤트 구독 해제
        LoginFlowStateMachine.OnStateChanged = null;
        LoginFlowStateMachine.OnTransitionFailed = null;
        LoginFlowStateMachine.OnErrorStateEntered = null;
        LoginFlowStateMachine.OnStateRestored = null;
        
        // PlayerPrefs 클리어
        PlayerPrefs.DeleteAll();
    }
    
    #region Singleton Tests
    [Test]
    public void Singleton_Instance_ShouldNotBeNull()
    {
        // Arrange & Act
        var instance = LoginFlowStateMachine.Instance;
        
        // Assert
        Assert.IsNotNull(instance);
        Assert.IsTrue(instance is LoginFlowStateMachine);
    }
    
    [Test]
    public void Singleton_MultipleAccess_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var instance1 = LoginFlowStateMachine.Instance;
        var instance2 = LoginFlowStateMachine.Instance;
        
        // Assert
        Assert.AreSame(instance1, instance2);
    }
    #endregion
    
    #region State Transition Tests
    [Test]
    public void ChangeState_ValidTransition_ShouldSucceed()
    {
        // Arrange
        _stateMachine.Start();
        
        // Act
        bool result = _stateMachine.ChangeState(LoginState.Ready);
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
    }
    
    [Test]
    public void ChangeState_InvalidTransition_ShouldFail()
    {
        // Arrange
        _stateMachine.Start();
        
        // Act
        bool result = _stateMachine.ChangeState(LoginState.Complete); // NotInitialized -> Complete는 불가능
        
        // Assert
        Assert.IsFalse(result);
        Assert.AreNotEqual(LoginState.Complete, _stateMachine.CurrentState);
    }
    
    [Test]
    public void ChangeState_FromReadyToAuthenticating_ShouldSucceed()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        bool result = _stateMachine.ChangeState(LoginState.Authenticating);
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LoginState.Authenticating, _stateMachine.CurrentState);
        Assert.AreEqual(LoginState.Ready, _stateMachine.PreviousState);
    }
    
    [Test]
    public void IsValidTransition_ValidPairs_ShouldReturnTrue()
    {
        // Arrange
        _stateMachine.Start();
        
        // Act & Assert
        Assert.IsTrue(_stateMachine.IsValidTransition(LoginState.NotInitialized, LoginState.Initializing));
        Assert.IsTrue(_stateMachine.IsValidTransition(LoginState.Initializing, LoginState.Ready));
        Assert.IsTrue(_stateMachine.IsValidTransition(LoginState.Ready, LoginState.Authenticating));
        Assert.IsTrue(_stateMachine.IsValidTransition(LoginState.Authenticating, LoginState.Success));
        Assert.IsTrue(_stateMachine.IsValidTransition(LoginState.Success, LoginState.Complete));
    }
    
    [Test]
    public void IsValidTransition_InvalidPairs_ShouldReturnFalse()
    {
        // Arrange
        _stateMachine.Start();
        
        // Act & Assert
        Assert.IsFalse(_stateMachine.IsValidTransition(LoginState.NotInitialized, LoginState.Complete));
        Assert.IsFalse(_stateMachine.IsValidTransition(LoginState.Ready, LoginState.Success));
        Assert.IsFalse(_stateMachine.IsValidTransition(LoginState.Complete, LoginState.Authenticating));
    }
    
    [Test]
    public void GetValidNextStates_FromReady_ShouldReturnAuthenticating()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        var validStates = _stateMachine.GetValidNextStates();
        
        // Assert
        Assert.IsTrue(validStates.Contains(LoginState.Authenticating));
        Assert.AreEqual(1, validStates.Count);
    }
    
    [Test]
    public void GetValidNextStates_FromSuccess_ShouldReturnMultipleStates()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        _stateMachine.ChangeState(LoginState.Authenticating);
        _stateMachine.ChangeState(LoginState.Success);
        
        // Act
        var validStates = _stateMachine.GetValidNextStates();
        
        // Assert
        Assert.IsTrue(validStates.Contains(LoginState.Ready));
        Assert.IsTrue(validStates.Contains(LoginState.NicknameSetup));
        Assert.IsTrue(validStates.Contains(LoginState.Complete));
        Assert.AreEqual(3, validStates.Count);
    }
    #endregion
    
    #region Event System Tests
    [Test]
    public void OnStateChanged_ValidTransition_ShouldFireEvent()
    {
        // Arrange
        _stateMachine.Start();
        LoginState fromState = LoginState.NotInitialized;
        LoginState toState = LoginState.NotInitialized;
        bool eventFired = false;
        
        LoginFlowStateMachine.OnStateChanged += (from, to) =>
        {
            fromState = from;
            toState = to;
            eventFired = true;
        };
        
        // Act
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.AreEqual(LoginState.Initializing, fromState);
        Assert.AreEqual(LoginState.Ready, toState);
    }
    
    [Test]
    public void OnTransitionFailed_InvalidTransition_ShouldFireEvent()
    {
        // Arrange
        _stateMachine.Start();
        LoginState fromState = LoginState.NotInitialized;
        LoginState toState = LoginState.NotInitialized;
        string errorMessage = "";
        bool eventFired = false;
        
        LoginFlowStateMachine.OnTransitionFailed += (from, to, error) =>
        {
            fromState = from;
            toState = to;
            errorMessage = error;
            eventFired = true;
        };
        
        // Act
        _stateMachine.ChangeState(LoginState.Complete); // 불가능한 전환
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotEmpty(errorMessage);
    }
    
    [Test]
    public void OnErrorStateEntered_TransitionToError_ShouldFireEvent()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        LoginState errorState = LoginState.NotInitialized;
        string errorMessage = "";
        bool eventFired = false;
        
        LoginFlowStateMachine.OnErrorStateEntered += (state, message) =>
        {
            errorState = state;
            errorMessage = message;
            eventFired = true;
        };
        
        // Act
        _stateMachine.TransitionToError("Test error");
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.AreEqual(LoginState.Error, errorState);
        Assert.AreEqual("Test error", errorMessage);
    }
    #endregion
    
    #region State Persistence Tests
    [Test]
    public void SaveState_ShouldPersistToPlayerPrefs()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        // 상태는 자동으로 저장됨
        
        // Assert - PlayerPrefs에 저장되었는지 확인
        Assert.IsTrue(PlayerPrefs.HasKey("LoginFlowState"));
        Assert.AreEqual((int)LoginState.Ready, PlayerPrefs.GetInt("LoginFlowState"));
    }
    
    [Test]
    public void RestoreState_ValidSavedState_ShouldRestore()
    {
        // Arrange
        PlayerPrefs.SetInt("LoginFlowState", (int)LoginState.Ready);
        PlayerPrefs.Save();
        
        // Act
        _stateMachine.Start();
        _stateMachine.RestoreState();
        
        // Assert
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
    }
    
    [Test]
    public void RestoreState_InvalidState_ShouldNotRestore()
    {
        // Arrange
        PlayerPrefs.SetInt("LoginFlowState", (int)LoginState.Authenticating); // 복원 불가능한 상태
        PlayerPrefs.Save();
        
        // Act
        _stateMachine.Start();
        _stateMachine.RestoreState();
        
        // Assert
        Assert.AreNotEqual(LoginState.Authenticating, _stateMachine.CurrentState);
    }
    
    [Test]
    public void ClearSavedState_ShouldRemovePlayerPrefs()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        _stateMachine.ClearSavedState();
        
        // Assert
        Assert.IsFalse(PlayerPrefs.HasKey("LoginFlowState"));
    }
    #endregion
    
    #region Error Handling Tests
    [Test]
    public void TransitionToError_FromAnyState_ShouldSucceed()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        bool result = _stateMachine.TransitionToError("Test error");
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LoginState.Error, _stateMachine.CurrentState);
    }
    
    [Test]
    public void ResetToReady_FromError_ShouldSucceed()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        _stateMachine.TransitionToError("Test error");
        
        // Act
        bool result = _stateMachine.ResetToReady();
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
    }
    
    [Test]
    public void ResetToReady_FromInvalidState_ShouldUseErrorPath()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        _stateMachine.ChangeState(LoginState.Authenticating);
        
        // Act
        bool result = _stateMachine.ResetToReady();
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
    }
    #endregion
    
    #region Integration Tests
    [UnityTest]
    public IEnumerator StateMachine_Initialize_ShouldCompleteWithoutErrors()
    {
        // Arrange & Act
        _stateMachine.Start();
        
        // Wait for initialization
        yield return new WaitForSeconds(0.1f);
        
        // Assert
        Assert.IsTrue(_stateMachine.IsInitialized);
        Assert.AreNotEqual(LoginState.NotInitialized, _stateMachine.CurrentState);
    }
    
    [Test]
    public void FullLoginFlow_ValidTransitions_ShouldCompleteSuccessfully()
    {
        // Arrange
        _stateMachine.Start();
        
        // Act & Assert - 전체 로그인 플로우 시뮬레이션
        Assert.IsTrue(_stateMachine.ChangeState(LoginState.Ready));
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
        
        Assert.IsTrue(_stateMachine.ChangeState(LoginState.Authenticating));
        Assert.AreEqual(LoginState.Authenticating, _stateMachine.CurrentState);
        
        Assert.IsTrue(_stateMachine.ChangeState(LoginState.Success));
        // Success에서 자동으로 NicknameSetup 또는 Complete로 전환될 수 있음
        Assert.IsTrue(_stateMachine.CurrentState == LoginState.Success || 
                     _stateMachine.CurrentState == LoginState.NicknameSetup ||
                     _stateMachine.CurrentState == LoginState.Complete);
    }
    
    [Test]
    public void GetStateDescription_AllStates_ShouldReturnValidDescriptions()
    {
        // Arrange
        _stateMachine.Start();
        var allStates = System.Enum.GetValues(typeof(LoginState));
        
        // Act & Assert
        foreach (LoginState state in allStates)
        {
            _stateMachine.ChangeState(LoginState.Error); // 모든 상태로 전환 가능하도록
            _stateMachine.ChangeState(state);
            
            string description = _stateMachine.GetStateDescription();
            Assert.IsNotEmpty(description);
            Assert.AreNotEqual("알 수 없는 상태", description);
        }
    }
    #endregion
    
    #region State Manager Tests
    [Test]
    public void StateManager_SaveAndLoad_ShouldWork()
    {
        // Arrange
        var stateManager = new StateManager();
        
        // Act
        stateManager.SaveState(LoginState.Ready, LoginState.Initializing);
        var loadedState = stateManager.LoadState();
        
        // Assert
        Assert.IsTrue(loadedState.HasValue);
        Assert.AreEqual(LoginState.Ready, loadedState.Value);
    }
    
    [Test]
    public void StateManager_LoadWithoutSave_ShouldReturnNull()
    {
        // Arrange
        var stateManager = new StateManager();
        
        // Act
        var loadedState = stateManager.LoadState();
        
        // Assert
        Assert.IsFalse(loadedState.HasValue);
    }
    
    [Test]
    public void StateManager_ClearSavedState_ShouldRemoveData()
    {
        // Arrange
        var stateManager = new StateManager();
        stateManager.SaveState(LoginState.Ready, LoginState.Initializing);
        
        // Act
        stateManager.ClearSavedState();
        var loadedState = stateManager.LoadState();
        
        // Assert
        Assert.IsFalse(loadedState.HasValue);
    }
    #endregion
    
    #region Edge Cases
    [Test]
    public void ChangeState_BeforeInitialization_ShouldFail()
    {
        // Arrange - Start()를 호출하지 않음
        
        // Act
        bool result = _stateMachine.ChangeState(LoginState.Ready);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [Test]
    public void ChangeState_SameState_ShouldSucceed()
    {
        // Arrange
        _stateMachine.Start();
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Act
        bool result = _stateMachine.ChangeState(LoginState.Ready);
        
        // Assert
        Assert.IsTrue(result); // 같은 상태로의 전환은 허용
        Assert.AreEqual(LoginState.Ready, _stateMachine.CurrentState);
    }
    
    [Test]
    public void MultipleEventHandlers_ShouldAllBeInvoked()
    {
        // Arrange
        _stateMachine.Start();
        int eventCount = 0;
        
        LoginFlowStateMachine.OnStateChanged += (from, to) => eventCount++;
        LoginFlowStateMachine.OnStateChanged += (from, to) => eventCount++;
        LoginFlowStateMachine.OnStateChanged += (from, to) => eventCount++;
        
        // Act
        _stateMachine.ChangeState(LoginState.Ready);
        
        // Assert
        Assert.AreEqual(3, eventCount);
    }
    #endregion
}