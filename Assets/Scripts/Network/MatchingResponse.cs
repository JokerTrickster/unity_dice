using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 응답 데이터 구조
/// 서버에서 클라이언트로 전송되는 매칭 관련 응답들을 정의
/// </summary>
[Serializable]
public class MatchingResponse
{
    #region Fields
    /// <summary>응답 성공 여부</summary>
    [SerializeField] public bool success;
    
    /// <summary>응답 메시지</summary>
    [SerializeField] public string message;
    
    /// <summary>에러 코드</summary>
    [SerializeField] public string errorCode;
    
    /// <summary>방 ID</summary>
    [SerializeField] public string roomId;
    
    /// <summary>방 코드 (사용자가 볼 수 있는 짧은 코드)</summary>
    [SerializeField] public string roomCode;
    
    /// <summary>매칭 상태</summary>
    [SerializeField] public string status;
    
    /// <summary>현재 대기 위치</summary>
    [SerializeField] public int queuePosition;
    
    /// <summary>예상 대기 시간(초)</summary>
    [SerializeField] public int estimatedWaitTime;
    
    /// <summary>매칭된 플레이어들</summary>
    [SerializeField] public List<PlayerInfo> players;
    
    /// <summary>게임 정보</summary>
    [SerializeField] public GameInfo gameInfo;
    
    /// <summary>서버 시간</summary>
    [SerializeField] public string serverTime;
    
    /// <summary>추가 데이터 (JSON 형태)</summary>
    [SerializeField] public string additionalData;
    #endregion

    #region Constructor
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MatchingResponse()
    {
        serverTime = DateTime.UtcNow.ToString("O");
        players = new List<PlayerInfo>();
        gameInfo = new GameInfo();
    }

    /// <summary>
    /// 성공 응답 생성자
    /// </summary>
    /// <param name="successMessage">성공 메시지</param>
    /// <param name="matchingStatus">매칭 상태</param>
    public MatchingResponse(string successMessage, string matchingStatus) : this()
    {
        success = true;
        message = successMessage;
        status = matchingStatus;
    }

    /// <summary>
    /// 실패 응답 생성자
    /// </summary>
    /// <param name="errorMessage">에러 메시지</param>
    /// <param name="error">에러 코드</param>
    public MatchingResponse(string errorMessage, string error) : this()
    {
        success = false;
        message = errorMessage;
        errorCode = error;
        status = "failed";
    }
    #endregion

    #region Factory Methods
    /// <summary>
    /// 대기열 응답 생성
    /// </summary>
    /// <param name="position">대기 위치</param>
    /// <param name="waitTime">예상 대기 시간</param>
    /// <returns>대기열 응답</returns>
    public static MatchingResponse CreateQueueResponse(int position, int waitTime)
    {
        return new MatchingResponse("매칭 대기 중", "queued")
        {
            queuePosition = position,
            estimatedWaitTime = waitTime
        };
    }

    /// <summary>
    /// 매칭 성공 응답 생성
    /// </summary>
    /// <param name="roomIdValue">방 ID</param>
    /// <param name="matchedPlayers">매칭된 플레이어들</param>
    /// <param name="game">게임 정보</param>
    /// <returns>매칭 성공 응답</returns>
    public static MatchingResponse CreateMatchFoundResponse(string roomIdValue, List<PlayerInfo> matchedPlayers, GameInfo game)
    {
        return new MatchingResponse("매칭이 완료되었습니다", "matched")
        {
            roomId = roomIdValue,
            players = matchedPlayers ?? new List<PlayerInfo>(),
            gameInfo = game ?? new GameInfo()
        };
    }

    /// <summary>
    /// 방 생성 응답 생성
    /// </summary>
    /// <param name="roomIdValue">방 ID</param>
    /// <param name="roomCodeValue">방 코드</param>
    /// <param name="host">방장 정보</param>
    /// <returns>방 생성 응답</returns>
    public static MatchingResponse CreateRoomCreatedResponse(string roomIdValue, string roomCodeValue, PlayerInfo host)
    {
        var players = new List<PlayerInfo>();
        if (host != null)
        {
            players.Add(host);
        }

        return new MatchingResponse("방이 생성되었습니다", "room_created")
        {
            roomId = roomIdValue,
            roomCode = roomCodeValue,
            players = players
        };
    }

    /// <summary>
    /// 방 참가 응답 생성
    /// </summary>
    /// <param name="roomIdValue">방 ID</param>
    /// <param name="currentPlayers">현재 방 참가자들</param>
    /// <param name="game">게임 정보</param>
    /// <returns>방 참가 응답</returns>
    public static MatchingResponse CreateRoomJoinedResponse(string roomIdValue, List<PlayerInfo> currentPlayers, GameInfo game)
    {
        return new MatchingResponse("방에 참가했습니다", "room_joined")
        {
            roomId = roomIdValue,
            players = currentPlayers ?? new List<PlayerInfo>(),
            gameInfo = game ?? new GameInfo()
        };
    }

