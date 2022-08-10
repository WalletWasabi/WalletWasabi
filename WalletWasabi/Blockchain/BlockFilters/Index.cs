using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters;

public class Index
{
	public Index(RpcPubkeyType[] filterType, Network network, string filePath)
	{
		FilterType = Guard.NotNullOrEmpty(nameof(filterType), filterType).ToArray();
		Filters = new();
		AsyncLock = new();
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);
		StartingHeight = SmartHeader.GetStartingHeader(network, filterType).Height;

		IoHelpers.EnsureContainingDirectoryExists(FilePath);

		// Testing permissions.
		using (var _ = File.Open(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
		{
		}

		if (File.Exists(FilePath))
		{
			if (network == Network.RegTest)
			{
				File.Delete(FilePath); // RegTest is not a global ledger, better to delete it.
			}
			else
			{
				foreach (var line in File.ReadAllLines(FilePath))
				{
					var filter = FilterModel.FromLine(line);
					Filters.Add(filter);
				}
			}
		}
	}

	public static byte[][] DummyScript { get; } = new byte[][] { ByteHelpers.FromHex("0009BBE4C2D17185643765C265819BF5261755247D") };

	public RpcPubkeyType[] FilterType { get; }
	private string FilePath { get; }
	public AsyncLock AsyncLock { get; }
	public uint StartingHeight { get; }
	public List<FilterModel> Filters { get; }

	public static GolombRiceFilter CreateDummyEmptyFilter(uint256 blockHash)
	{
		return new GolombRiceFilterBuilder()
			.SetKey(blockHash)
			.SetP(20)
			.SetM(1 << 20)
			.AddEntries(DummyScript)
			.Build();
	}

	public FilterModel GetLastFilter()
	{
		using (AsyncLock.Lock())
		{
			return Filters[^1];
		}
	}

	public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found)
	{
		using (AsyncLock.Lock())
		{
			found = false; // Only build the filter list from when the known hash is found.
			var filters = new List<FilterModel>();
			foreach (var filter in Filters)
			{
				if (found)
				{
					filters.Add(filter);
					if (filters.Count >= count)
					{
						break;
					}
				}
				else
				{
					if (filter.Header.BlockHash == bestKnownBlockHash)
					{
						found = true;
					}
				}
			}

			if (Filters.Count == 0)
			{
				return (Height.Unknown, Enumerable.Empty<FilterModel>());
			}
			else
			{
				return ((int)Filters[^1].Header.Height, filters);
			}
		}
	}

	public async Task ReorgOneAsync()
	{
		// 1. Rollback index
		using (await AsyncLock.LockAsync())
		{
			Logger.LogInfo($"REORG invalid block: {Filters[^1].Header.BlockHash}");
			Filters.RemoveLast();
		}

		// 2. Serialize Index. (Remove last line.)
		var lines = await File.ReadAllLinesAsync(FilePath).ConfigureAwait(false);
		await File.WriteAllLinesAsync(FilePath, lines.Take(lines.Length - 1).ToArray()).ConfigureAwait(false);
	}

	public async Task BuildAndSerializeFilterAsync(VerboseBlockInfo block, SmartHeader smartHeader)
	{
		var filter = BuildFilterForBlock(block);
		var filterModel = new FilterModel(smartHeader, filter);

		await File.AppendAllLinesAsync(FilePath, new[] { filterModel.ToLine() }).ConfigureAwait(false);

		using (await AsyncLock.LockAsync())
		{
			Filters.Add(filterModel);
		}
	}

	private GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block)
	{
		var scripts = FetchScripts(block);

		if (scripts.Any())
		{
			return new GolombRiceFilterBuilder()
				.SetKey(block.Hash)
				.SetP(20)
				.SetM(1 << 20)
				.AddEntries(scripts.Select(x => x.ToCompressedBytes()))
				.Build();
		}
		else
		{
			// We cannot have empty filters, because there was a bug in GolombRiceFilterBuilder that evaluates empty filters to true.
			// And this must be fixed in a backwards compatible way, so we create a fake filter with a random scp instead.
			return CreateDummyEmptyFilter(block.Hash);
		}
	}

	private List<Script> FetchScripts(VerboseBlockInfo block)
	{
		var scripts = new List<Script>();

		foreach (var tx in block.Transactions)
		{
			foreach (var input in tx.Inputs)
			{
				var prevOut = input.PrevOutput;
				if (prevOut is not null && FilterType.Contains(prevOut.PubkeyType))
				{
					scripts.Add(prevOut.ScriptPubKey);
				}
			}

			foreach (var output in tx.Outputs)
			{
				if (FilterType.Contains(output.PubkeyType))
				{
					scripts.Add(output.ScriptPubKey);
				}
			}
		}

		return scripts;
	}
}
