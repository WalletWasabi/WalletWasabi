using NBitcoin.DataEncoders;

namespace WalletWasabi.Wallets.SilentPayment;

public class SilentPaymentBech32Encoder : Bech32Encoder
{
	public SilentPaymentBech32Encoder(byte[] hrp) : base(hrp)
	{
		StrictLength = false;
	}

	public byte[] FromBase32(ReadOnlySpan<byte> data) =>
		ConvertBits(data, 5, 8, false);

	public byte[] ToBase32(ReadOnlySpan<byte> data) =>
		ConvertBits(data, 8, 5);
}
