using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 매칭 섹션 UI 컴포넌트
/// 게임 매칭 기능을 제공하는 UI 섹션입니다.
/// 50% 너비를 차지하는 가장 큰 섹션으로 주요 게임 기능을 담당합니다.
/// </summary>
public class MatchingSectionUI : SectionBase
{
    #region UI References
    [Header("Matching Controls")]
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button rankedMatchButton;
    [SerializeField] private Button customMatchButton;
    [SerializeField] private Button friendMatchButton;
    
    [Header("Match Status")]
    [SerializeField] private Text matchStatusText;
    [SerializeField] private Slider matchProgressSlider;
    [SerializeField] private Text waitTimeText;
    [SerializeField] private Button cancelMatchButton;
    
    [Header("Game Mode Selection")]
    [SerializeField] private Toggle classicModeToggle;
    [SerializeField] private Toggle speedModeToggle;
    [SerializeField] private Toggle challengeModeToggle;
    [SerializeField] private ToggleGroup gameModeToggleGroup;
    
    [Header("Player Info")]
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text averageWaitTimeText;
    [SerializeField] private Text recommendedModeText;
    
    [Header("Visual Elements")]
    [SerializeField] private GameObject matchingAnimationObject;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject onlineStatusIndicator;
    [SerializeField] private GameObject offlineNotification;
    #endregion

    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Matching;
    public override string SectionDisplayName => "매칭";
    #endregion

    #region Private Fields
    private MatchingState _currentMatchingState = MatchingState.Idle;
    private GameMode _selectedGameMode = GameMode.Classic;
    private DateTime _matchStartTime;
    private Coroutine _matchingTimerCoroutine;
    private Coroutine _matchingAnimationCoroutine;
    
    // Matching configuration
    private readonly Dictionary<GameMode, MatchConfig> _matchConfigs = new();
    private int _currentOnlinePlayerCount = 0;
    private TimeSpan _estimatedWaitTime = TimeSpan.Zero;
    
    // Energy integration
    private const int MATCH_ENERGY_COST = 1;
    private EnergySectionUI _energySection;
    #endregion

    #region Section Enums
    public enum MatchingState
    {
        Idle,
        Searching,
        Found,
        Connecting,
        Ready,
        Failed
    }

    public enum GameMode
    {
        Classic,
        Speed,
        Challenge
    }
    #endregion

    #region SectionBase Implementation
    protected override void OnInitialize()
    {
        SetupUIEvents();
        SetupMatchConfigs();
        SetupGameModeToggles();
        ValidateMatchingComponents();
        InitializeMatchingState();
    }

    protected override void OnActivate()
    {
        RefreshMatchingDisplay();
        UpdateOnlineStatus();
        StartPlayerCountUpdates();
    }

    protected override void OnDeactivate()
    {
        StopMatchingAnimations();
        StopPlayerCountUpdates();
        
        // 매칭 중이면 취소
        if (_currentMatchingState == MatchingState.Searching)
        {
            CancelMatching();
        }
    }

    protected override void OnCleanup()
    {
        UnsubscribeFromUIEvents();
        CancelMatching();
        StopAllCoroutines();
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null) return;
        
        UpdateMatchingAvailability(userData);
        UpdateRecommendedMode(userData);
        
