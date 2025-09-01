using UnityEngine;

/// <summary>
/// ProfileSection 사용 예제
/// 실제 게임에서 ProfileSection을 어떻게 설정하고 사용하는지 보여주는 예제입니다.
/// </summary>
public class ProfileSectionExample : MonoBehaviour
{
    [Header("Profile Section Setup")]
    [SerializeField] private GameObject profileSectionPrefab;
    [SerializeField] private Transform profileSectionParent;
    
    private ProfileSection _profileSection;
    private MainPageManager _mainPageManager;

    #region Unity Lifecycle
    private void Start()
    {
        // MainPageManager 준비 대기 후 설정
        StartCoroutine(SetupProfileSectionWhenReady());
    }
    
    private void OnDestroy()
    {
        // ProfileSection 정리
        if (_profileSection != null)
        {
            _mainPageManager?.UnregisterSection(MainPageSectionType.Profile);
        }
    }
    #endregion

    #region Setup Methods
    /// <summary>
    /// MainPageManager가 준비될 때까지 대기 후 ProfileSection 설정
    /// </summary>
    private System.Collections.IEnumerator SetupProfileSectionWhenReady()
    {
        // MainPageManager 초기화 대기
        while (MainPageManager.Instance == null || !MainPageManager.Instance.IsInitialized)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        _mainPageManager = MainPageManager.Instance;
        
        // ProfileSection 생성 및 설정
        SetupProfileSection();
        
        Debug.Log("[ProfileSectionExample] Profile section setup completed");
    }
    
    /// <summary>
    /// ProfileSection 생성 및 등록
    /// </summary>
    private void SetupProfileSection()
    {
        // 1. ProfileSection 게임 오브젝트 생성
        GameObject profileSectionObject;
        
        if (profileSectionPrefab != null)
        {
            // Prefab이 있는 경우 인스턴스화
            profileSectionObject = Instantiate(profileSectionPrefab, profileSectionParent);
        }
        else
        {
            // Prefab이 없는 경우 빈 오브젝트 생성
            profileSectionObject = new GameObject("ProfileSection");
            profileSectionObject.transform.SetParent(profileSectionParent);
        }
        
        // 2. ProfileSection 컴포넌트 추가 (없는 경우)
        _profileSection = profileSectionObject.GetComponent<ProfileSection>();
        if (_profileSection == null)
        {
            _profileSection = profileSectionObject.AddComponent<ProfileSection>();
        }
        
        // 3. ProfileSectionUI 설정 확인
        var profileSectionUI = profileSectionObject.GetComponentInChildren<ProfileSectionUI>();
        if (profileSectionUI == null)
        {
            Debug.LogWarning("[ProfileSectionExample] ProfileSectionUI component not found. Creating basic UI structure.");
            CreateBasicUIStructure(profileSectionObject);
        }
        
        // 4. MainPageManager에 섹션 등록
        _mainPageManager.RegisterSection(MainPageSectionType.Profile, _profileSection);
        
        Debug.Log("[ProfileSectionExample] ProfileSection registered successfully");
    }
    
    /// <summary>
    /// 기본 UI 구조 생성 (Prefab이 없는 경우)
    /// </summary>
    private void CreateBasicUIStructure(GameObject parentObject)
    {
        // UI 자식 오브젝트 생성
        var uiObject = new GameObject("ProfileSectionUI");
        uiObject.transform.SetParent(parentObject.transform);
        
        // ProfileSectionUI 컴포넌트 추가
        var profileSectionUI = uiObject.AddComponent<ProfileSectionUI>();
        
        // 기본 UI 컴포넌트들 추가 (실제로는 Prefab에서 설정되어야 함)
        // 여기서는 예제이므로 기본 구조만 생성
        var rectTransform = uiObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = uiObject.AddComponent<RectTransform>();
        }
        
