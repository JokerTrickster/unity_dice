using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 시스템 설정
/// ScriptableObject 기반으로 에디터에서 설정 가능하며, 런타임에서도 수정할 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "MatchingConfig", menuName = "Unity Dice/Matching Config")]
public class MatchingConfig : ScriptableObject
{
    [Header("General Matching Settings")]
    [SerializeField] private bool enableMatching = true;
    [SerializeField] private float maxWaitTimeSeconds = 300f; // 5분
    [SerializeField] private float matchingTimeoutSeconds = 60f; // 1분
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelaySeconds = 5f;
    
    [Header("Player Count Settings")]
    [SerializeField] private int minPlayersPerMatch = 2;
    [SerializeField] private int maxPlayersPerMatch = 4;
    [SerializeField] private int[] allowedPlayerCounts = {2, 3, 4};
    
    [Header("WebSocket Settings")]
    [SerializeField] private string matchingEndpoint = "/api/v1/matching";
    [SerializeField] private float heartbeatIntervalSeconds = 30f;
    [SerializeField] private float connectionTimeoutSeconds = 15f;
    
    [Header("Game Mode Configurations")]
    [SerializeField] private List<GameModeConfig> gameModeConfigs = new List<GameModeConfig>();
    
    [Header("UI Settings")]
    [SerializeField] private float uiUpdateIntervalSeconds = 1f;
    [SerializeField] private bool showEstimatedWaitTime = true;
    [SerializeField] private bool enableMatchingAnimations = true;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool simulateNetworkDelay = false;
    [SerializeField] private float simulatedNetworkDelaySeconds = 2f;

    #region Properties
    /// <summary>매칭 시스템 활성화 여부</summary>
    public bool EnableMatching => enableMatching;
    
    /// <summary>최대 대기 시간 (초)</summary>
    public float MaxWaitTimeSeconds => maxWaitTimeSeconds;
    
    /// <summary>매칭 타임아웃 시간 (초)</summary>
    public float MatchingTimeoutSeconds => matchingTimeoutSeconds;
    
    /// <summary>최대 재시도 횟수</summary>
    public int MaxRetryAttempts => maxRetryAttempts;
    
    /// <summary>재시도 지연 시간 (초)</summary>
    public float RetryDelaySeconds => retryDelaySeconds;
    
    /// <summary>최소 플레이어 수</summary>
    public int MinPlayersPerMatch => minPlayersPerMatch;
    
    /// <summary>최대 플레이어 수</summary>
    public int MaxPlayersPerMatch => maxPlayersPerMatch;
    
    /// <summary>허용된 플레이어 수 배열</summary>
    public int[] AllowedPlayerCounts => allowedPlayerCounts;
    
    /// <summary>매칭 엔드포인트</summary>
    public string MatchingEndpoint => matchingEndpoint;
    
    /// <summary>하트비트 간격 (초)</summary>
    public float HeartbeatIntervalSeconds => heartbeatIntervalSeconds;
    
    /// <summary>연결 타임아웃 (초)</summary>
    public float ConnectionTimeoutSeconds => connectionTimeoutSeconds;
    
    /// <summary>UI 업데이트 간격 (초)</summary>
    public float UIUpdateIntervalSeconds => uiUpdateIntervalSeconds;
    
    /// <summary>예상 대기 시간 표시 여부</summary>
    public bool ShowEstimatedWaitTime => showEstimatedWaitTime;
    
    /// <summary>매칭 애니메이션 활성화 여부</summary>
    public bool EnableMatchingAnimations => enableMatchingAnimations;
    
    /// <summary>디버그 로그 활성화 여부</summary>
    public bool EnableDebugLogs => enableDebugLogs;
    
    /// <summary>네트워크 지연 시뮬레이션 여부</summary>
    public bool SimulateNetworkDelay => simulateNetworkDelay;
    
    /// <summary>시뮬레이션된 네트워크 지연 시간 (초)</summary>
    public float SimulatedNetworkDelaySeconds => simulatedNetworkDelaySeconds;
    #endregion

    #region Game Mode Methods
    /// <summary>
    /// 게임 모드 설정 가져오기
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <returns>게임 모드 설정</returns>
    public GameModeConfig GetGameModeConfig(GameMode gameMode)
    {
        return gameModeConfigs.Find(config => config.gameMode == gameMode) ?? GetDefaultGameModeConfig(gameMode);
    }
    
    /// <summary>
    /// 모든 게임 모드 설정 가져오기
    /// </summary>
    /// <returns>게임 모드 설정 리스트</returns>
    public List<GameModeConfig> GetAllGameModeConfigs()
    {
        return new List<GameModeConfig>(gameModeConfigs);
    }
    
    /// <summary>
    /// 게임 모드 활성화 여부 확인
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <returns>활성화 여부</returns>
    public bool IsGameModeEnabled(GameMode gameMode)
    {
        var config = GetGameModeConfig(gameMode);
        return config.isEnabled;
    }
    
    /// <summary>
    /// 플레이어 수가 허용되는지 확인
    /// </summary>
    /// <param name="playerCount">플레이어 수</param>
    /// <returns>허용 여부</returns>
    public bool IsPlayerCountAllowed(int playerCount)
    {
        return Array.IndexOf(allowedPlayerCounts, playerCount) >= 0;
    }
    #endregion

