using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// WebSocket 메시지 큐잉 시스템
/// Thread-safe 한 메시지 큐잉 및 처리를 담당
/// </summary>
public class MessageQueue : IDisposable
{
    #region Events
    /// <summary>메시지 처리 완료 이벤트</summary>
    public event Action<string> OnMessageProcessed;
    
    /// <summary>메시지 처리 실패 이벤트</summary>
    public event Action<string, string> OnMessageFailed;
    
    /// <summary>큐 오버플로우 이벤트</summary>
    public event Action<int> OnQueueOverflow;
    #endregion

    #region Private Fields
    private readonly ConcurrentQueue<QueuedMessage> _messageQueue;
    private readonly WebSocketConfig _config;
    private readonly object _lockObject = new();
    private volatile bool _isProcessing = false;
    private volatile bool _isDisposed = false;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _processingTask;
    private Func<string, Task<bool>> _sendMessageFunc;
    #endregion

    #region Properties
    /// <summary>현재 큐에 대기 중인 메시지 수</summary>
    public int QueuedCount => _messageQueue.Count;
    
    /// <summary>메시지 처리 중 여부</summary>
    public bool IsProcessing => _isProcessing;
    
    /// <summary>큐가 활성화된 상태인지</summary>
    public bool IsActive => !_isDisposed && _sendMessageFunc != null;
    #endregion

