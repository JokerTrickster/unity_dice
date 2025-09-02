using System;
using UnityEngine;

/// <summary>
/// 매칭 시스템의 기본 메시지 구조
/// WebSocket을 통해 전송되는 모든 매칭 관련 메시지의 기본 형태
/// </summary>
[Serializable]
public class MatchingMessage
{
    #region Fields
    /// <summary>메시지 타입 식별자</summary>
    [SerializeField] public string type;
    
    /// <summary>메시지 고유 ID</summary>
    [SerializeField] public string messageId;
    
    /// <summary>메시지 페이로드 (JSON 문자열)</summary>
    [SerializeField] public string payload;
    
    /// <summary>메시지 생성 시간 (ISO 8601 형식)</summary>
    [SerializeField] public string timestamp;
    
    /// <summary>프로토콜 버전</summary>
    [SerializeField] public string version;
    
    /// <summary>메시지 우선순위</summary>
    [SerializeField] public int priority;
    #endregion

    #region Constructor
    /// <summary>
    /// 기본 생성자 (Unity JSON 직렬화용)
    /// </summary>
    public MatchingMessage()
    {
        messageId = Guid.NewGuid().ToString();
        timestamp = DateTime.UtcNow.ToString("O");
        version = MatchingProtocol.PROTOCOL_VERSION;
        priority = 0;
    }

    /// <summary>
    /// 매칭 메시지 생성자
    /// </summary>
    /// <param name="messageType">메시지 타입</param>
    /// <param name="payloadData">페이로드 데이터</param>
    /// <param name="messagePriority">메시지 우선순위</param>
    public MatchingMessage(string messageType, object payloadData, int messagePriority = 0) : this()
    {
        type = messageType;
        priority = messagePriority;
        
        if (payloadData != null)
        {
            payload = JsonUtility.ToJson(payloadData);
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// 메시지 유효성 검증
    /// </summary>
    /// <returns>유효한 메시지인지 여부</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(type))
        {
            Debug.LogError("[MatchingMessage] Message type is required");
            return false;
        }

        if (string.IsNullOrEmpty(messageId))
        {
            Debug.LogError("[MatchingMessage] Message ID is required");
            return false;
        }

        if (string.IsNullOrEmpty(timestamp))
        {
            Debug.LogError("[MatchingMessage] Timestamp is required");
            return false;
        }

        if (string.IsNullOrEmpty(version))
        {
            Debug.LogError("[MatchingMessage] Protocol version is required");
            return false;
        }

        if (!MatchingProtocol.IsValidMessageType(type))
        {
            Debug.LogError($"[MatchingMessage] Invalid message type: {type}");
            return false;
        }

        if (!MatchingProtocol.IsCompatibleVersion(version))
        {
            Debug.LogError($"[MatchingMessage] Incompatible protocol version: {version}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 메시지 크기 제한 검증
    /// </summary>
    /// <returns>크기 제한을 만족하는지 여부</returns>
    public bool IsWithinSizeLimit()
    {
        int messageSize = GetSerializedSize();
        bool isValid = messageSize <= MatchingProtocol.MAX_MESSAGE_SIZE;
        
        if (!isValid)
        {
            Debug.LogError($"[MatchingMessage] Message size ({messageSize} bytes) exceeds limit ({MatchingProtocol.MAX_MESSAGE_SIZE} bytes)");
        }
        
        return isValid;
    }

    /// <summary>
    /// 메시지 만료 여부 확인
    /// </summary>
    /// <param name="timeoutSeconds">타임아웃 시간(초)</param>
    /// <returns>만료된 메시지인지 여부</returns>
    public bool IsExpired(int timeoutSeconds = 30)
    {
        try
        {
            DateTime messageTime = DateTime.Parse(timestamp);
            TimeSpan elapsed = DateTime.UtcNow - messageTime;
            bool isExpired = elapsed.TotalSeconds > timeoutSeconds;
            
            if (isExpired)
            {
                Debug.LogWarning($"[MatchingMessage] Message expired: {messageId} (elapsed: {elapsed.TotalSeconds:F1}s)");
            }
            
            return isExpired;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingMessage] Invalid timestamp format: {timestamp} - {e.Message}");
            return true;
        }
    }
    #endregion

    #region Serialization
    /// <summary>
    /// 메시지를 JSON 문자열로 직렬화
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
            Debug.LogError($"[MatchingMessage] Serialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON 문자열에서 매칭 메시지로 역직렬화
    /// </summary>
    /// <param name="json">JSON 문자열</param>
    /// <returns>매칭 메시지 객체</returns>
    public static MatchingMessage FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[MatchingMessage] Cannot deserialize null or empty JSON");
            return null;
        }

        try
        {
            var message = JsonUtility.FromJson<MatchingMessage>(json);
            
            if (message != null && !message.IsValid())
            {
                Debug.LogError("[MatchingMessage] Deserialized message failed validation");
                return null;
            }
            
            return message;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingMessage] Deserialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 페이로드를 특정 타입으로 역직렬화
    /// </summary>
    /// <typeparam name="T">페이로드 타입</typeparam>
    /// <returns>역직렬화된 페이로드</returns>
    public T GetPayload<T>() where T : class
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(payload);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingMessage] Payload deserialization failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 직렬화된 메시지 크기 계산
    /// </summary>
    /// <returns>바이트 크기</returns>
    public int GetSerializedSize()
    {
        string json = ToJson();
        return string.IsNullOrEmpty(json) ? 0 : System.Text.Encoding.UTF8.GetByteCount(json);
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 메시지 복사
    /// </summary>
    /// <returns>복사된 메시지</returns>
    public MatchingMessage Clone()
    {
        try
        {
            string json = ToJson();
            return FromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchingMessage] Clone failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 메시지 요약 정보 반환
    /// </summary>
    /// <returns>메시지 요약</returns>
    public string GetSummary()
    {
        return $"[{messageId}] Type: {type}, Priority: {priority}, Size: {GetSerializedSize()} bytes";
    }

    /// <summary>
    /// 타임스탬프 업데이트
    /// </summary>
    public void UpdateTimestamp()
    {
        timestamp = DateTime.UtcNow.ToString("O");
    }
    #endregion

    #region ToString Override
    /// <summary>
    /// 문자열 표현
    /// </summary>
    /// <returns>메시지 정보</returns>
    public override string ToString()
    {
        return $"MatchingMessage({GetSummary()})";
    }
    #endregion
}