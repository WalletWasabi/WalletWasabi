#!/usr/bin/env dotnet
#:project ../../WalletWasabi/WalletWasabi.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0

/// <summary>
/// Tool that connects to a Bitcoin peer supporting compact block filters (BIP 157/158), fetches the block headers from the
/// checkpoint the client starts at up to a block plus the block's basic compact filter + filter header, and prints the
/// FilterCheckpoints and BlockHeaderCheckpoints entries for it.
/// </summary>
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Globalization;
using WalletWasabi.Blockchain.Blocks;

static void PrintUsage()
{
	Console.WriteLine("""
		Usage:
		  dotnet run --file GenerateCheckpoint.cs -- --height <uint> --hash <hex> [options]

		Required:
		  --height <uint>              Block height
		  --hash   <hex>               Block hash (64 hex chars)

		Optional:
		  -e, --endpoint <host:port>   Bitcoin peer endpoint _providing_ compact filters
		                               (default: [::ffff:89.58.60.208]:8333)
		  --help                       Show this help

		Examples:
		  # Get checkpoint data for the block '000000000000000000001268...' with height 960000 using a predefined P2P node.
		  dotnet run --file GenerateCheckpoint.cs -- --height 960000 --hash 000000000000000000001268aab06132c2dd203f77b6020462cd177942d6959d
		  # Get checkpoint data for the block '000000000000000000001268...' with height 960000 using '[::ffff:89.58.60.208]:8333' peer.
		  dotnet run --file GenerateCheckpoint.cs -- --endpoint "[::ffff:89.58.60.208]:8333" --height 960000 --hash 000000000000000000001268aab06132c2dd203f77b6020462cd177942d6959d
		""");
}

// Default parameters.
string nodeEndpoint = "[::ffff:89.58.60.208]:8333";
Network network = Network.Main;
uint256? blockHash = null;
uint? blockHeight = null;

if (!TryParseArgs(args, ref nodeEndpoint, ref blockHash, ref blockHeight, out var showHelp, out var error))
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

if (blockHeight is null || blockHash is null)
{
	Console.Error.WriteLine("error: --height and --hash are required");
	PrintUsage();
	return 1;
}

Console.WriteLine($"Connecting to '{nodeEndpoint}' on {network.Name} ...");
Console.WriteLine($"Target block: height={blockHeight}, hash={blockHash}");

try
{
	var data = await GetCheckPointDataAsync(network, nodeEndpoint, blockHash, blockHeight.Value);

	var grFilter = new GolombRiceFilter(data.CompactFilterPayload.FilterBytes);
	var actualCompactFilterHeader = grFilter.GetHeader(data.CompactFilterHeadersPayload.PreviousFilterHeader);

	var blockHashStr = data.Block.HashBlock.ToString();
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

	// The header chain must start at the first block of the checkpoint's difficulty period to validate the retargets after it.
	var baseHeight = blockHeight.Value - blockHeight.Value % 2016;
	Console.WriteLine();
	Console.WriteLine("=== BlockHeaderCheckpoints entry ===");
	Console.WriteLine();
	if (BlockHeaderCheckpoints.GetBase(network, blockHeight.Value).Height == baseHeight)
	{
		Console.WriteLine($"BlockHeaderCheckpoints already has block {baseHeight}.");
	}
	else
	{
		var baseBlock = data.Block.GetAncestor((int)baseHeight);
		Console.WriteLine($"""
			// {baseBlock.HashBlock}
			({baseHeight}, "{Convert.ToHexString(baseBlock.Header.ToBytes()).ToLowerInvariant()}"),
			""");
	}

	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Error: {ex.Message}");
	if (ex.InnerException is not null)
		Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
	return 1;
}

static bool TryParseArgs(string[] args, ref string endpoint, ref uint256? hash, ref uint? height, out bool showHelp, out string? error)
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

			case "--hash":
				if (i + 1 >= args.Length)
				{
					error = $"{arg} requires a value";
					return false;
				}
				try
				{
					hash = uint256.Parse(args[++i]);
				}
				catch
				{
					error = $"Invalid block hash: {args[i]}";
					return false;
				}
				break;

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

	var block = await SyncHeadersAsync(node, network, blockHash, blockHeight).ConfigureAwait(false);

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

	return new CheckPointData(block, compactFilterHeadersPayload, compactFilterPayload);
}

/// <summary>Downloads the block headers from the checkpoint the client starts at up to the block, validating them as the client does.</summary>
static async Task<ChainedBlock> SyncHeadersAsync(Node node, Network network, uint256 blockHash, uint blockHeight)
{
	var tip = BlockHeaderCheckpoints.GetBase(network, blockHeight).ToChainedBlock();
	Console.WriteLine($"Downloading block headers {tip.Height + 1}-{blockHeight} ...");

	using var listener = node.CreateListener().OfType<HeadersPayload>();
	while (tip.Height < blockHeight)
	{
		var payload = new GetHeadersPayload { BlockLocators = new BlockLocator { Blocks = [tip.HashBlock] }, HashStop = blockHash };
		await node.SendMessageAsync(payload).ConfigureAwait(false);
		var headers = await listener.ReceivePayloadAsync<HeadersPayload>().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		if (headers.Headers.Count == 0)
			throw new Exception($"Peer has no block after {tip.Height}: it is behind or on another chain");

		foreach (var header in headers.Headers)
		{
			if (header.HashPrevBlock != tip.HashBlock)
				throw new Exception($"Peer does not know block {tip.Height} {tip.HashBlock}: it sent headers of a different chain");

			tip = new ChainedBlock(header, header.GetHash(), tip);
			if (!tip.Validate(network))
				throw new Exception($"Block header {tip.Height} {tip.HashBlock} fails proof of work validation");

			if (tip.Height == blockHeight)
				break;
		}
	}

	if (tip.HashBlock != blockHash)
		throw new Exception($"Block {blockHeight} is {tip.HashBlock} on the peer's chain, not {blockHash}");

	return tip;
}

record CheckPointData(
	ChainedBlock Block,
	CompactFilterHeadersPayload CompactFilterHeadersPayload,
	CompactFilterPayload CompactFilterPayload);
