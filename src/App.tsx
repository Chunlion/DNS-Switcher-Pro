import { useEffect, useMemo, useState } from 'react';
import {
  Activity,
  Check,
  Copy,
  Gauge,
  Globe,
  GripVertical,
  Info,
  Monitor,
  Plus,
  RefreshCw,
  Settings,
  Shield,
  Terminal,
  Trash2,
  Wifi,
  Zap,
} from 'lucide-react';
import { AnimatePresence, motion } from 'motion/react';

interface DNSPreset {
  id: string;
  name: string;
  primary: string;
  secondary?: string;
  isCustom?: boolean;
}

const DEFAULT_PRESETS: DNSPreset[] = [
  { id: 'cloudflare', name: 'Cloudflare', primary: '1.1.1.1', secondary: '1.0.0.1' },
  { id: 'alidns', name: 'AliDNS', primary: '223.5.5.5', secondary: '223.6.6.6' },
  { id: 'dnspod', name: 'DNSPod', primary: '119.29.29.29', secondary: '119.28.28.28' },
  { id: '114dns', name: '114DNS', primary: '114.114.114.114', secondary: '114.114.115.115' },
  { id: 'google', name: 'Google DNS', primary: '8.8.8.8', secondary: '8.8.4.4' },
  { id: 'baidu', name: 'Baidu DNS', primary: '180.76.76.76' },
];

const STORAGE_PRESETS = 'dns_switcher_presets';
const STORAGE_INTERFACE = 'dns_switcher_selected_interface';
const STORAGE_INTERFACE_ORDER = 'dns_switcher_interface_order';
const STORAGE_LANGUAGE = 'dns_switcher_language';

type Language = 'en' | 'zh';

const TEXT = {
  en: {
    subtitle: 'Desktop DNS switching utility',
    desktopReady: 'Desktop Ready',
    bridgeMissing: 'Bridge Missing',
    adminMode: 'Admin mode',
    limitedMode: 'Limited mode',
    refresh: 'Refresh',
    testing: 'Testing',
    testLatency: 'Test latency',
    interfaces: 'Interfaces',
    dragToSort: 'Drag to sort',
    noInterfaces: 'No active interfaces found.',
    customDns: 'Custom DNS',
    namePlaceholder: 'Name, e.g. Office DNS',
    primaryPlaceholder: 'Primary DNS',
    secondaryPlaceholder: 'Secondary',
    savePreset: 'Save preset',
    dnsPresets: 'DNS presets',
    target: 'Target',
    noInterfaceSelected: 'No interface selected',
    commands: 'Commands',
    apply: 'Apply',
    manualCommands: 'Manual commands',
    close: 'Close',
    copy: 'Copy',
    footer: 'The app changes system DNS through native network commands. If Windows starts in Limited mode, Apply will try an elevated helper script; if UAC notifications are disabled and Windows refuses it, use Commands or run the app as administrator.',
    bridgeUnavailable: 'Desktop bridge is unavailable. Start the Electron app, not the browser page.',
    failedReadInterfaces: 'Failed to read network interfaces.',
    chooseInterface: 'Choose a network interface first.',
    invalidPrimary: 'Primary DNS must be a valid IPv4 address.',
    invalidSecondary: 'Secondary DNS must be a valid IPv4 address.',
    switched: 'Switched to',
    failedChange: 'Failed to change DNS. Run as administrator or use Commands.',
    failedCommands: 'Failed to generate commands.',
    enterName: 'Enter a name and primary DNS.',
  },
  zh: {
    subtitle: '桌面 DNS 快速切换工具',
    desktopReady: '桌面桥接正常',
    bridgeMissing: '桥接不可用',
    adminMode: '管理员模式',
    limitedMode: '受限模式',
    refresh: '刷新',
    testing: '测速中',
    testLatency: '测试延迟',
    interfaces: '网卡列表',
    dragToSort: '拖拽排序',
    noInterfaces: '没有找到可用网卡。',
    customDns: '自定义 DNS',
    namePlaceholder: '名称，例如公司 DNS',
    primaryPlaceholder: '主 DNS',
    secondaryPlaceholder: '备用 DNS',
    savePreset: '保存预设',
    dnsPresets: 'DNS 预设',
    target: '目标',
    noInterfaceSelected: '未选择网卡',
    commands: '命令',
    apply: '应用',
    manualCommands: '手动命令',
    close: '关闭',
    copy: '复制',
    footer: '应用会通过系统网络命令修改 DNS。Windows 在受限模式下点击应用会尝试管理员辅助脚本；如果你的 UAC 通知已关闭并且系统拒绝执行，请使用命令或以管理员身份运行应用。',
    bridgeUnavailable: '桌面桥接不可用。请启动 Electron 应用，而不是浏览器页面。',
    failedReadInterfaces: '读取网卡失败。',
    chooseInterface: '请先选择一个网卡。',
    invalidPrimary: '主 DNS 必须是有效 IPv4 地址。',
    invalidSecondary: '备用 DNS 必须是有效 IPv4 地址。',
    switched: '已切换到',
    failedChange: '修改 DNS 失败。请以管理员身份运行或使用命令。',
    failedCommands: '生成命令失败。',
    enterName: '请填写名称和主 DNS。',
  },
} satisfies Record<Language, Record<string, string>>;

