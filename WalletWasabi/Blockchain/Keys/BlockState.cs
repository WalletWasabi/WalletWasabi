using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockState : IComparable<BlockState>
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 BlockHash { get; }

		[JsonProperty(Order = 2)]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height BlockHeight { get; }

		[JsonProperty(Order = 3)]
		public List<int> TransactionIndices { get; }

		[JsonConstructor]
		public BlockState(uint256 blockHash, Height blockHeight, IEnumerable<int> transactionIndices)
		{
			BlockHash = blockHash;
			BlockHeight = blockHeight;
			TransactionIndices = transactionIndices?.OrderBy(x => x).ToList() ?? new List<int>();
		}

		/// <summary>
		/// Performs a comparison and return if compared values are equal, greater or less than the other one
		/// </summary>
		/// <param name="other">The blockheight to compare against.</param>
		/// <returns>0 if this an other are equal, -1 if this is less than other and 1 if this is greater than other.</returns>
		public int CompareTo(BlockState other) => BlockHeight.CompareTo(other.BlockHeight);
	}
}
