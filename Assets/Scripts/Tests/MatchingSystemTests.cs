using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// 매칭 시스템 통합 테스트
/// 전체 매칭 플로우와 시스템 통합을 종합적으로 검증합니다.
/// Stream D: Integration & Testing의 핵심 테스트 클래스입니다.
/// </summary>
[Category("Integration")]
[Category("Matching")]
[Category("System")]
public class MatchingSystemTests
{
    #region Test Components
    private GameObject _testGameObject;
    private MatchingNetworkHandler _networkHandler;
    private IntegratedMatchingUI _matchingUI;
    private EnergyMatchingIntegration _energyIntegration;
    private UserDataManager _userDataManager;
    private EnergyManager _energyManager;
    private NetworkManager _networkManager;
    
    // Test doubles and mocks
    private TestWebSocketClient _testWebSocket;
    private TestMatchingServer _testServer;
    #endregion

    #region Test Data
    private readonly string _testPlayerId = "test-player-123";
    private readonly string _testRoomCode = "TEST123";
    private const int DefaultPlayerCount = 2;
    private const int DefaultEnergyAmount = 10;
    private const float NetworkTimeoutSeconds = 5f;
    private const float UIResponseTimeSeconds = 2f;
    
    // Performance benchmarks
    private const int TargetFPS = 60;
    private const float MaxMemoryIncreaseMB = 5f;
    private const float MaxMatchingResponseTime = 2f;
    private const float MaxCancellationTime = 1f;
    #endregion

