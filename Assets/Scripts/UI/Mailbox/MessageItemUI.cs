using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 개별 메시지 아이템 UI 컴포넌트
/// 우편함의 각 메시지를 표시하며, 메시지 타입별 아이콘과 스타일링을 제공합니다.
/// 클릭 이벤트, 읽음 상태, 첨부 파일 표시 등을 관리합니다.
/// </summary>
public class MessageItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Events
    /// <summary>
    /// 메시지 아이템 클릭 이벤트
    /// </summary>
    public static event Action<string, MailboxMessage> OnMessageClicked; // messageId, message

    /// <summary>
    /// 첨부 파일 버튼 클릭 이벤트
    /// </summary>
    public static event Action<string, MailboxMessage> OnAttachmentClicked; // messageId, message

    /// <summary>
    /// 삭제 버튼 클릭 이벤트
    /// </summary>
    public static event Action<string, MailboxMessage> OnDeleteClicked; // messageId, message
    #endregion

    #region UI References
    [Header("Main UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image typeIconImage;
    [SerializeField] private Text senderNameText;
    [SerializeField] private Text titleText;
    [SerializeField] private Text sendTimeText;
    [SerializeField] private GameObject unreadIndicator;
    
    [Header("Attachment UI")]
    [SerializeField] private GameObject attachmentContainer;
    [SerializeField] private Image attachmentIcon;
    [SerializeField] private Button attachmentButton;
    [SerializeField] private Text attachmentCountText;
    
    [Header("Action Buttons")]
    [SerializeField] private Button deleteButton;
    [SerializeField] private GameObject actionButtonsContainer;
    
    [Header("Type Icons")]
    [SerializeField] private Sprite systemMessageIcon;
    [SerializeField] private Sprite friendMessageIcon;
    [SerializeField] private Sprite energyGiftIcon;
    [SerializeField] private Sprite achievementIcon;
    [SerializeField] private Sprite eventMessageIcon;
    [SerializeField] private Sprite defaultMessageIcon;
    
    [Header("Visual States")]
    [SerializeField] private Color readBackgroundColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color unreadBackgroundColor = Color.white;
    [SerializeField] private Color hoverBackgroundColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color readTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color unreadTextColor = Color.black;
    #endregion

    #region Private Fields
    private MailboxMessage _currentMessage;
    private bool _isInitialized = false;
    private bool _isHovered = false;
    private Color _originalBackgroundColor;
    private RectTransform _rectTransform;
    
    // 날짜 포맷 캐시
    private readonly string[] _timeFormats = { "HH:mm", "M월 d일", "yyyy년 M월 d일" };
    #endregion

    #region Properties
    /// <summary>
    /// 현재 표시 중인 메시지
    /// </summary>
    public MailboxMessage CurrentMessage => _currentMessage;
    
    /// <summary>
    /// 메시지 ID
    /// </summary>
    public string MessageId => _currentMessage?.messageId;
    
    /// <summary>
    /// 읽음 상태
    /// </summary>
    public bool IsRead => _currentMessage?.isRead ?? false;
    
    /// <summary>
    /// 메시지 타입
    /// </summary>
    public MailMessageType MessageType => _currentMessage?.type ?? MailMessageType.System;
    
    /// <summary>
    /// UI가 초기화되었는지 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializeUI();
    }

    private void Start()
    {
        SetupEventHandlers();
    }

    private void OnDestroy()
    {
        CleanupEventHandlers();
    }
    #endregion

    #region Initialization
    private void ValidateComponents()
    {
        _rectTransform = GetComponent<RectTransform>();
        
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
        
        if (backgroundImage == null)
        {
            Debug.LogError("[MessageItemUI] Background image component is required!");
        }
        
        if (titleText == null)
        {
            Debug.LogError("[MessageItemUI] Title text component is required!");
        }
        
        if (senderNameText == null)
        {
            Debug.LogWarning("[MessageItemUI] Sender name text is not assigned");
        }
        
        if (sendTimeText == null)
        {
            Debug.LogWarning("[MessageItemUI] Send time text is not assigned");
        }
    }

    private void InitializeUI()
    {
        if (backgroundImage != null)
        {
            _originalBackgroundColor = backgroundImage.color;
        }
        
        // 초기 상태로 비활성화
        HideActionButtons();
        
        _isInitialized = true;
        Debug.Log("[MessageItemUI] UI initialized successfully");
    }

    private void SetupEventHandlers()
    {
        if (attachmentButton != null)
        {
            attachmentButton.onClick.AddListener(HandleAttachmentButtonClicked);
        }
        
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(HandleDeleteButtonClicked);
        }
    }

    private void CleanupEventHandlers()
    {
        if (attachmentButton != null)
        {
            attachmentButton.onClick.RemoveAllListeners();
        }
        
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
        }
    }
    #endregion

    #region Message Display
    /// <summary>
    /// 메시지 데이터로 UI 업데이트
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    public void SetMessage(MailboxMessage message)
    {
        if (message == null)
        {
            Debug.LogError("[MessageItemUI] Cannot set null message");
            return;
        }
        
        _currentMessage = message;
        UpdateMessageDisplay();
        
        Debug.Log($"[MessageItemUI] Message set: {message.messageId} - {message.title}");
    }

    private void UpdateMessageDisplay()
    {
        if (_currentMessage == null || !_isInitialized) return;
        
        UpdateTypeIcon();
        UpdateSenderName();
        UpdateTitle();
        UpdateSendTime();
        UpdateReadState();
        UpdateAttachmentDisplay();
        
        Debug.Log($"[MessageItemUI] Display updated for message: {_currentMessage.messageId}");
    }

    private void UpdateTypeIcon()
    {
        if (typeIconImage == null) return;
        
        Sprite iconSprite = _currentMessage.type switch
        {
            MailMessageType.System => systemMessageIcon,
            MailMessageType.Friend => friendMessageIcon,
            MailMessageType.EnergyGift => energyGiftIcon,
            MailMessageType.Achievement => achievementIcon,
            MailMessageType.Event => eventMessageIcon,
            _ => defaultMessageIcon
        };
        
        typeIconImage.sprite = iconSprite ?? defaultMessageIcon;
        typeIconImage.gameObject.SetActive(iconSprite != null);
    }

    private void UpdateSenderName()
    {
        if (senderNameText == null) return;
        
        string senderName = string.IsNullOrEmpty(_currentMessage.senderName) ? 
            GetDefaultSenderName(_currentMessage.type) : _currentMessage.senderName;
        
        senderNameText.text = senderName;
        senderNameText.color = _currentMessage.isRead ? readTextColor : unreadTextColor;
    }

    private void UpdateTitle()
    {
        if (titleText == null) return;
        
        titleText.text = _currentMessage.title ?? "제목 없음";
        titleText.color = _currentMessage.isRead ? readTextColor : unreadTextColor;
    }

    private void UpdateSendTime()
    {
        if (sendTimeText == null) return;
        
        string timeString = FormatSendTime(_currentMessage.SentAt);
        sendTimeText.text = timeString;
        sendTimeText.color = _currentMessage.isRead ? readTextColor : unreadTextColor;
    }

    private void UpdateReadState()
    {
        // 읽지 않음 인디케이터
        if (unreadIndicator != null)
        {
            unreadIndicator.SetActive(!_currentMessage.isRead);
        }
        
        // 배경 색상
        if (backgroundImage != null && !_isHovered)
        {
            Color targetColor = _currentMessage.isRead ? readBackgroundColor : unreadBackgroundColor;
            backgroundImage.color = targetColor;
            _originalBackgroundColor = targetColor;
        }
    }

    private void UpdateAttachmentDisplay()
    {
        bool hasAttachments = _currentMessage.attachments != null && _currentMessage.attachments.Count > 0;
        
        if (attachmentContainer != null)
        {
            attachmentContainer.SetActive(hasAttachments);
        }
        
        if (hasAttachments)
        {
            // 첨부 파일 개수 표시
            if (attachmentCountText != null)
            {
                attachmentCountText.text = _currentMessage.attachments.Count.ToString();
            }
            
            // 첨부 파일 타입에 따른 아이콘 설정
            UpdateAttachmentIcon();
        }
    }

    private void UpdateAttachmentIcon()
    {
        if (attachmentIcon == null || _currentMessage.attachments == null) return;
        
        // 에너지 선물인 경우 특별한 아이콘
        if (_currentMessage.IsEnergyGift())
        {
            attachmentIcon.sprite = energyGiftIcon;
            attachmentIcon.color = Color.yellow;
        }
        else
        {
            // 기본 첨부 파일 아이콘
            attachmentIcon.color = Color.white;
        }
    }
    #endregion

    #region Utility Methods
    private string GetDefaultSenderName(MailMessageType type)
    {
        return type switch
        {
            MailMessageType.System => "시스템",
            MailMessageType.Friend => "친구",
            MailMessageType.EnergyGift => "선물함",
            MailMessageType.Achievement => "성취 시스템",
            MailMessageType.Event => "이벤트",
            _ => "알림"
        };
    }

    private string FormatSendTime(DateTime sendTime)
    {
        DateTime now = DateTime.Now;
        TimeSpan timeDiff = now - sendTime;
        
        if (timeDiff.TotalDays < 1)
        {
            // 오늘: 시간만 표시
            return sendTime.ToString(_timeFormats[0]);
        }
        else if (timeDiff.TotalDays < 365)
        {
            // 올해: 월일 표시
            return sendTime.ToString(_timeFormats[1]);
        }
        else
        {
            // 1년 이상: 전체 날짜 표시
            return sendTime.ToString(_timeFormats[2]);
        }
    }
    #endregion

    #region Event Handlers
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_currentMessage != null)
        {
            OnMessageClicked?.Invoke(_currentMessage.messageId, _currentMessage);
            Debug.Log($"[MessageItemUI] Message clicked: {_currentMessage.messageId}");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        
        if (backgroundImage != null)
        {
            backgroundImage.color = hoverBackgroundColor;
        }
        
        ShowActionButtons();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        
        if (backgroundImage != null)
        {
            backgroundImage.color = _originalBackgroundColor;
        }
        
        HideActionButtons();
    }

    private void HandleAttachmentButtonClicked()
    {
        if (_currentMessage != null)
        {
            OnAttachmentClicked?.Invoke(_currentMessage.messageId, _currentMessage);
            Debug.Log($"[MessageItemUI] Attachment clicked: {_currentMessage.messageId}");
        }
    }

    private void HandleDeleteButtonClicked()
    {
        if (_currentMessage != null)
        {
            OnDeleteClicked?.Invoke(_currentMessage.messageId, _currentMessage);
            Debug.Log($"[MessageItemUI] Delete clicked: {_currentMessage.messageId}");
        }
    }
    #endregion

    #region Action Buttons
    private void ShowActionButtons()
    {
        if (actionButtonsContainer != null)
        {
            actionButtonsContainer.SetActive(true);
        }
    }

    private void HideActionButtons()
    {
        if (actionButtonsContainer != null)
        {
            actionButtonsContainer.SetActive(false);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 메시지를 읽음 상태로 표시
    /// </summary>
    public void MarkAsRead()
    {
        if (_currentMessage != null && !_currentMessage.isRead)
        {
            _currentMessage.MarkAsRead();
            UpdateReadState();
            
            Debug.Log($"[MessageItemUI] Message marked as read: {_currentMessage.messageId}");
        }
    }

    /// <summary>
    /// UI 새로고침
    /// </summary>
    public void RefreshDisplay()
    {
        if (_currentMessage != null)
        {
            UpdateMessageDisplay();
        }
    }

    /// <summary>
    /// 선택 상태 설정
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
        {
            if (selected)
            {
                backgroundImage.color = hoverBackgroundColor;
            }
            else
            {
                Color targetColor = _currentMessage?.isRead == true ? readBackgroundColor : unreadBackgroundColor;
                backgroundImage.color = targetColor;
                _originalBackgroundColor = targetColor;
            }
        }
    }

    /// <summary>
    /// 높이 가져오기 (동적 크기 조정용)
    /// </summary>
    public float GetItemHeight()
    {
        return _rectTransform != null ? _rectTransform.rect.height : 80f;
    }

    /// <summary>
    /// 아이템 재사용을 위한 리셋
    /// </summary>
    public void ResetItem()
    {
        _currentMessage = null;
        _isHovered = false;
        
        HideActionButtons();
        
        if (backgroundImage != null)
        {
            backgroundImage.color = unreadBackgroundColor;
        }
        
        if (titleText != null)
            titleText.text = "";
        if (senderNameText != null)
            senderNameText.text = "";
        if (sendTimeText != null)
            sendTimeText.text = "";
        if (unreadIndicator != null)
            unreadIndicator.SetActive(false);
        if (attachmentContainer != null)
            attachmentContainer.SetActive(false);
    }
    #endregion
}