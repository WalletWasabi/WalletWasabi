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
		
		// Indicates how many scripts to try each time. The default value is 21
		public	const int DefaultChunkSize = 21;
		private const int BufferCount = 10;
		private ExtPubKey _extPubKey;
		private ArraySegment<byte[]>[] _buffers = new ArraySegment<byte[]>[BufferCount];
		private int _curBufferIndex = 0;
		private IEnumerator<byte[]> _generator;
		private int _chunkSize;

		public ExtPubKeyExplorer(ExtPubKey extPubKey, IEnumerable<FilterModel> filters)
			: this(extPubKey, filters, DefaultChunkSize)
		{}

		public ExtPubKeyExplorer(ExtPubKey extPubKey, IEnumerable<FilterModel> filters, int chunkSize)
		{
			_extPubKey = extPubKey;
			_chunkSize = chunkSize;
			var mainBuffer = new byte[BufferCount * _chunkSize][];
			for(var i=0; i < BufferCount; i++)
				_buffers[i] = new ArraySegment<byte[]>(mainBuffer, i * _chunkSize, _chunkSize);

			_generator = DerivateNext().GetEnumerator();
			_filters = filters.Where(x=>x.Filter != null).ToArray();
			if(_filters.Length == 0)
				throw new ArgumentException(nameof(filters), "There is not filter to match.");
		}

		private (int, ArraySegment<byte[]>) FindLastMatchingChunk()
		{
			var offset = 0;
			var lastOffset = 0;
			var scriptChunk = GetChunkOfScripts();
			var lastMatchingChunk = scriptChunk;
			while(true)
			{
				if(!Match(scriptChunk)) 
					return (lastOffset, lastMatchingChunk);

				lastMatchingChunk = scriptChunk;
				lastOffset = offset;
				offset += _chunkSize;
				scriptChunk = GetChunkOfScripts();
			}
		}

		public int GetIndexFirstUnusedKey()
		{
			var (absoluteOffset, lastMatchingCunck) = FindLastMatchingChunk();

			var arr = lastMatchingCunck.Array;
			var begin= lastMatchingCunck.Offset;
			var size = lastMatchingCunck.Count;

			while(size > 0)
			{
				var mid = (size+1) / 2;
				var rest = size-mid;

				var lh = new ArraySegment<byte[]>(arr, begin, mid);
				var rh = new ArraySegment<byte[]>(arr, begin + mid, rest);

				if(!Match(lh)) break;
				var frh = (rest > 0) ? Match(rh) : true;

				if(frh)
				{
					begin += mid; 
					size = rest;
				}
				else
				{
					size = mid;
				}
			}

			return absoluteOffset + (begin - lastMatchingCunck.Offset);
		}

		private bool Match(ArraySegment<byte[]> chunk)
		{
			var used = false;
			foreach (var filterModel in _filters)
			{
				used = filterModel.Filter.MatchAny(chunk, filterModel.FilterKey);
				if (used) 
					return true;
			}
			return false;
		}

		private ArraySegment<byte[]> GetChunkOfScripts()
		{
			var buffer = _buffers[_curBufferIndex % BufferCount];
			for(var i=0; i<_chunkSize; i++)
			{
				_generator.MoveNext();
				buffer[i] = _generator.Current;
			}
			_curBufferIndex++;
			return buffer;
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
