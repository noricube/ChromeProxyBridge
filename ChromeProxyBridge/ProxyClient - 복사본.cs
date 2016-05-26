using System;
using System.Collections.Generic;
using System.Linq;
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

            do
            {
                int readBytes = Client.Receive(buffer, pos, buffer.Length - pos, SocketFlags.None);
                if (readBytes == 0) // 접속 종료
                {
                    Client.Close();
                    return;
                }

                pos += readBytes;

                if (pos > 4)
                {
                    // 마지막에서 CRLF *2 찾기
                    // 요청이 끝났는지 확인하기 위함
                    if (buffer[pos - 4] == CR && buffer[pos - 3] == LF && buffer[pos - 2] == CR && buffer[pos - 1] == LF)
                    {
                        prevHeader = Encoding.UTF8.GetString(buffer, 0, pos).Replace("Connection: keep-alive", "Connection: close").Replace("HTTP/1.0","HTTP/1.1").Replace("Connection: keep-Alive", "Connection: close");

                        Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                        string timestamp = unixTimestamp.ToString();

                        var headerElements = prevHeader.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        bool first = true;
                        foreach(var header in headerElements)
                        {
                            if ( first )
                            {
                                first = false;
                                continue;
                            }

                            var semiColon = header.IndexOf(':');
                            var key = header.Substring(0, semiColon);
                            var value = header.Substring(semiColon + 2).Trim();

                            headerDict[key] = value;
                        }

                        // http://superuser.com/questions/945924/how-do-i-use-the-google-data-compression-proxy-on-firefox
                        //Console.WriteLine(prevHeader + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");


                        
                        if (prevHeader.StartsWith("CONNECT "))
                        {
                            isConnect = true;
                            modifiedHeader = Encoding.UTF8.GetBytes(prevHeader.Substring(0, prevHeader.Length - 2) + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");
                            Console.WriteLine(modifiedHeader);
                        }
                        else
                        {

                            var host = headerDict["Host"];
                            var realHost = host;
                            if (host.IndexOf(':') == -1)
                            {
                                realHost = host + ":80";
                            }

                            Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + " " + host);

                            modifiedHeader = Encoding.UTF8.GetBytes("CONNECT " + realHost + " HTTP/1.1\r\nHost: " + host + "\r\nProxy-Connection: Close\r\n" + "Chrome-Proxy: ps=" + timestamp + "-0-0-0, sid=" + Util.CalculateMD5Hash(timestamp + "ac4500dd3b7579186c1b0620614fdb1f7d61f944" + timestamp) + ", b=2214, p=115, c=win\r\n\r\n");
                        }

                        break;
                    }


                }
            }
            while (buffer.Length != pos);

            // 버퍼 꽉찰때까지 헤더 끝을 못찾으면 그냥 종료
            if (buffer.Length == pos)
            {
                Client.Close();
                return;
            }

            Socket googleProxy = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            googleProxy.Connect("ssl.googlezip.net", 443);

            googleProxy.Send(modifiedHeader);

            if (isConnect == false)
            {
                // 기존 헤더 날림
                googleProxy.Receive(buffer, buffer.Length, SocketFlags.None);

                googleProxy.Send(Encoding.UTF8.GetBytes(prevHeader));
            }
            else
            {
                googleProxy.Receive(buffer, buffer.Length, SocketFlags.None);
                Client.Send(Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
            }

            googleProxy.NoDelay = true;
            Client.NoDelay = true;

            var readList = new List<Socket>();
            var errorList = new List<Socket>();
            try
            {
                while (true)
                {
                    readList.Add(Client);
                    readList.Add(googleProxy);
                    errorList.Add(Client);
                    errorList.Add(googleProxy);
                    Socket.Select(readList, null, errorList, 100);

                    foreach(var sock in errorList)
                    {
                        googleProxy.Shutdown(SocketShutdown.Both);
                        Client.Shutdown(SocketShutdown.Both);
                        return;
                    }

                    foreach (var sock in readList)
                    {
                        buffer = new byte[8196];
                        if (sock == googleProxy)
                        {
                            
                            int readBytes = googleProxy.Receive(buffer, buffer.Length, SocketFlags.None);
                            Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + "<" + readBytes.ToString());
                            if (readBytes == 0)
                            {
                                googleProxy.Shutdown(SocketShutdown.Both);
                                Client.Shutdown(SocketShutdown.Both);
                                return;
                            }

                            Client.Send(buffer, readBytes, SocketFlags.None);
                        }
                        else
                        {
                            int readBytes = Client.Receive(buffer, buffer.Length, SocketFlags.None);

                            Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + ">" + readBytes.ToString());

                            if (readBytes == 0)
                            {
                                googleProxy.Shutdown(SocketShutdown.Both);
                                Client.Shutdown(SocketShutdown.Both);
                                return;
                            }

                            googleProxy.Send(buffer, readBytes, SocketFlags.None);
                        }
                    }
                }
            }
            catch(SocketException e)
            {
                googleProxy.Close();
                Client.Close();
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            
        }
    }
}