    /// <summary>
    /// 매칭 취소 응답 생성
    /// </summary>
    /// <returns>매칭 취소 응답</returns>
    public static MatchingResponse CreateCancelledResponse()
    {
        return new MatchingResponse("매칭이 취소되었습니다", "cancelled");
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    /// <param name="error">에러 코드</param>
    /// <param name="errorMessage">에러 메시지</param>
    /// <returns>에러 응답</returns>
    public static MatchingResponse CreateErrorResponse(MatchingErrorCode error, string errorMessage = "")
    {
        string message = !string.IsNullOrEmpty(errorMessage) ? errorMessage : GetDefaultErrorMessage(error);
        return new MatchingResponse(message, error.ToString());
    }
    #endregion

    #region Validation
    /// <summary>
    /// 응답 유효성 검증
    /// </summary>
    /// <returns>유효한 응답인지 여부</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(status))
        {
            Debug.LogError("[MatchingResponse] Status is required");
            return false;
        }

        if (string.IsNullOrEmpty(serverTime))
        {
            Debug.LogError("[MatchingResponse] Server time is required");
            return false;
        }

        // 상태별 검증
        switch (status.ToLower())
        {
            case "matched":
            case "room_created":
            case "room_joined":
                if (string.IsNullOrEmpty(roomId))
                {
                    Debug.LogError($"[MatchingResponse] Room ID is required for status: {status}");
                    return false;
                }
                break;

            case "queued":
                if (queuePosition < 0)
                {
                    Debug.LogError($"[MatchingResponse] Invalid queue position: {queuePosition}");
                    return false;
                }
                break;

            case "failed":
                if (!success && string.IsNullOrEmpty(errorCode))
                {
                    Debug.LogError("[MatchingResponse] Error code is required for failed status");
                    return false;
                }
                break;
        }

        return true;
    }

