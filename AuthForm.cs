using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace NetheritInjector
{
    public partial class AuthForm : Form
    {
        private List<Snowflake> snowflakes = null!;
        private System.Windows.Forms.Timer animationTimer = null!;
        private Random random = null!;
        private TextBox keyTextBox = null!;
        private Button loginButton = null!;
        private Label statusLabel = null!;

        public string? ValidatedKey { get; private set; }
        public int SubscriptionDays { get; private set; }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private const int HT_CAPTION = 0x2;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        public AuthForm()
        {
            InitializeComponent();
            InitializeSnowflakes();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Настройка формы
            this.Text = "Netherit Authentication";
            this.Size = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.95;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.DoubleBuffered = true; // Важно для плавности

            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
            this.Paint += AuthForm_Paint;

            // Заголовок
            Label titleLabel = new Label
            {
                Text = "NETHERIT",
                Font = new Font("Segoe UI Light", 32),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(500, 70),
                Location = new Point(0, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(titleLabel);

            // Метка для ключа
            Label keyLabel = new Label
            {
                Text = "AUTHENTICATION KEY",
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                ForeColor = Color.DarkGray,
                AutoSize = false,
                Size = new Size(500, 20),
                Location = new Point(0, 140),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(keyLabel);

            // Поле ввода ключа
            keyTextBox = new TextBox
            {
                Font = new Font("Segoe UI", 12),
                Size = new Size(320, 30),
                Location = new Point(90, 170),
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center,
                UseSystemPasswordChar = true
            };
            this.Controls.Add(keyTextBox);

            // Подложка под TextBox (для визуальной рамки)
            Panel textBoxBg = new Panel
            {
                Size = new Size(320, 1), // Only bottom underline
                Location = new Point(90, 200),
                BackColor = Color.Gray,
            };
            this.Controls.Add(textBoxBg);

            // Кнопка входа
            loginButton = new Button
            {
                Text = "LOGIN",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(220, 45),
                Location = new Point(140, 240),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            loginButton.FlatAppearance.BorderSize = 1;
            loginButton.FlatAppearance.BorderColor = Color.White;
            loginButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30,30,30);
            loginButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(40,40,40);
            loginButton.Click += LoginButton_Click;
            this.Controls.Add(loginButton);

            this.AcceptButton = loginButton; // Enter нажимает кнопку

            // Статус
            statusLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Red,
                AutoSize = false,
                Size = new Size(500, 30),
                Location = new Point(0, 280),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(statusLabel);

            // Таймер для анимации
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            // Custom Close Button
            Label closeButton = new Label
            {
                Text = "✖",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Location = new Point(470, 10)
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.White;
            this.Controls.Add(closeButton);

            random = new Random();
            this.ResumeLayout(false);
        }

        private void InitializeSnowflakes()
        {
            snowflakes = new List<Snowflake>();
            for (int i = 0; i < 40; i++)
            {
                snowflakes.Add(new Snowflake
                {
                    X = random.Next(0, this.Width),
                    Y = random.Next(-this.Height, 0),
                    Size = random.Next(2, 6),
                    Speed = random.NextDouble() * 1.5 + 0.5,
                    Opacity = random.Next(60, 200),
                    SwingAmplitude = random.NextDouble() * 1.5
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
                
                if (snowflake.X < -20 || snowflake.X > this.Width + 20)
                {
                    snowflake.X = random.Next(0, this.Width);
                }
            }
            this.Invalidate();
        }

        private void AuthForm_Paint(object? sender, PaintEventArgs e)
        {
            // Minimal Solid Background
            e.Graphics.Clear(Color.FromArgb(18, 18, 18));

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var snowflake in snowflakes)
            {
                // Subtle White particles
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(snowflake.Opacity / 2, 255, 255, 255)))
                {
                    e.Graphics.FillEllipse(brush, snowflake.X, snowflake.Y, snowflake.Size, snowflake.Size);
                }
            }
            
            // Draw Border
            using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 40), 1))
            {
                e.Graphics.DrawPath(borderPen, GetRoundedPath(ClientRectangle, 20));
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

        private void LoginButton_Click(object? sender, EventArgs e)
        {
            string key = keyTextBox.Text.Trim();
            
            if (KeySystem.ValidateKey(key, out int durationDays))
            {
                // Пытаемся активировать ключ
                if (KeySystem.ActivateKey(key, out string message))
                {
                    ValidatedKey = key;
                    SubscriptionDays = durationDays;
                    
                    string durationText = KeySystem.GetDurationText(durationDays);
                    statusLabel.ForeColor = Color.FromArgb(100, 255, 100);
                    statusLabel.Text = $"✓ KEY ACTIVATED - {durationText}";
                    
                    // Небольшая задержка перед открытием MainForm
                    System.Threading.Tasks.Task.Delay(800).ContinueWith(_ => 
                    {
                        this.Invoke((Action)(() =>
                        {
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }));
                    });
                }
                else if (KeySystem.IsKeyExpired(key))
                {
                    statusLabel.ForeColor = Color.FromArgb(255, 80, 80);
                    statusLabel.Text = "❌ KEY EXPIRED";
                    keyTextBox.BackColor = Color.FromArgb(60, 20, 20);
                    ShakeWindow();
                }
                else
                {
                    // Ключ уже активирован, проверяем не истек ли
                    long timeLeft = KeySystem.GetKeyTimeLeft(key);
                    if (timeLeft > 0)
                    {
                        ValidatedKey = key;
                        SubscriptionDays = durationDays;
                        
                        string timeText = KeySystem.FormatTimeLeft(timeLeft);
                        statusLabel.ForeColor = Color.FromArgb(100, 255, 100);
                        statusLabel.Text = $"✓ KEY ACCEPTED - {timeText} left";
                        
                        System.Threading.Tasks.Task.Delay(800).ContinueWith(_ => 
                        {
                            this.Invoke((Action)(() =>
                            {
                                this.DialogResult = DialogResult.OK;
                                this.Close();
                            }));
                        });
                    }
                    else
                    {
                        statusLabel.ForeColor = Color.FromArgb(255, 80, 80);
                        statusLabel.Text = "❌ KEY EXPIRED";
                        keyTextBox.BackColor = Color.FromArgb(60, 20, 20);
                        ShakeWindow();
                    }
                }
            }
            else
            {
                statusLabel.ForeColor = Color.FromArgb(255, 80, 80);
                statusLabel.Text = "❌ INVALID KEY";
                keyTextBox.BackColor = Color.FromArgb(60, 20, 20);
                
                // Простая анимация ошибки (тряска)
                ShakeWindow();
            }
        }

        private async void ShakeWindow()
        {
            var original = this.Location;
            var rnd = new Random();
            for (int i = 0; i < 10; i++)
            {
                this.Location = new Point(original.X + rnd.Next(-5, 5), original.Y + rnd.Next(-5, 5));
                await System.Threading.Tasks.Task.Delay(20);
            }
            this.Location = original;
            keyTextBox.BackColor = Color.FromArgb(40, 60, 100);
        }
    }
}
