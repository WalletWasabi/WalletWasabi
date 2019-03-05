using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Services
{
	public class ExtPubKeyExplorer
	{
		public ExtPubKey ExtPubKey { get; set; }

		public FilterModel[] Filters { get; }
		public IEnumerator<byte[]> Generator { get; set; }

		/// <summary>
		/// WARNING: ONLY CHECKS CONFIRMED AND BECH32 KEYPATHS
		/// </summary>
		public ExtPubKeyExplorer(ExtPubKey extPubKey, IEnumerable<FilterModel> filters)
		{
			ExtPubKey = extPubKey;

			Generator = DerivateNext().GetEnumerator();
			Filters = filters.Where(x => x.Filter != null).ToArray();
			if (Filters.Length == 0)
				throw new ArgumentException(nameof(filters), "There is no filter to match.");
		}

		public IEnumerable<byte[]> UnusedKeys()
		{
			while (true)
			{
				Generator.MoveNext();
				var cur = Generator.Current;
				if (!Match(cur))
					yield return cur;
			}
		}

		private bool Match(byte[] script)
		{
			var used = false;
			foreach (var filterModel in Filters)
			{
				used = filterModel.Filter.Match(script, filterModel.FilterKey);
				if (used)
					return true;
			}
			return false;
		}

		private IEnumerable<byte[]> DerivateNext()
		{
			var i = 0u;
			while (true)
			{
				var pubKey = ExtPubKey.Derive(i++).PubKey;
				var bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();
				yield return bytes;
			}
		}
	}
}
