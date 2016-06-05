using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChromeProxyBridge
{
    public partial class MainForm : Form
    {
        public static MainForm Instance { get; protected set; }
        private ProxyServer Proxy { get; set; }
        public MainForm()
        {
            InitializeComponent();
            Proxy = null;
            Instance = this;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            int port;
            try
            {
                port = int.Parse(PortTestBox.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("Port must be a number.");
                return;
            }

            try
            {
                Proxy = new ProxyServer(port);
                AddLog(string.Format("init server on {0}", port));

                Proxy.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                Proxy = null;
                return;
            }

            PortTestBox.Enabled = false;
            StartButton.Enabled = false;
            StopButton.Enabled = true;
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            Proxy.Stop();

            PortTestBox.Enabled = true;
            StartButton.Enabled = true;
            StopButton.Enabled = false;

            AddLog("stop server");
        }

        delegate void UpdateAddLogDelegate(String msg);
        public void AddLog(string message)
        {
            if (LogBox.InvokeRequired)
            {
                UpdateAddLogDelegate update = new UpdateAddLogDelegate(AddLog);
                LogBox.Invoke(update, message);
            }
            else
            {
                LogBox.Items.Add(message);

                // Make sure the last item is made visible
                LogBox.SelectedIndex = LogBox.Items.Count - 1;
                LogBox.ClearSelected();
            }
        }

    }
}
