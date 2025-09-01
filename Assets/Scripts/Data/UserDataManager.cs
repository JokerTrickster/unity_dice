using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 사용자 데이터 관리자
/// 로컬 캐싱, 영구 저장, 서버 동기화를 담당하는 Singleton 클래스
/// </summary>
public class UserDataManager : MonoBehaviour
{
    #region Events
    /// <summary>
    /// 사용자 데이터가 로드될 때 발생하는 이벤트
    /// </summary>
    public static event Action<UserData> OnUserDataLoaded;
    
    /// <summary>
    /// 사용자 데이터가 업데이트될 때 발생하는 이벤트
    /// </summary>
    public static event Action<UserData> OnUserDataUpdated;
    
    /// <summary>
    /// 서버 동기화가 완료될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnSyncCompleted; // true: 성공, false: 실패
    
    /// <summary>
    /// 오프라인 모드로 전환될 때 발생하는 이벤트
    /// </summary>
    public static event Action<bool> OnOfflineModeChanged;
    #endregion

    #region Singleton
    private static UserDataManager _instance;
    public static UserDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UserDataManager");
                _instance = go.AddComponent<UserDataManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private Dictionary<string, UserData> _userCache = new();
    private UserData _currentUserData;
    private bool _isOfflineMode = false;
    private bool _isInitialized = false;
    private Coroutine _autoSaveCoroutine;
    private Coroutine _syncCoroutine;
    
    // 저장 키 상수
    private const string USER_DATA_PREFIX = "user_data_";
    private const string CURRENT_USER_KEY = "current_user_id";
    private const string OFFLINE_MODE_KEY = "offline_mode";
    private const string LAST_SYNC_KEY = "last_sync_time";
    private const string PENDING_CHANGES_KEY = "pending_changes";
    
    // 동기화 설정
    private const float AUTO_SAVE_INTERVAL = 30f; // 30초마다 자동 저장
    private const float SYNC_INTERVAL = 300f; // 5분마다 동기화 시도
    private const int MAX_RETRY_ATTEMPTS = 3;
    
    // 서버 설정 (실제 환경에서는 설정 파일에서 로드)
    private const string SERVER_BASE_URL = "https://api.unitydice.com/v1";
    #endregion

    #region Public Properties
    /// <summary>
    /// 현재 사용자 데이터
    /// </summary>
    public UserData CurrentUser => _currentUserData;
    
    /// <summary>
    /// 오프라인 모드 상태
    /// </summary>
    public bool IsOfflineMode => _isOfflineMode;
    
