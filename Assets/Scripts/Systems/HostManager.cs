using System;
using UnityEngine;

/// <summary>
/// 방장 권한 관리 시스템
/// 방장 권한 부여, 위임, 검증 및 게임 시작 권한을 담당
/// </summary>
public class HostManager
{
    #region Events
    public event Action<string> OnHostChanged; // 새 방장 ID
    public event Action<bool> OnHostPrivilegesChanged; // 방장 권한 상태
    public event Action<string> OnGameStartRequested; // 게임 시작 요청
    public event Action<string> OnGameStartFailed; // 게임 시작 실패 사유
    #endregion

    #region Private Fields
    private RoomData _currentRoom;
    private string _localPlayerId;
    private bool _isLocalPlayerHost;
    private HostPermissions _hostPermissions;
    #endregion

    #region Properties
    public bool IsHost => _isLocalPlayerHost;
    public string CurrentHostId => _currentRoom?.HostPlayerId;
    public PlayerInfo CurrentHost => _currentRoom?.HostPlayer;
    public bool CanStartGame => _isLocalPlayerHost && _currentRoom != null && _currentRoom.CanStart;
    public bool CanModifyRoom => _isLocalPlayerHost;
    public bool CanKickPlayers => _isLocalPlayerHost && _hostPermissions.CanKickPlayers;
    public bool CanTransferHost => _isLocalPlayerHost && _hostPermissions.CanTransferHost;
    #endregion

    #region Constructor
    public HostManager(string localPlayerId)
    {
        _localPlayerId = localPlayerId;
        _hostPermissions = new HostPermissions();
        _isLocalPlayerHost = false;
        
        Debug.Log($"[HostManager] Initialized for player: {localPlayerId}");
    }
    #endregion

    #region Room Management
    /// <summary>
    /// 방 정보 설정
    /// </summary>
    public void SetRoom(RoomData roomData)
    {
        _currentRoom = roomData;
        UpdateHostStatus();
        
        Debug.Log($"[HostManager] Room set: {roomData?.GetSummary() ?? "null"}");
    }

    /// <summary>
    /// 방장 상태 업데이트
    /// </summary>
    private void UpdateHostStatus()
    {
        bool wasHost = _isLocalPlayerHost;
        _isLocalPlayerHost = _currentRoom != null && _currentRoom.IsPlayerHost(_localPlayerId);

        if (wasHost != _isLocalPlayerHost)
        {
            OnHostPrivilegesChanged?.Invoke(_isLocalPlayerHost);
            
            if (_isLocalPlayerHost)
            {
                Debug.Log($"[HostManager] Player {_localPlayerId} became host");
                OnBecomeHost();
            }
            else
            {
                Debug.Log($"[HostManager] Player {_localPlayerId} lost host privileges");
                OnLoseHost();
            }
        }
    }

    /// <summary>
    /// 방장 권한 획득시 호출
    /// </summary>
    private void OnBecomeHost()
    {
        // 방장 권한 설정 활성화
        _hostPermissions.EnableAllPermissions();
        
        // UI 업데이트 알림
        OnHostChanged?.Invoke(_localPlayerId);
        
        Debug.Log($"[HostManager] Host privileges activated for {_localPlayerId}");
    }

    /// <summary>
    /// 방장 권한 상실시 호출
    /// </summary>
    private void OnLoseHost()
    {
        // 방장 권한 비활성화
        _hostPermissions.DisableAllPermissions();
        
        Debug.Log($"[HostManager] Host privileges deactivated for {_localPlayerId}");
    }
    #endregion

    #region Host Transfer
    /// <summary>
    /// 방장 권한을 다른 플레이어에게 위임
    /// </summary>
    public bool TransferHostTo(string targetPlayerId)
    {
        if (!CanTransferHost)
        {
            Debug.LogWarning($"[HostManager] Cannot transfer host - insufficient permissions");
            return false;
        }

        if (_currentRoom == null)
        {
            Debug.LogError($"[HostManager] Cannot transfer host - no active room");
            return false;
        }

        if (string.IsNullOrEmpty(targetPlayerId))
        {
            Debug.LogError($"[HostManager] Cannot transfer host - invalid target player ID");
            return false;
        }

        if (targetPlayerId == _localPlayerId)
        {
            Debug.LogWarning($"[HostManager] Cannot transfer host to self");
            return false;
        }

        var targetPlayer = _currentRoom.GetPlayer(targetPlayerId);
        if (targetPlayer == null)
        {
            Debug.LogError($"[HostManager] Cannot transfer host - target player not found: {targetPlayerId}");
            return false;
        }

        // 방 데이터에서 방장 변경
        bool success = _currentRoom.TransferHostTo(targetPlayerId);
        if (success)
        {
            UpdateHostStatus();
            OnHostChanged?.Invoke(targetPlayerId);
            
            Debug.Log($"[HostManager] Host transferred from {_localPlayerId} to {targetPlayer.Nickname}");
        }

        return success;
    }

