using Open.Nat;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Reflection;

namespace FBReconnect
{
    public partial class Fbreconnect : Form
    {
        public Icon appIcon = Properties.Resources.FBReconnect;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuStrip;
        private ImageList menuImageList;
        private TableLayoutPanel tableLayoutPanel;
        private ToolStripLabel ipAddressMenuLabel;
        private PictureBox publicIpAddressFormLabelPrefix;
        private Label privateIpAddressFormLabel;
        private Label publicIpAddressFormLabel;
        private Label fbNameLabel;
        private Label fbVersionLabel;
        private PictureBox reconnectButton;
        private PictureBox exitButton;
        public IPHostEntry hostEntry;
        public string xml;
        public string pubIp;
        public string privIp;
        public static string noIp = "";
        public string FBVersion;
        public string FBName;
        public string FBSerial;
        public string fbUrl;
        public bool isFitzBox;

        public Fbreconnect()
        {
            // Check if there is a working network connection
            if (!CheckNetworkConnection())
            {
                // Display an error message with an error icon and exit the application
                MessageBox.Show(Properties.Resources.NoWorkingNetworkConnectionExitingApplication, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return; // Exit the constructor
            }
            else
            {

                InitializeComponent();

                this.Icon = appIcon;
                // Hide the Form
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                InitializeApp();

                // Handle the FormClosing event to minimize the form instead of closing
                this.FormClosing += Form1_FormClosing;

            }
        }

        public static Bitmap PngFromIcon(Icon icon)
        {
            Bitmap png = null;
            using (var iconStream = new MemoryStream())
            {
                icon.Save(iconStream);
                var decoder = new IconBitmapDecoder(iconStream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);

                using (var pngSteam = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(decoder.Frames[0]);
                    encoder.Save(pngSteam);
                    png = (Bitmap)Image.FromStream(pngSteam);
                }
            }
            return png;
        }

        public static string GetIPAddress(string hostname)
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(hostname);

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    //System.Diagnostics.Debug.WriteLine("LocalIPadress: " + ip);
                    return ip.ToString();
                }
            }
            return string.Empty;
        }
        private bool checkFritzBox()
        {
            if (CheckNetworkConnection() && CheckFritzBoxReachability())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CheckNetworkConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = ping.Send("8.8.8.8", 2000); // Ping Google's DNS server
                    return reply.Status == IPStatus.Success;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }


        private bool CheckFritzBoxReachability()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadData(fbUrl);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static string GetGatewayAddress()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;

                if (addresses.Count > 0)
                {
                    return addresses[0].Address.ToString();
                }
            }

            return "0.0.0.0";
        }
        public static IPAddress GetDefaultGateway()
        {
            IPAddress result = null;
            var cards = NetworkInterface.GetAllNetworkInterfaces().ToList();
            if (cards.Any())
            {
                foreach (var card in cards)
                {
                    var props = card.GetIPProperties();
                    if (props == null)
                        continue;

                    var gateways = props.GatewayAddresses;
                    if (!gateways.Any())
                        continue;

                    var gateway =
                        gateways.FirstOrDefault(g => g.Address.AddressFamily.ToString() == "InterNetwork");
                    if (gateway == null)
                        continue;

                    result = gateway.Address;
                    break;
                };
            }

            return result;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Minimize the form instead of closing
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
        }

        private async void InitializeApp()
        {
            fbUrl = "http://fritz.box:49000/tr64desc.xml";
//            fbUrl = String.Format("http://{0}:49000/tr64desc.xml", GetGatewayAddress());
            isFitzBox = CheckFritzBoxReachability();

            if (isFitzBox)
            {
                string xml = await FetchXmlFromUrl(fbUrl);
                FBVersion = GetDisplay(xml);
                FBName = GetModelName(xml);
                FBSerial = GetSerialNumber(xml);
            } else
            {
                FBVersion = "-";
                FBName = Properties.Resources.NoFritzBox;
                FBSerial = "-";
            }
            privIp = GetIPAddress("fritz.box").ToString();
            // Call the asynchronous initialization method
            await InitializeAsync();

            // Center the form on the screen
            CenterFormOnScreen();
        }

        public async Task InitializeAsync()
        {

            // Get the public IP address
            pubIp = await GetIPAddressAsync();

            // Create and configure the NotifyIcon
            notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true
            };
            notifyIcon.MouseClick += NotifyIcon_MouseClick;

            // Create and configure the ContextMenuStrip
            contextMenuStrip = new ContextMenuStrip();
            ToolStripMenuItem reconnectFritzBoxMenuItem = new ToolStripMenuItem(Properties.Resources.ReconnectFritzBox);
            ipAddressMenuLabel = new ToolStripLabel(string.Format(Properties.Resources.IP + "{0}", pubIp));
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem(Properties.Resources.Exit);

            // Load the icons into the ImageList
            menuImageList = new ImageList();
            menuImageList.Images.Add(Properties.Resources.network);
            menuImageList.Images.Add(Properties.Resources.connect);
            menuImageList.Images.Add(Properties.Resources.world);
            menuImageList.Images.Add(Properties.Resources.exit_icon);

            // Set ImageList for the context menu
            contextMenuStrip.ImageList = menuImageList;

            // Set ImageIndex for menu items
            exitMenuItem.ImageIndex = 3;

            reconnectFritzBoxMenuItem.Click += ReconnectFritzBox_Click;
            exitMenuItem.Click += ExitMenuItem_Click;

            contextMenuStrip.Items.Add(ipAddressMenuLabel);
            contextMenuStrip.Items.Add(reconnectFritzBoxMenuItem);
            contextMenuStrip.Items.Add(exitMenuItem);

            notifyIcon.ContextMenuStrip = contextMenuStrip;

            // Create and configure the TableLayoutPanel
            tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F)); // First row uses 33.33% of the height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F)); // Second row uses 33.33% of the height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F)); // Third row uses 33.34% of the height
            tableLayoutPanel.ColumnCount = 3;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // First column uses 33.33% of the width
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // Second column uses 33.33% of the width
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F)); // Third column uses 33.34% of the width
            this.Controls.Add(tableLayoutPanel);

            // Add controls to the TableLayoutPanel
            AddControlsToTableLayoutPanel();

            // Set the border style for the TableLayoutPanel
            tableLayoutPanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.Outset;

            // Center the form on the screen
            CenterFormOnScreen();

            // Check whether we are running a Fritz!Box
            if (!isFitzBox)
            ShowNotificationToast("None", Properties.Resources.FritzBoxNotFoundOrNotReachable, 10);

            Version shortVersion = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = string.Format(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + $" {shortVersion.Major}.{shortVersion.Minor}.{shortVersion.Build}");
        }

        private void AddControlsToTableLayoutPanel()
        {
            // Create and configure the IP address label prefix
            publicIpAddressFormLabelPrefix = new PictureBox();
            publicIpAddressFormLabelPrefix.Image = Bitmap.FromHicon(new Icon(Properties.Resources.connect, new Size(48, 48)).Handle);
            publicIpAddressFormLabelPrefix.SizeMode = PictureBoxSizeMode.Zoom; // Set PictureBox to use Zoom mode
            publicIpAddressFormLabelPrefix.Anchor = AnchorStyles.None; // Align center
            publicIpAddressFormLabelPrefix.Dock = DockStyle.Fill; // Align vertically in the middle

            // Create and configure the Fritz!Box name label
            fbNameLabel = new Label();
            fbNameLabel.Height = 50;
            fbNameLabel.Text = string.Format("{0}{1}{2}", FBName, Environment.NewLine, FBSerial);
            fbNameLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Create and configure the Firmware version label
            fbVersionLabel = new Label();
            fbVersionLabel.Text = string.Format("Fritz!OS{0}{1}", Environment.NewLine, FBVersion);
            fbVersionLabel.Height = 50;
            fbVersionLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Create and configure the public IP address label
            publicIpAddressFormLabel = new Label();
            publicIpAddressFormLabel.Height = 50;
            publicIpAddressFormLabel.Text = string.Format(Properties.Resources.PublicIP + "{0}{1}", Environment.NewLine, pubIp);
            publicIpAddressFormLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Create and configure the reconnect button
            Size size = new Size(16, 16);
            reconnectButton = new PictureBox();
            reconnectButton.Image = PngFromIcon(Properties.Resources.reset);
            reconnectButton.SizeMode = PictureBoxSizeMode.Zoom;
            reconnectButton.Anchor = AnchorStyles.None;
            reconnectButton.Dock = DockStyle.Fill;
            reconnectButton.Size = size;
            System.Windows.Forms.ToolTip ToolTip2 = new System.Windows.Forms.ToolTip();
            if (isFitzBox)
            {
                reconnectButton.Cursor = Cursors.Hand;
                reconnectButton.Click += ReconnectFritzBox_Click;
                ToolTip2.SetToolTip(this.reconnectButton, Properties.Resources.ReconnectFritzBox);
            }
            else
            {
                reconnectButton.Cursor = Cursors.No;
                ToolTip2.SetToolTip(this.reconnectButton, Properties.Resources.FritzBoxNotFoundOrNotReachable);
            }

            // Create and configure the exit button
            exitButton = new PictureBox();
            exitButton.Image = PngFromIcon(Properties.Resources.exit_icon);
            exitButton.SizeMode = PictureBoxSizeMode.Zoom;
            exitButton.Anchor = AnchorStyles.None;
            exitButton.Dock = DockStyle.Fill;
            exitButton.Size = size;
            exitButton.Cursor = Cursors.Hand;
            exitButton.Click += ExitButton_Click;
            System.Windows.Forms.ToolTip ToolTip1 = new System.Windows.Forms.ToolTip();
            ToolTip1.SetToolTip(this.exitButton, Properties.Resources.Exit);

            // Create and configure the private IP address label
            privateIpAddressFormLabel = new Label();
            privateIpAddressFormLabel.Height = 50;
            privateIpAddressFormLabel.Text = string.Format(Properties.Resources.PrivateIP + "{0}{1}", Environment.NewLine, privIp);
            privateIpAddressFormLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Add controls to the TableLayoutPanel
            tableLayoutPanel.Controls.Add(fbNameLabel, 0, 0); // Place the Name label in the first row, first column
            tableLayoutPanel.Controls.Add(publicIpAddressFormLabelPrefix, 1, 0); // Place the PictureBox in the first row, second column
            tableLayoutPanel.Controls.Add(fbVersionLabel, 2, 0); // Place the PictureBox in the first row, third column
            tableLayoutPanel.Controls.Add(publicIpAddressFormLabel, 1, 1); // Place the label in the second row, second column
            tableLayoutPanel.Controls.Add(reconnectButton, 0, 2); // Place the reconnect button in the third row, first column
            tableLayoutPanel.Controls.Add(privateIpAddressFormLabel, 1, 2); // Place the label in the third row, second column
            tableLayoutPanel.Controls.Add(exitButton, 2, 2); // Place the exit button in the third row, third column
        }
        private void CenterFormOnScreen()
        {
            // Calculate the center of the screen
            int centerX = Screen.PrimaryScreen.WorkingArea.Width / 2 - 150;
            int centerY = Screen.PrimaryScreen.WorkingArea.Height / 2 - 50;

            // Set the form's location
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(centerX, centerY);
        }
        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Show/Hide the form
                if (WindowState == FormWindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                }
                else
                {
                    this.Hide();
                    this.WindowState = FormWindowState.Minimized;
                }
            }
        }
        private void ExitButton_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        }

        private string GetDeviceName(string ip)
        {
            WebClient myWebClient = new WebClient();
            try
            {
                System.IO.Stream s = myWebClient.OpenRead("http://" + ip);
                var sr = new System.IO.StreamReader(s);
                while (!sr.EndOfStream)
                {
                    var L = sr.ReadLine();
                    var sb = new StringBuilder();
                    var st = 0;
                    var end = 0;
                    if ((st = L.ToLower().IndexOf("<title>")) != -1)
                    {
                        sb.Append(L);
                        while ((end = L.ToLower().IndexOf("</title>")) == -1)
                        {
                            L = sr.ReadLine();
                            sb.Append(L);
                        }
                        sr.Close();
                        s.Close();
                        myWebClient.Dispose();
                        var title = sb.ToString().Substring(st + 7, end - st - 7);
                        Regex r = new Regex("&#[^;]+;");
                        title = r.Replace(title, delegate (Match match)
                        {
                            string value = match.Value.ToString().Replace("&#", "").Replace(";", "");
                            int asciiCode;
                            if (int.TryParse(value, out asciiCode))
                                return Convert.ToChar(asciiCode).ToString();
                            else
                                return value;
                        });
                        return $"Router/AP ({title})";
                    }
                }
                sr.Close();
                s.Close();
                myWebClient.Dispose();
            }
            catch (System.Net.WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null)
                    return "";

                var name = response.Headers?["WWW-Authenticate"]?.Substring(12).Trim('"');
                myWebClient.Dispose();
                return name;
            }
            return "";
        }
        private async void ReconnectFritzBox_Click(object sender, EventArgs e)
        {
            if (checkFritzBox())
            {
                try
                {
                    publicIpAddressFormLabel.Text = noIp;
                    ipAddressMenuLabel.Text = string.Format("IP: " + "{0}", noIp);

                    await ReconnectFritzBoxAsync();

                    // Show success notification
                    ShowNotificationToast("None", Properties.Resources.FritzBoxHasBeenSuccessfullyReset, 5);
                    var newIP = await GetPublicFritzBoxIp();
                    ShowNotificationToast("None", string.Format(Properties.Resources.OldIpAddress + "{0}{1}" + Properties.Resources.NewIpAddress + "{2}", pubIp, Environment.NewLine, newIP), 10);
                    pubIp = newIP;
                    publicIpAddressFormLabel.Text = string.Format(Properties.Resources.PublicIP + "{0}{1}", Environment.NewLine, pubIp);
                    ipAddressMenuLabel.Text = string.Format(Properties.Resources.IP + "{0}", pubIp);
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                }

            }
            else
            {
                // Display a message if Fritz!Box is not reachable
                ShowNotificationToast("Error", Properties.Resources.FritzBoxNotFoundOrNotReachable, 5);
            }
        }

        private async Task<string> GetIPAddressAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string response = await client.GetStringAsync("http://checkip.dyndns.org/");
                    int first = response.IndexOf("Address: ") + 9;
                    int last = response.LastIndexOf("</body>");
                    string address = response.Substring(first, last - first).Trim();

                    return address;
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
                return noIp;
            }
        }
        private async Task ReconnectFritzBoxAsync()
        {
            try
            {
                String Request = "http://fritz.box:49000/igdupnp/control/WANIPConn1";
//                String Request = String.Format("http://{0}:49000/igdupnp/control/WANIPConn1", GetGatewayAddress());
                var httpRequest = (HttpWebRequest)WebRequest.Create(Request);
                httpRequest.Method = "POST";
                httpRequest.Headers["SOAPACTION"] = "urn:schemas-upnp-org:service:WANIPConnection:1#ForceTermination";
                httpRequest.ContentType = "text/xml; charset=utf-8";
                var data = "<?xml version=\"1.0\" encoding=\"utf-8\"?><s:Envelope s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\" xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope\"><s:Body><u:ForceTermination xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\" /></s:Body></s:Envelope>";
                httpRequest.ContentLength = data.Length;

                using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
                {
                    await streamWriter.WriteAsync(data);
                }

                using (var httpResponse = (HttpWebResponse)await httpRequest.GetResponseAsync())
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        static async Task<string> GetPublicFritzBoxIp()
        {
            // Create a NAT discovery instance
            var natDiscoverer = new NatDiscoverer();

            // Discover NAT devices in the network
            var device = await natDiscoverer.DiscoverDeviceAsync();

            // Get the external IP address
            var ip = await device.GetExternalIPAsync();

            return ip.ToString();
        }
        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        }
        private void ShowNotificationToast(string iconType, string message, int seconds)
        {
            ToolTipIcon tooltipIcon;

            switch (iconType.ToLower())
            {
                case "info":
                    tooltipIcon = ToolTipIcon.Info;
                    break;
                case "warning":
                    tooltipIcon = ToolTipIcon.Warning;
                    break;
                case "error":
                    tooltipIcon = ToolTipIcon.Error;
                    break;
                default:
                    tooltipIcon = ToolTipIcon.None;
                    break;
            }
            notifyIcon.BalloonTipIcon = tooltipIcon;
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(seconds * 1000);
        }
        private void HandleError(Exception ex)
        {
            ShowNotificationToast("Error", ex.Message, 5);
            publicIpAddressFormLabel.Text = noIp;
            ipAddressMenuLabel.Text = string.Format(Properties.Resources.IP + "{0}", noIp);
        }
        static async Task<string> FetchXmlFromUrl(string url)
        {
            using (HttpClient client = new HttpClient())
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception)
                {
                    MessageBox.Show(Properties.Resources.FritzBoxNotFoundOrNotReachable, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return "";
                }
        }

        static string GetDisplay(string xml)
        {
            _ = "urn:dslforum-org:device-1-0";
            XElement root = XElement.Parse(xml);
            XElement displayElement = root.XPathSelectElement("//ns:Display", CreateNamespaceManager(root));

            if (displayElement != null)
            {
                return displayElement.Value;
            }
            else
            {
                throw new Exception(Properties.Resources.DisplayElementNotFound);
            }
        }

        static string GetPrivateIp(string xml)
        {
            _ = "urn:dslforum-org:device-1-0";
            XElement root = XElement.Parse(xml);
            XElement privateIpElement = root.XPathSelectElement("//ns:presentationURL", CreateNamespaceManager(root));

            if (privateIpElement != null)
            {
                return privateIpElement.Value.Substring(7);
            }
            else
            {
                throw new Exception(Properties.Resources.PrivateIpElementNotFound);
            }
        }

        static string GetModelName(string xml)
        {
            _ = "urn:dslforum-org:device-1-0";
            XElement root = XElement.Parse(xml);
            XElement modelNameElement = root.XPathSelectElement("//ns:modelName", CreateNamespaceManager(root));

            if (modelNameElement != null)
            {
                return modelNameElement.Value;
            }
            else
            {
                throw new Exception(Properties.Resources.ModelNameElementNotFound);
            }
        }

        static string GetSerialNumber(string xml)
        {
            _ = "urn:dslforum-org:device-1-0";
            XElement root = XElement.Parse(xml);
            XElement serialNumberElement = root.XPathSelectElement("//ns:serialNumber", CreateNamespaceManager(root));

            if (serialNumberElement != null)
            {
                return serialNumberElement.Value;
            }
            else
            {
                throw new Exception(Properties.Resources.SerialNumberElementNotFound);
            }
        }

        private static XmlNamespaceManager CreateNamespaceManager(XElement element)
        {
            var nsManager = new XmlNamespaceManager(new NameTable());
            nsManager.AddNamespace("ns", element.GetDefaultNamespace().NamespaceName);
            return nsManager;
        }

    }
}
