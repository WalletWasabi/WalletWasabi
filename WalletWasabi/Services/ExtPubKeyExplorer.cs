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
		private readonly FilterModel[] _filters;
		
		private ExtPubKey _extPubKey;
		private IEnumerator<byte[]> _generator;

		public ExtPubKeyExplorer(ExtPubKey extPubKey, IEnumerable<FilterModel> filters)
		{
			_extPubKey = extPubKey;

			_generator = DerivateNext().GetEnumerator();
			_filters = filters.Where(x=>x.Filter != null).ToArray();
			if(_filters.Length == 0)
				throw new ArgumentException(nameof(filters), "There is no filter to match.");
		}

		public IEnumerable<byte[]> UnusedKeys()
		{
			while(true)
			{
				_generator.MoveNext();
				var cur = _generator.Current;
				if(!Match(cur)) 
					yield return cur;
			}
		}

		private bool Match(byte[] script)
		{
			var used = false;
			foreach (var filterModel in _filters)
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
			while(true)
			{
				var pubKey = _extPubKey.Derive(i++).PubKey;
				var bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();
				yield return bytes;
			}
		}
	}
}
