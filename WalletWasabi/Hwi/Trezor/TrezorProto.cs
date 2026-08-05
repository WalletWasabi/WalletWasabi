using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Minimal protobuf (proto2) wire-format codec for the handful of Trezor messages Wasabi needs.
/// Only varint (wire type 0) and length-delimited (wire type 2) fields are used by those messages.
/// </summary>
public class ProtoWriter
{
	private readonly MemoryStream _stream = new();

	public byte[] ToBytes() => _stream.ToArray();

	public ProtoWriter WriteVarIntField(int fieldNumber, ulong value)
	{
		WriteVarInt((ulong)(fieldNumber << 3));
		WriteVarInt(value);
		return this;
	}

	public ProtoWriter WriteBoolField(int fieldNumber, bool value) =>
		WriteVarIntField(fieldNumber, value ? 1UL : 0UL);

	public ProtoWriter WriteBytesField(int fieldNumber, byte[] value)
	{
		WriteVarInt((ulong)(fieldNumber << 3 | 2));
		WriteVarInt((ulong)value.Length);
		_stream.Write(value);
		return this;
	}

	public ProtoWriter WriteStringField(int fieldNumber, string value) =>
		WriteBytesField(fieldNumber, Encoding.UTF8.GetBytes(value));

	public ProtoWriter WriteMessageField(int fieldNumber, ProtoWriter message) =>
		WriteBytesField(fieldNumber, message.ToBytes());

	/// <summary>Proto2 repeated scalars are encoded unpacked.</summary>
	public ProtoWriter WriteRepeatedVarIntField(int fieldNumber, IEnumerable<uint> values)
	{
		foreach (uint value in values)
		{
			WriteVarIntField(fieldNumber, value);
		}
		return this;
	}

	private void WriteVarInt(ulong value)
	{
		while (value >= 0x80)
		{
			_stream.WriteByte((byte)(value | 0x80));
			value >>= 7;
		}
		_stream.WriteByte((byte)value);
	}
}

public class ProtoReader
{
	public ProtoReader(byte[] buffer)
	{
		_buffer = buffer;
	}

	private readonly byte[] _buffer;
	private int _position;

	public bool TryReadField(out int fieldNumber, out ulong varIntValue, out byte[] lengthDelimitedValue)
	{
		varIntValue = 0;
		lengthDelimitedValue = [];
		fieldNumber = 0;

		if (_position >= _buffer.Length)
		{
			return false;
		}

		ulong key = ReadVarInt();
		fieldNumber = (int)(key >> 3);
		int wireType = (int)(key & 7);

		switch (wireType)
		{
			case 0:
				varIntValue = ReadVarInt();
				break;

			case 2:
				int length = (int)ReadVarInt();
				lengthDelimitedValue = _buffer[_position..(_position + length)];
				_position += length;
				break;

			case 5:
				_position += 4;
				break;

			case 1:
				_position += 8;
				break;

			default:
				throw new InvalidDataException($"Unsupported protobuf wire type {wireType}.");
		}

		return true;
	}

	public static Dictionary<int, List<(ulong VarInt, byte[] Bytes)>> ReadAllFields(byte[] buffer)
	{
		var reader = new ProtoReader(buffer);
		Dictionary<int, List<(ulong, byte[])>> fields = new();
		while (reader.TryReadField(out int fieldNumber, out ulong varIntValue, out byte[] bytesValue))
		{
			if (!fields.TryGetValue(fieldNumber, out var list))
			{
				list = new List<(ulong, byte[])>();
				fields[fieldNumber] = list;
			}
			list.Add((varIntValue, bytesValue));
		}
		return fields;
	}

	private ulong ReadVarInt()
	{
		ulong result = 0;
		int shift = 0;
		while (true)
		{
			byte b = _buffer[_position++];
			result |= (ulong)(b & 0x7F) << shift;
			if ((b & 0x80) == 0)
			{
				return result;
			}
			shift += 7;
		}
	}
}
