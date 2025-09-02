using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 매칭 프로토콜 관리자
/// 매칭 시스템의 메시지 직렬화/역직렬화, 타입 검증, 버전 관리를 담당
/// </summary>
public static class MatchingProtocol
{
    #region Constants
    /// <summary>현재 프로토콜 버전</summary>
    public const string PROTOCOL_VERSION = "1.0.0";
    
    /// <summary>지원 프로토콜 버전 목록</summary>
    public static readonly string[] SUPPORTED_VERSIONS = { "1.0.0" };
    
    /// <summary>최대 메시지 크기 (1MB)</summary>
    public const int MAX_MESSAGE_SIZE = 1024 * 1024;
    
    /// <summary>최대 페이로드 크기 (900KB)</summary>
    public const int MAX_PAYLOAD_SIZE = 900 * 1024;
    
    /// <summary>메시지 타임아웃 (30초)</summary>
    public const int MESSAGE_TIMEOUT_SECONDS = 30;
    
    /// <summary>최대 플레이어 수</summary>
    public const int MAX_PLAYERS = 4;
    
    /// <summary>최소 플레이어 수</summary>
    public const int MIN_PLAYERS = 2;
    #endregion

    #region Message Types
    /// <summary>유효한 메시지 타입 목록</summary>
    public static readonly HashSet<string> VALID_MESSAGE_TYPES = new()
    {
        // 매칭 요청
        "join_queue",           // 랜덤 매칭 대기열 참가
        "leave_queue",          // 대기열 나가기
        "room_create",          // 방 생성
        "room_join",           // 방 참가
        "room_leave",          // 방 나가기
        "tournament_join",     // 토너먼트 참가
        "matching_cancel",     // 매칭 취소
        
        // 매칭 응답
        "queue_status",        // 대기열 상태
        "match_found",         // 매칭 성공
        "room_created",        // 방 생성 완료
        "room_joined",         // 방 참가 완료
        "room_left",           // 방 나가기 완료
        "match_cancelled",     // 매칭 취소 완료
        "match_error",         // 매칭 오류
        
        // 방 관리
        "room_update",         // 방 정보 업데이트
        "player_joined",       // 새 플레이어 참가
        "player_left",         // 플레이어 나가기
        "room_ready_check",    // 준비 확인
        "game_start",          // 게임 시작
        
        // 시스템
        "heartbeat",           // 하트비트
        "pong",                // 하트비트 응답
        "protocol_error",      // 프로토콜 오류
        "server_message"       // 서버 공지
    };

    /// <summary>클라이언트 전송 메시지 타입</summary>
    public static readonly HashSet<string> CLIENT_MESSAGE_TYPES = new()
    {
        "join_queue", "leave_queue", "room_create", "room_join", 
        "room_leave", "tournament_join", "matching_cancel", "heartbeat", "pong"
    };

    /// <summary>서버 전송 메시지 타입</summary>
    public static readonly HashSet<string> SERVER_MESSAGE_TYPES = new()
    {
        "queue_status", "match_found", "room_created", "room_joined", 
        "room_left", "match_cancelled", "match_error", "room_update", 
        "player_joined", "player_left", "room_ready_check", "game_start", 
        "heartbeat", "pong", "protocol_error", "server_message"
    };
    #endregion

    #region Validation Methods
    /// <summary>
    /// 메시지 타입이 유효한지 확인
    /// </summary>
    /// <param name="messageType">확인할 메시지 타입</param>
    /// <returns>유효한 메시지 타입인지 여부</returns>
    public static bool IsValidMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && VALID_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>
    /// 클라이언트에서 전송 가능한 메시지 타입인지 확인
    /// </summary>
    /// <param name="messageType">확인할 메시지 타입</param>
    /// <returns>클라이언트 전송 가능 여부</returns>
    public static bool IsClientMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && CLIENT_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>
    /// 서버에서 전송 가능한 메시지 타입인지 확인
    /// </summary>
    /// <param name="messageType">확인할 메시지 타입</param>
    /// <returns>서버 전송 가능 여부</returns>
    public static bool IsServerMessageType(string messageType)
    {
        return !string.IsNullOrEmpty(messageType) && SERVER_MESSAGE_TYPES.Contains(messageType.ToLower());
    }

