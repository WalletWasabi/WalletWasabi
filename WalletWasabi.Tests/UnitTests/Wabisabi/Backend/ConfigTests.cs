using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.JsonConverters.Timing;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class ConfigTests
	{
		[Fact]
		public async Task CreatesConfigAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
			CoordinatorParameters coordinatorParameters = new(workDir);
			using WabiSabiCoordinator coordinator = new(coordinatorParameters, new MockRpcClient());
			await coordinator.StartAsync(CancellationToken.None);

			Assert.True(File.Exists(Path.Combine(workDir, "WabiSabiConfig.json")));

			await coordinator.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task LoadsConfigAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
			CoordinatorParameters coordinatorParameters = new(workDir);

			// Create the config first with default value.
			using WabiSabiCoordinator coordinator = new(coordinatorParameters, new MockRpcClient());
			await coordinator.StartAsync(CancellationToken.None);
			await coordinator.StopAsync(CancellationToken.None);

			// Change the file.
			var configPath = Path.Combine(workDir, "WabiSabiConfig.json");
			WabiSabiConfig configChanger = new(configPath);
			configChanger.LoadOrCreateDefaultFile();
			var newTarget = 729;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();

			// Assert the new value is loaded and not the default one.
			using WabiSabiCoordinator coordinator2 = new(coordinatorParameters, new MockRpcClient());
			await coordinator2.StartAsync(CancellationToken.None);
			Assert.Equal(newTarget, coordinator2.Config.ConfirmationTarget);
			await coordinator2.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task LoadsIncompleteConfigAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
			CoordinatorParameters coordinatorParameters = new(workDir);

			// Create the config first with default value.
			using WabiSabiCoordinator coordinator = new(coordinatorParameters, new MockRpcClient());
			await coordinator.StartAsync(CancellationToken.None);
			await coordinator.StopAsync(CancellationToken.None);

			// Remove a line.
			var configPath = Path.Combine(workDir, "WabiSabiConfig.json");
			var lines = File.ReadAllLines(configPath);
			var incompleteLines = lines.Where(x => !x.Contains("ReleaseUtxoFromPrisonAfter", StringComparison.Ordinal)).ToArray();
			Assert.NotEqual(lines.Length, incompleteLines.Length);
			File.WriteAllLines(configPath, incompleteLines);

			// Assert the new default value is loaded.
			CoordinatorParameters coordinatorParameters2 = new(workDir);
			using WabiSabiCoordinator coordinator2 = new(coordinatorParameters2, new MockRpcClient());
			await coordinator2.StartAsync(CancellationToken.None);
			var defaultValue = TimeSpanJsonConverter.Parse("0d 3h 0m 0s");
			Assert.Equal(TimeSpan.FromHours(3), defaultValue);
			Assert.Equal(defaultValue, coordinator2.Config.ReleaseUtxoFromPrisonAfter);
			await coordinator2.StopAsync(CancellationToken.None);

			// Assert the new default value is serialized.
			lines = File.ReadAllLines(configPath);
			Assert.Contains(lines, x => x.Contains("\"ReleaseUtxoFromPrisonAfter\": \"0d 3h 0m 0s\"", StringComparison.Ordinal));
		}

		[Fact]
		public async Task ChecksConfigChangesAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);

			CoordinatorParameters coordinatorParameters = new(workDir);
			using WabiSabiCoordinator coordinator = new(coordinatorParameters, new MockRpcClient());
			await coordinator.StartAsync(CancellationToken.None);

			var configPath = Path.Combine(workDir, "WabiSabiConfig.json");
			WabiSabiConfig configChanger = new(configPath);
			configChanger.LoadOrCreateDefaultFile();
			var newTarget = 729;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();
			var configWatcher = coordinator.ConfigWatcher;
			await configWatcher.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			Assert.Equal(newTarget, coordinator.Config.ConfirmationTarget);

			// Do it one more time.
			newTarget = 372;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();
			await configWatcher.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			Assert.Equal(newTarget, coordinator.Config.ConfirmationTarget);

			await coordinator.StopAsync(CancellationToken.None);
		}
	}
}
