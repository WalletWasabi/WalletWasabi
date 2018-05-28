using NBitcoin;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models
{
	public class FilterModel
	{
		public Height BlockHeight { get; set; }
		public uint256 BlockHash { get; set; }
		public GolombRiceFilter Filter { get; set; }

		// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
		// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
		// is constructed.This ensures the key is deterministic while still varying from block to block.
		public byte[] FilterKey => BlockHash.ToBytes().Take(16).ToArray();

		public string ToLine()
		{
			var builder = new StringBuilder();
			builder.Append(BlockHash);
			if (Filter != null) // bech found here
			{
				builder.Append(":");
				builder.Append(Filter);
			}

			return builder.ToString();
		}

		public static FilterModel FromLine(string line, Height height)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			var parts = line.Split(':');

			if (parts.Length == 1) // no bech here
			{
				return new FilterModel
				{
					BlockHeight = Guard.NotNull(nameof(height), height),
					BlockHash = new uint256(parts[0]),
					Filter = null
				};
			}
			return new FilterModel
			{
				BlockHeight = Guard.NotNull(nameof(height), height),
				BlockHash = new uint256(parts[0]),
				Filter = GolombRiceFilter.Parse(parts[1])
			};
		}
	}
}
