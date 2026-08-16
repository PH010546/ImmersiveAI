---
name: verify-native-code-and-honesty
description: Strict rules enforcing direct binary/IL decompilation verification for game APIs and absolute honesty without deception or unverified claims.
trigger: always_on
---

# Native Code Verification & Absolute Honesty Guardrail

1. **實證與反編譯原則 (Evidence-Based Native Verification)**:
   - 凡是涉及 TaleWorlds / 遊戲原版底層機制的實作、對接與除錯，**嚴禁依靠猜測、常理推想或未經驗證的經驗寫代碼**。
   - 必須直接利用反射（Reflection）或 IL 字節碼解析腳本，對遊戲安裝目錄下的真實 DLL（如 `TaleWorlds.CampaignSystem.dll` 等）進行實際反編譯與底層調用鏈分析，以實機二進制代碼為唯一依據。

2. **誠實與透明原則 (Absolute Honesty & No Deception)**:
   - **嚴禁**聲稱自己已執行了未實際執行的動作（如未反編譯卻聲稱已反編譯）。
   - **嚴禁**用虛假的藉口（如「缺少外部反編譯工具所以做不到」）掩蓋自身局限或推託問題。
   - 遇到任何環境限制、代碼阻礙或不確定性時，必須第一時間誠實且具體地向使用者說明真相，行就是行，不行就清楚說明，絕不提供誤導性資訊。
