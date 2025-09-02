using System;
using UnityEngine;

/// <summary>
/// 매칭 요청 데이터 구조
/// 클라이언트에서 서버로 전송하는 매칭 관련 요청들을 정의
/// </summary>
[Serializable]
public class MatchingRequest
{
    #region Fields
    /// <summary>플레이어 ID</summary>
    [SerializeField] public string playerId;
    
    /// <summary>매칭 유형 ("random", "room", "tournament")</summary>
    [SerializeField] public string matchType;
    
    /// <summary>방 참가자 수 (2-4명)</summary>
    [SerializeField] public int playerCount;
    
    /// <summary>방 코드 (방 참가용)</summary>
    [SerializeField] public string roomCode;
    
    /// <summary>게임 모드</summary>
    [SerializeField] public string gameMode;
    
    /// <summary>베팅 금액</summary>
    [SerializeField] public int betAmount;
    
    /// <summary>플레이어 레벨 (매칭용)</summary>
    [SerializeField] public int playerLevel;
    
    /// <summary>선호 언어</summary>
    [SerializeField] public string language;
    
    /// <summary>요청 시간</summary>
    [SerializeField] public string requestTime;
    
    /// <summary>추가 옵션 (JSON 형태)</summary>
    [SerializeField] public string options;
    #endregion

    #region Constructor
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MatchingRequest()
    {
        requestTime = DateTime.UtcNow.ToString("O");
        playerCount = 2; // 기본값
        gameMode = "classic";
        language = "ko";
    }

    /// <summary>
    /// 랜덤 매칭 요청 생성자
    /// </summary>
    /// <param name="playerIdValue">플레이어 ID</param>
    /// <param name="playerCountValue">플레이어 수</param>
    /// <param name="gameModeValue">게임 모드</param>
    /// <param name="betAmountValue">베팅 금액</param>
    public MatchingRequest(string playerIdValue, int playerCountValue, string gameModeValue = "classic", int betAmountValue = 0) : this()
    {
        playerId = playerIdValue;
        matchType = "random";
        playerCount = playerCountValue;
        gameMode = gameModeValue;
        betAmount = betAmountValue;
    }

    /// <summary>
    /// 방 참가 요청 생성자
    /// </summary>
    /// <param name="playerIdValue">플레이어 ID</param>
    /// <param name="roomCodeValue">방 코드</param>
    public MatchingRequest(string playerIdValue, string roomCodeValue) : this()
    {
        playerId = playerIdValue;
        matchType = "room";
        roomCode = roomCodeValue;
    }

    /// <summary>
    /// 방 생성 요청 생성자
    /// </summary>
    /// <param name="playerIdValue">플레이어 ID</param>
    /// <param name="playerCountValue">최대 플레이어 수</param>
    /// <param name="gameModeValue">게임 모드</param>
    /// <param name="betAmountValue">베팅 금액</param>
    /// <param name="isPrivate">비공개 방 여부</param>
    public static MatchingRequest CreateRoom(string playerIdValue, int playerCountValue, string gameModeValue = "classic", int betAmountValue = 0, bool isPrivate = false)
    {
        var request = new MatchingRequest(playerIdValue, playerCountValue, gameModeValue, betAmountValue);
        request.matchType = "room_create";
        
        if (isPrivate)
        {
            request.options = JsonUtility.ToJson(new RoomOptions { isPrivate = true });
        }
        
        return request;
    }
    #endregion

    #region Validation
    /// <summary>
    /// 요청 유효성 검증
    /// </summary>
    /// <returns>유효한 요청인지 여부</returns>
    public bool IsValid()
    {
        // 필수 필드 검증
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("[MatchingRequest] Player ID is required");
            return false;
        }

        if (string.IsNullOrEmpty(matchType))
        {
            Debug.LogError("[MatchingRequest] Match type is required");
            return false;
        }

