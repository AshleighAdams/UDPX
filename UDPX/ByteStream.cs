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
		{
			this.WriteByte(1);
		}else{
			this.WriteByte(0);
		}
	}
	
	public virtual void WriteShort(short Val)
	{
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public virtual void WriteUShort(ushort Val)
	{
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public virtual void WriteInt(int Val)
	{
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public virtual void WriteUInt(uint Val)
	{
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public virtual void WriteLong(long Val)
	{
		this.WriteByte((byte)(Val >> BS7));
		this.WriteByte((byte)(Val >> BS6));
		this.WriteByte((byte)(Val >> BS5));
		this.WriteByte((byte)(Val >> BS4));
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public virtual void WriteULong(ulong Val)
	{
		this.WriteByte((byte)(Val >> BS7));
		this.WriteByte((byte)(Val >> BS6));
		this.WriteByte((byte)(Val >> BS5));
		this.WriteByte((byte)(Val >> BS4));
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
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
		short total = 0;
		total += (short)(this.ReadByte() << BS1);
		total += (short)(this.ReadByte());
		return total;
	}
	
	public virtual ushort ReadUShort()
	{
		ushort total = 0;
		total += (ushort)(this.ReadByte() << BS1);
		total += (ushort)(this.ReadByte());
		return total;
	}
	
	public virtual int ReadInt()
	{
		int total = 0;
		total += this.ReadByte() << BS3;
		total += this.ReadByte() << BS2;
		total += this.ReadByte() << BS1;
		total += this.ReadByte();
		return total;
	}
	
	public virtual uint ReadUInt()
	{
		uint total = 0;
		total += (uint)(this.ReadByte() << BS3);
		total += (uint)(this.ReadByte() << BS2);
		total += (uint)(this.ReadByte() << BS1);
		total += (uint)(this.ReadByte());
		return total;
	}
	
	public virtual long ReadLong()
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
	
	public virtual ulong ReadULong()
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





