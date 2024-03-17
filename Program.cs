using System;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace FBReconnect
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = ping.Send("8.8.8.8", 2000); // Ping Google's DNS server
                    if (reply.Status == IPStatus.Success)
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new Form1());
                    }
                    else
                    {
                        MessageBox.Show("No working network connection. Exiting application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
