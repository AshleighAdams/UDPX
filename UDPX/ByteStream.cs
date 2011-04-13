using System;
using System.Text;
using System.Collections.Generic;


/// <summary>
/// Interface to write to a byte array.
/// </summary>
public abstract class ByteStreamWriter
{
	private const int BS1 = 8;
	private const int BS2 = 16;
	private const int BS3 = 24;
	private const int BS4 = 32;
	private const int BS5 = 40;
	private const int BS6 = 48;
	private const int BS7 = 56;
	private const int BS8 = 64;
	
	public abstract void WriteByte(byte Val);

	public virtual void WriteBytes(byte[] Val)
	{
		for(int i = 0; i < Val.Length; i++)
			this.WriteByte(Val[i]);
	}
	
	public virtual void WriteBool(bool Val)
	{
		if(Val)
			this.WriteByte(1);
		else
			this.WriteByte(0);
	}
	
	public virtual void WriteShort(short Val)
	{
		this.WriteByte((byte)(Val));
        this.WriteByte((byte)(Val >> BS1));
	}
	
	public virtual void WriteUShort(ushort Val)
	{
		this.WriteByte((byte)(Val));
        this.WriteByte((byte)(Val >> BS1));
	}
	
	public virtual void WriteInt(int Val)
	{
        this.WriteByte((byte)(Val));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS3));
	}
	
	public virtual void WriteUInt(uint Val)
	{
        this.WriteByte((byte)(Val));
        this.WriteByte((byte)(Val >> BS1));
        this.WriteByte((byte)(Val >> BS2));
        this.WriteByte((byte)(Val >> BS3));
	}
	
	public virtual void WriteLong(long Val)
	{
        this.WriteByte((byte)(Val));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS4));
		this.WriteByte((byte)(Val >> BS5));
		this.WriteByte((byte)(Val >> BS6));
		this.WriteByte((byte)(Val >> BS7));
	}
	
	public virtual void WriteULong(ulong Val)
	{
        this.WriteByte((byte)(Val));
        this.WriteByte((byte)(Val >> BS1));
        this.WriteByte((byte)(Val >> BS2));
        this.WriteByte((byte)(Val >> BS3));
        this.WriteByte((byte)(Val >> BS4));
        this.WriteByte((byte)(Val >> BS5));
        this.WriteByte((byte)(Val >> BS6));
        this.WriteByte((byte)(Val >> BS7));
	}
	
	public virtual void WriteString(string Val)
	{
		byte[] bytes = ASCIIEncoding.ASCII.GetBytes(Val);
		this.WriteInt(bytes.Length);
		this.WriteBytes(bytes);
	}
	
	/// <summary>
	/// Write a wide string (Unicode)
	/// </summary>
	public virtual void WriteWString(string Val)
	{
		byte[] bytes = ASCIIEncoding.Unicode.GetBytes(Val);
		this.WriteInt(bytes.Length);
		this.WriteBytes(bytes);
	}
}

/// <summary>
/// Interface to read from a byte array.
/// </summary>
public abstract class ByteStreamReader
{
	private const int BS1 = 8;
	private const int BS2 = 16;
	private const int BS3 = 24;
	private const int BS4 = 32;
	private const int BS5 = 40;
	private const int BS6 = 48;
	private const int BS7 = 56;
	private const int BS8 = 64;
	
	public abstract byte ReadByte();
	
	public virtual byte[] ReadBytes(int Length)
	{
		byte[] ret = new byte[Length];
		for(int i = 0; i < Length; i++)
			ret[i] = this.ReadByte();
		return ret;
	}
	
	public virtual bool ReadBool()
	{
		return this.ReadByte() != 0;
	}
	
	public virtual short ReadShort()
	{
        short a = (short)(this.ReadByte());
        short b = (short)(this.ReadByte() << BS1);
		return (short)(a | b);
	}
	
	public virtual ushort ReadUShort()
	{
        ushort a = (ushort)(this.ReadByte());
        ushort b = (ushort)(this.ReadByte() << BS1);
        return (ushort)(a | b);
	}
	
	public virtual int ReadInt()
	{
        int a = this.ReadByte();
        int b = this.ReadByte() << BS1;
        int c = this.ReadByte() << BS2;
        int d = this.ReadByte() << BS3;
        return a | b | c | d;
	}
	
	public virtual uint ReadUInt()
	{
        uint a = (uint)(this.ReadByte());
        uint b = (uint)(this.ReadByte() << BS1);
        uint c = (uint)(this.ReadByte() << BS2);
        uint d = (uint)(this.ReadByte() << BS3);
        return a | b | c | d;
	}
	
	public virtual long ReadLong()
	{
        long a = (long)(this.ReadByte());
        long b = (long)(this.ReadByte() << BS1);
        long c = (long)(this.ReadByte() << BS2);
        long d = (long)(this.ReadByte() << BS3);
        long e = (long)(this.ReadByte() << BS4);
        long f = (long)(this.ReadByte() << BS5);
        long g = (long)(this.ReadByte() << BS6);
        long h = (long)(this.ReadByte() << BS7);
        return a | b | c | d | e | f | g | h;
	}
	
	public virtual ulong ReadULong()
	{
        ulong a = (ulong)(this.ReadByte());
        ulong b = (ulong)(this.ReadByte() << BS1);
        ulong c = (ulong)(this.ReadByte() << BS2);
        ulong d = (ulong)(this.ReadByte() << BS3);
        ulong e = (ulong)(this.ReadByte() << BS4);
        ulong f = (ulong)(this.ReadByte() << BS5);
        ulong g = (ulong)(this.ReadByte() << BS6);
        ulong h = (ulong)(this.ReadByte() << BS7);
        return a | b | c | d | e | f | g | h;
	}
	
	public virtual string ReadString()
	{
		int len = this.ReadInt();
		byte[] bytes = this.ReadBytes(len);
		return ASCIIEncoding.ASCII.GetString(bytes);
	}
	
	public virtual string ReadWString()
	{
		int len = this.ReadInt();
		byte[] bytes = this.ReadBytes(len);
		return ASCIIEncoding.Unicode.GetString(bytes);
	}
	
	public abstract int BytesAvailable { get; }
}





