using NBitcoin;
using NBitcoin.DataEncoders;
using System.Text;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend.Models;

public class FilterModel
{
	public FilterModel(SmartHeader header, GolombRiceFilter filter)
	{
		Header = Guard.NotNull(nameof(header), header);
		Filter = Guard.NotNull(nameof(filter), filter);
	}

	public SmartHeader Header { get; }

	public GolombRiceFilter Filter { get; }

	// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
	// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
	// is constructed.This ensures the key is deterministic while still varying from block to block.
	public byte[] FilterKey => Header.BlockHash.ToBytes()[..16];

	public static FilterModel FromLine(string line)
	{
		Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
		string[] parts = line.Split(':');

		if (parts.Length < 5)
		{
			throw new ArgumentException(line, nameof(line));
		}

		var blockHeight = uint.Parse(parts[0]);
		var blockHash = uint256.Parse(parts[1]);
		var filterData = Encoders.Hex.DecodeData(parts[2]);
		GolombRiceFilter filter = new(filterData, 20, 1 << 20);
		var prevBlockHash = uint256.Parse(parts[3]);
		var blockTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[4]));

		return new FilterModel(new SmartHeader(blockHash, prevBlockHash, blockHeight, blockTime), filter);
	}

	public string ToLine()
	{
		var builder = new StringBuilder();
		builder.Append(Header.Height);
		builder.Append(':');
		builder.Append(Header.BlockHash);
		builder.Append(':');
		builder.Append(Filter);
		builder.Append(':');
		builder.Append(Header.PrevHash);
		builder.Append(':');
		builder.Append(Header.BlockTime.ToUnixTimeSeconds());

		return builder.ToString();
	}
}
