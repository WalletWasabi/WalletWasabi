using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.Crypto;

public record Bip322Signature : IBitcoinSerializable
{
	private Script _scriptSig = Script.Empty;

	public Bip322Signature(Script scriptSig, WitScript witness)
	{
		_scriptSig = scriptSig;
		Witness = witness;
	}

	public Bip322Signature()
	{
	}

	public Script ScriptSig => _scriptSig;

	public WitScript Witness { get; private set; } = WitScript.Empty;

	public void ReadWrite(BitcoinStream bitcoinStream)
	{
		bitcoinStream.ReadWrite(ref _scriptSig);
		if (bitcoinStream.Serializing)
		{
			Witness.WriteToStream(bitcoinStream);
		}
		else
		{
			Witness = WitScript.Load(bitcoinStream);
		}
	}

	public static Bip322Signature FromBytes(byte[] bip322SignatureBytes) =>
		NBitcoinExtensions.FromBytes<Bip322Signature>(bip322SignatureBytes);

	public bool Verify(uint256 hash, Script scriptPubKey) =>
		scriptPubKey.TryGetScriptType() switch
		{
			ScriptType.P2WPKH => VerifyP2wpkh(hash, scriptPubKey),
			ScriptType.Taproot => VerifyP2tr(hash, scriptPubKey),
			_ => throw new NotImplementedException("Only P2WPKH and P2TR scripts are supported.")
		};

	public static Bip322Signature Generate(Key key, uint256 hash, ScriptPubKeyType scriptPubKeyType) =>
		scriptPubKeyType switch
		{
			ScriptPubKeyType.Segwit => new Bip322Signature(
				Script.Empty,
				PayToWitPubKeyHashTemplate.Instance.GenerateWitScript(
					key.Sign(hash, new SigningOptions(SigHash.All, useLowR: false)),
					key.PubKey)),
			ScriptPubKeyType.TaprootBIP86 => new Bip322Signature(
				Script.Empty,
				PayToTaprootTemplate.Instance.GenerateWitScript(
					key.SignTaprootKeySpend(hash, null, uint256.Zero, TaprootSigHash.Default))),
			_ => throw new NotImplementedException("Only P2WPKH and P2TR scripts are supported.")
		};

	private bool VerifyP2wpkh(uint256 hash, Script scriptPubKey)
	{
		if (ScriptSig != Script.Empty)
		{
			return false;
		}

		try
		{
			if (PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(Witness) is not { } witnessParameters)
			{
				return false;
			}

			if (witnessParameters.PublicKey.GetScriptPubKey(ScriptPubKeyType.Segwit) != scriptPubKey)
			{
				return false;
			}

			if (witnessParameters.TransactionSignature is not { } transactionSignature)
			{
				return false;
			}

			return witnessParameters.PublicKey.Verify(hash, transactionSignature.Signature);
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private bool VerifyP2tr(uint256 hash, Script scriptPubKey)
	{
		if (ScriptSig != Script.Empty)
		{
			return false;
		}

		try
		{
			if (PayToTaprootTemplate.Instance.ExtractWitScriptParameters(Witness) is not { } witnessParameters)
			{
				return false;
			}

			if (PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) is not { } publicKey)
			{
				return false;
			}

			return publicKey.VerifySignature(hash, witnessParameters.TransactionSignature.SchnorrSignature);
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
