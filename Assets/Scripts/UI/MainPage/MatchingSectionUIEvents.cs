using System;
using UnityEngine;

/// <summary>
/// MatchingSectionUI에서 사용할 이벤트 및 UI 컴포넌트 확장
/// MatchingSection과의 통신을 위한 이벤트들을 정의합니다.
/// </summary>
public static class MatchingSectionUIEvents
{
    // Events that MatchingSectionUI can trigger for MatchingSection
    public static event Action<MatchingRequest> OnMatchingRequested;
    public static event Action<RoomCreationRequest> OnRoomCreationRequested;
    public static event Action<RoomJoinRequest> OnRoomJoinRequested;
    public static event Action OnMatchCancelRequested;
    public static event Action<GameMode> OnGameModeSelected;

    public static void TriggerMatchingRequest(MatchingRequest request)
    {
        OnMatchingRequested?.Invoke(request);
    }

    public static void TriggerRoomCreationRequest(RoomCreationRequest request)
    {
        OnRoomCreationRequested?.Invoke(request);
    }

    public static void TriggerRoomJoinRequest(RoomJoinRequest request)
    {
        OnRoomJoinRequested?.Invoke(request);
    }

    public static void TriggerMatchCancelRequest()
    {
        OnMatchCancelRequested?.Invoke();
    }

    public static void TriggerGameModeSelection(GameMode gameMode)
    {
        OnGameModeSelected?.Invoke(gameMode);
    }

    public static void ClearEvents()
    {
        OnMatchingRequested = null;
        OnRoomCreationRequested = null;
        OnRoomJoinRequested = null;
        OnMatchCancelRequested = null;
        OnGameModeSelected = null;
    }
}

/// <summary>
/// MatchingSectionUI용 확장 메서드들
/// 기존 MatchingSectionUI에 새로운 기능을 추가하기 위한 확장입니다.
/// </summary>
public static class MatchingSectionUIExtensions
{
    /// <summary>
    /// MatchingSectionUI에 새로운 이벤트 핸들러 추가
    /// </summary>
    public static void BindToMatchingSection(this MatchingSectionUI ui, MatchingSection matchingSection)
    {
        if (ui == null || matchingSection == null) return;

        // Connect UI events to MatchingSection
        MatchingSectionUIEvents.OnMatchingRequested += matchingSection.HandleMatchingRequest;
        MatchingSectionUIEvents.OnRoomCreationRequested += matchingSection.HandleRoomCreationRequest;
        MatchingSectionUIEvents.OnRoomJoinRequested += matchingSection.HandleRoomJoinRequest;
        MatchingSectionUIEvents.OnMatchCancelRequested += matchingSection.HandleMatchCancelRequest;
        MatchingSectionUIEvents.OnGameModeSelected += matchingSection.HandleGameModeSelection;
    }

    /// <summary>
    /// MatchingSection에서 UI 업데이트를 위한 확장 메서드들
    /// </summary>
    public static void UpdateUserInfo(this MatchingSectionUI ui, string displayName, int level, int currentEnergy, int maxEnergy)
    {
        // This would be implemented to update user info display in the UI
        Debug.Log($"[MatchingSectionUI] Updating user info: {displayName} (Level {level}) - Energy: {currentEnergy}/{maxEnergy}");
    }

    public static void SetMatchingState(this MatchingSectionUI ui, MatchingState state)
    {
        // This would be implemented to update the matching state in the UI
        Debug.Log($"[MatchingSectionUI] Setting matching state: {state}");
    }

    public static void ShowMessage(this MatchingSectionUI ui, string message, MessageType type = MessageType.Info)
    {
        // This would be implemented to show messages in the UI
        Debug.Log($"[MatchingSectionUI] Showing message ({type}): {message}");
    }

    public static void SetButtonsInteractable(this MatchingSectionUI ui, bool interactable)
    {
        // This would be implemented to enable/disable buttons
        Debug.Log($"[MatchingSectionUI] Setting buttons interactable: {interactable}");
    }

    public static void ShowMatchingProgress(this MatchingSectionUI ui, bool show)
    {
        // This would be implemented to show/hide matching progress
        Debug.Log($"[MatchingSectionUI] Show matching progress: {show}");
    }

    public static void UpdateGameModeInfo(this MatchingSectionUI ui, GameMode gameMode, MatchConfig config)
    {
        // This would be implemented to update game mode information
        Debug.Log($"[MatchingSectionUI] Updating game mode info: {gameMode} - {config.DisplayName}");
    }

