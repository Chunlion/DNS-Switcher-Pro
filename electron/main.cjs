const { app, BrowserWindow, Menu, ipcMain } = require("electron");
const path = require("path");
const os = require("os");
const fs = require("fs");
const { execFile, execFileSync } = require("child_process");
const { promisify } = require("util");

const execFileAsync = promisify(execFile);
const isDev = Boolean(process.env.ELECTRON_START_URL);
let mainWindow;

function log(message, error) {
  try {
    const details = error ? `\n${error.stack || error.message || String(error)}` : "";
    fs.appendFileSync(
      path.join(app.getPath("userData"), "dns-switcher.log"),
      `[${new Date().toISOString()}] ${message}${details}\n`,
      "utf8",
    );
  } catch {
    // Logging must never block startup.
  }
}

function run(command, args, options = {}) {
  return execFileAsync(command, args, {
    windowsHide: true,
    timeout: 15000,
    maxBuffer: 1024 * 1024,
    ...options,
  });
}

function isWindowsAdmin() {
  if (process.platform !== "win32") return true;
  try {
    execFileSync("net", ["session"], { stdio: "ignore", windowsHide: true });
    return true;
  } catch {
    return false;
  }
}

function getPrivilegeStatus() {
  return {
    platform: process.platform,
    isAdmin: isWindowsAdmin(),
    needsAdminForDns: process.platform === "win32",
  };
}

function isIPv4(value) {
  if (typeof value !== "string") return false;
  const parts = value.trim().split(".");
  return parts.length === 4 && parts.every((part) => {
    if (!/^\d+$/.test(part)) return false;
    const number = Number(part);
    return number >= 0 && number <= 255 && String(number) === part;
  });
}

function requireIPv4(value, label) {
  if (!isIPv4(value)) throw new Error(`${label} must be a valid IPv4 address.`);
}

function requireInterfaceName(value) {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error("Please choose a network interface first.");
  }
  return value.trim();
}

async function getInterfaces() {
  const interfaces = os.networkInterfaces();
  const platform = os.platform();
  const result = [];

  if (platform === "win32") {
    const script = [
      "$adapters = Get-NetAdapter | Select-Object Name, InterfaceDescription, Status;",
      "Get-NetIPConfiguration | Where-Object { $_.IPv4Address -and $_.NetAdapter.Status -ne 'Disabled' } | ForEach-Object {",
      "$alias = $_.InterfaceAlias;",
      "$adapter = $adapters | Where-Object { $_.Name -eq $alias } | Select-Object -First 1;",
      "[PSCustomObject]@{",
      "Name = $alias;",
      "InterfaceDescription = if ($adapter) { $adapter.InterfaceDescription } else { $alias };",
      "Status = if ($adapter) { $adapter.Status } else { '' };",
      "Address = @($_.IPv4Address | Select-Object -ExpandProperty IPAddress)[0];",
      "Dns = @($_.DNSServer.ServerAddresses | Where-Object { $_ -match '^\\d+\\.\\d+\\.\\d+\\.\\d+$' })",
      "}",
      "} | ConvertTo-Json -Depth 5",
    ].join(" ");
    const { stdout } = await run("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script]);
    const parsed = stdout.trim() ? JSON.parse(stdout) : [];
    const adapters = Array.isArray(parsed) ? parsed : [parsed];

    for (const adapter of adapters) {
      if (!adapter?.Name || !adapter?.Address) continue;
      const dns = adapter.Dns ? (Array.isArray(adapter.Dns) ? adapter.Dns : [adapter.Dns]) : [];
      result.push({
        name: adapter.Name,
        description: adapter.InterfaceDescription || adapter.Name,
        status: adapter.Status || "",
        address: adapter.Address,
        currentDns: dns.filter(isIPv4),
      });
    }
  } else if (platform === "darwin") {
    const { stdout } = await run("networksetup", ["-listnetworkserviceorder"]);
    const serviceMap = {};
    let currentService = "";

    for (const line of stdout.split("\n")) {
      const serviceMatch = line.match(/^\(\d+\)\s+(.*)$/);
      const deviceMatch = line.match(/Device:\s+([^)]+)/);
      if (serviceMatch) currentService = serviceMatch[1];
      if (deviceMatch && currentService) serviceMap[deviceMatch[1]] = currentService;
    }

    for (const name of Object.keys(interfaces)) {
      const ipv4 = (interfaces[name] || []).find((item) => item.family === "IPv4");
      if (!ipv4 || ipv4.internal) continue;

      const serviceName = serviceMap[name] || name;
      let currentDns = [];
      try {
        const { stdout: dnsOut } = await run("networksetup", ["-getdnsservers", serviceName]);
        if (!dnsOut.includes("There aren't any DNS Servers")) {
          currentDns = dnsOut.split("\n").map((line) => line.trim()).filter(isIPv4);
        }
      } catch {
        currentDns = [];
      }

      result.push({
        name: serviceName,
        description: name,
        status: "",
        address: ipv4.address,
        currentDns,
      });
    }
  } else {
    for (const name of Object.keys(interfaces)) {
      const ipv4 = (interfaces[name] || []).find((item) => item.family === "IPv4");
      if (!ipv4 || ipv4.internal) continue;

      let currentDns = [];
      try {
        const { stdout } = await run("resolvectl", ["dns", name]);
        const match = stdout.match(/:\s+(.*)$/);
        if (match) currentDns = match[1].split(/\s+/).filter(isIPv4);
      } catch {
        currentDns = [];
      }

      result.push({
        name,
        description: name,
        status: "",
        address: ipv4.address,
        currentDns,
      });
    }
  }

  return result;
}

