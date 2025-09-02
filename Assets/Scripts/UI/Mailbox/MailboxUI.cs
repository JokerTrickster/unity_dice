using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 우편함 UI 메인 컨트롤러
/// 메시지 목록 표시, 스크롤링, 검색, 정렬 기능을 제공합니다.
/// 객체 풀링을 통한 60FPS 성능 최적화와 가상화를 지원합니다.
/// </summary>
public class MailboxUI : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 우편함 UI가 열릴 때 발생
    /// </summary>
    public static event Action OnMailboxOpened;

    /// <summary>
    /// 우편함 UI가 닫힐 때 발생
    /// </summary>
    public static event Action OnMailboxClosed;

    /// <summary>
    /// 메시지 상세 보기 요청 시 발생
    /// </summary>
    public static event Action<MailboxMessage> OnMessageDetailRequested;
    #endregion

    #region UI References
    [Header("Main UI")]
    [SerializeField] private GameObject mailboxModal;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text headerTitleText;
    [SerializeField] private Text unreadCountText;
    
    [Header("Action Buttons")]
    [SerializeField] private Button markAllReadButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button sortButton;
    
    [Header("Message List")]
    [SerializeField] private ScrollRect messageScrollRect;
    [SerializeField] private RectTransform messageListContent;
    [SerializeField] private MessageItemUI messageItemPrefab;
    [SerializeField] private Text emptyStateText;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Filter & Sort")]
    [SerializeField] private Dropdown messageTypeFilter;
    [SerializeField] private Dropdown sortOrderDropdown;
    [SerializeField] private InputField searchInputField;
    
    [Header("Animation")]
    [SerializeField] private AnimationCurve openAnimationCurve = AnimationCurve.EaseOutQuart(0, 0, 1, 1);
    [SerializeField] private AnimationCurve closeAnimationCurve = AnimationCurve.EaseInQuart(0, 1, 1, 0);
    [SerializeField] private float animationDuration = 0.3f;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxVisibleItems = 10;
    [SerializeField] private int poolSize = 15;
    [SerializeField] private float itemHeight = 80f;
    [SerializeField] private bool enableVirtualization = true;
    #endregion

    #region Private Fields
    // 데이터
    private List<MailboxMessage> _allMessages = new List<MailboxMessage>();
    private List<MailboxMessage> _filteredMessages = new List<MailboxMessage>();
    private MailboxData _currentMailboxData;
    
    // UI 상태
    private bool _isInitialized = false;
    private bool _isVisible = false;
    private bool _isLoading = false;
    private bool _isAnimating = false;
    
    // 객체 풀링
    private Queue<MessageItemUI> _messageItemPool = new Queue<MessageItemUI>();
    private List<MessageItemUI> _activeMessageItems = new List<MessageItemUI>();
    
    // 가상화
    private int _firstVisibleIndex = 0;
    private int _lastVisibleIndex = 0;
    private float _scrollPosition = 0f;
    
    // 필터링 & 정렬
    private MailMessageType _currentTypeFilter = (MailMessageType)(-1); // All types
    private SortOrder _currentSortOrder = SortOrder.DateDescending;
    private string _searchQuery = "";
    
    // 캐시된 문자열 (GC 최적화)
    private readonly string _emptyStateMessage = "우편함이 비어있습니다";
    private readonly string _loadingMessage = "메시지를 불러오는 중...";
    private readonly string _noResultsMessage = "검색 결과가 없습니다";
    private readonly string _headerTitleFormat = "우편함 ({0})";
    private readonly string _unreadCountFormat = "읽지 않음: {0}개";
    
    // 성능 최적화
    private Coroutine _refreshCoroutine;
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    #endregion

    #region Enums
    public enum SortOrder
    {
        DateAscending,
        DateDescending,
        SenderName,
        MessageType,
        ReadStatus
    }
    #endregion

    #region Properties
    /// <summary>
    /// 우편함이 표시되고 있는지 여부
    /// </summary>
    public bool IsVisible => _isVisible;
    
    /// <summary>
    /// 로딩 중인지 여부
    /// </summary>
    public bool IsLoading => _isLoading;
    
    /// <summary>
    /// 현재 표시 중인 메시지 수
    /// </summary>
    public int DisplayedMessageCount => _filteredMessages.Count;
    
    /// <summary>
    /// 전체 메시지 수
    /// </summary>
    public int TotalMessageCount => _allMessages.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
    }

    private void Start()
    {
        InitializeUI();
        SubscribeToEvents();
        InitializeObjectPool();
        SetupScrollRectCallback();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupObjectPool();
        
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
        }
    }

    private void Update()
    {
        // 가상화 업데이트 (성능 최적화)
        if (_isVisible && enableVirtualization && _filteredMessages.Count > maxVisibleItems)
        {
            UpdateVirtualization();
        }
    }
    #endregion

    #region Initialization
    private void ValidateComponents()
    {
        if (mailboxModal == null)
        {
            Debug.LogError("[MailboxUI] Mailbox modal is required!");
        }
        
        if (messageScrollRect == null)
        {
            Debug.LogError("[MailboxUI] Message scroll rect is required!");
        }
        
        if (messageItemPrefab == null)
        {
            Debug.LogError("[MailboxUI] Message item prefab is required!");
        }
        
        if (messageListContent == null)
        {
            messageListContent = messageScrollRect?.content;
        }
    }

    private void InitializeUI()
    {
        if (mailboxModal != null)
        {
            mailboxModal.SetActive(false);
        }
        
        SetupEventHandlers();
        InitializeFilterDropdowns();
        UpdateEmptyState();
        
        _isInitialized = true;
        Debug.Log("[MailboxUI] UI initialized successfully");
    }

    private void SetupEventHandlers()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMailbox);
        }
        
        if (markAllReadButton != null)
        {
            markAllReadButton.onClick.AddListener(MarkAllMessagesAsRead);
        }
        
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshMailbox);
        }
        
        if (sortButton != null)
        {
            sortButton.onClick.AddListener(ToggleSortOrder);
        }
        
        if (messageTypeFilter != null)
        {
            messageTypeFilter.onValueChanged.AddListener(HandleTypeFilterChanged);
        }
        
        if (sortOrderDropdown != null)
        {
            sortOrderDropdown.onValueChanged.AddListener(HandleSortOrderChanged);
        }
        
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(HandleSearchQueryChanged);
        }
    }

    private void InitializeFilterDropdowns()
    {
        // 메시지 타입 필터 초기화
        if (messageTypeFilter != null)
        {
            messageTypeFilter.ClearOptions();
            List<string> typeOptions = new List<string>
            {
                "모든 타입",
                "시스템",
                "친구",
                "에너지 선물",
                "성취",
                "이벤트"
            };
            messageTypeFilter.AddOptions(typeOptions);
            messageTypeFilter.value = 0;
        }
        
        // 정렬 순서 드롭다운 초기화
        if (sortOrderDropdown != null)
        {
            sortOrderDropdown.ClearOptions();
            List<string> sortOptions = new List<string>
            {
                "최신순",
                "오래된 순",
                "발신자별",
                "타입별",
                "읽음 상태별"
            };
            sortOrderDropdown.AddOptions(sortOptions);
            sortOrderDropdown.value = 0;
        }
    }

    private void InitializeObjectPool()
    {
        if (messageItemPrefab == null || messageListContent == null) return;
        
        // 풀 크기만큼 미리 생성
        for (int i = 0; i < poolSize; i++)
        {
            MessageItemUI item = Instantiate(messageItemPrefab, messageListContent);
            item.gameObject.SetActive(false);
            _messageItemPool.Enqueue(item);
        }
        
        Debug.Log($"[MailboxUI] Object pool initialized with {poolSize} items");
    }

    private void SetupScrollRectCallback()
    {
        if (messageScrollRect != null)
        {
            messageScrollRect.onValueChanged.AddListener(HandleScrollValueChanged);
        }
    }
    #endregion

    #region Event Subscription
    private void SubscribeToEvents()
    {
        // MailboxManager 이벤트
        MailboxManager.OnMailboxLoaded += HandleMailboxLoaded;
        MailboxManager.OnMailboxUpdated += HandleMailboxUpdated;
        MailboxManager.OnUnreadCountChanged += HandleUnreadCountChanged;
        MailboxManager.OnNewMessageReceived += HandleNewMessage;
        MailboxManager.OnMessageRead += HandleMessageRead;
        MailboxManager.OnMessageDeleted += HandleMessageDeleted;
        MailboxManager.OnSyncStatusChanged += HandleSyncStatusChanged;
        MailboxManager.OnError += HandleMailboxError;
        
        // MessageItemUI 이벤트
        MessageItemUI.OnMessageClicked += HandleMessageClicked;
        MessageItemUI.OnAttachmentClicked += HandleAttachmentClicked;
        MessageItemUI.OnDeleteClicked += HandleDeleteClicked;
        
        Debug.Log("[MailboxUI] Subscribed to MailboxManager events");
    }

    private void UnsubscribeFromEvents()
    {
        // MailboxManager 이벤트
        MailboxManager.OnMailboxLoaded -= HandleMailboxLoaded;
        MailboxManager.OnMailboxUpdated -= HandleMailboxUpdated;
        MailboxManager.OnUnreadCountChanged -= HandleUnreadCountChanged;
        MailboxManager.OnNewMessageReceived -= HandleNewMessage;
        MailboxManager.OnMessageRead -= HandleMessageRead;
        MailboxManager.OnMessageDeleted -= HandleMessageDeleted;
        MailboxManager.OnSyncStatusChanged -= HandleSyncStatusChanged;
        MailboxManager.OnError -= HandleMailboxError;
        
        // MessageItemUI 이벤트
        MessageItemUI.OnMessageClicked -= HandleMessageClicked;
        MessageItemUI.OnAttachmentClicked -= HandleAttachmentClicked;
        MessageItemUI.OnDeleteClicked -= HandleDeleteClicked;
        
        Debug.Log("[MailboxUI] Unsubscribed from MailboxManager events");
    }
    #endregion

    #region Event Handlers
    private void HandleMailboxLoaded(MailboxData mailboxData)
    {
        _currentMailboxData = mailboxData;
        LoadMessagesFromData();
        UpdateHeaderInfo();
        
        Debug.Log($"[MailboxUI] Mailbox loaded with {mailboxData?.messages?.Count ?? 0} messages");
    }

    private void HandleMailboxUpdated(MailboxData mailboxData)
    {
        _currentMailboxData = mailboxData;
        LoadMessagesFromData();
        UpdateHeaderInfo();
        
        Debug.Log("[MailboxUI] Mailbox updated");
    }

    private void HandleUnreadCountChanged(int newCount)
    {
        UpdateUnreadCountDisplay(newCount);
        Debug.Log($"[MailboxUI] Unread count changed: {newCount}");
    }

    private void HandleNewMessage(MailboxMessage message)
    {
        if (_isVisible)
        {
            // 즉시 UI 업데이트
            RefreshMessageList();
        }
        
        Debug.Log($"[MailboxUI] New message received: {message?.title}");
    }

    private void HandleMessageRead(string messageId)
    {
        UpdateMessageReadState(messageId, true);
        Debug.Log($"[MailboxUI] Message read: {messageId}");
    }

    private void HandleMessageDeleted(string messageId)
    {
        RemoveMessageFromUI(messageId);
        Debug.Log($"[MailboxUI] Message deleted: {messageId}");
    }

    private void HandleSyncStatusChanged(bool success, string message)
    {
        if (!success)
        {
            ShowError($"동기화 실패: {message}");
        }
        
        SetLoadingState(false);
    }

    private void HandleMailboxError(string error)
    {
        ShowError(error);
        SetLoadingState(false);
    }

    private void HandleMessageClicked(string messageId, MailboxMessage message)
    {
        // 메시지를 읽음으로 표시
        if (!message.isRead)
        {
            MailboxManager.Instance.MarkMessageAsRead(messageId);
        }
        
        // 상세 보기 요청
        OnMessageDetailRequested?.Invoke(message);
        
        Debug.Log($"[MailboxUI] Message detail requested: {messageId}");
    }

    private void HandleAttachmentClicked(string messageId, MailboxMessage message)
    {
        if (message.IsEnergyGift())
        {
            // 에너지 선물 처리
            MailboxManager.Instance.ProcessMessage(messageId);
        }
        else
        {
            // 기타 첨부 파일 처리
            Debug.Log($"[MailboxUI] Attachment clicked: {messageId}");
        }
    }

    private void HandleDeleteClicked(string messageId, MailboxMessage message)
    {
        // 삭제 확인 후 처리
        MailboxManager.Instance.DeleteMessage(messageId);
    }

    private void HandleScrollValueChanged(Vector2 scrollValue)
    {
        _scrollPosition = scrollValue.y;
        
        // 가상화 업데이트는 Update()에서 처리
    }

    private void HandleTypeFilterChanged(int value)
    {
        _currentTypeFilter = value == 0 ? (MailMessageType)(-1) : (MailMessageType)(value - 1);
        ApplyFilters();
        
        Debug.Log($"[MailboxUI] Type filter changed: {_currentTypeFilter}");
    }

    private void HandleSortOrderChanged(int value)
    {
        _currentSortOrder = (SortOrder)value;
        SortMessages();
        RefreshMessageList();
        
        Debug.Log($"[MailboxUI] Sort order changed: {_currentSortOrder}");
    }

    private void HandleSearchQueryChanged(string query)
    {
        _searchQuery = query?.Trim() ?? "";
        ApplyFilters();
        
        Debug.Log($"[MailboxUI] Search query changed: '{_searchQuery}'");
    }
    #endregion

    #region Public API
    /// <summary>
    /// 우편함 열기
    /// </summary>
    public void OpenMailbox()
    {
        if (_isVisible || _isAnimating) return;
        
        StartCoroutine(OpenMailboxCoroutine());
    }

    /// <summary>
    /// 우편함 닫기
    /// </summary>
    public void CloseMailbox()
    {
        if (!_isVisible || _isAnimating) return;
        
        StartCoroutine(CloseMailboxCoroutine());
    }

    /// <summary>
    /// 우편함 새로고침
    /// </summary>
    public void RefreshMailbox()
    {
        if (_isLoading) return;
        
        SetLoadingState(true);
        
        if (MailboxManager.Instance != null)
        {
            MailboxManager.Instance.RefreshFromServer();
        }
        
        Debug.Log("[MailboxUI] Mailbox refresh requested");
    }

    /// <summary>
    /// 모든 메시지를 읽음으로 표시
    /// </summary>
    public void MarkAllMessagesAsRead()
    {
        if (MailboxManager.Instance != null)
        {
            MailboxManager.Instance.MarkAllMessagesAsRead();
        }
        
        Debug.Log("[MailboxUI] Mark all messages as read requested");
    }
    #endregion

    #region UI Animation
    private IEnumerator OpenMailboxCoroutine()
    {
        _isAnimating = true;
        
        if (mailboxModal != null)
        {
            mailboxModal.SetActive(true);
            mailboxModal.transform.localScale = Vector3.zero;
        }
        
        // 데이터 로드
        LoadCurrentMailboxData();
        
        float elapsedTime = 0f;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / animationDuration;
            float curveValue = openAnimationCurve.Evaluate(normalizedTime);
            
            if (mailboxModal != null)
            {
                mailboxModal.transform.localScale = Vector3.one * curveValue;
            }
            
            yield return null;
        }
        
        if (mailboxModal != null)
        {
            mailboxModal.transform.localScale = Vector3.one;
        }
        
        _isVisible = true;
        _isAnimating = false;
        
        OnMailboxOpened?.Invoke();
        Debug.Log("[MailboxUI] Mailbox opened");
    }

    private IEnumerator CloseMailboxCoroutine()
    {
        _isAnimating = true;
        
        float elapsedTime = 0f;
        Vector3 startScale = mailboxModal != null ? mailboxModal.transform.localScale : Vector3.one;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / animationDuration;
            float curveValue = closeAnimationCurve.Evaluate(normalizedTime);
            
            if (mailboxModal != null)
            {
                mailboxModal.transform.localScale = startScale * curveValue;
            }
            
            yield return null;
        }
        
        if (mailboxModal != null)
        {
            mailboxModal.SetActive(false);
        }
        
        _isVisible = false;
        _isAnimating = false;
        
        OnMailboxClosed?.Invoke();
        Debug.Log("[MailboxUI] Mailbox closed");
    }
    #endregion

    #region Message List Management
    private void LoadCurrentMailboxData()
    {
        SetLoadingState(true);
        
        if (MailboxManager.Instance != null && MailboxManager.Instance.IsInitialized)
        {
            var mailboxData = MailboxManager.Instance.MailboxData;
            if (mailboxData != null)
            {
                HandleMailboxLoaded(mailboxData);
            }
        }
        
        SetLoadingState(false);
    }

    private void LoadMessagesFromData()
    {
        if (_currentMailboxData?.messages == null)
        {
            _allMessages.Clear();
        }
        else
        {
            _allMessages = new List<MailboxMessage>(_currentMailboxData.messages);
        }
        
        SortMessages();
        ApplyFilters();
    }

    private void SortMessages()
    {
        if (_allMessages.Count == 0) return;
        
        switch (_currentSortOrder)
        {
            case SortOrder.DateAscending:
                _allMessages.Sort((a, b) => a.SentAt.CompareTo(b.SentAt));
                break;
            case SortOrder.DateDescending:
                _allMessages.Sort((a, b) => b.SentAt.CompareTo(a.SentAt));
                break;
            case SortOrder.SenderName:
                _allMessages.Sort((a, b) => string.Compare(a.senderName, b.senderName, StringComparison.OrdinalIgnoreCase));
                break;
            case SortOrder.MessageType:
                _allMessages.Sort((a, b) => a.type.CompareTo(b.type));
                break;
            case SortOrder.ReadStatus:
                _allMessages.Sort((a, b) => a.isRead.CompareTo(b.isRead));
                break;
        }
    }

    private void ApplyFilters()
    {
        _filteredMessages.Clear();
        
        foreach (var message in _allMessages)
        {
            bool passesFilter = true;
            
            // 타입 필터
            if (_currentTypeFilter != (MailMessageType)(-1) && message.type != _currentTypeFilter)
            {
                passesFilter = false;
            }
            
            // 검색 필터
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                bool matchesSearch = 
                    (message.title?.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (message.content?.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (message.senderName?.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
                
                if (!matchesSearch)
                {
                    passesFilter = false;
                }
            }
            
            if (passesFilter)
            {
                _filteredMessages.Add(message);
            }
        }
        
        RefreshMessageList();
    }

    private void RefreshMessageList()
    {
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
        }
        
        _refreshCoroutine = StartCoroutine(RefreshMessageListCoroutine());
    }

    private IEnumerator RefreshMessageListCoroutine()
    {
        // 기존 아이템들을 풀로 반환
        ReturnAllItemsToPool();
        
        // 프레임 분할하여 UI 업데이트
        yield return _waitForEndOfFrame;
        
        UpdateEmptyState();
        UpdateContentSize();
        
        if (enableVirtualization && _filteredMessages.Count > maxVisibleItems)
        {
            UpdateVirtualizedItems();
        }
        else
        {
            UpdateAllItems();
        }
        
        _refreshCoroutine = null;
    }

    private void UpdateAllItems()
    {
        for (int i = 0; i < _filteredMessages.Count; i++)
        {
            var item = GetPooledItem();
            if (item != null)
            {
                item.SetMessage(_filteredMessages[i]);
                _activeMessageItems.Add(item);
            }
        }
    }

    private void UpdateVirtualizedItems()
    {
        CalculateVisibleRange();
        
        for (int i = _firstVisibleIndex; i <= _lastVisibleIndex && i < _filteredMessages.Count; i++)
        {
            var item = GetPooledItem();
            if (item != null)
            {
                item.SetMessage(_filteredMessages[i]);
                PositionVirtualizedItem(item, i);
                _activeMessageItems.Add(item);
            }
        }
    }

    private void UpdateVirtualization()
    {
        int newFirstVisible = Mathf.FloorToInt(_scrollPosition * (_filteredMessages.Count - maxVisibleItems));
        newFirstVisible = Mathf.Clamp(newFirstVisible, 0, Mathf.Max(0, _filteredMessages.Count - maxVisibleItems));
        
        if (newFirstVisible != _firstVisibleIndex)
        {
            _firstVisibleIndex = newFirstVisible;
            _lastVisibleIndex = Mathf.Min(_firstVisibleIndex + maxVisibleItems - 1, _filteredMessages.Count - 1);
            
            RefreshMessageList();
        }
    }

    private void CalculateVisibleRange()
    {
        if (!enableVirtualization || _filteredMessages.Count <= maxVisibleItems)
        {
            _firstVisibleIndex = 0;
            _lastVisibleIndex = _filteredMessages.Count - 1;
            return;
        }
        
        _firstVisibleIndex = Mathf.FloorToInt(_scrollPosition * (_filteredMessages.Count - maxVisibleItems));
        _firstVisibleIndex = Mathf.Clamp(_firstVisibleIndex, 0, _filteredMessages.Count - maxVisibleItems);
        _lastVisibleIndex = Mathf.Min(_firstVisibleIndex + maxVisibleItems - 1, _filteredMessages.Count - 1);
    }

    private void PositionVirtualizedItem(MessageItemUI item, int index)
    {
        if (item == null) return;
        
        float yPosition = -index * itemHeight;
        var rectTransform = item.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0, yPosition);
        }
    }

    private void UpdateContentSize()
    {
        if (messageListContent == null) return;
        
        float contentHeight = _filteredMessages.Count * itemHeight;
        messageListContent.sizeDelta = new Vector2(messageListContent.sizeDelta.x, contentHeight);
    }
    #endregion

    #region Object Pool Management
    private MessageItemUI GetPooledItem()
    {
        MessageItemUI item = null;
        
        if (_messageItemPool.Count > 0)
        {
            item = _messageItemPool.Dequeue();
        }
        else
        {
            // 풀이 비어있으면 새로 생성
            item = Instantiate(messageItemPrefab, messageListContent);
        }
        
        if (item != null)
        {
            item.gameObject.SetActive(true);
        }
        
        return item;
    }

    private void ReturnItemToPool(MessageItemUI item)
    {
        if (item == null) return;
        
        item.ResetItem();
        item.gameObject.SetActive(false);
        _messageItemPool.Enqueue(item);
    }

    private void ReturnAllItemsToPool()
    {
        foreach (var item in _activeMessageItems)
        {
            ReturnItemToPool(item);
        }
        _activeMessageItems.Clear();
    }

    private void CleanupObjectPool()
    {
        ReturnAllItemsToPool();
        
        while (_messageItemPool.Count > 0)
        {
            var item = _messageItemPool.Dequeue();
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
    }
    #endregion

    #region UI State Management
    private void UpdateHeaderInfo()
    {
        if (headerTitleText != null)
        {
            headerTitleText.text = string.Format(_headerTitleFormat, _allMessages.Count);
        }
        
        if (_currentMailboxData != null)
        {
            UpdateUnreadCountDisplay(_currentMailboxData.unreadCount);
        }
    }

    private void UpdateUnreadCountDisplay(int unreadCount)
    {
        if (unreadCountText != null)
        {
            unreadCountText.text = string.Format(_unreadCountFormat, unreadCount);
        }
    }

    private void UpdateEmptyState()
    {
        bool isEmpty = _filteredMessages.Count == 0;
        
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(isEmpty && !_isLoading);
            
            if (isEmpty && !_isLoading)
            {
                if (!string.IsNullOrEmpty(_searchQuery) || _currentTypeFilter != (MailMessageType)(-1))
                {
                    emptyStateText.text = _noResultsMessage;
                }
                else
                {
                    emptyStateText.text = _emptyStateMessage;
                }
            }
        }
        
        // 버튼 상태 업데이트
        if (markAllReadButton != null)
        {
            markAllReadButton.interactable = !isEmpty && (_currentMailboxData?.unreadCount ?? 0) > 0;
        }
    }

    private void SetLoadingState(bool loading)
    {
        _isLoading = loading;
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(loading);
        }
        
        if (refreshButton != null)
        {
            refreshButton.interactable = !loading;
        }
        
        UpdateEmptyState();
    }

    private void UpdateMessageReadState(string messageId, bool isRead)
    {
        var activeItem = _activeMessageItems.FirstOrDefault(item => item.MessageId == messageId);
        if (activeItem != null)
        {
            if (isRead)
            {
                activeItem.MarkAsRead();
            }
            else
            {
                activeItem.RefreshDisplay();
            }
        }
    }

    private void RemoveMessageFromUI(string messageId)
    {
        // 데이터에서 제거
        _allMessages.RemoveAll(m => m.messageId == messageId);
        _filteredMessages.RemoveAll(m => m.messageId == messageId);
        
        // UI 새로고침
        RefreshMessageList();
        UpdateHeaderInfo();
    }

    private void ShowError(string errorMessage)
    {
        Debug.LogError($"[MailboxUI] Error: {errorMessage}");
        // TODO: 에러 UI 표시 구현
    }

    private void ToggleSortOrder()
    {
        int currentIndex = (int)_currentSortOrder;
        int nextIndex = (currentIndex + 1) % Enum.GetValues(typeof(SortOrder)).Length;
        
        _currentSortOrder = (SortOrder)nextIndex;
        
        if (sortOrderDropdown != null)
        {
            sortOrderDropdown.value = nextIndex;
        }
        
        SortMessages();
        RefreshMessageList();
    }
    #endregion
}