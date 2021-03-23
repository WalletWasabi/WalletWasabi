using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets
{
	public class AddressDeriver
	{
		public KeyPath AccountKeyPath { get; }
		public bool IsInternal { get; }
		public ExtPubKey ExtPubKey { get; }
		private int AdditionalGap { get; set; }
		private Dictionary<Script, int> Script { get; } = new Dictionary<Script, int>();

		public AddressDeriver(ExtPubKey extPubKey, KeyPath accountKeyPath, bool isInternal, int lastIndex, int additionalGap)
		{
			ExtPubKey = extPubKey;
			AccountKeyPath = accountKeyPath;
			AdditionalGap = additionalGap;
			IsInternal = isInternal;
			EnsureAdditionalGap(lastIndex);
		}

		public bool IsMyScript(Script scriptPubKey)
		{
			if (Script.TryGetValue(scriptPubKey, out var index))
			{
				EnsureAdditionalGap(index);
				return true;
			}

			return false;
		}

		private void EnsureAdditionalGap(int fromIndex)
		{
			while (Script.Count < (fromIndex + AdditionalGap))
			{
				int nextIndex = Script.Count == 0 ? 0 : Script.Last().Value + 1;
				Script.Add(ExtPubKey.Derive(IsInternal ? 1 : 0, false).Derive(nextIndex, false).PubKey.WitHash.ScriptPubKey, nextIndex);
			}
		}
	}
}
