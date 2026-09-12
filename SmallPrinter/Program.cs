using System;
using System.Windows.Forms;

namespace LaserAdd.SmallPrinter
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SmallPrinterForm());
        }
    }
}
