using System;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace UDPX
{
	public class UDPX
	{
		public int Port
		{
			get
			{
				return this._Port;
			}
			set
			{
				this._Port = value;
			}
		}
		
		public UDPX(int Port)
		{
			int seed = DateTime.Now.Millisecond;
			Random r = new Random(seed);
			
			this._LocalSequence = r.Next();
			this._RemoteSequence = 0;
			
			this._UDPConnection = new System.Net.Sockets.UdpClient(this.Port);
			this._UDPConnection.BeginReceive(this._Recive, null);
		}
		
		private void _Recive(IAsyncResult res)
		{
			IPEndPoint ip = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = this._UDPConnection.EndReceive(res, ref ip);
		}
		
		public void Send(Packet packet)
		{
			if((packet.Flags & PacketFlags.Sequenced) == PacketFlags.Sequenced)
				this._LocalSequence++;
			
			MemoryStream s = new MemoryStream();
			BinaryWriter r = new BinaryWriter(s);
			
			r.Write((byte)packet.Flags);
			
		}
		
		private int _Port;
		private int _RemoteSequence;
		private int _LocalSequence;
		private bool _Connected;
		
		private IPEndPoint _RemotePeer;
		private System.Net.Sockets.UdpClient _UDPConnection;
	}
	
	public struct Packet
	{
		public byte Opcode;
		public byte[] Data;
		public PacketFlags Flags;
	}
	
	[Flags]
	public enum PacketFlags : byte
	{
		Sequenced,
		NoneSequenced,
		Handshake,
		RequestRetransmition
	}
}

