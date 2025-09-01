using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매칭 섹션 컨트롤러
/// 게임 매칭 시스템을 관리하고 UI와 연동합니다.
/// 랜덤 매칭, 방 생성/참여, 매칭 상태 관리를 담당합니다.
/// </summary>
public class MatchingSection : SectionBase
{
    #region Section Properties
    public override MainPageSectionType SectionType => MainPageSectionType.Matching;
    public override string SectionDisplayName => "매칭";
    #endregion

    #region UI Components
    [Header("UI References")]
    [SerializeField] public MatchingSectionUI matchingUI;
    #endregion

    #region Matching System
    private MatchingManager _matchingManager;
    private NetworkManager _networkManager;
    private RoomManager _roomManager;
    private PlayerCountManager _playerCountManager;
    
    // Matching state
    private MatchingState _currentMatchingState = MatchingState.Idle;
    private GameMode _selectedGameMode = GameMode.Classic;
    private string _currentRoomCode;
    private DateTime _matchStartTime;
    
    // Coroutines
    private Coroutine _matchingCoroutine;
    private Coroutine _playerCountUpdateCoroutine;
    private Coroutine _matchingStatusCoroutine;
    
    // Matching configuration
    private readonly Dictionary<GameMode, MatchConfig> _matchConfigs = new();
    private MatchingConfig _matchingConfig;
    #endregion

    #region Events
    public static event Action<MatchingState> OnMatchingStateChanged;
    public static event Action<GameMode> OnGameModeChanged;
    public static event Action<string> OnRoomCodeGenerated;
    public static event Action<MatchFoundData> OnMatchFound;
    public static event Action<string> OnMatchingError;
    public static event Action<int> OnPlayerCountUpdated;
    #endregion

    #region SectionBase Implementation
    protected override void OnInitialize()
    {
        InitializeMatchingConfig();
        InitializeMatchingSystems();
        InitializeMatchConfigs();
        ValidateMatchingComponents();
        
        Debug.Log($"[MatchingSection] Initialized with {_matchConfigs.Count} game modes");
    }

    protected override void OnActivate()
    {
        StartMatchingServices();
        StartPlayerCountUpdates();
        RefreshMatchingDisplay();
        
        // Subscribe to UI events
        if (matchingUI != null)
        {
            matchingUI.OnMatchingRequested += HandleMatchingRequest;
            matchingUI.OnRoomCreationRequested += HandleRoomCreationRequest;
            matchingUI.OnRoomJoinRequested += HandleRoomJoinRequest;
            matchingUI.OnMatchCancelRequested += HandleMatchCancelRequest;
            matchingUI.OnGameModeSelected += HandleGameModeSelection;
        }
    }

    protected override void OnDeactivate()
    {
        StopMatchingServices();
        StopPlayerCountUpdates();
        
        // Cancel any ongoing matching
        if (_currentMatchingState == MatchingState.Searching)
        {
            CancelMatching();
        }
        
        // Unsubscribe from UI events
        if (matchingUI != null)
        {
            matchingUI.OnMatchingRequested -= HandleMatchingRequest;
            matchingUI.OnRoomCreationRequested -= HandleRoomCreationRequest;
            matchingUI.OnRoomJoinRequested -= HandleRoomJoinRequest;
            matchingUI.OnMatchCancelRequested -= HandleMatchCancelRequest;
            matchingUI.OnGameModeSelected -= HandleGameModeSelection;
        }
    }

    protected override void OnCleanup()
    {
        _matchingManager?.Cleanup();
        _roomManager?.Cleanup();
        _playerCountManager?.Cleanup();
        
        StopAllCoroutines();
        
        // Clear events
        OnMatchingStateChanged = null;
        OnGameModeChanged = null;
        OnRoomCodeGenerated = null;
        OnMatchFound = null;
        OnMatchingError = null;
        OnPlayerCountUpdated = null;
    }

    protected override void UpdateUI(UserData userData)
    {
        if (userData == null || matchingUI == null) return;
        
        // Update UI with user data
        matchingUI.UpdateUserInfo(
            userData.DisplayName,
            userData.Level,
            userData.CurrentEnergy,
            userData.MaxEnergy
        );
        
        // Update matching availability
        UpdateMatchingAvailability(userData);
        
        Debug.Log($"[MatchingSection] UI updated for user: {userData.DisplayName} (Level {userData.Level})");
    }

    protected override void ValidateComponents()
    {
        if (matchingUI == null)
        {
            // Try to find in children
            matchingUI = GetComponentInChildren<MatchingSectionUI>();
            if (matchingUI == null)
            {
                ReportError("MatchingSectionUI component is missing!");
                return;
            }
        }
    }
    #endregion

