using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 선물 처리 핸들러
/// 피로도 선물 메시지의 수령, 중복 방지, EnergyManager 통합을 담당
/// </summary>
public class EnergyGiftHandler : IMailboxMessageHandler
{
    #region Events
    /// <summary>
    /// 에너지 선물 수령 완료 시 발생
    /// </summary>
    public static event Action<string, int> OnEnergyGiftClaimed; // messageId, energyAmount
    
    /// <summary>
    /// 에너지 선물 수령 실패 시 발생
    /// </summary>
    public static event Action<string, string> OnEnergyGiftClaimFailed; // messageId, error
    #endregion
    
    #region Private Fields
    private static HashSet<string> _claimedGifts = new HashSet<string>();
    private static readonly object _lockObject = new object();
    
    // PlayerPrefs 키
    private const string CLAIMED_GIFTS_KEY = "claimed_energy_gifts";
    private const string CLAIMED_GIFTS_TIMESTAMP_KEY = "claimed_gifts_timestamp";
    
    // 중복 방지 설정
    private const int CLAIMED_GIFTS_RETENTION_DAYS = 30; // 30일간 중복 방지 기록 유지
    
    // API 엔드포인트
    private const string API_CLAIM_ENDPOINT = "/api/mailbox/claim";
    #endregion
    
    #region Initialization
    static EnergyGiftHandler()
    {
        LoadClaimedGifts();
    }
    #endregion
    
    #region IMailboxMessageHandler Implementation
    /// <summary>
    /// 에너지 선물 메시지 처리
    /// </summary>
    public void HandleMessage(MailboxMessage message, MailboxManager manager)
    {
        if (message == null || manager == null)
        {
            Debug.LogError("[EnergyGiftHandler] Invalid parameters");
            return;
        }
        
        if (message.type != MailMessageType.EnergyGift)
        {
            Debug.LogWarning($"[EnergyGiftHandler] Wrong message type: {message.type}");
            return;
        }
        
        if (!message.IsEnergyGift())
        {
            Debug.LogError($"[EnergyGiftHandler] Message is not a valid energy gift: {message.messageId}");
            OnEnergyGiftClaimFailed?.Invoke(message.messageId, "유효하지 않은 에너지 선물입니다");
            return;
        }
        
        // 에너지 선물 처리
        manager.StartCoroutine(ProcessEnergyGift(message, manager));
    }
    #endregion
    
    #region Energy Gift Processing
    /// <summary>
    /// 에너지 선물 처리 코루틴
    /// </summary>
    private IEnumerator ProcessEnergyGift(MailboxMessage message, MailboxManager manager)
    {
        string messageId = message.messageId;
        string giftId = message.GetGiftId();
        int energyAmount = message.GetEnergyAmount();
        
        Debug.Log($"[EnergyGiftHandler] Processing energy gift: {messageId}, Amount: {energyAmount}");
        
        // 1. 중복 수령 확인
        if (IsGiftAlreadyClaimed(giftId))
        {
            string error = "이미 받은 선물입니다";
            Debug.LogWarning($"[EnergyGiftHandler] Gift already claimed: {giftId}");
            OnEnergyGiftClaimFailed?.Invoke(messageId, error);
            yield break;
        }
        
        // 2. 에너지 양 유효성 검증
        if (energyAmount <= 0)
        {
            string error = "유효하지 않은 에너지 양입니다";
            Debug.LogError($"[EnergyGiftHandler] Invalid energy amount: {energyAmount}");
            OnEnergyGiftClaimFailed?.Invoke(messageId, error);
            yield break;
        }
        
        // 3. 최대 에너지 초과 확인 (EnergyManager가 있는 경우)
        if (!CanReceiveEnergy(energyAmount))
        {
            string error = "최대 에너지를 초과합니다";
            Debug.LogWarning($"[EnergyGiftHandler] Would exceed max energy: {energyAmount}");
            OnEnergyGiftClaimFailed?.Invoke(messageId, error);
            yield break;
        }
        
        // 4. 서버에 선물 수령 요청
        yield return StartCoroutine(ClaimEnergyGiftOnServer(messageId, giftId, energyAmount, manager));
    }
    
