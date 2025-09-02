using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 실시간 플레이어 목록 UI 컴포넌트
/// 방 내 플레이어들의 정보를 실시간으로 표시하고 관리합니다.
/// 방장 권한, 준비 상태, 플레이어 관리 기능을 제공합니다.
/// </summary>
public class RoomPlayerList : MonoBehaviour
{
    #region UI References
    [Header("Player List Container")]
    [SerializeField] private Transform playerListParent;
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private ScrollRect scrollRect;
    
    [Header("List Header")]
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text listHeaderText;
    
    [Header("Empty State")]
    [SerializeField] private GameObject emptyStatePanel;
    [SerializeField] private Text emptyStateText;
    
    [Header("Layout Settings")]
    [SerializeField] private bool autoSizeContent = true;
    [SerializeField] private float itemSpacing = 5f;
    [SerializeField] private int maxVisiblePlayers = 6;
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableItemAnimations = true;
    [SerializeField] private float itemAnimationDuration = 0.3f;
    [SerializeField] private AnimationCurve itemAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion

    #region Private Fields
    private Dictionary<string, GameObject> _playerItems = new Dictionary<string, GameObject>();
    private Dictionary<string, PlayerItemUI> _playerItemComponents = new Dictionary<string, PlayerItemUI>();
    private List<PlayerInfo> _currentPlayers = new List<PlayerInfo>();
    
    private bool _isHost = false;
    private string _localPlayerId;
    private VerticalLayoutGroup _layoutGroup;
    private ContentSizeFitter _contentSizeFitter;
    
    // Performance optimization
    private Queue<GameObject> _pooledPlayerItems = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 8;
    
    // Animation coroutines
    private Dictionary<string, Coroutine> _animationCoroutines = new Dictionary<string, Coroutine>();
    #endregion

