using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우편함 메시지 타입 열거형
/// </summary>
[Serializable]
public enum MailMessageType
{
    System,         // 시스템 공지사항
    Friend,         // 친구 메시지
    EnergyGift,     // 피로도 선물
    Achievement,    // 성취 보상
    Event           // 이벤트 알림
}

/// <summary>
/// 우편함 메시지 구조체
/// Unity JsonUtility 직렬화를 지원하는 메시지 데이터
/// </summary>
[Serializable]
public class MailboxMessage
{
    [Header("Basic Message Info")]
    public string messageId;
    public MailMessageType type;
    public string title;
    [TextArea(3, 10)]
    public string content;
    
    [Header("Sender Info")]
    public string senderId;
    public string senderName;
    
    [Header("Timestamps")]
    // Unity JsonUtility는 DateTime을 지원하지 않으므로 string으로 저장
    public string sentAtString;
    public string readAtString;
    
    [Header("Status")]
    public bool isRead;
    
    [Header("Attachments")]
    // Dictionary는 JsonUtility에서 직렬화되지 않으므로 대안 사용
    public List<MailboxAttachment> attachments;
    
    // DateTime 프로퍼티 (Unity 인스펙터에서는 보이지 않음)
    public DateTime SentAt
    {
        get
        {
            if (DateTime.TryParse(sentAtString, out DateTime result))
                return result;
            return DateTime.MinValue;
        }
        set
        {
            sentAtString = value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
    
    public DateTime? ReadAt
    {
        get
        {
            if (string.IsNullOrEmpty(readAtString))
                return null;
            if (DateTime.TryParse(readAtString, out DateTime result))
                return result;
            return null;
        }
        set
        {
            readAtString = value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        }
    }
    
    /// <summary>
    /// 생성자
    /// </summary>
    public MailboxMessage()
    {
        attachments = new List<MailboxAttachment>();
        SentAt = DateTime.UtcNow;
        isRead = false;
    }
    
    /// <summary>
    /// 메시지를 읽음 상태로 표시
    /// </summary>
    public void MarkAsRead()
    {
        isRead = true;
        ReadAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 특정 타입의 첨부 파일 가져오기
    /// </summary>
    public MailboxAttachment GetAttachment(string key)
    {
        return attachments?.Find(a => a.key == key);
    }
    
    /// <summary>
    /// 첨부 파일 추가
    /// </summary>
    public void AddAttachment(string key, object value, string displayName = "")
    {
        if (attachments == null)
            attachments = new List<MailboxAttachment>();
        
        var existing = attachments.Find(a => a.key == key);
        if (existing != null)
        {
            existing.value = value?.ToString() ?? "";
            existing.displayName = displayName;
        }
        else
        {
            attachments.Add(new MailboxAttachment
            {
                key = key,
                value = value?.ToString() ?? "",
                displayName = displayName
            });
        }
    }
    
    /// <summary>
    /// 에너지 선물 여부 확인
    /// </summary>
    public bool IsEnergyGift()
    {
        return type == MailMessageType.EnergyGift && 
               GetAttachment("energy") != null;
    }
    
    /// <summary>
    /// 에너지 선물 수량 가져오기
    /// </summary>
    public int GetEnergyAmount()
    {
        var energyAttachment = GetAttachment("energy");
        if (energyAttachment != null && int.TryParse(energyAttachment.value, out int amount))
            return amount;
        return 0;
    }
    
    /// <summary>
    /// 선물 ID 가져오기 (중복 방지용)
    /// </summary>
    public string GetGiftId()
    {
        var giftAttachment = GetAttachment("giftId");
        return giftAttachment?.value ?? "";
    }
}

/// <summary>
/// 우편함 첨부 파일 구조체
/// JsonUtility 직렬화를 위한 Key-Value 페어 대안
/// </summary>
[Serializable]
public class MailboxAttachment
{
    public string key;
    public string value;
    public string displayName;
    
    /// <summary>
    /// 정수 값 가져오기
    /// </summary>
    public int GetIntValue()
    {
        if (int.TryParse(value, out int result))
            return result;
        return 0;
    }
    
    /// <summary>
    /// 부울 값 가져오기
    /// </summary>
    public bool GetBoolValue()
    {
        if (bool.TryParse(value, out bool result))
            return result;
        return false;
    }
}

/// <summary>
/// 우편함 데이터 구조체
/// 전체 우편함 상태와 메시지 목록을 관리
/// </summary>
[Serializable]
public class MailboxData
{
    [Header("Messages")]
    public List<MailboxMessage> messages;
    
    [Header("Status")]
    public int unreadCount;
    public string lastSyncTimeString;
    
    [Header("Cache Info")]
    public string cacheVersion = "1.0";
    public int totalMessageCount;
    
    /// <summary>
    /// DateTime 프로퍼티
    /// </summary>
    public DateTime LastSyncTime
    {
        get
        {
            if (DateTime.TryParse(lastSyncTimeString, out DateTime result))
                return result;
            return DateTime.MinValue;
        }
        set
        {
            lastSyncTimeString = value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
    
    /// <summary>
    /// 생성자
    /// </summary>
    public MailboxData()
    {
        messages = new List<MailboxMessage>();
        unreadCount = 0;
        LastSyncTime = DateTime.UtcNow;
        totalMessageCount = 0;
    }
    
    /// <summary>
    /// 읽지 않은 메시지 수 다시 계산
    /// </summary>
    public void RecalculateUnreadCount()
    {
        unreadCount = 0;
        if (messages != null)
        {
            foreach (var message in messages)
            {
                if (!message.isRead)
                    unreadCount++;
            }
        }
    }
    
    /// <summary>
    /// 메시지 ID로 메시지 찾기
    /// </summary>
    public MailboxMessage GetMessage(string messageId)
    {
        return messages?.Find(m => m.messageId == messageId);
    }
    
    /// <summary>
    /// 메시지 추가 (중복 방지)
    /// </summary>
    public bool AddMessage(MailboxMessage message)
    {
        if (message == null || string.IsNullOrEmpty(message.messageId))
            return false;
        
        if (messages == null)
            messages = new List<MailboxMessage>();
        
        // 중복 확인
        if (messages.Exists(m => m.messageId == message.messageId))
            return false;
        
        messages.Add(message);
        totalMessageCount = messages.Count;
        
        if (!message.isRead)
            unreadCount++;
        
        return true;
    }
    
    /// <summary>
    /// 메시지 제거
    /// </summary>
    public bool RemoveMessage(string messageId)
    {
        if (messages == null || string.IsNullOrEmpty(messageId))
            return false;
        
        var message = messages.Find(m => m.messageId == messageId);
        if (message == null)
            return false;
        
        bool wasUnread = !message.isRead;
        bool removed = messages.Remove(message);
        
        if (removed)
        {
            totalMessageCount = messages.Count;
            if (wasUnread)
                unreadCount--;
        }
        
        return removed;
    }
    
    /// <summary>
    /// 메시지를 읽음 상태로 표시
    /// </summary>
    public bool MarkMessageAsRead(string messageId)
    {
        var message = GetMessage(messageId);
        if (message == null || message.isRead)
            return false;
        
        message.MarkAsRead();
        unreadCount--;
        
        return true;
    }
    
    /// <summary>
    /// 모든 메시지를 읽음 상태로 표시
    /// </summary>
    public void MarkAllAsRead()
    {
        if (messages != null)
        {
            foreach (var message in messages)
            {
                if (!message.isRead)
                {
                    message.MarkAsRead();
                }
            }
        }
        unreadCount = 0;
    }
    
    /// <summary>
    /// 타입별 메시지 가져오기
    /// </summary>
    public List<MailboxMessage> GetMessagesByType(MailMessageType type)
    {
        if (messages == null)
            return new List<MailboxMessage>();
        
        return messages.FindAll(m => m.type == type);
    }
    
    /// <summary>
    /// 읽지 않은 메시지 가져오기
    /// </summary>
    public List<MailboxMessage> GetUnreadMessages()
    {
        if (messages == null)
            return new List<MailboxMessage>();
        
        return messages.FindAll(m => !m.isRead);
    }
    
    /// <summary>
    /// 메시지 정렬 (최신순)
    /// </summary>
    public void SortMessagesByDate()
    {
        if (messages != null && messages.Count > 1)
        {
            messages.Sort((a, b) => b.SentAt.CompareTo(a.SentAt));
        }
    }
    
    /// <summary>
    /// 데이터 유효성 검증
    /// </summary>
    public bool IsValid()
    {
        if (messages == null)
            return false;
        
        // 메시지 ID 중복 확인
        var messageIds = new HashSet<string>();
        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.messageId))
                return false;
            
            if (messageIds.Contains(message.messageId))
                return false;
            
            messageIds.Add(message.messageId);
        }
        
        // unreadCount 검증
        int actualUnreadCount = messages.Count(m => !m.isRead);
        if (unreadCount != actualUnreadCount)
        {
            Debug.LogWarning($"[MailboxData] Unread count mismatch: expected {actualUnreadCount}, got {unreadCount}");
            unreadCount = actualUnreadCount;
        }
        
        return true;
    }
}