<div align="center">

![App Icon](Resources/Icon256.png)

# Project Reforged Manager

**A modern HD texture patch manager for Turtle WOW**

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

[English](#english) | [中文](#中文)

</div>

---

<a name="english"></a>

### 📋 Overview

**Project Reforged Manager** is a modern, user-friendly HD texture patch management tool designed specifically for [Turtle WOW](https://turtle-wow.org/) players. It simplifies the process of downloading, installing, and managing high-definition texture patches, letting you enjoy stunning visuals with just a few clicks.

### ✨ Features

- 🌐 **Multi-language Support** - 6 languages: English, 中文, Español, Deutsch, Português, Русский
- 📦 **One-Click Installation** - Download and install patches automatically from the official source
- 🔄 **Smart Version Management** - Automatically detects updates and keeps patches up to date
- 🎯 **Patch Dependencies** - Automatically checks and prompts for missing dependencies
- ⚡ **Enable/Disable Patches** - Toggle patches on/off without deleting files
- 💾 **Download Resume** - Supports pausing and resuming downloads
- 🧹 **Cache Management** - Clean up incomplete downloads to free disk space
- 🔍 **System Dependency Check** - Validates VanillaHelpers and DXVK installation

### 📥 Download

Download the latest release from the [Releases](https://github.com/Cliencer/TurtleWOW-HDTextureManager/releases) page.

**Requirements:**
- Windows 7 or later
- [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472) or later

### 🚀 Quick Start

1. **Download** the latest `TurtleWOW-HDTextureManager.exe` from Releases
2. **Run** the executable (no installation required)
3. **Set Game Path** - Click "Browse..." and select your `WoW.exe`
4. **Select Language** - Choose your preferred language from the dropdown
5. **Download Patches** - Browse modules and click "Download & Install"
6. **Enable Patches** - Toggle patches on/off as desired

### 📖 Usage Guide

#### Installing Patches
1. Select the module category (Core, Optional, Audio, Ultra HD)
2. Click the download button on the desired patch card
3. Wait for the download to complete
4. The patch will be automatically enabled

#### Managing Patches
- **Enable**: Click the toggle switch to activate a patch
- **Disable**: Click again to deactivate (patch file will be renamed with `_` prefix)
- **Update**: When a new version is available, click "Update to vX.X.X"

#### Clearing Cache
Click "Clear Cache" in settings to remove incomplete download files and free up disk space.

### 🛠️ Building from Source

```bash
# Clone the repository
git clone https://github.com/Cliencer/TurtleWOW-HDTextureManager.git

# Open the solution in Visual Studio
cd TurtleWOW-HDTextureManager
start TurtleWOW-HDTextureManager.sln

# Build the project (Release configuration)
# Output: bin\Release\TurtleWOW-HDTextureManager.exe
```

### 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### 🙏 Acknowledgments

- [Turtle WOW](https://turtle-wow.org/) - The best WoW private server
- [Project Reforged](https://www.reforged.moe/) - High-quality HD texture project
- [StormLib](https://github.com/ladislav-zezula/StormLib) - MPQ file handling library

---

<a name="中文"></a>

### 📋 简介

**Project Reforged 管理器**是一款专为[乌龟服(Turtle WOW)](https://turtle-wow.org/)玩家打造的现代化高清纹理补丁管理工具。它简化了高清纹理补丁的下载、安装和管理过程，让您只需点击几下即可享受惊艳的视觉效果。

### ✨ 功能特性

- 🌐 **多语言支持** - 支持 6 种语言：English, 中文, Español, Deutsch, Português, Русский
- 📦 **一键安装** - 自动从官方源下载并安装补丁
- 🔄 **智能版本管理** - 自动检测更新，保持补丁最新
- 🎯 **补丁依赖检查** - 自动检查并提示缺失的依赖项
- ⚡ **启用/停用补丁** - 无需删除文件即可切换补丁开关
- 💾 **断点续传** - 支持暂停和恢复下载
- 🧹 **缓存管理** - 清理未完成下载，释放磁盘空间
- 🔍 **系统依赖检查** - 验证 VanillaHelpers 和 DXVK 安装状态

### 📥 下载

从 [Releases](https://github.com/Cliencer/TurtleWOW-HDTextureManager/releases) 页面下载最新版本。

**系统要求：**
- Windows 7 或更高版本
- [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472) 或更高版本

### 🚀 快速开始

1. **下载** - 从 Releases 下载最新的 `TurtleWOW-HDTextureManager.exe`
2. **运行** - 直接运行可执行文件（无需安装）
3. **设置游戏路径** - 点击"浏览..."选择您的 `WoW.exe`
4. **选择语言** - 从下拉菜单选择偏好语言
5. **下载补丁** - 浏览模块列表，点击"下载安装"
6. **启用补丁** - 根据需要开启或关闭补丁

### 📖 使用指南

#### 安装补丁
1. 选择模块分类（核心模块、可选增强、音频增强、超高清材质）
2. 在目标补丁卡片上点击下载按钮
3. 等待下载完成
4. 补丁将自动启用

#### 管理补丁
- **启用**: 点击切换开关激活补丁
- **停用**: 再次点击关闭（补丁文件将被重命名为 `_` 前缀）
- **更新**: 当有新版本时，点击"更新到 vX.X.X"

#### 清理缓存
在设置中点击"清除缓存"按钮，可移除未完成的下载文件，释放磁盘空间。

### 🛠️ 从源码构建

```bash
# 克隆仓库
git clone https://github.com/Cliencer/TurtleWOW-HDTextureManager.git

# 在 Visual Studio 中打开解决方案
cd TurtleWOW-HDTextureManager
start TurtleWOW-HDTextureManager.sln

# 构建项目（Release 配置）
# 输出文件: bin\Release\TurtleWOW-HDTextureManager.exe
```

### 📝 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

### 🙏 致谢

- [Turtle WOW](https://turtle-wow.org/) - 最棒的魔兽世界私服
- [Project Reforged](https://www.reforged.moe/) - 高质量高清纹理项目
- [StormLib](https://github.com/ladislav-zezula/StormLib) - MPQ 文件处理库