        Debug.Log($"[MatchingSectionUI] UI updated for user: {userData.DisplayName}");
    }

    protected override void ValidateComponents()
    {
        // 필수 컴포넌트 검증
        if (quickMatchButton == null)
            ReportError("Quick match button is missing!");
            
        if (rankedMatchButton == null)
            ReportError("Ranked match button is missing!");
            
        if (matchStatusText == null)
            ReportError("Match status text is missing!");
            
        // 경고 레벨 컴포넌트
        if (cancelMatchButton == null)
            Debug.LogWarning("[MatchingSectionUI] Cancel match button is not assigned");
            
        if (gameModeToggleGroup == null)
            Debug.LogWarning("[MatchingSectionUI] Game mode toggle group is not assigned");
    }
    #endregion

    #region UI Event Setup
    private void SetupUIEvents()
    {
        // 매칭 버튼들
        if (quickMatchButton != null)
            quickMatchButton.onClick.AddListener(OnQuickMatchClicked);
            
        if (rankedMatchButton != null)
            rankedMatchButton.onClick.AddListener(OnRankedMatchClicked);
            
        if (customMatchButton != null)
            customMatchButton.onClick.AddListener(OnCustomMatchClicked);
            
        if (friendMatchButton != null)
            friendMatchButton.onClick.AddListener(OnFriendMatchClicked);
            
        if (cancelMatchButton != null)
            cancelMatchButton.onClick.AddListener(OnCancelMatchClicked);
            
        // 게임 모드 토글들
        if (classicModeToggle != null)
            classicModeToggle.onValueChanged.AddListener((isOn) => OnGameModeChanged(GameMode.Classic, isOn));
            
        if (speedModeToggle != null)
            speedModeToggle.onValueChanged.AddListener((isOn) => OnGameModeChanged(GameMode.Speed, isOn));
            
        if (challengeModeToggle != null)
            challengeModeToggle.onValueChanged.AddListener((isOn) => OnGameModeChanged(GameMode.Challenge, isOn));
    }

    private void UnsubscribeFromUIEvents()
    {
        if (quickMatchButton != null)
            quickMatchButton.onClick.RemoveListener(OnQuickMatchClicked);
            
        if (rankedMatchButton != null)
            rankedMatchButton.onClick.RemoveListener(OnRankedMatchClicked);
            
        if (customMatchButton != null)
            customMatchButton.onClick.RemoveListener(OnCustomMatchClicked);
            
        if (friendMatchButton != null)
            friendMatchButton.onClick.RemoveListener(OnFriendMatchClicked);
            
        if (cancelMatchButton != null)
            cancelMatchButton.onClick.RemoveListener(OnCancelMatchClicked);
            
        if (classicModeToggle != null)
            classicModeToggle.onValueChanged.RemoveAllListeners();
            
        if (speedModeToggle != null)
            speedModeToggle.onValueChanged.RemoveAllListeners();
            
        if (challengeModeToggle != null)
            challengeModeToggle.onValueChanged.RemoveAllListeners();
    }
    #endregion

    #region Matching System
    private void SetupMatchConfigs()
    {
        _matchConfigs[GameMode.Classic] = new MatchConfig
        {
            DisplayName = "클래식",
            EnergyCost = 1,
            EstimatedWaitTime = TimeSpan.FromSeconds(30),
            MinPlayerLevel = 1,
            Description = "기본 게임 모드"
        };
        
        _matchConfigs[GameMode.Speed] = new MatchConfig
        {
            DisplayName = "스피드",
            EnergyCost = 2,
            EstimatedWaitTime = TimeSpan.FromSeconds(45),
            MinPlayerLevel = 5,
            Description = "빠른 진행의 게임 모드"
        };
        
        _matchConfigs[GameMode.Challenge] = new MatchConfig
        {
            DisplayName = "챌린지",
            EnergyCost = 3,
            EstimatedWaitTime = TimeSpan.FromMinutes(2),
            MinPlayerLevel = 10,
            Description = "고난이도 게임 모드"
        };
    }

    private void InitializeMatchingState()
    {
        SetMatchingState(MatchingState.Idle);
        _selectedGameMode = GameMode.Classic;
        
        // 기본 게임 모드 선택
        if (classicModeToggle != null)
            classicModeToggle.isOn = true;
    }

    private void SetMatchingState(MatchingState newState)
    {
        if (_currentMatchingState == newState) return;
        
        MatchingState previousState = _currentMatchingState;
        _currentMatchingState = newState;
        
        UpdateUIForMatchingState(newState);
        
        Debug.Log($"[MatchingSectionUI] Matching state changed: {previousState} -> {newState}");
    }

    private void UpdateUIForMatchingState(MatchingState state)
    {
        SafeUIUpdate(() =>
        {
            switch (state)
            {
                case MatchingState.Idle:
                    matchStatusText.text = "매칭 대기 중";
                    EnableMatchingButtons(true);
                    ShowMatchingAnimation(false);
                    ShowCancelButton(false);
                    break;
                    
                case MatchingState.Searching:
                    matchStatusText.text = "상대방을 찾는 중...";
                    EnableMatchingButtons(false);
                    ShowMatchingAnimation(true);
                    ShowCancelButton(true);
                    StartMatchingTimer();
                    break;
                    
                case MatchingState.Found:
                    matchStatusText.text = "상대방을 찾았습니다!";
                    ShowMatchingAnimation(false);
                    break;
                    
                case MatchingState.Connecting:
                    matchStatusText.text = "게임에 연결 중...";
                    break;
                    
                case MatchingState.Ready:
                    matchStatusText.text = "게임 시작 준비 완료";
                    break;
                    
                case MatchingState.Failed:
                    matchStatusText.text = "매칭 실패";
                    EnableMatchingButtons(true);
                    ShowMatchingAnimation(false);
                    ShowCancelButton(false);
                    break;
            }
        }, "Matching State Update");
    }

    private void StartMatchingTimer()
    {
        if (_matchingTimerCoroutine != null)
            StopCoroutine(_matchingTimerCoroutine);
            
        _matchStartTime = DateTime.Now;
        _matchingTimerCoroutine = StartCoroutine(MatchingTimerCoroutine());
    }

    private void StopMatchingTimer()
    {
        if (_matchingTimerCoroutine != null)
        {
            StopCoroutine(_matchingTimerCoroutine);
            _matchingTimerCoroutine = null;
        }
    }

    private IEnumerator MatchingTimerCoroutine()
    {
        while (_currentMatchingState == MatchingState.Searching)
        {
            var elapsed = DateTime.Now - _matchStartTime;
            
            SafeUIUpdate(() =>
            {
                if (waitTimeText != null)
                    waitTimeText.text = $"대기 시간: {elapsed:mm\\:ss}";
                    
                if (matchProgressSlider != null)
                {
                    var config = _matchConfigs[_selectedGameMode];
                    float progress = Mathf.Clamp01((float)(elapsed.TotalSeconds / config.EstimatedWaitTime.TotalSeconds));
                    matchProgressSlider.value = progress;
                }
            }, "Matching Timer");
            
            yield return new WaitForSeconds(1f);
        }
    }
    #endregion

    #region UI State Management
    private void EnableMatchingButtons(bool enabled)
    {
        if (quickMatchButton != null)
            quickMatchButton.interactable = enabled;
            
        if (rankedMatchButton != null)
            rankedMatchButton.interactable = enabled;
            
        if (customMatchButton != null)
            customMatchButton.interactable = enabled;
            
        if (friendMatchButton != null)
            friendMatchButton.interactable = enabled;
    }

    private void ShowCancelButton(bool show)
    {
        if (cancelMatchButton != null)
            cancelMatchButton.gameObject.SetActive(show);
    }

    private void ShowMatchingAnimation(bool show)
    {
        if (matchingAnimationObject != null)
            matchingAnimationObject.SetActive(show);
            
        if (show && _matchingAnimationCoroutine == null)
        {
            _matchingAnimationCoroutine = StartCoroutine(MatchingAnimationCoroutine());
        }
        else if (!show && _matchingAnimationCoroutine != null)
        {
            StopCoroutine(_matchingAnimationCoroutine);
            _matchingAnimationCoroutine = null;
        }
    }

    private IEnumerator MatchingAnimationCoroutine()
    {
        while (matchingAnimationObject != null && matchingAnimationObject.activeInHierarchy)
        {
            // 간단한 회전 애니메이션
            matchingAnimationObject.transform.Rotate(0, 0, 90 * Time.deltaTime);
            yield return null;
        }
    }

    private void StopMatchingAnimations()
    {
        if (_matchingAnimationCoroutine != null)
        {
            StopCoroutine(_matchingAnimationCoroutine);
            _matchingAnimationCoroutine = null;
        }
        
        ShowMatchingAnimation(false);
    }

    private void UpdateOnlineStatus()
    {
        bool isOnline = !_isOfflineMode;
        
        if (onlineStatusIndicator != null)
            onlineStatusIndicator.SetActive(isOnline);
            
        if (offlineNotification != null)
            offlineNotification.SetActive(!isOnline);
            
        // 오프라인 모드에서는 매칭 버튼 비활성화
        if (!isOnline)
        {
            EnableMatchingButtons(false);
            if (_currentMatchingState == MatchingState.Searching)
            {
                CancelMatching();
            }
        }
        else
        {
            EnableMatchingButtons(_currentMatchingState == MatchingState.Idle);
        }
    }
    #endregion

    #region Game Mode Management
    private void SetupGameModeToggles()
    {
        if (gameModeToggleGroup != null)
        {
            gameModeToggleGroup.allowSwitchOff = false;
        }
    }

    private void OnGameModeChanged(GameMode mode, bool isOn)
    {
        if (isOn)
        {
            _selectedGameMode = mode;
            UpdateGameModeInfo();
            
            Debug.Log($"[MatchingSectionUI] Game mode selected: {mode}");
        }
    }

    private void UpdateGameModeInfo()
    {
        if (_matchConfigs.TryGetValue(_selectedGameMode, out MatchConfig config))
        {
            SafeUIUpdate(() =>
            {
                if (averageWaitTimeText != null)
                    averageWaitTimeText.text = $"예상 대기 시간: {config.EstimatedWaitTime:mm\\:ss}";
                    
            }, "Game Mode Info");
        }
    }

    private void UpdateRecommendedMode(UserData userData)
    {
        if (recommendedModeText == null) return;
        
        GameMode recommended = GameMode.Classic;
        
        // 사용자 레벨에 따른 추천 모드 결정
        if (userData.Level >= 10)
            recommended = GameMode.Challenge;
        else if (userData.Level >= 5)
            recommended = GameMode.Speed;
        
        SafeUIUpdate(() =>
        {
            recommendedModeText.text = $"추천: {_matchConfigs[recommended].DisplayName}";
        }, "Recommended Mode");
    }

    private void UpdateMatchingAvailability(UserData userData)
    {
        // 각 모드별 가용성 확인
        foreach (var kvp in _matchConfigs)
        {
            bool canPlay = userData.Level >= kvp.Value.MinPlayerLevel && 
                          userData.CurrentEnergy >= kvp.Value.EnergyCost;
                          
            UpdateModeButtonAvailability(kvp.Key, canPlay);
        }
    }

    private void UpdateModeButtonAvailability(GameMode mode, bool available)
    {
        Toggle toggle = mode switch
        {
            GameMode.Classic => classicModeToggle,
            GameMode.Speed => speedModeToggle,
            GameMode.Challenge => challengeModeToggle,
            _ => null
        };
        
        if (toggle != null)
        {
            toggle.interactable = available;
        }
    }
    #endregion

    #region Player Count Updates
    private void StartPlayerCountUpdates()
    {
        // 실제 구현에서는 서버에서 플레이어 수 정보를 받아와야 함
        // 현재는 임시 데이터 사용
        StartCoroutine(PlayerCountUpdateCoroutine());
    }

    private void StopPlayerCountUpdates()
    {
        // 플레이어 수 업데이트 중지
    }

    private IEnumerator PlayerCountUpdateCoroutine()
    {
        while (_isActive)
        {
            // 임시 플레이어 수 (실제로는 서버에서 받아옴)
            _currentOnlinePlayerCount = UnityEngine.Random.Range(50, 200);
            
            SafeUIUpdate(() =>
            {
                if (playerCountText != null)
                    playerCountText.text = $"온라인 플레이어: {_currentOnlinePlayerCount}명";
            }, "Player Count");
            
            yield return new WaitForSeconds(30f); // 30초마다 업데이트
        }
    }
    #endregion

    #region Event Handlers
    private void OnQuickMatchClicked()
    {
        Debug.Log("[MatchingSectionUI] Quick match clicked");
        StartMatching(MatchType.Quick);
    }

    private void OnRankedMatchClicked()
    {
        Debug.Log("[MatchingSectionUI] Ranked match clicked");
        StartMatching(MatchType.Ranked);
    }

    private void OnCustomMatchClicked()
    {
        Debug.Log("[MatchingSectionUI] Custom match clicked");
        StartMatching(MatchType.Custom);
    }

    private void OnFriendMatchClicked()
    {
        Debug.Log("[MatchingSectionUI] Friend match clicked");
        StartMatching(MatchType.Friend);
    }

    private void OnCancelMatchClicked()
    {
        Debug.Log("[MatchingSectionUI] Cancel match clicked");
        CancelMatching();
    }
    #endregion

    #region Matching Operations
    private void StartMatching(MatchType matchType)
    {
        // 에너지 체크
        if (!CheckEnergyRequirement())
        {
            ShowEnergyRequiredMessage();
            return;
        }
        
        // 오프라인 모드 체크
        if (_isOfflineMode)
        {
            ShowOfflineMessage();
            return;
        }
        
        // 에너지 소모
        ConsumeEnergyForMatch();
        
        SetMatchingState(MatchingState.Searching);
        
        // 실제 매칭 로직 시작 (향후 NetworkManager와 연동)
        StartCoroutine(MockMatchingCoroutine(matchType));
        
        Debug.Log($"[MatchingSectionUI] Started matching: {matchType} in {_selectedGameMode} mode");
    }

    private void CancelMatching()
    {
        if (_currentMatchingState != MatchingState.Searching) return;
        
        SetMatchingState(MatchingState.Idle);
        StopMatchingTimer();
        
        // 에너지 환불 (선택적)
        RefundEnergyForCancelledMatch();
        
        Debug.Log("[MatchingSectionUI] Matching cancelled");
    }

    private IEnumerator MockMatchingCoroutine(MatchType matchType)
    {
        // 임시 매칭 시뮬레이션
        float waitTime = UnityEngine.Random.Range(10f, 30f);
        yield return new WaitForSeconds(waitTime);
        
        if (_currentMatchingState == MatchingState.Searching)
        {
            // 80% 확률로 매칭 성공
            if (UnityEngine.Random.value < 0.8f)
            {
                SetMatchingState(MatchingState.Found);
                yield return new WaitForSeconds(2f);
                
                SetMatchingState(MatchingState.Connecting);
                yield return new WaitForSeconds(3f);
                
                SetMatchingState(MatchingState.Ready);
                
                // 게임 화면으로 전환 (향후 구현)
                OnMatchingSuccess();
            }
            else
            {
                SetMatchingState(MatchingState.Failed);
                yield return new WaitForSeconds(3f);
                SetMatchingState(MatchingState.Idle);
            }
        }
    }

    private void OnMatchingSuccess()
    {
        Debug.Log("[MatchingSectionUI] Matching successful - transitioning to game");
        
        // 게임 화면으로 전환하거나 게임 시작 알림
        BroadcastToAllSections(new MatchFoundMessage
        {
            GameMode = _selectedGameMode,
            Timestamp = DateTime.Now
        });
    }
    #endregion

    #region Energy Integration
    private bool CheckEnergyRequirement()
    {
        var config = _matchConfigs[_selectedGameMode];
        
        // EnergySection에서 에너지 상태 확인
        SendMessageToSection(MainPageSectionType.Energy, new EnergyRequest
        {
            RequestType = "check",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        });
        
        // 임시로 UserData에서 직접 확인
        if (_userDataManager?.CurrentUser != null)
        {
            return _userDataManager.CurrentUser.CurrentEnergy >= config.EnergyCost;
        }
        
        return false;
    }

    private void ConsumeEnergyForMatch()
    {
        var config = _matchConfigs[_selectedGameMode];
        
        SendMessageToSection(MainPageSectionType.Energy, new EnergyRequest
        {
            RequestType = "consume",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        });
    }

    private void RefundEnergyForCancelledMatch()
    {
        var config = _matchConfigs[_selectedGameMode];
        
        SendMessageToSection(MainPageSectionType.Energy, new EnergyRequest
        {
            RequestType = "add",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        });
    }

    private void ShowEnergyRequiredMessage()
    {
        var config = _matchConfigs[_selectedGameMode];
        matchStatusText.text = $"에너지 {config.EnergyCost}개 필요";
        
        // 에너지 섹션으로 포커스 요청
        SendMessageToSection(MainPageSectionType.Energy, "focus_requested");
    }

    private void ShowOfflineMessage()
    {
        matchStatusText.text = "온라인 연결이 필요합니다";
    }
    #endregion

    #region Virtual Method Overrides
    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        UpdateOnlineStatus();
        
        if (isOfflineMode && _currentMatchingState == MatchingState.Searching)
        {
            CancelMatching();
        }
    }

    protected override void OnForceRefresh()
    {
        RefreshMatchingDisplay();
        UpdateOnlineStatus();
    }

    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[MatchingSectionUI] Received message from {fromSection}: {data?.GetType().Name}");
        
        if (data is EnergyStatusResponse energyResponse)
        {
            HandleEnergyStatusResponse(energyResponse);
        }
        else if (data is string message && message == "focus_requested")
        {
            // 매칭 섹션에 포커스 요청 받음
            RefreshMatchingDisplay();
        }
    }
    #endregion

    #region Message Handling
    private void HandleEnergyStatusResponse(EnergyStatusResponse response)
    {
        // 에너지 상태에 따른 UI 업데이트
        bool canMatch = response.CanUseEnergy;
        
        if (!canMatch && _currentMatchingState == MatchingState.Idle)
        {
            EnableMatchingButtons(false);
        }
    }
    #endregion

    #region Utility Methods
    private void RefreshMatchingDisplay()
    {
        UpdateGameModeInfo();
        
        if (_userDataManager?.CurrentUser != null)
        {
            UpdateUI(_userDataManager.CurrentUser);
        }
        
        UpdateOnlineStatus();
    }
    #endregion
}

#region Enums and Data Classes
public enum MatchType
{
    Quick,
    Ranked,
    Custom,
    Friend
}

[System.Serializable]
public class MatchConfig
{
    public string DisplayName;
    public int EnergyCost;
    public TimeSpan EstimatedWaitTime;
    public int MinPlayerLevel;
    public string Description;
}

[System.Serializable]
public class MatchFoundMessage
{
    public MatchingSectionUI.GameMode GameMode;
    public DateTime Timestamp;
}
#endregion