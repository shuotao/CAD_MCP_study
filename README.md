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
   AI 現在可以透過指令 (如 `draw_wall`) 控制 AutoCAD。

---

## 🛠️ 開發參考 (Revit MCP Style)

本專案代碼結構致敬 Revit MCP：
- **App.cs**: 實作 `IExtensionApplication`，負責 UI 初始化。
- **SocketServer.cs**: 非同步 TCP 監聽器，確保不卡住 AutoCAD 主執行緒。
- **CommandHandler.cs**: 集中管理所有指令邏輯，易於維護。
- **PackageContents.xml**: 標準 Autoloader 格式，支援隨插即用。

---

## 📄 授權
MIT License
