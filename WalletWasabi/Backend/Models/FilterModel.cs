using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models
{
	public class FilterModel
	{
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height BlockHeight { get; set; }

		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 BlockHash { get; set; }

		[JsonConverter(typeof(GolombRiceFilterJsonConverter))]
		public GolombRiceFilter Filter { get; set; }

		// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
		// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
		// is constructed.This ensures the key is deterministic while still varying from block to block.
		public byte[] FilterKey => BlockHash.ToBytes().Take(16).ToArray();

		public string ToHeightlessLine()
		{
			var builder = new StringBuilder();
			builder.Append(BlockHash);
			if (!(Filter is null)) // bech found here
			{
				builder.Append(":");
				builder.Append(Filter);
			}

			return builder.ToString();
		}

		public string ToFullLine()
		{
			var builder = new StringBuilder();
			builder.Append(BlockHeight.ToString());
			builder.Append(":");
			builder.Append(BlockHash);
			if (Filter != null) // bech found here
			{
				builder.Append(":");
				builder.Append(Filter);
			}

			return builder.ToString();
		}

		public static FilterModel FromFullLine(string line)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			string[] parts = line.Split(':');

			if (parts.Length <= 1)
			{
				throw new ArgumentException(nameof(line), line);
			}
			else if (parts.Length == 2) // no bech here
			{
				return new FilterModel
				{
					BlockHeight = new Height(parts[0]),
					BlockHash = new uint256(parts[1]),
					Filter = null
				};
			}

			var data = Encoders.Hex.DecodeData(parts[2]);
			var filter = new GolombRiceFilter(data, 20, 1 << 20);

			return new FilterModel
			{
				BlockHeight = new Height(parts[0]),
				BlockHash = new uint256(parts[1]),
				Filter = filter
			};
		}

		public byte[] ToBinary()
		{
			var blockHashBytes = BlockHash.ToBytes();
			var filterBytes = Filter != null ? Filter.ToBytes() : new byte[0];
			var filterLengthBytes = BitConverter.GetBytes(filterBytes.Length);
			var buffer = new byte[blockHashBytes.Length + filterLengthBytes.Length + filterBytes.Length];
			Buffer.BlockCopy(blockHashBytes, 0, buffer, 0, blockHashBytes.Length);
			Buffer.BlockCopy(filterLengthBytes, 0, buffer, blockHashBytes.Length, filterLengthBytes.Length);
			Buffer.BlockCopy(filterBytes, 0, buffer, blockHashBytes.Length + filterLengthBytes.Length, filterBytes.Length);
			return buffer;
		}

		public static FilterModel FromHeightlessLine(string line, Height height)
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

			var data = Encoders.Hex.DecodeData(parts[1]);
			var filter = new GolombRiceFilter(data, 20, 1 << 20);

			return new FilterModel
			{
				BlockHeight = Guard.NotNull(nameof(height), height),
				BlockHash = new uint256(parts[0]),
				Filter = filter
			};
		}

		public static FilterModel FromStream(Stream stream, Height height)
		{
			var blockHash = new uint256(stream.ReadBytes(32));
			var filterSize = BitConverter.ToInt32(stream.ReadBytes(4));
			var data = stream.ReadBytes(filterSize);
			var filter = filterSize > 0 ? new GolombRiceFilter(data, 20, 1 << 20) : null;

			return new FilterModel
			{
				BlockHeight = Guard.NotNull(nameof(height), height),
				BlockHash = blockHash,
				Filter = filter
			};
		}
	}
}
