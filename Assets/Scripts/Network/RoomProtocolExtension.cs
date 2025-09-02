using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 시스템 프로토콜 확장
/// MatchingProtocol을 확장하여 방 생성/참여/관리 메시지를 지원
/// 실시간 방 상태 동기화 및 플레이어 이벤트 처리
/// </summary>
public static class RoomProtocolExtension
{
    #region Room Message Types
    /// <summary>방 관련 메시지 타입</summary>
    public static readonly HashSet<string> ROOM_MESSAGE_TYPES = new()
    {
        // Room management
        "create_room",          // 방 생성 요청
        "join_room",           // 방 참여 요청
        "leave_room",          // 방 나가기 요청
        "room_update",         // 방 정보 업데이트
        "room_state_sync",     // 방 상태 동기화
        "close_room",          // 방 닫기 요청
        
        // Room responses
        "room_created",        // 방 생성 완료
        "room_joined",         // 방 참여 완료
        "room_left",           // 방 나가기 완료
        "room_updated",        // 방 정보 업데이트 완료
        "room_closed",         // 방 닫힘
        "room_error",          // 방 관련 오류
        
        // Player events
        "player_joined",       // 플레이어 참여
        "player_left",         // 플레이어 나가기
        "player_ready",        // 플레이어 준비
        "player_updated",      // 플레이어 정보 업데이트
        "host_changed",        // 방장 변경
        
        // Game flow
        "game_start_request",  // 게임 시작 요청
        "game_starting",       // 게임 시작 중
        "game_started",        // 게임 시작됨
        "ready_check",         // 준비 확인
        "ready_check_result"   // 준비 확인 결과
    };

    /// <summary>클라이언트에서 전송 가능한 방 메시지</summary>
    public static readonly HashSet<string> CLIENT_ROOM_MESSAGE_TYPES = new()
    {
        "create_room", "join_room", "leave_room", "room_update", 
        "close_room", "player_ready", "game_start_request", "ready_check"
    };

    /// <summary>서버에서 전송하는 방 메시지</summary>
    public static readonly HashSet<string> SERVER_ROOM_MESSAGE_TYPES = new()
    {
        "room_created", "room_joined", "room_left", "room_updated", 
        "room_closed", "room_error", "player_joined", "player_left", 
        "player_updated", "host_changed", "game_starting", "game_started",
        "ready_check_result", "room_state_sync"
    };
    #endregion

