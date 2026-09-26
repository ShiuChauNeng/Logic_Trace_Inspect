# LogicTrace

LogicTrace 是讀取 LogicTrace 2.0 JSON 的 WinForms 桌面工具。它提供 Feature Overview、可點擊邏輯連結、Section / Rule / Evidence 導覽、人工驗證狀態，以及實際 Source Code Evidence 檢視。

## 執行

```powershell
dotnet run --project .\LogicTrace\LogicTrace.csproj
```

在程式內：

1. Project Root 選擇 `Examples\LegacySystem`。
2. 開啟 `Examples\LegacySystem\logictrace.sample.json`。
3. 點 Overview 藍色連結，或從左側 TreeView 下鑽。
4. 在 Rule 頁修改 Verification Status，按 `Ctrl+S` 寫回 JSON。

## 驗證

```powershell
dotnet build .\LogicTrace.sln
dotnet run --project .\LogicTrace\LogicTrace.csproj -- --self-test .\Examples\LegacySystem\logictrace.sample.json
```

Smoke test 會驗證 JSON、Overview Link 目標、Tree 所需資料、所有 Evidence 原始碼範圍，以及儲存後重新讀取。
