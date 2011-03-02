
using System;
using System.Collections.Generic;
using System.Text;

public class MemoryByteSteamWriter : ByteStreamWriter
{
	private const int FractionSize = 1024;

	public MemoryByteSteamWriter()
	{
		this._Bytes = new List<byte[]>();
		this._Buffer = new byte[FractionSize];
		this._BufferPosition = 0;
		this._Length = 0;
	}
	
	private byte[] _Buffer;
	private ushort _BufferPosition;
	public override void WriteByte (byte Val)
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
	
	private List<byte[]> _Bytes;
}
