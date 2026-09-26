---
name: feature-logic-tracer
description: 針對指定功能或 Entry Point 逆向追蹤既有程式碼，先建立功能整體理解與分析範圍，再拆解 Logic Sections、Rules 與 Source Evidence，計算 Analysis Coverage、Logic Weight、Code Share 與 Evidence LOC Coverage，最後直接產生 LogicTrace 相容的實體 JSON 檔案。不得只在對話中輸出 JSON，也不得修改 Production Source Code。
---

# Logic Inventory

本 Skill 用於逆向盤點既有程式的一個指定功能。

最終目的不是產生一堆彼此孤立的規則。

必須建立：

```text
Feature Overview
       ↓
Logic Sections
       ↓
Rules
       ↓
Source Evidence
```

並同時回答：

```text
這個功能在做什麼？
哪些地方是主要邏輯？
每段邏輯約占整體多少？
目前盤點了多少？
哪些地方還沒有確認？
實際證據在哪些程式碼？
```

分析完成後：

> 必須直接建立 LogicTrace JSON 實體檔案。

不得只把 JSON 顯示在對話中。

---

# 1. 分析範圍

從使用者指定的 Entry Point 開始。

例如：

* Event Handler
* API Endpoint
* Public Method
* Controller Action
* Service Method
* Command Handler
* Scheduled Job

只分析與指定 Feature 有直接關係的程式。

不要擴張成整個 Solution 的全面盤點。

---

# 2. 第一階段：先探索 Feature Scope

不要一開始就建立 Rule。

第一步先探索：

```text
Entry Point
↓
直接呼叫
↓
重要 Service
↓
Business Logic
↓
Repository / Data Access
↓
External Interaction
↓
回傳 / UI Update
```

找出此次 Feature 涉及的重要程式區域。

建立：

```text
Feature Scope
```

Feature Scope 用於後續判斷：

* 是否盤點完整
* Effective LOC
* Evidence LOC Coverage
* Unresolved Area

---

# 3. 不得只根據 Method Name 判斷行為

例如看到：

```text
ValidateOrder()
```

不能直接寫：

```text
驗證訂單狀態與權限。
```

必須實際閱讀 Implementation。

Method Name：

> 只能當成追蹤線索。

不能當成 Evidence。

---

# 4. 追蹤重要 Method Call

如果目前 Method 將重要行為委派出去：

必須繼續追蹤 Implementation。

例如：

```text
Form
↓
OrderService.Cancel
↓
Validate
↓
Repository.Update
↓
Transaction
```

不得只停在：

```text
_orderService.Cancel(id);
```

然後猜測 Cancel 做了什麼。

---

# 5. 避免無限展開

以下通常不需要深入：

* String.Trim
* ToString
* 單純 Logging
* 簡單 Mapping
* 無關 Utility

判斷原則：

> 此 Method 的內部行為是否會實質影響我們對這個 Feature 的理解？

如果不會：

停止展開。

---

# 6. 第二階段：建立 Feature Overview

充分探索 Feature Scope 後，才建立 Overview。

Feature 至少包含：

```text
Name
Purpose
Overview
EntryPoint
MainFlow
Components
Metrics
```

Overview 必須：

> 讓沒有閱讀 Source Code 的工程師，在短時間內理解這個功能大致如何運作。

不要把 Overview 寫成程式碼流水帳。

---

# 7. Overview 必須建立 References

Overview 中的重要邏輯描述應該能被 LogicTrace 點擊。

因此 Overview 必須輸出：

```text
Text
References[]
```

Reference：

```text
Text
TargetType
TargetId
```

`Reference.Text` 必須是 `Overview.Text` 中：

* 完全相同
* 連續
* 非空白

的原文片段。

必須先完成 `Overview.Text`，再直接從其中複製文字建立 Reference。

不得改寫、縮寫、跨句拼接，或使用只在語意上相近但字面不同的文字。

