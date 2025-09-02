---
issue: 20
title: Task 07: 우편함 시스템 구현 (기존 NetworkManager 활용)
analyzed: 2025-09-02T05:25:19Z
estimated_hours: 14
parallelization_factor: 2.5
---

# Parallel Work Analysis: Issue #20

## Overview
친구 메시지, 공지사항, 피로도 선물을 위한 우편함 시스템 구현: 기존 NetworkManager HTTP API 활용, 로컬 캐싱, 읽음/안읽음 상태 관리, 피로도 선물 수령 기능

## Parallel Streams

### Stream A: Core Mailbox System & Data Management
**Scope**: 우편함 핵심 로직 및 데이터 관리
**Files**:
- `Assets/Scripts/Managers/MailboxManager.cs`
- `Assets/Scripts/Data/MailboxData.cs`
- `Assets/Scripts/Systems/MailboxCache.cs`
- `Assets/Scripts/Systems/EnergyGiftHandler.cs`
**Agent Type**: backend-specialist
**Can Start**: immediately
**Estimated Hours**: 6
**Dependencies**: none

### Stream B: Mailbox UI Components
**Scope**: 우편함 UI 및 메시지 표시 시스템
**Files**:
- `Assets/Scripts/UI/Mailbox/MailboxUI.cs`
- `Assets/Scripts/UI/Mailbox/MessageItemUI.cs`
- `Assets/Scripts/UI/Mailbox/MailboxBadge.cs`
- `Assets/Prefabs/UI/MailboxSection.prefab`
**Agent Type**: frontend-specialist
**Can Start**: immediately
**Estimated Hours**: 5
**Dependencies**: none (Mock 데이터로 독립 개발)

### Stream C: Network Integration & HTTP API
**Scope**: NetworkManager HTTP API 통합 및 서버 통신
**Files**:
- `Assets/Scripts/Network/MailboxNetworkHandler.cs`
- `Assets/Scripts/Systems/MailboxSynchronizer.cs`
**Agent Type**: backend-specialist
**Can Start**: after Stream A basic structure
**Estimated Hours**: 3
**Dependencies**: Stream A (MailboxManager, MailboxData)

### Stream D: Integration & Testing
**Scope**: 시스템 통합 및 종합 테스트
**Files**:
- `Assets/Scripts/Tests/MailboxSystemTests.cs`
- `Assets/Scripts/Tests/MailboxUITests.cs`
- `Assets/Scripts/Integration/EnergyMailboxIntegration.cs`
**Agent Type**: quality-engineer
**Can Start**: after Streams A & B interfaces
**Estimated Hours**: 3
**Dependencies**: Stream A (mailbox logic), Stream B (UI components)

## Coordination Points

### Shared Files
다음 파일들은 여러 스트림에서 참조하거나 수정이 필요:
- `Assets/Scripts/Data/MailboxData.cs` - Streams A & C (데이터 구조 및 네트워크 직렬화)
- 기존 NetworkManager - Stream C (HTTP API 메서드 추가)
- EnergyManager - Stream A (피로도 선물 연동)

### Sequential Requirements
다음 작업들은 순차적으로 진행되어야 함:
1. MailboxManager 및 데이터 구조 (Stream A) → HTTP API 통합 (Stream C)
2. UI 컴포넌트 인터페이스 (Stream B) → 통합 테스트 (Stream D)
3. 핵심 기능 완성 (Streams A, B, C) → 종합 테스트 및 EnergyManager 통합 (Stream D)

## Conflict Risk Assessment
- **Low Risk**: 대부분 새로운 파일 생성으로 충돌 위험 낮음
- **Medium Risk**: NetworkManager 확장 시 기존 HTTP 기능과 조정 필요
- **Low Risk**: EnergyManager 연동은 기존 패턴 활용

## Parallelization Strategy

**Recommended Approach**: hybrid

**Phase 1 (Parallel)**: Stream A & B 동시 시작
- Stream A: 우편함 핵심 로직, 캐싱, 선물 처리 시스템
- Stream B: UI 컴포넌트 (Mock 데이터로 독립 개발)

**Phase 2 (Dependent)**: Stream A 기본 완성 후 Stream C 시작
- Stream C: NetworkManager HTTP API 통합 (MailboxData 구조 확정 후)

**Phase 3 (Integration)**: Streams A, B 인터페이스 완성 후 Stream D
- Stream D: 통합 테스트 및 EnergyManager 연동

## Expected Timeline

With parallel execution:
- Phase 1: 6 hours (A & B 병렬, A가 더 오래 걸림)
- Phase 2: 3 hours (C 순차, A 기본 구조 대기)
- Phase 3: 3 hours (D 순차, A&B 인터페이스 대기)
- Wall time: 9 hours (6 + 2 + 3, 일부 겹침)
- Total work: 17 hours
- Efficiency gain: 47%

Without parallel execution:
- Wall time: 17 hours (6+5+3+3)

## Notes

**Key Success Factors:**
- Stream A에서 MailboxData 구조를 조기에 확정하여 Stream C 블로킹 최소화
- Stream B는 Mock 메시지 데이터로 완전 독립 개발 가능
- 기존 NetworkManager HTTP 패턴 최대한 활용
- PlayerPrefs + CryptoHelper 패턴 기존 구현 재사용

**Risk Mitigation:**
- 로컬 캐싱으로 네트워크 오류 시 대응
- 피로도 선물 중복 수령 방지 메커니즘
- 메시지 암호화 저장으로 보안 강화
- 3초 로딩 시간 제한으로 UX 개선

**Critical Integration Points:**
- Issue #16의 EnergyManager와 피로도 선물 연동
- 기존 NetworkManager HTTP API 패턴 활용
- PlayerPrefs + CryptoHelper 기존 암호화 시스템 재사용
- 설정 섹션에서 우편함 접근 통합

**Performance Considerations:**
- 메시지 목록 가상화로 60FPS 스크롤 유지
- 캐시 히트 시 1초 이내 즉시 로딩
- 메모리 사용량 10MB 이하 제한
- 대량 메시지 처리 시 페이징 고려