        // RectTransform 기본 설정
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        Debug.Log("[ProfileSectionExample] Basic UI structure created");
    }
    #endregion

    #region Public Methods (Example Usage)
    /// <summary>
    /// 프로필 섹션 활성화 (외부에서 호출 가능)
    /// </summary>
    public void ActivateProfileSection()
    {
        if (_profileSection != null)
        {
            _mainPageManager?.SetSectionActive(MainPageSectionType.Profile, true);
            Debug.Log("[ProfileSectionExample] Profile section activated");
        }
        else
        {
            Debug.LogWarning("[ProfileSectionExample] Profile section not ready");
        }
    }
    
    /// <summary>
    /// 프로필 섹션 비활성화 (외부에서 호출 가능)
    /// </summary>
    public void DeactivateProfileSection()
    {
        if (_profileSection != null)
        {
            _mainPageManager?.SetSectionActive(MainPageSectionType.Profile, false);
            Debug.Log("[ProfileSectionExample] Profile section deactivated");
        }
    }
    
    /// <summary>
    /// 프로필 데이터 강제 새로고침 (외부에서 호출 가능)
    /// </summary>
    public void RefreshProfileData()
    {
        if (_profileSection != null)
        {
            _profileSection.ForceRefresh();
            Debug.Log("[ProfileSectionExample] Profile data refreshed");
        }
        else
        {
            Debug.LogWarning("[ProfileSectionExample] Profile section not available");
        }
    }
    
    /// <summary>
    /// 프로필 정보 업데이트 예제 (외부에서 호출 가능)
    /// </summary>
    public void UpdateProfileExample(string newDisplayName = "Updated Player")
    {
        if (_profileSection != null)
        {
            _profileSection.UpdateProfile(newDisplayName, "New Title");
            Debug.Log($"[ProfileSectionExample] Profile updated to: {newDisplayName}");
        }
        else
        {
            Debug.LogWarning("[ProfileSectionExample] Profile section not available");
        }
    }
    
    /// <summary>
    /// 프로필 섹션 상태 정보 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Print Profile Section Status")]
    public void PrintProfileSectionStatus()
    {
        if (_profileSection != null)
        {
            var status = _profileSection.GetProfileSectionStatus();
            Debug.Log($"[ProfileSectionExample] Profile Section Status:\n" +
                     $"- Initialized: {status.BaseStatus.IsInitialized}\n" +
                     $"- Active: {status.BaseStatus.IsActive}\n" +
                     $"- Current User: {status.CurrentUserId}\n" +
                     $"- Has Profile Picture: {status.HasProfilePicture}\n" +
                     $"- Data Synced: {status.IsDataSynced}\n" +
                     $"- Last Update: {status.LastDataUpdate}");
        }
        else
        {
            Debug.LogWarning("[ProfileSectionExample] Profile section not available");
        }
    }
    #endregion

    #region Event Handlers (Example)
    private void OnEnable()
    {
        // ProfileSection 이벤트 구독 예제
        SectionBase.OnSectionInitialized += OnSectionInitialized;
        SectionBase.OnSectionActivated += OnSectionActivated;
        SectionBase.OnSectionDeactivated += OnSectionDeactivated;
        SectionBase.OnSectionError += OnSectionError;
    }
    
    private void OnDisable()
    {
        // 이벤트 구독 해제
        SectionBase.OnSectionInitialized -= OnSectionInitialized;
        SectionBase.OnSectionActivated -= OnSectionActivated;
        SectionBase.OnSectionDeactivated -= OnSectionDeactivated;
        SectionBase.OnSectionError -= OnSectionError;
    }
    
    private void OnSectionInitialized(MainPageSectionType sectionType)
    {
        if (sectionType == MainPageSectionType.Profile)
        {
            Debug.Log("[ProfileSectionExample] Profile section initialized event received");
        }
    }
    
    private void OnSectionActivated(MainPageSectionType sectionType)
    {
        if (sectionType == MainPageSectionType.Profile)
        {
            Debug.Log("[ProfileSectionExample] Profile section activated event received");
        }
    }
    
    private void OnSectionDeactivated(MainPageSectionType sectionType)
    {
        if (sectionType == MainPageSectionType.Profile)
        {
            Debug.Log("[ProfileSectionExample] Profile section deactivated event received");
        }
    }
    
    private void OnSectionError(MainPageSectionType sectionType, string errorMessage)
    {
        if (sectionType == MainPageSectionType.Profile)
        {
            Debug.LogError($"[ProfileSectionExample] Profile section error: {errorMessage}");
        }
    }
    #endregion
}