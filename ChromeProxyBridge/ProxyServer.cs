using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChromeProxyBridge
{
    class ProxyServer
    {
        public int Port { get; }
        public TcpListener Listener { get; set; }

        public ProxyServer(int port)
        {
            Port = port;
            Listener = new TcpListener(new System.Net.IPEndPoint(System.Net.IPAddress.Any, Port));

        }

        public void Run()
        {
            Listener.Start(1000);

            while (true)
            {
                var clientSocket = Listener.AcceptSocket();
                var handler = new ProxyClient(clientSocket);
                handler.StartWorkerThread();
            }
        }
    }
}
