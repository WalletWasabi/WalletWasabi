using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>
/// Drives <see cref="TrezorDevice.SignCoinJoinAsync"/> against a scripted transport to exercise
/// the full SignTx/TxRequest/TxAck state machine without a device.
/// </summary>
public class TrezorSigningFlowTests
{
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

		var (inputs, outputs) = CoinJoin();

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
		Assert.Equal(2UL, txInputFields[6][0].VarInt); // EXTERNAL
		Assert.False(txInputFields.ContainsKey(1)); // No address_n for a foreign input.
	}

	/// <summary>
	/// The key chain tells the device which input and output are ours and proves the foreign input, signs once for the
	/// whole round, and answers the per-coin calls from what the device returned.
	/// </summary>
	[Fact]
	public void TheKeyChainStreamsTheRoundAndSignsItOnce()
	{
		var keyManager = TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: true);
		var ownKey = keyManager.GetNextReceiveKey(new LabelsArray("test"), ScriptPubKeyType.TaprootBIP86);
		var ownCoin = new Coin(new OutPoint(uint256.One, 0), new TxOut(Money.Coins(1m), ownKey.P2Taproot));
		var (foreignKeyChain, foreignCoin, _) = WabiSabiFactory.CreateCoinKeyPairs();
		var commitment = new CoinJoinInputCommitmentData("coordinator", uint256.One);
		var foreignProof = foreignKeyChain.GetOwnershipProof(foreignCoin, commitment);
		var ownOutput = keyManager.GetNextReceiveKey(new LabelsArray("test"), ScriptPubKeyType.TaprootBIP86);
		var regularOutput = keyManager.GetNextReceiveKey(new LabelsArray("test"), ScriptPubKeyType.Segwit);

		var tx = Network.Main.CreateTransaction();
		tx.Inputs.Add(ownCoin.Outpoint);
		tx.Inputs.Add(foreignCoin.Outpoint);
		tx.Outputs.Add(Money.Coins(0.5m), ownOutput.P2Taproot);
		tx.Outputs.Add(Money.Coins(0.5m), regularOutput.P2wpkhScript);
		var unsigned = new TransactionWithPrecomputedData(
			tx,
			tx.PrecomputeTransactionData([ownCoin, foreignCoin.Coin]),
			ImmutableDictionary<OutPoint, OwnershipProof>.Empty.Add(foreignCoin.Outpoint, foreignProof));

		byte[] signature = Enumerable.Repeat((byte)0xAA, 64).ToArray();
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(TrezorMessage.Empty(TrezorMessageType.PreauthorizedRequest));
		transport.Responses.Enqueue(new TrezorMessage(TrezorMessageType.OwnershipProof, new ProtoWriter().WriteBytesField(1, foreignProof.ToBytes()).ToBytes()));
		transport.Responses.Enqueue(TrezorMessage.Empty(TrezorMessageType.PreauthorizedRequest));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxInput, 0));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxInput, 1));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxOutput, 0));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxOutput, 1));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxFinished, serialized: (0, signature)));
		using var device = new TrezorDevice(transport);
		using var keyChain = new TrezorKeyChain(device, keyManager);

		keyChain.GetOwnershipProof(ownKey.GetAddress(Network.Main), commitment);
		var signed = keyChain.Sign(unsigned, ownCoin);
		int callsAfterSigning = transport.Received.Count;
		keyChain.Sign(unsigned, ownCoin);

		Assert.Equal(callsAfterSigning, transport.Received.Count);
		Assert.Equal(signature, signed.Inputs[0].WitScript[0]);

		var acks = transport.Received.Where(message => message.MessageType == TrezorMessageType.TxAck).ToList();
		static Dictionary<int, List<(ulong VarInt, byte[] Bytes)>> Payload(TrezorMessage ack, int field) =>
			ProtoReader.ReadAllFields(ProtoReader.ReadAllFields(ack.ReadFields()[1][0].Bytes)[field][0].Bytes);

		var ownInput = Payload(acks[0], 2);
		Assert.Equal((ulong)TrezorInputScriptType.SpendTaproot, ownInput[6][0].VarInt);
		Assert.Equal(ownKey.FullKeyPath.Indexes, ownInput[1].Select(index => (uint)index.VarInt));
		Assert.False(ownInput.ContainsKey(14));

		var foreignInput = Payload(acks[1], 2);
		Assert.Equal((ulong)TrezorInputScriptType.External, foreignInput[6][0].VarInt);
		Assert.Equal(foreignProof.ToBytes(), foreignInput[14][0].Bytes);
		Assert.Equal(commitment.ToBytes(), foreignInput[15][0].Bytes);
		Assert.Equal(foreignCoin.ScriptPubKey.ToBytes(), foreignInput[19][0].Bytes);

		var ownOutputAck = Payload(acks[2], 5);
		Assert.Equal((ulong)TrezorOutputScriptType.PayToTaproot, ownOutputAck[4][0].VarInt);
		Assert.Equal(ownOutput.FullKeyPath.Indexes, ownOutputAck[2].Select(index => (uint)index.VarInt));

		// A key of this wallet, but outside the coinjoin account: to the device it is foreign.
		var regularOutputAck = Payload(acks[3], 5);
		Assert.Equal((ulong)TrezorOutputScriptType.PayToAddress, regularOutputAck[4][0].VarInt);
		Assert.False(regularOutputAck.ContainsKey(2));
	}

	/// <summary>Our taproot input and a foreign one, our output and a foreign one.</summary>
	private static (List<TrezorTxInput> Inputs, List<TrezorTxOutput> Outputs) CoinJoin()
	{
		var accountKeyPath = Slip25.GetCoinJoinAccountKeyPath(Network.Main);
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
		return (inputs, outputs);
	}

	/// <summary>The signatures come from the device, which is untrusted input like everything the bridge relays.</summary>
	[Theory]
	[InlineData(5)] // An input that does not exist.
	[InlineData(1)] // The foreign input, while ours stays unsigned.
	public async Task ASignatureForAnotherInputThanOursIsRefusedAsync(int signatureIndex)
	{
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(TrezorMessage.Empty(TrezorMessageType.PreauthorizedRequest));
		transport.Responses.Enqueue(TxRequest(TrezorTxRequestType.TxFinished, serialized: (signatureIndex, new byte[64])));
		var (inputs, outputs) = CoinJoin();

		using var device = new TrezorDevice(transport);
		await Assert.ThrowsAsync<TrezorException>(() => device.SignCoinJoinAsync(inputs, outputs, version: 1, lockTime: 0, Money.Satoshis(5000), Network.Main, CancellationToken.None));
	}
}