    public static void UpdateMatchingProgress(this MatchingSectionUI ui, TimeSpan elapsed, TimeSpan estimated)
    {
        // This would be implemented to update matching progress
        Debug.Log($"[MatchingSectionUI] Updating matching progress: {elapsed:mm\\:ss} / {estimated:mm\\:ss}");
    }

    public static void SetGameModeAvailable(this MatchingSectionUI ui, GameMode gameMode, bool available)
    {
        // This would be implemented to set game mode availability
        Debug.Log($"[MatchingSectionUI] Setting {gameMode} available: {available}");
    }

    public static void UpdatePlayerCount(this MatchingSectionUI ui, int count)
    {
        // This would be implemented to update player count display
        Debug.Log($"[MatchingSectionUI] Updating player count: {count}");
    }

    public static void UpdateEstimatedWaitTimes(this MatchingSectionUI ui, Dictionary<GameMode, int> gameModePlayerCounts)
    {
        // This would be implemented to update estimated wait times based on player counts
        Debug.Log($"[MatchingSectionUI] Updating estimated wait times for {gameModePlayerCounts.Count} game modes");
    }

    public static void SetOfflineMode(this MatchingSectionUI ui, bool isOffline)
    {
        // This would be implemented to handle offline mode UI changes
        Debug.Log($"[MatchingSectionUI] Setting offline mode: {isOffline}");
    }

    public static void ForceRefresh(this MatchingSectionUI ui)
    {
        // This would be implemented to force refresh the UI
        Debug.Log("[MatchingSectionUI] Force refreshing UI");
    }
}

/// <summary>
/// UI 컴포넌트와 MatchingSection 간의 어댑터 클래스
/// 기존 MatchingSectionUI를 새로운 MatchingSection과 연동하기 위한 어댑터입니다.
/// </summary>
[RequireComponent(typeof(MatchingSectionUI))]
public class MatchingSectionUIAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchingSectionUI matchingUI;
    [SerializeField] private MatchingSection matchingSection;
    
    private void Awake()
    {
        if (matchingUI == null)
            matchingUI = GetComponent<MatchingSectionUI>();
            
        if (matchingSection == null)
            matchingSection = GetComponent<MatchingSection>();
    }

    private void Start()
    {
        if (matchingUI != null && matchingSection != null)
        {
            // Bind UI to MatchingSection
            matchingUI.BindToMatchingSection(matchingSection);
            
            // Set up the reference in MatchingSection
            if (matchingSection.matchingUI == null)
            {
                var field = typeof(MatchingSection).GetField("matchingUI", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(matchingSection, matchingUI);
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up events
        MatchingSectionUIEvents.ClearEvents();
    }

    /// <summary>
    /// UI에서 매칭 요청이 발생했을 때 호출
    /// </summary>
    public void OnUIMatchingRequest(MatchType matchType, GameMode gameMode)
    {
        var request = new MatchingRequest
        {
            MatchType = matchType,
            GameMode = gameMode,
            RequestTime = DateTime.Now
        };
        
        MatchingSectionUIEvents.TriggerMatchingRequest(request);
    }

    /// <summary>
    /// UI에서 방 생성 요청이 발생했을 때 호출
    /// </summary>
    public void OnUIRoomCreationRequest(GameMode gameMode, int maxPlayers, bool isPrivate)
    {
        var request = new RoomCreationRequest
        {
            GameMode = gameMode,
            MaxPlayers = maxPlayers,
            IsPrivate = isPrivate
        };
        
        MatchingSectionUIEvents.TriggerRoomCreationRequest(request);
    }

    /// <summary>
    /// UI에서 방 참여 요청이 발생했을 때 호출
    /// </summary>
    public void OnUIRoomJoinRequest(string roomCode)
    {
        var request = new RoomJoinRequest
        {
            RoomCode = roomCode
        };
        
        MatchingSectionUIEvents.TriggerRoomJoinRequest(request);
    }

    /// <summary>
    /// UI에서 매칭 취소 요청이 발생했을 때 호출
    /// </summary>
    public void OnUIMatchCancelRequest()
    {
        MatchingSectionUIEvents.TriggerMatchCancelRequest();
    }

    /// <summary>
    /// UI에서 게임 모드 선택이 발생했을 때 호출
    /// </summary>
    public void OnUIGameModeSelection(GameMode gameMode)
    {
        MatchingSectionUIEvents.TriggerGameModeSelection(gameMode);
    }
}