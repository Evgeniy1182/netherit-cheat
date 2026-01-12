using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace NetheritInjector
{
    public class SettingsForm : Form
    {
        private AppConfig config;
        private CheckBox minimizeToTrayCheckBox = null!;
        private CheckBox showNotificationsCheckBox = null!;
        private CheckBox autoInjectCheckBox = null!;
        private CheckBox saveHistoryCheckBox = null!;
        private CheckBox showProcessInfoCheckBox = null!;
        private NumericUpDown delayNumeric = null!;
        private Button saveButton = null!;
        private Button cancelButton = null!;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int HT_CAPTION = 0x2;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        public SettingsForm(AppConfig config)
        {
            this.config = config;
            InitializeComponent();
            LoadSettings();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void InitializeComponent()
        {
            this.Text = "Настройки";
            this.Size = new Size(450, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.DoubleBuffered = true;

            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // Заголовок
            Label titleLabel = new Label
            {
                Text = "⚙️ НАСТРОЙКИ",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(450, 50),
                Location = new Point(0, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(titleLabel);

            int yPos = 80;

            // Minimize to tray
            minimizeToTrayCheckBox = new CheckBox
            {
                Text = "Сворачивать в трей",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(minimizeToTrayCheckBox);
            yPos += 35;

            // Show notifications
            showNotificationsCheckBox = new CheckBox
            {
                Text = "Показывать уведомления",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(showNotificationsCheckBox);
            yPos += 35;

            // Auto inject
            autoInjectCheckBox = new CheckBox
            {
                Text = "Авто-инъекция при запуске процесса",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(autoInjectCheckBox);
            yPos += 35;

            // Save history
            saveHistoryCheckBox = new CheckBox
            {
                Text = "Сохранять историю инъекций",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(saveHistoryCheckBox);
            yPos += 35;

            // Show process info
            showProcessInfoCheckBox = new CheckBox
            {
                Text = "Показывать информацию о процессе",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(showProcessInfoCheckBox);
            yPos += 35;

            // Injection delay
            Label delayLabel = new Label
            {
                Text = "Задержка перед инъекцией (мс):",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                Location = new Point(30, yPos),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(delayLabel);

            delayNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Increment = 100,
                Location = new Point(280, yPos - 3),
                Width = 100,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            this.Controls.Add(delayNumeric);
            yPos += 50;

            // Save button
            saveButton = new Button
            {
                Text = "Сохранить",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(150, 40),
                Location = new Point(50, yPos + 20),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 1;
            saveButton.FlatAppearance.BorderColor = Color.FromArgb(100, 255, 100);
            saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel button
            cancelButton = new Button
            {
                Text = "Отмена",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(150, 40),
                Location = new Point(250, yPos + 20),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = Color.Gray;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            cancelButton.Click += (s, e) => this.Close();
            this.Controls.Add(cancelButton);

            // Close button
            Label closeButton = new Label
            {
                Text = "✖",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Location = new Point(420, 10)
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.White;
            this.Controls.Add(closeButton);
        }

        private void LoadSettings()
        {
            minimizeToTrayCheckBox.Checked = config.MinimizeToTray;
            showNotificationsCheckBox.Checked = config.ShowNotifications;
            autoInjectCheckBox.Checked = config.AutoInjectOnProcessStart;
            saveHistoryCheckBox.Checked = config.SaveInjectionHistory;
            showProcessInfoCheckBox.Checked = config.ShowProcessInfo;
            delayNumeric.Value = config.InjectionDelay;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            config.MinimizeToTray = minimizeToTrayCheckBox.Checked;
            config.ShowNotifications = showNotificationsCheckBox.Checked;
            config.AutoInjectOnProcessStart = autoInjectCheckBox.Checked;
            config.SaveInjectionHistory = saveHistoryCheckBox.Checked;
            config.ShowProcessInfo = showProcessInfoCheckBox.Checked;
            config.InjectionDelay = (int)delayNumeric.Value;
            
            config.Save();
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), 2))
            {
                e.Graphics.DrawRectangle(borderPen, 1, 1, Width - 3, Height - 3);
            }
        }
    }
}
