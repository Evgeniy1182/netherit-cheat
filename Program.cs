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
                    // Если ключ верный, запускаем основную форму
                    Application.Run(new MainForm());
                }
            }
        }
    }
}
