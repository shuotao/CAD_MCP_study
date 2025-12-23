# AutoCAD MCP - AI 驅動的建築繪圖助手 (.NET Add-in 版)

> ⚠️ 注意：本專案僅支援 **AutoCAD 完整版** (2021+)。
> ⚠️ 本機開發備註：因權限限制，本專案提供 `build_release.ps1` 供製作攜帶版安裝包。

本專案參考 **Revit MCP** 架構，採用 Socket 通訊模式 (Port 8964)。AI 透過 MCP Server 發送指令，由 C# Add-in 在 AutoCAD 內部執行。

---

## 🏗️ 系統架構

```
Claude/Gemini (AI)
      ↓ MCP Protocol
MCP-Server (Python Client)
      ↓ Socket (127.0.0.1:8964)
AutoCADMCP.dll (C# Server)
      ↓ .NET API
AutoCAD
```

## 📁 主要檔案

- `AutoCAD-MCP-Addin/`: C# 插件原始碼
- `MCP-Server/`: Python MCP 伺服器
- `install.ps1`: 自動安裝腳本 (需本機權限)
- `build_release.ps1`: **[推薦]** 製作發佈包腳本 (可複製到其他電腦)

---

## 📦 部署指南 (針對權限受限環境)

若您無法在本機執行 `install.ps1`，請使用以下方式製作安裝包：

### 1. 製作安裝包
執行 `build_release.ps1`：
```powershell
.\build_release.ps1
```
這會在專案目錄下產生 `Output\AutoCADMCP.bundle` 資料夾。

### 2. 手動安裝 (在目標電腦上)
將 `Output\AutoCADMCP.bundle` 資料夾完整複製到以下任一路徑：
- **目前使用者**: `%APPDATA%\Autodesk\ApplicationPlugins\`
- **所有使用者**: `%ProgramData%\Autodesk\ApplicationPlugins\` (需管理員權限)

### 3. 設定 MCP Server
在目標電腦上設定 Python 環境：
```powershell
cd MCP-Server
pip install -r requirements.txt
```

---

## 🚀 啟動流程

1. **啟動 AutoCAD**：Ribbon 會出現 "MCP Tools"。
2. **啟動監聽**：點擊 "Start Server" 按鈕。
3. **設定 AI**：
   在 `claude_desktop_config.json` 中指向 `MCP-Server/server.py`。
4. **開始使用**：
   AI 現在可以透過指令控制 AutoCAD：

   | 指令 | 說明 | 參數 |
   |---|---|---|
   | `draw_wall` | 繪製雙線牆 | start, end, width |
   | `get_layers` | 查詢圖層 | 無 |
   | `find_overlaps` | 查詢重疊線段 | layer (optional) |
   | `clean_overlaps` | 刪除重疊短線 | layer (optional) |
   | `connect_lines` | 線段接合確保 | layer, tolerance |
   | `get_blocks_in_view` | 查詢視圖內圖塊 | 無 |
   | `rename_block` | 重新命名圖塊 | old_name, new_name |
   | `update_block_description` | 更新圖塊描述 | name, description |

---

## � 使用情境與指令範例

### 情境 1：圖面整理 - 清除重疊線段
> 適用：從其他軟體匯入的圖面，或經過多次編輯後產生的冗餘線段

```
用戶：幫我檢查目前圖面有沒有重疊的線段
AI：[呼叫 find_overlaps] 找到 23 組重疊線段對...

用戶：把這些重疊的短線段刪掉
AI：[呼叫 clean_overlaps] 已清除 18 條短重疊線段

用戶：順便把 A-WALL 圖層上斷開的線條接起來
AI：[呼叫 connect_lines layer="A-WALL" tolerance=5] 已接合 12 個端點 (容許值 5mm)
```

### 情境 2：圖塊標準化
> 適用：統一專案內圖塊命名規範，或為圖塊補充描述方便日後查詢

```
用戶：列出目前視圖內有哪些圖塊
AI：[呼叫 get_blocks_in_view] 找到以下圖塊：
    - "DOOR-01" (x15) - 描述: 無
    - "WIN_A" (x8) - 描述: 無
    - "CHAIR" (x20) - 描述: 辦公椅

用戶：把 DOOR-01 改名為 A-DOOR-SINGLE，並加上描述「單扇門 900x2100」
AI：[呼叫 rename_block] 已將 "DOOR-01" 改名為 "A-DOOR-SINGLE"
    [呼叫 update_block_description] 已更新描述為「單扇門 900x2100」
```

### 情境 3：建築圖層設定
> 適用：新建專案時快速建立標準圖層結構

```
用戶：幫我建立標準建築圖層，包含牆、門、窗、標註
AI：[呼叫 create_layer] 已建立：
    - A-WALL (紅色)
    - A-DOOR (青色)
    - A-WIND (藍色)
    - A-DIMS (綠色)

用戶：把結構圖層改成紅色
AI：[呼叫 set_layer_color layer="S-COLS" color=1] 已將 S-COLS 圖層顏色改為紅色
```

### 情境 4：繪製建築元素
> 適用：快速產生基本圖面元素

```
用戶：在 (0,0) 到 (6000,0) 畫一道 200mm 厚的牆
AI：[呼叫 draw_wall] 已繪製牆體，長度 6000mm，厚度 200mm

用戶：列出目前有幾個圖層
AI：[呼叫 get_layers] 目前有 8 個圖層：0, A-WALL, A-DOOR...
```

---


## �🛠️ 開發參考 (Revit MCP Style)

本專案代碼結構致敬 Revit MCP：
- **App.cs**: 實作 `IExtensionApplication`，負責 UI 初始化。
- **SocketServer.cs**: 非同步 TCP 監聽器，確保不卡住 AutoCAD 主執行緒。
- **CommandHandler.cs**: 集中管理所有指令邏輯，易於維護。
- **PackageContents.xml**: 標準 Autoloader 格式，支援隨插即用。

---

## 📄 授權
MIT License
