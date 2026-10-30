#!/usr/bin/env dotnet
#:package NBitcoin
#:property PublishAot=false
#:property TargetFramework=net10.0

/// <summary>
/// Standalone tool that connects to a Bitcoin peer supporting compact block filters (BIP 157/158), and fetches:
/// 
/// * a block,
/// * its basic compact filter, and
/// * filter header.
///
/// The script then prints checkpoint data suitable for WalletWasabi-style filter checkpoints.
/// </summary>
/// <remarks>Block hashes are looked up by height using multiple third-party block explorers (all must agree).</remarks>
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Globalization;

static void PrintUsage()
{
	Console.WriteLine("""
		Usage:
		  dotnet run --file GenerateCheckpoint.cs -- --height <uint> [options]

		Required:
		  --height <uint>              Block height

		Optional:
		  -e, --endpoint <host:port>   Bitcoin peer endpoint _providing_ compact filters
		                               (default: [::ffff:89.58.60.208]:8333)
		  --help                       Show this help

		The block hash is looked up by height from mempool.space and blockstream.info (both must agree).

		Examples:
		  # Get checkpoint data for the block with height 960000 using a predefined P2P node.
		  dotnet run --file GenerateCheckpoint.cs -- --height 960000
		  # Get checkpoint data for the block with height 960000 using '[::ffff:89.58.60.208]:8333' peer.
		  dotnet run --file GenerateCheckpoint.cs -- --endpoint "[::ffff:89.58.60.208]:8333" --height 960000
		""");
}

// Default parameters.
string nodeEndpoint = "[::ffff:89.58.60.208]:8333";
Network network = Network.Main;
uint? blockHeight = null;
const string BlockHashProviders = "https://mempool.space/api/block-height/;https://blockstream.info/api/block-height/";

if (!TryParseArgs(args, ref nodeEndpoint, ref blockHeight, out var showHelp, out var error))
{
	if (error is not null)
		Console.Error.WriteLine($"error: {error}");
	PrintUsage();
	return 1;
}

if (showHelp)
{
	PrintUsage();
	return 0;
}

if (blockHeight is null)
{
	Console.Error.WriteLine("error: --height is required");
	PrintUsage();
	return 1;
}

try
{
	Console.WriteLine($"Looking up hash of block {blockHeight}…");
	var blockHash = await GetBlockHashAsync(blockHeight.Value);
	Console.WriteLine($"Block hash for height {blockHeight} is '{blockHash}'.");
	Console.WriteLine();

	Console.WriteLine($"Connecting to '{nodeEndpoint}' on {network.Name} network…");
	var data = await GetCheckPointDataAsync(network, nodeEndpoint, blockHash, blockHeight.Value);

	var grFilter = new GolombRiceFilter(data.CompactFilterPayload.FilterBytes);
	var actualCompactFilterHeader = grFilter.GetHeader(data.CompactFilterHeadersPayload.PreviousFilterHeader);

	var blockHashStr = data.Block.GetHash().ToString();
	var blockTime = data.Block.Header.BlockTime.ToUnixTimeSeconds();
	var blockFilter = Convert.ToHexString(data.CompactFilterPayload.FilterBytes).ToLowerInvariant();

	var nfi = new NumberFormatInfo
	{
		NumberGroupSeparator = "_",   // your custom thousands separator
		NumberGroupSizes = new[] { 3 } // groups of 3 digits
	};

	Console.WriteLine();
	Console.WriteLine("=== Checkpoint data ===");
	Console.WriteLine();
	Console.WriteLine($"Block hash:               {blockHashStr}");
	Console.WriteLine($"Block filter header hash: {actualCompactFilterHeader}");
	Console.WriteLine($"Block height              {blockHeight}");
	Console.WriteLine($"Block time:               {blockTime}");
	Console.WriteLine($"Block filter:             {blockFilter[..Math.Min(100, blockFilter.Length)]}...");
	Console.WriteLine();

	Console.WriteLine("=== C# code ===");
	Console.WriteLine();
	Console.WriteLine($"""
		// Block {blockHeight}
		new FilterModel(
			new SmartHeader(
				new uint256("{blockHashStr}"),
				new uint256("{actualCompactFilterHeader}"),
				{((decimal)blockHeight).ToString("N0", nfi)}u,
				{blockTime}L),
			new GolombRiceFilter(Convert.FromHexString("{blockFilter}"))
		),
		""");

	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Error: {ex.Message}");
	if (ex.InnerException is not null)
		Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
	return 1;
}

