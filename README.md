# DNS Switcher Pro

> English | [中文](#中文)

DNS Switcher Pro is a tiny native Windows desktop utility for switching DNS servers quickly. The release executable is built with .NET Framework WinForms and is about 25 KB, with no Electron or Chromium runtime.

## Features

- Switch DNS for the selected Windows network adapter
- Built-in presets for Cloudflare, AliDNS, DNSPod, 114DNS, Google DNS, and Baidu DNS
- Custom DNS presets
- Adapter and preset ordering
- English and Chinese UI
- Manual PowerShell command display
- Native portable `.exe`

## Download

Download the portable Windows build from the latest release:

`DNS Switcher Pro 1.0.exe`

## Windows Permission Notes

Changing system DNS requires administrator permission on Windows. If the app is not running as administrator, Windows may prompt for elevation when applying DNS. If UAC notifications are disabled and Windows refuses elevation, run the app as administrator or use the generated manual command.

## Build

No Node.js or npm is required for the lightweight version. On Windows:

```powershell
.\build-native.ps1
```

The executable is generated in `native-dist/`.

## Project Structure

- `native-windows/` - C# WinForms source code
- `build/` - application icon assets
- `native-dist/` - local build output, ignored by git except for release uploads

## License

MIT

---

# 中文

> [English](#dns-switcher-pro) | 中文

DNS Switcher Pro 是一个轻量级原生 Windows 桌面 DNS 切换工具。发布版使用 .NET Framework WinForms 构建，体积约 25 KB，不包含 Electron 或 Chromium 运行时。

## 功能

- 为选中的 Windows 网卡切换 DNS
- 内置 Cloudflare、阿里 DNS、DNSPod、114DNS、Google DNS、百度 DNS 等预设
- 支持自定义 DNS 预设
- 支持调整网卡和 DNS 预设顺序
- 支持中英文界面
- 支持显示手动 PowerShell 命令
- 原生绿色版 `.exe`

## 下载

从最新 Release 下载 Windows 绿色版：

`DNS Switcher Pro 1.0.exe`

## Windows 权限说明

Windows 修改系统 DNS 需要管理员权限。如果应用不是以管理员身份运行，应用 DNS 时 Windows 可能会请求提权。如果你的 UAC 通知已关闭并且系统拒绝提权，请以管理员身份运行应用，或使用界面生成的手动命令。

## 构建

轻量版不需要 Node.js 或 npm。在 Windows 上运行：

```powershell
.\build-native.ps1
```

可执行文件会生成在 `native-dist/` 目录。

## 项目结构

- `native-windows/` - C# WinForms 源码
- `build/` - 应用图标资源
- `native-dist/` - 本地构建输出，源码仓库忽略该目录，Release 上传时使用

## 许可证

MIT
