using NBitcoin;
using NBitcoin.DataEncoders;
using System.Text;
using System.Threading;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Backend.Models;

public class FilterModel
{
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

	private Lazy<GolombRiceFilter> _filter;
	public GolombRiceFilter Filter => _filter.Value;

	// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
	// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
	// is constructed.This ensures the key is deterministic while still varying from block to block.
	public byte[] FilterKey => Header.BlockHash.ToBytes()[..16];

	public static FilterModel FromLine(string line)
	{
		string[] parts = line.Split(':');

		if (parts.Length < 5)
		{
			throw new ArgumentException(line, nameof(line));
		}

		uint blockHeight = uint.Parse(parts[0]);
		uint256 blockHash = uint256.Parse(parts[1]);
		byte[] filterData = Encoders.Hex.DecodeData(parts[2]);
		Lazy<GolombRiceFilter> filter = new(() => new GolombRiceFilter(filterData, 20, 1 << 20), LazyThreadSafetyMode.ExecutionAndPublication);
		uint256 prevBlockHash = uint256.Parse(parts[3]);
		long blockTime = long.Parse(parts[4]);

		return new FilterModel(new SmartHeader(blockHash, prevBlockHash, blockHeight, blockTime), filter);
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