    #region Room Data Models
    /// <summary>방 생성 요청 데이터</summary>
    [Serializable]
    public class CreateRoomRequest
    {
        public string roomCode;
        public string hostPlayerId;
        public string hostNickname;
        public int maxPlayers;
        public string gameMode = "classic";
        public int betAmount = 0;
        public bool isPrivate = false;
        public Dictionary<string, string> roomSettings = new();
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(roomCode) && 
                   !string.IsNullOrEmpty(hostPlayerId) && 
                   !string.IsNullOrEmpty(hostNickname) &&
                   MatchingProtocol.IsValidPlayerCount(maxPlayers);
        }
    }

    /// <summary>방 참여 요청 데이터</summary>
    [Serializable]
    public class JoinRoomRequest
    {
        public string roomCode;
        public string playerId;
        public string nickname;
        public Dictionary<string, string> playerData = new();
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(roomCode) && 
                   !string.IsNullOrEmpty(playerId) &&
                   !string.IsNullOrEmpty(nickname) &&
                   RoomCodeGenerator.IsValidRoomCodeFormat(roomCode);
        }
    }

    /// <summary>방 업데이트 요청 데이터</summary>
    [Serializable]
    public class RoomUpdateRequest
    {
        public string roomCode;
        public string playerId;
        public RoomUpdateType updateType;
        public object updateData;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(roomCode) && 
                   !string.IsNullOrEmpty(playerId);
        }
    }

    /// <summary>방 응답 데이터</summary>
    [Serializable]
    public class RoomResponse
    {
        public bool success;
        public string roomCode;
        public RoomData roomData;
        public PlayerInfo playerInfo;
        public string errorCode;
        public string errorMessage;
        public DateTime timestamp = DateTime.UtcNow;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(roomCode);
        }
        
        public string GetSummary()
        {
            var status = success ? "SUCCESS" : $"ERROR({errorCode})";
            var playerCount = roomData?.CurrentPlayerCount ?? 0;
            var maxPlayers = roomData?.MaxPlayers ?? 0;
            return $"[{status}] Room:{roomCode} Players:{playerCount}/{maxPlayers}";
        }
    }

    /// <summary>실시간 상태 동기화 데이터</summary>
    [Serializable]
    public class RoomStateSyncData
    {
        public string roomCode;
        public RoomData roomData;
        public List<PlayerInfo> players = new();
        public RoomStatus status;
        public DateTime lastUpdate = DateTime.UtcNow;
        public int syncVersion = 1;
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(roomCode) && roomData != null;
        }
    }
    #endregion

    #region Room Update Types
    public enum RoomUpdateType
    {
        PlayerReady,        // 플레이어 준비 상태 변경
        RoomSettings,       // 방 설정 변경
        GameMode,          // 게임 모드 변경
        MaxPlayers,        // 최대 플레이어 수 변경
        PlayerKick,        // 플레이어 강제 퇴장
        HostTransfer      // 방장 권한 이양
    }
    #endregion

    #region Validation Methods
    /// <summary>방 메시지 타입 유효성 확인</summary>
    public static bool IsValidRoomMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && 
               ROOM_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>클라이언트 방 메시지 타입인지 확인</summary>
    public static bool IsClientRoomMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && 
               CLIENT_ROOM_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>서버 방 메시지 타입인지 확인</summary>
    public static bool IsServerRoomMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && 
               SERVER_ROOM_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>방 코드 형식 검증</summary>
    public static bool ValidateRoomCode(string roomCode)
    {
        return RoomCodeGenerator.IsValidRoomCodeFormat(roomCode);
    }

    /// <summary>방 상태 동기화 데이터 검증</summary>
    public static bool ValidateSyncData(RoomStateSyncData syncData)
    {
        if (!syncData.IsValid()) return false;
        
        // 플레이어 수 일관성 검증
        if (syncData.players.Count != syncData.roomData.CurrentPlayerCount)
        {
            Debug.LogWarning($"[RoomProtocol] Player count mismatch: {syncData.players.Count} vs {syncData.roomData.CurrentPlayerCount}");
            return false;
        }
        
        // 방장 존재 검증
        var hostExists = syncData.players.Exists(p => p.IsHost);
        if (!hostExists && syncData.players.Count > 0)
        {
            Debug.LogWarning($"[RoomProtocol] No host found in room {syncData.roomCode}");
            return false;
        }
        
        return true;
    }
    #endregion

    #region Message Factory Methods
    /// <summary>방 생성 메시지 생성</summary>
    public static string CreateRoomCreateMessage(string playerId, string nickname, int maxPlayers, 
        string gameMode = "classic", int betAmount = 0, bool isPrivate = false)
    {
        var roomCode = RoomCodeGenerator.Instance.GenerateRoomCode();
        var request = new CreateRoomRequest
        {
            roomCode = roomCode,
            hostPlayerId = playerId,
            hostNickname = nickname,
            maxPlayers = maxPlayers,
            gameMode = gameMode,
            betAmount = betAmount,
            isPrivate = isPrivate
        };

        if (!request.IsValid())
        {
            Debug.LogError("[RoomProtocol] Invalid create room request");
            return null;
        }

        var message = new MatchingMessage("create_room", request, 2);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>방 참여 메시지 생성</summary>
    public static string CreateRoomJoinMessage(string playerId, string nickname, string roomCode)
    {
        var request = new JoinRoomRequest
        {
            roomCode = roomCode,
            playerId = playerId,
            nickname = nickname
        };

        if (!request.IsValid())
        {
            Debug.LogError("[RoomProtocol] Invalid join room request");
            return null;
        }

        var message = new MatchingMessage("join_room", request, 2);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>방 나가기 메시지 생성</summary>
    public static string CreateRoomLeaveMessage(string playerId, string roomCode)
    {
        var request = new { playerId, roomCode };
        var message = new MatchingMessage("leave_room", request, 1);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>방 상태 업데이트 메시지 생성</summary>
    public static string CreateRoomUpdateMessage(string playerId, string roomCode, 
        RoomUpdateType updateType, object updateData)
    {
        var request = new RoomUpdateRequest
        {
            roomCode = roomCode,
            playerId = playerId,
            updateType = updateType,
            updateData = updateData
        };

        if (!request.IsValid())
        {
            Debug.LogError("[RoomProtocol] Invalid room update request");
            return null;
        }

        var message = new MatchingMessage("room_update", request, 1);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>게임 시작 요청 메시지 생성</summary>
    public static string CreateGameStartRequestMessage(string hostId, string roomCode)
    {
        var request = new { hostId, roomCode, timestamp = DateTime.UtcNow };
        var message = new MatchingMessage("game_start_request", request, 3);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>플레이어 준비 상태 메시지 생성</summary>
    public static string CreatePlayerReadyMessage(string playerId, string roomCode, bool isReady)
    {
        var request = new { playerId, roomCode, isReady, timestamp = DateTime.UtcNow };
        var message = new MatchingMessage("player_ready", request, 1);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>방 상태 동기화 메시지 생성</summary>
    public static string CreateRoomStateSyncMessage(RoomStateSyncData syncData)
    {
        if (!ValidateSyncData(syncData))
        {
            Debug.LogError("[RoomProtocol] Invalid sync data");
            return null;
        }

        var message = new MatchingMessage("room_state_sync", syncData, 2);
        return MatchingProtocol.SerializeMessage(message);
    }
    #endregion

    #region Message Parsing Methods
    /// <summary>방 메시지에서 CreateRoomRequest 추출</summary>
    public static CreateRoomRequest ParseCreateRoomRequest(MatchingMessage message)
    {
        if (message?.type != "create_room") return null;
        return message.GetPayload<CreateRoomRequest>();
    }

    /// <summary>방 메시지에서 JoinRoomRequest 추출</summary>
    public static JoinRoomRequest ParseJoinRoomRequest(MatchingMessage message)
    {
        if (message?.type != "join_room") return null;
        return message.GetPayload<JoinRoomRequest>();
    }

    /// <summary>방 메시지에서 RoomResponse 추출</summary>
    public static RoomResponse ParseRoomResponse(MatchingMessage message)
    {
        if (!IsServerRoomMessageType(message?.type)) return null;
        return message.GetPayload<RoomResponse>();
    }

    /// <summary>방 메시지에서 RoomStateSyncData 추출</summary>
    public static RoomStateSyncData ParseRoomStateSyncData(MatchingMessage message)
    {
        if (message?.type != "room_state_sync") return null;
        return message.GetPayload<RoomStateSyncData>();
    }

    /// <summary>일반적인 방 이벤트 데이터 추출</summary>
    public static T ParseRoomEventData<T>(MatchingMessage message) where T : class
    {
        if (!IsValidRoomMessageType(message?.type)) return null;
        return message.GetPayload<T>();
    }
    #endregion

    #region Error Handling
    /// <summary>방 관련 에러 메시지 생성</summary>
    public static string CreateRoomErrorMessage(string errorCode, string errorMessage, 
        string roomCode = "", string originalMessageId = "")
    {
        var payload = new RoomResponse
        {
            success = false,
            roomCode = roomCode,
            errorCode = errorCode,
            errorMessage = errorMessage
        };

        var message = new MatchingMessage("room_error", payload, 3);
        return MatchingProtocol.SerializeMessage(message);
    }

    /// <summary>방 코드 오류 메시지 생성</summary>
    public static string CreateInvalidRoomCodeError(string roomCode, string originalMessageId = "")
    {
        return CreateRoomErrorMessage(
            "INVALID_ROOM_CODE",
            $"Invalid or expired room code: {roomCode}",
            roomCode,
            originalMessageId
        );
    }

    /// <summary>방이 가득 참 오류 메시지 생성</summary>
    public static string CreateRoomFullError(string roomCode, string originalMessageId = "")
    {
        return CreateRoomErrorMessage(
            "ROOM_FULL",
            "Room has reached maximum player capacity",
            roomCode,
            originalMessageId
        );
    }

    /// <summary>권한 없음 오류 메시지 생성</summary>
    public static string CreatePermissionDeniedError(string action, string roomCode, string originalMessageId = "")
    {
        return CreateRoomErrorMessage(
            "PERMISSION_DENIED",
            $"Permission denied for action: {action}",
            roomCode,
            originalMessageId
        );
    }

    /// <summary>방 상태 불일치 오류 메시지 생성</summary>
    public static string CreateRoomStateConflictError(string roomCode, string expectedState, string actualState)
    {
        return CreateRoomErrorMessage(
            "ROOM_STATE_CONFLICT",
            $"Room state conflict: expected {expectedState}, actual {actualState}",
            roomCode
        );
    }
    #endregion

    #region Protocol Integration
    /// <summary>기존 MatchingProtocol에 방 메시지 타입 추가</summary>
    public static void ExtendMatchingProtocol()
    {
        // 기존 VALID_MESSAGE_TYPES에 방 메시지 타입 추가
        foreach (var messageType in ROOM_MESSAGE_TYPES)
        {
            MatchingProtocol.VALID_MESSAGE_TYPES.Add(messageType);
        }

        // 클라이언트 메시지 타입 추가
        foreach (var messageType in CLIENT_ROOM_MESSAGE_TYPES)
        {
            MatchingProtocol.CLIENT_MESSAGE_TYPES.Add(messageType);
        }

        // 서버 메시지 타입 추가
        foreach (var messageType in SERVER_ROOM_MESSAGE_TYPES)
        {
            MatchingProtocol.SERVER_MESSAGE_TYPES.Add(messageType);
        }

        Debug.Log("[RoomProtocol] Extended MatchingProtocol with room message types");
    }

    /// <summary>방 프로토콜 통계 정보</summary>
    public static string GetRoomProtocolStats()
    {
        return $"Room Protocol Extension:\n" +
               $"- Room Message Types: {ROOM_MESSAGE_TYPES.Count}\n" +
               $"- Client Messages: {CLIENT_ROOM_MESSAGE_TYPES.Count}\n" +
               $"- Server Messages: {SERVER_ROOM_MESSAGE_TYPES.Count}\n" +
               $"- Protocol Version: {MatchingProtocol.PROTOCOL_VERSION}";
    }
    #endregion

    #region Utility Methods
    /// <summary>방 메시지 우선순위 결정</summary>
    public static MessagePriority GetRoomMessagePriority(string messageType)
    {
        return messageType?.ToLower() switch
        {
            "create_room" or "join_room" or "game_start_request" => MessagePriority.High,
            "room_state_sync" or "player_ready" or "room_update" => MessagePriority.Normal,
            "leave_room" or "player_left" => MessagePriority.Low,
            _ => MessagePriority.Normal
        };
    }

    /// <summary>메시지 타입에서 예상 응답 타입 반환</summary>
    public static string GetExpectedResponseType(string requestType)
    {
        return requestType?.ToLower() switch
        {
            "create_room" => "room_created",
            "join_room" => "room_joined", 
            "leave_room" => "room_left",
            "room_update" => "room_updated",
            "game_start_request" => "game_starting",
            _ => null
        };
    }

    /// <summary>디버그 정보 출력</summary>
    public static void LogRoomMessageDebug(MatchingMessage message, string prefix = "[RoomProtocol]")
    {
        if (message == null)
        {
            Debug.Log($"{prefix} Message is null");
            return;
        }

        var priority = GetRoomMessagePriority(message.type);
        Debug.Log($"{prefix} {message.GetSummary()} Priority:{priority}");

        if (IsValidRoomMessageType(message.type))
        {
            var expectedResponse = GetExpectedResponseType(message.type);
            if (!string.IsNullOrEmpty(expectedResponse))
            {
                Debug.Log($"{prefix} Expected response: {expectedResponse}");
            }
        }
    }
    #endregion
}