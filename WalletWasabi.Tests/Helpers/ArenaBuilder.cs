using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.Tests.Helpers;

/// <summary>
/// Builder class for <see cref="Arena"/>.
/// </summary>
public class ArenaBuilder
{
	public static ArenaBuilder Default => new();

	public TimeSpan? Period { get; set; }
	public WabiSabiConfig? Config { get; set; }
	public IRPCClient? Rpc { get; set; }
	public Prison? Prison { get; set; }
	public RoundParametersFactory? RoundParametersFactory { get; set; }

	/// <param name="rounds">Rounds to initialize <see cref="Arena"/> with.</param>
	public Arena Create(params Round[] rounds)
	{
		TimeSpan period = Period ?? TimeSpan.FromHours(1);
		Prison prison = Prison ?? WabiSabiFactory.CreatePrison();
		WabiSabiConfig config = Config ?? new();
		IRPCClient rpc = Rpc ?? WabiSabiFactory.CreatePreconfiguredRpcClient();
		RoundParametersFactory roundParametersFactory = RoundParametersFactory ?? CreateRoundParametersFactory(config);
		FeeRateProvider feeProvider = FeeRateProviders.RpcAsync(rpc);

		Arena arena = new(config, rpc, prison, roundParametersFactory, feeProvider, period:period);

		foreach (var round in rounds)
		{
			arena.Rounds.Add(round);
		}

		return arena;
	}

	public Task<Arena> CreateAndStartAsync(params Round[] rounds)
		=> CreateAndStartAsync(rounds, CancellationToken.None);

	public async Task<Arena> CreateAndStartAsync(Round[] rounds, CancellationToken cancellationToken = default)
	{
		Arena? toDispose = null;

		try
		{
			toDispose = Create(rounds);
			Arena arena = toDispose;
			await arena.StartAsync(cancellationToken).ConfigureAwait(false);
			toDispose = null;
			return arena;
		}
		finally
		{
			toDispose?.Dispose();
		}
	}

	public ArenaBuilder With(IRPCClient rpc)
	{
		Rpc = rpc;
		return this;
	}

	public ArenaBuilder With(RoundParametersFactory roundParametersFactory)
	{
		RoundParametersFactory = roundParametersFactory;
		return this;
	}

	public static ArenaBuilder From(WabiSabiConfig cfg) => new() { Config = cfg };

	public static ArenaBuilder From(WabiSabiConfig cfg, Prison prison) => new() { Config = cfg, Prison = prison };

	public static ArenaBuilder From(WabiSabiConfig cfg, IRPCClient mockRpc, Prison prison) => new() { Config = cfg, Rpc = mockRpc, Prison = prison };

	private static RoundParametersFactory CreateRoundParametersFactory(WabiSabiConfig cfg) =>
		WabiSabiFactory.CreateRoundParametersFactory(cfg, Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize);
}