function loadLanguage(): Language {
  return localStorage.getItem(STORAGE_LANGUAGE) === 'zh' ? 'zh' : 'en';
}

function isIPv4(value: string) {
  const parts = value.trim().split('.');
  return parts.length === 4 && parts.every((part) => {
    if (!/^\d+$/.test(part)) return false;
    const number = Number(part);
    return number >= 0 && number <= 255 && String(number) === part;
  });
}

function loadJsonArray(key: string) {
  try {
    const saved = localStorage.getItem(key);
    if (!saved) return [];
    const parsed = JSON.parse(saved);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function loadStoredPresets() {
  const parsed = loadJsonArray(STORAGE_PRESETS);
  const validPresets = parsed.filter((item): item is DNSPreset => (
    item
    && typeof item.id === 'string'
    && typeof item.name === 'string'
    && typeof item.primary === 'string'
    && (!item.secondary || typeof item.secondary === 'string')
  ));
  return validPresets.length > 0 ? validPresets : DEFAULT_PRESETS;
}

function normalizeInterfaces(value: unknown): NetworkInterfaceInfo[] {
  if (!Array.isArray(value)) return [];

  return value.filter((item): item is NetworkInterfaceInfo => (
    item
    && typeof item.name === 'string'
    && typeof item.description === 'string'
    && typeof item.address === 'string'
  )).map((item) => ({
    ...item,
    currentDns: Array.isArray(item.currentDns)
      ? item.currentDns.filter((dns): dns is string => typeof dns === 'string')
      : [],
  }));
}

function applyInterfaceOrder(items: NetworkInterfaceInfo[]) {
  const order = loadJsonArray(STORAGE_INTERFACE_ORDER).filter((item): item is string => typeof item === 'string');
  if (order.length === 0) return items;

  return [...items].sort((a, b) => {
    const indexA = order.indexOf(a.name);
    const indexB = order.indexOf(b.name);
    if (indexA === -1 && indexB === -1) return 0;
    if (indexA === -1) return 1;
    if (indexB === -1) return -1;
    return indexA - indexB;
  });
}

function moveItem<T>(items: T[], from: number, to: number) {
  const next = [...items];
  const moved = next[from];
  if (moved === undefined) return next;
  next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

function wait(ms: number) {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

export default function App() {
  const [interfaces, setInterfaces] = useState<NetworkInterfaceInfo[]>([]);
  const [selectedInterface, setSelectedInterface] = useState('');
  const [presets, setPresets] = useState<DNSPreset[]>(loadStoredPresets);
  const [latencies, setLatencies] = useState<Record<string, number | null>>({});
  const [commands, setCommands] = useState<DnsCommands | null>(null);
  const [activePreset, setActivePreset] = useState<DNSPreset | null>(null);
  const [customName, setCustomName] = useState('');
  const [customPrimary, setCustomPrimary] = useState('');
  const [customSecondary, setCustomSecondary] = useState('');
  const [busy, setBusy] = useState(false);
  const [testing, setTesting] = useState(false);
  const [copied, setCopied] = useState('');
  const [notice, setNotice] = useState<{ kind: 'success' | 'error' | 'info'; text: string } | null>(null);
  const [draggedInterfaceIndex, setDraggedInterfaceIndex] = useState<number | null>(null);
  const [draggedPresetIndex, setDraggedPresetIndex] = useState<number | null>(null);
  const [privilegeStatus, setPrivilegeStatus] = useState<PrivilegeStatus | null>(null);
  const [language, setLanguage] = useState<Language>(loadLanguage);
  const t = TEXT[language];

  const bridgeReady = Boolean(window.dnsBridge);
  const selectedInterfaceInfo = useMemo(
    () => interfaces.find((item) => item.name === selectedInterface),
    [interfaces, selectedInterface],
  );

  const showNotice = (kind: 'success' | 'error' | 'info', text: string) => {
    setNotice({ kind, text });
    window.setTimeout(() => setNotice(null), 4200);
  };

  const refreshInterfaces = async () => {
    if (!window.dnsBridge) {
      showNotice('error', t.bridgeUnavailable);
      return;
    }

    try {
      const data = applyInterfaceOrder(normalizeInterfaces(await window.dnsBridge.getInterfaces()));
      setInterfaces(data);
      const stored = localStorage.getItem(STORAGE_INTERFACE);
      const next = data.find((item) => item.name === stored)?.name || data[0]?.name || '';
      setSelectedInterface((current) => data.find((item) => item.name === current)?.name || next);
    } catch (error) {
      showNotice('error', error instanceof Error ? error.message : t.failedReadInterfaces);
    }
  };

  const refreshPrivilegeStatus = async () => {
    if (!window.dnsBridge) return;

    try {
      setPrivilegeStatus(await window.dnsBridge.getPrivilegeStatus());
    } catch {
      setPrivilegeStatus(null);
    }
  };

  useEffect(() => {
    refreshPrivilegeStatus();
    refreshInterfaces();
  }, []);

  useEffect(() => {
    localStorage.setItem(STORAGE_PRESETS, JSON.stringify(presets));
  }, [presets]);

  useEffect(() => {
    if (selectedInterface) localStorage.setItem(STORAGE_INTERFACE, selectedInterface);
  }, [selectedInterface]);

  useEffect(() => {
    localStorage.setItem(STORAGE_LANGUAGE, language);
  }, [language]);

  const handleInterfaceDrop = (targetIndex: number) => {
    if (draggedInterfaceIndex === null || draggedInterfaceIndex === targetIndex) return;
    const next = moveItem<NetworkInterfaceInfo>(interfaces, draggedInterfaceIndex, targetIndex);
    setInterfaces(next);
    localStorage.setItem(STORAGE_INTERFACE_ORDER, JSON.stringify(next.map((item) => item.name)));
    setDraggedInterfaceIndex(null);
  };

  const handlePresetDrop = (targetIndex: number) => {
    if (draggedPresetIndex === null || draggedPresetIndex === targetIndex) return;
    const next = moveItem<DNSPreset>(presets, draggedPresetIndex, targetIndex);
    setPresets(next);
    setDraggedPresetIndex(null);
  };

  const validatePreset = (preset: DNSPreset) => {
    if (!selectedInterface) return t.chooseInterface;
    if (!isIPv4(preset.primary)) return t.invalidPrimary;
    if (preset.secondary && !isIPv4(preset.secondary)) return t.invalidSecondary;
    return '';
  };

  const applyPreset = async (preset: DNSPreset) => {
    if (!window.dnsBridge) return showNotice('error', t.bridgeUnavailable);
    const validation = validatePreset(preset);
    if (validation) return showNotice('error', validation);

    setBusy(true);
    try {
      await window.dnsBridge.setDns({
        interfaceName: selectedInterface,
        primary: preset.primary,
        secondary: preset.secondary,
      });
      showNotice('success', `${t.switched} ${preset.name}.`);
      await wait(600);
      await refreshInterfaces();
    } catch (error) {
      showNotice('error', error instanceof Error ? error.message : t.failedChange);
    } finally {
      setBusy(false);
    }
  };

  const openCommands = async (preset: DNSPreset) => {
    if (!window.dnsBridge) return showNotice('error', t.bridgeUnavailable);
    const validation = validatePreset(preset);
    if (validation) return showNotice('error', validation);

    try {
      const data = await window.dnsBridge.getCommands({
        interfaceName: selectedInterface,
        primary: preset.primary,
        secondary: preset.secondary,
      });
      setActivePreset(preset);
      setCommands(data);
    } catch (error) {
      showNotice('error', error instanceof Error ? error.message : t.failedCommands);
    }
  };

  const testLatency = async () => {
    if (!window.dnsBridge) return showNotice('error', t.bridgeUnavailable);
    setTesting(true);
    const next: Record<string, number | null> = {};

    const uniquePrimaryDns = Array.from(new Set<string>(presets.map((preset) => preset.primary)));
    for (const ip of uniquePrimaryDns) {
      try {
        const result = await window.dnsBridge.ping(ip);
        next[ip] = result.latency;
        setLatencies({ ...next });
      } catch {
        next[ip] = null;
      }
    }

    setLatencies(next);
    setTesting(false);
  };

  const addPreset = () => {
    const name = customName.trim();
    const primary = customPrimary.trim();
    const secondary = customSecondary.trim();

    if (!name || !primary) return showNotice('error', t.enterName);
    if (!isIPv4(primary)) return showNotice('error', t.invalidPrimary);
    if (secondary && !isIPv4(secondary)) return showNotice('error', t.invalidSecondary);

    setPresets((items) => [
      ...items,
      {
        id: `custom-${Date.now()}`,
        name,
        primary,
        secondary: secondary || undefined,
        isCustom: true,
      },
    ]);
    setCustomName('');
    setCustomPrimary('');
    setCustomSecondary('');
  };

  const deletePreset = (id: string) => {
    setPresets((items) => items.filter((item) => item.id !== id));
  };

  const copyToClipboard = async (text: string, key: string) => {
    await navigator.clipboard.writeText(text);
    setCopied(key);
    window.setTimeout(() => setCopied(''), 1600);
  };

  return (
    <div className="min-h-screen bg-[#f4f6f8] text-[#171717]">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 px-6 py-4 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-[#111827] text-white shadow-sm">
              <Globe size={22} />
            </div>
            <div>
              <h1 className="text-lg font-semibold">DNS Switcher Pro</h1>
              <p className="text-xs text-slate-500">{t.subtitle}</p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-xs font-medium ${bridgeReady ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-red-200 bg-red-50 text-red-700'}`}>
              <Activity size={14} />
              {bridgeReady ? t.desktopReady : t.bridgeMissing}
            </span>
            {privilegeStatus?.needsAdminForDns && (
              <span className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-xs font-medium ${privilegeStatus.isAdmin ? 'border-blue-200 bg-blue-50 text-blue-700' : 'border-amber-200 bg-amber-50 text-amber-700'}`}>
                <Shield size={14} />
                {privilegeStatus.isAdmin ? t.adminMode : t.limitedMode}
              </span>
            )}
            <div className="flex rounded-lg border border-slate-200 bg-white p-1 text-xs font-semibold">
              <button
                onClick={() => setLanguage('en')}
                className={`rounded-md px-2 py-1 ${language === 'en' ? 'bg-[#111827] text-white' : 'text-slate-500 hover:bg-slate-50'}`}
              >
                EN
              </button>
              <button
                onClick={() => setLanguage('zh')}
                className={`rounded-md px-2 py-1 ${language === 'zh' ? 'bg-[#111827] text-white' : 'text-slate-500 hover:bg-slate-50'}`}
              >
                中文
              </button>
            </div>
            <button
              onClick={refreshInterfaces}
              className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium hover:bg-slate-50"
            >
              <RefreshCw size={15} />
              {t.refresh}
            </button>
            <button
              onClick={testLatency}
              disabled={testing}
              className="flex items-center gap-2 rounded-lg bg-[#111827] px-3 py-2 text-sm font-semibold text-white hover:bg-[#263244] disabled:opacity-60"
            >
              <Gauge size={15} className={testing ? 'animate-spin' : ''} />
              {testing ? t.testing : t.testLatency}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto grid max-w-7xl grid-cols-1 gap-6 p-6 lg:grid-cols-[360px_1fr]">
        <aside className="space-y-6">
          <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
            <div className="mb-4 flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500">
                <Wifi size={15} />
                {t.interfaces}
              </div>
              <span className="text-xs text-slate-400">{t.dragToSort}</span>
            </div>

            <div className="space-y-2">
              {interfaces.map((item, index) => (
                <div
                  key={item.name}
                  draggable
                  onDragStart={() => setDraggedInterfaceIndex(index)}
                  onDragEnd={() => setDraggedInterfaceIndex(null)}
                  onDragOver={(event) => event.preventDefault()}
                  onDrop={() => handleInterfaceDrop(index)}
                  className={`flex w-full items-start gap-2 rounded-lg border p-4 text-left transition ${selectedInterface === item.name ? 'border-[#111827] bg-slate-50 ring-1 ring-[#111827]' : 'border-slate-200 hover:border-slate-400'} ${draggedInterfaceIndex === index ? 'opacity-50' : ''}`}
                >
                  <button className="mt-0.5 cursor-grab rounded p-1 text-slate-400 hover:bg-slate-100" title={t.dragToSort}>
                    <GripVertical size={16} />
                  </button>
                  <button onClick={() => setSelectedInterface(item.name)} className="min-w-0 flex-1 text-left">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold" title={item.description}>{item.description}</p>
                        <p className="mt-1 font-mono text-xs text-slate-500">{item.name} · {item.address}</p>
                      </div>
                      {selectedInterface === item.name && <Check size={17} />}
                    </div>
                    {item.currentDns && item.currentDns.length > 0 && (
                      <div className="mt-3 flex flex-wrap gap-1.5">
                        {item.currentDns.map((dns) => (
                          <span key={dns} className="rounded border border-blue-100 bg-blue-50 px-2 py-1 font-mono text-[11px] text-blue-700">
                            {dns}
                          </span>
                        ))}
                      </div>
                    )}
                  </button>
                </div>
              ))}

              {interfaces.length === 0 && (
                <div className="rounded-lg border border-dashed border-slate-300 p-5 text-center text-sm text-slate-500">
                  {t.noInterfaces}
                </div>
              )}
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
            <div className="mb-4 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500">
              <Plus size={15} />
              {t.customDns}
            </div>

            <div className="space-y-3">
              <input
                value={customName}
                onChange={(event) => setCustomName(event.target.value)}
                className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-[#111827]"
                placeholder={t.namePlaceholder}
              />
              <div className="grid grid-cols-2 gap-2">
                <input
                  value={customPrimary}
                  onChange={(event) => setCustomPrimary(event.target.value)}
                  className="w-full rounded-lg border border-slate-200 px-3 py-2 font-mono text-sm outline-none focus:border-[#111827]"
                  placeholder={t.primaryPlaceholder}
                />
                <input
                  value={customSecondary}
                  onChange={(event) => setCustomSecondary(event.target.value)}
                  className="w-full rounded-lg border border-slate-200 px-3 py-2 font-mono text-sm outline-none focus:border-[#111827]"
                  placeholder={t.secondaryPlaceholder}
                />
              </div>
              <button
                onClick={addPreset}
                className="flex w-full items-center justify-center gap-2 rounded-lg bg-[#111827] px-3 py-2.5 text-sm font-semibold text-white hover:bg-[#263244]"
              >
                <Plus size={16} />
                {t.savePreset}
              </button>
            </div>
          </section>
        </aside>

        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div className="mb-5 flex items-center justify-between gap-4">
            <div>
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500">
                <Shield size={15} />
                {t.dnsPresets}
              </div>
              <p className="mt-2 text-sm text-slate-500">
                {t.target}: {selectedInterfaceInfo ? `${selectedInterfaceInfo.description} (${selectedInterfaceInfo.address})` : t.noInterfaceSelected}
              </p>
            </div>
            <span className="text-xs text-slate-400">{t.dragToSort}</span>
          </div>

          <div className="grid grid-cols-1 gap-3 xl:grid-cols-2">
            {presets.map((preset, index) => (
              <article
                key={preset.id}
                draggable
                onDragStart={() => setDraggedPresetIndex(index)}
                onDragEnd={() => setDraggedPresetIndex(null)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={() => handlePresetDrop(index)}
                className={`rounded-lg border border-slate-200 p-4 transition hover:border-slate-400 hover:shadow-sm ${draggedPresetIndex === index ? 'opacity-50' : ''}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex min-w-0 items-start gap-3">
                    <button className="mt-2 cursor-grab rounded p-1 text-slate-400 hover:bg-slate-100" title={t.dragToSort}>
                      <GripVertical size={16} />
                    </button>
                    <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${preset.isCustom ? 'bg-amber-50 text-amber-700' : 'bg-blue-50 text-blue-700'}`}>
                      {preset.isCustom ? <Settings size={18} /> : <Shield size={18} />}
                    </div>
                    <div className="min-w-0">
                      <h2 className="truncate text-sm font-semibold">{preset.name}</h2>
                      <p className="mt-1 font-mono text-xs text-slate-500">
                        {preset.primary}{preset.secondary ? ` / ${preset.secondary}` : ''}
                      </p>
                    </div>
                  </div>

                  {preset.isCustom && (
                    <button
                      onClick={() => deletePreset(preset.id)}
                      className="rounded-md p-2 text-slate-400 hover:bg-red-50 hover:text-red-600"
                      title="Delete"
                    >
                      <Trash2 size={16} />
                    </button>
                  )}
                </div>

                <div className="mt-4 flex items-center justify-between gap-3">
                  <div className="font-mono text-xs">
                    {latencies[preset.primary] === undefined && <span className="text-slate-400">-- ms</span>}
                    {latencies[preset.primary] === null && <span className="text-red-600">TIMEOUT</span>}
                    {typeof latencies[preset.primary] === 'number' && (
                      <span className={latencies[preset.primary]! < 80 ? 'text-emerald-600' : latencies[preset.primary]! < 180 ? 'text-amber-600' : 'text-red-600'}>
                        {latencies[preset.primary]} ms
                      </span>
                    )}
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => openCommands(preset)}
                      className="flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-2 text-xs font-semibold hover:bg-slate-50"
                    >
                      <Terminal size={14} />
                      {t.commands}
                    </button>
                    <button
                      onClick={() => applyPreset(preset)}
                      disabled={busy}
                      className="flex items-center gap-1.5 rounded-lg bg-[#111827] px-3 py-2 text-xs font-semibold text-white hover:bg-[#263244] disabled:opacity-60"
                    >
                      {busy ? <RefreshCw size={14} className="animate-spin" /> : <Zap size={14} />}
                      {t.apply}
                    </button>
                  </div>
                </div>
              </article>
            ))}
          </div>

          <div className="mt-6 flex gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
            <Info size={18} className="mt-0.5 shrink-0 text-blue-600" />
            <p>
              {t.footer}
            </p>
          </div>
        </section>
      </main>

      <AnimatePresence>
        {notice && (
          <motion.div
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 24 }}
            className={`fixed bottom-6 right-6 z-50 rounded-lg border px-5 py-4 shadow-xl ${notice.kind === 'success' ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : notice.kind === 'error' ? 'border-red-200 bg-red-50 text-red-800' : 'border-blue-200 bg-blue-50 text-blue-800'}`}
          >
            <div className="flex items-center gap-3 text-sm font-medium">
              {notice.kind === 'success' ? <Check size={18} /> : <Info size={18} />}
              {notice.text}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {commands && activePreset && (
          <div className="fixed inset-0 z-40 flex items-center justify-center p-4">
            <motion.button
              aria-label={t.close}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setCommands(null)}
              className="absolute inset-0 bg-black/40"
            />
            <motion.div
              initial={{ opacity: 0, scale: 0.96, y: 14 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.96, y: 14 }}
              className="relative w-full max-w-3xl overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xl"
            >
              <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 p-5">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-[#111827] text-white">
                    <Terminal size={20} />
                  </div>
                  <div>
                    <h2 className="text-base font-semibold">{t.manualCommands}</h2>
                    <p className="text-xs text-slate-500">{activePreset.name} · {selectedInterface}</p>
                  </div>
                </div>
                <button onClick={() => setCommands(null)} className="rounded-lg px-3 py-2 text-sm font-semibold hover:bg-slate-200">
                  {t.close}
                </button>
              </div>

              <div className="max-h-[70vh] space-y-5 overflow-y-auto p-5">
                <CommandBlock title="Windows" lines={commands.windows} copied={copied} copyLabel={t.copy} onCopy={copyToClipboard} />
                <CommandBlock title="macOS" lines={[commands.macos]} copied={copied} copyLabel={t.copy} onCopy={copyToClipboard} />
                <CommandBlock title="Linux" lines={[commands.linux]} copied={copied} copyLabel={t.copy} onCopy={copyToClipboard} />
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>
    </div>
  );
}

function CommandBlock({
  title,
  lines,
  copied,
  copyLabel,
  onCopy,
}: {
  title: string;
  lines: string[];
  copied: string;
  copyLabel: string;
  onCopy: (text: string, key: string) => void;
}) {
  return (
    <section>
      <div className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500">
        <Monitor size={14} />
        {title}
      </div>
      <div className="space-y-2">
        {lines.map((line, index) => {
          const key = `${title}-${index}`;
          return (
            <div key={key} className="group relative">
              <pre className="overflow-x-auto whitespace-pre-wrap rounded-lg bg-[#111827] p-4 pr-12 font-mono text-xs leading-relaxed text-white">
                {line}
              </pre>
              <button
                onClick={() => onCopy(line, key)}
                className="absolute right-2 top-2 rounded-md bg-white/10 p-2 text-white opacity-100 hover:bg-white/20"
                title={copyLabel}
              >
                {copied === key ? <Check size={15} className="text-emerald-300" /> : <Copy size={15} />}
              </button>
            </div>
          );
        })}
      </div>
    </section>
  );
}
