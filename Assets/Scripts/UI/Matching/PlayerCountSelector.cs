using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 인원수 선택 컴포넌트 (2-4명)
/// 시각적 피드백과 부드러운 애니메이션을 제공합니다.
/// 60FPS 유지를 위해 최적화된 UI 업데이트를 수행합니다.
/// </summary>
public class PlayerCountSelector : MonoBehaviour
{
    #region UI References
    [Header("Player Count Buttons")]
    [SerializeField] private Button player2Button;
    [SerializeField] private Button player3Button;
    [SerializeField] private Button player4Button;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image player2Background;
    [SerializeField] private Image player3Background;
    [SerializeField] private Image player4Background;
    
    [Header("Text Components")]
    [SerializeField] private Text player2Text;
    [SerializeField] private Text player3Text;
    [SerializeField] private Text player4Text;
    [SerializeField] private Text selectedCountText;
    
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private AnimationCurve selectionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float scaleMultiplier = 1.1f;
    
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color hoveredColor = Color.yellow;
    [SerializeField] private Color disabledColor = Color.gray;
    
    [Header("Sound Effects")]
    [SerializeField] private AudioClip selectionSound;
    [SerializeField] private AudioClip hoverSound;
    #endregion
    
    #region Private Fields
    private int _currentPlayerCount = 2;
    private bool _isInitialized = false;
    private Dictionary<int, ButtonData> _buttonData;
    private Coroutine _animationCoroutine;
    private AudioSource _audioSource;
    
    // Optimization: cached components
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    
    // Visual state tracking
    private int _hoveredPlayerCount = -1;
    private bool _isInteractable = true;
    #endregion
    
