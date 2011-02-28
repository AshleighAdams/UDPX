using System;
using System.Collections.Generic;

/// <summary>
/// Interface to write to a byte array.
/// </summary>
public interface IByteStreamWriter
{
	void WriteByte(byte Val);
	void WriteBytes(byte[] Val);
	
	void WriteBool(bool Val);
	
	void WriteShort(short Val);
	void WriteUShort(ushort Val);
	void WriteInt(int Val);
	void WriteUInt(uint Val);
	void WriteLong(long Val);
	void WriteULong(ulong Val);
	
	void WriteString(string Val);
	void WriteWString(string Val);
}

/// <summary>
/// Interface to read from a byte array.
/// </summary>
public interface IByteStreamReader
{
	byte ReadByte();
	byte[] ReadBytes(int Length);
	
	bool ReadBool();
	
	short ReadShort();
	ushort ReadUShort();
	int ReadInt();
	uint ReadUInt();
	long ReadLong();
	ulong ReadULong();
	
	string ReadString();
	string ReadWString();
	
	int BytesAvailible { get; }
}





