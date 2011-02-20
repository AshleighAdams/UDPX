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
                    conn.KeepAlive = 5.0;
                    conn.Send(e.GetBytes("Here I am"));
                    conn.ReceivePacketOrdered += delegate(bool Checked, byte[] Data)
                    {
                        Console.WriteLine(e.GetString(Data));
                    };
                }
                else
                {
                    Console.WriteLine("Connection failed");
                }
            };

            Console.Write("Server(s), Client(c) >> ");
            char c = Console.ReadKey().KeyChar;
            Console.WriteLine();
            if (c == 's')
            {
                UDPX.Listen(101, ch);
            }
            if (c == 'c')
            {
                Console.Write("Endpoint >> ");
                string endpoint = Console.ReadLine();
                UDPX.Connect(new IPEndPoint(IPAddress.Parse(endpoint), 101), ch);
            }

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