function getCommands({ interfaceName, primary, secondary }) {
  const iface = requireInterfaceName(interfaceName);
  requireIPv4(primary, "Primary DNS");
  if (secondary) requireIPv4(secondary, "Secondary DNS");

  const windows = [`netsh interface ipv4 set dns name="${iface}" static ${primary}`];
  if (secondary) windows.push(`netsh interface ipv4 add dns name="${iface}" ${secondary} index=2`);

  return {
    windows: [
      `Set-DnsClientServerAddress -InterfaceAlias "${iface}" -ServerAddresses (${secondary ? `"${primary}","${secondary}"` : `"${primary}"`})`,
      ...windows,
    ],
    macos: `networksetup -setdnsservers "${iface}" ${secondary ? `${primary} ${secondary}` : primary}`,
    linux: `resolvectl dns ${iface} ${secondary ? `${primary} ${secondary}` : primary}`,
  };
}

function quotePowerShellString(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

function getWindowsDnsScript(payload) {
  const addresses = [payload.primary, payload.secondary].filter(Boolean);
  const addressList = addresses.map(quotePowerShellString).join(",");

  return [
    "$ErrorActionPreference = 'Stop'",
    `$alias = ${quotePowerShellString(payload.interfaceName)}`,
    `$addresses = @(${addressList})`,
    "Set-DnsClientServerAddress -InterfaceAlias $alias -ServerAddresses $addresses",
    "$current = @(Get-DnsClientServerAddress -InterfaceAlias $alias -AddressFamily IPv4).ServerAddresses",
    "$missing = @($addresses | Where-Object { $current -notcontains $_ })",
    "if ($missing.Count -gt 0) { throw \"DNS verification failed. Current DNS: $($current -join ', ')\" }",
  ].join("; ");
}

async function runWindowsDns(payload) {
  await run("powershell.exe", [
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-Command",
    getWindowsDnsScript(payload),
  ], { timeout: 30000 });
}

async function runWindowsDnsElevated(payload) {
  const tempDir = path.join(app.getPath("temp"), "dns-switcher-pro");
  fs.mkdirSync(tempDir, { recursive: true });
  const scriptPath = path.join(tempDir, `set-dns-${Date.now()}.ps1`);
  fs.writeFileSync(scriptPath, getWindowsDnsScript(payload), "utf8");

  const psCommand = [
    "$p = Start-Process",
    "-FilePath 'powershell.exe'",
    `-ArgumentList ${JSON.stringify(`-NoProfile -ExecutionPolicy Bypass -File "${scriptPath}"`)}`,
    "-Verb RunAs",
    "-Wait",
    "-PassThru;",
    "exit $p.ExitCode",
  ].join(" ");

  try {
    await run("powershell.exe", [
      "-NoProfile",
      "-ExecutionPolicy",
      "Bypass",
      "-Command",
      psCommand,
    ], { timeout: 60000 });
  } catch (error) {
    const manualCommands = getCommands(payload).windows;
    const wrapped = new Error([
      "Windows refused the elevated DNS helper.",
      "Because UAC notifications are disabled on this PC, run the app as administrator or use the manual commands.",
      "",
      ...manualCommands,
    ].join("\n"));
    wrapped.cause = error;
    throw wrapped;
  } finally {
    try {
      fs.unlinkSync(scriptPath);
    } catch {
      // The elevated process can still hold the file briefly.
    }
  }
}

async function setDns(payload) {
  const iface = requireInterfaceName(payload.interfaceName);
  requireIPv4(payload.primary, "Primary DNS");
  if (payload.secondary) requireIPv4(payload.secondary, "Secondary DNS");

  const platform = os.platform();
  if (platform === "win32") {
    if (isWindowsAdmin()) {
      await runWindowsDns({ ...payload, interfaceName: iface });
    } else {
      await runWindowsDnsElevated({ ...payload, interfaceName: iface });
    }
  } else if (platform === "darwin") {
    const servers = payload.secondary ? [payload.primary, payload.secondary] : [payload.primary];
    await run("networksetup", ["-setdnsservers", iface, ...servers]);
  } else if (platform === "linux") {
    const servers = payload.secondary ? [payload.primary, payload.secondary] : [payload.primary];
    await run("resolvectl", ["dns", iface, ...servers]);
  } else {
    throw new Error(`Unsupported platform: ${platform}`);
  }

  return { success: true };
}

async function pingDns(ip) {
  requireIPv4(ip, "DNS server");
  const platform = os.platform();
  const args = platform === "win32"
    ? ["-n", "1", "-w", "2000", ip]
    : ["-c", "1", "-W", platform === "darwin" ? "2000" : "2", ip];

  try {
    const { stdout } = await run("ping", args, { timeout: 3000 });
    const match = platform === "win32"
      ? stdout.match(/(?:time|时间)[=<](\d+)\s*ms/i)
      : stdout.match(/time=([\d.]+)\s*ms/i);
    return { ip, latency: match ? Number(match[1]) : null };
  } catch {
    return { ip, latency: null };
  }
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1180,
    height: 780,
    minWidth: 940,
    minHeight: 640,
    title: "DNS Switcher Pro",
    icon: path.join(__dirname, "..", "build", "icon.ico"),
    backgroundColor: "#f5f5f5",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (isDev) {
    mainWindow.loadURL(process.env.ELECTRON_START_URL);
    mainWindow.webContents.openDevTools({ mode: "detach" });
  } else {
    mainWindow.loadFile(path.join(__dirname, "..", "dist", "renderer", "index.html"));
  }

  mainWindow.webContents.on("did-fail-load", (_event, code, description, url) => {
    log(`Window failed to load: ${code} ${description} ${url}`);
  });

  mainWindow.webContents.on("render-process-gone", (_event, details) => {
    log(`Renderer process gone: ${JSON.stringify(details)}`);
  });

  mainWindow.webContents.on("console-message", (_event, level, message, line, sourceId) => {
    if (level >= 2) log(`Renderer console: ${message} (${sourceId}:${line})`);
  });
}

app.whenReady()
  .then(() => {
    Menu.setApplicationMenu(null);
    ipcMain.handle("dns:getPrivilegeStatus", getPrivilegeStatus);
    ipcMain.handle("dns:getInterfaces", getInterfaces);
    ipcMain.handle("dns:getCommands", (_event, payload) => getCommands(payload));
    ipcMain.handle("dns:set", (_event, payload) => setDns(payload));
    ipcMain.handle("dns:ping", (_event, ip) => pingDns(ip));
    createWindow();

    app.on("activate", () => {
      if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
  })
  .catch((error) => {
    log("Startup failed", error);
    console.error("Startup failed", error);
  });

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