    #region Setup & Teardown
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // 테스트 환경 전역 설정
        Debug.Log("[MatchingSystemTests] Starting integration test suite");
    }

    [SetUp]
    public void SetUp()
    {
        // 각 테스트마다 깨끗한 환경 구성
        SetupTestEnvironment();
        InitializeTestComponents();
        ConfigureTestDependencies();
        
        Debug.Log($"[MatchingSystemTests] Test setup completed for: {TestContext.CurrentContext.Test.Name}");
    }

    [TearDown]
    public void TearDown()
    {
        CleanupTestEnvironment();
        Debug.Log($"[MatchingSystemTests] Test cleanup completed for: {TestContext.CurrentContext.Test.Name}");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("[MatchingSystemTests] Integration test suite completed");
    }

    private void SetupTestEnvironment()
    {
        _testGameObject = new GameObject("MatchingSystemTest");
        
        // 테스트용 WebSocket 서버 설정
        _testServer = new TestMatchingServer();
        _testWebSocket = new TestWebSocketClient();
        _testWebSocket.ConnectToTestServer(_testServer);
    }

    private void InitializeTestComponents()
    {
        // 핵심 매니저들 초기화
        _userDataManager = _testGameObject.AddComponent<UserDataManager>();
        _energyManager = _testGameObject.AddComponent<EnergyManager>();
        _networkManager = _testGameObject.AddComponent<NetworkManager>();
        
        // 매칭 관련 컴포넌트들 초기화
        _networkHandler = _testGameObject.AddComponent<MatchingNetworkHandler>();
        _matchingUI = _testGameObject.AddComponent<IntegratedMatchingUI>();
        _energyIntegration = _testGameObject.AddComponent<EnergyMatchingIntegration>();
    }

    private void ConfigureTestDependencies()
    {
        // 테스트 사용자 데이터 설정
        _userDataManager.InitializeWithTestData(new UserData
        {
            UserId = _testPlayerId,
            DisplayName = "Test Player",
            Level = 5,
            CurrentEnergy = DefaultEnergyAmount,
            MaxEnergy = 100
        });

        // 에너지 매니저 초기화
        _energyManager.InitializeForTesting(DefaultEnergyAmount, 100);

        // 네트워크 매니저에 테스트 WebSocket 연결
        _networkManager.SetTestWebSocketClient(_testWebSocket);
    }

    private void CleanupTestEnvironment()
    {
        if (_testServer != null)
        {
            _testServer.Dispose();
            _testServer = null;
        }

        if (_testWebSocket != null)
        {
            _testWebSocket.Dispose();
            _testWebSocket = null;
        }

        if (_testGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_testGameObject);
            _testGameObject = null;
        }
    }
    #endregion

    #region End-to-End Integration Tests
    [UnityTest]
    [Description("전체 매칭 플로우 통합 테스트 - UI에서 WebSocket까지")]
    public IEnumerator Test_EndToEnd_MatchingFlow_Success()
    {
        // Arrange
        bool matchingCompleted = false;
        MatchingResponse receivedResponse = null;
        var startTime = Time.time;

        _networkHandler.OnMatchingResponse += (response) =>
        {
            receivedResponse = response;
            matchingCompleted = true;
        };

        _testServer.SetMatchingResult(true, "Match found successfully", 
            new[] { _testPlayerId, "player2" });

        // Act - UI에서 매칭 요청 시작
        _matchingUI.SetMatchingState(MatchingState.Idle);
        
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount, "classic");
        bool requestSent = yield return StartCoroutineAndWait(
            SendMatchingRequestCoroutine(matchingRequest)
        );

        // 매칭 응답 대기
        yield return new WaitUntil(() => matchingCompleted || Time.time - startTime > MaxMatchingResponseTime);

        // Assert
        var responseTime = Time.time - startTime;
        
        Assert.IsTrue(requestSent, "매칭 요청이 성공적으로 전송되어야 함");
        Assert.IsTrue(matchingCompleted, "매칭이 완료되어야 함");
        Assert.IsNotNull(receivedResponse, "매칭 응답을 받아야 함");
        Assert.IsTrue(receivedResponse.Success, "매칭이 성공해야 함");
        Assert.That(responseTime, Is.LessThan(MaxMatchingResponseTime), 
            $"매칭 응답 시간이 {MaxMatchingResponseTime}초 이내여야 함");

        Debug.Log($"[MatchingSystemTests] End-to-end matching completed in {responseTime:F2}s");
    }

    [UnityTest]
    [Description("매칭 실패 시나리오 통합 테스트")]
    public IEnumerator Test_EndToEnd_MatchingFlow_Failure()
    {
        // Arrange
        bool matchingCompleted = false;
        MatchingResponse receivedResponse = null;

        _networkHandler.OnMatchingResponse += (response) =>
        {
            receivedResponse = response;
            matchingCompleted = true;
        };

        _testServer.SetMatchingResult(false, "No players available", null);

        // Act
        var matchingRequest = new MatchingRequest(_testPlayerId, 4, "ranked"); // 4명 랭크 매칭 (어려운 조건)
        bool requestSent = yield return StartCoroutineAndWait(
            SendMatchingRequestCoroutine(matchingRequest)
        );

        yield return new WaitUntil(() => matchingCompleted || Time.time > 3f);

        // Assert
        Assert.IsTrue(requestSent, "매칭 요청이 전송되어야 함");
        Assert.IsTrue(matchingCompleted, "매칭 실패 응답을 받아야 함");
        Assert.IsNotNull(receivedResponse, "실패 응답을 받아야 함");
        Assert.IsFalse(receivedResponse.Success, "매칭이 실패해야 함");
        Assert.IsTrue(receivedResponse.ErrorMessage.Contains("No players"), "적절한 에러 메시지를 받아야 함");
    }

    [UnityTest]
    [Description("매칭 취소 시나리오 통합 테스트")]
    public IEnumerator Test_EndToEnd_MatchingCancellation()
    {
        // Arrange
        var startTime = Time.time;
        bool cancellationCompleted = false;
        
        _networkHandler.OnMatchingCancelled += (playerId) =>
        {
            cancellationCompleted = true;
        };

        // 매칭 시작
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount);
        yield return StartCoroutineAndWait(SendMatchingRequestCoroutine(matchingRequest));
        
        _matchingUI.SetMatchingState(MatchingState.Searching);

        // Act - 0.5초 후 매칭 취소
        yield return new WaitForSeconds(0.5f);
        
        bool cancelSent = yield return StartCoroutineAndWait(
            SendMatchingCancelCoroutine(_testPlayerId)
        );

        yield return new WaitUntil(() => cancellationCompleted || Time.time - startTime > MaxCancellationTime);

        // Assert
        var cancellationTime = Time.time - startTime - 0.5f; // 취소 요청 후 실제 소요 시간
        
        Assert.IsTrue(cancelSent, "취소 요청이 전송되어야 함");
        Assert.IsTrue(cancellationCompleted, "취소가 완료되어야 함");
        Assert.That(cancellationTime, Is.LessThan(MaxCancellationTime), 
            $"매칭 취소가 {MaxCancellationTime}초 이내에 완료되어야 함");
        
        Debug.Log($"[MatchingSystemTests] Matching cancelled in {cancellationTime:F2}s");
    }
    #endregion

    #region Energy Integration Tests
    [UnityTest]
    [Description("에너지 부족 시 매칭 차단 테스트")]
    public IEnumerator Test_EnergyIntegration_InsufficientEnergy()
    {
        // Arrange - 에너지를 0으로 설정
        _energyManager.SetEnergyForTesting(0);
        
        bool energyValidationCalled = false;
        bool matchingBlocked = false;
        string validationMessage = "";

        _energyIntegration.OnEnergyValidationComplete += (isValid, message) =>
        {
            energyValidationCalled = true;
            matchingBlocked = !isValid;
            validationMessage = message;
        };

        // Act - 매칭 요청 시도
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount);
        
        // UI 레벨에서 매칭 요청 (에너지 검증이 먼저 수행됨)
        _matchingUI.OnMatchingRequested?.Invoke(matchingRequest);

        yield return new WaitForSeconds(0.1f); // 검증 완료 대기

        // Assert
        Assert.IsTrue(energyValidationCalled, "에너지 검증이 호출되어야 함");
        Assert.IsTrue(matchingBlocked, "에너지 부족으로 매칭이 차단되어야 함");
        Assert.IsTrue(validationMessage.Contains("부족"), "적절한 에너지 부족 메시지를 받아야 함");

        Debug.Log($"[MatchingSystemTests] Energy validation blocked matching: {validationMessage}");
    }

    [UnityTest]
    [Description("매칭 시작 시 에너지 소모 테스트")]
    public IEnumerator Test_EnergyIntegration_EnergyConsumption()
    {
        // Arrange
        const int initialEnergy = 5;
        const int requiredEnergy = 1;
        
        _energyManager.SetEnergyForTesting(initialEnergy);
        _energyIntegration.SetRequiredEnergy(requiredEnergy);

        bool energyConsumed = false;
        int previousEnergy = 0;
        int currentEnergy = 0;

        _energyIntegration.OnEnergyConsumed += (prev, curr) =>
        {
            energyConsumed = true;
            previousEnergy = prev;
            currentEnergy = curr;
        };

        // Act - 매칭 요청
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount);
        _matchingUI.OnMatchingRequested?.Invoke(matchingRequest);

        yield return new WaitForSeconds(0.2f); // 처리 대기

        // Assert
        Assert.IsTrue(energyConsumed, "에너지가 소모되어야 함");
        Assert.AreEqual(initialEnergy, previousEnergy, "이전 에너지 값이 정확해야 함");
        Assert.AreEqual(initialEnergy - requiredEnergy, currentEnergy, "현재 에너지가 정확하게 감소해야 함");

        Debug.Log($"[MatchingSystemTests] Energy consumed: {previousEnergy} → {currentEnergy}");
    }

    [UnityTest]
    [Description("매칭 취소 시 에너지 복원 테스트")]
    public IEnumerator Test_EnergyIntegration_EnergyRestoration()
    {
        // Arrange - 먼저 에너지 소모
        _energyManager.SetEnergyForTesting(5);
        _energyIntegration.ConsumeEnergyForMatching(1);
        
        bool energyRestored = false;
        int restoredFromEnergy = 0;
        int restoredToEnergy = 0;

        _energyIntegration.OnEnergyRestored += (prev, curr) =>
        {
            energyRestored = true;
            restoredFromEnergy = prev;
            restoredToEnergy = curr;
        };

        // Act - 매칭 취소
        _matchingUI.OnMatchCancelRequested?.Invoke();

        yield return new WaitForSeconds(0.1f); // 처리 대기

        // Assert
        Assert.IsTrue(energyRestored, "에너지가 복원되어야 함");
        Assert.Greater(restoredToEnergy, restoredFromEnergy, "에너지가 증가해야 함");

        Debug.Log($"[MatchingSystemTests] Energy restored: {restoredFromEnergy} → {restoredToEnergy}");
    }
    #endregion

    #region State Transition Tests
    [UnityTest]
    [Description("매칭 상태 전환 시퀀스 테스트")]
    public IEnumerator Test_StateTransition_MatchingSequence()
    {
        // Arrange
        var stateHistory = new List<MatchingState>();
        
        // UI 상태 변경 추적
        _matchingUI.OnMatchingRequested += (request) => stateHistory.Add(MatchingState.Searching);

        _testServer.SetMatchingResult(true, "Match found", new[] { _testPlayerId, "player2" });

        // Act - 매칭 플로우 시작
        stateHistory.Add(MatchingState.Idle);
        
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount);
        yield return StartCoroutineAndWait(SendMatchingRequestCoroutine(matchingRequest));

        // 매칭 완료까지 대기
        yield return new WaitForSeconds(1f);
        stateHistory.Add(MatchingState.Found);

        // Assert - 상태 전환 시퀀스 검증
        var expectedSequence = new[] { MatchingState.Idle, MatchingState.Searching, MatchingState.Found };
        
        Assert.GreaterOrEqual(stateHistory.Count, expectedSequence.Length, "최소 상태 개수가 있어야 함");
        
        for (int i = 0; i < expectedSequence.Length; i++)
        {
            Assert.AreEqual(expectedSequence[i], stateHistory[i], 
                $"상태 전환 시퀀스 {i}번째가 {expectedSequence[i]}이어야 함");
        }

        Debug.Log($"[MatchingSystemTests] State transition sequence verified: {string.Join(" → ", stateHistory)}");
    }

    [UnityTest]
    [Description("네트워크 연결 끊김 중 매칭 처리 테스트")]
    public IEnumerator Test_StateTransition_NetworkDisconnection()
    {
        // Arrange
        bool networkErrorDetected = false;
        string errorCode = "";
        string errorMessage = "";

        _networkHandler.OnNetworkError += (code, message) =>
        {
            networkErrorDetected = true;
            errorCode = code;
            errorMessage = message;
        };

        // 매칭 시작
        var matchingRequest = new MatchingRequest(_testPlayerId, DefaultPlayerCount);
        yield return StartCoroutineAndWait(SendMatchingRequestCoroutine(matchingRequest));

        // Act - 네트워크 연결 끊기
        _testWebSocket.SimulateDisconnection();

        yield return new WaitForSeconds(2f); // 재연결 시도 대기

        // Assert
        Assert.IsTrue(networkErrorDetected, "네트워크 에러가 감지되어야 함");
        Assert.IsNotEmpty(errorCode, "에러 코드가 있어야 함");
        Assert.IsNotEmpty(errorMessage, "에러 메시지가 있어야 함");

        Debug.Log($"[MatchingSystemTests] Network disconnection handled: {errorCode} - {errorMessage}");
    }
    #endregion

    #region Performance Tests
    [UnityTest]
    [Description("UI 성능 - 60FPS 유지 테스트")]
    public IEnumerator Test_Performance_UIFramerate()
    {
        // Arrange
        var frameTimes = new List<float>();
        const int sampleCount = 120; // 2초간 샘플링
        
        // 매칭 UI 활성 상태 설정
        _matchingUI.SetMatchingState(MatchingState.Searching);

        // Act - 프레임 시간 측정
        for (int i = 0; i < sampleCount; i++)
        {
            float frameStart = Time.realtimeSinceStartup;
            yield return null; // 다음 프레임까지 대기
            float frameTime = Time.realtimeSinceStartup - frameStart;
            frameTimes.Add(frameTime);
        }

        // Assert
        float averageFrameTime = frameTimes.Average();
        float averageFPS = 1f / averageFrameTime;
        float minFPS = 1f / frameTimes.Max();

        Assert.Greater(averageFPS, TargetFPS * 0.9f, $"평균 FPS가 {TargetFPS * 0.9f} 이상이어야 함");
        Assert.Greater(minFPS, TargetFPS * 0.8f, $"최소 FPS가 {TargetFPS * 0.8f} 이상이어야 함");

        Debug.Log($"[MatchingSystemTests] UI Performance - Avg FPS: {averageFPS:F1}, Min FPS: {minFPS:F1}");
    }

    [UnityTest]
    [Description("메모리 사용량 테스트")]
    public IEnumerator Test_Performance_MemoryUsage()
    {
        // Arrange
        System.GC.Collect();
        yield return null;
        
        long initialMemory = System.GC.GetTotalMemory(false);

        // Act - 매칭 시스템 집중 사용
        for (int i = 0; i < 10; i++)
        {
            var request = new MatchingRequest($"player-{i}", DefaultPlayerCount);
            yield return StartCoroutineAndWait(SendMatchingRequestCoroutine(request));
            
            yield return StartCoroutineAndWait(SendMatchingCancelCoroutine($"player-{i}"));
            yield return new WaitForSeconds(0.1f);
        }

        // Assert
        System.GC.Collect();
        yield return null;
        
        long finalMemory = System.GC.GetTotalMemory(false);
        long memoryIncrease = finalMemory - initialMemory;
        float memoryIncreaseMB = memoryIncrease / (1024f * 1024f);

        Assert.That(memoryIncreaseMB, Is.LessThan(MaxMemoryIncreaseMB), 
            $"메모리 증가량이 {MaxMemoryIncreaseMB}MB 이하여야 함");

        Debug.Log($"[MatchingSystemTests] Memory increase: {memoryIncreaseMB:F2}MB");
    }
    #endregion

    #region Helper Methods
    private IEnumerator SendMatchingRequestCoroutine(MatchingRequest request)
    {
        var task = _networkHandler.SendJoinQueueRequestAsync(
            request.playerId, 
            request.playerCount, 
            request.gameMode, 
            request.betAmount
        );

        yield return new WaitUntil(() => task.IsCompleted);
        yield return task.Result;
    }

    private IEnumerator SendMatchingCancelCoroutine(string playerId)
    {
        var task = _networkHandler.SendMatchingCancelRequestAsync(playerId);
        yield return new WaitUntil(() => task.IsCompleted);
        yield return task.Result;
    }

    private Coroutine StartCoroutineAndWait<T>(IEnumerator coroutine)
    {
        return _testGameObject.GetComponent<MonoBehaviour>().StartCoroutine(coroutine);
    }
    #endregion

    #region Test Doubles
    /// <summary>
    /// 테스트용 WebSocket 클라이언트
    /// </summary>
    private class TestWebSocketClient : IDisposable
    {
        private TestMatchingServer _server;
        private bool _isConnected = true;

        public void ConnectToTestServer(TestMatchingServer server)
        {
            _server = server;
        }

        public void SimulateDisconnection()
        {
            _isConnected = false;
        }

        public bool SendMessage(string message)
        {
            return _isConnected && _server?.ProcessMessage(message) == true;
        }

        public void Dispose()
        {
            _server = null;
        }
    }

    /// <summary>
    /// 테스트용 매칭 서버
    /// </summary>
    private class TestMatchingServer : IDisposable
    {
        private bool _shouldSucceed = true;
        private string _successMessage = "Match found";
        private string[] _matchedPlayers;

        public void SetMatchingResult(bool success, string message, string[] players)
        {
            _shouldSucceed = success;
            _successMessage = message;
            _matchedPlayers = players;
        }

        public bool ProcessMessage(string message)
        {
            // 간단한 메시지 처리 시뮬레이션
            if (message.Contains("join_queue"))
            {
                return SimulateMatchingResponse();
            }
            
            return true;
        }

        private bool SimulateMatchingResponse()
        {
            // 실제 서버 응답 시뮬레이션
            return _shouldSucceed;
        }

        public void Dispose()
        {
            // 리소스 정리
        }
    }
    #endregion
}