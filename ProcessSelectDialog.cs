using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace NetheritInjector
{
    public partial class ProcessSelectDialog : Form
    {
        private ListBox processListBox = null!;
        private Button selectButton = null!;
        private Button refreshButton = null!;
        public int SelectedProcessId { get; private set; }
        public string? SelectedProcessName { get; private set; }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int HT_CAPTION = 0x2;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        public ProcessSelectDialog()
        {
            InitializeComponent();
            LoadProcesses();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void InitializeComponent()
        {
            this.Text = "Select Target Process";
            this.Size = new Size(600, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.95;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
            this.Paint += ProcessSelectDialog_Paint;

            // Заголовок
            Label titleLabel = new Label
            {
                Text = "SELECT TARGET",
                Font = new Font("Segoe UI Light", 16),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(580, 40),
                Location = new Point(10, 10),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(titleLabel);

            // Список процессов
            processListBox = new ListBox
            {
                Location = new Point(10, 60),
                Size = new Size(570, 310),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.DarkGray,
                BorderStyle = BorderStyle.None
            };
            processListBox.DrawMode = DrawMode.OwnerDrawFixed;
            processListBox.DrawItem += (s, e) => {
                if (e.Index < 0) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                e.Graphics.FillRectangle(new SolidBrush(isSelected ? Color.FromArgb(30, 30, 30) : Color.FromArgb(18, 18, 18)), e.Bounds);
                string text = ((ProcessItem)processListBox.Items[e.Index]).DisplayName;
                e.Graphics.DrawString(text, e.Font, new SolidBrush(isSelected ? Color.White : Color.Gray), e.Bounds.Location);
                // No focus rect
            };
            this.Controls.Add(processListBox);

            // Кнопка обновления
            refreshButton = new Button
            {
                Text = "REFRESH",
                Location = new Point(10, 380),
                Size = new Size(120, 35),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            refreshButton.FlatAppearance.BorderSize = 1;
            refreshButton.FlatAppearance.BorderColor = Color.FromArgb(60,60,60);
            refreshButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30,30,30);
            refreshButton.Click += RefreshButton_Click;
            this.Controls.Add(refreshButton);

            // Кнопка выбора
            selectButton = new Button
            {
                Text = "SELECT",
                Location = new Point(460, 380),
                Size = new Size(120, 35),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            selectButton.FlatAppearance.BorderSize = 1;
            selectButton.FlatAppearance.BorderColor = Color.White;
            selectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30,30,30);
            selectButton.Click += SelectButton_Click;
            this.Controls.Add(selectButton);

            // Custom Close Button
            Label closeButton = new Label
            {
                Text = "✖",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Location = new Point(570, 10)
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.White;
            this.Controls.Add(closeButton);

            this.AcceptButton = selectButton;
        }

        private void ProcessSelectDialog_Paint(object? sender, PaintEventArgs e)
        {
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

        private void LoadProcesses()
        {
            processListBox.Items.Clear();
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) || p.ProcessName != "Idle")
                    .OrderBy(p => p.ProcessName)
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        string displayName = $"{process.ProcessName} (PID: {process.Id})";
                        if (!string.IsNullOrEmpty(process.MainWindowTitle))
                        {
                            displayName += $" - {process.MainWindowTitle}";
                        }
                        processListBox.Items.Add(new ProcessItem { Process = process, DisplayName = displayName });
                    }
                    catch
                    {
                        // Пропускаем процессы, к которым нет доступа
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке процессов: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            LoadProcesses();
        }

        private void SelectButton_Click(object? sender, EventArgs e)
        {
            if (processListBox.SelectedItem != null)
            {
                ProcessItem item = (ProcessItem)processListBox.SelectedItem;
                SelectedProcessId = item.Process.Id;
                SelectedProcessName = item.DisplayName;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select a process from the list.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private class ProcessItem
        {
            public Process Process { get; set; } = null!;
            public string DisplayName { get; set; } = null!;

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
