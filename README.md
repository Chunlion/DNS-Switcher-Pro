# DNS Switcher Pro

> English | [中文](#中文)

DNS Switcher Pro is a lightweight Windows desktop utility for switching DNS servers quickly. It provides a clean Electron interface for selecting a network adapter, applying DNS presets, testing latency, adding custom DNS entries, and reordering adapters or presets.

## Features

- One-click DNS switching for Windows
- Built-in presets for Cloudflare, AliDNS, DNSPod, 114DNS, Google DNS, and Baidu DNS
- Custom DNS presets
- Drag-and-drop sorting for network adapters and DNS presets
- Latency testing
- English and Chinese UI
- Portable Windows executable
- Manual command generation for Windows, macOS, and Linux

## Download

Download the portable Windows build from the latest release:

`DNS Switcher Pro 1.0.0.exe`

## Windows Permission Notes

Changing system DNS requires administrator permission on Windows. The app opens normally in limited mode, and DNS changes are attempted through a temporary elevated helper. If Windows UAC notifications are disabled and the helper is refused, run the app as administrator or use the generated manual commands.

## Development

```powershell
npm.cmd install
npm.cmd run dev
```

## Build

```powershell
npm.cmd install
npm.cmd run dist
```

The portable executable is generated in `dist/`.

## Project Structure

- `src/` - React renderer UI
- `electron/` - Electron main process and preload bridge
- `build/` - application icon assets
- `dist/` - local build output, ignored by git except for release uploads

## License

MIT

---

# 中文

> [English](#dns-switcher-pro) | 中文

DNS Switcher Pro 是一个轻量级 Windows 桌面 DNS 切换工具。它基于 Electron 构建，支持选择网卡、应用 DNS 预设、测试延迟、添加自定义 DNS，并可以对网卡列表和 DNS 预设进行拖拽排序。

## 功能

- Windows 下一键切换 DNS
- 内置 Cloudflare、阿里 DNS、DNSPod、114DNS、Google DNS、百度 DNS 等预设
- 支持自定义 DNS 预设
- 支持网卡列表和 DNS 预设拖拽排序
- 支持 DNS 延迟测试
- 支持中英文界面切换
- 提供 Windows 绿色版可执行文件
- 支持生成 Windows、macOS、Linux 手动命令

## 下载

从最新 Release 下载 Windows 绿色版：

`DNS Switcher Pro 1.0.0.exe`

## Windows 权限说明

Windows 修改系统 DNS 需要管理员权限。应用本身会以普通模式正常打开；如果处于受限模式，点击应用 DNS 时会尝试通过临时管理员辅助脚本执行修改。如果你的 UAC 通知已关闭并且系统拒绝辅助脚本，请以管理员身份运行应用，或使用界面生成的手动命令。

## 开发

```powershell
npm.cmd install
npm.cmd run dev
```

## 构建

```powershell
npm.cmd install
npm.cmd run dist
```

绿色版可执行文件会生成在 `dist/` 目录。

## 项目结构

- `src/` - React 渲染进程界面
- `electron/` - Electron 主进程和 preload 桥接
- `build/` - 应用图标资源
- `dist/` - 本地构建输出，源码仓库忽略该目录，Release 上传时使用

## 许可证

MIT
