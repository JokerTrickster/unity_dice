using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 데이터 구조
/// 방의 상태, 플레이어 정보, 설정 등을 관리하는 데이터 클래스
/// </summary>
[System.Serializable]
public class RoomData
{
    #region Core Properties
    [SerializeField] private string roomCode;
    [SerializeField] private string hostPlayerId;
    [SerializeField] private List<PlayerInfo> players;
    [SerializeField] private int maxPlayers;
    [SerializeField] private RoomStatus status;
    [SerializeField] private DateTime createdAt;
    [SerializeField] private DateTime expiresAt;
    #endregion

    #region Public Properties
    public string RoomCode 
    { 
        get => roomCode; 
        set => roomCode = value; 
    }
    
    public string HostPlayerId 
    { 
        get => hostPlayerId; 
        set => hostPlayerId = value; 
    }
    
    public List<PlayerInfo> Players 
    { 
        get => players ?? (players = new List<PlayerInfo>()); 
        set => players = value; 
    }
    
    public int MaxPlayers 
    { 
        get => maxPlayers; 
        set => maxPlayers = Mathf.Clamp(value, 2, 4); 
    }
    
    public RoomStatus Status 
    { 
        get => status; 
        set => status = value; 
    }
    
    public DateTime CreatedAt 
    { 
        get => createdAt; 
        set => createdAt = value; 
    }
    
    public DateTime ExpiresAt 
    { 
        get => expiresAt; 
        set => expiresAt = value; 
    }

    // Computed Properties
    public int CurrentPlayerCount => Players.Count;
    public bool IsFull => CurrentPlayerCount >= MaxPlayers;
    public bool IsEmpty => CurrentPlayerCount == 0;
    public bool HasMinimumPlayers => CurrentPlayerCount >= 2;
    public bool IsExpired => DateTime.Now > ExpiresAt;
    public bool CanStart => HasMinimumPlayers && Status == RoomStatus.Waiting && !IsExpired;
    public PlayerInfo HostPlayer => Players.Find(p => p.PlayerId == HostPlayerId);
    #endregion

    #region Constructor
    public RoomData()
    {
        players = new List<PlayerInfo>();
        status = RoomStatus.Waiting;
        createdAt = DateTime.Now;
        expiresAt = DateTime.Now.AddMinutes(30); // 30분 자동 만료
    }

    public RoomData(string roomCode, string hostPlayerId, int maxPlayers) : this()
    {
        this.roomCode = roomCode;
        this.hostPlayerId = hostPlayerId;
        this.maxPlayers = Mathf.Clamp(maxPlayers, 2, 4);
    }
    #endregion

    #region Player Management
    /// <summary>
    /// 플레이어를 방에 추가
    /// </summary>
    public bool AddPlayer(PlayerInfo playerInfo)
    {
        if (playerInfo == null) return false;
        if (IsFull) return false;
        if (Status != RoomStatus.Waiting) return false;
        if (Players.Exists(p => p.PlayerId == playerInfo.PlayerId)) return false;

        Players.Add(playerInfo);
        Debug.Log($"[RoomData] Player {playerInfo.Nickname} added to room {RoomCode}. Current count: {CurrentPlayerCount}/{MaxPlayers}");
        return true;
    }

    /// <summary>
    /// 플레이어를 방에서 제거
    /// </summary>
    public bool RemovePlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return false;
        
        var player = Players.Find(p => p.PlayerId == playerId);
        if (player == null) return false;

        Players.Remove(player);
        Debug.Log($"[RoomData] Player {player.Nickname} removed from room {RoomCode}. Current count: {CurrentPlayerCount}/{MaxPlayers}");

        // 방장이 나간 경우 권한 위임
        if (playerId == HostPlayerId && Players.Count > 0)
        {
            TransferHostToNextPlayer();
        }

