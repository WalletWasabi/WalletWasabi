using System;
using NBitcoin;

namespace WalletWasabi.Crypto
{
	public class Bip322Signature : IBitcoinSerializable
	{
		private Script scriptSig = Script.Empty;
		private WitScript witness = WitScript.Empty; 

		public Bip322Signature(Script scriptSig, WitScript witness)
		{
			this.scriptSig = scriptSig;
			this.witness = witness; 
		}

		public Bip322Signature()
		{
		}

		public Script ScriptSig => scriptSig;

		public WitScript Witness => witness;

		public void ReadWrite(BitcoinStream bitcoinStream)
		{
			bitcoinStream.ReadWrite(ref scriptSig);
			if (bitcoinStream.Serializing)
			{
				witness.WriteToStream(bitcoinStream);
			}
			else
			{
				witness = WitScript.Load(bitcoinStream);
			}
		}

		public static Bip322Signature FromBytes(byte[] bip322SignatureBytes) =>
			NBitcoinExtensions.FromBytes<Bip322Signature>(bip322SignatureBytes);

		public bool Verify(uint256 hash, Script scriptPubKey) =>
			scriptPubKey.IsScriptType(ScriptType.P2WPKH) switch
			{
				true => VerifySegwit(hash, scriptPubKey),
				false => throw new NotImplementedException("Only P2WPKH scripts are supported.")
			};

		public static Bip322Signature Generate(Key key, uint256 hash, ScriptPubKeyType scriptPubKeyType) =>
			scriptPubKeyType switch
			{
				ScriptPubKeyType.Segwit => new Bip322Signature(
					Script.Empty, 
					PayToWitPubKeyHashTemplate.Instance.GenerateWitScript(
						key.Sign(hash, SigHash.All, false), 
						key.PubKey)),
				_ => throw new NotImplementedException("Only P2WPKH scripts are supported.")
			};

		private bool VerifySegwit(uint256 hash, Script scriptPubKey)
		{
			if (ScriptSig != Script.Empty)
			{
				return false;
			}

			if (PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(Witness) is not { } witnessParameters)
			{
				return false;
			}

			if (witnessParameters.PublicKey.GetScriptPubKey(ScriptPubKeyType.Segwit) != scriptPubKey)
			{
				return false;
			}

			return witnessParameters.PublicKey.Verify(hash, witnessParameters.TransactionSignature.Signature);
		}
	}
}