    /// <summary>
    /// 프로토콜 버전 호환성 확인
    /// </summary>
    /// <param name="version">확인할 버전</param>
    /// <returns>호환 가능한 버전인지 여부</returns>
    public static bool IsCompatibleVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return false;

        return SUPPORTED_VERSIONS.Contains(version);
    }

    /// <summary>
    /// 메시지 크기 제한 확인
    /// </summary>
    /// <param name="messageJson">JSON 메시지</param>
    /// <returns>크기 제한을 만족하는지 여부</returns>
    public static bool IsWithinSizeLimit(string messageJson)
    {
        if (string.IsNullOrEmpty(messageJson))
            return true;

        int size = System.Text.Encoding.UTF8.GetByteCount(messageJson);
        return size <= MAX_MESSAGE_SIZE;
    }

    /// <summary>
    /// 플레이어 수 유효성 확인
    /// </summary>
    /// <param name="playerCount">플레이어 수</param>
    /// <returns>유효한 플레이어 수인지 여부</returns>
    public static bool IsValidPlayerCount(int playerCount)
    {
        return playerCount >= MIN_PLAYERS && playerCount <= MAX_PLAYERS;
    }
    #endregion

    #region Serialization Methods
    /// <summary>
    /// 매칭 요청을 JSON으로 직렬화
    /// </summary>
    /// <param name="request">매칭 요청</param>
    /// <returns>JSON 문자열</returns>
    public static string SerializeRequest(MatchingRequest request)
    {
        if (request == null)
        {
            Debug.LogError("[MatchingProtocol] Cannot serialize null request");
            return null;
        }

        if (!request.IsValid())
        {
            Debug.LogError("[MatchingProtocol] Request validation failed");
            return null;
        }

        try
        {
            var message = request.ToMessage();
            string json = message.ToJson();
            
            if (!IsWithinSizeLimit(json))
            {
                Debug.LogError("[MatchingProtocol] Request exceeds size limit");
                return null;
            }
            
            return json;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingProtocol] Request serialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 매칭 응답을 JSON으로 직렬화
    /// </summary>
    /// <param name="response">매칭 응답</param>
    /// <returns>JSON 문자열</returns>
    public static string SerializeResponse(MatchingResponse response)
    {
        if (response == null)
        {
            Debug.LogError("[MatchingProtocol] Cannot serialize null response");
            return null;
        }

        if (!response.IsValid())
        {
            Debug.LogError("[MatchingProtocol] Response validation failed");
            return null;
        }

        try
        {
            var message = response.ToMessage();
            string json = message.ToJson();
            
            if (!IsWithinSizeLimit(json))
            {
                Debug.LogError("[MatchingProtocol] Response exceeds size limit");
                return null;
            }
            
            return json;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingProtocol] Response serialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 매칭 메시지를 JSON으로 직렬화
    /// </summary>
    /// <param name="message">매칭 메시지</param>
    /// <returns>JSON 문자열</returns>
    public static string SerializeMessage(MatchingMessage message)
    {
        if (message == null)
        {
            Debug.LogError("[MatchingProtocol] Cannot serialize null message");
            return null;
        }

        if (!message.IsValid())
        {
            Debug.LogError("[MatchingProtocol] Message validation failed");
            return null;
        }

        if (!message.IsWithinSizeLimit())
        {
            Debug.LogError("[MatchingProtocol] Message exceeds size limit");
            return null;
        }

        return message.ToJson();
    }
    #endregion

    #region Deserialization Methods
    /// <summary>
    /// JSON에서 매칭 메시지로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 메시지</returns>
    public static MatchingMessage DeserializeMessage(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[MatchingProtocol] Cannot deserialize null or empty JSON");
            return null;
        }

        if (!IsWithinSizeLimit(json))
        {
            Debug.LogError("[MatchingProtocol] Message size exceeds limit");
            return null;
        }

        var message = MatchingMessage.FromJson(json);
        if (message == null)
        {
            Debug.LogError("[MatchingProtocol] Failed to deserialize message");
            return null;
        }

        if (message.IsExpired())
        {
            Debug.LogWarning("[MatchingProtocol] Received expired message");
            return null;
        }

        return message;
    }

    /// <summary>
    /// JSON에서 매칭 요청으로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 요청</returns>
    public static MatchingRequest DeserializeRequest(string json)
    {
        var message = DeserializeMessage(json);
        if (message == null)
            return null;

        if (!IsClientMessageType(message.type))
        {
            Debug.LogError($"[MatchingProtocol] Invalid client message type: {message.type}");
            return null;
        }

        return message.GetPayload<MatchingRequest>();
    }

    /// <summary>
    /// JSON에서 매칭 응답으로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 응답</returns>
    public static MatchingResponse DeserializeResponse(string json)
    {
        var message = DeserializeMessage(json);
        if (message == null)
            return null;

        if (!IsServerMessageType(message.type))
        {
            Debug.LogError($"[MatchingProtocol] Invalid server message type: {message.type}");
            return null;
        }

        return message.GetPayload<MatchingResponse>();
    }
    #endregion

    #region Message Factory Methods
    /// <summary>
    /// 랜덤 매칭 요청 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <returns>JSON 메시지</returns>
    public static string CreateJoinQueueMessage(string playerId, int playerCount, string gameMode = "classic", int betAmount = 0)
    {
        var request = new MatchingRequest(playerId, playerCount, gameMode, betAmount);
        return SerializeRequest(request);
    }

    /// <summary>
    /// 방 생성 요청 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="playerCount">최대 플레이어 수</param>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="betAmount">베팅 금액</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    /// <returns>JSON 메시지</returns>
    public static string CreateRoomCreateMessage(string playerId, int playerCount, string gameMode = "classic", int betAmount = 0, bool isPrivate = false)
    {
        var request = MatchingRequest.CreateRoom(playerId, playerCount, gameMode, betAmount, isPrivate);
        return SerializeRequest(request);
    }

    /// <summary>
    /// 방 참가 요청 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <param name="roomCode">방 코드</param>
    /// <returns>JSON 메시지</returns>
    public static string CreateRoomJoinMessage(string playerId, string roomCode)
    {
        var request = new MatchingRequest(playerId, roomCode);
        return SerializeRequest(request);
    }

    /// <summary>
    /// 매칭 취소 요청 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>JSON 메시지</returns>
    public static string CreateCancelMessage(string playerId)
    {
        var message = new MatchingMessage("matching_cancel", new { playerId }, 2);
        return SerializeMessage(message);
    }

    /// <summary>
    /// 하트비트 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>JSON 메시지</returns>
    public static string CreateHeartbeatMessage(string playerId = "")
    {
        var payload = string.IsNullOrEmpty(playerId) ? new { } : new { playerId };
        var message = new MatchingMessage("heartbeat", payload, 0);
        return SerializeMessage(message);
    }

    /// <summary>
    /// 하트비트 응답 메시지 생성
    /// </summary>
    /// <param name="playerId">플레이어 ID</param>
    /// <returns>JSON 메시지</returns>
    public static string CreatePongMessage(string playerId = "")
    {
        var payload = string.IsNullOrEmpty(playerId) ? new { } : new { playerId };
        var message = new MatchingMessage("pong", payload, 0);
        return SerializeMessage(message);
    }
    #endregion

    #region Error Handling
    /// <summary>
    /// 프로토콜 에러 메시지 생성
    /// </summary>
    /// <param name="errorCode">에러 코드</param>
    /// <param name="errorMessage">에러 메시지</param>
    /// <param name="originalMessageId">원본 메시지 ID</param>
    /// <returns>에러 메시지</returns>
    public static string CreateProtocolErrorMessage(string errorCode, string errorMessage, string originalMessageId = "")
    {
        var payload = new 
        { 
            errorCode, 
            errorMessage, 
            originalMessageId,
            timestamp = DateTime.UtcNow.ToString("O")
        };
        
        var message = new MatchingMessage("protocol_error", payload, 3);
        return SerializeMessage(message);
    }

    /// <summary>
    /// 잘못된 메시지 타입 에러 생성
    /// </summary>
    /// <param name="invalidType">잘못된 타입</param>
    /// <param name="originalMessageId">원본 메시지 ID</param>
    /// <returns>에러 메시지</returns>
    public static string CreateInvalidMessageTypeError(string invalidType, string originalMessageId = "")
    {
        return CreateProtocolErrorMessage(
            "INVALID_MESSAGE_TYPE",
            $"Invalid message type: {invalidType}",
            originalMessageId
        );
    }

    /// <summary>
    /// 버전 호환성 에러 생성
    /// </summary>
    /// <param name="clientVersion">클라이언트 버전</param>
    /// <param name="originalMessageId">원본 메시지 ID</param>
    /// <returns>에러 메시지</returns>
    public static string CreateVersionMismatchError(string clientVersion, string originalMessageId = "")
    {
        return CreateProtocolErrorMessage(
            "VERSION_MISMATCH",
            $"Unsupported protocol version: {clientVersion}. Supported versions: {string.Join(", ", SUPPORTED_VERSIONS)}",
            originalMessageId
        );
    }

    /// <summary>
    /// 메시지 크기 초과 에러 생성
    /// </summary>
    /// <param name="actualSize">실제 크기</param>
    /// <param name="originalMessageId">원본 메시지 ID</param>
    /// <returns>에러 메시지</returns>
    public static string CreateMessageTooLargeError(int actualSize, string originalMessageId = "")
    {
        return CreateProtocolErrorMessage(
            "MESSAGE_TOO_LARGE",
            $"Message size {actualSize} bytes exceeds limit {MAX_MESSAGE_SIZE} bytes",
            originalMessageId
        );
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 메시지 타입에서 기대되는 페이로드 타입 반환
    /// </summary>
    /// <param name="messageType">메시지 타입</param>
    /// <returns>페이로드 타입</returns>
    public static Type GetExpectedPayloadType(string messageType)
    {
        return messageType?.ToLower() switch
        {
            "join_queue" or "room_create" or "room_join" or "tournament_join" => typeof(MatchingRequest),
            "queue_status" or "match_found" or "room_created" or "room_joined" 
                or "match_cancelled" or "match_error" => typeof(MatchingResponse),
            "heartbeat" or "pong" => typeof(object),
            _ => typeof(object)
        };
    }

    /// <summary>
    /// 메시지 통계 정보
    /// </summary>
    /// <returns>통계 정보</returns>
    public static string GetProtocolStats()
    {
        return $"Protocol Version: {PROTOCOL_VERSION}\n" +
               $"Supported Versions: {string.Join(", ", SUPPORTED_VERSIONS)}\n" +
               $"Message Types: {VALID_MESSAGE_TYPES.Count}\n" +
               $"Max Message Size: {MAX_MESSAGE_SIZE / 1024} KB\n" +
               $"Max Payload Size: {MAX_PAYLOAD_SIZE / 1024} KB";
    }

    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    /// <param name="message">메시지</param>
    /// <param name="prefix">접두사</param>
    public static void LogDebugInfo(MatchingMessage message, string prefix = "[MatchingProtocol]")
    {
        if (message == null)
        {
            Debug.Log($"{prefix} Message is null");
            return;
        }

        Debug.Log($"{prefix} {message.GetSummary()}");
        
        if (!string.IsNullOrEmpty(message.payload))
        {
            Debug.Log($"{prefix} Payload: {message.payload.Substring(0, Math.Min(200, message.payload.Length))}...");
        }
    }
    #endregion
}