using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DnsSwitcherPro
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class DnsPreset
    {
        public string Id;
        public string Name;
        public string Primary;
        public string Secondary;
        public bool Custom;

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Secondary)
                ? string.Format("{0}  ({1})", Name, Primary)
                : string.Format("{0}  ({1} / {2})", Name, Primary, Secondary);
        }
    }

    internal sealed class AdapterInfo
    {
        public string Name;
        public string Description;
        public string Address;
        public List<string> Dns = new List<string>();

        public override string ToString()
        {
            var dns = Dns.Count == 0 ? "DNS: -" : "DNS: " + string.Join(", ", Dns.ToArray());
            return string.Format("{0}\r\n{1} · {2}\r\n{3}", Description, Name, Address, dns);
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly ListBox adaptersList = new ListBox();
        private readonly ListBox presetsList = new ListBox();
        private readonly TextBox nameBox = new TextBox();
        private readonly TextBox primaryBox = new TextBox();
        private readonly TextBox secondaryBox = new TextBox();
        private readonly Button applyButton = new Button();
        private readonly Button commandsButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button addButton = new Button();
        private readonly Button deleteButton = new Button();
        private readonly Button upAdapterButton = new Button();
        private readonly Button downAdapterButton = new Button();
        private readonly Button upPresetButton = new Button();
        private readonly Button downPresetButton = new Button();
        private readonly Button languageButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label targetLabel = new Label();

        private readonly List<AdapterInfo> adapters = new List<AdapterInfo>();
        private readonly List<DnsPreset> presets = new List<DnsPreset>();
        private readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DNS Switcher Pro");
        private bool chinese;

        public MainForm()
        {
            Text = "DNS Switcher Pro";
            Width = 980;
            Height = 680;
            MinimumSize = new Size(860, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            Icon = LoadAppIcon();

            Directory.CreateDirectory(dataDir);
            chinese = File.Exists(Path.Combine(dataDir, "zh.flag"));
            BuildUi();
            LoadPresets();
            RefreshAdapters();
            ApplyLanguage();
        }

        private Icon LoadAppIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath)) return new Icon(iconPath);
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            var title = new Label { Text = "DNS Switcher Pro", AutoSize = true, Font = new Font(Font.FontFamily, 16, FontStyle.Bold), Margin = new Padding(0, 8, 20, 0) };
            statusLabel.AutoSize = true;
            statusLabel.Margin = new Padding(0, 13, 12, 0);
            languageButton.Width = 82;
            languageButton.Height = 30;
            languageButton.Margin = new Padding(0, 8, 0, 0);
            languageButton.Click += delegate { ToggleLanguage(); };
            header.Controls.Add(title);
            header.Controls.Add(statusLabel);
            header.Controls.Add(languageButton);
            root.SetColumnSpan(header, 2);
            root.Controls.Add(header, 0, 0);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.Controls.Add(left, 0, 1);

            targetLabel.Dock = DockStyle.Fill;
            targetLabel.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
            left.Controls.Add(targetLabel, 0, 0);

            adaptersList.Dock = DockStyle.Fill;
            adaptersList.DrawMode = DrawMode.OwnerDrawVariable;
            adaptersList.MeasureItem += MeasureListItem;
            adaptersList.DrawItem += DrawListItem;
            adaptersList.SelectedIndexChanged += delegate { UpdateTargetLabel(); };
            left.Controls.Add(adaptersList, 0, 1);

            var adapterButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            refreshButton.Width = 92;
            upAdapterButton.Width = 54;
            downAdapterButton.Width = 54;
            refreshButton.Click += delegate { RefreshAdapters(); };
            upAdapterButton.Click += delegate { MoveAdapter(-1); };
            downAdapterButton.Click += delegate { MoveAdapter(1); };
            adapterButtons.Controls.Add(refreshButton);
            adapterButtons.Controls.Add(upAdapterButton);
            adapterButtons.Controls.Add(downAdapterButton);
            left.Controls.Add(adapterButtons, 0, 2);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(14, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.Controls.Add(right, 1, 1);

            presetsList.Dock = DockStyle.Fill;
            presetsList.DrawMode = DrawMode.OwnerDrawVariable;
            presetsList.MeasureItem += MeasureListItem;
            presetsList.DrawItem += DrawListItem;
            right.Controls.Add(presetsList, 0, 0);

            var presetButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            upPresetButton.Width = 54;
            downPresetButton.Width = 54;
            deleteButton.Width = 80;
            commandsButton.Width = 112;
            applyButton.Width = 112;
            upPresetButton.Click += delegate { MovePreset(-1); };
            downPresetButton.Click += delegate { MovePreset(1); };
            deleteButton.Click += delegate { DeletePreset(); };
            commandsButton.Click += delegate { ShowCommands(); };
            applyButton.Click += delegate { ApplySelectedPreset(); };
            presetButtons.Controls.Add(upPresetButton);
            presetButtons.Controls.Add(downPresetButton);
            presetButtons.Controls.Add(deleteButton);
            presetButtons.Controls.Add(commandsButton);
            presetButtons.Controls.Add(applyButton);
            right.Controls.Add(presetButtons, 0, 1);

            var customGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            customGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            customGrid.Controls.Add(new Label { Text = "Name", Dock = DockStyle.Fill }, 0, 0);
            customGrid.Controls.Add(new Label { Text = "Primary", Dock = DockStyle.Fill }, 1, 0);
            customGrid.Controls.Add(new Label { Text = "Secondary", Dock = DockStyle.Fill }, 2, 0);
            customGrid.Controls.Add(nameBox, 0, 1);
            customGrid.Controls.Add(primaryBox, 1, 1);
            customGrid.Controls.Add(secondaryBox, 2, 1);
            right.Controls.Add(customGrid, 0, 2);

            addButton.Dock = DockStyle.Left;
            addButton.Width = 140;
            addButton.Click += delegate { AddPreset(); };
            right.Controls.Add(addButton, 0, 3);
        }

        private void ApplyLanguage()
        {
            statusLabel.Text = IsAdmin()
                ? T("Admin mode", "管理员模式")
                : T("Limited mode", "受限模式");
            languageButton.Text = chinese ? "English" : "中文";
            refreshButton.Text = T("Refresh", "刷新");
            upAdapterButton.Text = "↑";
            downAdapterButton.Text = "↓";
            upPresetButton.Text = "↑";
            downPresetButton.Text = "↓";
            deleteButton.Text = T("Delete", "删除");
            commandsButton.Text = T("Commands", "命令");
            applyButton.Text = T("Apply", "应用");
            addButton.Text = T("Save preset", "保存预设");
            UpdateTargetLabel();
            adaptersList.Invalidate();
            presetsList.Invalidate();
        }

        private string T(string en, string zh)
        {
            return chinese ? zh : en;
        }

        private void ToggleLanguage()
        {
            chinese = !chinese;
            var flag = Path.Combine(dataDir, "zh.flag");
            if (chinese) File.WriteAllText(flag, "1");
            else if (File.Exists(flag)) File.Delete(flag);
            ApplyLanguage();
        }

        private void LoadPresets()
        {
            presets.Clear();
            var file = Path.Combine(dataDir, "presets.txt");
            if (File.Exists(file))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 5)
                    {
                        presets.Add(new DnsPreset { Id = parts[0], Name = parts[1], Primary = parts[2], Secondary = parts[3], Custom = parts[4] == "1" });
                    }
                }
            }

            if (presets.Count == 0)
            {
                presets.Add(new DnsPreset { Id = "cloudflare", Name = "Cloudflare", Primary = "1.1.1.1", Secondary = "1.0.0.1" });
                presets.Add(new DnsPreset { Id = "alidns", Name = "AliDNS", Primary = "223.5.5.5", Secondary = "223.6.6.6" });
                presets.Add(new DnsPreset { Id = "dnspod", Name = "DNSPod", Primary = "119.29.29.29", Secondary = "119.28.28.28" });
                presets.Add(new DnsPreset { Id = "114dns", Name = "114DNS", Primary = "114.114.114.114", Secondary = "114.114.115.115" });
                presets.Add(new DnsPreset { Id = "google", Name = "Google DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4" });
                presets.Add(new DnsPreset { Id = "baidu", Name = "Baidu DNS", Primary = "180.76.76.76" });
            }

            BindPresets();
        }

        private void SavePresets()
        {
            var lines = presets.Select(p => string.Join("\t", new[] { p.Id, p.Name, p.Primary, p.Secondary ?? "", p.Custom ? "1" : "0" }));
            File.WriteAllLines(Path.Combine(dataDir, "presets.txt"), lines.ToArray(), Encoding.UTF8);
        }

        private void BindPresets()
        {
            var selected = presetsList.SelectedIndex;
            presetsList.Items.Clear();
            foreach (var preset in presets) presetsList.Items.Add(preset);
            if (presetsList.Items.Count > 0) presetsList.SelectedIndex = Math.Max(0, Math.Min(selected, presetsList.Items.Count - 1));
        }

        private void RefreshAdapters()
        {
            adapters.Clear();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.OperationalStatus != OperationalStatus.Up) continue;
                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(ipv4)) continue;

                adapters.Add(new AdapterInfo
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Address = ipv4,
                    Dns = props.DnsAddresses
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.ToString())
                        .ToList()
                });
            }

            adaptersList.Items.Clear();
            foreach (var adapter in adapters) adaptersList.Items.Add(adapter);
            if (adaptersList.Items.Count > 0) adaptersList.SelectedIndex = 0;
            UpdateTargetLabel();
        }

        private void UpdateTargetLabel()
        {
            var adapter = adaptersList.SelectedItem as AdapterInfo;
            targetLabel.Text = adapter == null
                ? T("Network adapters", "网卡列表")
                : string.Format("{0}: {1}", T("Target", "目标"), adapter.Description);
        }

        private void MoveAdapter(int direction)
        {
            MoveListItem(adaptersList, adapters, direction);
        }

        private void MovePreset(int direction)
        {
            MoveListItem(presetsList, presets, direction);
            SavePresets();
        }

        private static void MoveListItem<T>(ListBox listBox, List<T> source, int direction)
        {
            var index = listBox.SelectedIndex;
            var next = index + direction;
            if (index < 0 || next < 0 || next >= source.Count) return;
            var item = source[index];
            source.RemoveAt(index);
            source.Insert(next, item);
            listBox.Items.Clear();
            foreach (var value in source) listBox.Items.Add(value);
            listBox.SelectedIndex = next;
        }

        private void AddPreset()
        {
            var name = nameBox.Text.Trim();
            var primary = primaryBox.Text.Trim();
            var secondary = secondaryBox.Text.Trim();
            if (name.Length == 0 || !IsIPv4(primary) || (secondary.Length > 0 && !IsIPv4(secondary)))
            {
                MessageBox.Show(T("Enter a name and valid IPv4 DNS addresses.", "请输入名称和有效的 IPv4 DNS 地址。"));
                return;
            }

            presets.Add(new DnsPreset { Id = Guid.NewGuid().ToString("N"), Name = name, Primary = primary, Secondary = secondary, Custom = true });
            nameBox.Clear();
            primaryBox.Clear();
            secondaryBox.Clear();
            SavePresets();
            BindPresets();
        }

        private void DeletePreset()
        {
            var preset = presetsList.SelectedItem as DnsPreset;
            if (preset == null || !preset.Custom) return;
            presets.Remove(preset);
            SavePresets();
            BindPresets();
        }

        private void ApplySelectedPreset()
        {
            var adapter = adaptersList.SelectedItem as AdapterInfo;
            var preset = presetsList.SelectedItem as DnsPreset;
            if (adapter == null || preset == null) return;

            try
            {
                SetDns(adapter.Name, preset);
                RefreshAdapters();
                MessageBox.Show(T("DNS applied successfully.", "DNS 应用成功。"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, T("Failed", "失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowCommands()
        {
            var adapter = adaptersList.SelectedItem as AdapterInfo;
            var preset = presetsList.SelectedItem as DnsPreset;
            if (adapter == null || preset == null) return;
            MessageBox.Show(BuildPowerShellCommand(adapter.Name, preset), T("Commands", "命令"));
        }

        private static void SetDns(string adapterName, DnsPreset preset)
        {
            var script = BuildPowerShellCommand(adapterName, preset)
                + "; $current = @(Get-DnsClientServerAddress -InterfaceAlias $alias -AddressFamily IPv4).ServerAddresses"
                + "; $missing = @($addresses | Where-Object { $current -notcontains $_ })"
                + "; if ($missing.Count -gt 0) { throw \"DNS verification failed: $($current -join ', ')\" }";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command " + QuoteArg(script))
            {
                UseShellExecute = true,
                Verb = IsAdmin() ? "" : "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var process = Process.Start(psi);
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("DNS command failed or administrator permission was denied.");
        }

        private static string BuildPowerShellCommand(string adapterName, DnsPreset preset)
        {
            var addresses = string.IsNullOrWhiteSpace(preset.Secondary)
                ? QuotePs(preset.Primary)
                : QuotePs(preset.Primary) + "," + QuotePs(preset.Secondary);
            return "$ErrorActionPreference='Stop'; $alias=" + QuotePs(adapterName)
                + "; $addresses=@(" + addresses + ")"
                + "; Set-DnsClientServerAddress -InterfaceAlias $alias -ServerAddresses $addresses";
        }

        private static bool IsIPv4(string value)
        {
            IPAddress address;
            return IPAddress.TryParse(value, out address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private static bool IsAdmin()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string QuotePs(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }

        private static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void MeasureListItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 58;
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;
            var box = (ListBox)sender;
            var text = box.Items[e.Index].ToString();
            var color = (e.State & DrawItemState.Selected) == DrawItemState.Selected ? SystemColors.HighlightText : ForeColor;
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            e.DrawFocusRectangle();
        }
    }
}
