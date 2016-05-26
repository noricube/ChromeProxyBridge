using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChromeProxyBridge
{
    class ProxyClient
    {
        const int CR = 13;
        const int LF = 10;

        public Socket Client { get; }
        public Socket GoogleProxy { get; set; }

        public ProxyClient(Socket client)
        {
            Client = client;
        }

        public void StartWorkerThread()
        {
            var thread = new System.Threading.Thread(RunWorker);
            thread.Start(this);
        }

        public static void RunWorker(object state)
        {
            ProxyClient client = state as ProxyClient;
            client.Worker();
        }
        public void Worker()
        {
            bool isConnect = false;

            byte[] buffer = new byte[16384];
            Dictionary<string, string> headerDict = new Dictionary<string, string>();
            byte[] modifiedHeader = null;
            string prevHeader = null;


            int pos = 0;

            int readPointer = 0;

            // > Client Hello
            int bytes = Client.Receive(buffer, pos, buffer.Length - pos, SocketFlags.None);

            if (buffer[0] == 0x04)
            {
                Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string timestamp = unixTimestamp.ToString();

                int port = buffer[2] * 256 + buffer[3];

                Int32 IP1 = Convert.ToInt32(buffer[4]);
                Int32 IP2 = Convert.ToInt32(buffer[5]);
                Int32 IP3 = Convert.ToInt32(buffer[6]);
                Int32 IP4 = Convert.ToInt32(buffer[7]);
                long ipInt = IP1 + (IP2 * 256) + (IP3 * 256 * 256) + (IP4 * 256 * 256 * 256);

                string ip = IP1.ToString() + "." + IP2.ToString() + "." + IP3.ToString() + "." + IP4.ToString();

                //Console.WriteLine("CONNECT " + ip + ":" + port + " HTTP/1.1\r\n" + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");
                modifiedHeader = Encoding.UTF8.GetBytes("CONNECT " + ip + ":" + port + " HTTP/1.1\r\n" + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");

                GoogleProxy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                GoogleProxy.Connect("ssl.googlezip.net", 443);

                GoogleProxy.Send(modifiedHeader);

                int recv = GoogleProxy.Receive(buffer, buffer.Length, SocketFlags.None);

                var responseHeader = Encoding.UTF8.GetString(buffer, 0, recv);

                Console.WriteLine(responseHeader);
                if (responseHeader.IndexOf("200") == -1 )
                {

                    Client.Send(new byte[] { 0x00, 0x5B, 0, 0, 0, 0, 0, 0 });
                    Client.Close(10);
                    return;
                }

                Client.Send(new byte[] { 0x00, 0x5A, 0, 0, 0, 0, 0, 0 });
                GoogleProxy.NoDelay = true;
                Client.NoDelay = true;

                var ev = new SocketAsyncEventArgs();
                ev.SetBuffer(new byte[8196], 0, 8196);
                ev.Completed += OnGoogleReceived;
                if (GoogleProxy.ReceiveAsync(ev) == false)
                {
                    OnGoogleReceived(GoogleProxy, ev);
                }

                ev = new SocketAsyncEventArgs();
                ev.SetBuffer(new byte[8196], 0, 8196);
                ev.Completed += OnClientReceived;

                if (Client.ReceiveAsync(ev) == false)
                {
                    OnClientReceived(GoogleProxy, ev);
                }
            }
            else
            {
                // < Server Hello
                Client.Send(new byte[] { 0x05, 0x00 });
                // > client connect
                bytes = Client.Receive(buffer, 0, buffer.Length, SocketFlags.None);


                if ( buffer[3] != 1)
                {
                    throw new Exception("what the hell");
                }
                Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string timestamp = unixTimestamp.ToString();

                int port = buffer[8] * 256 + buffer[9];

                Int32 IP1 = Convert.ToInt32(buffer[4]);
                Int32 IP2 = Convert.ToInt32(buffer[5]);
                Int32 IP3 = Convert.ToInt32(buffer[6]);
                Int32 IP4 = Convert.ToInt32(buffer[7]);
                long ipInt = IP1 + (IP2 * 256) + (IP3 * 256 * 256) + (IP4 * 256 * 256 * 256);

                string ip = IP1.ToString() + "." + IP2.ToString() + "." + IP3.ToString() + "." + IP4.ToString();

                //Console.WriteLine("CONNECT " + ip + ":" + port + " HTTP/1.1\r\n" + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");
                modifiedHeader = Encoding.UTF8.GetBytes("CONNECT " + ip + ":" + port + " HTTP/1.1\r\n" + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");

                GoogleProxy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                GoogleProxy.Connect("ssl.googlezip.net", 443);

                GoogleProxy.Send(modifiedHeader);

                int recv = GoogleProxy.Receive(buffer, buffer.Length, SocketFlags.None);

                var responseHeader = Encoding.UTF8.GetString(buffer, 0, recv);

                Console.WriteLine(responseHeader);
                if (responseHeader.IndexOf("200") == -1)
                {

                    Client.Send(new byte[] { 0x05, 0x03, 0, 0, 0, 0, 0, 0 });
                    Client.Close(10);
                    return;
                }

                Client.Send(new byte[] { 0x05, 0x00, 0, 0, 0, 0, 0, 0 });
                GoogleProxy.NoDelay = true;
                Client.NoDelay = true;

                var ev = new SocketAsyncEventArgs();
                ev.SetBuffer(new byte[8196], 0, 8196);
                ev.Completed += OnGoogleReceived;
                if (GoogleProxy.ReceiveAsync(ev) == false)
                {
                    OnGoogleReceived(GoogleProxy, ev);
                }

                ev = new SocketAsyncEventArgs();
                ev.SetBuffer(new byte[8196], 0, 8196);
                ev.Completed += OnClientReceived;

                if (Client.ReceiveAsync(ev) == false)
                {
                    OnClientReceived(GoogleProxy, ev);
                }
            }
        }

        private void OnGoogleReceived(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.BytesTransferred == 0)
                {
                    Cleanup();
                }
                Client.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);

                e.SetBuffer(new byte[8196], 0, 8196);
                GoogleProxy.ReceiveAsync(e);
            }
            catch (Exception)
            {
                Cleanup();
            }
        }

        private void OnClientReceived(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.BytesTransferred == 0)
                {
                    Cleanup();
                }
                GoogleProxy.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);

                e.SetBuffer(new byte[8196], 0, 8196);
                Client.ReceiveAsync(e);
            }
            catch (Exception)
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            Console.WriteLine("disconnect ");
            Client.Close(10);
            GoogleProxy.Close(10);
        }
    }
}
