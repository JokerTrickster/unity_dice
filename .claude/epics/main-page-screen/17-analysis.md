---
issue: 17
title: Task 04: WebSocketClient 구현 및 NetworkManager 통합
analyzed: 2025-09-02T02:10:52Z
estimated_hours: 24
parallelization_factor: 2.8
---

# Parallel Work Analysis: Issue #17

## Overview
실시간 매칭을 위한 WebSocket 클라이언트 구현 및 기존 NetworkManager와의 하이브리드 통합: 기존 HTTP 기능 유지하면서 WebSocket 매칭 기능을 추가하여 안정적인 실시간 통신 구현

## Parallel Streams

### Stream A: Core WebSocket Client Implementation
**Scope**: 기본 WebSocket 클라이언트 및 연결 관리
**Files**:
- `Assets/Scripts/Network/WebSocketClient.cs`
- `Assets/Scripts/Network/ConnectionManager.cs`
- `Assets/Scripts/Network/MessageQueue.cs`
- `Assets/Scripts/Config/WebSocketConfig.cs`
**Agent Type**: backend-specialist
**Can Start**: immediately
**Estimated Hours**: 10
**Dependencies**: none

### Stream B: Protocol & Message System
**Scope**: 매칭 프로토콜 및 메시지 처리 시스템
**Files**:
- `Assets/Scripts/Network/MatchingProtocol.cs`
- `Assets/Scripts/Network/MatchingMessage.cs`
- `Assets/Scripts/Network/MatchingRequest.cs`
- `Assets/Scripts/Network/MatchingResponse.cs`
**Agent Type**: backend-specialist
**Can Start**: immediately
**Estimated Hours**: 6
**Dependencies**: none

### Stream C: NetworkManager Integration
**Scope**: 기존 NetworkManager 확장 및 WebSocket 통합
**Files**:
- `Assets/Scripts/Network/NetworkManager.cs` (확장)
- `Assets/Scripts/Network/NetworkManagerExtensions.cs`
- `Assets/Scripts/Network/HybridNetworkManager.cs`
**Agent Type**: backend-specialist
**Can Start**: after Stream A basic WebSocketClient structure
**Estimated Hours**: 6
**Dependencies**: Stream A (WebSocketClient interface 정의 필요)

### Stream D: Testing & Mock Infrastructure
**Scope**: 테스트 시스템 및 Mock 서버 구현
**Files**:
- `Assets/Scripts/Tests/WebSocketClientTests.cs`
- `Assets/Scripts/Tests/MatchingProtocolTests.cs`
- `Assets/Scripts/Tests/ConnectionManagerTests.cs`
- `Assets/Scripts/Network/Mock/MockWebSocketServer.cs`
**Agent Type**: quality-engineer
**Can Start**: after Streams A & B interfaces defined
**Estimated Hours**: 8
**Dependencies**: Stream A (WebSocket interfaces), Stream B (protocol definitions)

## Coordination Points

### Shared Files
다음 파일들은 여러 스트림에서 참조하거나 수정이 필요:
- `Assets/Scripts/Network/WebSocketClient.cs` - Streams A & C (인터페이스 조정)
- `Assets/Scripts/Network/NetworkManager.cs` - Stream C (기존 코드 확장)
- Protocol 메시지 클래스들 - Streams B & D (테스트 데이터 구조)

### Sequential Requirements
다음 작업들은 순차적으로 진행되어야 함:
1. WebSocketClient 기본 인터페이스 (Stream A) → NetworkManager 통합 (Stream C)
2. 프로토콜 메시지 구조 (Stream B) → 테스트 케이스 작성 (Stream D)
3. 핵심 기능 완성 (Streams A, B, C) → 종합 테스트 (Stream D)

## Conflict Risk Assessment
- **Medium Risk**: NetworkManager 기존 코드 수정으로 기능 충돌 가능성
- **Low Risk**: WebSocket 관련 파일들은 대부분 신규 생성
- **Medium Risk**: 테스트 파일들이 여러 컴포넌트에 의존

## Parallelization Strategy

**Recommended Approach**: hybrid

**Phase 1 (Parallel)**: Stream A & B 동시 시작
- Stream A: WebSocketClient 기본 구조 및 연결 관리
- Stream B: 매칭 프로토콜 및 메시지 시스템

**Phase 2 (Dependent)**: Stream A 기본 완성 후 Stream C 시작
- Stream C: NetworkManager 통합 (WebSocketClient 인터페이스 확정 후)

**Phase 3 (Integration)**: Streams A, B 인터페이스 완성 후 Stream D 시작
- Stream D: 테스트 시스템 및 Mock 인프라 (프로토콜 구조 확정 후)

## Expected Timeline

With parallel execution:
- Phase 1: 10 hours (A & B 병렬, A가 더 오래 걸림)
- Phase 2: 6 hours (C 순차, A 완성 대기)
- Phase 3: 8 hours (D 순차, A&B 완성 대기)
- Wall time: 18 hours (10 + 6 + 8, 일부 겹침 고려)
- Total work: 30 hours
- Efficiency gain: 40%

Without parallel execution:
- Wall time: 30 hours (10+6+6+8)

## Notes

**Key Success Factors:**
- Stream A에서 WebSocketClient 인터페이스를 초기에 명확히 정의
- Stream B는 완전 독립적 개발 가능하므로 동시 진행
- NetworkManager 기존 코드 분석을 Stream C 시작 전 완료
- Mock 서버 구현을 통한 독립적 테스트 환경 구축

**Risk Mitigation:**
- NetworkManager 기존 HTTP 기능 무손실 보장을 위한 점진적 통합
- WebSocket 연결 실패 시 HTTP fallback 구조 구현
- 메시지 큐잉 시스템으로 연결 불안정성 대응
- 단위 테스트 우선 구현으로 통합 리스크 최소화

**Critical Dependencies:**
- WebSocket-sharp 라이브러리 설치 (모든 스트림 시작 전)
- 기존 NetworkManager 코드 분석 완료 (Stream C 시작 전)
- Thread-safe 구현을 위한 Unity 메인 스레드 패턴 적용