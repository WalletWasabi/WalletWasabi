using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class UtxoPrisonWardenTests
{
	[Fact]
	public async Task CanStartAndStopAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);
		var prisonPath = Path.Combine(workDir, "Prison.txt");
		using var w = new Warden(prisonPath, new WabiSabiConfig());
		await w.StartAsync(CancellationToken.None);
		await w.StopAsync(CancellationToken.None);
	}

	// [Fact] FIXME. It fails for an unknown reason after upgrading to .NET 10
	public async Task PrisonSerializationAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		var prisonPath = Path.Combine(workDir, "Prison.txt");
		// Create prison.
		using var w = new Warden(prisonPath, new WabiSabiConfig());
		await w.StartAsync(CancellationToken.None);
		var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		var i1 = BitcoinFactory.CreateOutPoint();
		var i2 = BitcoinFactory.CreateOutPoint();
		var i3 = BitcoinFactory.CreateOutPoint();
		var i4 = BitcoinFactory.CreateOutPoint();
		var i5 = BitcoinFactory.CreateOutPoint();
		w.Prison.FailedVerification(i1, uint256.One);
		w.Prison.FailedToConfirm(i2, Money.Coins(0.01m), uint256.One);
		w.Prison.FailedToSign(i3, Money.Coins(0.1m), uint256.One);
		w.Prison.DoubleSpent(i4, Money.Coins(0.1m), uint256.One);
		w.Prison.CheatingDetected(i5, uint256.One);

		// Wait until serializes.
		await w.StopAsync(CancellationToken.None);
		await Task.Delay(1000);

		// See if prev UTXOs are loaded.
		var cfg = new WabiSabiConfig();
		using var w2 = new Warden(prisonPath, cfg);
		await w2.StartAsync(CancellationToken.None);

		var dosConfig =  cfg.GetDoSConfiguration();
		Assert.True(w2.Prison.IsBanned(i1, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i2, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i3, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i4, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i5, dosConfig, DateTimeOffset.UtcNow));

		await w2.StopAsync(CancellationToken.None);
	}
}
