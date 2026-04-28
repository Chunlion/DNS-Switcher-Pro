using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
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

        public string DnsText
        {
            get { return string.IsNullOrWhiteSpace(Secondary) ? Primary : Primary + ", " + Secondary; }
        }
    }

    internal sealed class AdapterInfo
    {
        public string Name;
        public string Description;
        public string Address;
        public List<string> Dns = new List<string>();

        public string DnsText
        {
            get { return Dns.Count == 0 ? "-" : string.Join(", ", Dns.ToArray()); }
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Page = Color.FromArgb(247, 248, 250);
        private static readonly Color Panel = Color.White;
        private static readonly Color Ink = Color.FromArgb(31, 41, 55);
        private static readonly Color Muted = Color.FromArgb(107, 114, 128);
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);

        private readonly ListView adapterView = new ListView();
        private readonly ListView presetView = new ListView();
        private readonly TextBox nameBox = new TextBox();
        private readonly TextBox primaryBox = new TextBox();
        private readonly TextBox secondaryBox = new TextBox();
        private readonly Button refreshButton = new Button();
        private readonly Button swapColumnsButton = new Button();
        private readonly Button adapterUpButton = new Button();
        private readonly Button adapterDownButton = new Button();
        private readonly Button presetUpButton = new Button();
        private readonly Button presetDownButton = new Button();
        private readonly Button deleteButton = new Button();
        private readonly Button commandButton = new Button();
        private readonly Button applyButton = new Button();
        private readonly Button addButton = new Button();
        private readonly Button languageButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label adapterTitle = new Label();
        private readonly Label presetTitle = new Label();
        private readonly Label customTitle = new Label();
        private readonly Label targetLabel = new Label();
        private readonly Label nameLabel = new Label();
        private readonly Label primaryLabel = new Label();
        private readonly Label secondaryLabel = new Label();

        private readonly List<AdapterInfo> adapters = new List<AdapterInfo>();
        private readonly List<DnsPreset> presets = new List<DnsPreset>();
        private readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DNS Switcher Pro");
        private bool chinese;
        private bool dnsFirst = true;

        private string PresetsFile { get { return Path.Combine(dataDir, "presets.txt"); } }
        private string AdapterOrderFile { get { return Path.Combine(dataDir, "adapter-order.txt"); } }
        private string AdapterColumnsFile { get { return Path.Combine(dataDir, "adapter-columns.txt"); } }
        private string LanguageFile { get { return Path.Combine(dataDir, "zh.flag"); } }

        public MainForm()
        {
            Text = "DNS Switcher Pro";
            Width = 940;
            Height = 620;
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Page;
            Icon = LoadAppIcon();

            Directory.CreateDirectory(dataDir);
            chinese = File.Exists(LanguageFile);
            dnsFirst = !File.Exists(AdapterColumnsFile) || File.ReadAllText(AdapterColumnsFile).Trim() != "ip-first";

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
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.BackColor = Page;
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 4;
            header.RowCount = 2;
            header.BackColor = Page;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.SetColumnSpan(header, 2);
            root.Controls.Add(header, 0, 0);

            var title = new Label();
            title.Text = "DNS Switcher Pro";
            title.Dock = DockStyle.Fill;
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.ForeColor = Ink;
            header.Controls.Add(title, 0, 0);

            var subtitle = new Label();
            subtitle.Text = "Small native DNS switcher";
            subtitle.Dock = DockStyle.Fill;
            subtitle.ForeColor = Muted;
            header.Controls.Add(subtitle, 0, 1);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.Margin = new Padding(0, 6, 8, 7);
            header.SetRowSpan(statusLabel, 2);
            header.Controls.Add(statusLabel, 1, 0);

            languageButton.Dock = DockStyle.Fill;
            languageButton.Margin = new Padding(0, 6, 0, 7);
            languageButton.Click += delegate { ToggleLanguage(); };
            header.SetRowSpan(languageButton, 2);
            header.Controls.Add(languageButton, 2, 0);
            StyleSecondaryButton(languageButton);

            var left = CreatePanel();
            root.Controls.Add(left, 0, 1);

            adapterTitle.Dock = DockStyle.Fill;
            adapterTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            adapterTitle.ForeColor = Ink;
            left.Controls.Add(adapterTitle, 0, 0);

            targetLabel.Dock = DockStyle.Fill;
            targetLabel.ForeColor = Muted;
            left.Controls.Add(targetLabel, 0, 1);

            ConfigureListView(adapterView);
            adapterView.Columns.Add("Adapter", 210);
            adapterView.Columns.Add("DNS", 220);
            adapterView.Columns.Add("IP", 96);
            adapterView.SelectedIndexChanged += delegate { UpdateTargetLabel(); };
            left.Controls.Add(adapterView, 0, 2);

            var adapterButtons = CreateButtonRow();
            StyleSecondaryButton(refreshButton);
            StyleSecondaryButton(swapColumnsButton);
            StyleSecondaryButton(adapterUpButton);
            StyleSecondaryButton(adapterDownButton);
            refreshButton.Width = 78;
            swapColumnsButton.Width = 86;
            adapterUpButton.Width = 42;
            adapterDownButton.Width = 42;
            refreshButton.Click += delegate { RefreshAdapters(); };
            swapColumnsButton.Click += delegate { ToggleAdapterColumns(); };
            adapterUpButton.Click += delegate { MoveAdapter(-1); };
            adapterDownButton.Click += delegate { MoveAdapter(1); };
            adapterButtons.Controls.Add(refreshButton);
            adapterButtons.Controls.Add(swapColumnsButton);
            adapterButtons.Controls.Add(adapterUpButton);
            adapterButtons.Controls.Add(adapterDownButton);
            left.Controls.Add(adapterButtons, 0, 3);

            var right = CreatePanel();
            right.Margin = new Padding(14, 0, 0, 0);
            root.Controls.Add(right, 1, 1);

            presetTitle.Dock = DockStyle.Fill;
            presetTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            presetTitle.ForeColor = Ink;
            right.Controls.Add(presetTitle, 0, 0);

            var hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = Muted;
            right.Controls.Add(hint, 0, 1);

            ConfigureListView(presetView);
            presetView.Columns.Add("Name", 180);
            presetView.Columns.Add("DNS", 210);
            presetView.Columns.Add("Type", 80);
            right.Controls.Add(presetView, 0, 2);

            var presetButtons = CreateButtonRow();
            StyleSecondaryButton(presetUpButton);
            StyleSecondaryButton(presetDownButton);
            StyleSecondaryButton(deleteButton);
            StyleSecondaryButton(commandButton);
            StylePrimaryButton(applyButton);
            presetUpButton.Width = 42;
            presetDownButton.Width = 42;
            deleteButton.Width = 74;
            commandButton.Width = 94;
            applyButton.Width = 96;
            presetUpButton.Click += delegate { MovePreset(-1); };
            presetDownButton.Click += delegate { MovePreset(1); };
            deleteButton.Click += delegate { DeletePreset(); };
            commandButton.Click += delegate { ShowCommands(); };
            applyButton.Click += delegate { ApplySelectedPreset(); };
            presetButtons.Controls.Add(presetUpButton);
            presetButtons.Controls.Add(presetDownButton);
            presetButtons.Controls.Add(deleteButton);
            presetButtons.Controls.Add(commandButton);
            presetButtons.Controls.Add(applyButton);
            right.Controls.Add(presetButtons, 0, 3);

            customTitle.Dock = DockStyle.Fill;
            customTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            customTitle.ForeColor = Ink;
            right.Controls.Add(customTitle, 0, 4);

            var customGrid = new TableLayoutPanel();
            customGrid.Dock = DockStyle.Fill;
            customGrid.ColumnCount = 3;
            customGrid.RowCount = 2;
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            customGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            customGrid.BackColor = Panel;
            customGrid.Controls.Add(nameLabel, 0, 0);
            customGrid.Controls.Add(primaryLabel, 1, 0);
            customGrid.Controls.Add(secondaryLabel, 2, 0);
            customGrid.Controls.Add(nameBox, 0, 1);
            customGrid.Controls.Add(primaryBox, 1, 1);
            customGrid.Controls.Add(secondaryBox, 2, 1);
            StyleInput(nameBox);
            StyleInput(primaryBox);
            StyleInput(secondaryBox);
            StyleFieldLabel(nameLabel);
            StyleFieldLabel(primaryLabel);
            StyleFieldLabel(secondaryLabel);
            right.Controls.Add(customGrid, 0, 5);

            var addRow = CreateButtonRow();
            StyleSecondaryButton(addButton);
            addButton.Width = 120;
            addButton.Click += delegate { AddPreset(); };
            addRow.Controls.Add(addButton);
            right.Controls.Add(addRow, 0, 6);
        }

        private TableLayoutPanel CreatePanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Panel;
            panel.Padding = new Padding(14);
            panel.RowCount = 7;
            panel.ColumnCount = 1;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            return panel;
        }

        private FlowLayoutPanel CreateButtonRow()
        {
            var row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.BackColor = Panel;
            row.WrapContents = false;
            return row;
        }

        private void ConfigureListView(ListView list)
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.MultiSelect = false;
            list.GridLines = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.BackColor = Color.White;
            list.ForeColor = Ink;
            list.Font = Font;
        }

        private void StylePrimaryButton(Button button)
        {
            StyleButton(button, Accent, Color.White, Accent);
            button.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        }

        private void StyleSecondaryButton(Button button)
        {
            StyleButton(button, Color.White, Ink, Color.FromArgb(209, 213, 219));
        }

        private void StyleButton(Button button, Color back, Color fore, Color border)
        {
            button.Height = 30;
            button.Margin = new Padding(0, 7, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Cursor = Cursors.Hand;
        }

        private void StyleInput(TextBox box)
        {
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 0, 8, 0);
            box.BorderStyle = BorderStyle.FixedSingle;
        }

        private void StyleFieldLabel(Label label)
        {
            label.Dock = DockStyle.Fill;
            label.ForeColor = Muted;
            label.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular);
        }

        private void ApplyLanguage()
        {
            statusLabel.Text = IsAdmin() ? T("Admin mode", "管理员模式") : T("Limited mode", "受限模式");
            statusLabel.BackColor = IsAdmin() ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 243, 199);
            statusLabel.ForeColor = Ink;
            languageButton.Text = chinese ? "English" : "中文";
            adapterTitle.Text = T("Network adapters", "网卡列表");
            presetTitle.Text = T("DNS presets", "DNS 预设");
            customTitle.Text = T("Custom DNS", "自定义 DNS");
            nameLabel.Text = T("Name", "名称");
            primaryLabel.Text = T("Primary", "主 DNS");
            secondaryLabel.Text = T("Secondary", "备用 DNS");
            refreshButton.Text = T("Refresh", "刷新");
            swapColumnsButton.Text = dnsFirst ? T("DNS first", "DNS 在前") : T("IP first", "IP 在前");
            adapterUpButton.Text = "↑";
            adapterDownButton.Text = "↓";
            presetUpButton.Text = "↑";
            presetDownButton.Text = "↓";
            deleteButton.Text = T("Delete", "删除");
            commandButton.Text = T("Command", "命令");
            applyButton.Text = T("Apply", "应用");
            addButton.Text = T("Save preset", "保存预设");
            adapterView.Columns[0].Text = T("Adapter", "网卡");
            adapterView.Columns[1].Text = dnsFirst ? "DNS" : "IP";
            adapterView.Columns[2].Text = dnsFirst ? "IP" : "DNS";
            presetView.Columns[0].Text = T("Name", "名称");
            presetView.Columns[1].Text = "DNS";
            presetView.Columns[2].Text = T("Type", "类型");
            UpdateTargetLabel();
            BindAdapters();
            BindPresets();
        }

        private string T(string en, string zh)
        {
            return chinese ? zh : en;
        }

        private void ToggleLanguage()
        {
            chinese = !chinese;
            if (chinese) File.WriteAllText(LanguageFile, "1");
            else if (File.Exists(LanguageFile)) File.Delete(LanguageFile);
            ApplyLanguage();
        }

        private void ToggleAdapterColumns()
        {
            dnsFirst = !dnsFirst;
            File.WriteAllText(AdapterColumnsFile, dnsFirst ? "dns-first" : "ip-first", Encoding.UTF8);
            ApplyLanguage();
        }

        private void LoadPresets()
        {
            presets.Clear();
            if (File.Exists(PresetsFile))
            {
                foreach (var line in File.ReadAllLines(PresetsFile))
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
            File.WriteAllLines(PresetsFile, lines.ToArray(), Encoding.UTF8);
        }

        private void BindPresets()
        {
            var selectedId = GetSelectedPreset() == null ? null : GetSelectedPreset().Id;
            presetView.Items.Clear();
            foreach (var preset in presets)
            {
                var item = new ListViewItem(preset.Name);
                item.SubItems.Add(preset.DnsText);
                item.SubItems.Add(preset.Custom ? T("Custom", "自定义") : T("Built-in", "内置"));
                item.Tag = preset;
                presetView.Items.Add(item);
                if (preset.Id == selectedId) item.Selected = true;
            }
            if (presetView.SelectedItems.Count == 0 && presetView.Items.Count > 0) presetView.Items[0].Selected = true;
        }

        private void RefreshAdapters()
        {
            var selected = GetSelectedAdapter();
            var selectedName = selected == null ? null : selected.Name;
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

            ApplyAdapterOrder();
            BindAdapters();
            if (!string.IsNullOrEmpty(selectedName)) SelectAdapter(selectedName);
            if (adapterView.SelectedItems.Count == 0 && adapterView.Items.Count > 0) adapterView.Items[0].Selected = true;
            UpdateTargetLabel();
        }

        private void BindAdapters()
        {
            var selected = GetSelectedAdapter();
            var selectedName = selected == null ? null : selected.Name;
            adapterView.Items.Clear();
            foreach (var adapter in adapters)
            {
                var item = new ListViewItem(adapter.Description);
                if (dnsFirst)
                {
                    item.SubItems.Add(adapter.DnsText);
                    item.SubItems.Add(adapter.Address);
                }
                else
                {
                    item.SubItems.Add(adapter.Address);
                    item.SubItems.Add(adapter.DnsText);
                }
                item.Tag = adapter;
                adapterView.Items.Add(item);
                if (adapter.Name == selectedName) item.Selected = true;
            }
        }

        private void SelectAdapter(string name)
        {
            foreach (ListViewItem item in adapterView.Items)
            {
                var adapter = item.Tag as AdapterInfo;
                if (adapter != null && adapter.Name == name)
                {
                    item.Selected = true;
                    item.Focused = true;
                    return;
                }
            }
        }

        private void ApplyAdapterOrder()
        {
            if (!File.Exists(AdapterOrderFile)) return;
            var order = File.ReadAllLines(AdapterOrderFile).ToList();
            adapters.Sort(delegate (AdapterInfo a, AdapterInfo b)
            {
                var ia = order.IndexOf(a.Name);
                var ib = order.IndexOf(b.Name);
                if (ia < 0 && ib < 0) return 0;
                if (ia < 0) return 1;
                if (ib < 0) return -1;
                return ia.CompareTo(ib);
            });
        }

        private void SaveAdapterOrder()
        {
            File.WriteAllLines(AdapterOrderFile, adapters.Select(a => a.Name).ToArray(), Encoding.UTF8);
        }

        private AdapterInfo GetSelectedAdapter()
        {
            if (adapterView.SelectedItems.Count == 0) return null;
            return adapterView.SelectedItems[0].Tag as AdapterInfo;
        }

        private DnsPreset GetSelectedPreset()
        {
            if (presetView.SelectedItems.Count == 0) return null;
            return presetView.SelectedItems[0].Tag as DnsPreset;
        }

        private void UpdateTargetLabel()
        {
            var adapter = GetSelectedAdapter();
            targetLabel.Text = adapter == null
                ? T("No adapter selected", "未选择网卡")
                : string.Format("{0}: {1}", T("Target", "目标"), adapter.Description);
        }

        private void MoveAdapter(int direction)
        {
            var adapter = GetSelectedAdapter();
            if (adapter == null) return;
            MoveItem(adapters, adapter, direction);
            SaveAdapterOrder();
            BindAdapters();
            SelectAdapter(adapter.Name);
        }

        private void MovePreset(int direction)
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;
            MoveItem(presets, preset, direction);
            SavePresets();
            BindPresets();
            foreach (ListViewItem item in presetView.Items)
            {
                if (item.Tag == preset)
                {
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
        }

        private static void MoveItem<T>(List<T> source, T item, int direction)
        {
            var index = source.IndexOf(item);
            var next = index + direction;
            if (index < 0 || next < 0 || next >= source.Count) return;
            source.RemoveAt(index);
            source.Insert(next, item);
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

            var preset = new DnsPreset { Id = Guid.NewGuid().ToString("N"), Name = name, Primary = primary, Secondary = secondary, Custom = true };
            presets.Add(preset);
            nameBox.Clear();
            primaryBox.Clear();
            secondaryBox.Clear();
            SavePresets();
            BindPresets();
        }

        private void DeletePreset()
        {
            var preset = GetSelectedPreset();
            if (preset == null || !preset.Custom) return;
            presets.Remove(preset);
            SavePresets();
            BindPresets();
        }

        private void ApplySelectedPreset()
        {
            var adapter = GetSelectedAdapter();
            var preset = GetSelectedPreset();
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
            var adapter = GetSelectedAdapter();
            var preset = GetSelectedPreset();
            if (adapter == null || preset == null) return;
            MessageBox.Show(BuildPowerShellCommand(adapter.Name, preset), T("Command", "命令"));
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
    }
}
