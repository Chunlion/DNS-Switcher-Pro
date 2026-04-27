# DNS Switcher Pro

DNS Switcher Pro is a small Windows desktop utility for switching DNS servers quickly. It provides a clean Electron interface for selecting a network adapter, applying DNS presets, testing latency, adding custom DNS entries, and reordering adapters or presets.

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

Use the portable Windows build from the latest release:

`DNS Switcher Pro 1.0.8.exe`

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
