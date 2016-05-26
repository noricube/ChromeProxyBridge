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
            var proxy = new ProxyServer(58080);
            proxy.Run();
        }
    }
}
