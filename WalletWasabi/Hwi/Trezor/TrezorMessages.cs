using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Message ids from trezor-firmware common/protob/messages.proto.
/// </summary>
public enum TrezorMessageType : ushort
{
	Initialize = 0,
	Success = 2,
	Failure = 3,
	GetPublicKey = 11,
	PublicKey = 12,
	SignTx = 15,
	Features = 17,
	TxRequest = 21,
	TxAck = 22,
	ButtonRequest = 26,
	ButtonAck = 27,
	GetAddress = 29,
	Address = 30,
	PassphraseRequest = 41,
	PassphraseAck = 42,
	GetOwnershipProof = 49,
	OwnershipProof = 50,
	AuthorizeCoinJoin = 51,
	GetFeatures = 55,
	DoPreauthorized = 84,
	PreauthorizedRequest = 85,
	UnlockPath = 93,
	UnlockedPathRequest = 94,
}

public enum TrezorInputScriptType : uint
{
	SpendAddress = 0,
	External = 2,
	SpendWitness = 3,
	SpendTaproot = 5,
}

public enum TrezorOutputScriptType : uint
{
	PayToAddress = 0,
	PayToTaproot = 6,
}

public enum TrezorTxRequestType : uint
{
	TxInput = 0,
	TxOutput = 1,
	TxMeta = 2,
	TxFinished = 3,
}

public record TrezorMessage(TrezorMessageType MessageType, byte[] Payload)
{
	public static TrezorMessage Empty(TrezorMessageType messageType) => new(messageType, []);

	public Dictionary<int, List<(ulong VarInt, byte[] Bytes)>> ReadFields() => ProtoReader.ReadAllFields(Payload);
}

public record TrezorFeatures(uint MajorVersion, uint MinorVersion, uint PatchVersion, string Model)
{
	public Version Version => new((int)MajorVersion, (int)MinorVersion, (int)PatchVersion);

	public static TrezorFeatures FromMessage(TrezorMessage message)
	{
		var fields = message.ReadFields();
		return new TrezorFeatures(
			MajorVersion: (uint)fields[2][0].VarInt,
			MinorVersion: (uint)fields[3][0].VarInt,
			PatchVersion: (uint)fields[4][0].VarInt,
			Model: fields.TryGetValue(21, out var model) ? Encoding.UTF8.GetString(model[0].Bytes) : "");
	}
}

public record TrezorTxRequest(TrezorTxRequestType RequestType, int RequestIndex, int? SignatureIndex, byte[] Signature)
{
	public static TrezorTxRequest FromMessage(TrezorMessage message)
	{
		var fields = message.ReadFields();
		var requestType = (TrezorTxRequestType)(fields.TryGetValue(1, out var type) ? type[0].VarInt : 0);

		int requestIndex = 0;
		if (fields.TryGetValue(2, out var details))
		{
			var detailFields = ProtoReader.ReadAllFields(details[0].Bytes);
			requestIndex = detailFields.TryGetValue(1, out var index) ? (int)index[0].VarInt : 0;
		}

		int? signatureIndex = null;
		byte[] signature = [];
		if (fields.TryGetValue(3, out var serialized))
		{
			var serializedFields = ProtoReader.ReadAllFields(serialized[0].Bytes);
			if (serializedFields.TryGetValue(1, out var sigIndex))
			{
				signatureIndex = (int)sigIndex[0].VarInt;
			}
			if (serializedFields.TryGetValue(2, out var sig))
			{
				signature = sig[0].Bytes;
			}
		}

		return new TrezorTxRequest(requestType, requestIndex, signatureIndex, signature);
	}
}

/// <summary>Transaction input as streamed to the device during SignTx.</summary>
public record TrezorTxInput
{
	public uint[] AddressN { get; init; } = [];
	public required byte[] PrevHash { get; init; }
	public required uint PrevIndex { get; init; }
	public uint Sequence { get; init; } = 0xFFFFFFFF;
	public required TrezorInputScriptType ScriptType { get; init; }
	public required ulong Amount { get; init; }
	public byte[] ScriptPubKey { get; init; } = [];
	public byte[] OwnershipProof { get; init; } = [];
	public byte[] CommitmentData { get; init; } = [];

	public TrezorMessage ToTxAckInput()
	{
		var input = new ProtoWriter()
			.WriteRepeatedVarIntField(1, AddressN)
			.WriteBytesField(2, PrevHash)
			.WriteVarIntField(3, PrevIndex)
			.WriteVarIntField(5, Sequence)
			.WriteVarIntField(6, (ulong)ScriptType)
			.WriteVarIntField(8, Amount);

		if (ScriptType == TrezorInputScriptType.External)
		{
			// The device verifies the externality of foreign inputs with their SLIP-19 ownership proofs.
			input.WriteBytesField(14, OwnershipProof);
			input.WriteBytesField(15, CommitmentData);
			input.WriteBytesField(19, ScriptPubKey);
		}

		// TxAckInput { tx = 1 { input = 2 } }, wire-alias of TxAck.
		var wrapper = new ProtoWriter().WriteMessageField(2, input);
		return new TrezorMessage(TrezorMessageType.TxAck, new ProtoWriter().WriteMessageField(1, wrapper).ToBytes());
	}
}

/// <summary>Transaction output as streamed to the device during SignTx.</summary>
public record TrezorTxOutput
{
	public uint[] AddressN { get; init; } = [];
	public string Address { get; init; } = "";
	public required ulong Amount { get; init; }
	public required TrezorOutputScriptType ScriptType { get; init; }

