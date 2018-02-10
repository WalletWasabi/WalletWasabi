using System.Collections.Generic;
using NBitcoin;

namespace MagicalCryptoWallet.Backend.Gcs
{
	public class Builder
	{
		private const int P = 20;

		public GCSFilter Build(Block block)
		{
			var key = block.GetHash().ToBytes();

			var buffer = new List<byte[]>();
			buffer.Add(key);

			foreach (var tx in block.Transactions)
			{
				foreach (var txOutput in tx.Outputs)
				{
					var witDestination = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters(txOutput.ScriptPubKey);
					var isValidPayToWitness = witDestination != null;

					if (isValidPayToWitness)
					{
						var scriptPubKeyBytes = txOutput.ScriptPubKey.ToBytes();
						buffer.Add(scriptPubKeyBytes);
					}
				}
			}

			return GCSFilter.Build(key, P, buffer);
		}
	}
}
