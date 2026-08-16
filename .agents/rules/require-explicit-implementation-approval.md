---
name: require-explicit-implementation-approval
description: Universal constraint prohibiting any file creation or editing before the user explicitly gives implementation approval, and requiring explicit diff presentation for all code/prompt changes.
trigger: always_on
---

# Strict Implementation Guardrail: Explicit User Approval & Detailed Diff Required

1. **Discussion vs. Implementation Boundary**:
   - During requirement analysis, hypothetical edge-case discussions, mathematical modeling, or design planning, the agent must remain in **Read-Only / Advisory Mode**.
   - **NEVER** modify existing files, create new source files, or execute state-modifying scripts before the user explicitly issues an approval command (e.g. "開始實作", "請開始開發", "好，就這麼做", "請執行修改").

2. **No Direct Unapproved Code / Prompt Modifications (禁止直接修改現有程式碼與 Prompt)**:
   - **嚴禁直接修改現有程式碼或 Prompt 文字**。
   - 凡涉及任何現有程式碼、邏輯分支或 Prompt 字句的增刪修改，**必須以 Diff 格式明確列出修改前（Before / 原版設計）與修改後（After / 擬議修改）的具體差異點**，並清晰說明修改原由。
   - 必須等待使用者審閱 Diff 並發出明確同意指令後，始得著手執行代碼修改。

3. **Required Workflow**:
   - Step 1: Clarify and align on the design plan.
   - Step 2: Present the proposed plan/architecture along with a detailed code/prompt **Diff (Before vs. After)** to the user.
   - Step 3: **Wait for the user's explicit confirmation/instruction.**
   - Step 4: Only begin modifying code and creating files after explicit confirmation is received.