        // 매칭 타입별 검증
        switch (matchType.ToLower())
        {
            case "random":
                return ValidateRandomMatch();
                
            case "room":
                return ValidateRoomJoin();
                
            case "room_create":
                return ValidateRoomCreate();
                
            case "tournament":
                return ValidateTournament();
                
            default:
                Debug.LogError($"[MatchingRequest] Invalid match type: {matchType}");
                return false;
        }
    }

    /// <summary>
    /// 랜덤 매칭 검증
    /// </summary>
    private bool ValidateRandomMatch()
    {
        if (playerCount < 2 || playerCount > 4)
        {
            Debug.LogError($"[MatchingRequest] Invalid player count for random match: {playerCount} (must be 2-4)");
            return false;
        }

        if (betAmount < 0)
        {
            Debug.LogError($"[MatchingRequest] Invalid bet amount: {betAmount} (must be >= 0)");
            return false;
        }

        if (string.IsNullOrEmpty(gameMode))
        {
            Debug.LogError("[MatchingRequest] Game mode is required for random match");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 방 참가 검증
    /// </summary>
    private bool ValidateRoomJoin()
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogError("[MatchingRequest] Room code is required for room join");
            return false;
        }

        if (roomCode.Length < 4 || roomCode.Length > 10)
        {
            Debug.LogError($"[MatchingRequest] Invalid room code length: {roomCode.Length} (must be 4-10 characters)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 방 생성 검증
    /// </summary>
    private bool ValidateRoomCreate()
    {
        if (playerCount < 2 || playerCount > 4)
        {
            Debug.LogError($"[MatchingRequest] Invalid player count for room create: {playerCount} (must be 2-4)");
            return false;
        }

        if (betAmount < 0)
        {
            Debug.LogError($"[MatchingRequest] Invalid bet amount: {betAmount} (must be >= 0)");
            return false;
        }

        if (string.IsNullOrEmpty(gameMode))
        {
            Debug.LogError("[MatchingRequest] Game mode is required for room create");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 토너먼트 검증
    /// </summary>
    private bool ValidateTournament()
    {
        if (betAmount <= 0)
        {
            Debug.LogError($"[MatchingRequest] Tournament requires bet amount > 0: {betAmount}");
            return false;
        }

        if (playerLevel < 1)
        {
            Debug.LogError($"[MatchingRequest] Invalid player level for tournament: {playerLevel}");
            return false;
        }

        return true;
    }
    #endregion

    #region Serialization
    /// <summary>
    /// 요청을 JSON으로 직렬화
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
            Debug.LogError($"[MatchingRequest] Serialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON에서 요청으로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 요청 객체</returns>
    public static MatchingRequest FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[MatchingRequest] Cannot deserialize null or empty JSON");
            return null;
        }

        try
        {
            var request = JsonUtility.FromJson<MatchingRequest>(json);
            
            if (request != null && !request.IsValid())
            {
                Debug.LogError("[MatchingRequest] Deserialized request failed validation");
                return null;
            }
            
            return request;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingRequest] Deserialization failed: {e.Message}");
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
        switch (matchType?.ToLower())
        {
            case "random":
                return "join_queue";
            case "room":
                return "room_join";
            case "room_create":
                return "room_create";
            case "tournament":
                return "tournament_join";
            default:
                return "matching_request";
        }
    }

    /// <summary>
    /// 메시지 우선순위 결정
    /// </summary>
    /// <returns>우선순위 (높을수록 우선)</returns>
    private int GetMessagePriority()
    {
        switch (matchType?.ToLower())
        {
            case "tournament":
                return 3; // 높은 우선순위
            case "room":
                return 2; // 중간 우선순위
            case "room_create":
                return 2;
            case "random":
                return 1; // 일반 우선순위
            default:
                return 0; // 기본 우선순위
        }
    }

    /// <summary>
    /// 요청 요약 정보
    /// </summary>
    /// <returns>요약 정보</returns>
    public string GetSummary()
    {
        return $"Player: {playerId}, Type: {matchType}, Count: {playerCount}, Bet: {betAmount}";
    }

    /// <summary>
    /// 옵션 객체 가져오기
    /// </summary>
    /// <typeparam name="T">옵션 타입</typeparam>
    /// <returns>옵션 객체</returns>
    public T GetOptions<T>() where T : class
    {
        if (string.IsNullOrEmpty(options))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(options);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingRequest] Options deserialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 옵션 설정
    /// </summary>
    /// <param name="optionObject">옵션 객체</param>
    public void SetOptions(object optionObject)
    {
        if (optionObject == null)
        {
            options = null;
            return;
        }

        try
        {
            options = JsonUtility.ToJson(optionObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingRequest] Options serialization failed: {e.Message}");
        }
    }
    #endregion

    #region ToString Override
    /// <summary>
    /// 문자열 표현
    /// </summary>
    /// <returns>요청 정보</returns>
    public override string ToString()
    {
        return $"MatchingRequest({GetSummary()})";
    }
    #endregion
}

/// <summary>
/// 방 생성 옵션
/// </summary>
[Serializable]
public class RoomOptions
{
    public bool isPrivate = false;
    public string password = "";
    public int maxSpectators = 0;
    public bool allowReconnection = true;
    public int turnTimeLimit = 30;
    public bool enableChat = true;
}

/// <summary>
/// 토너먼트 옵션
/// </summary>
[Serializable]
public class TournamentOptions
{
    public string tournamentId = "";
    public int entryFee = 0;
    public string prizeStructure = "";
    public int maxParticipants = 0;
}