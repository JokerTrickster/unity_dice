---
name: main-page-screen
updated: 2025-09-02T10:51:34Zstatus: in-progress
created: 2025-09-01T04:58:18Z
progress: 70%
prd: .claude/prds/main-page-screen.md
github: https://github.com/JokerTrickster/unity_dice/issues/24
tasks_created: 2025-09-01T05:00:23Z
total_tasks: 10
estimated_hours: 164
critical_path: "01→04→05→06→09→10 (94h)"
github_issues:
  epic: 13
  tasks:
    - 14  # Task 01: UI Layout
    - 15  # Task 02: Profile Section  
    - 16  # Task 03: Energy System
    - 17  # Task 04: WebSocket Client
    - 18  # Task 05: Matching UI
    - 19  # Task 06: Room System
    - 20  # Task 07: Mailbox System
    - 21  # Task 08: Settings Extension
    - 22  # Task 09: Integration Testing
    - 23  # Task 10: Optimization & Deployment
---

# Epic: main-page-screen

## Overview

메인 페이지 화면은 Unity 주사위 게임의 핵심 허브로, 기존 로그인 시스템 위에 구축되는 통합 대시보드입니다. 기존의 NetworkManager, UserDataManager, SettingsManager 등 완성된 인프라를 최대한 활용하여 프로필, 피로도, 매칭, 설정의 4가지 핵심 기능을 제공합니다. 웹소켓 기반 실시간 매칭 시스템을 새로 구현하되, 기존 HTTP 통신 인프라와 통합하여 개발 효율성을 극대화합니다.

## Architecture Decisions

### 핵심 설계 결정사항

**1. 기존 시스템 재사용 우선 (Leverage Existing Systems)**
- **UserDataManager**: 프로필 정보 관리 재사용
- **SettingsManager**: 오디오 설정 및 기본 설정 재사용  
- **NetworkManager**: HTTP API 통신 인프라 재사용
- **AuthenticationManager**: 인증 상태 및 사용자 정보 재사용

**2. 단일 화면 통합 접근법 (Unified Dashboard Pattern)**
- 모든 기능을 하나의 메인 화면에 통합
- 섹션별 모듈화로 유지보수성 확보
- 기존 UI 패턴 및 컴포넌트 재사용

**3. 웹소켓 전용 매칭 시스템 (WebSocket-First Matching)**
- 기존 NetworkManager HTTP 기능은 유지
- WebSocketClient를 NetworkManager에 추가로 통합
- 매칭 전용 프로토콜 구현으로 복잡성 최소화

**4. 설정 기반 구성 관리 (Configuration-Driven)**
- ScriptableObject 기반 게임 설정 관리
- 서버 URL 및 게임 파라미터 외부화
- 빌드 후 설정 변경 가능한 구조

## Technical Approach

### Frontend Components

**재사용 가능한 기존 컴포넌트:**
- `UserDataManager` → 프로필 정보 및 닉네임 관리
- `SettingsManager` → 오디오 설정 (배경음악, 효과음)
- `NetworkManager` → HTTP API 통신 (프로필, 피로도, 우편함)
- `AuthenticationManager` → 로그아웃 기능

**새로 구현할 컴포넌트:**
- `MainPageScreen` → 통합 UI 컨트롤러
- `EnergyManager` → 피로도 시스템 관리
- `WebSocketMatchingClient` → 실시간 매칭 통신
- `MailboxManager` → 우편함 데이터 관리

**UI 구조 최적화:**
```
MainPageScreen (단일 Scene)
├── ProfileSection (기존 컴포넌트 재사용)
├── EnergySection (신규 + NetworkManager 연동)
├── MatchingSection (신규 + WebSocket)
└── SettingsSection (기존 SettingsManager 확장)
```

### Backend Services Integration

**HTTP API 연동 (기존 NetworkManager 활용):**
- `GET /api/user/profile` → 프로필 정보 조회
- `POST /api/energy/purchase` → 피로도 구매
- `GET /api/mailbox/messages` → 우편함 메시지
- `PUT /api/user/settings` → 설정 동기화

**WebSocket 연동 (신규 구현):**
- `ws://game-api.unitydice.com/matching` → 매칭 전용 연결
- 프로토콜: JSON 기반 단순 메시지 구조
- 연결 관리: 자동 재연결 및 상태 복구

