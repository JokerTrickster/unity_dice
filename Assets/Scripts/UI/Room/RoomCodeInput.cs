using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 코드 입력 UI 컴포넌트
/// 4자리 숫자 방 코드 입력, 유효성 검증, 자동 포맷팅을 담당합니다.
/// 실시간 유효성 검사 및 사용자 친화적 입력 경험을 제공합니다.
/// </summary>
public class RoomCodeInput : MonoBehaviour
{
    #region UI References
    [Header("Input Components")]
    [SerializeField] private InputField roomCodeInputField;
    [SerializeField] private Text inputPlaceholder;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button pasteButton;
    
    [Header("Validation Feedback")]
    [SerializeField] private Text validationMessageText;
    [SerializeField] private Image inputBackgroundImage;
    [SerializeField] private GameObject validationIcon;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Input Visual States")]
    [SerializeField] private Color normalBorderColor = Color.gray;
    [SerializeField] private Color validBorderColor = Color.green;
    [SerializeField] private Color invalidBorderColor = Color.red;
    [SerializeField] private Color focusedBorderColor = Color.blue;
    
    [Header("Animation Settings")]
    [SerializeField] private float shakeIntensity = 10f;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private int shakeVibrato = 10;
    
    [Header("Input Settings")]
    [SerializeField] private bool enableAutoFormat = true;
    [SerializeField] private bool enablePasteFromClipboard = true;
    [SerializeField] private bool enableRealTimeValidation = true;
    [SerializeField] private float validationDelay = 0.5f;
    #endregion

    #region Private Fields
    private string _currentInput = "";
    private bool _isValid = false;
    private bool _isJoining = false;
    
    // Validation
    private static readonly Regex ROOM_CODE_REGEX = new Regex(@"^\d{4}$");
    private Coroutine _validationCoroutine;
    private Coroutine _shakeCoroutine;
    
    // Visual components
    private Outline _inputOutline;
    private RectTransform _inputRectTransform;
    private Vector3 _originalInputPosition;
    
    // Input state
    private bool _isFocused = false;
    private string _lastValidInput = "";
    #endregion

    #region Events
    public event Action<string> OnRoomJoinRequested;
    public event Action OnJoinRoomCancelled;
    public event Action<string> OnInputChanged;
    public event Action<bool> OnValidationChanged;
    #endregion

