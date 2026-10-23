using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SonarAutoSwitch
{
    public class SettingsForm : Form
    {
        private AppConfig _config;
        private SonarAppContext _appContext;

        // UI Controls
        private ListBox _listPriorities;
        private ComboBox _comboActiveDevices;
        private TextBox _txtCustomKeyword;
        private ComboBox _comboDefaultDevice;
        private CheckBox _chkNotifications;
        private CheckBox _chkAutoStart;
        private Label _lblStatus;

        // Color Palette (Modern Dark / Catppuccin Mocha style)
        private static readonly Color BgDark = Color.FromArgb(24, 24, 37);
        private static readonly Color BgCard = Color.FromArgb(30, 30, 46);
        private static readonly Color BgInput = Color.FromArgb(49, 50, 68);
        private static readonly Color TextPrimary = Color.FromArgb(205, 214, 244);
        private static readonly Color TextMuted = Color.FromArgb(166, 173, 200);
        private static readonly Color AccentBlue = Color.FromArgb(137, 180, 250);
        private static readonly Color AccentGreen = Color.FromArgb(166, 227, 161);
        private static readonly Color AccentRed = Color.FromArgb(243, 139, 168);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public SettingsForm(AppConfig config, SonarAppContext appContext)
        {
            _config = config;
            _appContext = appContext;

            InitializeComponents();
            LoadConfigData();
            RefreshDeviceLists();
        }

        public void CenterOnPrimaryScreen()
        {
            try
            {
                var primary = Screen.PrimaryScreen.WorkingArea;
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(
                    primary.Left + Math.Max(0, (primary.Width - this.Width) / 2),
                    primary.Top + Math.Max(0, (primary.Height - this.Height) / 2)
                );
            }
            catch { }
        }

        public void BringToFrontOfPrimaryScreen()
        {
            try
            {
                this.ShowInTaskbar = true;
                this.Opacity = 1;
                this.Visible = true;
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                CenterOnPrimaryScreen();
                this.Show();
                ShowWindowAsync(this.Handle, 9 /* SW_RESTORE */);
                this.BringToFront();
                this.Activate();
                SetForegroundWindow(this.Handle);
            }
            catch (Exception ex)
            {
                SonarClient.Log("Error in BringToFrontOfPrimaryScreen: " + ex.Message);
            }
        }

        private void InitializeComponents()
        {
            this.Text = "SonarAutoSwitch - Settings";
            this.Size = new Size(600, 720);
            this.StartPosition = FormStartPosition.Manual;
            CenterOnPrimaryScreen();
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = BgDark;
            this.ForeColor = TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            try
            {
                Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (appIcon != null) this.Icon = appIcon;
            }
            catch { }

            // Header Panel
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = BgCard,
                Padding = new Padding(20, 12, 20, 10)
            };

            Label titleLabel = new Label
            {
                Text = "🎧 Sonar Auto Switcher",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = AccentBlue,
                AutoSize = true,
                Location = new Point(18, 12)
            };

            _lblStatus = new Label
            {
                Text = "Status: Detecting Sonar...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(22, 40)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(_lblStatus);
            this.Controls.Add(headerPanel);

            // Main Container
            Panel mainPanel = new Panel
            {
                Location = new Point(18, 80),
                Size = new Size(550, 530),
                AutoScroll = true
            };

            int curY = 5;

            // Section 1: Priorities
            Label lblPrioTitle = new Label
            {
                Text = "Fallback Priority List (Checked Top-to-Bottom):",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AccentGreen,
                AutoSize = true,
                Location = new Point(5, curY)
            };
            mainPanel.Controls.Add(lblPrioTitle);
            curY += 24;

            Label lblPrioHelp = new Label
            {
                Text = "When connected, the topmost active device will automatically receive all Sonar audio.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.2f),
                AutoSize = true,
                Location = new Point(5, curY)
            };
            mainPanel.Controls.Add(lblPrioHelp);
            curY += 20;

            _listPriorities = new ListBox
            {
                Location = new Point(5, curY),
                Size = new Size(430, 110),
                BackColor = BgInput,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f)
            };
            mainPanel.Controls.Add(_listPriorities);

            Button btnUp = CreateButton("▲ Up", new Point(445, curY), new Size(95, 32));
            btnUp.Click += (s, e) => MovePriorityItem(-1);
            mainPanel.Controls.Add(btnUp);

            Button btnDown = CreateButton("▼ Down", new Point(445, curY + 38), new Size(95, 32));
            btnDown.Click += (s, e) => MovePriorityItem(1);
            mainPanel.Controls.Add(btnDown);

            Button btnRemove = CreateButton("✕ Delete", new Point(445, curY + 76), new Size(95, 32));
            btnRemove.ForeColor = AccentRed;
            btnRemove.Click += (s, e) => RemovePriorityItem();
            mainPanel.Controls.Add(btnRemove);

            curY += 120;

            // Add Device Sub-section
            Label lblAddDesc = new Label
            {
                Text = "Add Device by Detected Name or Custom Keyword:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(5, curY)
            };
            mainPanel.Controls.Add(lblAddDesc);
            curY += 22;

            _comboActiveDevices = new ComboBox
            {
                Location = new Point(5, curY),
                Size = new Size(340, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            mainPanel.Controls.Add(_comboActiveDevices);

            Button btnAddDetected = CreateButton("+ Add Detected", new Point(355, curY - 1), new Size(115, 27));
            btnAddDetected.Click += (s, e) => AddSelectedDetected();
            mainPanel.Controls.Add(btnAddDetected);

            Button btnRefresh = CreateButton("🔄", new Point(478, curY - 1), new Size(40, 27));
            btnRefresh.Click += (s, e) => RefreshDeviceLists();
            mainPanel.Controls.Add(btnRefresh);

            curY += 34;

            _txtCustomKeyword = new TextBox
            {
                Location = new Point(5, curY),
                Size = new Size(340, 25),
                BackColor = BgInput,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            SetWatermark(_txtCustomKeyword, "Or type keyword (e.g. TOZO, AirPods, Sony)");
            mainPanel.Controls.Add(_txtCustomKeyword);

            Button btnAddKeyword = CreateButton("+ Add Keyword", new Point(355, curY - 1), new Size(115, 27));
            btnAddKeyword.Click += (s, e) => AddCustomKeyword();
            mainPanel.Controls.Add(btnAddKeyword);

            curY += 45;

            // Section 2: Default Device
            Label lblDefTitle = new Label
            {
                Text = "Default / Secondary Device (When priority devices are disconnected):",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AccentBlue,
                AutoSize = true,
                Location = new Point(5, curY)
            };
            mainPanel.Controls.Add(lblDefTitle);
            curY += 24;

            _comboDefaultDevice = new ComboBox
            {
                Location = new Point(5, curY),
                Size = new Size(430, 28),
                BackColor = BgInput,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            mainPanel.Controls.Add(_comboDefaultDevice);

            curY += 45;

            // Section 3: Settings & Options
            Label lblOptionsTitle = new Label
            {
                Text = "Options & Behavior:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(5, curY)
            };
            mainPanel.Controls.Add(lblOptionsTitle);
            curY += 24;

            _chkNotifications = new CheckBox
            {
                Text = "Show Toast Notification on Switch (إشعار عند حدوث تبديل)",
                Location = new Point(10, curY),
                Size = new Size(500, 24),
                ForeColor = TextPrimary,
                Checked = _config.ShowNotifications
            };
            mainPanel.Controls.Add(_chkNotifications);
            curY += 28;

            _chkAutoStart = new CheckBox
            {
                Text = "Start automatically with Windows (تشغيل تلقائي مع بدء النظام)",
                Location = new Point(10, curY),
                Size = new Size(500, 24),
                ForeColor = TextPrimary,
                Checked = _config.AutoStartWithWindows
            };
            mainPanel.Controls.Add(_chkAutoStart);
            curY += 35;

            Button btnOpenLogs = CreateButton("📄 View Logs (سجل العمليات)", new Point(10, curY), new Size(200, 30));
            btnOpenLogs.Click += (s, e) => OpenLogFile();
            mainPanel.Controls.Add(btnOpenLogs);

            this.Controls.Add(mainPanel);

            // Bottom Action Bar
            Panel footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                BackColor = BgCard,
                Padding = new Padding(20, 15, 20, 15)
            };

            Button btnSave = new Button
            {
                Text = "Save & Apply (حفظ وتطبيق)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Size = new Size(180, 38),
                Location = new Point(220, 14),
                BackColor = AccentGreen,
                ForeColor = Color.FromArgb(17, 17, 27),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveAndApply();
            footerPanel.Controls.Add(btnSave);

            Button btnCancel = new Button
            {
                Text = "Hide to Tray (إخفاء)",
                Size = new Size(130, 38),
                Location = new Point(410, 14),
                BackColor = BgInput,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.Hide();
            footerPanel.Controls.Add(btnCancel);

            this.Controls.Add(footerPanel);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        private Button CreateButton(string text, Point loc, Size sz)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = sz,
                BackColor = BgInput,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(69, 71, 90);
            return btn;
        }

        private void SetWatermark(TextBox tb, string watermark)
        {
            tb.Text = watermark;
            tb.ForeColor = TextMuted;
            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == watermark)
                {
                    tb.Text = "";
                    tb.ForeColor = TextPrimary;
                }
            };
            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = watermark;
                    tb.ForeColor = TextMuted;
                }
            };
        }

        private void LoadConfigData()
        {
            _listPriorities.Items.Clear();
            foreach (var item in _config.PriorityDevices)
            {
                _listPriorities.Items.Add(item);
            }

            _comboDefaultDevice.Text = _config.DefaultDevice;
            _chkNotifications.Checked = _config.ShowNotifications;
            _chkAutoStart.Checked = _config.AutoStartWithWindows;

            int port = SonarClient.GetSonarPort();
            if (port > 0)
            {
                _lblStatus.Text = string.Format("Status: Connected to SteelSeries Sonar (Port {0})", port);
                _lblStatus.ForeColor = AccentGreen;
            }
            else
            {
                _lblStatus.Text = "Status: SteelSeries Sonar not detected or running without Sonar enabled";
                _lblStatus.ForeColor = AccentRed;
            }
        }

        private void RefreshDeviceLists()
        {
            _comboActiveDevices.Items.Clear();
            _comboDefaultDevice.Items.Clear();

            var devices = AudioHelper.GetAllRenderDevices();

            foreach (var dev in devices)
            {
                string item = dev.DisplayName;
                _comboActiveDevices.Items.Add(item);
                _comboDefaultDevice.Items.Add(item);
            }

            if (_comboActiveDevices.Items.Count > 0)
            {
                _comboActiveDevices.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(_config.DefaultDevice))
            {
                _comboDefaultDevice.Text = _config.DefaultDevice;
            }
        }

        private void AddSelectedDetected()
        {
            if (_comboActiveDevices.SelectedItem == null) return;
            string selected = _comboActiveDevices.SelectedItem.ToString();
            string name = selected;
            int paren = selected.IndexOf(" (");
            if (paren > 0) name = selected.Substring(0, paren);

            if (!_listPriorities.Items.Contains(name))
            {
                _listPriorities.Items.Add(name);
            }
        }

        private void AddCustomKeyword()
        {
            string kw = _txtCustomKeyword.Text.Trim();
            if (string.IsNullOrEmpty(kw) || kw.StartsWith("Or type keyword")) return;

            if (!_listPriorities.Items.Contains(kw))
            {
                _listPriorities.Items.Add(kw);
                _txtCustomKeyword.Text = "";
            }
        }

        private void MovePriorityItem(int direction)
        {
            int idx = _listPriorities.SelectedIndex;
            if (idx == -1) return;

            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _listPriorities.Items.Count) return;

            object item = _listPriorities.Items[idx];
            _listPriorities.Items.RemoveAt(idx);
            _listPriorities.Items.Insert(newIdx, item);
            _listPriorities.SelectedIndex = newIdx;
        }

        private void RemovePriorityItem()
        {
            int idx = _listPriorities.SelectedIndex;
            if (idx == -1) return;
            _listPriorities.Items.RemoveAt(idx);
        }

        private void SaveAndApply()
        {
            var newList = new List<string>();
            foreach (var item in _listPriorities.Items)
            {
                newList.Add(item.ToString());
            }

            _config.PriorityDevices = newList;
            _config.DefaultDevice = _comboDefaultDevice.Text.Trim();
            _config.ShowNotifications = _chkNotifications.Checked;
            _config.AutoStartWithWindows = _chkAutoStart.Checked;

            _config.Save();

            ConfigureStartupShortcut(_config.AutoStartWithWindows);
            if (_appContext != null)
            {
                _appContext.NotifyConfigUpdated(_config);
            }

            MessageBox.Show(
                "تم حفظ الإعدادات بنجاح وتطبيقها على خدمة التحويل الفوري!",
                "Sonar Auto Switch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            this.Hide();
        }

        public static void ConfigureStartupShortcut(bool enable)
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string lnkPath = Path.Combine(startupPath, "SonarAutoSwitch.lnk");

                if (!enable)
                {
                    if (File.Exists(lnkPath)) File.Delete(lnkPath);
                }
                else
                {
                    string exePath = Application.ExecutablePath;
                    CreateShortcut(lnkPath, exePath, "--silent");
                }
            }
            catch (Exception ex)
            {
                SonarClient.Log("Error updating startup shortcut: " + ex.Message);
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string arguments = "")
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                if (!string.IsNullOrEmpty(arguments))
                {
                    shortcut.Arguments = arguments;
                }
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = "Sonar Auto Switcher";
                shortcut.IconLocation = targetPath + ",0";
                shortcut.Save();
            }
            catch { }
        }

        private void OpenLogFile()
        {
            try
            {
                if (File.Exists(SonarClient.LogPath))
                {
                    Process.Start("notepad.exe", SonarClient.LogPath);
                }
                else
                {
                    MessageBox.Show("لا يوجد سجل حتى الآن.", "SonarAutoSwitch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في فتح السجل: " + ex.Message);
            }
        }
    }
}