如果後續修改 `Overview.Text`：

> 必須重新檢查所有 References。

TargetType：

```text
LogicSection
Rule
Evidence
```

例如：

Overview：

```text
系統收到取消要求後，先驗證訂單，
再更新訂單狀態，
寫入取消紀錄，
最後重新整理畫面。
```

應建立 References：

```text
驗證訂單
→ S002

更新訂單狀態
→ R006

寫入取消紀錄
→ R007

重新整理畫面
→ R010
```

Overview 不得嵌入 HTML。

---

# 8. 第三階段：建立 Main Flow

將整個功能整理成少量具有實際意義的主要流程。

例如：

```text
1. 取得取消要求
2. 驗證訂單
3. 執行取消
4. 儲存資料
5. 更新畫面
```

不要把每個 Method 都變成 Main Flow。

Main Flow 描述的是：

> Feature 的主要執行階段。

---

# 9. 第四階段：建立 Logic Section

依 Main Flow 建立 Logic Section。

每個 Section：

```text
Id
Sequence
Title
Description
Metrics
Rules
```

Section 必須具有清楚的邏輯目的。

不得只是：

```text
呼叫 Method A
呼叫 Method B
呼叫 Method C
```

---

# 10. 第五階段：建立 Rules

每個 Rule 必須：

* 隸屬某個 Logic Section
* 代表具體程式行為或業務邏輯
* 至少具有 Evidence，或明確標示 NeedsInvestigation

適合建立 Rule：

* Input
* Validation
* Business Rule
* Branch
* Permission
* State Transition
* Database Query
* Data Mutation
* Transaction
* Exception Path
* External Call
* Output
* UI Update

避免把以下瑣碎內容獨立當 Rule：

```text
建立 List
設定暫存變數
Trim
ToString
一般 Assignment
```

除非它實際影響 Feature 行為。

---

# 11. Evidence

每個 Rule 可以有多筆 Evidence。

Evidence：

```text
Id
FilePath
ClassName
MethodName
StartLine
EndLine
Reason
```

FilePath 必須使用：

> Repository Root Relative Path。

不得寫 Absolute Path。

---

# 12. Line Range

Line Number：

* 1-based
* 必須對應實際檔案
* 儘量精準
* 不要無意義地包含整個檔案

Evidence 應指出：

> 真正支撐 Rule 的程式區域。

---

# 13. Evidence Level

每個 Rule 設定：

```text
Observed
Inferred
Unknown
```

## Observed

程式碼直接證明。

## Inferred

由多段已觀察到的程式碼合理推論。

必須清楚說明推論依據。

## Unknown

目前無法確認。

不得猜測。

---

# 14. Confidence

設定：

```text
High
Medium
Low
```

代表分析器對該 Rule 的信心。

不是 Human Verification。

---

# 15. Analysis Status

設定：

```text
Confirmed
NeedsInvestigation
```

遇到：

* External DLL
* Reflection
* Runtime DI
* Dynamic Dispatch
* 找不到 Implementation
* Source Missing
* Generated Runtime Behavior

如果無法確認：

設定：

```text
NeedsInvestigation
```

並記錄原因。

---

# 16. Verification Status

所有新產生 Rule：

```text
verificationStatus = Unverified
```

本 Skill 不得自行設定：

```text
Verified
Incorrect
NeedsReview
```

這是 LogicTrace 裡的人工作業。

---

# 17. Unresolved Items

所有未能合理完成分析的重要區域，都必須建立：

```text
UnresolvedItem
```

至少包含：

```text
Id
Title
Reason
EstimatedWeight
RelatedSectionId
```

不得單純忽略未確認程式。

---

# 18. Logic Weight

每個 Logic Section 必須具有：

```text
Logic Weight
```

表示：

> 此 Section 約占整個 Feature 邏輯量的比例。

所有 Section 的 Logic Weight 加總：

```text
100%
```

Logic Weight 不得純粹隨意猜測。