    #region Events
    public event Action<int> OnPlayerCountChanged;
    public event Action<int> OnPlayerCountHovered;
    public event Action OnPlayerCountUnhovered;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        ValidateReferences();
        SetupButtonData();
    }
    
    private void Start()
    {
        SetupEventListeners();
        SetPlayerCount(_currentPlayerCount);
        _isInitialized = true;
    }
    
    private void OnEnable()
    {
        if (_isInitialized)
        {
            RefreshVisualState();
        }
    }
    
    private void OnDisable()
    {
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
    }
    
    private void OnDestroy()
    {
        CleanupEventListeners();
    }
    #endregion
    
    #region Initialization
    private void InitializeComponents()
    {
        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }
    
    private void ValidateReferences()
    {
        if (player2Button == null) Debug.LogError("[PlayerCountSelector] Player 2 button is missing!");
        if (player3Button == null) Debug.LogError("[PlayerCountSelector] Player 3 button is missing!");
        if (player4Button == null) Debug.LogError("[PlayerCountSelector] Player 4 button is missing!");
        
        // Optional components (warnings only)
        if (selectedCountText == null) Debug.LogWarning("[PlayerCountSelector] Selected count text is missing");
        if (selectionSound == null) Debug.LogWarning("[PlayerCountSelector] Selection sound is missing");
    }
    
    private void SetupButtonData()
    {
        _buttonData = new Dictionary<int, ButtonData>
        {
            {
                2, new ButtonData
                {
                    Button = player2Button,
                    Background = player2Background,
                    Text = player2Text,
                    OriginalScale = player2Button?.transform.localScale ?? Vector3.one
                }
            },
            {
                3, new ButtonData
                {
                    Button = player3Button,
                    Background = player3Background,
                    Text = player3Text,
                    OriginalScale = player3Button?.transform.localScale ?? Vector3.one
                }
            },
            {
                4, new ButtonData
                {
                    Button = player4Button,
                    Background = player4Background,
                    Text = player4Text,
                    OriginalScale = player4Button?.transform.localScale ?? Vector3.one
                }
            }
        };
    }
    #endregion
    
    #region Event Management
    private void SetupEventListeners()
    {
        if (player2Button != null)
        {
            player2Button.onClick.AddListener(() => SelectPlayerCount(2));
            AddHoverListeners(player2Button, 2);
        }
        
        if (player3Button != null)
        {
            player3Button.onClick.AddListener(() => SelectPlayerCount(3));
            AddHoverListeners(player3Button, 3);
        }
        
        if (player4Button != null)
        {
            player4Button.onClick.AddListener(() => SelectPlayerCount(4));
            AddHoverListeners(player4Button, 4);
        }
    }
    
    private void AddHoverListeners(Button button, int playerCount)
    {
        var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null)
            eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        // Hover enter
        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener((data) => OnButtonHoverEnter(playerCount));
        eventTrigger.triggers.Add(enterEntry);
        
        // Hover exit
        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener((data) => OnButtonHoverExit(playerCount));
        eventTrigger.triggers.Add(exitEntry);
    }
    
    private void CleanupEventListeners()
    {
        if (player2Button != null) player2Button.onClick.RemoveAllListeners();
        if (player3Button != null) player3Button.onClick.RemoveAllListeners();
        if (player4Button != null) player4Button.onClick.RemoveAllListeners();
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 컴포넌트를 초기화합니다
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;
        
        Debug.Log("[PlayerCountSelector] Initializing component");
        SetPlayerCount(_currentPlayerCount);
        RefreshVisualState();
        _isInitialized = true;
    }
    
    /// <summary>
    /// 플레이어 수를 설정합니다
    /// </summary>
    /// <param name="playerCount">설정할 플레이어 수 (2-4)</param>
    public void SetPlayerCount(int playerCount)
    {
        if (playerCount < 2 || playerCount > 4)
        {
            Debug.LogWarning($"[PlayerCountSelector] Invalid player count: {playerCount}. Must be 2-4.");
            return;
        }
        
        if (_currentPlayerCount == playerCount) return;
        
        int previousCount = _currentPlayerCount;
        _currentPlayerCount = playerCount;
        
        UpdateVisualSelection();
        UpdateSelectedCountText();
        
        OnPlayerCountChanged?.Invoke(playerCount);
        
        Debug.Log($"[PlayerCountSelector] Player count changed: {previousCount} -> {playerCount}");
    }
    
    /// <summary>
    /// 현재 선택된 플레이어 수를 반환합니다
    /// </summary>
    public int GetSelectedPlayerCount()
    {
        return _currentPlayerCount;
    }
    
    /// <summary>
    /// 상호작용 가능 여부를 설정합니다
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;
        _canvasGroup.interactable = interactable;
        _canvasGroup.alpha = interactable ? 1f : 0.5f;
        
        RefreshButtonStates();
    }
    
    /// <summary>
    /// UI를 새로고침합니다
    /// </summary>
    public void RefreshUI()
    {
        RefreshVisualState();
        RefreshButtonStates();
    }
    
    /// <summary>
    /// 특정 플레이어 수 버튼을 활성화/비활성화합니다
    /// </summary>
    public void SetPlayerCountAvailable(int playerCount, bool available)
    {
        if (_buttonData.TryGetValue(playerCount, out ButtonData data))
        {
            if (data.Button != null)
            {
                data.Button.interactable = available && _isInteractable;
                
                // Visual feedback for disabled state
                Color color = available ? normalColor : disabledColor;
                if (playerCount != _currentPlayerCount)
                {
                    if (data.Background != null)
                        data.Background.color = color;
                }
            }
        }
    }
    #endregion
    
    #region Private Methods
    private void SelectPlayerCount(int playerCount)
    {
        if (!_isInteractable) return;
        
        SetPlayerCount(playerCount);
        PlaySelectionSound();
        AnimateSelection(playerCount);
    }
    
    private void OnButtonHoverEnter(int playerCount)
    {
        if (!_isInteractable) return;
        
        _hoveredPlayerCount = playerCount;
        PlayHoverSound();
        UpdateHoverState(playerCount, true);
        OnPlayerCountHovered?.Invoke(playerCount);
    }
    
    private void OnButtonHoverExit(int playerCount)
    {
        if (!_isInteractable) return;
        
        _hoveredPlayerCount = -1;
        UpdateHoverState(playerCount, false);
        OnPlayerCountUnhovered?.Invoke();
    }
    
    private void UpdateVisualSelection()
    {
        foreach (var kvp in _buttonData)
        {
            int playerCount = kvp.Key;
            ButtonData data = kvp.Value;
            
            bool isSelected = playerCount == _currentPlayerCount;
            bool isHovered = playerCount == _hoveredPlayerCount;
            
            Color targetColor = isSelected ? selectedColor : 
                               isHovered ? hoveredColor : normalColor;
            
            if (data.Background != null)
                data.Background.color = targetColor;
            
            if (data.Text != null)
                data.Text.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
        }
    }
    
    private void UpdateHoverState(int playerCount, bool isHovered)
    {
        if (_buttonData.TryGetValue(playerCount, out ButtonData data))
        {
            if (playerCount != _currentPlayerCount) // Don't change selected button color
            {
                Color targetColor = isHovered ? hoveredColor : normalColor;
                if (data.Background != null)
                    data.Background.color = targetColor;
            }
        }
    }
    
    private void UpdateSelectedCountText()
    {
        if (selectedCountText != null)
        {
            selectedCountText.text = $"선택된 인원: {_currentPlayerCount}명";
        }
    }
    
    private void RefreshVisualState()
    {
        UpdateVisualSelection();
        UpdateSelectedCountText();
    }
    
    private void RefreshButtonStates()
    {
        foreach (var kvp in _buttonData)
        {
            ButtonData data = kvp.Value;
            if (data.Button != null)
            {
                data.Button.interactable = _isInteractable;
            }
        }
    }
    
    private void AnimateSelection(int playerCount)
    {
        if (!_buttonData.TryGetValue(playerCount, out ButtonData data) || data.Button == null)
            return;
        
        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);
        
        _animationCoroutine = StartCoroutine(AnimateButtonSelection(data));
    }
    
    private IEnumerator AnimateButtonSelection(ButtonData data)
    {
        Transform buttonTransform = data.Button.transform;
        Vector3 originalScale = data.OriginalScale;
        Vector3 targetScale = originalScale * scaleMultiplier;
        
        float elapsed = 0f;
        
        // Scale up
        while (elapsed < animationDuration / 2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = selectionCurve.Evaluate(elapsed / (animationDuration / 2));
            buttonTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }
        
        elapsed = 0f;
        
        // Scale down
        while (elapsed < animationDuration / 2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = selectionCurve.Evaluate(elapsed / (animationDuration / 2));
            buttonTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }
        
        buttonTransform.localScale = originalScale;
        _animationCoroutine = null;
    }
    
    private void PlaySelectionSound()
    {
        if (_audioSource != null && selectionSound != null)
        {
            _audioSource.PlayOneShot(selectionSound);
        }
    }
    
    private void PlayHoverSound()
    {
        if (_audioSource != null && hoverSound != null)
        {
            _audioSource.PlayOneShot(hoverSound, 0.5f);
        }
    }
    #endregion
    
    #region Data Structures
    [Serializable]
    private class ButtonData
    {
        public Button Button;
        public Image Background;
        public Text Text;
        public Vector3 OriginalScale;
    }
    #endregion
    
    #region Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Test Player Count Selection")]
    private void TestPlayerCountSelection()
    {
        if (!Application.isPlaying) return;
        
        Debug.Log("[PlayerCountSelector] Testing player count selection...");
        StartCoroutine(TestSelectionSequence());
    }
    
    private IEnumerator TestSelectionSequence()
    {
        for (int i = 2; i <= 4; i++)
        {
            SelectPlayerCount(i);
            yield return new WaitForSeconds(1f);
        }
    }
    #endif
    #endregion
}