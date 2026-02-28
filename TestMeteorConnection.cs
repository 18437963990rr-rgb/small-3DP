using System;
using System.Windows.Forms;

namespace BinderJetting
{
    public class TestMeteorConnection
    {
        public static void Main()
        {
            // 测试MeteorPrintEngine类是否可访问
            try
            {
                bool isConnected = MeteorPrintEngine.IsPrinterConnected();
                Console.WriteLine("IsPrinterConnected() returned: " + isConnected);
                
                bool ensured = MeteorPrintEngine.EnsurePrinterOpened();
                Console.WriteLine("EnsurePrinterOpened() returned: " + ensured);
                
                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Test failed: " + ex.Message);
            }
        }
    }
}