可依以下資訊評估：

```text
Rule Count
Branch Count
Business Rule Count
Data Mutation
Repository Interaction
External Interaction
Transaction
Exception / Alternate Path
```

使用一致標準比較各 Section。

Logic Weight 是：

> 相對邏輯複雜度與功能占比估計。

不是 LOC。

---

# 19. Effective LOC

分析 Feature Scope 時，計算：

```text
Effective LOC
```

盡量排除：

* 空白行
* 純註解
* 單獨 `{`
* 單獨 `}`
* Generated Code

不要求靜態分析器等級精準。

但整份分析必須採用一致規則。

---

# 20. Unique LOC

計算 LOC 時：

> 同一個實際 Source Line 不得重複計算。

例如：

```text
OrderService.cs:80-100
```

同時被 R001 與 R002 引用：

Feature LOC 仍只計算一次。

不得變成兩倍。

---

# 21. Code Share

每個 Logic Section 必須計算：

```text
Code Share
```

公式：

```text
Section Effective Unique LOC
/
Feature Scope Effective Unique LOC
× 100
```

例如：

```text
Feature Scope = 500 LOC
Validation = 150 LOC

Code Share = 30%
```

Code Share 與 Logic Weight 必須分開。

---

# 22. Evidence LOC Coverage

計算：

```text
Evidence LOC Coverage
```

公式：

```text
Unique Effective LOC Covered By Evidence
/
Feature Scope Effective Unique LOC
× 100
```

它回答：

> 此次認定屬於 Feature Scope 的程式碼，有多少實際被 Rule Evidence 覆蓋？

---

# 23. Section Coverage

每個 Logic Section 需要一個：

```text
Coverage
```

代表：

> 此 Section 中已經得到合理分析與 Evidence 支持的程度。

Section Coverage 必須考慮：

* Confirmed Rule
* NeedsInvestigation
* Unknown
* Unresolved Area

不得直接憑感覺寫百分比。

---

# 24. Analysis Coverage

Feature 層級計算：

```text
Analysis Coverage
```

建議使用：

```text
Σ(
  LogicSection LogicWeight
  ×
  LogicSection Coverage
)
```

結果 Normalize 為：

```text
0-100%
```

例如：

```text
取得輸入
Weight 10%
Coverage 100%

驗證
Weight 30%
Coverage 100%

資料異動
Weight 40%
Coverage 70%

更新畫面
Weight 20%
Coverage 100%
```

Overall Analysis Coverage：

```text
88%
```

---

# 25. Coverage 不是 Code Coverage

輸出欄位必須叫：

```text
analysisCoverage
```

不得叫：

```text
codeCoverage
```

避免與 Test Code Coverage 混淆。

---

# 26. Feature Metrics

Feature Metrics：

```text
analysisCoverage
evidenceLocCoverage
totalEffectiveLoc
unresolvedWeight
```

---

# 27. Section Metrics

Section Metrics：

```text
logicWeight
codeShare
coverage
effectiveLoc
```

---

# 28. JSON Schema

輸出必須符合：

```json
{
  "schemaVersion": "2.0",

  "feature": {
    "name": "",
    "purpose": "",

    "overview": {
      "text": "",
      "references": [
        {
          "text": "",
          "targetType": "LogicSection",
          "targetId": ""
        }
      ]
    },

    "entryPoint": {
      "filePath": "",
      "className": "",
      "methodName": ""
    },

    "mainFlow": [],

    "components": [],

    "metrics": {
      "analysisCoverage": 0,
      "evidenceLocCoverage": 0,
      "totalEffectiveLoc": 0,
      "unresolvedWeight": 0
    }
  },

  "logicSections": [
    {
      "id": "S001",
      "sequence": 1,
      "title": "",
      "description": "",

      "metrics": {
        "logicWeight": 0,
        "codeShare": 0,
        "coverage": 0,
        "effectiveLoc": 0
      },

      "rules": [
        {
          "id": "R001",
          "sequence": 1,
          "title": "",
          "description": "",
          "category": "",
          "evidenceLevel": "Observed",
          "confidence": "High",
          "analysisStatus": "Confirmed",
          "verificationStatus": "Unverified",

          "evidences": [
            {
              "id": "E001",
              "filePath": "",
              "className": "",
              "methodName": "",
              "startLine": 1,
              "endLine": 1,
              "reason": ""
            }
          ]
        }
      ]
    }
  ],

  "unresolvedItems": [
    {
      "id": "U001",
      "title": "",
      "reason": "",
      "estimatedWeight": 0,
      "relatedSectionId": ""
    }
  ]
}
```