    #region Matching System Initialization
    private void InitializeMatchingConfig()
    {
        _matchingConfig = new MatchingConfig
        {
            MaxWaitTime = TimeSpan.FromMinutes(GetSetting<float>("Matching.MaxWaitTimeMinutes", 5f)),
            PlayerCountUpdateInterval = GetSetting<float>("Matching.PlayerCountUpdateInterval", 30f),
            EnableRoomCreation = GetSetting<bool>("Matching.EnableRoomCreation", true),
            EnableRandomMatching = GetSetting<bool>("Matching.EnableRandomMatching", true),
            MaxPlayersPerRoom = GetSetting<int>("Matching.MaxPlayersPerRoom", 4),
            MinPlayersPerRoom = GetSetting<int>("Matching.MinPlayersPerRoom", 2)
        };
    }

    private void InitializeMatchingSystems()
    {
        // Initialize networking
        _networkManager = NetworkManager.Instance;
        
        // Initialize matching manager
        _matchingManager = new MatchingManager(_matchingConfig, _networkManager);
        _matchingManager.OnMatchingStateChanged += OnMatchingManagerStateChanged;
        _matchingManager.OnMatchFound += OnMatchingManagerFound;
        _matchingManager.OnMatchingError += OnMatchingManagerError;
        
        // Initialize room manager
        _roomManager = new RoomManager(_networkManager);
        _roomManager.OnRoomCreated += OnRoomManagerCreated;
        _roomManager.OnRoomJoined += OnRoomManagerJoined;
        _roomManager.OnRoomError += OnRoomManagerError;
        
        // Initialize player count manager
        _playerCountManager = new PlayerCountManager(_networkManager);
        _playerCountManager.OnPlayerCountUpdated += OnPlayerCountManagerUpdated;
        
        Debug.Log("[MatchingSection] Matching systems initialized");
    }

    private void InitializeMatchConfigs()
    {
        _matchConfigs[GameMode.Classic] = new MatchConfig
        {
            DisplayName = "클래식",
            EnergyCost = 1,
            EstimatedWaitTime = TimeSpan.FromSeconds(30),
            MinPlayerLevel = 1,
            MaxPlayers = 4,
            MinPlayers = 2,
            Description = "기본 4인 주사위 게임"
        };
        
        _matchConfigs[GameMode.Speed] = new MatchConfig
        {
            DisplayName = "스피드",
            EnergyCost = 2,
            EstimatedWaitTime = TimeSpan.FromSeconds(45),
            MinPlayerLevel = 5,
            MaxPlayers = 4,
            MinPlayers = 2,
            Description = "빠른 진행의 주사위 게임"
        };
        
        _matchConfigs[GameMode.Challenge] = new MatchConfig
        {
            DisplayName = "챌린지",
            EnergyCost = 3,
            EstimatedWaitTime = TimeSpan.FromMinutes(2),
            MinPlayerLevel = 10,
            MaxPlayers = 4,
            MinPlayers = 2,
            Description = "고난이도 주사위 게임"
        };

        _matchConfigs[GameMode.Ranked] = new MatchConfig
        {
            DisplayName = "랭크",
            EnergyCost = 2,
            EstimatedWaitTime = TimeSpan.FromMinutes(1.5f),
            MinPlayerLevel = 15,
            MaxPlayers = 4,
            MinPlayers = 2,
            Description = "랭킹전 주사위 게임"
        };
    }
    #endregion

    #region Matching Services
    private void StartMatchingServices()
    {
        _matchingManager?.StartServices();
        _roomManager?.StartServices();
        _playerCountManager?.StartServices();
        
        // Start matching status updates
        if (_matchingStatusCoroutine == null)
        {
            _matchingStatusCoroutine = StartCoroutine(MatchingStatusUpdateCoroutine());
        }
        
        Debug.Log("[MatchingSection] Matching services started");
    }

    private void StopMatchingServices()
    {
        _matchingManager?.StopServices();
        _roomManager?.StopServices();
        _playerCountManager?.StopServices();
        
        if (_matchingStatusCoroutine != null)
        {
            StopCoroutine(_matchingStatusCoroutine);
            _matchingStatusCoroutine = null;
        }
        
        Debug.Log("[MatchingSection] Matching services stopped");
    }

    private void StartPlayerCountUpdates()
    {
        if (_playerCountUpdateCoroutine == null && _playerCountManager != null)
        {
            _playerCountUpdateCoroutine = StartCoroutine(PlayerCountUpdateCoroutine());
        }
    }

    private void StopPlayerCountUpdates()
    {
        if (_playerCountUpdateCoroutine != null)
        {
            StopCoroutine(_playerCountUpdateCoroutine);
            _playerCountUpdateCoroutine = null;
        }
    }

    private IEnumerator MatchingStatusUpdateCoroutine()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(1f); // Update every second
            
