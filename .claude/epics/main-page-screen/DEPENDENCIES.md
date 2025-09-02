# Task Dependencies Map - Main Page Screen Epic

## Dependency Graph Overview

```
01 (UI Layout) ──┬─→ 02 (Profile)
                 ├─→ 03 (Energy) 
                 ├─→ 07 (Mailbox) ──┐
                 └─→ 08 (Settings) ──┘
                     │
                     └─→ 09 (Integration) ──→ 10 (Optimization)
                         ↑
04 (WebSocket) ──→ 05 (Matching) ──┐
                    │               │
                    └─→ 06 (Room) ──┘
```

## Critical Path Analysis

**Critical Path**: 01 → 04 → 05 → 06 → 09 → 10
**Total Duration**: 94 hours (≈ 4.7 weeks)

### Phase 1: Foundation (Week 1-2)
- **Task 01** (16h): UI Layout - 필수 선행 작업
- **Task 04** (24h): WebSocket - 가장 복잡하고 위험한 작업

### Phase 2: Parallel Development (Week 2-3)  
- **Task 02** (12h): Profile (depends on 01)
- **Task 03** (20h): Energy (depends on 01)
- **Task 05** (18h): Matching (depends on 04)

### Phase 3: Advanced Features (Week 3-4)
- **Task 06** (16h): Room System (depends on 05)
- **Task 07** (14h): Mailbox (depends on 01)
- **Task 08** (8h): Settings (depends on 01, 07)

### Phase 4: Quality Assurance (Week 4-6)
- **Task 09** (20h): Integration Testing (depends on all feature tasks)
- **Task 10** (16h): Optimization (depends on 09)

## Detailed Task Dependencies

### Task 01: Main Page UI Layout
**Dependencies**: None (Entry Point)
**Blocks**: 02, 03, 07, 08
**Risk**: High - Foundation task, delays affect everything

### Task 02: Profile Section  
**Dependencies**: [01] - UI Layout
**Blocks**: None
**Risk**: Low - Independent feature
**Parallel with**: 03, 07, 08

### Task 03: Energy System
**Dependencies**: [01] - UI Layout
**Blocks**: None (but integrates with 05, 06)
**Risk**: Medium - Complex payment logic
**Parallel with**: 02, 07, 08

### Task 04: WebSocket Client
**Dependencies**: None (can start with 01)
**Blocks**: 05, 06
**Risk**: Critical - Most complex, server dependency
**Note**: Can start development with mock server

### Task 05: Matching UI System
**Dependencies**: [04] - WebSocket Client
**Blocks**: 06
**Risk**: High - Depends on WebSocket stability
**Integration**: Requires 03 (Energy) for validation

### Task 06: Room System
**Dependencies**: [05] - Matching UI System  
**Blocks**: None
**Risk**: Medium - Complex real-time state management
**Integration**: Requires 03 (Energy) for game start

### Task 07: Mailbox System
**Dependencies**: [01] - UI Layout
**Blocks**: 08 (Settings needs mailbox button)
**Risk**: Low - Uses existing HTTP infrastructure
**Parallel with**: 02, 03

### Task 08: Settings Extension
**Dependencies**: [01] - UI Layout, [07] - Mailbox (for button)
**Blocks**: None
**Risk**: Very Low - Mostly UI reorganization
**Integration**: Uses existing SettingsManager

### Task 09: Integration Testing
**Dependencies**: [02, 03, 05, 06, 07, 08] - All feature tasks
**Blocks**: 10
**Risk**: Medium - May discover integration issues
**Critical**: Quality gate before optimization

### Task 10: Optimization & Deployment
**Dependencies**: [09] - Integration Testing
**Blocks**: None (Final task)
**Risk**: Low - Performance tuning and build optimization

## Parallel Execution Opportunities

