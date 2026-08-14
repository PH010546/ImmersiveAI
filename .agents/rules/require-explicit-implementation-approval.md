---
name: require-explicit-implementation-approval
description: Universal constraint prohibiting any file creation or editing before the user explicitly gives implementation approval.
trigger: always_on
---

# Strict Implementation Guardrail: Explicit User Approval Required

1. **Discussion vs. Implementation Boundary**:
   - During requirement analysis, hypothetical edge-case discussions, mathematical modeling, or design planning, the agent must remain in **Read-Only / Advisory Mode**.
   - **NEVER** modify existing files, create new source files, or execute state-modifying scripts before the user explicitly issues an approval command (e.g. "開始實作", "請開始開發", "好，就這麼做", "請執行修改").

2. **Required Workflow**:
   - Step 1: Clarify and align on the design plan.
   - Step 2: Present the proposed plan/architecture to the user.
   - Step 3: **Wait for the user's explicit confirmation/instruction.**
   - Step 4: Only begin modifying code and creating files after explicit confirmation is received.
