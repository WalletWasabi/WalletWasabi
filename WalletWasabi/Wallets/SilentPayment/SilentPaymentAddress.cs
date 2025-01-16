using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Wallets.SilentPayment;

public record SilentPaymentAddress(int Version, ECPubKey ScanKey, ECPubKey SpendKey)
{
	public SilentPaymentAddress(int version, PubKey scanKey, PubKey spendKey)
		: this(version, ECPubKey.Create(scanKey.ToBytes()), ECPubKey.Create(spendKey.ToBytes()))
	{}

	public static SilentPaymentAddress Parse(string encoded, Network network)
	{
		var spEncoder = network.GetSilentPaymentBech32Encoder();
		var result = spEncoder.DecodeDataRaw(encoded, out _);
		var version = result[0];
		if (version != 0)
		{
			throw new FormatException("Unexpected version of silent payment code");
		}

		if (result.Length != 107)
		{
			throw new FormatException("Wrong length");
		}

		var data = spEncoder.FromBase32(result[1..]);
		return new SilentPaymentAddress(
			Version: 0,
			ScanKey: ECPubKey.Create(data[..33]),
			SpendKey: ECPubKey.Create(data[33..]));
	}

	public string ToWip(Network network)
	{
		var spEncoder = network.GetSilentPaymentBech32Encoder();
		var data = new byte[66];
		Buffer.BlockCopy(ScanKey.ToBytes(), 0, data, 0, 33);
		Buffer.BlockCopy(SpendKey.ToBytes(),0, data,33, 33);
		var base32 = spEncoder.ToBase32(data);
		var buffer = new byte[base32.Length + 1];
		buffer[0] = (byte) Version;
		Buffer.BlockCopy(base32, 0, buffer, 1, base32.Length);
		return spEncoder.EncodeRaw(buffer, Bech32EncodingType.BECH32M);
	}

	public SilentPaymentAddress DeriveAddressForLabel(ECPubKey mG)
	{
		var bm = (SpendKey.Q.ToGroupElementJacobian() + mG.Q).ToGroupElement();
		return this with {SpendKey = new ECPubKey(bm, null)};
	}
}
