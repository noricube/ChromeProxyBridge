using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChromeProxyBridge
{
    class ProxyServer
    {
        public int Port { get; }
        public Socket Listener { get; set; }

        public List<ProxyClient> Clients = new List<ProxyClient>();
        public int ClientCount
        {
            get
            {
                return Clients.Count;
            }
        }

        public ProxyServer(int port)
        {
            Port = port;
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, Port));
            Listener.Listen(1000);

        }

        private void OnAccept(object sender, SocketAsyncEventArgs e)
        {
            if ( e.SocketError != SocketError.Success)
            {
                // 소켓 에러 발생시 더 이상 listen 하지 않음
                return;
            }
            var clientSocket = e.AcceptSocket;
            var handler = new ProxyClient(clientSocket, this);
            handler.StartWorkerThread();

            lock (Clients)
            {
                Clients.Add(handler);
            }

            e.AcceptSocket = null;
            Listener.AcceptAsync(e);

        }

        public void Run()
        {
            var ev = new SocketAsyncEventArgs();
            ev.Completed += OnAccept;
            if (Listener.AcceptAsync(ev) == false)
            {
                OnAccept(Listener, ev);
            }

        }

        public void Disconnect(ProxyClient client)
        {
            lock(Clients)
            {
                Clients.Remove(client);
            }
        }

        public void Stop()
        {
            Listener.Close();
        }
    }
}
