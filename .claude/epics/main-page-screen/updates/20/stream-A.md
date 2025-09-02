---
issue: 20
stream: Core Mailbox System & Data Management
agent: backend-specialist
started: 2025-09-02T05:25:19Z
status: completed
completed: 2025-09-02T14:45:00Z
---

# Stream A: Core Mailbox System & Data Management

## Scope
우편함 핵심 로직 및 데이터 관리

## Files
- Assets/Scripts/Managers/MailboxManager.cs ✅
- Assets/Scripts/Data/MailboxData.cs ✅
- Assets/Scripts/Systems/MailboxCache.cs ✅
- Assets/Scripts/Systems/EnergyGiftHandler.cs ✅

## Progress
- ✅ **Core Data Structures Implemented**
  - MailboxData.cs: Complete message data structure with Unity JsonUtility support
  - MailboxMessage class with proper DateTime serialization handling
  - MailMessageType enum for all message types
  - MailboxAttachment system for flexible key-value data storage

- ✅ **Secure Caching System**
  - MailboxCache.cs: PlayerPrefs + CryptoHelper integration
  - 6-hour cache expiry with automatic cleanup
  - User-specific encryption keys using device fingerprinting
  - Cache validation and corruption recovery

- ✅ **MailboxManager Singleton**
  - Complete singleton pattern implementation
  - Comprehensive event system for UI updates
  - NetworkManager integration for HTTP API calls
  - Auto-sync every 5 minutes with retry logic
  - Read/unread state management with server synchronization

- ✅ **EnergyGiftHandler**
  - Message type handler interface implementation
  - Duplicate gift prevention with encrypted local storage
  - EnergyManager integration (AddEnergy, CanAddEnergy)
  - Server-side claim validation with rollback support
  - Comprehensive error handling and user feedback

## API Documentation for Stream C (HTTP Integration)

### Required Endpoints
The system expects these HTTP endpoints to be implemented:

```
GET /api/mailbox/messages
- Returns: MailboxData JSON with messages array
- Used by: MailboxManager.LoadMessagesFromServer()

POST /api/mailbox/read
- Body: { messageId: string }
- Used by: MailboxManager.MarkAsReadOnServer()

DELETE /api/mailbox/messages/{messageId}
- Used by: MailboxManager.DeleteMessageOnServer()

POST /api/mailbox/claim
- Body: { messageId: string, giftId: string, energyAmount: int }
- Used by: EnergyGiftHandler.ClaimEnergyGiftOnServer()
```

### Data Structure Contracts
```csharp
// Expected server response format
public class MailboxData {
    public List<MailboxMessage> messages;
    public int unreadCount;
    public string lastSyncTimeString; // "yyyy-MM-dd HH:mm:ss" format
}

public class MailboxMessage {
    public string messageId;
    public MailMessageType type; // 0=System, 1=Friend, 2=EnergyGift, 3=Achievement, 4=Event
    public string title;
    public string content;
    public string senderId;
    public string senderName;
    public string sentAtString; // "yyyy-MM-dd HH:mm:ss" format
    public string readAtString; // "yyyy-MM-dd HH:mm:ss" format or empty
    public bool isRead;
    public List<MailboxAttachment> attachments;
}
```

## Event System Documentation for Stream B (UI Integration)

### Available Events
```csharp
// Mailbox data events
MailboxManager.OnMailboxLoaded(MailboxData data)
MailboxManager.OnMailboxUpdated(MailboxData data)
MailboxManager.OnUnreadCountChanged(int unreadCount)

// Message events
MailboxManager.OnNewMessageReceived(MailboxMessage message)
MailboxManager.OnMessageRead(string messageId)
MailboxManager.OnMessageDeleted(string messageId)

// System status events
MailboxManager.OnSyncStatusChanged(bool success, string message)
MailboxManager.OnError(string errorMessage)

// Energy gift events
EnergyGiftHandler.OnEnergyGiftClaimed(string messageId, int energyAmount)
EnergyGiftHandler.OnEnergyGiftClaimFailed(string messageId, string error)
```

### Usage Example for UI
```csharp
void Start() {
    // Subscribe to events
    MailboxManager.OnUnreadCountChanged += UpdateUnreadBadge;
    MailboxManager.OnMailboxUpdated += RefreshMessageList;
    MailboxManager.OnError += ShowErrorDialog;
    
    // Initialize mailbox
    MailboxManager.Instance.Initialize(currentUserId);
}

void UpdateUnreadBadge(int unreadCount) {
    unreadBadgeText.text = unreadCount.ToString();
    unreadBadge.SetActive(unreadCount > 0);
}
```

## Key Features Delivered

1. **Complete Data Management**: Full CRUD operations for mailbox messages with validation
2. **Secure Local Caching**: Encrypted storage with automatic expiry and corruption recovery
3. **Network Integration**: Full NetworkManager integration with retry logic and error handling
4. **Event-Driven Architecture**: Comprehensive event system for loose coupling with UI
5. **Energy Gift Processing**: Complete gift claiming workflow with duplicate prevention
6. **Singleton Pattern**: Thread-safe singleton with proper Unity lifecycle management
7. **Error Resilience**: Graceful degradation and comprehensive error handling

## Dependencies Confirmed
- ✅ EnergyManager.AddEnergy() integration
- ✅ NetworkManager HTTP API methods
- ✅ CryptoHelper encryption utilities
- ✅ PlayerPrefs for local storage
- ✅ Unity JsonUtility for serialization

## Ready for Integration
The core mailbox system is now complete and ready for Stream B (UI) and Stream C (HTTP API) integration. All public APIs and event systems are documented and ready for use.