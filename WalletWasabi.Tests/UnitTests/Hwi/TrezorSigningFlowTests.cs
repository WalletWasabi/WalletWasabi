using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>
/// Drives <see cref="TrezorDevice.SignCoinJoinAsync"/> against a scripted transport to exercise
/// the full SignTx/TxRequest/TxAck state machine without a device.
/// </summary>
public class TrezorSigningFlowTests
{
	private class ScriptedTransport : TrezorBridgeTransport
	{
		public ScriptedTransport()
			: base("http://127.0.0.1:0")
		{
		}

		public List<TrezorMessage> Received { get; } = new();
		public Queue<TrezorMessage> Responses { get; } = new();

		public override Task<TrezorMessage> CallAsync(string session, TrezorMessage message, CancellationToken cancellationToken)
		{
			Received.Add(message);
			return Task.FromResult(Responses.Dequeue());
		}
	}

	private static TrezorMessage TxRequest(TrezorTxRequestType requestType, int requestIndex = 0, (int Index, byte[] Signature)? serialized = null)
	{
		var writer = new ProtoWriter()
			.WriteVarIntField(1, (ulong)requestType)
			.WriteMessageField(2, new ProtoWriter().WriteVarIntField(1, (ulong)requestIndex));

		if (serialized is { } s)
		{
			writer.WriteMessageField(3, new ProtoWriter().WriteVarIntField(1, (ulong)s.Index).WriteBytesField(2, s.Signature));
		}

		return new TrezorMessage(TrezorMessageType.TxRequest, writer.ToBytes());
	}

	[Fact]
	public async Task SignCoinJoinRunsTheTxRequestStateMachineAsync()
	{
		byte[] signature = Enumerable.Repeat((byte)0xAA, 64).ToArray();
		using var transport = new ScriptedTransport(); // Also disposed by the device, double dispose is fine.
		transport.Responses.Enqueue(TrezorMessage.Empty(TrezorMessageType.PreauthorizedRequest)); // DoPreauthorized
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxInput, 0));                   // SignTx
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxInput, 1));                   // TxAckInput 0
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxOutput, 0));                  // TxAckInput 1
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxOutput, 1));                  // TxAckOutput 0
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxInput, 0));                   // TxAckOutput 1, signing pass starts
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxFinished, serialized: (0, signature)));

		var accountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main);
		var inputs = new List<TrezorTxInput>
		{
			new()
			{
				AddressN = accountKeyPath.Derive(1, false).Derive(0, false).Indexes,
				PrevHash = new byte[32],
				PrevIndex = 0,
				ScriptType = TrezorInputScriptType.SpendTaproot,
				Amount = 100_000,
			},
			new()
			{
				PrevHash = new byte[32],
				PrevIndex = 1,
				ScriptType = TrezorInputScriptType.External,
				Amount = 200_000,
				ScriptPubKey = [0x51, 0x20],
				OwnershipProof = [0x53, 0x4C],
				CommitmentData = [0x01],
			},
		};
		var outputs = new List<TrezorTxOutput>
		{
			new() { AddressN = accountKeyPath.Derive(1, false).Derive(1, false).Indexes, Amount = 99_000, ScriptType = TrezorOutputScriptType.PayToTaproot },
			new() { Address = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", Amount = 200_500, ScriptType = TrezorOutputScriptType.PayToAddress },
		};

		using var device = new TrezorDevice(transport);
		var signatures = await device.SignCoinJoinAsync(inputs, outputs, version: 1, lockTime: 0, Money.Satoshis(5000), Network.Main, CancellationToken.None);

		var single = Assert.Single(signatures);
		Assert.Equal(0, single.Key);
		Assert.Equal(signature, single.Value);

		Assert.Equal(
			[
				TrezorMessageType.DoPreauthorized,
				TrezorMessageType.SignTx,
				TrezorMessageType.TxAck, // input 0
				TrezorMessageType.TxAck, // input 1
				TrezorMessageType.TxAck, // output 0
				TrezorMessageType.TxAck, // output 1
				TrezorMessageType.TxAck, // input 0 again for signing
			],
			transport.Received.Select(x => x.MessageType));

		// The external input must carry its ownership proof, commitment data and scriptPubKey.
		var externalInputAck = transport.Received[3].Payload;
		var txInputFields = ProtoReader.ReadAllFields(ProtoReader.ReadAllFields(ProtoReader.ReadAllFields(externalInputAck)[1][0].Bytes)[2][0].Bytes);
		Assert.Equal(new byte[] { 0x53, 0x4C }, txInputFields[14][0].Bytes);
		Assert.Equal(new byte[] { 0x01 }, txInputFields[15][0].Bytes);
		Assert.Equal(new byte[] { 0x51, 0x20 }, txInputFields[19][0].Bytes);
	}
}
