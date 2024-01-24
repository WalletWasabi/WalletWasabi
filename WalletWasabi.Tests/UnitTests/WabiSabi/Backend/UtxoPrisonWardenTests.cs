using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
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
		using var w = new Warden(
			coordinatorParameters.PrisonFilePath,
			WabiSabiFactory.CreateCoinJoinIdStore(),
			coordinatorParameters.RuntimeCoordinatorConfig);
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
		using var w = new Warden(
			coordinatorParameters.PrisonFilePath,
			WabiSabiFactory.CreateCoinJoinIdStore(),
			coordinatorParameters.RuntimeCoordinatorConfig);
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

		// See if prev UTXOs are loaded.
		CoordinatorParameters coordinatorParameters2 = new(workDir);
		var coinjoinIdStoreMock = new Mock<ICoinJoinIdStore>();
		coinjoinIdStoreMock.Setup(x => x.Contains(It.IsAny<uint256>())).Returns(true);
		using var w2 = new Warden(coordinatorParameters2.PrisonFilePath, coinjoinIdStoreMock.Object, coordinatorParameters2.RuntimeCoordinatorConfig);
		await w2.StartAsync(CancellationToken.None);

		var dosConfig = coordinatorParameters2.RuntimeCoordinatorConfig.GetDoSConfiguration();
		Assert.True(w2.Prison.IsBanned(i1, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i2, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i3, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i4, dosConfig, DateTimeOffset.UtcNow));
		Assert.True(w2.Prison.IsBanned(i5, dosConfig, DateTimeOffset.UtcNow));

		await w2.StopAsync(CancellationToken.None);
	}
}