    /// <summary>
    /// 자동 방장 위임 (방장이 나갔을 때)
    /// </summary>
    public void HandleHostDisconnection()
    {
        if (_currentRoom == null || _currentRoom.Players.Count == 0)
        {
            Debug.Log($"[HostManager] No players left for host transfer");
            return;
        }

        // 다음 플레이어를 방장으로 지정
        _currentRoom.TransferHostToNextPlayer();
        UpdateHostStatus();
        
        var newHost = _currentRoom.HostPlayer;
        OnHostChanged?.Invoke(newHost?.PlayerId);
        
        Debug.Log($"[HostManager] Auto-transferred host to {newHost?.Nickname ?? "Unknown"}");
    }
    #endregion

    #region Game Start Management
    /// <summary>
    /// 게임 시작 시도
    /// </summary>
    public void RequestStartGame()
    {
        if (!CanStartGame)
        {
            string reason = GetGameStartFailureReason();
            Debug.LogWarning($"[HostManager] Cannot start game: {reason}");
            OnGameStartFailed?.Invoke(reason);
            return;
        }

        // 최종 검증
        var validation = ValidateGameStart();
        if (!validation.IsValid)
        {
            string reason = string.Join(", ", validation.Errors);
            Debug.LogWarning($"[HostManager] Game start validation failed: {reason}");
            OnGameStartFailed?.Invoke(reason);
            return;
        }

        // 게임 시작 요청 발행
        OnGameStartRequested?.Invoke(_currentRoom.RoomCode);
        Debug.Log($"[HostManager] Game start requested for room {_currentRoom.RoomCode}");
    }

    /// <summary>
    /// 게임 시작 실패 사유 조회
    /// </summary>
    private string GetGameStartFailureReason()
    {
        if (!_isLocalPlayerHost)
            return "방장 권한이 없습니다";

        if (_currentRoom == null)
            return "활성화된 방이 없습니다";

        if (_currentRoom.Status != RoomStatus.Waiting)
            return $"방 상태가 대기 중이 아닙니다: {_currentRoom.Status}";

        if (!_currentRoom.HasMinimumPlayers)
            return $"최소 인원이 부족합니다: {_currentRoom.CurrentPlayerCount}/2";

        if (_currentRoom.IsExpired)
            return "방이 만료되었습니다";

        return "알 수 없는 오류";
    }

    /// <summary>
    /// 게임 시작 조건 상세 검증
    /// </summary>
    public ValidationResult ValidateGameStart()
    {
        var result = new ValidationResult();

        // 방장 권한 확인
        if (!_isLocalPlayerHost)
        {
            result.AddError("Host privileges required");
        }

        // 방 상태 확인
        if (_currentRoom == null)
        {
            result.AddError("No active room");
            return result;
        }

        // 방 상태 검증
        if (_currentRoom.Status != RoomStatus.Waiting)
        {
            result.AddError($"Invalid room status: {_currentRoom.Status}");
        }

        // 플레이어 수 검증
        if (_currentRoom.CurrentPlayerCount < 2)
        {
            result.AddError($"Insufficient players: {_currentRoom.CurrentPlayerCount}/2 minimum");
        }

        if (_currentRoom.CurrentPlayerCount > _currentRoom.MaxPlayers)
        {
            result.AddError($"Too many players: {_currentRoom.CurrentPlayerCount}/{_currentRoom.MaxPlayers}");
        }

        // 만료 시간 확인
        if (_currentRoom.IsExpired)
        {
            result.AddError("Room has expired");
        }

        // 모든 플레이어 준비 상태 확인
        if (!_currentRoom.AllPlayersReady())
        {
            result.AddWarning("Not all players are ready");
        }

        // 에너지 검증 (EnergyManager와 연동 필요)
        if (!ValidatePlayersEnergy())
        {
            result.AddError("One or more players don't have sufficient energy");
        }

        return result;
    }

    /// <summary>
    /// 모든 플레이어의 에너지 상태 검증
    /// </summary>
    private bool ValidatePlayersEnergy()
    {
        // 실제 구현시에는 EnergyManager.Instance.CanStartGame() 등을 사용
        // 현재는 기본적으로 true 반환
        return true;
    }
    #endregion

