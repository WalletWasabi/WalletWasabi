using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class TrezorProtocolTests
{
	[Fact]
	public void ProtobufRoundtrip()
	{
		byte[] payload = new ProtoWriter()
			.WriteVarIntField(1, 0)
			.WriteVarIntField(2, 300)
			.WriteVarIntField(3, ulong.MaxValue)
			.WriteRepeatedVarIntField(4, [0x80000000, 5])
			.WriteStringField(5, "coordinator")
			.WriteBytesField(6, [0xDE, 0xAD])
			.WriteMessageField(7, new ProtoWriter().WriteVarIntField(1, 42))
			.ToBytes();

		var fields = ProtoReader.ReadAllFields(payload);

		Assert.Equal(0UL, fields[1][0].VarInt);
		Assert.Equal(300UL, fields[2][0].VarInt);
		Assert.Equal(ulong.MaxValue, fields[3][0].VarInt);
		Assert.Equal([0x80000000UL, 5UL], fields[4].Select(x => x.VarInt));
		Assert.Equal("coordinator", System.Text.Encoding.UTF8.GetString(fields[5][0].Bytes));
		Assert.Equal(new byte[] { 0xDE, 0xAD }, fields[6][0].Bytes);
		Assert.Equal(42UL, ProtoReader.ReadAllFields(fields[7][0].Bytes)[1][0].VarInt);
	}

	[Fact]
	public void AuthorizeCoinJoinEncoding()
	{
		var accountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main);
		var message = TrezorMessages.AuthorizeCoinJoin("CoinJoinCoordinatorIdentifier", 10, 0, 150_000, accountKeyPath.Indexes, "Bitcoin");

		Assert.Equal(TrezorMessageType.AuthorizeCoinJoin, message.MessageType);

		var fields = message.ReadFields();
		Assert.Equal("CoinJoinCoordinatorIdentifier", System.Text.Encoding.UTF8.GetString(fields[1][0].Bytes));
		Assert.Equal(10UL, fields[2][0].VarInt);
		Assert.Equal(0UL, fields[3][0].VarInt);
		Assert.Equal(150_000UL, fields[4][0].VarInt);
		Assert.Equal([10025 | 0x80000000UL, 0x80000000UL, 0x80000000UL, 1 | 0x80000000UL], fields[5].Select(x => x.VarInt));
		Assert.Equal(5UL, fields[7][0].VarInt); // SPENDTAPROOT
	}

	[Fact]
	public void TxAckInputWrapsInputTwice()
	{
		var txInput = new TrezorTxInput
		{
			AddressN = [10025 | 0x80000000, 0x80000000, 0x80000000, 1 | 0x80000000, 1, 0],
			PrevHash = Enumerable.Repeat((byte)0xAB, 32).ToArray(),
			PrevIndex = 3,
			ScriptType = TrezorInputScriptType.SpendTaproot,
			Amount = 123_456,
		};

		var message = txInput.ToTxAckInput();
		Assert.Equal(TrezorMessageType.TxAck, message.MessageType);

		// TxAckInput { tx = 1 { input = 2 { TxInput } } }
		var wrapper = ProtoReader.ReadAllFields(message.Payload)[1][0].Bytes;
		var inner = ProtoReader.ReadAllFields(wrapper)[2][0].Bytes;
		var fields = ProtoReader.ReadAllFields(inner);

		Assert.Equal(6, fields[1].Count);
		Assert.Equal(txInput.PrevHash, fields[2][0].Bytes);
		Assert.Equal(3UL, fields[3][0].VarInt);
		Assert.Equal(0xFFFFFFFFUL, fields[5][0].VarInt);
		Assert.Equal(5UL, fields[6][0].VarInt);
		Assert.Equal(123_456UL, fields[8][0].VarInt);
		Assert.False(fields.ContainsKey(19)); // script_pubkey is only set for external inputs.
	}

	[Fact]
	public void ExternalInputCarriesScriptPubKey()
	{
		var txInput = new TrezorTxInput
		{
			PrevHash = new byte[32],
			PrevIndex = 0,
			ScriptType = TrezorInputScriptType.External,
			Amount = 1000,
			ScriptPubKey = [0x51, 0x20],
		};

		var wrapper = ProtoReader.ReadAllFields(txInput.ToTxAckInput().Payload)[1][0].Bytes;
		var fields = ProtoReader.ReadAllFields(ProtoReader.ReadAllFields(wrapper)[2][0].Bytes);

		Assert.Equal(2UL, fields[6][0].VarInt); // EXTERNAL
		Assert.Equal(new byte[] { 0x51, 0x20 }, fields[19][0].Bytes);
		Assert.False(fields.ContainsKey(1)); // No address_n.
	}

	[Fact]
	public void TxRequestParsing()
	{
		// TxRequest { request_type = TXOUTPUT, details = { request_index = 7 }, serialized = { signature_index = 2, signature = 0xCAFE } }
		byte[] payload = new ProtoWriter()
			.WriteVarIntField(1, 1)
			.WriteMessageField(2, new ProtoWriter().WriteVarIntField(1, 7))
			.WriteMessageField(3, new ProtoWriter().WriteVarIntField(1, 2).WriteBytesField(2, [0xCA, 0xFE]))
			.ToBytes();

		var request = TrezorTxRequest.FromMessage(new TrezorMessage(TrezorMessageType.TxRequest, payload));

		Assert.Equal(TrezorTxRequestType.TxOutput, request.RequestType);
		Assert.Equal(7, request.RequestIndex);
		Assert.Equal(2, request.SignatureIndex);
		Assert.Equal(new byte[] { 0xCA, 0xFE }, request.Signature);
	}

	[Theory]
	[InlineData("10025'/0'/0'/1'/0/5", false, 5)] // SLIP-25 external.
	[InlineData("10025'/0'/0'/1'/1/7", true, 7)]  // SLIP-25 internal (coinjoin/change).
	[InlineData("84'/0'/0'/1/3", true, 3)]        // Standard segwit internal still works.
	[InlineData("86'/0'/0'/0/2", false, 2)]       // Standard taproot external still works.
	public void ChangeAndIndexReadFromPathEnd(string keyPath, bool expectedInternal, int expectedIndex)
	{
		using var key = new Key();
		var hdPubKey = new HdPubKey(key.PubKey, KeyPath.Parse(keyPath), LabelsArray.Empty, KeyState.Clean);

		// SLIP-25 paths are 6 levels deep, so a fixed index would read the wrong element and mislabel the key.
		Assert.Equal(expectedInternal, hdPubKey.IsInternal);
		Assert.Equal(expectedIndex, hdPubKey.Index);
	}

	[Theory]
	[InlineData("84'/0'/0'/0/0", ScriptPubKeyType.Segwit)]        // Standard segwit unchanged.
	[InlineData("86'/0'/0'/0/0", ScriptPubKeyType.TaprootBIP86)]  // Standard taproot unchanged.
	[InlineData("10025'/0'/0'/1'/0/0", ScriptPubKeyType.TaprootBIP86)] // SLIP-25 is taproot.
	[InlineData("352'/0'/0'/1'/0", ScriptPubKeyType.Segwit)]      // Silent payment path keeps its previous (default) classification.
	[InlineData("999'/0'/0'/0/0", ScriptPubKeyType.Segwit)]       // Unknown purpose keeps the segwit default.
	public void ScriptTypeFromPurpose(string keyPath, ScriptPubKeyType expected)
	{
		// The purpose must be read from the real index, not the low byte of its serialization (10025 & 0xFF == 41).
		Assert.Equal(expected, KeyPath.Parse(keyPath).GetScriptTypeFromKeyPath());
	}

	[Fact]
	public void EnableCoinJoinAddsSlip25TaprootAccount()
	{
		var mnemonic = new Mnemonic("all all all all all all all all all all all all");
		var masterExtKey = mnemonic.DeriveExtKey();

		// A plain segwit-only Trezor watch-only wallet (no taproot account), like one imported without coinjoin.
		var keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(
			masterExtKey.Neuter().PubKey.GetHDFingerPrint(),
			masterExtKey.Derive(new KeyPath("84'/0'/0'")).Neuter(),
			taprootExtPubKey: null,
			null,
			null,
			Network.Main);
		Assert.False(keyManager.IsTrezorCoinJoinWallet());
		Assert.Null(keyManager.TaprootExtPubKey);

		var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main);
		var coinJoinExtPubKey = masterExtKey.Derive(coinJoinAccountKeyPath).Neuter();
		keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey);

		Assert.True(keyManager.IsTrezorCoinJoinWallet());
		Assert.Equal(coinJoinExtPubKey, keyManager.TaprootExtPubKey);
		Assert.Equal(coinJoinAccountKeyPath, keyManager.TaprootAccountKeyPath);

		// The coinjoin account provides internal (change/output) keys flagged internal, which coinjoin needs.
		Assert.NotEmpty(keyManager.GetKeys(k => k.IsInternal && k.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86));

		// A wallet that already has a taproot account must not be silently converted (would orphan its coins).
		Assert.Throws<InvalidOperationException>(() => keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, coinJoinExtPubKey));
	}

	[Fact]
	public void NormalWalletIsUnaffectedByCoinJoinChanges()
	{
		// A hot wallet must not be treated as a Trezor coinjoin wallet and its change keys must still be segwit/taproot.
		var keyManager = KeyManager.CreateNew(out _, "", Network.Main);
		Assert.False(keyManager.IsTrezorCoinJoinWallet());

		var changeKey = keyManager.GetNextChangeKey();
		Assert.True(changeKey.IsInternal);
		Assert.False(changeKey.FullKeyPath.IsSlip25KeyPath());

		var provider = new WalletWasabi.WabiSabi.Client.InternalDestinationProvider(keyManager);
		Assert.Contains(NBitcoin.ScriptType.P2WPKH, provider.SupportedScriptTypes);
	}

	[Fact]
	public void Slip25AccountDetection()
	{
		Assert.Equal(new KeyPath("10025'/0'/0'/1'"), TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main));
		Assert.Equal(new KeyPath("10025'/1'/0'/1'"), TrezorDevice.GetCoinJoinAccountKeyPath(Network.TestNet));
		Assert.Equal(new KeyPath("10025'/1'/0'/1'"), TrezorDevice.GetCoinJoinAccountKeyPath(Network.RegTest));

		Assert.True(TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main).IsSlip25KeyPath());
		Assert.False(new KeyPath("86'/0'/0'").IsSlip25KeyPath());

		var mnemonic = new Mnemonic("all all all all all all all all all all all all");
		var masterExtKey = mnemonic.DeriveExtKey();
		var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main);

		var trezorCoinJoinWallet = KeyManager.CreateNewHardwareWalletWatchOnly(
			masterExtKey.Neuter().PubKey.GetHDFingerPrint(),
			masterExtKey.Derive(new KeyPath("84'/0'/0'")).Neuter(),
			masterExtKey.Derive(coinJoinAccountKeyPath).Neuter(),
			null,
			null,
			Network.Main,
			taprootAccountKeyPath: coinJoinAccountKeyPath);
		Assert.True(trezorCoinJoinWallet.IsTrezorCoinJoinWallet());

		var plainHardwareWallet = KeyManager.CreateNewHardwareWalletWatchOnly(
			masterExtKey.Neuter().PubKey.GetHDFingerPrint(),
			masterExtKey.Derive(new KeyPath("84'/0'/0'")).Neuter(),
			null,
			null,
			null,
			Network.Main);
		Assert.False(plainHardwareWallet.IsTrezorCoinJoinWallet());
	}
}