static bool TryParseArgs(string[] args, ref string endpoint, ref uint? height, out bool showHelp, out string? error)
{
	showHelp = false;
	error = null;

	for (int i = 0; i < args.Length; i++)
	{
		var arg = args[i];

		switch (arg)
		{
			case "--help":
				showHelp = true;
				return true;

			case "--height":
				if (i + 1 >= args.Length)
				{
					error = $"{arg} requires a value";
					return false;
				}
				if (!uint.TryParse(args[++i], out var h))
				{
					error = $"Invalid height: {args[i]}";
					return false;
				}
				height = h;
				break;

			case "-e":
			case "--endpoint":
				if (i + 1 >= args.Length)
				{
					error = $"{arg} requires a value";
					return false;
				}
				endpoint = args[++i];
				break;

			default:
				error = $"Unrecognized argument: {arg}";
				return false;
		}
	}

	return true;
}

/// <summary>
/// Gets block hash for the provided block height from multiple block explorers.
/// </summary>
static async Task<uint256> GetBlockHashAsync(uint height)
{
#pragma warning disable RS0030 // Do not use banned APIs – Clearnet is acceptable here.
	using var httpClient = new HttpClient()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};
#pragma warning restore RS0030 // Do not use banned APIs

	(string Provider, uint256 Hash)[] results = await Task.WhenAll(BlockHashProviders.Split(";").Select(async baseUrl =>
	{
		var response = await httpClient.GetStringAsync($"{baseUrl}{height}").ConfigureAwait(false);
		var hash = uint256.Parse(response.Trim());

		return (Provider: baseUrl, Hash: hash);
	})).ConfigureAwait(false);

	var hash = results[0].Hash;

	if (results.Any(r => r.Hash != hash))
	{
		var details = string.Join(", ", results.Select(r => $"{new Uri(r.Provider).Host}={r.Hash}"));
		throw new Exception($"Providers disagree on the hash of block {height}: {details}");
	}

	return hash;
}

static async Task<CheckPointData> GetCheckPointDataAsync(
	Network network,
	string endpoint,
	uint256 blockHash,
	uint blockHeight)
{
	using var node = await Node.ConnectAsync(network, endpoint);
	node.Behaviors.Add(new PingPongBehavior());
	await node.VersionHandshakeAsync().ConfigureAwait(false);

	if (!node.PeerVersion.Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS))
		throw new Exception("Peer does not support compact block filters (NODE_COMPACT_FILTERS)");

	if (!node.PeerVersion.Services.HasFlag(NodeServices.Network))
		throw new Exception("Peer does not provide blocks (NODE_NETWORK)");

	// Get the block
	Block block;
	{
		var blocks = node.GetBlocks([blockHash]);
		block = blocks.Single();

		if (block.GetHash() != blockHash)
			throw new Exception($"Received block hash mismatch: expected {blockHash}, got {block.GetHash()}");

		if (!block.CheckMerkleRoot())
			throw new Exception("Block merkle root check failed");
	}

	CompactFilterPayload compactFilterPayload;
	CompactFilterHeadersPayload compactFilterHeadersPayload;

	using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
	var tcsFilter = new TaskCompletionSource<CompactFilterPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
	var tcsFilterHeader = new TaskCompletionSource<CompactFilterHeadersPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

	void Listener(Node s, IncomingMessage message)
	{
		switch (message.Message.Payload)
		{
			case CompactFilterPayload cFilter:
				tcsFilter.TrySetResult(cFilter);
				break;
			case CompactFilterHeadersPayload cfHeaders:
				tcsFilterHeader.TrySetResult(cfHeaders);
				break;
		}
	}

	node.MessageReceived += Listener;

	try
	{
		// Request the compact filter for the block
		{
			var payload = new GetCompactFiltersPayload(FilterType.Basic, startHeight: blockHeight, stopHash: blockHash);
			node.SendMessage(payload);
			compactFilterPayload = await tcsFilter.Task.WaitAsync(cts.Token).ConfigureAwait(false);
		}

		// Request the compact filter header for the block
		{
			var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, startHeight: blockHeight, stopHash: blockHash);
			node.SendMessage(payload);
			compactFilterHeadersPayload = await tcsFilterHeader.Task.WaitAsync(cts.Token).ConfigureAwait(false);
		}
	}
	finally
	{
		node.MessageReceived -= Listener;
	}

	return new CheckPointData(compactFilterHeadersPayload, compactFilterPayload, block);
}

record CheckPointData(
	CompactFilterHeadersPayload CompactFilterHeadersPayload,
	CompactFilterPayload CompactFilterPayload,
	Block Block);
