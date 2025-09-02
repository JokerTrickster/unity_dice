---
issue: 18
title: Task 05: 매칭 UI 및 랜덤 매칭 시스템
analyzed: 2025-09-02T02:51:20Z
estimated_hours: 18
parallelization_factor: 3.5
---

# Parallel Work Analysis: Issue #18

## Overview
WebSocketClient를 활용한 실시간 랜덤 매칭 시스템 구현: 2-4명 인원수 선택, 실시간 상태 표시, 매칭 취소, WebSocket 통신 연동을 포함한 완전한 매칭 UI 시스템

## Parallel Streams

### Stream A: Core Matching System & State Management
**Scope**: 매칭 핵심 로직 및 상태 관리
**Files**:
- `Assets/Scripts/Managers/MatchingManager.cs`
- `Assets/Scripts/Data/MatchingState.cs`
- `Assets/Scripts/Systems/MatchingStateManager.cs`
- `Assets/Scripts/Config/MatchingConfig.cs`
**Agent Type**: backend-specialist
**Can Start**: immediately
**Estimated Hours**: 7
**Dependencies**: WebSocket 시스템 (완료됨)

### Stream B: UI Components & Visual System
**Scope**: 매칭 UI 컴포넌트 및 시각적 요소
**Files**:
- `Assets/Scripts/UI/Matching/MatchingUI.cs`
- `Assets/Scripts/UI/Matching/PlayerCountSelector.cs`
- `Assets/Scripts/UI/Matching/MatchingStatusDisplay.cs`
- `Assets/Scripts/UI/Matching/MatchingProgressAnimator.cs`
- `Assets/Prefabs/UI/MatchingSection.prefab`
**Agent Type**: frontend-specialist
**Can Start**: immediately
**Estimated Hours**: 6
**Dependencies**: none (UI 독립 개발 가능)

### Stream C: WebSocket Integration & Communication
**Scope**: WebSocket 통신 및 서버 연동
**Files**:
- `Assets/Scripts/Network/MatchingNetworkHandler.cs`
- `Assets/Scripts/Systems/MatchingTimeout.cs`
- `Assets/Scripts/Systems/MatchingReconnection.cs`
**Agent Type**: backend-specialist
**Can Start**: after Stream A basics (MatchingManager 인터페이스)
**Estimated Hours**: 5
**Dependencies**: Stream A (MatchingManager 구조)

### Stream D: Integration & Testing
**Scope**: 시스템 통합 및 테스트
**Files**:
- `Assets/Scripts/Tests/MatchingSystemTests.cs`
- `Assets/Scripts/Tests/MatchingUITests.cs`
- `Assets/Scripts/Integration/EnergyMatchingIntegration.cs`
**Agent Type**: quality-engineer
**Can Start**: after Streams A & B interfaces
**Estimated Hours**: 4
**Dependencies**: Stream A (core logic), Stream B (UI components)

## Coordination Points

### Shared Files
다음 파일들은 여러 스트림에서 참조하거나 수정이 필요:
- `Assets/Scripts/Managers/MatchingManager.cs` - Streams A & C (상태 관리 및 네트워크 연동)
- UI 이벤트 시스템 - Streams B & A (UI-백엔드 통신)
- 기존 NetworkManager - Stream C (WebSocket 확장 활용)

### Sequential Requirements
다음 작업들은 순차적으로 진행되어야 함:
1. MatchingManager 기본 구조 (Stream A) → WebSocket 통신 연동 (Stream C)
2. UI 컴포넌트 인터페이스 (Stream B) → 통합 테스트 (Stream D)
3. 핵심 기능 완성 (Streams A, B, C) → 종합 테스트 및 최적화 (Stream D)

## Conflict Risk Assessment
- **Low Risk**: 대부분 새로운 파일 생성으로 충돌 위험 낮음
- **Medium Risk**: EnergyManager와의 통합 지점에서 조정 필요
- **Low Risk**: WebSocket 시스템은 이미 완성되어 통합 용이

## Parallelization Strategy

**Recommended Approach**: hybrid

**Phase 1 (Parallel)**: Stream A & B 동시 시작
- Stream A: 매칭 핵심 로직 및 상태 관리 시스템
- Stream B: UI 컴포넌트 및 시각적 요소 (독립 개발)

**Phase 2 (Dependent)**: Stream A 기본 완성 후 Stream C 시작
- Stream C: WebSocket 통신 연동 (MatchingManager 인터페이스 확정 후)

**Phase 3 (Integration)**: Streams A, B 인터페이스 완성 후 Stream D
- Stream D: 통합 테스트 및 EnergyManager 연동

## Expected Timeline

With parallel execution:
- Phase 1: 7 hours (A & B 병렬, A가 더 오래 걸림)
- Phase 2: 5 hours (C 순차, A 기본 구조 대기)
- Phase 3: 4 hours (D 순차, A&B 인터페이스 대기)
- Wall time: 12 hours (7 + 3 + 4, 일부 겹침)
- Total work: 22 hours
- Efficiency gain: 45%

Without parallel execution:
- Wall time: 22 hours (7+6+5+4)

## Notes

**Key Success Factors:**
- Stream A에서 MatchingManager 인터페이스를 조기에 확정
- Stream B는 Mock 데이터로 완전 독립 개발 가능
- 기존 WebSocket 시스템(Issue #17)과의 원활한 통합
- EnergyManager와의 연동 지점 명확히 정의

**Risk Mitigation:**
- WebSocket 연결 실패 시 사용자 친화적 에러 처리
- 매칭 타임아웃 및 취소 기능으로 UX 개선
- 실시간 상태 업데이트를 통한 반응성 확보
- 피로도 부족 시 적절한 안내 및 구매 유도

**Critical Integration Points:**
- Issue #17의 WebSocket 시스템 활용 (MatchingProtocol, NetworkManager)
- Issue #16의 EnergyManager와 피로도 검증 연동
- 기존 UserDataManager를 통한 사용자 정보 활용