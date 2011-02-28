using System;
using System.Collections.Generic;
using System.Text;

public class ByteSteamReader : IByteStreamReader
{
	private const int BS1 = 8;
	private const int BS2 = 16;
	private const int BS3 = 24;
	private const int BS4 = 32;
	private const int BS5 = 40;
	private const int BS6 = 48;
	private const int BS7 = 56;
	private const int BS8 = 64;
	private const int BS9 = 72;
	
	public ByteSteamReader(byte[] Data)
	{
		this._Data = Data;
		this._Position = 0;
		this._Length = this._Data.Length;
	}
	
	public byte ReadByte()
	{
		if(this.BytesAvailible == 0) return 0;
		return this._Data[this._Position++];
	}
	
	public byte[] ReadBytes(int Length)
	{
		byte[] ret = new byte[Length];
		for(int i = 0; i < Length; i++)
			ret[i] = this.ReadByte();
		return ret;
	}
	
	public bool ReadBool()
	{
		return this.ReadByte() != 0;
	}
	
	public short ReadShort()
	{
		short total = 0;
		total += (short)(this.ReadByte() << BS1);
		total += (short)(this.ReadByte());
		return total;
	}
	
	public ushort ReadUShort()
	{
		ushort total = 0;
		total += (ushort)(this.ReadByte() << BS1);
		total += (ushort)(this.ReadByte());
		return total;
	}
	
	public int ReadInt()
	{
		int total = 0;
		total += this.ReadByte() << BS3;
		total += this.ReadByte() << BS2;
		total += this.ReadByte() << BS1;
		total += this.ReadByte();
		return total;
	}
	
	public uint ReadUInt()
	{
		uint total = 0;
		total += (uint)(this.ReadByte() << BS3);
		total += (uint)(this.ReadByte() << BS2);
		total += (uint)(this.ReadByte() << BS1);
		total += (uint)(this.ReadByte());
		return total;
	}
	
	public long ReadLong()
	{
		long total = 0;
		total += (long)(this.ReadByte() << BS7);
		total += (long)(this.ReadByte() << BS6);
		total += (long)(this.ReadByte() << BS5);
		total += (long)(this.ReadByte() << BS4);
		total += (long)(this.ReadByte() << BS3);
		total += (long)(this.ReadByte() << BS2);
		total += (long)(this.ReadByte() << BS1);
		total += (long)(this.ReadByte());
		return total;
	}
	
	public ulong ReadULong()
	{
		ulong total = 0;
		total += (ulong)(this.ReadByte() << BS7);
		total += (ulong)(this.ReadByte() << BS6);
		total += (ulong)(this.ReadByte() << BS5);
		total += (ulong)(this.ReadByte() << BS4);
		total += (ulong)(this.ReadByte() << BS3);
		total += (ulong)(this.ReadByte() << BS2);
		total += (ulong)(this.ReadByte() << BS1);
		total += (ulong)(this.ReadByte());
		return total;
	}
	
	public string ReadString()
	{
		int len = this.ReadInt();
		byte[] bytes = this.ReadBytes(len);
		return ASCIIEncoding.ASCII.GetString(bytes);
	}
	
	/// <summary>
	/// Read a wide string
	/// </summary>
	public string ReadWString()
	{
		int len = this.ReadInt();
		byte[] bytes = this.ReadBytes(len);
		return ASCIIEncoding.Unicode.GetString(bytes);
	}
	
	/// <summary>
	/// The number of bytes that can be read.
	/// </summary>
	public int BytesAvailible
	{
		get{ return this._Length - this._Position; }
	}
	
	private int _Position;
	private int _Length;
	private byte[] _Data;
}
