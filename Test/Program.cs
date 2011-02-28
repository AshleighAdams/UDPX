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
			ByteSteamWriter w = new ByteSteamWriter();
			w.WriteByte(123);
			w.WriteInt(500);
			w.WriteString("ABC");
			byte[] result = w.GetBytes();
			
			ByteSteamReader r = new ByteSteamReader(result);
			byte a = r.ReadByte();
			int b = r.ReadInt();
			string cc = r.ReadString();
			
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
				if(message == "dos")
				{
					for( int i = 0; i < 100000; i++ )
						conn.Send(e.GetBytes("Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message Big Message "), 1);
				}
                if (conn != null)
                {
                    conn.Send(e.GetBytes(message));
                }
            }
        }
    }
}