    #region Events
    public event Action<string> OnPlayerKickRequested;
    public event Action<string, bool> OnPlayerReadyToggled;
    public event Action<PlayerInfo> OnPlayerSelected;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        SetupObjectPool();
        ValidateComponents();
    }

    private void Start()
    {
        InitializePlayerList();
    }

    private void OnDestroy()
    {
        CleanupPlayerList();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Get or add required components
        if (playerListParent != null)
        {
            _layoutGroup = playerListParent.GetComponent<VerticalLayoutGroup>();
            if (_layoutGroup == null)
            {
                _layoutGroup = playerListParent.gameObject.AddComponent<VerticalLayoutGroup>();
                _layoutGroup.spacing = itemSpacing;
                _layoutGroup.childAlignment = TextAnchor.UpperCenter;
                _layoutGroup.childControlWidth = true;
                _layoutGroup.childControlHeight = false;
                _layoutGroup.childScaleWidth = false;
                _layoutGroup.childScaleHeight = false;
                _layoutGroup.childForceExpandWidth = true;
                _layoutGroup.childForceExpandHeight = false;
            }

            if (autoSizeContent)
            {
                _contentSizeFitter = playerListParent.GetComponent<ContentSizeFitter>();
                if (_contentSizeFitter == null)
                {
                    _contentSizeFitter = playerListParent.gameObject.AddComponent<ContentSizeFitter>();
                    _contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
        }
    }

    private void SetupObjectPool()
    {
        if (playerItemPrefab == null) return;

        // Pre-instantiate player items for object pooling
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            var pooledItem = CreatePlayerItem(null);
            pooledItem.SetActive(false);
            _pooledPlayerItems.Enqueue(pooledItem);
        }
    }

    private void InitializePlayerList()
    {
        // Get local player ID from UserDataManager
        if (UserDataManager.Instance != null)
        {
            _localPlayerId = UserDataManager.Instance.GetUserId();
        }
        
        UpdateEmptyState(true);
    }
    #endregion

    #region Public API
    /// <summary>
    /// 플레이어 목록 업데이트 (메인 진입점)
    /// </summary>
    public void UpdatePlayerList(List<PlayerInfo> players, bool isHost)
    {
        if (players == null) return;

        _isHost = isHost;
        _currentPlayers = new List<PlayerInfo>(players);
        
        StartCoroutine(UpdatePlayerListCoroutine(players));
    }

    /// <summary>
    /// 특정 플레이어 정보 업데이트
    /// </summary>
    public void UpdatePlayer(PlayerInfo playerInfo)
    {
        if (playerInfo == null) return;

        var existingPlayer = _currentPlayers.Find(p => p.PlayerId == playerInfo.PlayerId);
        if (existingPlayer != null)
        {
            existingPlayer.Nickname = playerInfo.Nickname;
            existingPlayer.IsReady = playerInfo.IsReady;
            existingPlayer.IsHost = playerInfo.IsHost;
            
            if (_playerItemComponents.TryGetValue(playerInfo.PlayerId, out PlayerItemUI itemUI))
            {
                itemUI.UpdatePlayerInfo(playerInfo, _isHost, _localPlayerId);
            }
        }
    }

    /// <summary>
    /// 플레이어 목록 클리어
    /// </summary>
    public void ClearPlayerList()
    {
        StartCoroutine(ClearPlayerListCoroutine());
    }

    /// <summary>
    /// 강제 새로고침
    /// </summary>
    public void ForceRefresh()
    {
        if (_currentPlayers != null && _currentPlayers.Count > 0)
        {
            UpdatePlayerList(_currentPlayers, _isHost);
        }
    }
    #endregion

    #region Core Update Logic
    private IEnumerator UpdatePlayerListCoroutine(List<PlayerInfo> players)
    {
        // Performance optimization: yield if too many updates
        yield return new WaitForEndOfFrame();
        
        try
        {
            // Update header
            UpdateListHeader(players);
            
            // Handle player additions, updates, and removals
            var playersToAdd = GetPlayersToAdd(players);
            var playersToUpdate = GetPlayersToUpdate(players);
            var playersToRemove = GetPlayersToRemove(players);
            
            // Remove players first (with animation)
            foreach (var playerId in playersToRemove)
            {
                yield return StartCoroutine(RemovePlayerCoroutine(playerId));
            }
            
            // Add new players (with animation)
            foreach (var player in playersToAdd)
            {
                yield return StartCoroutine(AddPlayerCoroutine(player));
            }
            
            // Update existing players
            foreach (var player in playersToUpdate)
            {
                UpdateExistingPlayer(player);
            }
            
            // Update layout and empty state
            UpdateLayoutAndEmpty();
            
            Debug.Log($"[RoomPlayerList] Updated player list: {players.Count} players");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomPlayerList] Error updating player list: {e.Message}");
        }
    }

    private List<PlayerInfo> GetPlayersToAdd(List<PlayerInfo> players)
    {
        return players.Where(p => !_playerItems.ContainsKey(p.PlayerId)).ToList();
    }

    private List<PlayerInfo> GetPlayersToUpdate(List<PlayerInfo> players)
    {
        return players.Where(p => _playerItems.ContainsKey(p.PlayerId)).ToList();
    }

    private List<string> GetPlayersToRemove(List<PlayerInfo> players)
    {
        var currentPlayerIds = players.Select(p => p.PlayerId).ToHashSet();
        return _playerItems.Keys.Where(id => !currentPlayerIds.Contains(id)).ToList();
    }
    #endregion

    #region Player Item Management
    private IEnumerator AddPlayerCoroutine(PlayerInfo player)
    {
        var playerItem = GetPlayerItemFromPool();
        if (playerItem == null)
        {
            Debug.LogError("[RoomPlayerList] Failed to create player item");
            yield break;
        }

        // Setup player item
        var itemUI = playerItem.GetComponent<PlayerItemUI>();
        if (itemUI == null)
        {
            Debug.LogError("[RoomPlayerList] PlayerItemUI component missing from prefab");
            ReturnPlayerItemToPool(playerItem);
            yield break;
        }

        // Configure player item
        itemUI.SetupPlayerItem(player, _isHost, _localPlayerId);
        itemUI.OnKickRequested += (playerId) => OnPlayerKickRequested?.Invoke(playerId);
        itemUI.OnReadyToggled += (playerId, ready) => OnPlayerReadyToggled?.Invoke(playerId, ready);
        itemUI.OnPlayerClicked += (playerInfo) => OnPlayerSelected?.Invoke(playerInfo);

        // Add to collections
        _playerItems[player.PlayerId] = playerItem;
        _playerItemComponents[player.PlayerId] = itemUI;

        // Animate in
        if (enableItemAnimations)
        {
            yield return StartCoroutine(AnimatePlayerItemIn(playerItem));
        }
        else
        {
            playerItem.SetActive(true);
        }
    }

    private IEnumerator RemovePlayerCoroutine(string playerId)
    {
        if (!_playerItems.TryGetValue(playerId, out GameObject playerItem))
            yield break;

        // Stop any ongoing animation
        if (_animationCoroutines.TryGetValue(playerId, out Coroutine animCoroutine))
        {
            StopCoroutine(animCoroutine);
            _animationCoroutines.Remove(playerId);
        }

        // Animate out
        if (enableItemAnimations && playerItem.activeInHierarchy)
        {
            yield return StartCoroutine(AnimatePlayerItemOut(playerItem));
        }

        // Cleanup
        var itemUI = _playerItemComponents[playerId];
        if (itemUI != null)
        {
            itemUI.OnKickRequested -= (pId) => OnPlayerKickRequested?.Invoke(pId);
            itemUI.OnReadyToggled -= (pId, ready) => OnPlayerReadyToggled?.Invoke(pId, ready);
            itemUI.OnPlayerClicked -= (playerInfo) => OnPlayerSelected?.Invoke(playerInfo);
        }

        // Remove from collections
        _playerItems.Remove(playerId);
        _playerItemComponents.Remove(playerId);

        // Return to pool
        ReturnPlayerItemToPool(playerItem);
    }

    private void UpdateExistingPlayer(PlayerInfo player)
    {
        if (_playerItemComponents.TryGetValue(player.PlayerId, out PlayerItemUI itemUI))
        {
            itemUI.UpdatePlayerInfo(player, _isHost, _localPlayerId);
        }
    }

    private GameObject GetPlayerItemFromPool()
    {
        GameObject playerItem;
        
        if (_pooledPlayerItems.Count > 0)
        {
            playerItem = _pooledPlayerItems.Dequeue();
        }
        else
        {
            playerItem = CreatePlayerItem(null);
        }

        if (playerItem != null && playerListParent != null)
        {
            playerItem.transform.SetParent(playerListParent, false);
        }

        return playerItem;
    }

    private void ReturnPlayerItemToPool(GameObject playerItem)
    {
        if (playerItem == null) return;

        playerItem.SetActive(false);
        playerItem.transform.SetParent(transform, false); // Move to pool container
        
        // Reset item state
        var itemUI = playerItem.GetComponent<PlayerItemUI>();
        itemUI?.Reset();
        
        _pooledPlayerItems.Enqueue(playerItem);
    }

    private GameObject CreatePlayerItem(PlayerInfo player)
    {
        if (playerItemPrefab == null)
        {
            Debug.LogError("[RoomPlayerList] Player item prefab is not assigned!");
            return null;
        }

        var playerItem = Instantiate(playerItemPrefab, transform);
        
        // Ensure PlayerItemUI component exists
        if (playerItem.GetComponent<PlayerItemUI>() == null)
        {
            Debug.LogError("[RoomPlayerList] PlayerItemUI component missing from prefab! Adding default component.");
            playerItem.AddComponent<PlayerItemUI>();
        }

        return playerItem;
    }
    #endregion

    #region Animations
    private IEnumerator AnimatePlayerItemIn(GameObject playerItem)
    {
        if (playerItem == null) yield break;

        var canvasGroup = playerItem.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = playerItem.AddComponent<CanvasGroup>();

        var rectTransform = playerItem.GetComponent<RectTransform>();
        
        // Setup initial state
        playerItem.SetActive(true);
        canvasGroup.alpha = 0f;
        
        if (rectTransform != null)
        {
            var originalScale = rectTransform.localScale;
            rectTransform.localScale = Vector3.zero;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < itemAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / itemAnimationDuration;
                float curveValue = itemAnimationCurve.Evaluate(progress);
                
                canvasGroup.alpha = curveValue;
                rectTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, curveValue);
                
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
            rectTransform.localScale = originalScale;
        }
        else
        {
            // Fallback: just fade in
            float elapsedTime = 0f;
            
            while (elapsedTime < itemAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / itemAnimationDuration;
                canvasGroup.alpha = itemAnimationCurve.Evaluate(progress);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator AnimatePlayerItemOut(GameObject playerItem)
    {
        if (playerItem == null) yield break;

        var canvasGroup = playerItem.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = playerItem.AddComponent<CanvasGroup>();

        var rectTransform = playerItem.GetComponent<RectTransform>();
        
        if (rectTransform != null)
        {
            var originalScale = rectTransform.localScale;
            float elapsedTime = 0f;
            
            while (elapsedTime < itemAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / itemAnimationDuration;
                float curveValue = 1f - itemAnimationCurve.Evaluate(progress);
                
                canvasGroup.alpha = curveValue;
                rectTransform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
                
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            rectTransform.localScale = Vector3.zero;
        }
        else
        {
            // Fallback: just fade out
            float elapsedTime = 0f;
            
            while (elapsedTime < itemAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / itemAnimationDuration;
                canvasGroup.alpha = 1f - itemAnimationCurve.Evaluate(progress);
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
        }
    }
    #endregion

    #region UI Updates
    private void UpdateListHeader(List<PlayerInfo> players)
    {
        if (playerCountText != null)
        {
            int maxPlayers = 4; // Default, could be set from room data
            playerCountText.text = $"{players.Count}/{maxPlayers}";
        }

        if (listHeaderText != null)
        {
            listHeaderText.text = "플레이어 목록";
        }
    }

    private void UpdateLayoutAndEmpty()
    {
        bool isEmpty = _playerItems.Count == 0;
        UpdateEmptyState(isEmpty);
        
        if (autoSizeContent && _contentSizeFitter != null)
        {
            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerListParent as RectTransform);
        }
    }

    private void UpdateEmptyState(bool isEmpty)
    {
        if (emptyStatePanel != null)
        {
            emptyStatePanel.SetActive(isEmpty);
        }

        if (emptyStateText != null && isEmpty)
        {
            emptyStateText.text = "플레이어가 없습니다";
        }

        if (scrollRect != null)
        {
            scrollRect.gameObject.SetActive(!isEmpty);
        }
    }

    private IEnumerator ClearPlayerListCoroutine()
    {
        var playerIds = new List<string>(_playerItems.Keys);
        
        foreach (var playerId in playerIds)
        {
            yield return StartCoroutine(RemovePlayerCoroutine(playerId));
        }
        
        _currentPlayers.Clear();
        UpdateEmptyState(true);
        
        Debug.Log("[RoomPlayerList] Player list cleared");
    }
    #endregion

    #region Utility Methods
    public int GetPlayerCount()
    {
        return _playerItems.Count;
    }

    public bool HasPlayer(string playerId)
    {
        return _playerItems.ContainsKey(playerId);
    }

    public PlayerInfo GetPlayerInfo(string playerId)
    {
        return _currentPlayers.Find(p => p.PlayerId == playerId);
    }

    public List<PlayerInfo> GetCurrentPlayers()
    {
        return new List<PlayerInfo>(_currentPlayers);
    }

    public void SetMaxVisiblePlayers(int maxPlayers)
    {
        maxVisiblePlayers = Mathf.Max(1, maxPlayers);
        
        if (scrollRect != null && scrollRect.content != null)
        {
            // Update scroll rect height based on max visible players
            var rectTransform = scrollRect.GetComponent<RectTransform>();
            if (rectTransform != null && playerItemPrefab != null)
            {
                float itemHeight = playerItemPrefab.GetComponent<RectTransform>().rect.height;
                float totalHeight = (itemHeight + itemSpacing) * maxVisiblePlayers;
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            }
        }
    }
    #endregion

    #region Component Validation
    private void ValidateComponents()
    {
        if (playerListParent == null)
        {
            Debug.LogError("[RoomPlayerList] Player List Parent is not assigned!");
        }

        if (playerItemPrefab == null)
        {
            Debug.LogError("[RoomPlayerList] Player Item Prefab is not assigned!");
        }
        else if (playerItemPrefab.GetComponent<PlayerItemUI>() == null)
        {
            Debug.LogError("[RoomPlayerList] Player Item Prefab must have PlayerItemUI component!");
        }

        if (scrollRect == null)
        {
            Debug.LogWarning("[RoomPlayerList] Scroll Rect is not assigned - list won't be scrollable");
        }
    }

    private void CleanupPlayerList()
    {
        // Stop all running coroutines
        foreach (var coroutine in _animationCoroutines.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }
        _animationCoroutines.Clear();

        // Cleanup player items
        foreach (var kvp in _playerItemComponents)
        {
            if (kvp.Value != null)
            {
                kvp.Value.OnKickRequested -= (playerId) => OnPlayerKickRequested?.Invoke(playerId);
                kvp.Value.OnReadyToggled -= (playerId, ready) => OnPlayerReadyToggled?.Invoke(playerId, ready);
                kvp.Value.OnPlayerClicked -= (playerInfo) => OnPlayerSelected?.Invoke(playerInfo);
            }
        }

        _playerItems.Clear();
        _playerItemComponents.Clear();
        _currentPlayers.Clear();

        // Clear event handlers
        OnPlayerKickRequested = null;
        OnPlayerReadyToggled = null;
        OnPlayerSelected = null;

        Debug.Log("[RoomPlayerList] Player list cleaned up");
    }
    #endregion
}

/// <summary>
/// 개별 플레이어 아이템 UI 컴포넌트
/// 플레이어 정보 표시 및 상호작용을 담당합니다.
/// </summary>
public class PlayerItemUI : MonoBehaviour
{
    [Header("Player Info Display")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text playerLevelText;
    [SerializeField] private Image playerAvatar;
    [SerializeField] private GameObject hostIndicator;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject localPlayerIndicator;

    [Header("Interactive Elements")]
    [SerializeField] private Button playerButton;
    [SerializeField] private Button kickButton;
    [SerializeField] private Toggle readyToggle;

    [Header("Visual States")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hostColor = Color.yellow;
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    #region Events
    public event Action<string> OnKickRequested;
    public event Action<string, bool> OnReadyToggled;
    public event Action<PlayerInfo> OnPlayerClicked;
    #endregion

    #region Private Fields
    private PlayerInfo _playerInfo;
    private bool _isHostUser;
    private bool _isLocalPlayer;
    private Image _backgroundImage;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        SetupEventHandlers();
    }

    private void OnDestroy()
    {
        CleanupEventHandlers();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        _backgroundImage = GetComponent<Image>();
        
        // Add components if missing
        if (playerButton == null)
        {
            playerButton = GetComponent<Button>();
            if (playerButton == null)
                playerButton = gameObject.AddComponent<Button>();
        }
    }

    private void SetupEventHandlers()
    {
        if (playerButton != null)
            playerButton.onClick.AddListener(OnPlayerButtonClicked);

        if (kickButton != null)
            kickButton.onClick.AddListener(OnKickButtonClicked);

        if (readyToggle != null)
            readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);
    }

    private void CleanupEventHandlers()
    {
        if (playerButton != null)
            playerButton.onClick.RemoveListener(OnPlayerButtonClicked);

        if (kickButton != null)
            kickButton.onClick.RemoveListener(OnKickButtonClicked);

        if (readyToggle != null)
            readyToggle.onValueChanged.RemoveListener(OnReadyToggleChanged);
    }
    #endregion

    #region Public API
    /// <summary>
    /// 플레이어 아이템 설정
    /// </summary>
    public void SetupPlayerItem(PlayerInfo playerInfo, bool isHostUser, string localPlayerId)
    {
        _playerInfo = playerInfo;
        _isHostUser = isHostUser;
        _isLocalPlayer = playerInfo.PlayerId == localPlayerId;
        
        UpdatePlayerInfo(playerInfo, isHostUser, localPlayerId);
    }

    /// <summary>
    /// 플레이어 정보 업데이트
    /// </summary>
    public void UpdatePlayerInfo(PlayerInfo playerInfo, bool isHostUser, string localPlayerId)
    {
        if (playerInfo == null) return;

        _playerInfo = playerInfo;
        _isHostUser = isHostUser;
        _isLocalPlayer = playerInfo.PlayerId == localPlayerId;

        UpdatePlayerDisplay();
        UpdateInteractiveElements();
        UpdateVisualState();
    }

    /// <summary>
    /// 아이템 리셋 (풀링용)
    /// </summary>
    public void Reset()
    {
        _playerInfo = null;
        _isHostUser = false;
        _isLocalPlayer = false;
        
        if (playerNameText != null) playerNameText.text = "";
        if (playerLevelText != null) playerLevelText.text = "";
        if (hostIndicator != null) hostIndicator.SetActive(false);
        if (readyIndicator != null) readyIndicator.SetActive(false);
        if (localPlayerIndicator != null) localPlayerIndicator.SetActive(false);
        if (kickButton != null) kickButton.gameObject.SetActive(false);
        if (readyToggle != null) 
        {
            readyToggle.isOn = false;
            readyToggle.interactable = false;
        }
    }
    #endregion

    #region UI Updates
    private void UpdatePlayerDisplay()
    {
        if (_playerInfo == null) return;

        if (playerNameText != null)
            playerNameText.text = _playerInfo.Nickname;

        if (playerLevelText != null)
            playerLevelText.text = $"Lv.{_playerInfo.PlayerId.GetHashCode() % 50 + 1}"; // Temp level display

        if (hostIndicator != null)
            hostIndicator.SetActive(_playerInfo.IsHost);

        if (readyIndicator != null)
            readyIndicator.SetActive(_playerInfo.IsReady);

        if (localPlayerIndicator != null)
            localPlayerIndicator.SetActive(_isLocalPlayer);
    }

    private void UpdateInteractiveElements()
    {
        if (_playerInfo == null) return;

        // Kick button - only show for host, and not for the host themselves
        if (kickButton != null)
        {
            bool showKick = _isHostUser && !_playerInfo.IsHost && !_isLocalPlayer;
            kickButton.gameObject.SetActive(showKick);
        }

        // Ready toggle - only show for the local player and not for host
        if (readyToggle != null)
        {
            bool showReadyToggle = _isLocalPlayer && !_playerInfo.IsHost;
            readyToggle.gameObject.SetActive(showReadyToggle);
            
            if (showReadyToggle)
            {
                readyToggle.interactable = true;
                readyToggle.SetIsOnWithoutNotify(_playerInfo.IsReady);
            }
        }
    }

    private void UpdateVisualState()
    {
        if (_playerInfo == null) return;

        Color targetColor = normalColor;

        if (_playerInfo.IsHost)
            targetColor = hostColor;
        else if (_playerInfo.IsReady)
            targetColor = readyColor;
        else
            targetColor = notReadyColor;

        if (_backgroundImage != null)
            _backgroundImage.color = targetColor;

        // Update text colors based on state
        if (playerNameText != null)
        {
            playerNameText.color = _isLocalPlayer ? Color.white : Color.black;
        }
    }
    #endregion

    #region Event Handlers
    private void OnPlayerButtonClicked()
    {
        if (_playerInfo != null)
        {
            OnPlayerClicked?.Invoke(_playerInfo);
            Debug.Log($"[PlayerItemUI] Player clicked: {_playerInfo.Nickname}");
        }
    }

    private void OnKickButtonClicked()
    {
        if (_playerInfo != null && _isHostUser && !_playerInfo.IsHost)
        {
            OnKickRequested?.Invoke(_playerInfo.PlayerId);
            Debug.Log($"[PlayerItemUI] Kick requested for: {_playerInfo.Nickname}");
        }
    }

    private void OnReadyToggleChanged(bool isReady)
    {
        if (_playerInfo != null && _isLocalPlayer)
        {
            OnReadyToggled?.Invoke(_playerInfo.PlayerId, isReady);
            Debug.Log($"[PlayerItemUI] Ready state changed: {_playerInfo.Nickname} = {isReady}");
        }
    }
    #endregion
}