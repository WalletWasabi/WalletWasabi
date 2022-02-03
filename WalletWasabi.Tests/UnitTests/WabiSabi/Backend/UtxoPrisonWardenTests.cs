using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class UtxoPrisonWardenTests
{
	[Fact]
	public async Task CanStartAndStopAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);
		CoordinatorParameters coordinatorParameters = new(workDir);
		using var w = new Warden(coordinatorParameters.UtxoWardenPeriod, coordinatorParameters.PrisonFilePath, coordinatorParameters.RuntimeCoordinatorConfig);
		await w.StartAsync(CancellationToken.None);
		await w.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task PrisonSerializationAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		CoordinatorParameters coordinatorParameters = new(workDir);
		using var w = new Warden(coordinatorParameters.UtxoWardenPeriod, coordinatorParameters.PrisonFilePath, coordinatorParameters.RuntimeCoordinatorConfig);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		w.Prison.Punish(i1);
		w.Prison.Punish(i2);

		// Wait until serializes.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		await w.StopAsync(CancellationToken.None);

		// See if prev UTXOs are loaded.
		CoordinatorParameters coordinatorParameters2 = new(workDir);
		using var w2 = new Warden(coordinatorParameters2.UtxoWardenPeriod, coordinatorParameters2.PrisonFilePath, coordinatorParameters2.RuntimeCoordinatorConfig);
		await w2.StartAsync(CancellationToken.None);

		Assert.True(w2.Prison.TryGet(i1.Utxo, out var sameI1));
		Assert.True(w2.Prison.TryGet(i2.Utxo, out var sameI2));
		Assert.Equal(i1.LastDisruptedRoundId, sameI1!.LastDisruptedRoundId);
		Assert.Equal(i2.LastDisruptedRoundId, sameI2!.LastDisruptedRoundId);
		Assert.Equal(i1.Punishment, sameI1!.Punishment);
		Assert.Equal(i2.Punishment, sameI2!.Punishment);
		Assert.Equal(i1.Started, sameI1!.Started);
		Assert.Equal(i2.Started, sameI2!.Started);

		await w2.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NoPrisonSerializationAsync()
	{
		// Don't serialize when there's no change.
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		CoordinatorParameters coordinatorParameters = new(workDir);
		using var w = new Warden(coordinatorParameters.UtxoWardenPeriod, coordinatorParameters.PrisonFilePath, coordinatorParameters.RuntimeCoordinatorConfig);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		w.Prison.Punish(i1);
		w.Prison.Punish(i2);

		// Wait until serializes.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

		// Make sure it does not serialize again as there was no change.
		File.Delete(w.PrisonFilePath);
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		Assert.False(File.Exists(w.PrisonFilePath));
		await w.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ReleasesInmatesAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		CoordinatorParameters coordinatorParameters = new(workDir);
		coordinatorParameters.RuntimeCoordinatorConfig.ReleaseUtxoFromPrisonAfter = TimeSpan.FromMilliseconds(1);

		using var w = new Warden(coordinatorParameters.UtxoWardenPeriod, coordinatorParameters.PrisonFilePath, coordinatorParameters.RuntimeCoordinatorConfig);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var p = w.Prison;
		p.Punish(i1);
		p.Punish(i2);
		Assert.NotEmpty(p.GetInmates());

		// Wait until releases from prison.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		Assert.Empty(p.GetInmates());
		await w.StopAsync(CancellationToken.None);
	}
}
