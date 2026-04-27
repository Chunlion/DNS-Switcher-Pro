const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("dnsBridge", {
  getPrivilegeStatus: () => ipcRenderer.invoke("dns:getPrivilegeStatus"),
  getInterfaces: () => ipcRenderer.invoke("dns:getInterfaces"),
  getCommands: (payload) => ipcRenderer.invoke("dns:getCommands", payload),
  setDns: (payload) => ipcRenderer.invoke("dns:set", payload),
  ping: (ip) => ipcRenderer.invoke("dns:ping", ip),
});