    /// <summary>
    /// 서버에 에너지 선물 수령 요청
    /// </summary>
    private IEnumerator ClaimEnergyGiftOnServer(string messageId, string giftId, int energyAmount, MailboxManager manager)
    {
        bool requestCompleted = false;
        bool success = false;
        string errorMessage = "";
        
        var claimData = new
        {
            messageId = messageId,
            giftId = giftId,
            energyAmount = energyAmount
        };
        
        NetworkManager.Instance.Post(API_CLAIM_ENDPOINT, claimData, (response) =>
        {
            requestCompleted = true;
            success = response.IsSuccess;
            
            if (!success)
            {
                errorMessage = response.Error ?? "서버 요청 실패";
                Debug.LogError($"[EnergyGiftHandler] Server claim failed: {errorMessage}");
            }
            else
            {
                Debug.Log($"[EnergyGiftHandler] Server claim successful: {messageId}");
            }
        });
        
        // 서버 응답 대기
        yield return new WaitUntil(() => requestCompleted);
        
        if (success)
        {
            // 5. 로컬 에너지 추가
            bool energyAdded = AddEnergyToPlayer(energyAmount);
            
            if (energyAdded)
            {
                // 6. 중복 방지 기록 추가
                lock (_lockObject)
                {
                    _claimedGifts.Add(giftId);
                    SaveClaimedGifts();
                }
                
                // 7. 메시지를 읽음으로 표시
                manager.MarkMessageAsRead(messageId, false); // 서버 동기화는 이미 완료됨
                
                // 8. 성공 이벤트 발생
                OnEnergyGiftClaimed?.Invoke(messageId, energyAmount);
                
                Debug.Log($"[EnergyGiftHandler] Energy gift claimed successfully: {messageId}, Energy: {energyAmount}");
            }
            else
            {
                // 로컬 에너지 추가 실패
                string error = "에너지 추가에 실패했습니다";
                Debug.LogError($"[EnergyGiftHandler] Failed to add energy locally: {messageId}");
                OnEnergyGiftClaimFailed?.Invoke(messageId, error);
                
                // TODO: 서버에 롤백 요청을 보낼 수 있음
            }
        }
        else
        {
            // 서버 요청 실패
            OnEnergyGiftClaimFailed?.Invoke(messageId, errorMessage);
        }
    }
    #endregion
    
    #region Energy Management
    /// <summary>
    /// 플레이어에게 에너지 추가
    /// </summary>
    private bool AddEnergyToPlayer(int amount)
    {
        try
        {
            // EnergyManager 인스턴스 확인 (실제 구현에 따라 다를 수 있음)
            var energyManager = FindEnergyManager();
            if (energyManager == null)
            {
                Debug.LogError("[EnergyGiftHandler] EnergyManager not found");
                return false;
            }
            
            // 에너지 추가
            energyManager.AddEnergy(amount);
            Debug.Log($"[EnergyGiftHandler] Added {amount} energy to player");
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyGiftHandler] Failed to add energy: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 에너지 수령 가능 여부 확인
    /// </summary>
    private bool CanReceiveEnergy(int amount)
    {
        try
        {
            var energyManager = FindEnergyManager();
            if (energyManager == null)
                return true; // EnergyManager가 없으면 제한 없이 허용
            
            return energyManager.CanAddEnergy(amount);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EnergyGiftHandler] Error checking energy capacity: {e.Message}");
            return true; // 오류 시 허용
        }
    }
    
    /// <summary>
    /// EnergyManager 찾기
    /// </summary>
    private EnergyManager FindEnergyManager()
    {
        // 실제 구현에 따라 EnergyManager를 찾는 방법이 다를 수 있음
        // 여기서는 일반적인 패턴을 가정
        
        // 1. Singleton 패턴 시도
        try
        {
            // EnergyManager가 Singleton이라면
            var energyManagerType = Type.GetType("EnergyManager");
            if (energyManagerType != null)
            {
                var instanceProperty = energyManagerType.GetProperty("Instance", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProperty != null)
                {
                    return instanceProperty.GetValue(null) as EnergyManager;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EnergyGiftHandler] Failed to get EnergyManager instance: {e.Message}");
        }
        
        // 2. FindObjectOfType으로 찾기
        try
        {
            return UnityEngine.Object.FindObjectOfType<EnergyManager>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EnergyGiftHandler] Failed to find EnergyManager object: {e.Message}");
        }
        
        return null;
    }
    #endregion
    
    #region Duplicate Prevention
    /// <summary>
    /// 선물이 이미 수령되었는지 확인
    /// </summary>
    private bool IsGiftAlreadyClaimed(string giftId)
    {
        if (string.IsNullOrEmpty(giftId))
            return false;
        
        lock (_lockObject)
        {
            return _claimedGifts.Contains(giftId);
        }
    }
    