        return true;
    }

    /// <summary>
    /// 플레이어 정보 업데이트
    /// </summary>
    public bool UpdatePlayer(PlayerInfo updatedPlayerInfo)
    {
        if (updatedPlayerInfo == null) return false;
        
        var existingPlayer = Players.Find(p => p.PlayerId == updatedPlayerInfo.PlayerId);
        if (existingPlayer == null) return false;

        // 플레이어 정보 업데이트
        existingPlayer.Nickname = updatedPlayerInfo.Nickname;
        existingPlayer.IsReady = updatedPlayerInfo.IsReady;
        existingPlayer.IsHost = updatedPlayerInfo.IsHost;

        return true;
    }

    /// <summary>
    /// 플레이어 조회
    /// </summary>
    public PlayerInfo GetPlayer(string playerId)
    {
        return Players.Find(p => p.PlayerId == playerId);
    }

    /// <summary>
    /// 모든 플레이어가 준비 상태인지 확인
    /// </summary>
    public bool AllPlayersReady()
    {
        if (CurrentPlayerCount < 2) return false;
        
        foreach (var player in Players)
        {
            // 방장은 자동으로 준비 상태로 간주
            if (player.IsHost) continue;
            if (!player.IsReady) return false;
        }
        
        return true;
    }
    #endregion

    #region Host Management
    /// <summary>
    /// 방장 권한을 다음 플레이어에게 위임
    /// </summary>
    public void TransferHostToNextPlayer()
    {
        if (Players.Count == 0) return;

        // 기존 방장 권한 제거
        var oldHost = Players.Find(p => p.PlayerId == HostPlayerId);
        if (oldHost != null)
        {
            oldHost.IsHost = false;
        }

        // 첫 번째 플레이어를 새 방장으로 설정
        var newHost = Players[0];
        newHost.IsHost = true;
        HostPlayerId = newHost.PlayerId;

        Debug.Log($"[RoomData] Host transferred to {newHost.Nickname} in room {RoomCode}");
    }

    /// <summary>
    /// 특정 플레이어를 방장으로 지정
    /// </summary>
    public bool TransferHostTo(string playerId)
    {
        var newHost = Players.Find(p => p.PlayerId == playerId);
        if (newHost == null) return false;

        // 기존 방장 권한 제거
        var oldHost = Players.Find(p => p.PlayerId == HostPlayerId);
        if (oldHost != null)
        {
            oldHost.IsHost = false;
        }

        // 새 방장 설정
        newHost.IsHost = true;
        HostPlayerId = newHost.PlayerId;

        Debug.Log($"[RoomData] Host manually transferred to {newHost.Nickname} in room {RoomCode}");
        return true;
    }

    /// <summary>
    /// 특정 플레이어가 방장인지 확인
    /// </summary>
    public bool IsPlayerHost(string playerId)
    {
        return !string.IsNullOrEmpty(playerId) && playerId == HostPlayerId;
    }
    #endregion

    #region Validation
    /// <summary>
    /// 방 데이터 유효성 검증
    /// </summary>
    public ValidationResult Validate()
    {
        var result = new ValidationResult();

        // 방 코드 검증
        if (string.IsNullOrEmpty(RoomCode) || RoomCode.Length != 4 || !int.TryParse(RoomCode, out _))
        {
            result.AddError("Invalid room code format. Must be 4-digit number.");
        }

        // 방장 ID 검증
        if (string.IsNullOrEmpty(HostPlayerId))
        {
            result.AddError("Host player ID is required.");
        }

        // 플레이어 수 검증
        if (MaxPlayers < 2 || MaxPlayers > 4)
        {
            result.AddError("Max players must be between 2 and 4.");
        }

        if (CurrentPlayerCount > MaxPlayers)
        {
            result.AddError("Current player count exceeds max players.");
        }

        // 방장이 플레이어 목록에 있는지 확인
        if (!string.IsNullOrEmpty(HostPlayerId) && !Players.Exists(p => p.PlayerId == HostPlayerId))
        {
            result.AddError("Host player not found in player list.");
        }

        // 만료 시간 검증
        if (IsExpired)
        {
            result.AddWarning("Room has expired.");
        }

        return result;
    }
    #endregion

    #region Utility
    /// <summary>
    /// JSON으로 변환
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// JSON에서 생성
    /// </summary>
    public static RoomData FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<RoomData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomData] Failed to parse JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 방 정보 요약
    /// </summary>
    public string GetSummary()
    {
        return $"Room {RoomCode}: {CurrentPlayerCount}/{MaxPlayers} players, Status: {Status}, Host: {HostPlayer?.Nickname ?? "Unknown"}";
    }
    #endregion
}

/// <summary>
/// 플레이어 정보 구조
/// </summary>
[System.Serializable]
public class PlayerInfo
{
    [SerializeField] private string playerId;
    [SerializeField] private string nickname;
    [SerializeField] private bool isHost;
    [SerializeField] private bool isReady;
    [SerializeField] private DateTime joinedAt;

    #region Properties
    public string PlayerId 
    { 
        get => playerId; 
        set => playerId = value; 
    }
    
    public string Nickname 
    { 
        get => nickname; 
        set => nickname = value; 
    }
    
    public bool IsHost 
    { 
        get => isHost; 
        set => isHost = value; 
    }
    
    public bool IsReady 
    { 
        get => isReady; 
        set => isReady = value; 
    }
    
    public DateTime JoinedAt 
    { 
        get => joinedAt; 
        set => joinedAt = value; 
    }
    #endregion

    #region Constructor
    public PlayerInfo()
    {
        joinedAt = DateTime.Now;
    }

    public PlayerInfo(string playerId, string nickname, bool isHost = false) : this()
    {
        this.playerId = playerId;
        this.nickname = nickname;
        this.isHost = isHost;
        this.isReady = isHost; // 방장은 기본적으로 준비 상태
    }
    #endregion

    #region Utility
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public static PlayerInfo FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<PlayerInfo>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerInfo] Failed to parse JSON: {ex.Message}");
            return null;
        }
    }
    #endregion
}

/// <summary>
/// 방 상태 열거형
/// </summary>
public enum RoomStatus
{
    Waiting = 0,    // 플레이어 대기 중
    Starting = 1,   // 게임 시작 중
    InGame = 2,     // 게임 진행 중
    Closed = 3      // 방 종료
}

/// <summary>
/// 유효성 검증 결과
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; private set; } = new List<string>();
    public List<string> Warnings { get; private set; } = new List<string>();
    
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;

    public void AddError(string error)
    {
        Errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    public string GetSummary()
    {
        var summary = $"Valid: {IsValid}";
        if (Errors.Count > 0)
        {
            summary += $", Errors: {string.Join(", ", Errors)}";
        }
        if (Warnings.Count > 0)
        {
            summary += $", Warnings: {string.Join(", ", Warnings)}";
        }
        return summary;
    }
}