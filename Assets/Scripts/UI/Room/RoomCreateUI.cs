using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 생성 UI 컴포넌트
/// 방 생성 모달의 설정 옵션, 플레이어 수 선택, 생성 확인을 담당합니다.
/// 사용자 친화적인 방 설정 인터페이스를 제공합니다.
/// </summary>
public class RoomCreateUI : MonoBehaviour
{
    #region UI References
    [Header("Modal Components")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private Text modalTitleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;
    
    [Header("Player Count Selection")]
    [SerializeField] private Slider playerCountSlider;
    [SerializeField] private Text playerCountValueText;
    [SerializeField] private Text playerCountDescriptionText;
    [SerializeField] private GameObject[] playerCountButtons; // 2, 3, 4 player buttons
    
    [Header("Room Settings")]
    [SerializeField] private Toggle privateRoomToggle;
    [SerializeField] private InputField roomNameInputField;
    [SerializeField] private Text roomNameCharCountText;
    [SerializeField] private Dropdown gameTypeDropdown;
    
    [Header("Energy Cost Display")]
    [SerializeField] private GameObject energyCostPanel;
    [SerializeField] private Text energyCostText;
    [SerializeField] private Image energyIcon;
    [SerializeField] private Text energyWarningText;
    
    [Header("Action Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Preview Display")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Text previewSummaryText;
    [SerializeField] private Text estimatedWaitTimeText;
    
    [Header("Animation Settings")]
    [SerializeField] private float modalAnimationDuration = 0.3f;
    [SerializeField] private AnimationCurve modalAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool enableScaleAnimation = true;
    [SerializeField] private bool enableFadeAnimation = true;
    #endregion

    #region Private Fields
    private int _selectedPlayerCount = 4;
    private bool _isPrivateRoom = false;
    private string _roomName = "";
    private int _selectedGameType = 0;
    private bool _isCreating = false;
    
    // Modal animation
    private Coroutine _modalAnimationCoroutine;
    private CanvasGroup _modalCanvasGroup;
    private RectTransform _modalRectTransform;
    private Vector3 _originalModalScale;
    
    // Energy system integration
    private UserDataManager _userDataManager;
    private EnergyManager _energyManager;
    
    // UI state
    private bool _isInitialized = false;
    private readonly Dictionary<int, string> _playerCountDescriptions = new Dictionary<int, string>
    {
        { 2, "빠른 1:1 대결" },
        { 3, "긴장감 넘치는 3인전" },
        { 4, "클래식 4인 배틀" }
    };
    
    // Room name validation
    private const int MAX_ROOM_NAME_LENGTH = 20;
    private const int MIN_ROOM_NAME_LENGTH = 0;
    #endregion

    #region Events
    public event Action<int> OnRoomCreationRequested;
    public event Action OnCreateRoomCancelled;
    public event Action<RoomCreationSettings> OnSettingsChanged;
    #endregion

    #region Properties
    public int SelectedPlayerCount => _selectedPlayerCount;
    public bool IsPrivateRoom => _isPrivateRoom;
    public string RoomName => _roomName;
    public bool IsCreating => _isCreating;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
        SetupEventHandlers();
        ValidateComponents();
    }

    private void Start()
    {
        InitializeRoomCreationUI();
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            ShowModal();
        }
    }

    private void OnDisable()
    {
        if (_modalAnimationCoroutine != null)
        {
            StopCoroutine(_modalAnimationCoroutine);
            _modalAnimationCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        CleanupEventHandlers();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Get modal animation components
        if (modalPanel != null)
        {
            _modalCanvasGroup = modalPanel.GetComponent<CanvasGroup>();
            if (_modalCanvasGroup == null && enableFadeAnimation)
                _modalCanvasGroup = modalPanel.AddComponent<CanvasGroup>();

            _modalRectTransform = modalPanel.GetComponent<RectTransform>();
            if (_modalRectTransform != null)
                _originalModalScale = _modalRectTransform.localScale;
        }

        // Get manager references
        _userDataManager = UserDataManager.Instance;
        _energyManager = EnergyManager.Instance;
    }

    private void SetupEventHandlers()
    {
        // Player count controls
        if (playerCountSlider != null)
            playerCountSlider.onValueChanged.AddListener(OnPlayerCountSliderChanged);

        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            if (playerCountButtons[i] != null)
            {
                int playerCount = i + 2; // 2, 3, 4 players
                var button = playerCountButtons[i].GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => SetPlayerCount(playerCount));
            }
        }