    /// <summary>
    /// 수령한 선물 목록 로드
    /// </summary>
    private static void LoadClaimedGifts()
    {
        try
        {
            string claimedGiftsData = PlayerPrefs.GetString(CLAIMED_GIFTS_KEY, "");
            string timestampData = PlayerPrefs.GetString(CLAIMED_GIFTS_TIMESTAMP_KEY, "");
            
            if (string.IsNullOrEmpty(claimedGiftsData) || string.IsNullOrEmpty(timestampData))
            {
                _claimedGifts = new HashSet<string>();
                return;
            }
            
            // 타임스탬프 확인 (오래된 데이터 제거)
            if (DateTime.TryParse(timestampData, out DateTime lastUpdate))
            {
                if ((DateTime.UtcNow - lastUpdate).TotalDays > CLAIMED_GIFTS_RETENTION_DAYS)
                {
                    Debug.Log("[EnergyGiftHandler] Claimed gifts data is old, clearing");
                    _claimedGifts = new HashSet<string>();
                    return;
                }
            }
            
            // 암호화된 데이터 복호화
            string decryptedData = CryptoHelper.DecryptAES(claimedGiftsData, GetEncryptionKey());
            if (!string.IsNullOrEmpty(decryptedData))
            {
                var giftIds = JsonUtility.FromJson<SerializableHashSet>(decryptedData);
                _claimedGifts = new HashSet<string>(giftIds.items);
                
                Debug.Log($"[EnergyGiftHandler] Loaded {_claimedGifts.Count} claimed gifts");
            }
            else
            {
                _claimedGifts = new HashSet<string>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyGiftHandler] Failed to load claimed gifts: {e.Message}");
            _claimedGifts = new HashSet<string>();
        }
    }
    
    /// <summary>
    /// 수령한 선물 목록 저장
    /// </summary>
    private static void SaveClaimedGifts()
    {
        try
        {
            var serializableSet = new SerializableHashSet { items = new List<string>(_claimedGifts) };
            string jsonData = JsonUtility.ToJson(serializableSet);
            
            // 암호화
            string encryptedData = CryptoHelper.EncryptAES(jsonData, GetEncryptionKey());
            
            PlayerPrefs.SetString(CLAIMED_GIFTS_KEY, encryptedData);
            PlayerPrefs.SetString(CLAIMED_GIFTS_TIMESTAMP_KEY, DateTime.UtcNow.ToString());
            PlayerPrefs.Save();
            
            Debug.Log($"[EnergyGiftHandler] Saved {_claimedGifts.Count} claimed gifts");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyGiftHandler] Failed to save claimed gifts: {e.Message}");
        }
    }
    
    /// <summary>
    /// 암호화 키 생성
    /// </summary>
    private static string GetEncryptionKey()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        return CryptoHelper.ComputeSHA256Hash($"energy_gift_claimed_{deviceId}");
    }
    
    /// <summary>
    /// 수령한 선물 목록 초기화
    /// </summary>
    public static void ClearClaimedGifts()
    {
        lock (_lockObject)
        {
            _claimedGifts.Clear();
            
            PlayerPrefs.DeleteKey(CLAIMED_GIFTS_KEY);
            PlayerPrefs.DeleteKey(CLAIMED_GIFTS_TIMESTAMP_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("[EnergyGiftHandler] Cleared claimed gifts");
        }
    }
    #endregion
    
    #region Public Utility Methods
    /// <summary>
    /// 수령한 선물 수 가져오기
    /// </summary>
    public static int GetClaimedGiftsCount()
    {
        lock (_lockObject)
        {
            return _claimedGifts.Count;
        }
    }
    
    /// <summary>
    /// 특정 선물이 수령되었는지 확인 (공개 메서드)
    /// </summary>
    public static bool IsGiftClaimed(string giftId)
    {
        if (string.IsNullOrEmpty(giftId))
            return false;
        
        lock (_lockObject)
        {
            return _claimedGifts.Contains(giftId);
        }
    }
    
    /// <summary>
    /// 에너지 선물 핸들러 상태 정보
    /// </summary>
    public static EnergyGiftHandlerStatus GetStatus()
    {
        lock (_lockObject)
        {
            return new EnergyGiftHandlerStatus
            {
                ClaimedGiftsCount = _claimedGifts.Count,
                RetentionDays = CLAIMED_GIFTS_RETENTION_DAYS,
                HasEnergyManager = FindEnergyManagerStatic() != null
            };
        }
    }
    
    /// <summary>
    /// EnergyManager 찾기 (정적 메서드)
    /// </summary>
    private static EnergyManager FindEnergyManagerStatic()
    {
        try
        {
            return UnityEngine.Object.FindObjectOfType<EnergyManager>();
        }
        catch
        {
            return null;
        }
    }
    #endregion
}

#region Data Structures
/// <summary>
/// JsonUtility 직렬화를 위한 HashSet 래퍼
/// </summary>
[Serializable]
public class SerializableHashSet
{
    public List<string> items = new List<string>();
}

/// <summary>
/// 에너지 선물 핸들러 상태
/// </summary>
[Serializable]
public class EnergyGiftHandlerStatus
{
    public int ClaimedGiftsCount;
    public int RetentionDays;
    public bool HasEnergyManager;
}
#endregion