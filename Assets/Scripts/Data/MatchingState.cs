using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 상태 열거형
/// </summary>
public enum MatchingState
{
    Idle,           // 매칭 대기
    Searching,      // 매칭 검색 중
    Found,          // 매칭 완료
    Starting,       // 게임 시작 중
    Cancelled,      // 매칭 취소됨
    Failed          // 매칭 실패
}

/// <summary>
/// 게임 모드 열거형
/// </summary>
public enum GameMode
{
    Classic,        // 클래식 모드 (1 energy, Level 1+)
    Speed,          // 스피드 모드 (2 energy, Level 5+)
    Challenge,      // 챌린지 모드 (3 energy, Level 10+)
    Ranked          // 랭크 모드 (2 energy, Level 15+)
}

/// <summary>
/// 매칭 타입 열거형
/// </summary>
public enum MatchType
{
    Random,         // 랜덤 매칭
    Ranked,         // 랭크 매칭
    Room            // 방 생성/참가
}

/// <summary>
/// 매칭 요청 데이터
/// </summary>
[Serializable]
public class MatchingRequest
{
    public string playerId;
    public int playerCount = 2;
    public GameMode gameMode = GameMode.Classic;
    public MatchType matchType = MatchType.Random;
    public string roomCode = "";
    public float timestamp;
    
    public MatchingRequest()
    {
        timestamp = Time.time;
    }
}

/// <summary>
/// 매칭 응답 데이터
/// </summary>
[Serializable]
public class MatchingResponse
{
    public string type;
    public bool success;
    public string error;
    public string roomId;
    public List<PlayerInfo> players = new List<PlayerInfo>();
    public GameMode gameMode;
    public float estimatedStartTime;
    public Dictionary<string, object> metadata = new Dictionary<string, object>();
}

/// <summary>
/// 플레이어 정보
/// </summary>
[Serializable]
public class PlayerInfo
{
    public string playerId;
    public string displayName;
    public int level;
    public int ranking = 0;
    public string avatarUrl = "";
    public bool isReady = false;
}

/// <summary>
/// 매칭 상태 정보
/// </summary>
[Serializable]
public class MatchingStateInfo
{
    public MatchingState currentState = MatchingState.Idle;
    public GameMode selectedGameMode = GameMode.Classic;
    public MatchType matchType = MatchType.Random;
    public int selectedPlayerCount = 2;
    public string currentRoomCode = "";
    public float searchStartTime = 0f;
    public float estimatedWaitTime = 0f;
    public List<PlayerInfo> matchedPlayers = new List<PlayerInfo>();
    public string lastErrorMessage = "";
    
    /// <summary>
    /// 검색 중인지 확인
    /// </summary>
    public bool IsSearching => currentState == MatchingState.Searching;
    
    /// <summary>
    /// 매칭이 완료되었는지 확인
    /// </summary>
    public bool IsMatched => currentState == MatchingState.Found || currentState == MatchingState.Starting;
    
    /// <summary>
    /// 에러 상태인지 확인
    /// </summary>
    public bool HasError => currentState == MatchingState.Failed || !string.IsNullOrEmpty(lastErrorMessage);
    
    /// <summary>
    /// 현재 검색 시간 (초)
    /// </summary>
    public float CurrentSearchTime => IsSearching ? Time.time - searchStartTime : 0f;
}

/// <summary>
/// 게임 시작 데이터
/// </summary>
[Serializable]
public class GameStartData
{
    public string roomId;
    public List<PlayerInfo> players = new List<PlayerInfo>();
    public GameMode gameMode;
    public Dictionary<string, object> gameSettings = new Dictionary<string, object>();
    public float startCountdown = 3f;
}

/// <summary>
/// 매칭 실패 데이터
/// </summary>
[Serializable]
public class MatchingFailureData
{
    public string reason;
    public string detailedMessage;
    public int errorCode;
    public bool canRetry;
    public float retryDelay = 5f;
}

/// <summary>
/// 매칭 통계 데이터
/// </summary>
[Serializable]
public class MatchingStats
{
    public int totalMatchesAttempted;
    public int totalMatchesCompleted;
    public int totalMatchesCancelled;
    public float averageWaitTime;
    public Dictionary<GameMode, float> averageWaitTimeByMode = new Dictionary<GameMode, float>();
    public DateTime lastMatchTime;
}