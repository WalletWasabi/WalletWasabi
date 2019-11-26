using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
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
			if (Filter is { }) // bech found here
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
			if (Filter is { }) // bech found here
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

			GolombRiceFilter filter;
			if (parts.Length <= 1)
			{
				throw new ArgumentException(nameof(line), line);
			}
			else if (parts.Length == 2) // no bech here
			{
				filter = null;
			}
			else
			{
				var data = Encoders.Hex.DecodeData(parts[2]);
				filter = new GolombRiceFilter(data, 20, 1 << 20);
			}

			if (Height.TryParse(parts[0], out Height blockHeight))
			{
				return new FilterModel
				{
					BlockHeight = blockHeight,
					BlockHash = new uint256(parts[1]),
					Filter = filter
				};
			}
			else
			{
				throw new FormatException($"Could not parse {nameof(Height)}.");
			}
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

		public byte[] ToBytes()
		{
			byte[] blockHashBytes = BlockHash.ToBytes();
			byte[] filterBytes = Filter is null ? Array.Empty<byte>() : Filter.ToBytes();
			byte[] filterLengthBytes = BitConverter.GetBytes(filterBytes.Length);
			byte[] buffer = new byte[blockHashBytes.Length + filterLengthBytes.Length + filterBytes.Length];
			Buffer.BlockCopy(blockHashBytes, 0, buffer, 0, blockHashBytes.Length);
			Buffer.BlockCopy(filterLengthBytes, 0, buffer, blockHashBytes.Length, filterLengthBytes.Length);
			Buffer.BlockCopy(filterBytes, 0, buffer, blockHashBytes.Length + filterLengthBytes.Length, filterBytes.Length);
			return buffer;
		}

		public static FilterModel FromStream(Stream stream, Height height)
		{
			uint256 blockHash = new uint256(stream.ReadBytes(32));
			int filterSize = BitConverter.ToInt32(stream.ReadBytes(4));
			byte[] data = stream.ReadBytes(filterSize);
			GolombRiceFilter filter = filterSize > 0 ? new GolombRiceFilter(data, 20, 1 << 20) : null;

			return new FilterModel
			{
				BlockHeight = Guard.NotNull(nameof(height), height),
				BlockHash = blockHash,
				Filter = filter
			};
		}
	}
}
