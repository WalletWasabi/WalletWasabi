using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	/// <summary>
	/// Tests to run without connecting any hardware wallet to the computer.
	/// </summary>
	public class DefaultResponseTests
	{
		#region SharedVariables

		// Bottleneck: Windows CI.
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(1);

		#endregion SharedVariables

		#region Tests

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public void CanCreate(Network network)
		{
			new HwiClient(network);
		}

		[Fact]
		public void ConstructorThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new HwiClient(null!));
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task GetVersionTestsAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			Version version = await client.GetVersionAsync(cts.Token);
			Assert.Equal(WalletWasabi.Helpers.Constants.HwiVersion, version);
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task GetHelpTestsAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			string help = await client.GetHelpAsync(cts.Token);
			Assert.NotEmpty(help);
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task CanEnumerateAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Empty(enumerate);
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task ThrowOperationCanceledExceptionsAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource();
			cts.Cancel();
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetVersionAsync(cts.Token));
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetHelpAsync(cts.Token));
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.EnumerateAsync(cts.Token));
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task ThrowArgumentExceptionsForWrongDevicePathAsync(HwiClient client)
		{
			var wrongDeviePaths = new[] { "", " " };
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			foreach (HardwareWalletModels deviceType in Enum.GetValues(typeof(HardwareWalletModels)).Cast<HardwareWalletModels>())
			{
				foreach (var wrongDevicePath in wrongDeviePaths)
				{
					await Assert.ThrowsAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, wrongDevicePath, cts.Token));
					await Assert.ThrowsAsync<ArgumentException>(async () => await client.SetupAsync(deviceType, wrongDevicePath, false, cts.Token));
				}
				await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.WipeAsync(deviceType, null!, cts.Token));
				await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SetupAsync(deviceType, null!, false, cts.Token));
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task CanCallAsynchronouslyAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource();
			var tasks = new List<Task>
				{
					client.GetVersionAsync(cts.Token),
					client.GetVersionAsync(cts.Token),
					client.GetHelpAsync(cts.Token),
					client.GetHelpAsync(cts.Token),
					client.EnumerateAsync(cts.Token),
					client.EnumerateAsync(cts.Token)
				};

			cts.CancelAfter(ReasonableRequestTimeout * tasks.Count);

			await Task.WhenAny(tasks);
		}

		[Fact]
		public async Task OpenConsoleDoesntThrowAsync()
		{
			var pb = new HwiProcessBridge(new ProcessInvoker());

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var res = await pb.SendCommandAsync("version", openConsole: true, cts.Token);
				Assert.Contains("success", res.response);
			}
			else
			{
				await Assert.ThrowsAsync<PlatformNotSupportedException>(async () => await pb.SendCommandAsync("enumerate", openConsole: true, cts.Token));
			}
		}

		[Theory]
		[InlineData("", false)]
		[InlineData("hwi", false)]
		[InlineData("hwi ", false)]
		[InlineData("hwi 1", false)]
		[InlineData("hwi 1.", false)]
		[InlineData("hwi 1.1", false)]
		[InlineData("hwi 1.1.", false)]
		[InlineData("hwi 1.1.2\n", true)]
		[InlineData("hwi 1.1.2", true)]
		[InlineData("hwi 1.1.2-rc1\n", true)]
		[InlineData("hwi 1.1.2-rc1", true)]
		[InlineData("hwi.exe 1.1.2\n", true)]
		[InlineData("hwi.exe 1.1.2", true)]
		[InlineData("hwi.exe 1.1.2-", true)]
		[InlineData("hwi.exe 1.1.2-rc1\n", true)]
		[InlineData("hwi.exe 1.1.2-rc1", true)]
		[InlineData("1.1.2-rc1\n", false)]
		[InlineData("1.1-rc1\n", false)]
		public void TryParseVersionTests(string input, bool isParsable)
		{
			Version expectedVersion = new Version(1, 1, 2);
			Assert.Equal(isParsable, HwiParser.TryParseVersion(input, out Version actualVersion));

			if (isParsable)
			{
				Assert.Equal(expectedVersion, actualVersion);
			}
		}

		#endregion Tests

		#region HelperMethods

		public static IEnumerable<object[]> GetDifferentNetworkValues()
		{
			var networks = new List<Network>
			{
				Network.Main,
				Network.TestNet,
				Network.RegTest
			};

			foreach (Network network in networks)
			{
				yield return new object[] { network };
			}
		}

		public static IEnumerable<object[]> GetHwiClientConfigurationCombinationValues()
		{
			var networks = new List<Network>
			{
				Network.Main,
				Network.TestNet,
				Network.RegTest
			};

			foreach (Network network in networks)
			{
				yield return new object[] { new HwiClient(network) };
			}
		}

		#endregion HelperMethods
	}
}