    #region Default Configurations
    /// <summary>
    /// 기본 게임 모드 설정 생성
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <returns>기본 설정</returns>
    private GameModeConfig GetDefaultGameModeConfig(GameMode gameMode)
    {
        return gameMode switch
        {
            GameMode.Classic => new GameModeConfig
            {
                gameMode = GameMode.Classic,
                displayName = "Classic",
                energyCost = 1,
                minimumLevel = 1,
                estimatedWaitTimeSeconds = 30f,
                isEnabled = true,
                description = "Standard 4-player dice game"
            },
            GameMode.Speed => new GameModeConfig
            {
                gameMode = GameMode.Speed,
                displayName = "Speed",
                energyCost = 2,
                minimumLevel = 5,
                estimatedWaitTimeSeconds = 45f,
                isEnabled = true,
                description = "Fast-paced gameplay"
            },
            GameMode.Challenge => new GameModeConfig
            {
                gameMode = GameMode.Challenge,
                displayName = "Challenge",
                energyCost = 3,
                minimumLevel = 10,
                estimatedWaitTimeSeconds = 60f,
                isEnabled = true,
                description = "High-difficulty matches"
            },
            GameMode.Ranked => new GameModeConfig
            {
                gameMode = GameMode.Ranked,
                displayName = "Ranked",
                energyCost = 2,
                minimumLevel = 15,
                estimatedWaitTimeSeconds = 90f,
                isEnabled = true,
                description = "Competitive ranking matches"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(gameMode), gameMode, null)
        };
    }
    #endregion

    #region Validation
    /// <summary>
    /// 설정 유효성 검사
    /// </summary>
    /// <returns>유효성 검사 결과</returns>
    public bool ValidateConfiguration()
    {
        // 기본 값 검증
        if (maxWaitTimeSeconds <= 0 || matchingTimeoutSeconds <= 0)
        {
            Debug.LogError("[MatchingConfig] Invalid timeout settings");
            return false;
        }
        
        if (minPlayersPerMatch < 1 || maxPlayersPerMatch < minPlayersPerMatch)
        {
            Debug.LogError("[MatchingConfig] Invalid player count settings");
            return false;
        }
        
        if (allowedPlayerCounts == null || allowedPlayerCounts.Length == 0)
        {
            Debug.LogError("[MatchingConfig] No allowed player counts configured");
            return false;
        }
        
        // 허용된 플레이어 수가 범위 내에 있는지 확인
        foreach (int count in allowedPlayerCounts)
        {
            if (count < minPlayersPerMatch || count > maxPlayersPerMatch)
            {
                Debug.LogError($"[MatchingConfig] Player count {count} is outside allowed range");
                return false;
            }
        }
        
        return true;
    }
    #endregion

    #region Editor Support
    /// <summary>
    /// 기본 설정으로 초기화
    /// </summary>
    [ContextMenu("Reset to Default")]
    public void ResetToDefault()
    {
        enableMatching = true;
        maxWaitTimeSeconds = 300f;
        matchingTimeoutSeconds = 60f;
        maxRetryAttempts = 3;
        retryDelaySeconds = 5f;
        minPlayersPerMatch = 2;
        maxPlayersPerMatch = 4;
        allowedPlayerCounts = new int[] { 2, 3, 4 };
        matchingEndpoint = "/api/v1/matching";
        heartbeatIntervalSeconds = 30f;
        connectionTimeoutSeconds = 15f;
        uiUpdateIntervalSeconds = 1f;
        showEstimatedWaitTime = true;
        enableMatchingAnimations = true;
        enableDebugLogs = true;
        simulateNetworkDelay = false;
        simulatedNetworkDelaySeconds = 2f;
        
        // 기본 게임 모드 설정 초기화
        gameModeConfigs.Clear();
        gameModeConfigs.Add(GetDefaultGameModeConfig(GameMode.Classic));
        gameModeConfigs.Add(GetDefaultGameModeConfig(GameMode.Speed));
        gameModeConfigs.Add(GetDefaultGameModeConfig(GameMode.Challenge));
        gameModeConfigs.Add(GetDefaultGameModeConfig(GameMode.Ranked));
    }

    private void OnValidate()
    {
        ValidateConfiguration();
    }
    #endregion
}

/// <summary>
/// 게임 모드별 설정
/// </summary>
[Serializable]
public class GameModeConfig
{
    [Header("Basic Settings")]
    public GameMode gameMode = GameMode.Classic;
    public string displayName = "";
    public string description = "";
    public bool isEnabled = true;
    
    [Header("Requirements")]
    public int energyCost = 1;
    public int minimumLevel = 1;
    
    [Header("Matching Settings")]
    public float estimatedWaitTimeSeconds = 30f;
    public int minPlayersForMode = 2;
    public int maxPlayersForMode = 4;
    
    [Header("Advanced Settings")]
    public bool allowRoomCreation = true;
    public bool allowRankedMatching = false;
    public Dictionary<string, object> customSettings = new Dictionary<string, object>();
}