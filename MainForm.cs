using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace NetheritInjector
{
    public partial class MainForm : Form
    {
        private List<Snowflake> snowflakes = null!;
        private System.Windows.Forms.Timer animationTimer = null!;
        private System.Windows.Forms.Timer subscriptionTimer = null!;
        private Random random = null!;
        private TextBox processTextBox = null!;
        private TextBox dllTextBox = null!;
        private Button injectButton = null!;
        private Label subscriptionLabel = null!;
        private Label timeLeftLabel = null!;
        private Button settingsButton = null!;
        private Button historyButton = null!;
        private Label processInfoLabel = null!;
        private ProgressBar injectionProgressBar = null!;
        private NotifyIcon? trayIcon;
        private AppConfig config = null!;
        private string? selectedDllPath;
        private int selectedProcessId;
        private string? selectedProcessName;
        private string? activatedKey;
        private int subscriptionDays;

        // Windows API для инжекта DLL
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int HT_CAPTION = 0x2;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        private const int PROCESS_CREATE_THREAD = 0x0002;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_OPERATION = 0x0008;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_READ = 0x0010;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RESERVE = 0x00002000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 4;

        public MainForm()
        {
            InitializeComponent();
            InitializeSnowflakes();
            CheckAdminRights();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 25, 25));
        }

        public MainForm(string key, int durationDays) : this()
        {
            activatedKey = key;
            subscriptionDays = durationDays;
            config = AppConfig.Load();
            LoadLastSettings();
            UpdateSubscriptionDisplay();
            StartSubscriptionTimer();
            InitializeTrayIcon();
            RegisterHotKeys();
        }

        private void LoadLastSettings()
        {
            if (!string.IsNullOrEmpty(config.LastDllPath) && File.Exists(config.LastDllPath))
            {
                selectedDllPath = config.LastDllPath;
                if (dllTextBox != null)
                    dllTextBox.Text = Path.GetFileName(selectedDllPath);
            }
            UpdateInjectButtonState();
        }

        private void InitializeTrayIcon()
        {
            if (!config.MinimizeToTray) return;

            trayIcon = new NotifyIcon
            {
                Text = "Netherit Injector",
                Visible = false
            };

            // Создаем простую иконку
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Purple);
                g.FillEllipse(Brushes.White, 4, 4, 8, 8);
                trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());
            }

            trayIcon.DoubleClick += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Открыть", null, (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
            });
            contextMenu.Items.Add("Настройки", null, (s, e) => ShowSettings());
            contextMenu.Items.Add("История", null, (s, e) => ShowHistory());
            contextMenu.Items.Add("-"); // Разделитель
            contextMenu.Items.Add("Выход", null, (s, e) => {
                trayIcon.Visible = false;
                Application.Exit();
            });
            
            trayIcon.ContextMenuStrip = contextMenu;

            // Обработка минимизации
            this.Resize += (s, e) => {
                if (config.MinimizeToTray && this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    trayIcon.Visible = true;
                    if (config.ShowNotifications)
                    {
                        trayIcon.ShowBalloonTip(1000, "Netherit Injector", "Приложение свернуто в трей", ToolTipIcon.Info);
                    }
                }
            };
        }

        private void RegisterHotKeys()
        {
            this.KeyPreview = true;
            this.KeyDown += (s, e) => {
                // Ctrl+I = Inject
                if (e.Control && e.KeyCode == Keys.I && injectButton.Enabled)
                {
                    InjectButton_Click(null, EventArgs.Empty);
                }
                // Ctrl+H = History
                else if (e.Control && e.KeyCode == Keys.H)
                {
                    ShowHistory();
                }
                // Ctrl+S = Settings  
                else if (e.Control && e.KeyCode == Keys.S)
                {
                    ShowSettings();
                }
            };
        }

        private void StartSubscriptionTimer()
        {
            subscriptionTimer = new System.Windows.Forms.Timer();
            subscriptionTimer.Interval = 1000; // Обновляем каждую секунду
            subscriptionTimer.Tick += SubscriptionTimer_Tick;
            subscriptionTimer.Start();
        }

        private void SubscriptionTimer_Tick(object? sender, EventArgs e)
        {
            if (activatedKey == null) return;

            // Проверяем истек ли ключ
            if (KeySystem.IsKeyExpired(activatedKey))
            {
                subscriptionTimer?.Stop();
                MessageBox.Show("⏰ Ваша подписка истекла!\n\nПожалуйста, активируйте новый ключ.", 
                    "Подписка истекла", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            UpdateSubscriptionDisplay();
        }

        private void CheckAdminRights()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                if (!isAdmin)
                {
                    MessageBox.Show("⚠️ WARNING: Not running as Administrator!\n\nDLL injection requires administrator privileges.\nPlease restart the application as Administrator.", "Admin Rights Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Настройка формы
            this.Text = "Netherit Injector";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.95;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(5, 5, 10); // Ultra Dark
            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
            this.DoubleBuffered = true;
            this.Paint += MainForm_Paint;

            // Кнопка закрытия (X)
            Button closeButton = new Button
            {
                Text = "×",
                Font = new Font("Arial", 22, FontStyle.Bold),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                Size = new Size(45, 45),
                Location = new Point(645, 3),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 0, 0);
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.LightGray;
            closeButton.Click += (s, e) => {
                if (config.MinimizeToTray && trayIcon != null)
                {
                    this.WindowState = FormWindowState.Minimized;
                }
                else
                {
                    Application.Exit();
                }
            };
            var closeToolTip = new ToolTip();
            closeToolTip.SetToolTip(closeButton, "Close");
            this.Controls.Add(closeButton);

            // Кнопка минимизации (_)
            Button minimizeButton = new Button
            {
                Text = "—",
                Font = new Font("Arial", 18, FontStyle.Bold),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                Size = new Size(45, 45),
                Location = new Point(595, 3),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            minimizeButton.MouseEnter += (s, e) => minimizeButton.ForeColor = Color.White;
            minimizeButton.MouseLeave += (s, e) => minimizeButton.ForeColor = Color.LightGray;
            minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            var minimizeToolTip = new ToolTip();
            minimizeToolTip.SetToolTip(minimizeButton, "Minimize");
            this.Controls.Add(minimizeButton);

            // Название
            Label titleLabel = new Label
            {
                Text = "NETHERIT",
                Font = new Font("Verdana", 48, FontStyle.Bold),
                ForeColor = Color.MediumOrchid,
                AutoSize = false,
                Size = new Size(700, 80),
                Location = new Point(0, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(titleLabel);

            // Подзаголовок
            Label subtitleLabel = new Label
            {
                Text = "PREMIUM INJECTOR",
                Font = new Font("Verdana", 14, FontStyle.Regular),
                ForeColor = Color.DarkGray,
                AutoSize = false,
                Size = new Size(700, 30),
                Location = new Point(0, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(subtitleLabel);

            // Subscription info (at the top)
            subscriptionLabel = new Label
            {
                Text = "Loading subscription...",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.MediumOrchid,
                AutoSize = false,
                Size = new Size(700, 25),
                Location = new Point(0, 155),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(subscriptionLabel);

            // Time left label
            timeLeftLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Light", 10),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(700, 25),
                Location = new Point(0, 175),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(timeLeftLabel);

            // Settings button (top right)
            settingsButton = new Button
            {
                Text = "⚙",
                Font = new Font("Segoe UI Symbol", 20, FontStyle.Bold),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                Size = new Size(45, 45),
                Location = new Point(585, 8),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            settingsButton.FlatAppearance.BorderSize = 0;
            settingsButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            settingsButton.MouseEnter += (s, e) => settingsButton.ForeColor = Color.MediumOrchid;
            settingsButton.MouseLeave += (s, e) => settingsButton.ForeColor = Color.LightGray;
            settingsButton.Click += (s, e) => ShowSettings();
            var settingsToolTip = new ToolTip();
            settingsToolTip.SetToolTip(settingsButton, "Settings (Ctrl+S)");
            this.Controls.Add(settingsButton);

            // History button
            historyButton = new Button
            {
                Text = "H",
                Font = new Font("Consolas", 18, FontStyle.Bold),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                Size = new Size(45, 45),
                Location = new Point(535, 8),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            historyButton.FlatAppearance.BorderSize = 0;
            historyButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
            historyButton.MouseEnter += (s, e) => historyButton.ForeColor = Color.MediumOrchid;
            historyButton.MouseLeave += (s, e) => historyButton.ForeColor = Color.LightGray;
            historyButton.Click += (s, e) => ShowHistory();
            var historyToolTip = new ToolTip();
            historyToolTip.SetToolTip(historyButton, "History (Ctrl+H)");
            this.Controls.Add(historyButton);

            // Process info label
            processInfoLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(400, 40),
                Location = new Point(150, 240),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(processInfoLabel);

            // Панель для процесса
            Label processLabel = new Label
            {
                Text = "TARGET PROCESS",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(700, 20),
                Location = new Point(0, 205),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(processLabel);

            processTextBox = new TextBox
            {
                Font = new Font("Segoe UI Light", 16),
                Size = new Size(400, 35),
                Location = new Point(150, 230),
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center,
                Text = "Not Selected"
            };
            this.Controls.Add(processTextBox);

            Panel procLine = new Panel { Size = new Size(400, 1), Location = new Point(150, 265), BackColor = Color.FromArgb(60, 60, 60) };
            this.Controls.Add(procLine);

            Button selectProcessButton = new Button
            {
                Text = "CHANGE PROCESS",
                Font = new Font("Segoe UI Light", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(160, 30),
                Location = new Point(270, 275),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            selectProcessButton.FlatAppearance.BorderSize = 1;
            selectProcessButton.FlatAppearance.BorderColor = Color.White;
            selectProcessButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 25, 25);
            selectProcessButton.Click += SelectProcessButton_Click;
            this.Controls.Add(selectProcessButton);

            // Панель для DLL
            Label dllLabel = new Label
            {
                Text = "INJECTION DLL",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(700, 20),
                Location = new Point(0, 335),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(dllLabel);

            dllTextBox = new TextBox
            {
                Font = new Font("Segoe UI Light", 16),
                Size = new Size(400, 35),
                Location = new Point(150, 360),
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center,
                Text = "Not Selected"
            };
            this.Controls.Add(dllTextBox);

            Panel dllLine = new Panel { Size = new Size(400, 1), Location = new Point(150, 395), BackColor = Color.FromArgb(60, 60, 60) };
            this.Controls.Add(dllLine);

            Button browseDllButton = new Button
            {
                Text = "BROWSE FILE",
                Font = new Font("Segoe UI Light", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(160, 30),
                Location = new Point(270, 405),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            browseDllButton.FlatAppearance.BorderSize = 1;
            browseDllButton.FlatAppearance.BorderColor = Color.White;
            browseDllButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 25, 25);
            browseDllButton.Click += BrowseDllButton_Click;
            this.Controls.Add(browseDllButton);

            // Кнопка инжекта
            injectButton = new Button
            {
                Text = "INJECT",
                Font = new Font("Segoe UI Light", 18),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(200, 50),
                Location = new Point(250, 485),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            injectButton.FlatAppearance.BorderSize = 1;
            injectButton.FlatAppearance.BorderColor = Color.White;
            injectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            injectButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 50);
            injectButton.Click += InjectButton_Click;
            this.Controls.Add(injectButton);

            // Progress bar for injection
            injectionProgressBar = new ProgressBar
            {
                Location = new Point(200, 545),
                Size = new Size(300, 8),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            this.Controls.Add(injectionProgressBar);

            // Version and credits label
            Label versionLabel = new Label
            {
                Text = "v2.0.0 | Made with ♥ by Netherit Team",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(700, 20),
                Location = new Point(0, 555),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(versionLabel);

            // Hotkeys hint
            Label hotkeysLabel = new Label
            {
                Text = "Hotkeys: Ctrl+I (Inject) | Ctrl+H (History) | Ctrl+S (Settings)",
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.DarkGray,
                AutoSize = false,
                Size = new Size(700, 15),
                Location = new Point(0, 572),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(hotkeysLabel);

            // Статус
            Label statusLabel = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI Light", 10),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(700, 20),
                Location = new Point(0, 530),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(statusLabel);

            // Таймер для анимации снежинок
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16; // ~60 FPS
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            random = new Random();
            this.ResumeLayout(false);
        }

        private void UpdateSubscriptionDisplay()
        {
            if (subscriptionLabel == null || timeLeftLabel == null) return;

            if (activatedKey == null)
            {
                subscriptionLabel.Text = "🔑 No active subscription";
                subscriptionLabel.ForeColor = Color.Gray;
                timeLeftLabel.Text = "";
                return;
            }

            long timeLeft = KeySystem.GetKeyTimeLeft(activatedKey);
            
            if (timeLeft <= 0)
            {
                subscriptionLabel.Text = "❌ Subscription expired";
                subscriptionLabel.ForeColor = Color.Red;
                timeLeftLabel.Text = "";
                return;
            }

            string timeText = KeySystem.FormatTimeLeft(timeLeft);

            if (subscriptionDays == -1)
            {
                subscriptionLabel.Text = "🔑 Subscription: LIFETIME";
                subscriptionLabel.ForeColor = Color.FromArgb(100, 255, 100);
                timeLeftLabel.Text = "∞ Бессрочная подписка";
                timeLeftLabel.ForeColor = Color.FromArgb(100, 255, 100);
            }
            else
            {
                long seconds = timeLeft / 1000;
                long hours = seconds / 3600;
                
                subscriptionLabel.Text = $"🔑 Active Subscription";
                timeLeftLabel.Text = $"⏰ {timeText} remaining";
                
                if (hours < 24)
                {
                    subscriptionLabel.ForeColor = Color.Orange;
                    timeLeftLabel.ForeColor = Color.Orange;
                }
                else if (hours < 168) // < 7 days
                {
                    subscriptionLabel.ForeColor = Color.Yellow;
                    timeLeftLabel.ForeColor = Color.Yellow;
                }
                else
                {
                    subscriptionLabel.ForeColor = Color.FromArgb(100, 255, 100);
                    timeLeftLabel.ForeColor = Color.FromArgb(150, 150, 150);
                }
            }
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            // Minimal Solid Background
            e.Graphics.Clear(Color.FromArgb(18, 18, 18));

            // Draw Border
            using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 40), 1))
            {
                e.Graphics.DrawPath(borderPen, GetRoundedPath(ClientRectangle, 25));
            }
        }
        
        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;
            Size size = new Size((int)diameter, (int)diameter);
            Rectangle arc = new Rectangle(rect.Location, size);
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - (int)diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - (int)diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SelectProcessButton_Click(object? sender, EventArgs e)
        {
            ProcessSelectDialog processDialog = new ProcessSelectDialog();
            if (processDialog.ShowDialog() == DialogResult.OK)
            {
                selectedProcessId = processDialog.SelectedProcessId;
                selectedProcessName = processDialog.SelectedProcessName;
                processTextBox.Text = selectedProcessName ?? $"Process ID: {selectedProcessId}";
                UpdateInjectButtonState();
                UpdateProcessInfo();
                
                // Сохраняем выбор
                if (config != null)
                {
                    config.LastProcessId = selectedProcessId;
                    config.LastProcessName = selectedProcessName;
                    config.Save();
                }
            }
        }

        private void UpdateProcessInfo()
        {
            if (!config?.ShowProcessInfo == true || selectedProcessId <= 0)
            {
                processInfoLabel.Text = "";
                return;
            }

            try
            {
                var process = Process.GetProcessById(selectedProcessId);
                string arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                string path = "";
                try { path = process.MainModule?.FileName ?? "N/A"; } catch { path = "N/A"; }
                
                processInfoLabel.Text = $"PID: {selectedProcessId} | Arch: {arch}\nPath: {(path.Length > 50 ? "..." + path.Substring(path.Length - 50) : path)}";
                processInfoLabel.ForeColor = Color.FromArgb(100, 200, 255);
            }
            catch
            {
                processInfoLabel.Text = "Process no longer exists";
                processInfoLabel.ForeColor = Color.Red;
            }
        }

        private void ShowSettings()
        {
            using (var settingsForm = new SettingsForm(config))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    config = AppConfig.Load();
                    UpdateProcessInfo();
                }
            }
        }

        private void ShowHistory()
        {
            using (var historyForm = new HistoryForm())
            {
                historyForm.ShowDialog();
            }
        }

        private void BrowseDllButton_Click(object? sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                Title = "Select DLL file to inject"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedDllPath = openFileDialog.FileName;
                dllTextBox.Text = Path.GetFileName(selectedDllPath);
                UpdateInjectButtonState();
                
                // Сохраняем выбор
                if (config != null)
                {
                    config.LastDllPath = selectedDllPath;
                    config.Save();
                }
            }
        }

        private void UpdateInjectButtonState()
        {
            bool canInject = selectedProcessId > 0 && !string.IsNullOrEmpty(selectedDllPath);
            
            // Проверяем что процесс все еще существует
            if (canInject && selectedProcessId > 0)
            {
                try
                {
                    Process.GetProcessById(selectedProcessId);
                }
                catch (ArgumentException)
                {
                    // Процесс завершился
                    canInject = false;
                    processTextBox.Text = "Process not found (closed)";
                    selectedProcessId = 0;
                }
            }
            
            // Проверяем что DLL файл существует
            if (canInject && !string.IsNullOrEmpty(selectedDllPath))
            {
                if (!File.Exists(selectedDllPath))
                {
                    canInject = false;
                    dllTextBox.Text = "DLL file not found";
                    selectedDllPath = "";
                }
            }
            
            injectButton.Enabled = canInject;
        }

        private void InitializeSnowflakes()
        {
            snowflakes = new List<Snowflake>();
            for (int i = 0; i < 80; i++)
            {
                snowflakes.Add(new Snowflake
                {
                    X = random.Next(0, this.Width),
                    Y = random.Next(-this.Height, 0),
                    Size = random.Next(2, 10),
                    Speed = random.NextDouble() * 2 + 0.5,
                    Opacity = random.Next(80, 255),
                    SwingAmplitude = random.NextDouble() * 2
                });
            }
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var snowflake in snowflakes)
            {
                snowflake.Y += (float)snowflake.Speed;
                snowflake.X += (float)(Math.Sin(snowflake.Y * 0.02) * snowflake.SwingAmplitude);

                if (snowflake.Y > this.Height)
                {
                    snowflake.Y = -10;
                    snowflake.X = random.Next(0, this.Width);
                }

                if (snowflake.X < -50 || snowflake.X > this.Width + 50)
                {
                    snowflake.X = random.Next(0, this.Width);
                }
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Рисуем снежинки
            foreach (var snowflake in snowflakes)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(snowflake.Opacity, 255, 255, 255)))
                {
                    g.FillEllipse(brush, snowflake.X, snowflake.Y, snowflake.Size, snowflake.Size);
                }
            }
        }

        private async void InjectButton_Click(object? sender, EventArgs e)
        {
            // Проверяем истек ли ключ перед инъекцией
            if (activatedKey != null && KeySystem.IsKeyExpired(activatedKey))
            {
                MessageBox.Show("⏰ Ваша подписка истекла!\n\nПожалуйста, активируйте новый ключ для продолжения.", 
                    "Подписка истекла", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            if (string.IsNullOrEmpty(selectedDllPath) || selectedProcessId <= 0)
            {
                MessageBox.Show("Please select both a process and DLL file first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Показываем прогресс-бар
            injectionProgressBar.Value = 0;
            injectionProgressBar.Visible = true;
            injectButton.Enabled = false;

            // Задержка если настроена
            if (config.InjectionDelay > 0)
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    injectionProgressBar.Value = i / 4; // 0-25%
                    await System.Threading.Tasks.Task.Delay(config.InjectionDelay / 10);
                }
            }

            injectionProgressBar.Value = 30;
            await System.Threading.Tasks.Task.Delay(50);

            InjectDLL(selectedProcessId, selectedDllPath);
            
            injectionProgressBar.Value = 100;
            await System.Threading.Tasks.Task.Delay(500);
            injectionProgressBar.Visible = false;
            injectButton.Enabled = true;
        }

        private void InjectDLL(int processId, string dllPath)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr allocMemAddress = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            bool success = false;
            string errorMessage = "";

            try
            {
                // Прогресс: 35%
                injectionProgressBar.Value = 35;
                
                // Проверяем существование файла
                if (!File.Exists(dllPath))
                {
                    errorMessage = $"DLL file not found:\n{dllPath}";
                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, "DLL file not found");
                    return;
                }

                // Прогресс: 40%
                injectionProgressBar.Value = 40;
                
                // Открываем процесс
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to open process. Error: {error}";
                    MessageBox.Show($"Failed to open process.\nError code: {error}\n\nMake sure you run the injector as Administrator!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                // Прогресс: 50%
                injectionProgressBar.Value = 50;
                
                // Получаем адрес LoadLibraryW (используем Unicode версию)
                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                if (hKernel32 == IntPtr.Zero)
                {
                    errorMessage = "Failed to get kernel32.dll handle";
                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                IntPtr loadLibraryAddr = GetProcAddress(hKernel32, "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero)
                {
                    errorMessage = "Failed to find LoadLibraryW";
                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                // Прогресс: 60%
                injectionProgressBar.Value = 60;
                
                // Конвертируем путь в Unicode с нулевым терминатором
                byte[] dllPathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
                uint size = (uint)dllPathBytes.Length;

                // Выделяем память в целевом процессе
                allocMemAddress = VirtualAllocEx(hProcess, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                if (allocMemAddress == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to allocate memory. Error: {error}";
                    MessageBox.Show($"Failed to allocate memory in target process.\nError code: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                // Прогресс: 70%
                injectionProgressBar.Value = 70;
                
                // Записываем путь к DLL в память процесса
                if (!WriteProcessMemory(hProcess, allocMemAddress, dllPathBytes, size, out UIntPtr bytesWritten))
                {
                    int error = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write memory. Error: {error}";
                    MessageBox.Show($"Failed to write to process memory.\nError code: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                // Прогресс: 80%
                injectionProgressBar.Value = 80;
                
                // Создаем удаленный поток для загрузки DLL
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to create thread. Error: {error}";
                    MessageBox.Show($"Failed to create remote thread.\nError code: {error}\n\nPossible reasons:\n- Target process architecture mismatch (32-bit vs 64-bit)\n- Antivirus blocking\n- Process protection", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    return;
                }

                // Прогресс: 90%
                injectionProgressBar.Value = 90;
                
                // Ждем завершения потока (максимум 5 секунд)
                uint waitResult = WaitForSingleObject(hThread, 5000);

                if (waitResult == 0) // WAIT_OBJECT_0
                {
                    // Получаем код возврата потока
                    if (GetExitCodeThread(hThread, out uint exitCode))
                    {
                        if (exitCode == 0)
                        {
                            errorMessage = "LoadLibrary returned NULL";
                            MessageBox.Show("LoadLibrary returned NULL.\n\nPossible reasons:\n- Invalid DLL file\n- Missing dependencies\n- DLL not compatible with target process\n- Path contains non-ASCII characters", "Injection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            if (config.SaveInjectionHistory)
                                InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                        }
                        else
                        {
                            success = true;
                            MessageBox.Show($"✓ DLL successfully injected!\n\nModule handle: 0x{exitCode:X}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (config.SaveInjectionHistory)
                                InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, true, $"Success (Handle: 0x{exitCode:X})");
                            
                            // Показываем уведомление если включено
                            if (config.ShowNotifications)
                            {
                                ShowNotification("Injection Successful", $"DLL injected into {selectedProcessName ?? processId.ToString()}");
                            }
                        }
                    }
                    else
                    {
                        errorMessage = "Couldn't get exit code";
                        MessageBox.Show("Thread completed but couldn't get exit code.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (config.SaveInjectionHistory)
                            InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                    }
                }
                else
                {
                    errorMessage = "Thread timeout or failed";
                    MessageBox.Show("Thread execution timeout or failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (config.SaveInjectionHistory)
                        InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Exception: {ex.Message}";
                MessageBox.Show($"Exception during DLL injection:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (config.SaveInjectionHistory)
                    InjectionHistory.LogInjection(selectedProcessName ?? processId.ToString(), processId, dllPath, false, errorMessage);
            }
            finally
            {
                // Очищаем ресурсы
                if (hThread != IntPtr.Zero)
                    CloseHandle(hThread);

                if (allocMemAddress != IntPtr.Zero && hProcess != IntPtr.Zero)
                    VirtualFreeEx(hProcess, allocMemAddress, 0, MEM_RELEASE);

                if (hProcess != IntPtr.Zero)
                    CloseHandle(hProcess);
            }
        }

        private void ShowNotification(string title, string message)
        {
            // Простое уведомление через иконку в трее
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
            }
        }
    }

    public class Snowflake
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int Size { get; set; }
        public double Speed { get; set; }
        public int Opacity { get; set; }
        public double SwingAmplitude { get; set; }
    }
}