    /// <summary>
    /// 초기화 완료 상태
    /// </summary>
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 캐시된 사용자 수
    /// </summary>
    public int CachedUserCount => _userCache.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton 패턴 구현
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeAsync());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        SaveCurrentUserData();
        
        // 이벤트 구독 해제
        OnUserDataLoaded = null;
        OnUserDataUpdated = null;
        OnSyncCompleted = null;
        OnOfflineModeChanged = null;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 비동기 초기화
    /// </summary>
    private IEnumerator InitializeAsync()
    {
        Debug.Log("[UserDataManager] Initializing...");
        
        // 오프라인 모드 설정 로드
        _isOfflineMode = PlayerPrefs.GetInt(OFFLINE_MODE_KEY, 0) == 1;
        
        // 현재 사용자 로드
        yield return LoadCurrentUser();
        
        // 자동 저장 및 동기화 시작
        StartAutoSave();
        if (!_isOfflineMode)
        {
            StartPeriodicSync();
        }
        
        _isInitialized = true;
        Debug.Log($"[UserDataManager] Initialization complete. Offline mode: {_isOfflineMode}");
    }

    /// <summary>
    /// 현재 사용자 로드
    /// </summary>
    private IEnumerator LoadCurrentUser()
    {
        string currentUserId = PlayerPrefs.GetString(CURRENT_USER_KEY, "");
        
        if (!string.IsNullOrEmpty(currentUserId))
        {
            _currentUserData = LoadUserFromLocal(currentUserId);
            
            if (_currentUserData != null)
            {
                _userCache[currentUserId] = _currentUserData;
                OnUserDataLoaded?.Invoke(_currentUserData);
                Debug.Log($"[UserDataManager] Loaded user: {_currentUserData.DisplayName}");
            }
        }
        
        yield return null;
    }
    #endregion

    #region User Data Management
    /// <summary>
    /// 사용자 설정 (로그인 시 호출)
    /// </summary>
    public void SetCurrentUser(string userId, string userName, string email = "")
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[UserDataManager] User ID cannot be null or empty");
            return;
        }

        // 기존 사용자 데이터 확인
        UserData userData = GetUserData(userId);
        
        if (userData == null)
        {
            // 새 사용자 생성
            userData = new UserData
            {
                UserId = userId,
                DisplayName = userName,
                Email = email,
                CreatedAt = DateTime.Now,
                LastLoginAt = DateTime.Now,
                IsNewUser = true
            };
        }
        else
        {
            // 기존 사용자 정보 업데이트
            userData.DisplayName = userName;
            userData.Email = email;
            userData.LastLoginAt = DateTime.Now;
            userData.IsNewUser = false;
        }

        _currentUserData = userData;
        _userCache[userId] = userData;
        
        // 현재 사용자 ID 저장
        PlayerPrefs.SetString(CURRENT_USER_KEY, userId);
        PlayerPrefs.Save();
        
        SaveUserToLocal(userData);
        
        OnUserDataUpdated?.Invoke(_currentUserData);
        Debug.Log($"[UserDataManager] Set current user: {userData.DisplayName}");
    }

    /// <summary>
    /// 사용자 데이터 가져오기
    /// </summary>
    public UserData GetUserData(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        // 캐시에서 확인
        if (_userCache.TryGetValue(userId, out UserData cachedData))
            return cachedData;

        // 로컬에서 로드
        UserData userData = LoadUserFromLocal(userId);
        if (userData != null)
        {
            _userCache[userId] = userData;
        }

        return userData;
    }

    /// <summary>
    /// 사용자 데이터 업데이트
    /// </summary>
    public void UpdateUserData(UserData userData)
    {
        if (userData == null || string.IsNullOrEmpty(userData.UserId))
        {
            Debug.LogError("[UserDataManager] Invalid user data for update");
            return;
        }

        // 유효성 검증
        if (!ValidateUserData(userData))
        {
            Debug.LogError("[UserDataManager] User data validation failed");
            return;
        }

        userData.UpdatedAt = DateTime.Now;
        _userCache[userData.UserId] = userData;
        
        if (_currentUserData?.UserId == userData.UserId)
        {
            _currentUserData = userData;
        }

        SaveUserToLocal(userData);
        OnUserDataUpdated?.Invoke(userData);
        
        // 서버 동기화 (오프라인 모드가 아닌 경우)
        if (!_isOfflineMode)
        {
            StartCoroutine(SyncUserToServer(userData));
        }
        
        Debug.Log($"[UserDataManager] Updated user data: {userData.DisplayName}");
    }

    /// <summary>
    /// 현재 사용자 데이터 업데이트 (편의 메서드)
    /// </summary>
    public void UpdateCurrentUser(UserData userData)
    {
        if (userData == null)
        {
            Debug.LogError("[UserDataManager] Cannot update with null user data");
            return;
        }
        
        if (_currentUserData == null)
        {
            Debug.LogWarning("[UserDataManager] No current user to update");
            return;
        }
        
        if (_currentUserData.UserId != userData.UserId)
        {
            Debug.LogWarning($"[UserDataManager] User ID mismatch: current={_currentUserData.UserId}, provided={userData.UserId}");
            return;
        }
        
        UpdateUserData(userData);
    }

    /// <summary>
    /// 현재 사용자 로그아웃
    /// </summary>
    public void LogoutCurrentUser()
    {
        if (_currentUserData != null)
        {
            SaveUserToLocal(_currentUserData);
            Debug.Log($"[UserDataManager] Logged out user: {_currentUserData.DisplayName}");
        }

        _currentUserData = null;
        PlayerPrefs.DeleteKey(CURRENT_USER_KEY);
        PlayerPrefs.Save();
    }
    #endregion

    #region Local Storage
    /// <summary>
    /// 사용자 데이터를 로컬에 저장
    /// </summary>
    private void SaveUserToLocal(UserData userData)
    {
        if (userData == null) return;

        try
        {
            string jsonData = JsonUtility.ToJson(userData);
            string encryptedData = EncryptData(jsonData);
            PlayerPrefs.SetString(USER_DATA_PREFIX + userData.UserId, encryptedData);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDataManager] Failed to save user data: {e.Message}");
        }
    }

    /// <summary>
    /// 로컬에서 사용자 데이터 로드
    /// </summary>
    private UserData LoadUserFromLocal(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        try
        {
            string encryptedData = PlayerPrefs.GetString(USER_DATA_PREFIX + userId, "");
            if (string.IsNullOrEmpty(encryptedData)) return null;

            string jsonData = DecryptData(encryptedData);
            return JsonUtility.FromJson<UserData>(jsonData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDataManager] Failed to load user data for {userId}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 현재 사용자 데이터 저장
    /// </summary>
    private void SaveCurrentUserData()
    {
        if (_currentUserData != null)
        {
            SaveUserToLocal(_currentUserData);
        }
    }
    #endregion

    #region Server Synchronization
    /// <summary>
    /// 서버와 동기화
    /// </summary>
    public void SyncWithServer()
    {
        if (_isOfflineMode)
        {
            Debug.LogWarning("[UserDataManager] Cannot sync in offline mode");
            return;
        }

        if (_currentUserData == null)
        {
            Debug.LogWarning("[UserDataManager] No current user to sync");
            return;
        }

        StartCoroutine(SyncUserToServer(_currentUserData));
    }

    /// <summary>
    /// 사용자 데이터를 서버에 동기화
    /// </summary>
    private IEnumerator SyncUserToServer(UserData userData)
    {
        Debug.Log($"[UserDataManager] Syncing user to server: {userData.UserId}");

        string jsonData = JsonUtility.ToJson(userData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest($"{SERVER_BASE_URL}/users/{userData.UserId}", "PUT");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        // TODO: 실제 환경에서는 인증 토큰 추가
        // request.SetRequestHeader("Authorization", $"Bearer {authToken}");

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        
        if (success)
        {
            PlayerPrefs.SetString(LAST_SYNC_KEY, DateTime.Now.ToBinary().ToString());
            Debug.Log($"[UserDataManager] Successfully synced user: {userData.UserId}");
        }
        else
        {
            Debug.LogError($"[UserDataManager] Failed to sync user: {request.error}");
            // 오프라인 모드로 전환 고려
            if (request.responseCode == 0) // 네트워크 연결 없음
            {
                SetOfflineMode(true);
            }
        }

        OnSyncCompleted?.Invoke(success);
    }

    /// <summary>
    /// 서버에서 사용자 데이터 로드
    /// </summary>
    private IEnumerator LoadUserFromServer(string userId)
    {
        UnityWebRequest request = UnityWebRequest.Get($"{SERVER_BASE_URL}/users/{userId}");
        
        // TODO: 인증 토큰 추가
        // request.SetRequestHeader("Authorization", $"Bearer {authToken}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                UserData serverData = JsonUtility.FromJson<UserData>(request.downloadHandler.text);
                if (serverData != null && ValidateUserData(serverData))
                {
                    _userCache[userId] = serverData;
                    SaveUserToLocal(serverData);
                    Debug.Log($"[UserDataManager] Loaded user from server: {serverData.DisplayName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UserDataManager] Failed to parse server data: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"[UserDataManager] Failed to load user from server: {request.error}");
        }
    }
    #endregion

    #region Auto Save & Sync
    /// <summary>
    /// 자동 저장 시작
    /// </summary>
    private void StartAutoSave()
    {
        if (_autoSaveCoroutine != null)
            StopCoroutine(_autoSaveCoroutine);
        
        _autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    /// <summary>
    /// 자동 저장 코루틴
    /// </summary>
    private IEnumerator AutoSaveCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(AUTO_SAVE_INTERVAL);
            
            if (_currentUserData != null)
            {
                SaveUserToLocal(_currentUserData);
            }
        }
    }

    /// <summary>
    /// 주기적 동기화 시작
    /// </summary>
    private void StartPeriodicSync()
    {
        if (_syncCoroutine != null)
            StopCoroutine(_syncCoroutine);
        
        _syncCoroutine = StartCoroutine(PeriodicSyncCoroutine());
    }

    /// <summary>
    /// 주기적 동기화 코루틴
    /// </summary>
    private IEnumerator PeriodicSyncCoroutine()
    {
        while (!_isOfflineMode)
        {
            yield return new WaitForSeconds(SYNC_INTERVAL);
            
            if (_currentUserData != null)
            {
                yield return SyncUserToServer(_currentUserData);
            }
        }
    }
    #endregion

    #region Offline Mode
    /// <summary>
    /// 오프라인 모드 설정
    /// </summary>
    public void SetOfflineMode(bool offline)
    {
        if (_isOfflineMode == offline) return;

        _isOfflineMode = offline;
        PlayerPrefs.SetInt(OFFLINE_MODE_KEY, offline ? 1 : 0);
        PlayerPrefs.Save();

        if (offline)
        {
            if (_syncCoroutine != null)
            {
                StopCoroutine(_syncCoroutine);
                _syncCoroutine = null;
            }
        }
        else
        {
            StartPeriodicSync();
        }

        OnOfflineModeChanged?.Invoke(_isOfflineMode);
        Debug.Log($"[UserDataManager] Offline mode: {_isOfflineMode}");
    }
    #endregion

    #region Data Validation
    /// <summary>
    /// 사용자 데이터 유효성 검증
    /// </summary>
    private bool ValidateUserData(UserData userData)
    {
        if (userData == null)
            return false;

        if (string.IsNullOrEmpty(userData.UserId))
            return false;

        if (string.IsNullOrEmpty(userData.DisplayName))
            return false;

        if (userData.DisplayName.Length > 50)
        {
            Debug.LogWarning("[UserDataManager] Display name too long");
            return false;
        }

        if (!string.IsNullOrEmpty(userData.Email) && !IsValidEmail(userData.Email))
        {
            Debug.LogWarning("[UserDataManager] Invalid email format");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 이메일 형식 검증
    /// </summary>
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Encryption
    /// <summary>
    /// 데이터 암호화 (간단한 XOR 암호화)
    /// </summary>
    private string EncryptData(string data)
    {
        if (string.IsNullOrEmpty(data)) return data;

        char[] chars = data.ToCharArray();
        const int key = 129;

        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(chars[i] ^ key);
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string(chars)));
    }

    /// <summary>
    /// 데이터 복호화
    /// </summary>
    private string DecryptData(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData)) return encryptedData;

        try
        {
            byte[] bytes = Convert.FromBase64String(encryptedData);
            char[] chars = System.Text.Encoding.UTF8.GetString(bytes).ToCharArray();
            const int key = 129;

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)(chars[i] ^ key);
            }

            return new string(chars);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDataManager] Decryption failed: {e.Message}");
            return "";
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 캐시 클리어
    /// </summary>
    public void ClearCache()
    {
        _userCache.Clear();
        Debug.Log("[UserDataManager] Cache cleared");
    }

    /// <summary>
    /// 모든 사용자 데이터 삭제 (로컬만)
    /// </summary>
    public void ClearAllLocalData()
    {
        _userCache.Clear();
        _currentUserData = null;
        
        // PlayerPrefs에서 모든 사용자 데이터 삭제
        var keys = new List<string>();
        // PlayerPrefs는 모든 키를 나열하는 방법이 없으므로, 패턴에 맞는 키들을 수동으로 관리해야 함
        // 실제 구현에서는 사용자 ID 목록을 별도로 저장하는 것이 좋음
        
        PlayerPrefs.DeleteKey(CURRENT_USER_KEY);
        PlayerPrefs.DeleteKey(OFFLINE_MODE_KEY);
        PlayerPrefs.DeleteKey(LAST_SYNC_KEY);
        PlayerPrefs.Save();
        
        Debug.Log("[UserDataManager] All local data cleared");
    }

    /// <summary>
    /// 마지막 동기화 시간 가져오기
    /// </summary>
    public DateTime GetLastSyncTime()
    {
        string syncTimeStr = PlayerPrefs.GetString(LAST_SYNC_KEY, "");
        if (string.IsNullOrEmpty(syncTimeStr))
            return DateTime.MinValue;

        try
        {
            long syncTimeBinary = Convert.ToInt64(syncTimeStr);
            return DateTime.FromBinary(syncTimeBinary);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
    #endregion
}

/// <summary>
/// 사용자 데이터 구조체
/// </summary>
[System.Serializable]
public class UserData
{
    public string UserId;
    public string DisplayName;
    public string Email;
    public DateTime CreatedAt;
    public DateTime UpdatedAt;
    public DateTime LastLoginAt;
    public bool IsNewUser;
    
    // 게임 관련 데이터
    public int Level = 1;
    public int Experience = 0;
    public int TotalGamesPlayed = 0;
    public int GamesWon = 0;
    public int GamesLost = 0;
    
    // 프로필 관련 데이터
    public string Title = ""; // 사용자 타이틀/칭호
    public string AvatarUrl = ""; // 프로필 이미지 URL
    public int Ranking = -1; // 현재 랭킹 (-1 = 랭킹 없음)
    
    // 에너지/스태미나 시스템
    public int CurrentEnergy = 100;
    public int MaxEnergy = 100;
    public DateTime LastEnergyRechargeTime = DateTime.Now;
    public int EnergyRechargeRate = 1; // 충전 주기마다 회복되는 에너지량
    public TimeSpan EnergyRechargeInterval = TimeSpan.FromMinutes(10); // 에너지 충전 간격
    
    // 설정
    public bool SoundEnabled = true;
    public bool MusicEnabled = true;
    public float MasterVolume = 1.0f;
    public string PreferredLanguage = "en";
    
    // 통계 계산 프로퍼티
    public float WinRate => TotalGamesPlayed > 0 ? (float)GamesWon / TotalGamesPlayed * 100f : 0f;
    public int TotalExperience => (Level - 1) * 1000 + Experience; // 레벨당 1000 경험치
    
    // 에너지 관련 계산 프로퍼티
    public float EnergyPercentage => MaxEnergy > 0 ? (float)CurrentEnergy / MaxEnergy : 0f;
    public bool IsEnergyLow => EnergyPercentage <= 0.2f; // 20% 이하면 부족
    public bool IsEnergyFull => CurrentEnergy >= MaxEnergy;
    public bool CanUseEnergy => CurrentEnergy > 0;
    public TimeSpan TimeUntilNextRecharge
    {
        get
        {
            var timeSinceLastRecharge = DateTime.Now - LastEnergyRechargeTime;
            var timeToNext = EnergyRechargeInterval - timeSinceLastRecharge;
            return timeToNext.TotalSeconds > 0 ? timeToNext : TimeSpan.Zero;
        }
    }
}