using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 닉네임 설정 화면 관리자
/// 실시간 중복 검사, 유효성 검증, 서버 연동을 포함한 닉네임 설정 시스템
/// </summary>
public class NicknameSetupScreen : MonoBehaviour
{
    #region UI References
    [Header("UI References")]
    [SerializeField] private InputField nicknameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Text feedbackText;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Image validationIcon;
    
    [Header("Validation Icons")]
    [SerializeField] private Sprite validIcon;
    [SerializeField] private Sprite invalidIcon;
    [SerializeField] private Sprite loadingSprite;
    
    [Header("Settings")]
    [SerializeField] private bool useKoreanLocalization = true;
    [SerializeField] private float validationDelay = 0.5f; // 입력 후 검증 지연 시간
    [SerializeField] private int minNicknameLength = 2;
    [SerializeField] private int maxNicknameLength = 12;
    #endregion

    #region Private Fields
    private ValidationState _currentValidationState = ValidationState.None;
    private Coroutine _validationCoroutine;
    private Coroutine _duplicateCheckCoroutine;
    private string _lastValidatedNickname = "";
    private bool _isValidating = false;
    
    // 서버 설정
    private const string SERVER_BASE_URL = "https://api.unitydice.com/v1";
    
    // 로컬라이제이션 텍스트
    private readonly string[] _titleTexts = ["Choose Your Nickname", "닉네임을 설정해주세요"];
    private readonly string[] _descriptionTexts = 
    [
        "Enter a unique nickname that other players will see",
        "다른 플레이어에게 보여질 고유한 닉네임을 입력해주세요"
    ];
    private readonly string[] _confirmButtonTexts = ["Confirm", "확인"];
    private readonly string[] _skipButtonTexts = ["Skip", "건너뛰기"];
    private readonly string[] _placeholderTexts = ["Enter nickname...", "닉네임 입력..."];
    
    // 피드백 메시지
    private readonly string[][] _feedbackMessages = 
    [
        // English
        [
            "Enter your nickname",
            "Nickname is too short",
            "Nickname is too long", 
            "Invalid characters",
            "Checking availability...",
            "Nickname is available",
            "Nickname already taken",
            "Connection error",
            "Nickname saved successfully!"
        ],
        // Korean
        [
            "닉네임을 입력해주세요",
            "닉네임이 너무 짧습니다",
            "닉네임이 너무 깁니다",
            "사용할 수 없는 문자입니다",
            "중복 확인 중...",
            "사용 가능한 닉네임입니다",
            "이미 사용 중인 닉네임입니다",
            "연결 오류가 발생했습니다",
            "닉네임이 저장되었습니다!"
        ]
    ];
    #endregion

    #region Enums
    private enum ValidationState
    {
        None,           // 초기 상태
        TooShort,       // 너무 짧음
        TooLong,        // 너무 김
        InvalidChars,   // 잘못된 문자
        Checking,       // 중복 검사 중
        Valid,          // 유효함
        Duplicate,      // 중복됨
        NetworkError    // 네트워크 오류
    }
    
    private enum FeedbackType
    {
        Initial = 0,
        TooShort = 1,
        TooLong = 2,
        InvalidChars = 3,
        Checking = 4,
        Valid = 5,
        Duplicate = 6,
        NetworkError = 7,
        Success = 8
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        SetupUI();
    }

    private void Start()
    {
        ApplyLocalization();
        SetupInputValidation();
        ShowFeedback(FeedbackType.Initial);
        UpdateConfirmButton();
    }

    private void OnDestroy()
    {
        StopAllValidationCoroutines();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// UI 컴포넌트 검증
    /// </summary>
    private void ValidateComponents()
    {
        if (nicknameInputField == null)
            Debug.LogError("[NicknameSetup] Nickname input field is not assigned!");
            
        if (confirmButton == null)
            Debug.LogError("[NicknameSetup] Confirm button is not assigned!");
            
        if (feedbackText == null)
            Debug.LogWarning("[NicknameSetup] Feedback text is not assigned");
    }

    /// <summary>
    /// UI 초기 설정
    /// </summary>
    private void SetupUI()
    {
        // 버튼 이벤트 설정
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        skipButton?.onClick.AddListener(OnSkipClicked);
        
        // 로딩 인디케이터 초기 상태
        loadingIndicator?.SetActive(false);
        
        // 검증 아이콘 초기 상태
        if (validationIcon != null)
            validationIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 입력 검증 설정
    /// </summary>
    private void SetupInputValidation()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.onValueChanged.AddListener(OnNicknameInputChanged);
            nicknameInputField.characterLimit = maxNicknameLength;
            
            // 입력 필터 설정 (한글, 영문, 숫자만 허용)
            nicknameInputField.inputType = InputField.InputType.Standard;
            nicknameInputField.keyboardType = TouchScreenKeyboardType.Default;
        }
    }