    #region Constructor
    /// <summary>
    /// 메시지 큐 초기화
    /// </summary>
    /// <param name="config">WebSocket 설정</param>
    public MessageQueue(WebSocketConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _messageQueue = new ConcurrentQueue<QueuedMessage>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        if (_config.EnableLogging)
        {
            Debug.Log("[MessageQueue] Initialized with max queue size: " + _config.MaxMessageQueueSize);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 메시지 전송 함수 설정
    /// </summary>
    /// <param name="sendMessageFunc">메시지 전송 함수</param>
    public void SetSendMessageFunction(Func<string, Task<bool>> sendMessageFunc)
    {
        _sendMessageFunc = sendMessageFunc ?? throw new ArgumentNullException(nameof(sendMessageFunc));
        
        if (_config.EnableLogging)
        {
            Debug.Log("[MessageQueue] Send message function set");
        }
    }

    /// <summary>
    /// 메시지를 큐에 추가
    /// </summary>
    /// <param name="message">전송할 메시지</param>
    /// <param name="priority">메시지 우선순위 (높을수록 먼저 처리)</param>
    /// <returns>큐 추가 성공 여부</returns>
    public bool EnqueueMessage(string message, MessagePriority priority = MessagePriority.Normal)
    {
        if (_isDisposed)
        {
            Debug.LogError("[MessageQueue] Cannot enqueue message: MessageQueue is disposed");
            return false;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("[MessageQueue] Cannot enqueue null or empty message");
            return false;
        }

        // 메시지 크기 검사
        if (System.Text.Encoding.UTF8.GetByteCount(message) > _config.MaxMessageSize)
        {
            Debug.LogError($"[MessageQueue] Message size exceeds limit: {_config.MaxMessageSize} bytes");
            return false;
        }

        // 큐 크기 검사
        if (_messageQueue.Count >= _config.MaxMessageQueueSize)
        {
            Debug.LogWarning($"[MessageQueue] Queue overflow, current size: {_messageQueue.Count}");
            OnQueueOverflow?.Invoke(_messageQueue.Count);
            
            // 낮은 우선순위 메시지 제거 시도
            if (priority == MessagePriority.High && TryRemoveLowPriorityMessage())
            {
                Debug.Log("[MessageQueue] Removed low priority message to make room for high priority message");
            }
            else
            {
                return false;
            }
        }

        var queuedMessage = new QueuedMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = message,
            Priority = priority,
            EnqueueTime = DateTime.UtcNow,
            RetryCount = 0
        };

        _messageQueue.Enqueue(queuedMessage);

        if (_config.EnableDetailedLogging)
        {
            Debug.Log($"[MessageQueue] Enqueued message [{queuedMessage.Id}] with priority {priority}");
        }

        // 즉시 처리 시작 (아직 처리 중이 아니라면)
        TryStartProcessing();
        
        return true;
    }

    /// <summary>
    /// 큐 처리 시작
    /// </summary>
    public void StartProcessing()
    {
        if (_isDisposed)
        {
            Debug.LogError("[MessageQueue] Cannot start processing: MessageQueue is disposed");
            return;
        }

        if (_sendMessageFunc == null)
        {
            Debug.LogError("[MessageQueue] Cannot start processing: Send message function not set");
            return;
        }

        TryStartProcessing();
    }

    /// <summary>
    /// 큐 처리 중지
    /// </summary>
    public void StopProcessing()
    {
        lock (_lockObject)
        {
            _isProcessing = false;
            _cancellationTokenSource?.Cancel();
        }

        if (_config.EnableLogging)
        {
            Debug.Log("[MessageQueue] Processing stopped");
        }
    }

    /// <summary>
    /// 큐 비우기
    /// </summary>
    public void Clear()
    {
        while (_messageQueue.TryDequeue(out _)) { }
        
        if (_config.EnableLogging)
        {
            Debug.Log("[MessageQueue] Queue cleared");
        }
    }

    /// <summary>
    /// 특정 우선순위 이상의 메시지만 남기고 정리
    /// </summary>
    /// <param name="minPriority">유지할 최소 우선순위</param>
    public void ClearLowPriorityMessages(MessagePriority minPriority)
    {
        var tempQueue = new Queue<QueuedMessage>();
        int removedCount = 0;

        while (_messageQueue.TryDequeue(out var message))
        {
            if (message.Priority >= minPriority)
            {
                tempQueue.Enqueue(message);
            }
            else
            {
                removedCount++;
            }
        }

        foreach (var message in tempQueue)
        {
            _messageQueue.Enqueue(message);
        }

        if (_config.EnableLogging && removedCount > 0)
        {
            Debug.Log($"[MessageQueue] Removed {removedCount} low priority messages");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 처리 시작 시도 (Thread-safe)
    /// </summary>
    private void TryStartProcessing()
    {
        lock (_lockObject)
        {
            if (_isProcessing || _isDisposed || _sendMessageFunc == null)
                return;

            if (_messageQueue.IsEmpty)
                return;

            _isProcessing = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _processingTask = ProcessQueueAsync(_cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// 큐 처리 비동기 메서드
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_messageQueue.IsEmpty)
            {
                if (!_messageQueue.TryDequeue(out var queuedMessage))
                    continue;

                await ProcessSingleMessage(queuedMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (_config.EnableLogging)
            {
                Debug.Log("[MessageQueue] Processing cancelled");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MessageQueue] Error in queue processing: {e.Message}");
        }
        finally
        {
            lock (_lockObject)
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// 단일 메시지 처리
    /// </summary>
    private async Task ProcessSingleMessage(QueuedMessage queuedMessage, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        
        try
        {
            // 메시지 타임아웃 검사
            if (IsMessageTimedOut(queuedMessage))
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.LogWarning($"[MessageQueue] Message [{queuedMessage.Id}] timed out, discarding");
                }
                OnMessageFailed?.Invoke(queuedMessage.Id, "Message timeout");
                return;
            }

            bool success = await _sendMessageFunc(queuedMessage.Message);
            
            if (success)
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[MessageQueue] Message [{queuedMessage.Id}] processed successfully");
                }
                OnMessageProcessed?.Invoke(queuedMessage.Id);
            }
            else
            {
                await HandleMessageFailure(queuedMessage, "Send failed", maxRetries);
            }
        }
        catch (Exception e)
        {
            await HandleMessageFailure(queuedMessage, e.Message, maxRetries);
        }
    }

    /// <summary>
    /// 메시지 처리 실패 핸들링
    /// </summary>
    private async Task HandleMessageFailure(QueuedMessage queuedMessage, string error, int maxRetries)
    {
        queuedMessage.RetryCount++;
        
        if (queuedMessage.RetryCount < maxRetries && queuedMessage.Priority >= MessagePriority.Normal)
        {
            // 재시도 지연
            int retryDelay = Math.Min(1000 * queuedMessage.RetryCount, 5000); // 최대 5초
            await Task.Delay(retryDelay);
            
            // 다시 큐에 추가 (우선순위 유지)
            _messageQueue.Enqueue(queuedMessage);
            
            if (_config.EnableDetailedLogging)
            {
                Debug.LogWarning($"[MessageQueue] Message [{queuedMessage.Id}] retry {queuedMessage.RetryCount}/{maxRetries}");
            }
        }
        else
        {
            Debug.LogError($"[MessageQueue] Message [{queuedMessage.Id}] failed permanently: {error}");
            OnMessageFailed?.Invoke(queuedMessage.Id, error);
        }
    }

    /// <summary>
    /// 메시지 타임아웃 검사
    /// </summary>
    private bool IsMessageTimedOut(QueuedMessage message)
    {
        var timeElapsed = DateTime.UtcNow - message.EnqueueTime;
        return timeElapsed.TotalMilliseconds > _config.MessageTimeout;
    }

    /// <summary>
    /// 낮은 우선순위 메시지 제거 시도
    /// </summary>
    private bool TryRemoveLowPriorityMessage()
    {
        var tempMessages = new List<QueuedMessage>();
        bool removed = false;

        while (_messageQueue.TryDequeue(out var message))
        {
            if (!removed && message.Priority == MessagePriority.Low)
            {
                removed = true; // 첫 번째 낮은 우선순위 메시지는 제거
                continue;
            }
            tempMessages.Add(message);
        }

        // 나머지 메시지들을 다시 큐에 추가
        foreach (var message in tempMessages)
        {
            _messageQueue.Enqueue(message);
        }

        return removed;
    }
    #endregion

    #region IDisposable
    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        StopProcessing();
        Clear();
        
        _cancellationTokenSource?.Dispose();
        _processingTask?.Dispose();

        OnMessageProcessed = null;
        OnMessageFailed = null;
        OnQueueOverflow = null;

        if (_config != null && _config.EnableLogging)
        {
            Debug.Log("[MessageQueue] Disposed");
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// 메시지 우선순위
/// </summary>
public enum MessagePriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// 큐에 저장되는 메시지 정보
/// </summary>
internal class QueuedMessage
{
    public string Id { get; set; }
    public string Message { get; set; }
    public MessagePriority Priority { get; set; }
    public DateTime EnqueueTime { get; set; }
    public int RetryCount { get; set; }
}
#endregion