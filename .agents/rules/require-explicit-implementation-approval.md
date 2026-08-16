---
name: require-explicit-implementation-approval
description: Universal constraint governing implementation approval levels for original mod code (mandatory full diff) vs newly added code (approval required, full diff optional), along with mandatory git branch verification before any edits.
trigger: always_on
---

# Strict Implementation Guardrail: Tiered Approval, Branch Verification & Modification Rules

1. **Original Mod Author's Code & Prompts (原 Mod 開發者的原始代碼、架構與 Prompt - Anton / Trax)**:
   - **嚴禁擅自直接改動**。
   - **必須以完整 Diff 格式明確列出修改前（Before / 原作者設計）與修改後（After / 擬議修改）的具體差異點**。
   - 必須清晰說明修改原由與架構演進脈絡（為何原作者的設計需要調整）。
   - **必須等待使用者審閱 Diff 並發出明確同意指令後，始得著手執行修改。**

2. **Our Own Newly Added Code & Modules (我們自己編寫/擴充的新程式碼模組)**:
   - **依然必須獲得使用者明確同意後，始得著手修改代碼（嚴禁未經同意擅自改動現有檔案）。**
   - **但無需列出完整繁瑣的 Code Diff**，只需以簡明清晰的文字重點說明修改方案與預期效果，徵詢使用者同意即可。

3. **Mandatory Git Branch Verification Before Edits (修改檔案前必先確認 Git 分支)**:
   - 在執行任何代碼修改或檔案寫入動作前，**必須先確認當前所處的 Git 分支是否正確**（例如對應的 `feat/*` 功能分支）。
   - 嚴禁在未切換至目標分支前直接在 `master` 或其他無關分支上進行修改。
   - 若不確定目標分支或尚未指定分支，**必須先主動詢問使用者確認目標分支**，確認並切換完成後始得動工。

4. **Discussion vs. Implementation Boundary**:
   - 於需求分析、邊界情況探討、數學建模或方案設計階段，必須嚴格保持在 **Read-Only / Advisory Mode**（唯讀諮詢模式）。
   - 必須獲得使用者明確同意指令（如「好」、「可以」、「請修改」、「請開始」、「同意」）後，始得執行代碼修改與檔案寫入。