    /// <summary>
    /// 로컬라이제이션 적용
    /// </summary>
    private void ApplyLocalization()
    {
        int langIndex = useKoreanLocalization ? 1 : 0;
        
        if (titleText != null)
            titleText.text = _titleTexts[langIndex];
            
        if (descriptionText != null)
            descriptionText.text = _descriptionTexts[langIndex];
            
        if (confirmButton != null)
        {
            var buttonText = confirmButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = _confirmButtonTexts[langIndex];
        }
        
        if (skipButton != null)
        {
            var buttonText = skipButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = _skipButtonTexts[langIndex];
        }
        
        if (nicknameInputField != null)
        {
            var placeholder = nicknameInputField.placeholder as Text;
            if (placeholder != null)
                placeholder.text = _placeholderTexts[langIndex];
        }
    }
    #endregion

    #region Input Validation
    /// <summary>
    /// 닉네임 입력 변경 처리
    /// </summary>
    private void OnNicknameInputChanged(string nickname)
    {
        // 기존 검증 중단
        StopValidationCoroutine();
        
        // 즉시 기본 검증 수행
        ValidationState immediateState = ValidateNicknameFormat(nickname);
        
        if (immediateState != ValidationState.None)
        {
            SetValidationState(immediateState);
            return;
        }
        
        // 지연된 중복 검사 시작
        _validationCoroutine = StartCoroutine(DelayedValidation(nickname));
    }

    /// <summary>
    /// 지연된 검증 코루틴
    /// </summary>
    private IEnumerator DelayedValidation(string nickname)
    {
        yield return new WaitForSeconds(validationDelay);
        
        if (nickname == nicknameInputField.text) // 입력이 변경되지 않았다면
        {
            yield return StartCoroutine(CheckNicknameDuplication(nickname));
        }
    }

    /// <summary>
    /// 닉네임 형식 검증
    /// </summary>
    private ValidationState ValidateNicknameFormat(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return ValidationState.None;
            
        if (nickname.Length < minNicknameLength)
            return ValidationState.TooShort;
            
        if (nickname.Length > maxNicknameLength)
            return ValidationState.TooLong;
            
        // 허용된 문자 검사 (한글, 영문, 숫자, 일부 특수문자)
        if (!IsValidNicknameCharacters(nickname))
            return ValidationState.InvalidChars;
            
        return ValidationState.None; // 형식상 문제없음
    }

    /// <summary>
    /// 닉네임 문자 유효성 검사
    /// </summary>
    private bool IsValidNicknameCharacters(string nickname)
    {
        // 한글, 영문, 숫자, 언더스코어, 하이픈만 허용
        string pattern = @"^[가-힣a-zA-Z0-9_-]+$";
        return Regex.IsMatch(nickname, pattern);
    }

