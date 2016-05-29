using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromeProxyBridge
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 58080;

            if ( args.Length > 0)
            {
                port = int.Parse(args[0]);
            }

            var proxy = new ProxyServer(port);
            Console.WriteLine("init server on {0}", port);

            proxy.Run();

            while (true)
            {
                Console.WriteLine("Active connections: {0}", proxy.ClientCount);

                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
