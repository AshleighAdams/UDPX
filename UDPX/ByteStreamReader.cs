using System;
using System.Collections.Generic;
using System.Text;

public class MemoryByteSteamReader : ByteStreamReader
{
	public MemoryByteSteamReader(byte[] Data)
	{
		this._Data = Data;
		this._Position = 0;
		this._Length = this._Data.Length;
	}
	
	public override byte ReadByte()
	{
		if(this.BytesAvailable == 0) return 0;
		return this._Data[this._Position++];
	}
	
	/// <summary>
	/// The number of bytes that can be read.
	/// </summary>
	public override int BytesAvailable
	{
		get{ return this._Length - this._Position; }
	}
	
	private int _Position;
	private int _Length;
	private byte[] _Data;
}
