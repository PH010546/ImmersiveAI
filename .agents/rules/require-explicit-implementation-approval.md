---
name: require-explicit-implementation-approval
description: Universal constraint prohibiting any unapproved modifications to original mod author's code/prompts, requiring explicit diff presentation and rationale review before execution.
trigger: always_on
---

# Strict Implementation Guardrail: Original Mod Code Preservation & Explicit Approval Required

1. **Prohibit Direct Unapproved Modifications to Original Mod Author's Code & Prompts (嚴禁直接改動原 Mod 開發者的程式碼與 Prompt)**:
   - 凡涉及原 Mod 開發者（Anton / Trax）的原有程式碼、邏輯架構、函式實作或 Prompt 字句的任何增刪修改或重構，**嚴禁在未經授權前直接改動**。
   - 必須以 **Diff 格式明確列出修改前（Before / 原作者設計）與修改後（After / 擬議修改）的具體差異點**。
   - 必須清晰說明**修改原由與架構演進脈絡**（為何原作者的設計需要調整、是否能相容原作者的設計哲學）。
   - 必須等待使用者審閱 Diff 並發出明確同意指令後，始得著手執行代碼修改。

2. **Discussion vs. Implementation Boundary**:
   - 於需求分析、邊界情況探討、數學建模或方案設計階段，必須嚴格保持在 **Read-Only / Advisory Mode**（唯讀諮詢模式）。
   - 嚴禁在使用者給予明確授權（如「開始實作」、「請開始開發」、「好，就這麼做」、「請執行修改」）前擅自修改現有檔案或建立新檔案。

3. **Required Standard Workflow**:
   - Step 1: 深入分析問題與原作者底層設計意圖。
   - Step 2: 向使用者呈報具體修復方案，並附帶詳細的 **Code/Prompt Diff (Before vs. After)** 與原由說明。
   - Step 3: **等待使用者明確審閱與指令確認。**
   - Step 4: 獲得指令後始得執行修改、編譯部署與 Git Commit（附帶詳盡純英文架構紀錄）。