### Week 1-2: Foundation + High Risk
```
┌─ Task 01 (UI Layout) [16h] ─┐
└─ Task 04 (WebSocket) [24h]  ┘ → 40h total
```

### Week 2-3: Parallel Feature Development  
```
┌─ Task 02 (Profile) [12h] ────┐
├─ Task 03 (Energy) [20h] ─────┤ → 20h total (longest)
├─ Task 05 (Matching) [18h] ───┤ (depends on Task 04)
└─ Task 07 (Mailbox) [14h] ────┘
```

### Week 3-4: Final Features
```
┌─ Task 06 (Room) [16h] ───────┐ → 16h total
└─ Task 08 (Settings) [8h] ────┘
```

### Week 4-6: Quality & Launch
```
┌─ Task 09 (Integration) [20h] ┐ → 36h total  
└─ Task 10 (Optimization) [16h]┘
```

## Risk-Based Scheduling

### High Priority (Start Early)
1. **Task 01** - Foundation for everything
2. **Task 04** - Most complex, external dependency  
3. **Task 05** - Critical user feature, depends on 04

### Medium Priority (Parallel Development)
4. **Task 03** - Complex business logic, integrates with matching
5. **Task 06** - Advanced matching feature
6. **Task 09** - Quality assurance gate

### Low Priority (Can be delayed if needed)
7. **Task 02** - Independent feature
8. **Task 07** - Independent feature  
9. **Task 08** - Simple UI reorganization
10. **Task 10** - Final polish

## Resource Allocation Strategy

### Single Developer Optimal Path
**Week 1**: Task 01 (3 days) + Task 04 start (2 days)
**Week 2**: Task 04 complete (3 days) + Task 03 start (2 days)
**Week 3**: Task 03 complete + Task 05 complete + Task 02 start
**Week 4**: Task 02 + Task 06 + Task 07 + Task 08
**Week 5**: Task 09 (Integration testing)
**Week 6**: Task 10 (Optimization) + Buffer

### Multi-Developer Scenario (if available)
**Dev 1 (Lead)**: 01 → 04 → 05 → 06 → 09 → 10
**Dev 2 (Support)**: 02 → 03 → 07 → 08 → 09 (assist)

## External Dependencies Management

### WebSocket Server (Task 04 blocker)
- **Risk**: Server team delay
- **Mitigation**: Mock server implementation
- **Timeline**: Required by Week 3

### Game Currency System (Task 03 dependency)  
- **Status**: Already implemented ✅
- **Integration**: HTTP API calls via NetworkManager

### Achievement System (Task 02 dependency)
- **Status**: Server API available ✅  
- **Integration**: HTTP API calls for profile unlocks

## Quality Gates

### Gate 1: Foundation Complete (End of Week 2)
- **Criteria**: Tasks 01, 04 complete and tested
- **Decision**: Proceed with parallel development or address foundational issues

### Gate 2: Core Features Complete (End of Week 4)
- **Criteria**: Tasks 02, 03, 05, 06, 07, 08 complete  
- **Decision**: Begin integration testing or extend feature development

### Gate 3: Integration Complete (End of Week 5)
- **Criteria**: Task 09 complete with all tests passing
- **Decision**: Proceed to optimization or address integration issues

### Gate 4: Production Ready (End of Week 6)
- **Criteria**: Task 10 complete with performance targets met
- **Decision**: Deploy to production or extend optimization

## Buffer Management

### Built-in Buffers
- **Task-level**: Each task has 15% buffer built into estimates
- **Phase-level**: 1 day buffer between phases
- **Epic-level**: Week 6 serves as final buffer

### Scope Reduction Options (if needed)
1. **Task 08**: Defer settings to post-launch (saves 8h)
2. **Task 06**: Simplify room features (saves 6h)  
3. **Task 10**: Reduce optimization scope (saves 8h)
4. **Task 02**: Use simpler profile selection (saves 4h)

Total reduction potential: 26 hours (≈ 1 week buffer)