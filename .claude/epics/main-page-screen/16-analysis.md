---
issue: 16
title: Task 03: EnergyManager 및 피로도 시스템 구현
analyzed: 2025-09-02T01:39:51Z
estimated_hours: 20
parallelization_factor: 3.2
---

# Parallel Work Analysis: Issue #16

## Overview
Unity 게임 내 피로도 시스템 구현: 현재/최대 피로도 표시, 자동 회복, 게임 내 재화를 통한 구매, 게임 플레이 제한 기능을 NetworkManager와 통합하여 구현

## Parallel Streams

### Stream A: Core Energy Management System
**Scope**: 피로도 핵심 로직 및 데이터 관리
**Files**:
- `Assets/Scripts/Managers/EnergyManager.cs`
- `Assets/Scripts/Data/EnergyData.cs` 
- `Assets/Scripts/Systems/EnergyRecoverySystem.cs`
- `Assets/Scripts/Config/EnergyConfig.cs`
**Agent Type**: backend-specialist
**Can Start**: immediately
**Estimated Hours**: 8
**Dependencies**: none

### Stream B: UI Components & Display System  
**Scope**: 피로도 표시 UI 컴포넌트 및 시각적 요소
**Files**:
- `Assets/Scripts/UI/Energy/EnergyDisplayUI.cs`
- `Assets/Scripts/UI/Energy/EnergyBar.cs`
- `Assets/Scripts/UI/Energy/EnergyPurchaseUI.cs`
- `Assets/Prefabs/UI/EnergySection.prefab`
- `Assets/Prefabs/UI/EnergyPurchaseModal.prefab`
**Agent Type**: frontend-specialist
**Can Start**: immediately 
**Estimated Hours**: 6
**Dependencies**: none

### Stream C: Network Integration & Purchase System
**Scope**: 서버 동기화 및 구매 처리 로직
**Files**:
- `Assets/Scripts/Network/EnergyAPI.cs`
- `Assets/Scripts/Managers/EnergyPurchaseManager.cs`
- `Assets/Scripts/Data/EnergyPurchaseRequest.cs`
**Agent Type**: backend-specialist
**Can Start**: after Stream A foundation (EnergyData models)
**Estimated Hours**: 5
**Dependencies**: Stream A (EnergyData 구조 정의 필요)

### Stream D: Game Integration & Testing
**Scope**: 게임 플로우 통합 및 종합 테스트
**Files**:
- `Assets/Scripts/Game/GameFlowIntegration.cs` (기존 수정)
- `Assets/Scripts/Tests/EnergyManagerTests.cs`
- `Assets/Scripts/Tests/EnergyRecoveryTests.cs` 
- `Assets/Scripts/Tests/EnergyValidationTests.cs`
**Agent Type**: backend-specialist
**Can Start**: after Streams A & C complete
**Estimated Hours**: 4
**Dependencies**: Stream A (core logic), Stream C (network integration)

## Coordination Points

### Shared Files
다음 파일들은 여러 스트림에서 참조하거나 수정이 필요:
- `Assets/Scripts/Data/EnergyData.cs` - Streams A & C (데이터 구조 조정)
- `Assets/Scripts/Managers/NetworkManager.cs` - Stream C (기존 HTTP 인프라 재사용)
- Main Scene 파일 - Stream B (UI 컴포넌트 배치)

### Sequential Requirements
다음 작업들은 순차적으로 진행되어야 함:
1. EnergyData 모델 정의 (Stream A) → API 요청/응답 구조 (Stream C)
2. EnergyManager 핵심 로직 (Stream A) → 게임 플로우 통합 (Stream D)
3. UI 컴포넌트 (Stream B) → 구매 UI 연동 (Stream C)
4. 모든 개별 컴포넌트 완료 → 통합 테스트 (Stream D)

## Conflict Risk Assessment
- **Low Risk**: 대부분 새로운 파일 생성으로 충돌 위험 낮음
- **Medium Risk**: NetworkManager 기존 코드 활용 시 조정 필요
- **Low Risk**: Scene 파일 UI 배치는 독립적 영역에 추가

## Parallelization Strategy

**Recommended Approach**: hybrid

**Phase 1 (Parallel)**: Stream A & B 동시 시작
- Stream A: 핵심 데이터 모델 및 매니저 로직 구현
- Stream B: UI 컴포넌트 및 표시 시스템 구현

**Phase 2 (Sequential)**: Stream A 완료 후 Stream C 시작  
- Stream C: EnergyData 구조 확정 후 네트워크 연동 구현

**Phase 3 (Integration)**: Stream D로 통합 및 테스트
- Stream A, C 완료 후 게임 플로우 통합 및 테스트

## Expected Timeline

With parallel execution:
- Phase 1: 8 hours (A & B 병렬)
- Phase 2: 5 hours (C 순차)  
- Phase 3: 4 hours (D 순차)
- Wall time: 17 hours
- Total work: 23 hours
- Efficiency gain: 26%

Without parallel execution:
- Wall time: 23 hours (8+6+5+4)

## Notes

**Key Success Factors:**
- EnergyData 모델을 Stream A에서 우선 확정하여 Stream C 블로킹 최소화
- UI 컴포넌트는 독립적으로 개발 가능하므로 Stream B는 완전 병렬 진행
- NetworkManager 기존 패턴 분석 후 Stream C 진행하여 일관성 유지
- 통합 테스트는 모든 컴포넌트 완성 후 진행하여 디버깅 효율성 확보

**Risk Mitigation:**
- Stream A에서 EnergyData 인터페이스를 초기에 정의하여 Stream C 사전 준비 가능
- UI Mock 데이터로 Stream B 독립 개발 진행
- NetworkManager 패턴 분석을 Stream C 초기에 완료하여 통합 리스크 감소