    /// <summary>
    /// 닉네임 중복 검사
    /// </summary>
    private IEnumerator CheckNicknameDuplication(string nickname)
    {
        if (nickname == _lastValidatedNickname)
            yield break; // 이미 검증된 닉네임
            
        SetValidationState(ValidationState.Checking);
        
        // 서버 API 호출
        UnityWebRequest request = UnityWebRequest.Get($"{SERVER_BASE_URL}/users/check-nickname?nickname={UnityWebRequest.EscapeURL(nickname)}");
        
        yield return request.SendWebRequest();
        
        if (nickname != nicknameInputField.text)
            yield break; // 입력이 변경됨
            
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonUtility.FromJson<NicknameCheckResponse>(request.downloadHandler.text);
                
                if (response.available)
                {
                    SetValidationState(ValidationState.Valid);
                    _lastValidatedNickname = nickname;
                }
                else
                {
                    SetValidationState(ValidationState.Duplicate);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NicknameSetup] Failed to parse server response: {e.Message}");
                SetValidationState(ValidationState.NetworkError);
            }
        }
        else
        {
            Debug.LogError($"[NicknameSetup] Network error: {request.error}");
            SetValidationState(ValidationState.NetworkError);
        }
    }

    /// <summary>
    /// 검증 상태 설정
    /// </summary>
    private void SetValidationState(ValidationState state)
    {
        _currentValidationState = state;
        UpdateUI();
        UpdateConfirmButton();
    }
    #endregion

    #region UI Updates
    /// <summary>
    /// UI 업데이트
    /// </summary>
    private void UpdateUI()
    {
        FeedbackType feedbackType = GetFeedbackType(_currentValidationState);
        ShowFeedback(feedbackType);
        UpdateValidationIcon();
        UpdateLoadingIndicator();
    }

    /// <summary>
    /// 피드백 표시
    /// </summary>
    private void ShowFeedback(FeedbackType feedbackType)
    {
        if (feedbackText == null) return;
        
        int langIndex = useKoreanLocalization ? 1 : 0;
        string message = _feedbackMessages[langIndex][(int)feedbackType];
        
        feedbackText.text = message;
        feedbackText.color = GetFeedbackColor(feedbackType);
    }

    /// <summary>
    /// 피드백 색상 가져오기
    /// </summary>
    private Color GetFeedbackColor(FeedbackType feedbackType)
    {
        return feedbackType switch
        {
            FeedbackType.Valid or FeedbackType.Success => Color.green,
            FeedbackType.TooShort or FeedbackType.TooLong or FeedbackType.InvalidChars or FeedbackType.Duplicate => Color.red,
            FeedbackType.NetworkError => new Color(1f, 0.5f, 0f), // Orange
            _ => Color.gray
        };
    }

    /// <summary>
    /// 검증 아이콘 업데이트
    /// </summary>
    private void UpdateValidationIcon()
    {
        if (validationIcon == null) return;
        
        switch (_currentValidationState)
        {
            case ValidationState.Valid:
                validationIcon.sprite = validIcon;
                validationIcon.gameObject.SetActive(true);
                break;
                
            case ValidationState.TooShort:
            case ValidationState.TooLong:
            case ValidationState.InvalidChars:
            case ValidationState.Duplicate:
            case ValidationState.NetworkError:
                validationIcon.sprite = invalidIcon;
                validationIcon.gameObject.SetActive(true);
                break;
                
            case ValidationState.Checking:
                validationIcon.sprite = loadingSprite;
                validationIcon.gameObject.SetActive(true);
                break;
                
            default:
                validationIcon.gameObject.SetActive(false);
                break;
        }
    }

    /// <summary>
    /// 로딩 인디케이터 업데이트
    /// </summary>
    private void UpdateLoadingIndicator()
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(_currentValidationState == ValidationState.Checking);
        }
    }

    /// <summary>
    /// 확인 버튼 상태 업데이트
    /// </summary>
    private void UpdateConfirmButton()
    {
        if (confirmButton != null)
        {
            bool isValid = _currentValidationState == ValidationState.Valid;
            confirmButton.interactable = isValid;
            
            // 시각적 피드백
            var colors = confirmButton.colors;
            colors.normalColor = isValid ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            confirmButton.colors = colors;
        }
    }
    #endregion

    #region Button Events
    /// <summary>
    /// 확인 버튼 클릭 처리
    /// </summary>
    private void OnConfirmClicked()
    {
        if (_currentValidationState != ValidationState.Valid)
        {
            Debug.LogWarning("[NicknameSetup] Confirm clicked with invalid nickname");
            return;
        }
        
        string nickname = nicknameInputField.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("[NicknameSetup] Empty nickname on confirm");
            return;
        }
        
        StartCoroutine(SaveNicknameToServer(nickname));
    }

    /// <summary>
    /// 건너뛰기 버튼 클릭 처리
    /// </summary>
    private void OnSkipClicked()
    {
        Debug.Log("[NicknameSetup] User skipped nickname setup");
        
        // 기본 닉네임 설정 또는 나중에 설정하도록 표시
        if (UserDataManager.Instance?.CurrentUser != null)
        {
            var userData = UserDataManager.Instance.CurrentUser;
            userData.DisplayName = GenerateDefaultNickname();
            UserDataManager.Instance.UpdateUserData(userData);
        }
        
        CompleteNicknameSetup(false);
    }
    #endregion

    #region Server Integration
    /// <summary>
    /// 서버에 닉네임 저장
    /// </summary>
    private IEnumerator SaveNicknameToServer(string nickname)
    {
        confirmButton.interactable = false;
        ShowFeedback(FeedbackType.Checking);
        
        if (UserDataManager.Instance?.CurrentUser == null)
        {
            Debug.LogError("[NicknameSetup] No current user to save nickname");
            ShowFeedback(FeedbackType.NetworkError);
            confirmButton.interactable = true;
            yield break;
        }
        
        var userData = UserDataManager.Instance.CurrentUser;
        userData.DisplayName = nickname;
        
        // 로컬에 먼저 저장
        UserDataManager.Instance.UpdateUserData(userData);
        
        // 서버에 저장 시도
        var saveRequest = new NicknameSaveRequest { nickname = nickname, userId = userData.UserId };
        string jsonData = JsonUtility.ToJson(saveRequest);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        
        UnityWebRequest request = new UnityWebRequest($"{SERVER_BASE_URL}/users/{userData.UserId}/nickname", "PUT");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            ShowFeedback(FeedbackType.Success);
            Debug.Log($"[NicknameSetup] Nickname saved successfully: {nickname}");
            
            yield return new WaitForSeconds(1.5f); // 성공 메시지 표시
            CompleteNicknameSetup(true);
        }
        else
        {
            Debug.LogError($"[NicknameSetup] Failed to save nickname: {request.error}");
            ShowFeedback(FeedbackType.NetworkError);
            confirmButton.interactable = true;
        }
    }

    /// <summary>
    /// 기본 닉네임 생성
    /// </summary>
    private string GenerateDefaultNickname()
    {
        if (UserDataManager.Instance?.CurrentUser != null)
        {
            string userId = UserDataManager.Instance.CurrentUser.UserId;
            // 사용자 ID의 마지막 4자리 또는 랜덤 숫자 사용
            if (userId.Length >= 4)
            {
                return "Player" + userId.Substring(userId.Length - 4);
            }
        }
        
        // 랜덤 숫자 생성
        return "Player" + UnityEngine.Random.Range(1000, 9999);
    }
    #endregion

    #region Screen Transition
    /// <summary>
    /// 닉네임 설정 완료
    /// </summary>
    private void CompleteNicknameSetup(bool wasSet)
    {
        Debug.Log($"[NicknameSetup] Setup completed. Nickname was set: {wasSet}");
        
        // TODO: 메인 게임 화면으로 전환
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainGame");
        
        // 현재는 화면 비활성화
        gameObject.SetActive(false);
        
        // 완료 이벤트 발생 (다른 시스템에서 처리할 수 있도록)
        OnNicknameSetupCompleted?.Invoke(wasSet);
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 검증 상태를 피드백 타입으로 변환
    /// </summary>
    private FeedbackType GetFeedbackType(ValidationState state)
    {
        return state switch
        {
            ValidationState.None => FeedbackType.Initial,
            ValidationState.TooShort => FeedbackType.TooShort,
            ValidationState.TooLong => FeedbackType.TooLong,
            ValidationState.InvalidChars => FeedbackType.InvalidChars,
            ValidationState.Checking => FeedbackType.Checking,
            ValidationState.Valid => FeedbackType.Valid,
            ValidationState.Duplicate => FeedbackType.Duplicate,
            ValidationState.NetworkError => FeedbackType.NetworkError,
            _ => FeedbackType.Initial
        };
    }

    /// <summary>
    /// 모든 검증 코루틴 중지
    /// </summary>
    private void StopAllValidationCoroutines()
    {
        StopValidationCoroutine();
        StopDuplicateCheckCoroutine();
    }

    /// <summary>
    /// 검증 코루틴 중지
    /// </summary>
    private void StopValidationCoroutine()
    {
        if (_validationCoroutine != null)
        {
            StopCoroutine(_validationCoroutine);
            _validationCoroutine = null;
        }
    }

    /// <summary>
    /// 중복 검사 코루틴 중지
    /// </summary>
    private void StopDuplicateCheckCoroutine()
    {
        if (_duplicateCheckCoroutine != null)
        {
            StopCoroutine(_duplicateCheckCoroutine);
            _duplicateCheckCoroutine = null;
        }
    }
    #endregion

    #region Public Events
    /// <summary>
    /// 닉네임 설정 완료 이벤트
    /// </summary>
    public static event System.Action<bool> OnNicknameSetupCompleted;
    #endregion

    #region Public Methods
    /// <summary>
    /// 닉네임 설정 화면 표시
    /// </summary>
    public void ShowNicknameSetup()
    {
        gameObject.SetActive(true);
        nicknameInputField?.Select();
        
        // 현재 사용자의 닉네임이 있다면 표시
        if (UserDataManager.Instance?.CurrentUser != null)
        {
            string currentNickname = UserDataManager.Instance.CurrentUser.DisplayName;
            if (!string.IsNullOrEmpty(currentNickname) && nicknameInputField != null)
            {
                nicknameInputField.text = currentNickname;
            }
        }
    }

    /// <summary>
    /// 언어 설정 변경
    /// </summary>
    public void SetLanguage(bool useKorean)
    {
        useKoreanLocalization = useKorean;
        ApplyLocalization();
        UpdateUI(); // 피드백 메시지도 새 언어로 업데이트
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 닉네임 중복 검사 응답
/// </summary>
[System.Serializable]
public class NicknameCheckResponse
{
    public bool available;
    public string message;
}

/// <summary>
/// 닉네임 저장 요청
/// </summary>
[System.Serializable]
public class NicknameSaveRequest
{
    public string nickname;
    public string userId;
}
#endregion