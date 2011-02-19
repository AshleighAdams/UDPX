using System;
using System.Collections.Generic;
using System.Net;

namespace Test
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if DEBUG
            UDPX.BeginListen(101, null);
            while (true)
            {
                System.Threading.Thread.Sleep(100);
            }
#else
            UDPX.Connect(new IPEndPoint(IPAddress.Loopback, 101), null);
#endif
        }
    }
}
