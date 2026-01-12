using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Linq;

namespace NetheritInjector
{
    public class HistoryForm : Form
    {
        private ListView historyListView = null!;
        private Button clearButton = null!;
        private Button exportButton = null!;
        private Label statusLabel = null!;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int HT_CAPTION = 0x2;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        public HistoryForm()
        {
            InitializeComponent();
            LoadHistory();
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void InitializeComponent()
        {
            this.Text = "История инъекций";
            this.Size = new Size(800, 500);
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
                Text = "📋 ИСТОРИЯ ИНЪЕКЦИЙ",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(800, 50),
                Location = new Point(0, 15),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(titleLabel);

            // ListView для истории
            historyListView = new ListView
            {
                Location = new Point(20, 70),
                Size = new Size(760, 340),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            historyListView.Columns.Add("Время", 130);
            historyListView.Columns.Add("Процесс", 120);
            historyListView.Columns.Add("PID", 70);
            historyListView.Columns.Add("DLL", 280);
            historyListView.Columns.Add("Статус", 80);
            historyListView.Columns.Add("Сообщение", 200);

            this.Controls.Add(historyListView);

            // Статус
            statusLabel = new Label
            {
                Text = "Загрузка...",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gray,
                Location = new Point(20, 420),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(statusLabel);

            // Clear button
            clearButton = new Button
            {
                Text = "Очистить историю",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(180, 35),
                Location = new Point(400, 420),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            clearButton.FlatAppearance.BorderSize = 1;
            clearButton.FlatAppearance.BorderColor = Color.FromArgb(255, 80, 80);
            clearButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            clearButton.Click += ClearButton_Click;
            this.Controls.Add(clearButton);

            // Export button
            exportButton = new Button
            {
                Text = "Экспорт",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(100, 35),
                Location = new Point(590, 420),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            exportButton.FlatAppearance.BorderSize = 1;
            exportButton.FlatAppearance.BorderColor = Color.FromArgb(100, 200, 255);
            exportButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            exportButton.Click += ExportButton_Click;
            this.Controls.Add(exportButton);

            // Close button
            Label closeButton = new Label
            {
                Text = "✖",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Location = new Point(770, 10)
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.White;
            this.Controls.Add(closeButton);
        }

        private void LoadHistory()
        {
            historyListView.Items.Clear();
            var history = InjectionHistory.GetHistory();

            foreach (var entry in history)
            {
                var item = new ListViewItem(entry.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"));
                item.SubItems.Add(entry.ProcessName);
                item.SubItems.Add(entry.ProcessId.ToString());
                item.SubItems.Add(System.IO.Path.GetFileName(entry.DllPath));
                item.SubItems.Add(entry.Success ? "✓" : "✗");
                item.SubItems.Add(entry.Message);

                if (entry.Success)
                    item.ForeColor = Color.FromArgb(100, 255, 100);
                else
                    item.ForeColor = Color.FromArgb(255, 100, 100);

                historyListView.Items.Add(item);
            }

            statusLabel.Text = $"Всего записей: {history.Count}";
        }

        private void ClearButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите очистить всю историю инъекций?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                InjectionHistory.ClearHistory();
                LoadHistory();
            }
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv";
                saveDialog.FileName = $"injection_history_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var history = InjectionHistory.GetHistory();
                        var lines = history.Select(e => 
                            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss}\t{e.ProcessName}\t{e.ProcessId}\t{e.DllPath}\t{(e.Success ? "Success" : "Failed")}\t{e.Message}"
                        );
                        System.IO.File.WriteAllLines(saveDialog.FileName, lines);
                        MessageBox.Show("История успешно экспортирована!", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
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
