using System;
using System.Windows.Forms;

namespace NetheritInjector
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Запускаем форму авторизации
            using (var authForm = new AuthForm())
            {
                if (authForm.ShowDialog() == DialogResult.OK)
                {
                    // Получаем данные о подписке из формы авторизации
                    string? key = authForm.ValidatedKey;
                    int durationDays = authForm.SubscriptionDays;
                    DateTime? expiresAt = authForm.ExpiresAt;
                    
                    // Если ключ верный, запускаем основную форму с данными подписки
                    Application.Run(new MainForm(key ?? "UNKNOWN", durationDays, expiresAt));
                }
            }
        }
    }
}