    #region Room Configuration
    /// <summary>
    /// 방 설정 변경 (방장만 가능)
    /// </summary>
    public bool UpdateRoomSettings(int maxPlayers)
    {
        if (!CanModifyRoom)
        {
            Debug.LogWarning($"[HostManager] Cannot modify room - insufficient permissions");
            return false;
        }

        if (_currentRoom == null)
        {
            Debug.LogError($"[HostManager] Cannot modify room - no active room");
            return false;
        }

        if (maxPlayers < _currentRoom.CurrentPlayerCount)
        {
            Debug.LogWarning($"[HostManager] Cannot set max players below current count: {maxPlayers} < {_currentRoom.CurrentPlayerCount}");
            return false;
        }

        int oldMaxPlayers = _currentRoom.MaxPlayers;
        _currentRoom.MaxPlayers = maxPlayers;

        Debug.Log($"[HostManager] Room settings updated - MaxPlayers: {oldMaxPlayers} -> {maxPlayers}");
        return true;
    }

    /// <summary>
    /// 플레이어 추방 (방장만 가능)
    /// </summary>
    public bool KickPlayer(string playerId)
    {
        if (!CanKickPlayers)
        {
            Debug.LogWarning($"[HostManager] Cannot kick player - insufficient permissions");
            return false;
        }

        if (_currentRoom == null)
        {
            Debug.LogError($"[HostManager] Cannot kick player - no active room");
            return false;
        }

        if (string.IsNullOrEmpty(playerId) || playerId == _localPlayerId)
        {
            Debug.LogWarning($"[HostManager] Cannot kick self or invalid player ID");
            return false;
        }

        var targetPlayer = _currentRoom.GetPlayer(playerId);
        if (targetPlayer == null)
        {
            Debug.LogWarning($"[HostManager] Cannot kick player - player not found: {playerId}");
            return false;
        }

        bool success = _currentRoom.RemovePlayer(playerId);
        if (success)
        {
            Debug.Log($"[HostManager] Player kicked: {targetPlayer.Nickname} ({playerId})");
        }

        return success;
    }
    #endregion

    #region Permissions Management
    /// <summary>
    /// 특정 권한 설정
    /// </summary>
    public void SetPermission(HostPermissionType permissionType, bool enabled)
    {
        _hostPermissions.SetPermission(permissionType, enabled);
        Debug.Log($"[HostManager] Permission {permissionType} set to {enabled}");
    }

    /// <summary>
    /// 권한 확인
    /// </summary>
    public bool HasPermission(HostPermissionType permissionType)
    {
        return _isLocalPlayerHost && _hostPermissions.HasPermission(permissionType);
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// 정리
    /// </summary>
    public void Cleanup()
    {
        _currentRoom = null;
        _isLocalPlayerHost = false;
        _hostPermissions.DisableAllPermissions();
        
        OnHostChanged = null;
        OnHostPrivilegesChanged = null;
        OnGameStartRequested = null;
        OnGameStartFailed = null;
        
        Debug.Log($"[HostManager] Cleaned up");
    }
    #endregion
}

/// <summary>
/// 방장 권한 관리 클래스
/// </summary>
public class HostPermissions
{
    public bool CanStartGame { get; private set; } = true;
    public bool CanModifyRoom { get; private set; } = true;
    public bool CanKickPlayers { get; private set; } = true;
    public bool CanTransferHost { get; private set; } = true;
    public bool CanInvitePlayers { get; private set; } = true;

    public void EnableAllPermissions()
    {
        CanStartGame = true;
        CanModifyRoom = true;
        CanKickPlayers = true;
        CanTransferHost = true;
        CanInvitePlayers = true;
    }

    public void DisableAllPermissions()
    {
        CanStartGame = false;
        CanModifyRoom = false;
        CanKickPlayers = false;
        CanTransferHost = false;
        CanInvitePlayers = false;
    }

    public void SetPermission(HostPermissionType permissionType, bool enabled)
    {
        switch (permissionType)
        {
            case HostPermissionType.StartGame:
                CanStartGame = enabled;
                break;
            case HostPermissionType.ModifyRoom:
                CanModifyRoom = enabled;
                break;
            case HostPermissionType.KickPlayers:
                CanKickPlayers = enabled;
                break;
            case HostPermissionType.TransferHost:
                CanTransferHost = enabled;
                break;
            case HostPermissionType.InvitePlayers:
                CanInvitePlayers = enabled;
                break;
        }
    }

    public bool HasPermission(HostPermissionType permissionType)
    {
        return permissionType switch
        {
            HostPermissionType.StartGame => CanStartGame,
            HostPermissionType.ModifyRoom => CanModifyRoom,
            HostPermissionType.KickPlayers => CanKickPlayers,
            HostPermissionType.TransferHost => CanTransferHost,
            HostPermissionType.InvitePlayers => CanInvitePlayers,
            _ => false
        };
    }
}

/// <summary>
/// 방장 권한 타입 열거형
/// </summary>
public enum HostPermissionType
{
    StartGame,
    ModifyRoom,
    KickPlayers,
    TransferHost,
    InvitePlayers
}