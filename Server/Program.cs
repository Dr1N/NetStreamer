using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.ThreadException += Application_ThreadException;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmServer());
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Debug.WriteLine("Неперехваченное исключение в одном из потоков\n{0}", sender.ToString());
            Debug.WriteLine(e.Exception.Message);
            Application.Exit();
        }

        private static void SetDebugListeners()
        {
            Debug.Listeners.Add(new ConsoleTraceListener());
        }
    }
}