        // Room settings
        if (privateRoomToggle != null)
            privateRoomToggle.onValueChanged.AddListener(OnPrivateRoomToggleChanged);

        if (roomNameInputField != null)
        {
            roomNameInputField.onValueChanged.AddListener(OnRoomNameChanged);
            roomNameInputField.characterLimit = MAX_ROOM_NAME_LENGTH;
        }

        if (gameTypeDropdown != null)
            gameTypeDropdown.onValueChanged.AddListener(OnGameTypeChanged);

        // Action buttons
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCancelButtonClicked);

        if (backgroundButton != null)
            backgroundButton.onClick.AddListener(OnCancelButtonClicked);
    }

    private void InitializeRoomCreationUI()
    {
        SetupPlayerCountSelection();
        SetupGameTypeDropdown();
        SetupInitialValues();
        UpdateUIDisplay();
        UpdateEnergyDisplay();
        
        _isInitialized = true;
    }

    private void SetupPlayerCountSelection()
    {
        if (playerCountSlider != null)
        {
            playerCountSlider.minValue = 2;
            playerCountSlider.maxValue = 4;
            playerCountSlider.wholeNumbers = true;
            playerCountSlider.value = _selectedPlayerCount;
        }

        UpdatePlayerCountButtons();
    }

    private void SetupGameTypeDropdown()
    {
        if (gameTypeDropdown != null)
        {
            gameTypeDropdown.ClearOptions();
            var options = new List<string> { "클래식", "스피드", "챌린지", "랭크" };
            gameTypeDropdown.AddOptions(options);
            gameTypeDropdown.value = _selectedGameType;
        }
    }

    private void SetupInitialValues()
    {
        _selectedPlayerCount = 4;
        _isPrivateRoom = false;
        _roomName = "";
        _selectedGameType = 0;
        _isCreating = false;

        if (privateRoomToggle != null)
            privateRoomToggle.isOn = _isPrivateRoom;

        if (roomNameInputField != null)
            roomNameInputField.text = _roomName;
    }
    #endregion

    #region Modal Animation
    private void ShowModal()
    {
        if (_modalAnimationCoroutine != null)
            StopCoroutine(_modalAnimationCoroutine);
        
        _modalAnimationCoroutine = StartCoroutine(AnimateModalVisibility(true));
    }

    private void HideModal()
    {
        if (_modalAnimationCoroutine != null)
            StopCoroutine(_modalAnimationCoroutine);
        
        _modalAnimationCoroutine = StartCoroutine(AnimateModalVisibility(false));
    }

    private IEnumerator AnimateModalVisibility(bool show)
    {
        if (modalPanel == null) yield break;

        // Ensure modal is active for show animation
        if (show && !modalPanel.activeInHierarchy)
            modalPanel.SetActive(true);

        float startTime = Time.time;
        float startAlpha = _modalCanvasGroup != null ? _modalCanvasGroup.alpha : 1f;
        float targetAlpha = show ? 1f : 0f;
        
        Vector3 startScale = _modalRectTransform != null ? _modalRectTransform.localScale : Vector3.one;
        Vector3 targetScale = show ? _originalModalScale : Vector3.zero;

        while (Time.time - startTime < modalAnimationDuration)
        {
            float progress = (Time.time - startTime) / modalAnimationDuration;
            float curveValue = modalAnimationCurve.Evaluate(progress);

            // Animate fade
            if (enableFadeAnimation && _modalCanvasGroup != null)
            {
                _modalCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);
            }

            // Animate scale
            if (enableScaleAnimation && _modalRectTransform != null)
            {
                _modalRectTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            }

            yield return null;
        }

        // Ensure final values
        if (enableFadeAnimation && _modalCanvasGroup != null)
            _modalCanvasGroup.alpha = targetAlpha;

        if (enableScaleAnimation && _modalRectTransform != null)
            _modalRectTransform.localScale = targetScale;

        // Hide modal after hide animation
        if (!show)
            modalPanel.SetActive(false);

        _modalAnimationCoroutine = null;
    }
    #endregion

    #region Event Handlers
    private void OnPlayerCountSliderChanged(float value)
    {
        SetPlayerCount(Mathf.RoundToInt(value));
    }

    private void OnPrivateRoomToggleChanged(bool isPrivate)
    {
        _isPrivateRoom = isPrivate;
        UpdateUIDisplay();
        NotifySettingsChanged();
    }

    private void OnRoomNameChanged(string roomName)
    {
        _roomName = roomName.Trim();
        UpdateRoomNameDisplay();
        UpdateCreateButtonState();
        NotifySettingsChanged();
    }

    private void OnGameTypeChanged(int gameTypeIndex)
    {
        _selectedGameType = gameTypeIndex;
        UpdateEnergyDisplay();
        UpdateUIDisplay();
        NotifySettingsChanged();
    }

    private void OnCreateRoomButtonClicked()
    {
        if (_isCreating || !CanCreateRoom()) return;

        RequestCreateRoom();
    }

    private void OnCancelButtonClicked()
    {
        if (_isCreating) return;

        OnCreateRoomCancelled?.Invoke();
        HideModal();
    }
    #endregion

    #region Player Count Management
    private void SetPlayerCount(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 2, 4);
        
        if (_selectedPlayerCount == playerCount) return;
        
        _selectedPlayerCount = playerCount;
        
        if (playerCountSlider != null)
            playerCountSlider.SetValueWithoutNotify(_selectedPlayerCount);
        
        UpdatePlayerCountDisplay();
        UpdatePlayerCountButtons();
        UpdateEnergyDisplay();
        UpdateUIDisplay();
        NotifySettingsChanged();
    }

    private void UpdatePlayerCountDisplay()
    {
        if (playerCountValueText != null)
            playerCountValueText.text = $"{_selectedPlayerCount}명";

        if (playerCountDescriptionText != null)
        {
            if (_playerCountDescriptions.TryGetValue(_selectedPlayerCount, out string description))
                playerCountDescriptionText.text = description;
            else
                playerCountDescriptionText.text = $"{_selectedPlayerCount}인 게임";
        }
    }

    private void UpdatePlayerCountButtons()
    {
        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            if (playerCountButtons[i] != null)
            {
                int buttonPlayerCount = i + 2;
                bool isSelected = buttonPlayerCount == _selectedPlayerCount;
                
                var button = playerCountButtons[i].GetComponent<Button>();
                if (button != null)
                {
                    // Update button visual state
                    var colors = button.colors;
                    colors.normalColor = isSelected ? Color.green : Color.white;
                    button.colors = colors;
                }

                // Update any selection indicator
                var selectionIndicator = playerCountButtons[i].transform.Find("SelectionIndicator");
                if (selectionIndicator != null)
                    selectionIndicator.gameObject.SetActive(isSelected);
            }
        }
    }
    #endregion

    #region UI Updates
    private void UpdateUIDisplay()
    {
        UpdatePreviewDisplay();
        UpdateCreateButtonState();
        UpdateModalTitle();
    }

    private void UpdatePreviewDisplay()
    {
        if (previewSummaryText != null)
        {
            string gameTypeName = GetGameTypeName(_selectedGameType);
            string privacyText = _isPrivateRoom ? "비공개" : "공개";
            string nameText = string.IsNullOrEmpty(_roomName) ? "방 이름 없음" : _roomName;
            
            previewSummaryText.text = $"<color=yellow>{gameTypeName}</color> · {_selectedPlayerCount}인 · {privacyText}\n" +
                                     $"방 이름: {nameText}";
        }

        if (estimatedWaitTimeText != null)
        {
            // Estimate wait time based on game type and player count
            int waitTimeSeconds = CalculateEstimatedWaitTime();
            estimatedWaitTimeText.text = $"예상 대기시간: {waitTimeSeconds}초";
        }
    }

    private void UpdateEnergyDisplay()
    {
        if (energyCostPanel == null) return;

        int energyCost = GetEnergyCost(_selectedGameType);
        int currentEnergy = _userDataManager?.CurrentUser?.CurrentEnergy ?? 0;
        bool hasEnoughEnergy = currentEnergy >= energyCost;

        if (energyCostText != null)
            energyCostText.text = $"필요 에너지: {energyCost}";

        if (energyWarningText != null)
        {
            if (!hasEnoughEnergy)
            {
                energyWarningText.text = $"에너지 부족! (현재: {currentEnergy})";
                energyWarningText.color = Color.red;
                energyWarningText.gameObject.SetActive(true);
            }
            else
            {
                energyWarningText.gameObject.SetActive(false);
            }
        }

        if (energyIcon != null)
        {
            energyIcon.color = hasEnoughEnergy ? Color.white : Color.red;
        }
    }

    private void UpdateRoomNameDisplay()
    {
        if (roomNameCharCountText != null)
        {
            int currentLength = _roomName.Length;
            roomNameCharCountText.text = $"{currentLength}/{MAX_ROOM_NAME_LENGTH}";
            
            // Change color based on length
            if (currentLength >= MAX_ROOM_NAME_LENGTH - 5)
                roomNameCharCountText.color = Color.yellow;
            else if (currentLength >= MAX_ROOM_NAME_LENGTH)
                roomNameCharCountText.color = Color.red;
            else
                roomNameCharCountText.color = Color.white;
        }
    }

    private void UpdateCreateButtonState()
    {
        bool canCreate = CanCreateRoom();
        
        if (createRoomButton != null)
        {
            createRoomButton.interactable = canCreate && !_isCreating;
            
            // Update button text
            var buttonText = createRoomButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                if (_isCreating)
                    buttonText.text = "생성 중...";
                else if (!canCreate)
                    buttonText.text = "생성 불가";
                else
                    buttonText.text = "방 생성";
            }
        }
    }

    private void UpdateModalTitle()
    {
        if (modalTitleText != null)
        {
            string gameTypeName = GetGameTypeName(_selectedGameType);
            modalTitleText.text = $"{gameTypeName} 방 생성";
        }
    }
    #endregion

    #region Room Creation Logic
    private void RequestCreateRoom()
    {
        if (!CanCreateRoom()) return;

        SetCreatingState(true);
        OnRoomCreationRequested?.Invoke(_selectedPlayerCount);
    }

    private bool CanCreateRoom()
    {
        // Check energy requirement
        int energyCost = GetEnergyCost(_selectedGameType);
        int currentEnergy = _userDataManager?.CurrentUser?.CurrentEnergy ?? 0;
        
        if (currentEnergy < energyCost)
            return false;

        // Check room name if provided
        if (!string.IsNullOrEmpty(_roomName) && _roomName.Length < MIN_ROOM_NAME_LENGTH)
            return false;

        // Add other validation rules as needed
        return true;
    }

    private void SetCreatingState(bool creating)
    {
        _isCreating = creating;
        
        if (loadingIndicator != null)
            loadingIndicator.SetActive(creating);
        
        UpdateCreateButtonState();
        
        // Disable other controls while creating
        SetControlsEnabled(!creating);
    }

    private void SetControlsEnabled(bool enabled)
    {
        if (playerCountSlider != null)
            playerCountSlider.interactable = enabled;

        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            var button = playerCountButtons[i]?.GetComponent<Button>();
            if (button != null)
                button.interactable = enabled;
        }

        if (privateRoomToggle != null)
            privateRoomToggle.interactable = enabled;

        if (roomNameInputField != null)
            roomNameInputField.interactable = enabled;

        if (gameTypeDropdown != null)
            gameTypeDropdown.interactable = enabled;

        if (cancelButton != null)
            cancelButton.interactable = enabled;
    }
    #endregion

    #region Utility Methods
    private int GetEnergyCost(int gameType)
    {
        // Return energy cost based on game type
        switch (gameType)
        {
            case 0: return 1; // Classic
            case 1: return 2; // Speed
            case 2: return 3; // Challenge
            case 3: return 2; // Ranked
            default: return 1;
        }
    }

    private string GetGameTypeName(int gameType)
    {
        switch (gameType)
        {
            case 0: return "클래식";
            case 1: return "스피드";
            case 2: return "챌린지";
            case 3: return "랭크";
            default: return "클래식";
        }
    }

    private int CalculateEstimatedWaitTime()
    {
        // Simple estimation based on game type and player count
        int baseTime = 30;
        int gameTypeMultiplier = _selectedGameType + 1;
        int playerCountMultiplier = (_selectedPlayerCount - 1);
        
        return baseTime + (gameTypeMultiplier * 15) + (playerCountMultiplier * 10);
    }

    private void NotifySettingsChanged()
    {
        var settings = new RoomCreationSettings
        {
            PlayerCount = _selectedPlayerCount,
            IsPrivate = _isPrivateRoom,
            RoomName = _roomName,
            GameType = _selectedGameType,
            EnergyCost = GetEnergyCost(_selectedGameType)
        };

        OnSettingsChanged?.Invoke(settings);
    }
    #endregion

    #region Public API
    /// <summary>
    /// 기본값으로 리셋
    /// </summary>
    public void ResetToDefaults()
    {
        _selectedPlayerCount = 4;
        _isPrivateRoom = false;
        _roomName = "";
        _selectedGameType = 0;
        _isCreating = false;

        if (playerCountSlider != null)
            playerCountSlider.value = _selectedPlayerCount;

        if (privateRoomToggle != null)
            privateRoomToggle.isOn = _isPrivateRoom;

        if (roomNameInputField != null)
            roomNameInputField.text = _roomName;

        if (gameTypeDropdown != null)
            gameTypeDropdown.value = _selectedGameType;

        UpdateUIDisplay();
        UpdateEnergyDisplay();
        SetCreatingState(false);
    }

    /// <summary>
    /// 방 생성 결과 처리
    /// </summary>
    public void HandleCreationResult(bool success, string message)
    {
        SetCreatingState(false);
        
        if (success)
        {
            HideModal();
        }
        else
        {
            // Show error message
            Debug.LogError($"[RoomCreateUI] Room creation failed: {message}");
            // Could show error UI here
        }
    }

    /// <summary>
    /// 특정 플레이어 수로 설정
    /// </summary>
    public void SetPlayerCount(int playerCount)
    {
        SetPlayerCount(playerCount);
    }

    /// <summary>
    /// 현재 설정 반환
    /// </summary>
    public RoomCreationSettings GetCurrentSettings()
    {
        return new RoomCreationSettings
        {
            PlayerCount = _selectedPlayerCount,
            IsPrivate = _isPrivateRoom,
            RoomName = _roomName,
            GameType = _selectedGameType,
            EnergyCost = GetEnergyCost(_selectedGameType)
        };
    }
    #endregion

    #region Component Validation and Cleanup
    private void ValidateComponents()
    {
        if (modalPanel == null)
            Debug.LogError("[RoomCreateUI] Modal Panel is not assigned!");

        if (playerCountSlider == null)
            Debug.LogWarning("[RoomCreateUI] Player Count Slider is not assigned");

        if (createRoomButton == null)
            Debug.LogWarning("[RoomCreateUI] Create Room Button is not assigned");

        if (cancelButton == null)
            Debug.LogWarning("[RoomCreateUI] Cancel Button is not assigned");
    }

    private void CleanupEventHandlers()
    {
        if (playerCountSlider != null)
            playerCountSlider.onValueChanged.RemoveListener(OnPlayerCountSliderChanged);

        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            var button = playerCountButtons[i]?.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        if (privateRoomToggle != null)
            privateRoomToggle.onValueChanged.RemoveListener(OnPrivateRoomToggleChanged);

        if (roomNameInputField != null)
            roomNameInputField.onValueChanged.RemoveListener(OnRoomNameChanged);

        if (gameTypeDropdown != null)
            gameTypeDropdown.onValueChanged.RemoveListener(OnGameTypeChanged);

        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnCreateRoomButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (backgroundButton != null)
            backgroundButton.onClick.RemoveListener(OnCancelButtonClicked);

        // Clear events
        OnRoomCreationRequested = null;
        OnCreateRoomCancelled = null;
        OnSettingsChanged = null;
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 방 생성 설정 데이터
/// </summary>
[Serializable]
public class RoomCreationSettings
{
    public int PlayerCount;
    public bool IsPrivate;
    public string RoomName;
    public int GameType;
    public int EnergyCost;

    public string GetGameTypeName()
    {
        switch (GameType)
        {
            case 0: return "클래식";
            case 1: return "스피드";
            case 2: return "챌린지";
            case 3: return "랭크";
            default: return "클래식";
        }
    }
}
#endregion