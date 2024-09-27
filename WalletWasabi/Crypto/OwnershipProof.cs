using System.Text;
using NBitcoin;
using NBitcoin.BIP322;

namespace WalletWasabi.Crypto;

public record OwnershipProof : IBitcoinSerializable
{
	public OwnershipProof() { }

	public OwnershipProof(BIP322Signature bip322Signature)
	{
		BIP322Signature = bip322Signature;
	}
	public BIP322Signature? BIP322Signature { get; private set; }
	public void ReadWrite(BitcoinStream stream)
	{
		var bytes = BIP322Signature?.ToBytes() ?? [];
		stream.ReadWrite(bytes);
		if(stream.Serializing)
		{
			BIP322Signature = bytes.Length == 0 ? null : BIP322Signature.TryCreate(bytes, Network.Main, out var sig) ? sig : throw new InvalidOperationException("Invalid BIP322 signature.");
		}

	}

	public static OwnershipProof FromBytes(byte[] ownershipProofBytes)
	{
		if (!BIP322Signature.TryCreate(ownershipProofBytes, Network.Main, out var sig))
		{
			throw new InvalidOperationException("Invalid BIP322 signature.");

		}

		return new OwnershipProof(sig);
	}

	public static OwnershipProof Generate(Key signingKey, IDestination address, byte[] data)
	{
		var str = Encoding.UTF8.GetString(data);
		return new OwnershipProof(signingKey.SignBIP322(address.ScriptPubKey.GetDestinationAddress(Network.Main)!, str, SignatureType.Full));
	}
	public static OwnershipProof Generate(Key signingKey, IDestination address, CoinJoinInputCommitmentData commitmentData)
	{
		return Generate(signingKey, address, commitmentData.ToBytes());
	}
	public static OwnershipProof Generate(Key signingKey, ScriptPubKeyType scriptPubKeyType, CoinJoinInputCommitmentData commitmentData)
	{
		return Generate(signingKey, signingKey.GetAddress(scriptPubKeyType, Network.Main), commitmentData.ToBytes());
	}
	public bool Verify(CoinJoinInputCommitmentData data, Coin coin)
	{
		return  BIP322Signature is not null && coin.TxOut.ScriptPubKey.GetDestinationAddress(Network.Main)!.VerifyBIP322(Encoding.UTF8.GetString(data.ToBytes()), BIP322Signature);
	}
}
