
using System;
using System.Collections.Generic;
using System.Text;

public class ByteSteamWriter : IByteStreamWriter
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
	private const int FractionSize = 1024;
	
	
	public ByteSteamWriter()
	{
		this._Bytes = new List<byte[]>();
		this._Buffer = new byte[FractionSize];
		this._BufferPosition = 0;
		this._Length = 0;
	}
	
	private byte[] _Buffer;
	private ushort _BufferPosition;
	public void WriteByte(byte Val)
	{
		this._Length++;
		this._Buffer[this._BufferPosition++] = Val;
		
		if(this._BufferPosition > FractionSize)
			this.Flush();
	}
	
	public void Flush()
	{
		if(this._BufferPosition > 0)
		{	
			this._Bytes.Add(this._Buffer);
			this._Buffer = new byte[FractionSize];
			this._BufferPosition = 0;
		}
	}
	
	/// <summary>
	/// The number of written bytes.
	/// </summary>
	public int Length { get { return this._Length; } }
	private int _Length;
	
	/// <summary>
	/// Retrive the byte array.
	/// </summary>
	public byte[] GetBytes()
	{
		this.Flush();
		byte[] ret = new byte[this.Length];
		for(int i = 0; i < this._Length; i++)
		{
			int listbit = i / FractionSize;
			int position = i % FractionSize;
			ret[i] = this._Bytes[listbit][position];
		}
		return ret;
	}
	
	public void WriteBytes(byte[] Val)
	{
		for(int i = 0; i < Val.Length; i++)
			this.WriteByte(Val[i]);
	}
	
	public void WriteBool(bool Val)
	{
		if(Val)
		{
			this.WriteByte(1);
		}else{
			this.WriteByte(0);
		}
	}
	
	public void WriteShort(short Val)
	{
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public void WriteUShort(ushort Val)
	{
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public void WriteInt(int Val)
	{
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public void WriteUInt(uint Val)
	{
		this.WriteByte((byte)(Val >> BS3));
		this.WriteByte((byte)(Val >> BS2));
		this.WriteByte((byte)(Val >> BS1));
		this.WriteByte((byte)(Val));
	}
	
	public void WriteLong(long Val)
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
	
	public void WriteULong(ulong Val)
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
	
	public void WriteString(string Val)
	{
		byte[] bytes = ASCIIEncoding.ASCII.GetBytes(Val);
		this.WriteInt(bytes.Length);
		this.WriteBytes(bytes);
	}
	
	/// <summary>
	/// Write a wide string (Unicode)
	/// </summary>
	public void WriteWString(string Val)
	{
		byte[] bytes = ASCIIEncoding.Unicode.GetBytes(Val);
		this.WriteInt(bytes.Length);
		this.WriteBytes(bytes);
	}
	
	private List<byte[]> _Bytes;
}
