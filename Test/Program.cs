using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

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
            Encoding e = Encoding.ASCII;
            IUDPXConnection conn = null;
            ConnectHandler ch = delegate(IUDPXConnection Connection)
            {
                if (Connection != null)
                {
                    Console.WriteLine("Connected");
                    conn = Connection;
                    conn.Send(e.GetBytes("Here I am"));
                    conn.ReceivePacketOrdered += delegate(byte[] Data)
                    {
                        Console.WriteLine(e.GetString(Data));
                    };
                }
                else
                {
                    Console.WriteLine("Connection failed");
                }
            };
#if !DEBUG
            UDPX.Listen(101, ch);
#else
            UDPX.Connect(new IPEndPoint(IPAddress.Loopback, 101), ch);
#endif
            while (true)
            {
                string message = Console.ReadLine();
                if (conn != null)
                {
                    conn.Send(e.GetBytes(message));
                }
            }
        }
    }
}