    /// <summary>
    /// 응답 만료 여부 확인
    /// </summary>
    /// <param name="timeoutSeconds">타임아웃 시간(초)</param>
    /// <returns>만료된 응답인지 여부</returns>
    public bool IsExpired(int timeoutSeconds = 60)
    {
        try
        {
            DateTime responseTime = DateTime.Parse(serverTime);
            TimeSpan elapsed = DateTime.UtcNow - responseTime;
            bool isExpired = elapsed.TotalSeconds > timeoutSeconds;
            
            if (isExpired)
            {
                Debug.LogWarning($"[MatchingResponse] Response expired (elapsed: {elapsed.TotalSeconds:F1}s)");
            }
            
            return isExpired;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingResponse] Invalid server time format: {serverTime} - {e.Message}");
            return true;
        }
    }
    #endregion

    #region Serialization
    /// <summary>
    /// 응답을 JSON으로 직렬화
    /// </summary>
    /// <returns>JSON 문자열</returns>
    public string ToJson()
    {
        try
        {
            return JsonUtility.ToJson(this);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingResponse] Serialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON에서 응답으로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 응답 객체</returns>
    public static MatchingResponse FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[MatchingResponse] Cannot deserialize null or empty JSON");
            return null;
        }

        try
        {
            var response = JsonUtility.FromJson<MatchingResponse>(json);
            
            if (response != null && !response.IsValid())
            {
                Debug.LogError("[MatchingResponse] Deserialized response failed validation");
                return null;
            }
            
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingResponse] Deserialization failed: {e.Message}");
            return null;
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 매칭 메시지로 변환
    /// </summary>
    /// <returns>매칭 메시지</returns>
    public MatchingMessage ToMessage()
    {
        string messageType = GetMessageType();
        return new MatchingMessage(messageType, this, GetMessagePriority());
    }

    /// <summary>
    /// 메시지 타입 결정
    /// </summary>
    /// <returns>메시지 타입</returns>
    private string GetMessageType()
    {
        switch (status?.ToLower())
        {
            case "queued":
                return "queue_status";
            case "matched":
                return "match_found";
            case "room_created":
                return "room_created";
            case "room_joined":
                return "room_joined";
            case "cancelled":
                return "match_cancelled";
            case "failed":
                return "match_error";
            default:
                return "matching_response";
        }
    }

    /// <summary>
    /// 메시지 우선순위 결정
    /// </summary>
    /// <returns>우선순위</returns>
    private int GetMessagePriority()
    {
        switch (status?.ToLower())
        {
            case "matched":
                return 5; // 최고 우선순위
            case "room_created":
            case "room_joined":
                return 4; // 높은 우선순위
            case "failed":
                return 3; // 중간 우선순위
            case "cancelled":
                return 2; // 낮은 우선순위
            case "queued":
                return 1; // 가장 낮은 우선순위
            default:
                return 0; // 기본 우선순위
        }
    }

    /// <summary>
    /// 추가 데이터 가져오기
    /// </summary>
    /// <typeparam name="T">데이터 타입</typeparam>
    /// <returns>추가 데이터</returns>
    public T GetAdditionalData<T>() where T : class
    {
        if (string.IsNullOrEmpty(additionalData))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(additionalData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingResponse] Additional data deserialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 추가 데이터 설정
    /// </summary>
    /// <param name="dataObject">데이터 객체</param>
    public void SetAdditionalData(object dataObject)
    {
        if (dataObject == null)
        {
            additionalData = null;
            return;
        }

        try
        {
            additionalData = JsonUtility.ToJson(dataObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingResponse] Additional data serialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// 응답 요약 정보
    /// </summary>
    /// <returns>요약 정보</returns>
    public string GetSummary()
    {
        string playerCount = players?.Count.ToString() ?? "0";
        return $"Status: {status}, Success: {success}, Players: {playerCount}, Room: {roomId ?? "N/A"}";
    }

    /// <summary>
    /// 기본 에러 메시지 반환
    /// </summary>
    /// <param name="errorCode">에러 코드</param>
    /// <returns>기본 에러 메시지</returns>
    private static string GetDefaultErrorMessage(MatchingErrorCode errorCode)
    {
        return errorCode switch
        {
            MatchingErrorCode.ROOM_NOT_FOUND => "방을 찾을 수 없습니다",
            MatchingErrorCode.ROOM_FULL => "방이 가득 찼습니다",
            MatchingErrorCode.INVALID_ROOM_CODE => "잘못된 방 코드입니다",
            MatchingErrorCode.PLAYER_ALREADY_IN_ROOM => "이미 방에 참가 중입니다",
            MatchingErrorCode.INSUFFICIENT_BALANCE => "잔액이 부족합니다",
            MatchingErrorCode.PLAYER_LEVEL_MISMATCH => "플레이어 레벨이 맞지 않습니다",
            MatchingErrorCode.MATCHING_TIMEOUT => "매칭 시간이 초과되었습니다",
            MatchingErrorCode.SERVER_ERROR => "서버 오류가 발생했습니다",
            MatchingErrorCode.MAINTENANCE => "서버 점검 중입니다",
            MatchingErrorCode.INVALID_REQUEST => "잘못된 요청입니다",
            _ => "알 수 없는 오류가 발생했습니다"
        };
    }
    #endregion

    #region ToString Override
    /// <summary>
    /// 문자열 표현
    /// </summary>
    /// <returns>응답 정보</returns>
    public override string ToString()
    {
        return $"MatchingResponse({GetSummary()})";
    }
    #endregion
}

/// <summary>
/// 플레이어 정보
/// </summary>
[Serializable]
public class PlayerInfo
{
    [SerializeField] public string playerId;
    [SerializeField] public string nickname;
    [SerializeField] public int level;
    [SerializeField] public string avatar;
    [SerializeField] public bool isHost;
    [SerializeField] public bool isReady;
    [SerializeField] public int rating;
    [SerializeField] public string status;

    public PlayerInfo()
    {
        status = "connected";
        isReady = false;
    }

    public PlayerInfo(string playerIdValue, string nicknameValue) : this()
    {
        playerId = playerIdValue;
        nickname = nicknameValue;
    }
}

/// <summary>
/// 게임 정보
/// </summary>
[Serializable]
public class GameInfo
{
    [SerializeField] public string gameMode;
    [SerializeField] public int betAmount;
    [SerializeField] public int turnTimeLimit;
    [SerializeField] public int maxPlayers;
    [SerializeField] public int currentPlayers;
    [SerializeField] public string language;
    [SerializeField] public bool enableChat;
    [SerializeField] public bool allowSpectators;

    public GameInfo()
    {
        gameMode = "classic";
        turnTimeLimit = 30;
        maxPlayers = 2;
        currentPlayers = 0;
        language = "ko";
        enableChat = true;
        allowSpectators = false;
    }
}

/// <summary>
/// 매칭 에러 코드
/// </summary>
public enum MatchingErrorCode
{
    UNKNOWN,
    ROOM_NOT_FOUND,
    ROOM_FULL,
    INVALID_ROOM_CODE,
    PLAYER_ALREADY_IN_ROOM,
    INSUFFICIENT_BALANCE,
    PLAYER_LEVEL_MISMATCH,
    MATCHING_TIMEOUT,
    SERVER_ERROR,
    MAINTENANCE,
    INVALID_REQUEST,
    NETWORK_ERROR,
    AUTHENTICATION_FAILED
}