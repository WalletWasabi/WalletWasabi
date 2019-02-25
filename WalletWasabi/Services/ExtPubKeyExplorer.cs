using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Services
{
	public class ScriptPubKeyProvider
	{
		public	const int ChunkSize = 1_000;
		private const int ChunkCount = 3;
		private	const int DefaultBufferSize = ChunkCount * ChunkSize;
		private ExtPubKey _extPubKey;
		private byte[][] _buffer; 
		private int _availableScriptCount = 0;
		private IEnumerator<byte[]> _generator;

		public int ScriptBufferSize => _buffer.Length;

		public ScriptPubKeyProvider(ExtPubKey extPubKey)
		{
			_extPubKey = extPubKey;
			_buffer = new byte[DefaultBufferSize][];
			_generator = GenerateNext().GetEnumerator();
		}

		public ArraySegment<byte[]> GetScripts(int offset, int count=100)
		{
			if(count > ChunkSize)
				throw new ArgumentNullException(nameof(count));

			offset %= DefaultBufferSize;
			_availableScriptCount %= DefaultBufferSize;
			if(offset + count > DefaultBufferSize) 
				throw new ArgumentNullException(nameof(count));
			
			while(offset + count > _availableScriptCount)
			{
				_generator.MoveNext();
				_buffer[_availableScriptCount++] = _generator.Current;
			}
			return new ArraySegment<byte[]>(_buffer, offset, count);
		}

		private IEnumerable<byte[]> GenerateNext()
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

	public class ExtPubKeyExplorer
	{
		private readonly ScriptPubKeyProvider _scriptProvider;
		private readonly FilterModel[] _filters;

		public ExtPubKeyExplorer(ScriptPubKeyProvider scriptProvider, IEnumerable<FilterModel> filters)
		{
			_scriptProvider = scriptProvider;
			_filters = filters.Where(x=>x.Filter != null).ToArray();
			if(_filters.Length == 0)
				throw new ArgumentException(nameof(filters), "There is not filter to match.");
		}

		private (int, ArraySegment<byte[]>) FindLastMatchingChunk()
		{
			var offset = 0;
			var lastOffset = 0;
			var scriptChunk = _scriptProvider.GetScripts(offset, 1_000);
			var lastMatchingChunk = scriptChunk;
			while(true)
			{
				foreach (var filterModel in _filters)
				{
					var matching = filterModel.Filter.MatchAny(scriptChunk, filterModel.FilterKey);
					if(!matching)
						return (lastOffset, lastMatchingChunk);
				}
				lastMatchingChunk = scriptChunk;
				lastOffset = offset;
				offset += 1_000;
				scriptChunk = _scriptProvider.GetScripts(offset, 1_000);
			}
		}

		public int GetIndexFirstUnusedKey()
		{
			var (absoluteOffset, lastMatchingCunck) = FindLastMatchingChunk();

			var begin= lastMatchingCunck.Offset;
			var size = lastMatchingCunck.Count;

			while(size > 0)
			{
				var mid = (size+1) / 2;
				var lh = _scriptProvider.GetScripts(begin, mid);

				var flh = true;
				var frh = true;
				foreach (var filterModel in _filters)
				{
					flh = filterModel.Filter.MatchAny(lh, filterModel.FilterKey);
					if (flh) break;
				}
				if(!flh) break;

				if(size-mid > 0)
				{
					var rh = _scriptProvider.GetScripts(begin + mid, size-mid);
					foreach (var filterModel in _filters)
					{
						frh = filterModel.Filter.MatchAny(rh, filterModel.FilterKey);
						if (frh) break;
					}
				}

				if( flh && frh) begin += mid; 
				size = mid;
			}

			var relativeOffset = begin + (((begin % ScriptPubKeyProvider.ChunkSize) != 0) ? 1 : 0);
			return absoluteOffset + relativeOffset;
		}
	}
}