**데이터 모델 확장:**
```csharp
// 기존 UserData 확장 사용
public class EnergyData {
    public int currentEnergy;
    public int maxEnergy;
    public DateTime lastRecovery;
}

// 매칭 전용 데이터
public class MatchingRequest {
    public int playerCount;
    public string matchType; // "random" or "room"
    public string roomCode;  // room join only
}
```

### Infrastructure

**기존 인프라 활용:**
- Unity UGUI → UI 프레임워크
- PlayerPrefs → 로컬 설정 저장
- JSON Serialization → 데이터 직렬화
- Singleton Pattern → 매니저 클래스 관리

**새로 추가할 인프라:**
- WebSocket-sharp → 실시간 통신 라이브러리
- ScriptableObject → 게임 설정 관리
- 커스텀 Event System → 매칭 상태 알림

**성능 최적화:**
- Object Pooling → UI 요소 재사용
- Lazy Loading → 섹션별 지연 초기화
- Memory Management → 매칭 데이터 자동 정리

## Implementation Strategy

### 개발 우선순위 (Risk-First Approach)

**Week 1-2: 기본 UI + 기존 시스템 통합**
1. MainPageScreen 기본 레이아웃 구성
2. 기존 UserDataManager로 프로필 섹션 구현
3. 기존 SettingsManager로 설정 섹션 구현
4. EnergyManager 구현 + NetworkManager HTTP 연동

**Week 3-4: WebSocket 매칭 시스템 (고위험)**
5. WebSocketClient 구현 및 NetworkManager 통합
6. 매칭 UI 및 상태 관리 시스템 구현
7. 방 생성/참여 로직 구현

**Week 5-6: 통합 및 최적화**
8. 우편함 시스템 구현 (NetworkManager HTTP 활용)
9. 전체 시스템 통합 테스트
10. 성능 최적화 및 에러 처리 강화

### Risk Mitigation

**High Risk: 웹소켓 서버 의존성**
- Mock WebSocket 서버 구현으로 독립 개발
- WebSocket 연결 실패 시 HTTP fallback 구조

**Medium Risk: UI 복잡성**
- 섹션별 독립 개발 및 단계적 통합
- 기존 UI 컴포넌트 최대한 재사용

### Testing Approach

**기존 테스트 인프라 활용:**
- UserDataManagerTests → 프로필 기능 검증
- NetworkManagerTests → HTTP 통신 검증
- SettingsManagerTests → 설정 기능 검증

**신규 테스트 추가:**
- EnergyManagerTests → 피로도 로직 검증
- WebSocketClientTests → 매칭 통신 검증
- MainPageIntegrationTests → 전체 플로우 검증

## Task Breakdown Complete

✅ **10개 작업 생성 완료** (총 164시간 예상)

### Phase 1: Foundation (Week 1-2, 40h)
- **Task 01**: UI 레이아웃 및 기존 시스템 통합 (16h) - 기반 구조
- **Task 04**: WebSocketClient 구현 및 NetworkManager 통합 (24h) - 가장 복잡

### Phase 2: Parallel Features (Week 2-3, 64h)
- **Task 02**: 프로필 섹션 (UserDataManager 활용) (12h) - 독립적
- **Task 03**: EnergyManager 및 피로도 시스템 (20h) - 복잡한 비즈니스 로직
- **Task 05**: 매칭 UI 및 랜덤 매칭 시스템 (18h) - WebSocket 의존
- **Task 07**: 우편함 시스템 (NetworkManager 활용) (14h) - 독립적

### Phase 3: Advanced Features (Week 3-4, 24h)  
- **Task 06**: 방 생성/참여 시스템 (16h) - 매칭 시스템 확장
- **Task 08**: 설정 섹션 확장 (SettingsManager 활용) (8h) - 간단한 UI 재배치

### Phase 4: Quality & Launch (Week 4-6, 36h)
- **Task 09**: 통합 테스트 및 에러 처리 강화 (20h) - 품질 보증
- **Task 10**: 성능 최적화 및 배포 준비 (16h) - 최종 polish

