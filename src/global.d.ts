export {};

declare global {
  interface Window {
    dnsBridge?: {
      getPrivilegeStatus: () => Promise<PrivilegeStatus>;
      getInterfaces: () => Promise<NetworkInterfaceInfo[]>;
      getCommands: (payload: DnsPayload) => Promise<DnsCommands>;
      setDns: (payload: DnsPayload) => Promise<{ success: boolean }>;
      ping: (ip: string) => Promise<{ ip: string; latency: number | null }>;
    };
  }

  interface DnsPayload {
    interfaceName: string;
    primary: string;
    secondary?: string;
  }

  interface PrivilegeStatus {
    platform: string;
    isAdmin: boolean;
    needsAdminForDns: boolean;
  }

  interface DnsCommands {
    windows: string[];
    macos: string;
    linux: string;
  }

  interface NetworkInterfaceInfo {
    name: string;
    description: string;
    status?: string;
    address: string;
    currentDns?: string[];
  }
}
