using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi;

public class WabiSabiCoordinator : BackgroundService
{
	public WabiSabiCoordinator(CoordinatorParameters parameters, IRPCClient rpc, ICoinJoinIdStore coinJoinIdStore, CoinJoinScriptStore coinJoinScriptStore, IHttpClientFactory httpClientFactory, CoinVerifier? coinVerifier = null)
	{
		Parameters = parameters;

		Warden = new(parameters.PrisonFilePath, coinJoinIdStore, Config);
		ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => Logger.LogInfo("WabiSabi configuration has changed."));
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifier = coinVerifier;
		CoinJoinTransactionArchiver transactionArchiver = new(Path.Combine(parameters.CoordinatorDataDir, "CoinJoinTransactions"));

		CoinJoinFeeRateStatStore = CoinJoinFeeRateStatStore.LoadFromFile(parameters.CoinJoinFeeRateStatStoreFilePath, Config, rpc);
		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinFeeRateStatStoreFilePath);
		CoinJoinFeeRateStatStore.NewStat += FeeRateStatStore_NewStat;

		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinScriptStoreFilePath);

		RoundParameterFactory roundParameterFactory = new(Config, rpc.Network);
		Arena = new(
			parameters.RoundProgressSteppingPeriod,
			Config,
			rpc,
			Warden.Prison,
			coinJoinIdStore,
			roundParameterFactory,
			transactionArchiver,
			coinJoinScriptStore,
			coinVerifier);
		AffiliationManager = new(Arena, Config, httpClientFactory);

		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinIdStoreFilePath);
		Arena.CoinJoinBroadcast += Arena_CoinJoinBroadcast;
	}

	public ConfigWatcher ConfigWatcher { get; }
	public ICoinJoinIdStore CoinJoinIdStore { get; private set; }
	public CoinVerifier? CoinVerifier { get; private set; }
	public Warden Warden { get; }

	public CoordinatorParameters Parameters { get; }
	public Arena Arena { get; }

	public CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }

	public WabiSabiConfig Config => Parameters.RuntimeCoordinatorConfig;
	public DateTimeOffset LastSuccessfulCoinJoinTime { get; private set; } = DateTimeOffset.UtcNow;

	public AffiliationManager AffiliationManager { get; }

	private void Arena_CoinJoinBroadcast(object? sender, Transaction transaction)
	{
		LastSuccessfulCoinJoinTime = DateTimeOffset.UtcNow;

		CoinJoinIdStore.TryAdd(transaction.GetHash());

		var coinJoinScriptStoreFilePath = Parameters.CoinJoinScriptStoreFilePath;
		try
		{
			File.AppendAllLines(coinJoinScriptStoreFilePath, transaction.Outputs.Select(x => x.ScriptPubKey.ToHex()));
		}
		catch (Exception ex)
		{
			Logger.LogError($"Could not write file {coinJoinScriptStoreFilePath}.", ex);
		}
	}

	private void FeeRateStatStore_NewStat(object? sender, CoinJoinFeeRateStat feeRateStat)
	{
		var filePath = Parameters.CoinJoinFeeRateStatStoreFilePath;
		try
		{
			File.AppendAllLines(filePath, new[] { feeRateStat.ToLine() });
		}
		catch (Exception ex)
		{
			Logger.LogError($"Could not write file {filePath}.", ex);
		}
	}

	public void BanDescendant(object? sender, Block block)
	{
		var now = DateTimeOffset.UtcNow;

		bool IsInputBanned(TxIn input) => Warden.Prison.IsBanned(input.PrevOut, Config.GetDoSConfiguration(), now);
		OutPoint[] BannedInputs(Transaction tx) => tx.Inputs.Where(IsInputBanned).Select(x => x.PrevOut).ToArray();

		var outpointsToBan = block.Transactions
			.Where(tx => !CoinJoinIdStore.Contains(tx.GetHash()))  // We don't ban coinjoin outputs
			.Select(tx => (Tx: tx, BannedInputs: BannedInputs(tx)))
			.Where(x => x.BannedInputs.Length != 0)
			.SelectMany(x => x.Tx.Outputs.Select((_, i) => (new OutPoint(x.Tx, i), x.BannedInputs)));

		foreach (var (outpoint, ancestors) in outpointsToBan)
		{
			Warden.Prison.InheritPunishment(outpoint, ancestors);
		}
	}

	public void BanDoubleSpenders(object? sender, Transaction tx)
	{
		var txId = tx.GetHash();
		if (IsWasabiCoinJoin(txId, tx))
		{
			return;
		}

		var inputOutPoints = tx.Inputs.Select(x => x.PrevOut);
		var disruptedRounds = Arena.GetRoundsContainingOutpoints(inputOutPoints);

		// No round was disrupted by the received transaction. Nothing to do here.
		if (disruptedRounds.Length == 0)
		{
			return;
		}

		// Ban all outputs created by the received transaction because it has spent coins participating in coinjoin rounds.
		foreach (var indexedOutput in tx.Outputs.AsIndexedOutputs())
		{
			Warden.Prison.DoubleSpent(new OutPoint(tx, indexedOutput.N), indexedOutput.TxOut.Value, disruptedRounds);
		}

		// Abort disrupted rounds
		foreach (var roundId in disruptedRounds)
		{
			Arena.AbortRound(roundId);
		}
	}

	private bool IsWasabiCoinJoin(uint256 txId, Transaction tx) =>
		CoinJoinIdStore.Contains(txId) || IsFinishedCoinJoin(txId) || IsWasabiCoinJoinLookingTx(tx);

	private bool IsFinishedCoinJoin(uint256 txId) =>
		Arena.RoundStates
		.Select(x => x.CoinjoinState)
		.OfType<SigningState>()
		.Any(x => x.CreateUnsignedTransaction().GetHash() == txId);

	private bool IsWasabiCoinJoinLookingTx(Transaction tx) =>
		tx.RBF == false
		&& tx.Inputs.Count >= Config.MinInputCountByRound
		&& tx.Inputs.Count <= Config.MaxInputCountByRound
		&& tx.Outputs.All(x => Config.AllowedOutputTypes.Any(y => x.ScriptPubKey.IsScriptType(y)))
		&& tx.Outputs.Zip(tx.Outputs.Skip(1), (a, b) => (First: a.Value, Second: b.Value)).All(p => p.First >= p.Second);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
		await Warden.StartAsync(stoppingToken).ConfigureAwait(false);
		await Arena.StartAsync(stoppingToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StartAsync(stoppingToken).ConfigureAwait(false);
		await AffiliationManager.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		await Arena.StopAsync(cancellationToken).ConfigureAwait(false);
		await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
		await Warden.StopAsync(cancellationToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StopAsync(cancellationToken).ConfigureAwait(false);
		await AffiliationManager.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		CoinJoinFeeRateStatStore.NewStat -= FeeRateStatStore_NewStat;
		Arena.CoinJoinBroadcast -= Arena_CoinJoinBroadcast;
		ConfigWatcher.Dispose();
		Warden.Dispose();
		AffiliationManager.Dispose();
		base.Dispose();
	}
}