### Critical Path: 01→04→05→06→09→10 (94시간 ≈ 4.7주)

**상세 문서**: 
- 개별 작업 명세: `01-main-page-ui-layout.md` ~ `10-optimization-deployment.md`
- 의존성 분석: `DEPENDENCIES.md`

## Dependencies

### 외부 종속성 (External)
- **WebSocket Server API**: 매칭 시스템 핵심 인프라
  - 상태: 개발 진행 중 (백엔드팀)
  - 완료 예정: 개발 시작 2주 후
  - 완화책: Mock 서버로 병렬 개발

- **게임 내 재화 API**: 피로도 구매 처리
  - 상태: 기 구축 완료
  - 연동: 기존 NetworkManager HTTP 활용

### 내부 종속성 (Internal - Already Completed)
- **UserDataManager**: 프로필 정보 관리 ✅
- **NetworkManager**: HTTP 통신 인프라 ✅
- **SettingsManager**: 설정 관리 ✅
- **AuthenticationManager**: 인증 및 로그아웃 ✅

### 기술 스택 확장
**기존 유지:**
- Unity UGUI, PlayerPrefs, JSON Serialization

**신규 추가:**
- WebSocket-sharp (NuGet 패키지)
- ScriptableObject 설정 시스템

## Success Criteria (Technical)

### 성능 벤치마크
- **화면 로딩**: 3초 이내 (기존 컴포넌트 재사용으로 달성)
- **매칭 요청**: 2초 이내 응답 (WebSocket 최적화)
- **프로필 변경**: 1초 이내 반영 (UserDataManager 활용)
- **메모리 사용**: 추가 30MB 이하 (Object Pooling 적용)

### 품질 게이트
- **단위 테스트**: 기존 85% + 신규 구현 80% 이상
- **통합 테스트**: 전체 플로우 95% 성공률
- **코드 커버리지**: 전체 80% 이상 유지
- **성능 테스트**: 동시 접속자 100명 기준 안정성

### 기술적 수용 기준
- 기존 인증 시스템과 완벽 통합
- WebSocket 연결 실패 시 graceful degradation
- 모든 UI 섹션 독립적 로딩 가능
- 설정 변경 시 실시간 반영

## Estimated Effort

### 전체 타임라인
- **총 개발 기간**: 6주 (240시간)
- **개발 리소스**: Unity 개발자 1명
- **병렬 작업**: 백엔드팀 WebSocket 서버 개발

### 작업 분배
- **기존 시스템 통합**: 80시간 (33%) - Week 1-2
- **WebSocket 매칭 시스템**: 100시간 (42%) - Week 3-4  
- **통합 및 최적화**: 60시간 (25%) - Week 5-6

### Critical Path Items
1. **WebSocket 서버 API 스펙 확정** (Week 1)
2. **WebSocketClient 구현 완료** (Week 3)
3. **매칭 시스템 기본 동작 검증** (Week 4)
4. **전체 통합 테스트 완료** (Week 6)

## Configuration Management

### 개발/운영 환경 설정

**Development Config** (`StreamingAssets/config-dev.json`):
```json
{
  "serverConfig": {
    "websocketUrl": "ws://localhost:8080/matching",
    "httpApiUrl": "http://localhost:3000/api",
    "timeout": 30000,
    "retryAttempts": 3,
    "enableMockData": true
  },
  "gameConfig": {
    "maxEnergyCapacity": 100,
    "energyRecoveryRate": 1,
    "maxPlayers": 4,
    "minPlayers": 2,
    "debugMode": true
  }
}
```

**Production Config** (`StreamingAssets/config-prod.json`):
```json
{
  "serverConfig": {
    "websocketUrl": "wss://game-api.unitydice.com/matching",
    "httpApiUrl": "https://game-api.unitydice.com/v1",
    "timeout": 10000,
    "retryAttempts": 5,
    "enableMockData": false
  },
  "gameConfig": {
    "maxEnergyCapacity": 100,
    "energyRecoveryRate": 1,
    "maxPlayers": 4,
    "minPlayers": 2,
    "debugMode": false
  }
}
```

이 설정 구조를 통해 서버 환경 변경 시에도 빌드 재배포 없이 설정 파일만 교체하여 대응할 수 있습니다.