	public TrezorMessage ToTxAckOutput()
	{
		var output = new ProtoWriter();
		if (AddressN.Length > 0)
		{
			output.WriteRepeatedVarIntField(2, AddressN);
		}
		else
		{
			output.WriteStringField(1, Address);
		}
		output.WriteVarIntField(3, Amount);
		output.WriteVarIntField(4, (ulong)ScriptType);

		// TxAckOutput { tx = 1 { output = 5 } }, wire-alias of TxAck.
		var wrapper = new ProtoWriter().WriteMessageField(5, output);
		return new TrezorMessage(TrezorMessageType.TxAck, new ProtoWriter().WriteMessageField(1, wrapper).ToBytes());
	}
}

public static class TrezorMessages
{
	public static TrezorMessage Initialize() =>
		TrezorMessage.Empty(TrezorMessageType.Initialize);

	public static TrezorMessage ButtonAck() =>
		TrezorMessage.Empty(TrezorMessageType.ButtonAck);

	public static TrezorMessage PassphraseAck(string passphrase) =>
		new(TrezorMessageType.PassphraseAck, new ProtoWriter().WriteStringField(1, passphrase).ToBytes());

	public static TrezorMessage DoPreauthorized() =>
		TrezorMessage.Empty(TrezorMessageType.DoPreauthorized);

	public static TrezorMessage UnlockPath(IEnumerable<uint> addressN) =>
		new(TrezorMessageType.UnlockPath, new ProtoWriter().WriteRepeatedVarIntField(1, addressN).ToBytes());

	public static TrezorMessage GetPublicKey(IEnumerable<uint> addressN, string coinName, TrezorInputScriptType scriptType) =>
		new(
			TrezorMessageType.GetPublicKey,
			new ProtoWriter()
				.WriteRepeatedVarIntField(1, addressN)
				.WriteStringField(4, coinName)
				.WriteVarIntField(5, (ulong)scriptType)
				.WriteBoolField(6, true) // ignore_xpub_magic: use xpub/tpub prefixes for all script types.
				.ToBytes());

	public static TrezorMessage GetAddress(IEnumerable<uint> addressN, string coinName, TrezorInputScriptType scriptType, bool showDisplay) =>
		new(
			TrezorMessageType.GetAddress,
			new ProtoWriter()
				.WriteRepeatedVarIntField(1, addressN)
				.WriteStringField(2, coinName)
				.WriteBoolField(3, showDisplay)
				.WriteVarIntField(5, (ulong)scriptType)
				.ToBytes());

	public static TrezorMessage AuthorizeCoinJoin(string coordinator, ulong maxRounds, ulong maxCoordinatorFeeRate, ulong maxFeePerKvbyte, IEnumerable<uint> addressN, string coinName) =>
		new(
			TrezorMessageType.AuthorizeCoinJoin,
			new ProtoWriter()
				.WriteStringField(1, coordinator)
				.WriteVarIntField(2, maxRounds)
				.WriteVarIntField(3, maxCoordinatorFeeRate)
				.WriteVarIntField(4, maxFeePerKvbyte)
				.WriteRepeatedVarIntField(5, addressN)
				.WriteStringField(6, coinName)
				.WriteVarIntField(7, (ulong)TrezorInputScriptType.SpendTaproot)
				.ToBytes());

	public static TrezorMessage GetOwnershipProof(IEnumerable<uint> addressN, string coinName, byte[] commitmentData) =>
		new(
			TrezorMessageType.GetOwnershipProof,
			new ProtoWriter()
				.WriteRepeatedVarIntField(1, addressN)
				.WriteStringField(2, coinName)
				.WriteVarIntField(3, (ulong)TrezorInputScriptType.SpendTaproot)
				.WriteBoolField(5, true) // user_confirmation, required for coinjoin proofs.
				.WriteBytesField(7, commitmentData)
				.ToBytes());

	public static TrezorMessage SignTx(int inputCount, int outputCount, string coinName, uint version, uint lockTime, (ulong FeeRate, ulong NoFeeThreshold, ulong MinRegistrableAmount)? coinJoinRequest)
	{
		var writer = new ProtoWriter()
			.WriteVarIntField(1, (ulong)outputCount)
			.WriteVarIntField(2, (ulong)inputCount)
			.WriteStringField(3, coinName)
			.WriteVarIntField(4, version)
			.WriteVarIntField(5, lockTime)
			.WriteBoolField(13, false); // serialize: signatures only, the transaction is assembled locally.

		if (coinJoinRequest is { } request)
		{
			writer.WriteMessageField(
				14,
				new ProtoWriter()
					.WriteVarIntField(1, request.FeeRate)
					.WriteVarIntField(2, request.NoFeeThreshold)
					.WriteVarIntField(3, request.MinRegistrableAmount));
		}

		return new TrezorMessage(TrezorMessageType.SignTx, writer.ToBytes());
	}

	public static string GetString(this TrezorMessage message, int fieldNumber)
	{
		var fields = message.ReadFields();
		return fields.TryGetValue(fieldNumber, out var field) ? Encoding.UTF8.GetString(field[0].Bytes) : "";
	}

	public static byte[] GetBytes(this TrezorMessage message, int fieldNumber)
	{
		var fields = message.ReadFields();
		return fields.TryGetValue(fieldNumber, out var field) ? field[0].Bytes : [];
	}
}
