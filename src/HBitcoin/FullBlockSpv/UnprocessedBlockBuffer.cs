using System;
using System.Collections.ObjectModel;
using System.Linq;
using HBitcoin.Models;
using NBitcoin;

namespace HBitcoin.FullBlockSpv
{
	public class UnprocessedBlockBuffer
    {
	    public const int Capacity = 50;
		private readonly ConcurrentObservableDictionary<Height, Block> _blocks = new ConcurrentObservableDictionary<Height, Block>();

		public event EventHandler HaveBlocks;
		private void OnHaveBlocks() => HaveBlocks?.Invoke(this, EventArgs.Empty);

		public void Clear() => _blocks.Clear();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="height"></param>
		/// <param name="block"></param>
		/// <returns>false if we have more than UnprocessedBlockBuffer.Capacity blocks in memory already</returns>
		public bool TryAddOrReplace(Height height, Block block)
	    {
			if (_blocks.Count > Capacity) return false;
			
		    _blocks.AddOrReplace(height, block);

			if (_blocks.Count == 1) OnHaveBlocks();
			return true;
	    }

	    public bool Full => _blocks.Count == Capacity;
	    public Height BestHeight => _blocks.Count == 0 ? Height.Unknown : _blocks.Keys.Max();

	    /// <summary>
	    /// 
	    /// </summary>
	    /// <returns>false if empty</returns>
	    public bool TryGetAndRemoveOldest(out Height height, out Block block)
	    {
		    height = Height.Unknown;
		    block = default(Block);
		    if(_blocks.Count == 0) return false;

			height = _blocks.Keys.Min();
			block = _blocks[height];
			_blocks.Remove(height);

			return true;
		}
	}
}