            if (_currentMatchingState == MatchingState.Searching)
            {
                UpdateMatchingUI();
            }
        }
    }

    private IEnumerator PlayerCountUpdateCoroutine()
    {
        while (_isActive && _playerCountManager != null)
        {
            yield return new WaitForSeconds(_matchingConfig.PlayerCountUpdateInterval);
            _playerCountManager.RefreshPlayerCount();
        }
    }
    #endregion

    #region UI Event Handlers
    public void HandleMatchingRequest(MatchingRequest request)
    {
        switch (request.MatchType)
        {
            case MatchType.Quick:
                StartQuickMatch(request.GameMode);
                break;
            case MatchType.Ranked:
                StartRankedMatch(request.GameMode);
                break;
            case MatchType.Custom:
                // Custom matching logic (future enhancement)
                Debug.Log("[MatchingSection] Custom matching not yet implemented");
                break;
            case MatchType.Friend:
                // Friend matching logic (future enhancement)
                Debug.Log("[MatchingSection] Friend matching not yet implemented");
                break;
        }
    }

    public void HandleRoomCreationRequest(RoomCreationRequest request)
    {
        CreateRoom(request.GameMode, request.MaxPlayers, request.IsPrivate);
    }

    public void HandleRoomJoinRequest(RoomJoinRequest request)
    {
        JoinRoom(request.RoomCode);
    }

    public void HandleMatchCancelRequest()
    {
        CancelMatching();
    }

    public void HandleGameModeSelection(GameMode gameMode)
    {
        SetSelectedGameMode(gameMode);
    }
    #endregion

    #region Matching Operations
    /// <summary>
    /// 빠른 매칭 시작
    /// </summary>
    public void StartQuickMatch(GameMode gameMode = GameMode.Classic)
    {
        if (!ValidateMatchingConditions(gameMode))
        {
            return;
        }

        SetSelectedGameMode(gameMode);
        
        // Energy validation
        if (!ValidateEnergyRequirement())
        {
            return;
        }

        // Consume energy for match
        ConsumeEnergyForMatch();
        
        SetMatchingState(MatchingState.Searching);
        
        var matchRequest = new MatchRequest
        {
            GameMode = gameMode,
            MatchType = MatchType.Quick,
            PlayerLevel = _cachedUserData?.Level ?? 1,
            RequestTime = DateTime.Now
        };
        
        _matchingManager.StartMatching(matchRequest);
        
        Debug.Log($"[MatchingSection] Quick match started: {gameMode}");
    }

    /// <summary>
    /// 랭크 매칭 시작
    /// </summary>
    public void StartRankedMatch(GameMode gameMode = GameMode.Classic)
    {
        if (!ValidateMatchingConditions(gameMode))
        {
            return;
        }

        // Additional validation for ranked matches
        if (!ValidateRankedMatchConditions())
        {
            return;
        }

        SetSelectedGameMode(gameMode);
        
        // Energy validation
        if (!ValidateEnergyRequirement())
        {
            return;
        }

        // Consume energy for match
        ConsumeEnergyForMatch();
        
        SetMatchingState(MatchingState.Searching);
        
        var matchRequest = new MatchRequest
        {
            GameMode = gameMode,
            MatchType = MatchType.Ranked,
            PlayerLevel = _cachedUserData?.Level ?? 1,
            PlayerRating = _cachedUserData?.Rating ?? 1000,
            RequestTime = DateTime.Now
        };
        
        _matchingManager.StartMatching(matchRequest);
        
        Debug.Log($"[MatchingSection] Ranked match started: {gameMode}");
    }

    /// <summary>
    /// 방 생성
    /// </summary>
    public void CreateRoom(GameMode gameMode, int maxPlayers = 4, bool isPrivate = false)
    {
        if (!ValidateMatchingConditions(gameMode))
        {
            return;
        }

        SetSelectedGameMode(gameMode);
        
        var roomConfig = new RoomConfig
        {
            GameMode = gameMode,
            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4),
            MinPlayers = 2,
            IsPrivate = isPrivate,
            CreatorId = _cachedUserData?.UserId ?? "unknown",
            CreatorName = _cachedUserData?.DisplayName ?? "Unknown Player",
            EnergyCost = _matchConfigs[gameMode].EnergyCost
        };
        
        _roomManager.CreateRoom(roomConfig);
        
        Debug.Log($"[MatchingSection] Creating room: {gameMode}, MaxPlayers: {maxPlayers}");
    }

    /// <summary>
    /// 방 참여
    /// </summary>
    public void JoinRoom(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode) || roomCode.Length != 4)
        {
            ShowError("올바른 4자리 방 코드를 입력해주세요.");
            return;
        }

        if (_isOfflineMode)
        {
            ShowError("온라인 연결이 필요합니다.");
            return;
        }

        SetMatchingState(MatchingState.Connecting);
        
        var joinRequest = new RoomJoinRequest
        {
            RoomCode = roomCode.ToUpper(),
            PlayerId = _cachedUserData?.UserId ?? "unknown",
            PlayerName = _cachedUserData?.DisplayName ?? "Unknown Player",
            PlayerLevel = _cachedUserData?.Level ?? 1
        };
        
        _roomManager.JoinRoom(joinRequest);
        
        Debug.Log($"[MatchingSection] Joining room: {roomCode}");
    }

    /// <summary>
    /// 매칭 취소
    /// </summary>
    public void CancelMatching()
    {
        if (_currentMatchingState != MatchingState.Searching)
        {
            return;
        }

        _matchingManager?.CancelMatching();
        SetMatchingState(MatchingState.Idle);
        
        // Refund energy for cancelled match
        RefundEnergyForCancelledMatch();
        
        Debug.Log("[MatchingSection] Matching cancelled");
    }
    #endregion

    #region Validation
    private bool ValidateMatchingConditions(GameMode gameMode)
    {
        // Offline mode check
        if (_isOfflineMode)
        {
            ShowError("온라인 연결이 필요합니다.");
            return false;
        }

        // User data check
        if (_cachedUserData == null)
        {
            ShowError("사용자 정보를 불러올 수 없습니다.");
            return false;
        }

        // Game mode config check
        if (!_matchConfigs.TryGetValue(gameMode, out MatchConfig config))
        {
            ShowError($"지원하지 않는 게임 모드입니다: {gameMode}");
            return false;
        }

        // Level requirement check
        if (_cachedUserData.Level < config.MinPlayerLevel)
        {
            ShowError($"{config.DisplayName} 모드는 레벨 {config.MinPlayerLevel} 이상 필요합니다.");
            return false;
        }

        // Already matching check
        if (_currentMatchingState == MatchingState.Searching || _currentMatchingState == MatchingState.Connecting)
        {
            ShowError("이미 매칭 중입니다.");
            return false;
        }

        return true;
    }

    private bool ValidateRankedMatchConditions()
    {
        // Additional validation for ranked matches
        if (_cachedUserData.Level < 15)
        {
            ShowError("랭크전은 레벨 15 이상 필요합니다.");
            return false;
        }

        return true;
    }

    private bool ValidateEnergyRequirement()
    {
        if (!_matchConfigs.TryGetValue(_selectedGameMode, out MatchConfig config))
        {
            return false;
        }

        // Check energy through EnergySection
        var energyRequest = new EnergyRequest
        {
            RequestType = "check",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        };

        SendMessageToSection(MainPageSectionType.Energy, energyRequest);

        // Also check cached user data
        bool hasEnoughEnergy = _cachedUserData?.CurrentEnergy >= config.EnergyCost;
        
        if (!hasEnoughEnergy)
        {
            ShowError($"{config.DisplayName} 모드는 에너지 {config.EnergyCost}개가 필요합니다.");
            
            // Request focus on energy section
            SendMessageToSection(MainPageSectionType.Energy, "focus_requested");
        }

        return hasEnoughEnergy;
    }

    private void ConsumeEnergyForMatch()
    {
        if (!_matchConfigs.TryGetValue(_selectedGameMode, out MatchConfig config))
        {
            return;
        }

        var energyRequest = new EnergyRequest
        {
            RequestType = "consume",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        };

        SendMessageToSection(MainPageSectionType.Energy, energyRequest);
    }

    private void RefundEnergyForCancelledMatch()
    {
        if (!_matchConfigs.TryGetValue(_selectedGameMode, out MatchConfig config))
        {
            return;
        }

        var energyRequest = new EnergyRequest
        {
            RequestType = "add",
            Amount = config.EnergyCost,
            RequesterSection = SectionType
        };

        SendMessageToSection(MainPageSectionType.Energy, energyRequest);
    }
    #endregion

    #region State Management
    private void SetMatchingState(MatchingState newState)
    {
        if (_currentMatchingState == newState) return;

        var previousState = _currentMatchingState;
        _currentMatchingState = newState;
        
        OnMatchingStateChanged?.Invoke(newState);
        
        // Update UI
        UpdateMatchingStateUI(newState);
        
        Debug.Log($"[MatchingSection] State changed: {previousState} -> {newState}");
    }

    private void SetSelectedGameMode(GameMode gameMode)
    {
        if (_selectedGameMode == gameMode) return;

        _selectedGameMode = gameMode;
        OnGameModeChanged?.Invoke(gameMode);
        
        // Update UI
        UpdateGameModeUI(gameMode);
        
        Debug.Log($"[MatchingSection] Game mode selected: {gameMode}");
    }

    private void UpdateMatchingStateUI(MatchingState state)
    {
        if (matchingUI == null) return;

        SafeUIUpdate(() =>
        {
            matchingUI.SetMatchingState(state);
            
            switch (state)
            {
                case MatchingState.Idle:
                    matchingUI.ShowMessage("매칭 대기 중");
                    matchingUI.SetButtonsInteractable(true);
                    break;
                    
                case MatchingState.Searching:
                    matchingUI.ShowMessage("상대방을 찾는 중...");
                    matchingUI.SetButtonsInteractable(false);
                    matchingUI.ShowMatchingProgress(true);
                    _matchStartTime = DateTime.Now;
                    break;
                    
                case MatchingState.Found:
                    matchingUI.ShowMessage("매칭 성공!");
                    matchingUI.ShowMatchingProgress(false);
                    break;
                    
                case MatchingState.Connecting:
                    matchingUI.ShowMessage("게임에 연결 중...");
                    break;
                    
                case MatchingState.Ready:
                    matchingUI.ShowMessage("게임 시작 준비 완료");
                    break;
                    
                case MatchingState.Failed:
                    matchingUI.ShowMessage("매칭 실패");
                    matchingUI.SetButtonsInteractable(true);
                    matchingUI.ShowMatchingProgress(false);
                    break;
            }
        }, "Matching State Update");
    }

    private void UpdateGameModeUI(GameMode gameMode)
    {
        if (matchingUI == null || !_matchConfigs.TryGetValue(gameMode, out MatchConfig config))
        {
            return;
        }

        SafeUIUpdate(() =>
        {
            matchingUI.UpdateGameModeInfo(gameMode, config);
        }, "Game Mode Update");
    }

    private void UpdateMatchingUI()
    {
        if (matchingUI == null || _currentMatchingState != MatchingState.Searching)
        {
            return;
        }

        var elapsed = DateTime.Now - _matchStartTime;
        var estimatedWait = _matchConfigs[_selectedGameMode].EstimatedWaitTime;
        
        SafeUIUpdate(() =>
        {
            matchingUI.UpdateMatchingProgress(elapsed, estimatedWait);
        }, "Matching Progress Update");
    }

    private void UpdateMatchingAvailability(UserData userData)
    {
        if (matchingUI == null) return;

        foreach (var kvp in _matchConfigs)
        {
            bool canPlay = userData.Level >= kvp.Value.MinPlayerLevel && 
                          userData.CurrentEnergy >= kvp.Value.EnergyCost;
                          
            matchingUI.SetGameModeAvailable(kvp.Key, canPlay);
        }
    }
    #endregion

    #region Event Handlers
    private void OnMatchingManagerStateChanged(MatchingState state)
    {
        SetMatchingState(state);
    }

    private void OnMatchingManagerFound(MatchFoundData matchData)
    {
        SetMatchingState(MatchingState.Found);
        OnMatchFound?.Invoke(matchData);
        
        // Start game connection process
        StartCoroutine(ConnectToGameCoroutine(matchData));
    }

    private void OnMatchingManagerError(string error)
    {
        SetMatchingState(MatchingState.Failed);
        OnMatchingError?.Invoke(error);
        ShowError(error);
        
        // Auto-return to idle state after delay
        StartCoroutine(ReturnToIdleAfterDelay(3f));
    }

    private void OnRoomManagerCreated(RoomCreatedData roomData)
    {
        _currentRoomCode = roomData.RoomCode;
        OnRoomCodeGenerated?.Invoke(roomData.RoomCode);
        
        SetMatchingState(MatchingState.Ready);
        ShowSuccess($"방이 생성되었습니다: {roomData.RoomCode}");
    }

    private void OnRoomManagerJoined(RoomJoinedData roomData)
    {
        _currentRoomCode = roomData.RoomCode;
        SetMatchingState(MatchingState.Ready);
        ShowSuccess($"방에 참여했습니다: {roomData.RoomCode}");
    }

    private void OnRoomManagerError(string error)
    {
        SetMatchingState(MatchingState.Failed);
        ShowError(error);
        
        // Auto-return to idle state after delay
        StartCoroutine(ReturnToIdleAfterDelay(3f));
    }

    private void OnPlayerCountManagerUpdated(PlayerCountData countData)
    {
        OnPlayerCountUpdated?.Invoke(countData.TotalOnlinePlayers);
        
        if (matchingUI != null)
        {
            SafeUIUpdate(() =>
            {
                matchingUI.UpdatePlayerCount(countData.TotalOnlinePlayers);
                matchingUI.UpdateEstimatedWaitTimes(countData.GameModePlayerCounts);
            }, "Player Count Update");
        }
    }

    private IEnumerator ConnectToGameCoroutine(MatchFoundData matchData)
    {
        SetMatchingState(MatchingState.Connecting);
        
        yield return new WaitForSeconds(2f);
        
        // Simulate connection process
        bool connectionSuccess = UnityEngine.Random.value > 0.1f; // 90% success rate
        
        if (connectionSuccess)
        {
            SetMatchingState(MatchingState.Ready);
            
            // Notify other sections about match success
            BroadcastToAllSections(new MatchReadyMessage
            {
                MatchData = matchData,
                GameMode = _selectedGameMode,
                RoomCode = matchData.RoomId,
                PlayerCount = matchData.PlayerCount
            });
            
            // Transition to game (future implementation)
            Debug.Log($"[MatchingSection] Ready to start game: {matchData.RoomId}");
        }
        else
        {
            SetMatchingState(MatchingState.Failed);
            ShowError("게임 연결에 실패했습니다.");
            
            yield return new WaitForSeconds(3f);
            SetMatchingState(MatchingState.Idle);
        }
    }

    private IEnumerator ReturnToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetMatchingState(MatchingState.Idle);
    }
    #endregion

    #region Message Handling
    protected override void OnReceiveMessage(MainPageSectionType fromSection, object data)
    {
        Debug.Log($"[MatchingSection] Received message from {fromSection}: {data?.GetType().Name}");
        
        switch (data)
        {
            case EnergyResponse energyResponse:
                HandleEnergyResponse(energyResponse, fromSection);
                break;
                
            case string message when message == "focus_requested":
                RefreshMatchingDisplay();
                break;
                
            case UserStatusUpdate userUpdate:
                HandleUserStatusUpdate(userUpdate);
                break;
        }
    }

    private void HandleEnergyResponse(EnergyResponse response, MainPageSectionType requester)
    {
        if (!response.Success)
        {
            ShowError("에너지가 부족합니다.");
            SetMatchingState(MatchingState.Idle);
        }
    }

    private void HandleUserStatusUpdate(UserStatusUpdate update)
    {
        if (update.IsOffline && _currentMatchingState == MatchingState.Searching)
        {
            CancelMatching();
            ShowError("연결이 끊어져 매칭을 취소했습니다.");
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// 현재 매칭 상태 반환
    /// </summary>
    public MatchingState GetCurrentState()
    {
        return _currentMatchingState;
    }

    /// <summary>
    /// 현재 선택된 게임 모드 반환
    /// </summary>
    public GameMode GetSelectedGameMode()
    {
        return _selectedGameMode;
    }

    /// <summary>
    /// 현재 방 코드 반환
    /// </summary>
    public string GetCurrentRoomCode()
    {
        return _currentRoomCode;
    }

    /// <summary>
    /// 게임 모드 설정 반환
    /// </summary>
    public MatchConfig GetGameModeConfig(GameMode gameMode)
    {
        return _matchConfigs.TryGetValue(gameMode, out MatchConfig config) ? config : null;
    }

    /// <summary>
    /// 매칭 가능 여부 확인
    /// </summary>
    public bool CanStartMatching(GameMode gameMode)
    {
        return ValidateMatchingConditions(gameMode) && ValidateEnergyRequirement();
    }

    /// <summary>
    /// 방 생성 가능 여부 확인
    /// </summary>
    public bool CanCreateRoom()
    {
        return !_isOfflineMode && 
               _currentMatchingState == MatchingState.Idle && 
               _matchingConfig.EnableRoomCreation;
    }

    /// <summary>
    /// 매칭 상태 정보 반환
    /// </summary>
    public MatchingStatusInfo GetMatchingStatus()
    {
        return new MatchingStatusInfo
        {
            CurrentState = _currentMatchingState,
            SelectedGameMode = _selectedGameMode,
            CurrentRoomCode = _currentRoomCode,
            MatchStartTime = _matchStartTime,
            ElapsedTime = _currentMatchingState == MatchingState.Searching ? DateTime.Now - _matchStartTime : TimeSpan.Zero,
            IsOfflineMode = _isOfflineMode,
            CanStartMatching = CanStartMatching(_selectedGameMode),
            CanCreateRoom = CanCreateRoom()
        };
    }
    #endregion

    #region Utility Methods
    private void RefreshMatchingDisplay()
    {
        if (_cachedUserData != null)
        {
            UpdateUI(_cachedUserData);
        }
        
        UpdateMatchingStateUI(_currentMatchingState);
        UpdateGameModeUI(_selectedGameMode);
    }

    private void ShowError(string message)
    {
        if (matchingUI != null)
        {
            matchingUI.ShowMessage(message, MessageType.Error);
        }
        Debug.LogError($"[MatchingSection] {message}");
    }

    private void ShowSuccess(string message)
    {
        if (matchingUI != null)
        {
            matchingUI.ShowMessage(message, MessageType.Success);
        }
        Debug.Log($"[MatchingSection] {message}");
    }

    private T GetSetting<T>(string settingName, T defaultValue)
    {
        return _settingsManager != null ? _settingsManager.GetSetting<T>(settingName, defaultValue) : defaultValue;
    }
    #endregion

    #region Settings Override
    protected override void OnSettingUpdated(string settingName, object newValue)
    {
        if (settingName.StartsWith("Matching."))
        {
            // Reload matching configuration
            InitializeMatchingConfig();
            _matchingManager?.UpdateConfig(_matchingConfig);
            
            Debug.Log($"[MatchingSection] Matching config updated: {settingName} = {newValue}");
        }
    }

    protected override void OnForceRefresh()
    {
        RefreshMatchingDisplay();
        _playerCountManager?.ForceRefresh();
        
        if (matchingUI != null)
        {
            matchingUI.ForceRefresh();
        }
    }

    protected override void OnOfflineModeChanged(bool isOfflineMode)
    {
        base.OnOfflineModeChanged(isOfflineMode);
        
        if (isOfflineMode)
        {
            // Cancel any ongoing matching
            if (_currentMatchingState == MatchingState.Searching)
            {
                CancelMatching();
            }
            
            // Update UI
            if (matchingUI != null)
            {
                matchingUI.SetOfflineMode(true);
                matchingUI.ShowMessage("오프라인 모드입니다. 매칭을 사용할 수 없습니다.");
            }
        }
        else
        {
            // Re-enable matching
            if (matchingUI != null)
            {
                matchingUI.SetOfflineMode(false);
                RefreshMatchingDisplay();
            }
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// 매칭 상태 열거형
/// </summary>
public enum MatchingState
{
    Idle,        // 대기 중
    Searching,   // 매칭 중
    Found,       // 매칭 성공
    Connecting,  // 연결 중
    Ready,       // 게임 준비
    Failed       // 실패
}

/// <summary>
/// 게임 모드 열거형
/// </summary>
public enum GameMode
{
    Classic,     // 클래식 모드
    Speed,       // 스피드 모드
    Challenge,   // 챌린지 모드
    Ranked       // 랭크 모드
}

/// <summary>
/// 매칭 타입 열거형
/// </summary>
public enum MatchType
{
    Quick,       // 빠른 매칭
    Ranked,      // 랭크 매칭
    Custom,      // 커스텀 방
    Friend       // 친구 초대
}

/// <summary>
/// 메시지 타입 열거형
/// </summary>
public enum MessageType
{
    Info,
    Warning,
    Error,
    Success
}

/// <summary>
/// 매칭 설정
/// </summary>
[Serializable]
public class MatchingConfig
{
    public TimeSpan MaxWaitTime;
    public float PlayerCountUpdateInterval;
    public bool EnableRoomCreation;
    public bool EnableRandomMatching;
    public int MaxPlayersPerRoom;
    public int MinPlayersPerRoom;
}

/// <summary>
/// 매치 설정
/// </summary>
[Serializable]
public class MatchConfig
{
    public string DisplayName;
    public int EnergyCost;
    public TimeSpan EstimatedWaitTime;
    public int MinPlayerLevel;
    public int MaxPlayers;
    public int MinPlayers;
    public string Description;
}

/// <summary>
/// 매칭 요청
/// </summary>
[Serializable]
public class MatchingRequest
{
    public MatchType MatchType;
    public GameMode GameMode;
    public DateTime RequestTime;
}

/// <summary>
/// 매칭 요청 데이터
/// </summary>
[Serializable]
public class MatchRequest
{
    public GameMode GameMode;
    public MatchType MatchType;
    public int PlayerLevel;
    public int PlayerRating;
    public DateTime RequestTime;
}

/// <summary>
/// 방 생성 요청
/// </summary>
[Serializable]
public class RoomCreationRequest
{
    public GameMode GameMode;
    public int MaxPlayers;
    public bool IsPrivate;
}

/// <summary>
/// 방 참여 요청
/// </summary>
[Serializable]
public class RoomJoinRequest
{
    public string RoomCode;
    public string PlayerId;
    public string PlayerName;
    public int PlayerLevel;
}

/// <summary>
/// 방 설정
/// </summary>
[Serializable]
public class RoomConfig
{
    public GameMode GameMode;
    public int MaxPlayers;
    public int MinPlayers;
    public bool IsPrivate;
    public string CreatorId;
    public string CreatorName;
    public int EnergyCost;
}

/// <summary>
/// 매치 발견 데이터
/// </summary>
[Serializable]
public class MatchFoundData
{
    public string RoomId;
    public GameMode GameMode;
    public int PlayerCount;
    public List<PlayerInfo> Players;
    public DateTime MatchTime;
}

/// <summary>
/// 방 생성 데이터
/// </summary>
[Serializable]
public class RoomCreatedData
{
    public string RoomCode;
    public string RoomId;
    public GameMode GameMode;
    public int MaxPlayers;
}

/// <summary>
/// 방 참여 데이터
/// </summary>
[Serializable]
public class RoomJoinedData
{
    public string RoomCode;
    public string RoomId;
    public GameMode GameMode;
    public int CurrentPlayers;
    public int MaxPlayers;
}

/// <summary>
/// 플레이어 수 데이터
/// </summary>
[Serializable]
public class PlayerCountData
{
    public int TotalOnlinePlayers;
    public Dictionary<GameMode, int> GameModePlayerCounts;
    public DateTime UpdateTime;
}

/// <summary>
/// 플레이어 정보
/// </summary>
[Serializable]
public class PlayerInfo
{
    public string PlayerId;
    public string PlayerName;
    public int PlayerLevel;
    public int PlayerRating;
}

/// <summary>
/// 매치 준비 메시지
/// </summary>
[Serializable]
public class MatchReadyMessage
{
    public MatchFoundData MatchData;
    public GameMode GameMode;
    public string RoomCode;
    public int PlayerCount;
}

/// <summary>
/// 사용자 상태 업데이트
/// </summary>
[Serializable]
public class UserStatusUpdate
{
    public bool IsOffline;
    public int Level;
    public int Energy;
    public DateTime UpdateTime;
}

/// <summary>
/// 매칭 상태 정보
/// </summary>
[Serializable]
public class MatchingStatusInfo
{
    public MatchingState CurrentState;
    public GameMode SelectedGameMode;
    public string CurrentRoomCode;
    public DateTime MatchStartTime;
    public TimeSpan ElapsedTime;
    public bool IsOfflineMode;
    public bool CanStartMatching;
    public bool CanCreateRoom;
}
#endregion

#region Manager Interfaces
/// <summary>
/// 매칭 매니저 인터페이스 (구현은 별도 클래스에서)
/// </summary>
public interface IMatchingManager
{
    event Action<MatchingState> OnMatchingStateChanged;
    event Action<MatchFoundData> OnMatchFound;
    event Action<string> OnMatchingError;
    
    void StartMatching(MatchRequest request);
    void CancelMatching();
    void StartServices();
    void StopServices();
    void UpdateConfig(MatchingConfig config);
    void Cleanup();
}

/// <summary>
/// 룸 매니저 인터페이스 (구현은 별도 클래스에서)
/// </summary>
public interface IRoomManager
{
    event Action<RoomCreatedData> OnRoomCreated;
    event Action<RoomJoinedData> OnRoomJoined;
    event Action<string> OnRoomError;
    
    void CreateRoom(RoomConfig config);
    void JoinRoom(RoomJoinRequest request);
    void StartServices();
    void StopServices();
    void Cleanup();
}

/// <summary>
/// 플레이어 수 매니저 인터페이스 (구현은 별도 클래스에서)
/// </summary>
public interface IPlayerCountManager
{
    event Action<PlayerCountData> OnPlayerCountUpdated;
    
    void RefreshPlayerCount();
    void ForceRefresh();
    void StartServices();
    void StopServices();
    void Cleanup();
}

/// <summary>
/// 매칭 매니저 임시 구현 (실제 구현은 별도 파일에서)
/// </summary>
public class MatchingManager : IMatchingManager
{
    public event Action<MatchingState> OnMatchingStateChanged;
    public event Action<MatchFoundData> OnMatchFound;
    public event Action<string> OnMatchingError;
    
    private MatchingConfig _config;
    private NetworkManager _networkManager;
    
    public MatchingManager(MatchingConfig config, NetworkManager networkManager)
    {
        _config = config;
        _networkManager = networkManager;
    }
    
    public void StartMatching(MatchRequest request) { /* Implementation */ }
    public void CancelMatching() { /* Implementation */ }
    public void StartServices() { /* Implementation */ }
    public void StopServices() { /* Implementation */ }
    public void UpdateConfig(MatchingConfig config) { _config = config; }
    public void Cleanup() { /* Implementation */ }
}

/// <summary>
/// 룸 매니저 임시 구현 (실제 구현은 별도 파일에서)
/// </summary>
public class RoomManager : IRoomManager
{
    public event Action<RoomCreatedData> OnRoomCreated;
    public event Action<RoomJoinedData> OnRoomJoined;
    public event Action<string> OnRoomError;
    
    private NetworkManager _networkManager;
    
    public RoomManager(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }
    
    public void CreateRoom(RoomConfig config) { /* Implementation */ }
    public void JoinRoom(RoomJoinRequest request) { /* Implementation */ }
    public void StartServices() { /* Implementation */ }
    public void StopServices() { /* Implementation */ }
    public void Cleanup() { /* Implementation */ }
}

/// <summary>
/// 플레이어 수 매니저 임시 구현 (실제 구현은 별도 파일에서)
/// </summary>
public class PlayerCountManager : IPlayerCountManager
{
    public event Action<PlayerCountData> OnPlayerCountUpdated;
    
    private NetworkManager _networkManager;
    
    public PlayerCountManager(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }
    
    public void RefreshPlayerCount() { /* Implementation */ }
    public void ForceRefresh() { /* Implementation */ }
    public void StartServices() { /* Implementation */ }
    public void StopServices() { /* Implementation */ }
    public void Cleanup() { /* Implementation */ }
}
#endregion