不得任意改欄位名稱。

---

# 29. JSON 必須直接寫成實體檔案

這是強制規則。

分析完成後：

> 不得只在 Codex 回覆中顯示 JSON。

必須實際寫入 Repository。

預設目錄：

```text
.docs/
```

如果不存在：

自動建立。

---

# 30. 檔名

預設：

```text
{feature-name}.logictrace.json
```

使用適合 File System 的 kebab-case。

例如：

```text
Cancel Order
```

輸出：

```text
.docs/cancel-order.logictrace.json
```

使用者若指定路徑：

> 使用指定路徑優先。

---

# 31. 已存在 JSON

如果目標檔案已存在：

首先檢查：

```text
verificationStatus
```

如果所有 Rule 都仍是：

```text
Unverified
```

可以直接重新產生。

如果已有：

```text
Verified
Incorrect
NeedsReview
```

代表有人工作業。

不得靜默覆蓋。

應：

* 保留人工 Verification Status，或
* 建立新的輸出檔案

不得丟失人工驗證結果。

---

# 32. 寫檔後驗證

產生檔案後必須再次：

1. 讀取剛產生的 JSON。
2. 確認為合法 JSON。
3. 確認必要欄位存在。
4. 確認 Logic Weight 合計合理。
5. 確認每筆 `References[].Text` 非空白，且 `Overview.Text` 以完全相同的連續文字包含該值。
6. 確認 References 的 TargetId 存在，且符合 TargetType。
7. 確認 Evidence FilePath 存在。
8. 確認 Evidence Line Range 合法。
9. 確認實體檔案已成功建立。

Reference 文字驗證必須等價於：

```text
Reference.Text is not empty
AND
Overview.Text.Contains(Reference.Text, Ordinal)
```

任一 Reference 無法通過時：

> 驗證失敗，不得回報盤點完成。

只有全部完成才算成功。

---

# 33. 完成後回覆

完成後不要再把完整 JSON 貼到 Codex 回覆。

只簡短回覆：

```text
盤點完成。

Feature:
取消訂單

Logic Sections:
5

Rules:
14

Analysis Coverage:
87%

Evidence LOC Coverage:
81%

Effective LOC:
486

Needs Investigation:
2

輸出：
.docs/cancel-order.logictrace.json
```

---

# 34. 禁止事項

不得：

* 修改 Production Source Code
* Refactor
* 修 Bug
* 做 Code Review
* 改命名
* 提供架構改善
* 重寫功能
* 為了提高 Coverage 而虛構 Rule
* 為了提高 Coverage 而把 Unknown 當成 Observed

本 Skill 的責任只有：

```text
探索 Feature Scope
↓
理解整體功能
↓
建立 Main Flow
↓
建立 Logic Sections
↓
建立 Rules
↓
建立 Evidence
↓
標示 Unresolved
↓
計算 Metrics
↓
產生實體 LogicTrace JSON
```

---

# 35. 最終品質原則

永遠遵守：

> 寧可 Coverage 較低，也不要假裝分析完整。

> 寧可標示 NeedsInvestigation，也不要根據名稱猜測。

> 每一個確定性的邏輯結論，都必須可以回到實際程式碼證據。
