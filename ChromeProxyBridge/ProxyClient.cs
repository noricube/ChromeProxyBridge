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

        public enum ClientType
        {
            SOCKS4,
            SOCKS4a,
            SOCKS5
        };

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
            try
            {
                ClientType clientType = ClientType.SOCKS4;

                byte[] buffer = new byte[8196];
                byte[] requestBuf = null;

                string host = "";
                int port = 0;

                // > Client Hello
                // 여기서 맨 처음 byte를 보고 SOCKS 버전을 판단한다.
                int bytes = Client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                if (bytes == 0) // disconnected
                {
                    Cleanup();
                    return;
                }

                requestBuf = new byte[bytes];
                Array.Copy(buffer, requestBuf, bytes);

                int bufferEndPos = 0;

                if (buffer[0] == 0x04)
                {
                    clientType = ClientType.SOCKS4;

                    port = buffer[2] * 256 + buffer[3];
                    host = buffer[4].ToString() + "." + buffer[5].ToString() + "." + buffer[6].ToString() + "." + buffer[7].ToString();

                    // 호스트 이름이 string인 경우 0.0.0.x의 IP 주소로 접속 요청이 온다
                    // 이런 경우 userid 뒤에 hostname이 있다
                    if (buffer[4] == 0 && buffer[5] == 0 && buffer[6] == 0 && buffer[7] != 0)
                    {
                        bool firstZero = true;
                        int userIdEndPos = 0;
                        int domainEndPos = 0;
                        for (int i = 8; i < buffer.Length; i++)
                        {
                            if (buffer[i] == 0)
                            {
                                if (firstZero == true)
                                {
                                    userIdEndPos = i;
                                    firstZero = false;
                                }
                                else
                                {
                                    domainEndPos = i;
                                    break;
                                }
                            }
                        }
                        host = Encoding.UTF8.GetString(buffer, userIdEndPos + 1, domainEndPos - userIdEndPos);
                        bufferEndPos = domainEndPos;
                    }
                    else // 도메인이 아닌경우에도 userid 끝을 찾아 버퍼의 끝을 찾는다
                    {
                        for (int i = 8; i < buffer.Length; i++)
                        {
                            if (buffer[i] == 0)
                            {
                                bufferEndPos = i;
                                break;
                            }
                        }
                    }
                }
                else // SOCKS5인 경우
                {
                    clientType = ClientType.SOCKS5;

                    // 추가 HandShake가 필요하다

                    // < Server Hello
                    Client.Send(new byte[] { 0x05, 0x00 });

                    // > client connect request
                    bytes = Client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    if (bytes == 0)
                    {
                        Cleanup();
                        return;
                    }

                    requestBuf = new byte[bytes];
                    Array.Copy(buffer, requestBuf, bytes);

                    if (buffer[3] == 1) // IP 인경우
                    {
                        host = buffer[4].ToString() + "." + buffer[5].ToString() + "." + buffer[6].ToString() + "." +
                               buffer[7].ToString();
                        port = buffer[8] * 256 + buffer[9];

                        bufferEndPos = 9;
                    }
                    else if (buffer[3] == 3) // 도메인
                    {
                        int hostLength = (int)buffer[4];
                        host = Encoding.UTF8.GetString(buffer, 5, hostLength);
                        port = buffer[5 + hostLength] * 256 + buffer[5 + hostLength + 1];

                        bufferEndPos = 5 + hostLength + 1;
                    }
                    else
                    {
                        throw new Exception("wtf");
                    }
                }

                string requestHeader = CreateRequestHeader(host, port);
                Console.WriteLine("[CONNECT] " + host + ":" + port);


                GoogleProxy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                GoogleProxy.Connect("ssl.googlezip.net", 443);

                GoogleProxy.Send(Encoding.UTF8.GetBytes(requestHeader));

                int recv = GoogleProxy.Receive(buffer, buffer.Length, SocketFlags.None);



                var responseHeader = Encoding.UTF8.GetString(buffer, 0, recv);
                if (responseHeader.IndexOf("200") == -1)
                {
                    if (clientType == ClientType.SOCKS4)
                    {
                        Client.Send(new byte[] { 0x00, 0x5B, 0, 0, 0, 0, 0, 0 });
                    }
                    else
                    {
                        Client.Send(new byte[] { 0x05, 0x03, 0, 0, 0, 0, 0, 0 });
                    }
                    Client.Close(10);
                    return;
                }

                if (clientType == ClientType.SOCKS4)
                {
                    Client.Send(new byte[] { 0x00, 0x5A, 0, 0, 0, 0, 0, 0 });
                }
                else
                {
                    requestBuf[1] = 0x00;
                    Client.Send(requestBuf);
                }

                // 이전 request에 같이 섞여온 데이터가 있다면 연결 후에 보내주도록 함
                if (requestBuf.Length - 1 != bufferEndPos)
                {
                    GoogleProxy.Send(requestBuf, bufferEndPos + 1, requestBuf.Length - bufferEndPos, SocketFlags.None);
                }

                LinkConnection();
            }

            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Cleanup();
            }
        }

        /// <summary>
        /// 두개의 연결이 서로 데이터를 받을때마다 서로에게 보내주도록 핸들러를 설정
        /// </summary>
        private void LinkConnection()
        {
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

        private static string CreateRequestHeader(string host, int port)
        {
            int unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            string timestamp = unixTimestamp.ToString();

            string requestHeader = "CONNECT " + host + ":" + port + " HTTP/1.1\r\n" + "Chrome-Proxy: ps=" + timestamp +
                            "-0-0-0, sid=" +
                            Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) +
                            ", b=2214, p=115, c=win\r\n\r\n";
            return requestHeader;
        }

        private void OnGoogleReceived(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.BytesTransferred == 0)
                {
                    Cleanup();
                    return;
                }
                Client.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);

                e.SetBuffer(new byte[8196], 0, 8196);
                if (GoogleProxy.ReceiveAsync(e) == false)
                {
                    OnGoogleReceived(sender, e);
                }
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
                    return;
                }
                GoogleProxy.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);

                e.SetBuffer(new byte[8196], 0, 8196);
                if (Client.ReceiveAsync(e) == false)
                {
                    OnClientReceived(sender, e);
                }
            }
            catch (Exception)
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            //Console.WriteLine("disconnect ");
            Client.Close(10);

            if (GoogleProxy != null)
            {
                GoogleProxy.Close(10);
            }
        }
    }
}