    #region Properties
    public string CurrentInput => _currentInput;
    public bool IsValid => _isValid;
    public bool IsJoining => _isJoining;
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
        InitializeInputField();
        SetInitialState();
    }

    private void OnDestroy()
    {
        CleanupEventHandlers();
        StopAllCoroutines();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Get or add required components
        if (roomCodeInputField != null)
        {
            _inputRectTransform = roomCodeInputField.GetComponent<RectTransform>();
            _originalInputPosition = _inputRectTransform.localPosition;
            
            _inputOutline = roomCodeInputField.GetComponent<Outline>();
            if (_inputOutline == null)
            {
                _inputOutline = roomCodeInputField.gameObject.AddComponent<Outline>();
                _inputOutline.effectColor = normalBorderColor;
                _inputOutline.effectDistance = new Vector2(1, 1);
            }
        }
    }

    private void SetupEventHandlers()
    {
        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.AddListener(OnInputValueChanged);
            roomCodeInputField.onEndEdit.AddListener(OnInputEndEdit);
            
            // Focus events (using custom focus detection)
            var selectable = roomCodeInputField.GetComponent<Selectable>();
            if (selectable != null)
            {
                // Add event triggers for focus
                var eventTrigger = roomCodeInputField.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (eventTrigger == null)
                    eventTrigger = roomCodeInputField.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                
                // Focus in
                var focusInEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                focusInEntry.eventID = UnityEngine.EventSystems.EventTriggerType.Select;
                focusInEntry.callback.AddListener((data) => OnInputFocused(true));
                eventTrigger.triggers.Add(focusInEntry);
                
                // Focus out
                var focusOutEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                focusOutEntry.eventID = UnityEngine.EventSystems.EventTriggerType.Deselect;
                focusOutEntry.callback.AddListener((data) => OnInputFocused(false));
                eventTrigger.triggers.Add(focusOutEntry);
            }
        }

        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (pasteButton != null)
            pasteButton.onClick.AddListener(OnPasteButtonClicked);
    }

    private void InitializeInputField()
    {
        if (roomCodeInputField != null)
        {
            roomCodeInputField.characterLimit = 4;
            roomCodeInputField.inputType = InputField.InputType.Standard;
            roomCodeInputField.keyboardType = TouchScreenKeyboardType.NumberPad;
            roomCodeInputField.contentType = InputField.ContentType.IntegerNumber;
            
            if (inputPlaceholder != null)
                inputPlaceholder.text = "4자리 방 코드 입력";
        }
    }

    private void SetInitialState()
    {
        UpdateVisualState(InputVisualState.Normal);
        UpdateButtonStates();
        ClearValidationMessage();
        
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);
            
        if (pasteButton != null)
            pasteButton.gameObject.SetActive(enablePasteFromClipboard);
    }
    #endregion

    #region Input Handling
    private void OnInputValueChanged(string value)
    {
        // Auto-format input (remove non-digits, limit to 4 chars)
        if (enableAutoFormat)
        {
            value = Regex.Replace(value, @"[^\d]", ""); // Remove non-digits
            if (value.Length > 4)
                value = value.Substring(0, 4);
                
            if (value != roomCodeInputField.text)
            {
                roomCodeInputField.text = value;
                return; // Prevent infinite recursion
            }
        }

        _currentInput = value;
        OnInputChanged?.Invoke(value);

        // Real-time validation
        if (enableRealTimeValidation)
        {
            if (_validationCoroutine != null)
                StopCoroutine(_validationCoroutine);
            _validationCoroutine = StartCoroutine(ValidateInputWithDelay());
        }
        
        UpdateButtonStates();
    }

    private void OnInputEndEdit(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            // Immediate validation on end edit
            ValidateInput(value);
            
            // Auto-join if valid and Enter was pressed
            if (_isValid && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                RequestJoinRoom();
            }
        }
    }

    private void OnInputFocused(bool focused)
    {
        _isFocused = focused;
        
        if (focused)
        {
            UpdateVisualState(InputVisualState.Focused);
        }
        else if (_isValid)
        {
            UpdateVisualState(InputVisualState.Valid);
        }
        else if (!string.IsNullOrEmpty(_currentInput))
        {
            UpdateVisualState(InputVisualState.Invalid);
        }
        else
        {
            UpdateVisualState(InputVisualState.Normal);
        }
    }
    #endregion

    #region Validation
    private IEnumerator ValidateInputWithDelay()
    {
        yield return new WaitForSeconds(validationDelay);
        ValidateInput(_currentInput);
        _validationCoroutine = null;
    }

    private void ValidateInput(string input)
    {
        bool wasValid = _isValid;
        _isValid = IsValidRoomCode(input);
        
        if (wasValid != _isValid)
        {
            OnValidationChanged?.Invoke(_isValid);
        }
        
        UpdateValidationFeedback(input);
        UpdateVisualState(_isFocused ? InputVisualState.Focused : 
                         (_isValid ? InputVisualState.Valid : 
                         (!string.IsNullOrEmpty(input) ? InputVisualState.Invalid : InputVisualState.Normal)));
        
        if (_isValid)
        {
            _lastValidInput = input;
        }
    }

    private bool IsValidRoomCode(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        
        // Check format (4 digits)
        if (!ROOM_CODE_REGEX.IsMatch(input)) return false;
        
        // Additional validation rules can be added here
        // For example: check against blacklisted codes, server availability, etc.
        
        return true;
    }

    private void UpdateValidationFeedback(string input)
    {
        if (validationMessageText == null) return;
        
        if (string.IsNullOrEmpty(input))
        {
            ClearValidationMessage();
        }
        else if (_isValid)
        {
            ShowValidationMessage("유효한 방 코드입니다", MessageType.Success);
        }
        else
        {
            if (input.Length < 4)
            {
                ShowValidationMessage("4자리 숫자를 입력하세요", MessageType.Info);
            }
            else if (!ROOM_CODE_REGEX.IsMatch(input))
            {
                ShowValidationMessage("숫자만 입력 가능합니다", MessageType.Error);
            }
            else
            {
                ShowValidationMessage("올바르지 않은 방 코드입니다", MessageType.Error);
            }
        }
    }
    #endregion

    #region Button Handlers
    private void OnJoinButtonClicked()
    {
        if (!_isValid || _isJoining) return;
        
        RequestJoinRoom();
    }

    private void OnCancelButtonClicked()
    {
        if (_isJoining) return;
        
        OnJoinRoomCancelled?.Invoke();
    }

    private void OnPasteButtonClicked()
    {
        if (!enablePasteFromClipboard || _isJoining) return;
        
        try
        {
            string clipboardText = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clipboardText))
            {
                // Extract numbers from clipboard (in case it contains other characters)
                string numbersOnly = Regex.Replace(clipboardText, @"[^\d]", "");
                if (numbersOnly.Length >= 4)
                {
                    string roomCode = numbersOnly.Substring(0, 4);
                    SetRoomCode(roomCode);
                    ShowTemporaryMessage("클립보드에서 방 코드를 가져왔습니다");
                }
                else
                {
                    ShowValidationMessage("클립보드에 유효한 방 코드가 없습니다", MessageType.Error);
                    StartShakeAnimation();
                }
            }
            else
            {
                ShowValidationMessage("클립보드가 비어있습니다", MessageType.Error);
                StartShakeAnimation();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomCodeInput] Failed to paste from clipboard: {e.Message}");
            ShowValidationMessage("클립보드 접근 실패", MessageType.Error);
        }
    }

    private void RequestJoinRoom()
    {
        if (!_isValid || _isJoining) return;
        
        SetJoiningState(true);
        OnRoomJoinRequested?.Invoke(_currentInput);
    }
    #endregion

    #region Public API
    /// <summary>
    /// 방 코드 설정
    /// </summary>
    public void SetRoomCode(string roomCode)
    {
        if (roomCodeInputField != null)
        {
            roomCodeInputField.text = roomCode;
            // OnInputValueChanged will be called automatically
        }
    }

    /// <summary>
    /// 입력 필드 클리어
    /// </summary>
    public void ClearInput()
    {
        if (roomCodeInputField != null)
        {
            roomCodeInputField.text = "";
            _currentInput = "";
            _isValid = false;
            UpdateButtonStates();
            ClearValidationMessage();
            UpdateVisualState(InputVisualState.Normal);
        }
    }

    /// <summary>
    /// 참여 상태 설정 (로딩 상태)
    /// </summary>
    public void SetJoiningState(bool joining)
    {
        _isJoining = joining;
        
        if (loadingIndicator != null)
            loadingIndicator.SetActive(joining);
        
        UpdateButtonStates();
        
        if (roomCodeInputField != null)
            roomCodeInputField.interactable = !joining;
    }

    /// <summary>
    /// 포커스 설정
    /// </summary>
    public void FocusInput()
    {
        if (roomCodeInputField != null && !_isJoining)
        {
            roomCodeInputField.Select();
            roomCodeInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// 에러 표시 및 진동 효과
    /// </summary>
    public void ShowErrorWithShake(string errorMessage)
    {
        ShowValidationMessage(errorMessage, MessageType.Error);
        StartShakeAnimation();
        UpdateVisualState(InputVisualState.Invalid);
    }

    /// <summary>
    /// 성공 메시지 표시
    /// </summary>
    public void ShowSuccess(string message)
    {
        ShowValidationMessage(message, MessageType.Success);
        UpdateVisualState(InputVisualState.Valid);
    }
    #endregion

    #region Visual Updates
    private void UpdateVisualState(InputVisualState state)
    {
        Color borderColor;
        
        switch (state)
        {
            case InputVisualState.Valid:
                borderColor = validBorderColor;
                break;
            case InputVisualState.Invalid:
                borderColor = invalidBorderColor;
                break;
            case InputVisualState.Focused:
                borderColor = focusedBorderColor;
                break;
            default:
                borderColor = normalBorderColor;
                break;
        }
        
        if (_inputOutline != null)
            _inputOutline.effectColor = borderColor;
        
        if (inputBackgroundImage != null)
        {
            Color bgColor = inputBackgroundImage.color;
            bgColor.a = state == InputVisualState.Focused ? 0.1f : 0.05f;
            inputBackgroundImage.color = bgColor;
        }
        
        if (validationIcon != null)
        {
            validationIcon.SetActive(state == InputVisualState.Valid || state == InputVisualState.Invalid);
            // You could change the icon sprite based on state
        }
    }

    private void UpdateButtonStates()
    {
        if (joinButton != null)
            joinButton.interactable = _isValid && !_isJoining;
        
        if (cancelButton != null)
            cancelButton.interactable = !_isJoining;
        
        if (pasteButton != null)
            pasteButton.interactable = !_isJoining && enablePasteFromClipboard;
    }

    private void ShowValidationMessage(string message, MessageType type)
    {
        if (validationMessageText == null) return;
        
        validationMessageText.text = message;
        
        Color messageColor;
        switch (type)
        {
            case MessageType.Success:
                messageColor = Color.green;
                break;
            case MessageType.Error:
                messageColor = Color.red;
                break;
            case MessageType.Warning:
                messageColor = Color.yellow;
                break;
            default:
                messageColor = Color.white;
                break;
        }
        
        validationMessageText.color = messageColor;
        validationMessageText.gameObject.SetActive(true);
    }

    private void ClearValidationMessage()
    {
        if (validationMessageText != null)
        {
            validationMessageText.gameObject.SetActive(false);
        }
    }

    private void ShowTemporaryMessage(string message, float duration = 2f)
    {
        ShowValidationMessage(message, MessageType.Info);
        StartCoroutine(HideMessageAfterDelay(duration));
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearValidationMessage();
    }
    #endregion

    #region Animation Effects
    private void StartShakeAnimation()
    {
        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);
        
        if (_inputRectTransform != null)
            _shakeCoroutine = StartCoroutine(ShakeAnimation());
    }

    private IEnumerator ShakeAnimation()
    {
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            float progress = elapsed / shakeDuration;
            float intensity = shakeIntensity * (1f - progress); // Fade out intensity
            
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-intensity, intensity),
                UnityEngine.Random.Range(-intensity, intensity),
                0f
            );
            
            _inputRectTransform.localPosition = _originalInputPosition + randomOffset;
            
            yield return null;
        }
        
        // Reset to original position
        _inputRectTransform.localPosition = _originalInputPosition;
        _shakeCoroutine = null;
    }
    #endregion

    #region Component Validation and Cleanup
    private void ValidateComponents()
    {
        if (roomCodeInputField == null)
        {
            Debug.LogError("[RoomCodeInput] Room Code Input Field is not assigned!");
        }

        if (joinButton == null)
        {
            Debug.LogWarning("[RoomCodeInput] Join Button is not assigned");
        }

        if (cancelButton == null)
        {
            Debug.LogWarning("[RoomCodeInput] Cancel Button is not assigned");
        }

        if (validationMessageText == null)
        {
            Debug.LogWarning("[RoomCodeInput] Validation Message Text is not assigned");
        }
    }

    private void CleanupEventHandlers()
    {
        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.RemoveListener(OnInputValueChanged);
            roomCodeInputField.onEndEdit.RemoveListener(OnInputEndEdit);
        }

        if (joinButton != null)
            joinButton.onClick.RemoveListener(OnJoinButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (pasteButton != null)
            pasteButton.onClick.RemoveListener(OnPasteButtonClicked);

        // Clear events
        OnRoomJoinRequested = null;
        OnJoinRoomCancelled = null;
        OnInputChanged = null;
        OnValidationChanged = null;
    }
    #endregion
}

#region Enums and Data Classes
/// <summary>
/// 입력 필드 시각적 상태
/// </summary>
public enum InputVisualState
{
    Normal,
    Focused,
    Valid,
    Invalid
}

/// <summary>
/// 메시지 타입 (RoomUI와 동일하게 유지)
/// </summary>
public enum MessageType
{
    Info,
    Warning,
    Error,
    Success
}
#endregion