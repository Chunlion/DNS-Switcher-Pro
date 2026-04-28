using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    }

    internal sealed class AdapterInfo
    {
        public string Name;
        public string Description;
        public string Address;
        public List<string> Dns = new List<string>();
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Page = Color.FromArgb(246, 248, 250);
        private static readonly Color Panel = Color.White;
        private static readonly Color Ink = Color.FromArgb(25, 31, 40);
        private static readonly Color Muted = Color.FromArgb(101, 116, 139);
        private static readonly Color Line = Color.FromArgb(222, 226, 232);
        private static readonly Color Accent = Color.FromArgb(29, 78, 216);
        private static readonly Color AccentSoft = Color.FromArgb(233, 240, 255);
        private static readonly Color SuccessSoft = Color.FromArgb(232, 246, 238);
        private static readonly Color WarningSoft = Color.FromArgb(255, 247, 226);

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
        private readonly Label adaptersTitle = new Label();
        private readonly Label presetsTitle = new Label();
        private readonly Label customTitle = new Label();
        private readonly Label nameLabel = new Label();
        private readonly Label primaryLabel = new Label();
        private readonly Label secondaryLabel = new Label();

        private readonly List<AdapterInfo> adapters = new List<AdapterInfo>();
        private readonly List<DnsPreset> presets = new List<DnsPreset>();
        private readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DNS Switcher Pro");
        private bool chinese;

        private string PresetsFile { get { return Path.Combine(dataDir, "presets.txt"); } }
        private string AdapterOrderFile { get { return Path.Combine(dataDir, "adapter-order.txt"); } }
        private string LanguageFile { get { return Path.Combine(dataDir, "zh.flag"); } }

        public MainForm()
        {
            Text = "DNS Switcher Pro";
            Width = 900;
            Height = 620;
            MinimumSize = new Size(820, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Page;
            Icon = LoadAppIcon();

            Directory.CreateDirectory(dataDir);
            chinese = File.Exists(LanguageFile);

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
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.Padding = new Padding(18);
            root.BackColor = Page;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new FlowLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.FlowDirection = FlowDirection.LeftToRight;
            header.WrapContents = false;
            header.BackColor = Page;
            root.SetColumnSpan(header, 2);
            root.Controls.Add(header, 0, 0);

            var logo = new Panel();
            logo.Width = 38;
            logo.Height = 38;
            logo.Margin = new Padding(0, 8, 10, 0);
            logo.Paint += PaintLogo;
            header.Controls.Add(logo);

            var titleBlock = new TableLayoutPanel();
            titleBlock.Width = 300;
            titleBlock.Height = 48;
            titleBlock.RowCount = 2;
            titleBlock.ColumnCount = 1;
            titleBlock.Margin = new Padding(0, 5, 14, 0);
            titleBlock.BackColor = Page;
            titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
            var title = new Label();
            title.Text = "DNS Switcher Pro";
            title.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
            title.ForeColor = Ink;
            title.Dock = DockStyle.Fill;
            var subtitle = new Label();
            subtitle.Text = "Native Windows DNS utility";
            subtitle.ForeColor = Muted;
            subtitle.Dock = DockStyle.Fill;
            titleBlock.Controls.Add(title, 0, 0);
            titleBlock.Controls.Add(subtitle, 0, 1);
            header.Controls.Add(titleBlock);

            StylePill(statusLabel);
            statusLabel.Margin = new Padding(0, 13, 8, 0);
            header.Controls.Add(statusLabel);

            StyleButton(languageButton, false);
            languageButton.Width = 78;
            languageButton.Height = 31;
            languageButton.Margin = new Padding(0, 9, 0, 0);
            languageButton.Click += delegate { ToggleLanguage(); };
            header.Controls.Add(languageButton);

            var left = CreateCardLayout();
            root.Controls.Add(left, 0, 1);

            adaptersTitle.Dock = DockStyle.Fill;
            adaptersTitle.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            adaptersTitle.ForeColor = Ink;
            left.Controls.Add(adaptersTitle, 0, 0);

            targetLabel.Dock = DockStyle.Fill;
            targetLabel.ForeColor = Muted;
            left.Controls.Add(targetLabel, 0, 1);

            ConfigureList(adaptersList);
            adaptersList.SelectedIndexChanged += delegate { UpdateTargetLabel(); };
            left.Controls.Add(adaptersList, 0, 2);

            var adapterButtons = CreateButtonRow();
            StyleButton(refreshButton, false);
            StyleButton(upAdapterButton, false);
            StyleButton(downAdapterButton, false);
            refreshButton.Width = 92;
            upAdapterButton.Width = 42;
            downAdapterButton.Width = 42;
            refreshButton.Click += delegate { RefreshAdapters(); };
            upAdapterButton.Click += delegate { MoveAdapter(-1); };
            downAdapterButton.Click += delegate { MoveAdapter(1); };
            adapterButtons.Controls.Add(refreshButton);
            adapterButtons.Controls.Add(upAdapterButton);
            adapterButtons.Controls.Add(downAdapterButton);
            left.Controls.Add(adapterButtons, 0, 3);

            var right = CreateCardLayout();
            right.Padding = new Padding(16, 14, 16, 16);
            root.Controls.Add(right, 1, 1);

            presetsTitle.Dock = DockStyle.Fill;
            presetsTitle.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            presetsTitle.ForeColor = Ink;
            right.Controls.Add(presetsTitle, 0, 0);

            var hint = new Label();
            hint.Text = "";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = Muted;
            right.Controls.Add(hint, 0, 1);

            ConfigureList(presetsList);
            right.Controls.Add(presetsList, 0, 2);

            var presetButtons = CreateButtonRow();
            StyleButton(upPresetButton, false);
            StyleButton(downPresetButton, false);
            StyleButton(deleteButton, false);
            StyleButton(commandsButton, false);
            StyleButton(applyButton, true);
            upPresetButton.Width = 42;
            downPresetButton.Width = 42;
            deleteButton.Width = 76;
            commandsButton.Width = 100;
            applyButton.Width = 104;
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
            right.Controls.Add(presetButtons, 0, 3);

            customTitle.Dock = DockStyle.Fill;
            customTitle.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            customTitle.ForeColor = Ink;
            right.Controls.Add(customTitle, 0, 4);

            var customGrid = new TableLayoutPanel();
            customGrid.Dock = DockStyle.Fill;
            customGrid.ColumnCount = 3;
            customGrid.RowCount = 2;
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            customGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
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
            StyleSmallLabel(nameLabel);
            StyleSmallLabel(primaryLabel);
            StyleSmallLabel(secondaryLabel);
            right.Controls.Add(customGrid, 0, 5);

            var addRow = CreateButtonRow();
            StyleButton(addButton, false);
            addButton.Width = 128;
            addButton.Click += delegate { AddPreset(); };
            addRow.Controls.Add(addButton);
            right.Controls.Add(addRow, 0, 6);
        }

        private TableLayoutPanel CreateCardLayout()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.RowCount = 7;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(16, 14, 16, 16);
            panel.Margin = new Padding(0, 0, 12, 0);
            panel.BackColor = Panel;
            panel.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            return panel;
        }

        private FlowLayoutPanel CreateButtonRow()
        {
            var row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.BackColor = Panel;
            return row;
        }

        private void ConfigureList(ListBox list)
        {
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Panel;
            list.DrawMode = DrawMode.OwnerDrawVariable;
            list.MeasureItem += MeasureListItem;
            list.DrawItem += DrawListItem;
        }

        private void StyleButton(Button button, bool primary)
        {
            button.Height = 31;
            button.Margin = new Padding(0, 6, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Line;
            button.BackColor = primary ? Accent : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.Font = new Font(Font.FontFamily, 9, primary ? FontStyle.Bold : FontStyle.Regular);
        }

        private void StyleInput(TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Margin = new Padding(0, 0, 8, 0);
        }

        private void StyleSmallLabel(Label label)
        {
            label.Dock = DockStyle.Fill;
            label.ForeColor = Muted;
            label.Font = new Font(Font.FontFamily, 8, FontStyle.Bold);
        }

        private void StylePill(Label label)
        {
            label.AutoSize = false;
            label.Width = 112;
            label.Height = 27;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.BackColor = IsAdmin() ? SuccessSoft : WarningSoft;
            label.ForeColor = Ink;
            label.Font = new Font(Font.FontFamily, 8, FontStyle.Bold);
        }

        private void PaintLogo(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Ink))
            using (var pen = new Pen(Color.White, 2.2f))
            {
                e.Graphics.FillRectangle(brush, 0, 0, 38, 38);
                e.Graphics.DrawEllipse(pen, 10, 9, 18, 18);
                e.Graphics.DrawLine(pen, 11, 18, 27, 18);
                e.Graphics.DrawArc(pen, 13, 10, 12, 17, 85, 190);
                e.Graphics.DrawArc(pen, 13, 10, 12, 17, 265, 190);
            }
        }

        private void ApplyLanguage()
        {
            statusLabel.Text = IsAdmin() ? T("Admin", "管理员") : T("Limited", "受限");
            statusLabel.BackColor = IsAdmin() ? SuccessSoft : WarningSoft;
            languageButton.Text = chinese ? "English" : "中文";
            adaptersTitle.Text = T("Network adapters", "网卡列表");
            presetsTitle.Text = T("DNS presets", "DNS 预设");
            customTitle.Text = T("Custom DNS", "自定义 DNS");
            nameLabel.Text = T("Name", "名称");
            primaryLabel.Text = T("Primary", "主 DNS");
            secondaryLabel.Text = T("Secondary", "备用 DNS");
            refreshButton.Text = T("Refresh", "刷新");
            upAdapterButton.Text = "↑";
            downAdapterButton.Text = "↓";
            upPresetButton.Text = "↑";
            downPresetButton.Text = "↓";
            deleteButton.Text = T("Delete", "删除");
            commandsButton.Text = T("Command", "命令");
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
            if (chinese) File.WriteAllText(LanguageFile, "1");
            else if (File.Exists(LanguageFile)) File.Delete(LanguageFile);
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
            var selected = presetsList.SelectedIndex;
            presetsList.Items.Clear();
            foreach (var preset in presets) presetsList.Items.Add(preset);
            if (presetsList.Items.Count > 0) presetsList.SelectedIndex = Math.Max(0, Math.Min(selected, presetsList.Items.Count - 1));
        }

        private void RefreshAdapters()
        {
            var selectedAdapter = adaptersList.SelectedItem as AdapterInfo;
            var selectedName = selectedAdapter == null ? null : selectedAdapter.Name;
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
            adaptersList.Items.Clear();
            foreach (var adapter in adapters) adaptersList.Items.Add(adapter);

            if (adaptersList.Items.Count > 0)
            {
                var index = adapters.FindIndex(a => a.Name == selectedName);
                adaptersList.SelectedIndex = index >= 0 ? index : 0;
            }
            UpdateTargetLabel();
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

        private void UpdateTargetLabel()
        {
            var adapter = adaptersList.SelectedItem as AdapterInfo;
            targetLabel.Text = adapter == null
                ? T("No adapter selected", "未选择网卡")
                : string.Format("{0}: {1}", T("Target", "目标"), adapter.Description);
        }

        private void MoveAdapter(int direction)
        {
            MoveListItem(adaptersList, adapters, direction);
            SaveAdapterOrder();
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

        private static void MeasureListItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 72;
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var box = (ListBox)sender;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 4, e.Bounds.Width - 8, e.Bounds.Height - 8);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var back = new SolidBrush(selected ? AccentSoft : Color.FromArgb(250, 251, 253)))
            using (var border = new Pen(selected ? Accent : Line))
            {
                FillRoundRect(e.Graphics, back, bounds, 8);
                DrawRoundRect(e.Graphics, border, bounds, 8);
            }

            var adapter = box.Items[e.Index] as AdapterInfo;
            if (adapter != null)
            {
                DrawAdapterItem(e.Graphics, bounds, adapter);
            }

            var preset = box.Items[e.Index] as DnsPreset;
            if (preset != null)
            {
                DrawPresetItem(e.Graphics, bounds, preset);
            }
        }

        private void DrawAdapterItem(Graphics g, Rectangle bounds, AdapterInfo adapter)
        {
            var title = TrimText(g, adapter.Description, new Font(Font.FontFamily, 9, FontStyle.Bold), bounds.Width - 24);
            var dns = adapter.Dns.Count == 0 ? "DNS: -" : "DNS: " + string.Join(", ", adapter.Dns.ToArray());
            TextRenderer.DrawText(g, title, new Font(Font.FontFamily, 9, FontStyle.Bold), new Point(bounds.X + 12, bounds.Y + 9), Ink);
            TextRenderer.DrawText(g, adapter.Name + " · " + adapter.Address, Font, new Point(bounds.X + 12, bounds.Y + 30), Muted);
            TextRenderer.DrawText(g, dns, Font, new Point(bounds.X + 12, bounds.Y + 49), Accent);
        }

        private void DrawPresetItem(Graphics g, Rectangle bounds, DnsPreset preset)
        {
            var title = TrimText(g, preset.Name, new Font(Font.FontFamily, 9, FontStyle.Bold), bounds.Width - 24);
            var dns = string.IsNullOrWhiteSpace(preset.Secondary) ? preset.Primary : preset.Primary + " / " + preset.Secondary;
            TextRenderer.DrawText(g, title, new Font(Font.FontFamily, 9, FontStyle.Bold), new Point(bounds.X + 12, bounds.Y + 13), Ink);
            TextRenderer.DrawText(g, dns, Font, new Point(bounds.X + 12, bounds.Y + 37), Muted);
        }

        private static string TrimText(Graphics g, string text, Font font, int maxWidth)
        {
            if (TextRenderer.MeasureText(g, text, font).Width <= maxWidth) return text;
            while (text.Length > 4 && TextRenderer.MeasureText(g, text + "...", font).Width > maxWidth)
            {
                text = text.Substring(0, text.Length - 1);
            }
            return text + "...";
        }

        private static void FillRoundRect(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (var path = RoundRect(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawRoundRect(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using (var path = RoundRect(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
