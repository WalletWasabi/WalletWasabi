using NBitcoin;
using NBitcoin.DataEncoders;
using System.Text;
using System.Threading;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Backend.Models;

public class FilterModel
{
	private Lazy<GolombRiceFilter> _filter;

	public FilterModel(SmartHeader header, GolombRiceFilter filter)
	{
		Header = header;
		_filter = new Lazy<GolombRiceFilter>(filter);
	}

	public FilterModel(SmartHeader header, Lazy<GolombRiceFilter> filter)
	{
		Header = header;
		_filter = filter;
	}

	public SmartHeader Header { get; }

	public GolombRiceFilter Filter => _filter.Value;

	// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
	// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
	// is constructed.This ensures the key is deterministic while still varying from block to block.
	public byte[] FilterKey => Header.BlockHash.ToBytes()[..16];

	public static FilterModel FromLine(string line)
	{
		try
		{
			// Splitting lines using Split(':') requires allocations. Working with .NET spans is faster.
			ReadOnlySpan<char> span = line;

			int m1 = line.IndexOf(':', 0);
			int m2 = line.IndexOf(':', m1 + 1);
			int m3 = line.IndexOf(':', m2 + 1);
			int m4 = line.IndexOf(':', m3 + 1);

			if (m1 == -1 || m2 == -1 || m3 == -1 || m4 == -1)
			{
				throw new ArgumentException(line, nameof(line));
			}

			uint blockHeight = uint.Parse(span[0..m1]);
			uint256 blockHash = new(Convert.FromHexString(span[(m1 + 1)..m2]), lendian: false);
			byte[] filterData = Convert.FromHexString(span[(m2 + 1)..m3]);

			Lazy<GolombRiceFilter> filter = new(() => new GolombRiceFilter(filterData, 20, 1 << 20), LazyThreadSafetyMode.ExecutionAndPublication);
			uint256 prevBlockHash = new(Convert.FromHexString(span[(m3 + 1)..m4]), lendian: false);
			long blockTime = long.Parse(span[(m4 + 1)..]);

			return new FilterModel(new SmartHeader(blockHash, prevBlockHash, blockHeight, blockTime), filter);
		}
		catch (FormatException ex)
		{
			throw new FormatException("An error occurred while parsing the block filters.", ex);
		}
	}

	public string ToLine()
	{
		StringBuilder builder = new(capacity: 160);
		builder.Append(Header.Height);
		builder.Append(':');
		builder.Append(Header.BlockHash);
		builder.Append(':');
		builder.Append(Filter);
		builder.Append(':');
		builder.Append(Header.PrevHash);
		builder.Append(':');
		builder.Append(Header.EpochBlockTime);

		return builder.ToString();
	}
}
