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

		public bool Verify(uint256 hash, Script scriptPubKey)
		{
			if (scriptPubKey.IsScriptType(ScriptType.P2WPKH))
			{
				if (ScriptSig.Length != 0)
				{
					return false;
				}

				var witnessParameters = PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(Witness);
				if (witnessParameters is null)
				{
					return false;
				}

				if (witnessParameters.PublicKey.GetScriptPubKey(ScriptPubKeyType.Segwit) != scriptPubKey)
				{
					return false;
				}
				
				// if (witnessParameters.TransactionSignature.SigHash != SigHash.All)
				//	 return false;

				return witnessParameters.PublicKey.Verify(hash, witnessParameters.TransactionSignature.Signature);
			}

			throw new NotImplementedException();
		}

		public static Bip322Signature FromBytes(byte[] bip322SignatureBytes) =>
			NBitcoinExtensions.FromBytes<Bip322Signature>(bip322SignatureBytes);

		public static Bip322Signature Generate(Key key, uint256 hash, ScriptPubKeyType scriptPubKeyType)
		{
			if (scriptPubKeyType == ScriptPubKeyType.Segwit)
			{
				var signature = key.Sign(hash, SigHash.All, false);
				var witness = PayToWitPubKeyHashTemplate.Instance.GenerateWitScript(signature, key.PubKey);
				return new Bip322Signature(Script.Empty, witness);
			}

			throw new NotImplementedException(); // TODO: Should we implement it?
		}
	}
}
