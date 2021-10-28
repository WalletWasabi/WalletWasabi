using Moq;
using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Tests.Helpers
{
	/// <summary>
	/// Builder class for <see cref="Arena"/>.
	/// </summary>
	public class ArenaBuilder
	{
		public TimeSpan? Period { get; set; }
		public Network? Network { get; set; }
		public WabiSabiConfig? Config { get; set; }
		public IRPCClient? Rpc { get; set; }
		public Prison? Prison { get; set; }

		/// <param name="rounds">Rounds to initialize <see cref="Arena"/> with.</param>
		public Arena Create(params Round[] rounds)
		{
			TimeSpan period = Period ?? TimeSpan.FromHours(1);
			Prison prison = Prison ?? new();
			WabiSabiConfig config = Config ?? new();
			IRPCClient rpc = Rpc ?? WabiSabiFactory.CreatePreconfiguredRpcClient().Object;
			Network network = Network ?? Network.Main;

			Arena arena = new(period, network, config, rpc, prison);

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

		public static ArenaBuilder From(WabiSabiConfig cfg) => new() { Config = cfg };

		public static ArenaBuilder From(WabiSabiConfig cfg, Prison prison) => new() { Config = cfg, Prison = prison };

		public static ArenaBuilder From(WabiSabiConfig cfg, IMock<IRPCClient> mockRpc, Prison prison) => new() { Config = cfg, Rpc = mockRpc.Object, Prison = prison };
	}
}
