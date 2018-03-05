using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Models;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
	public class FilterModel
	{
		public Height BlockHeight { get; set; }
		public uint256 BlockHash { get; set; }
		public GolombRiceFilter Filter { get; set; }

		public string ToLine()
		{
			var builder = new StringBuilder();
			builder.Append(BlockHash);
			if(Filter != null) // bech found here
			{
				builder.Append(":");
				builder.Append(Filter.N);
				builder.Append(":");
				builder.Append(Filter.Data.Length);
				builder.Append(":");
				builder.Append(ByteHelpers.ToHex(Filter.Data.ToByteArray()));
			}

			return builder.ToString();
		}

		public static FilterModel FromLine(string line, Height height)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			var parts = line.Split(':');

			if(parts.Length == 1) // no bech here
			{
				return new FilterModel
				{
					BlockHeight = Guard.NotNull(nameof(height), height),
					BlockHash = new uint256(parts[0]),
					Filter = null
				};
			}
			else
			{
				var n = int.Parse(parts[1]);
				var fba = new FastBitArray(ByteHelpers.FromHex(parts[3]));
				fba.Length = int.Parse(parts[2]);

				var filter = new GolombRiceFilter(fba, n);

				return new FilterModel
				{
					BlockHeight = Guard.NotNull(nameof(height), height),
					BlockHash = new uint256(parts[0]),
					Filter = filter
				};
			}			
		}
	}
}
