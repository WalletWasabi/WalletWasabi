using NBitcoin;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.ProcessBridge;
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
			Assert.Throws<ArgumentNullException>(() => new HwiClient(null));
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task GetVersionTestsAsync(HwiClient client)
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			Version version = await client.GetVersionAsync(cts.Token);
			Assert.Equal(new Version("1.0.3"), version);
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
			foreach (HardwareWalletModels deviceType in Enum.GetValues(typeof(HardwareWalletModels)))
			{
				foreach (var wrongDevicePath in wrongDeviePaths)
				{
					await Assert.ThrowsAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, wrongDevicePath, cts.Token));
					await Assert.ThrowsAsync<ArgumentException>(async () => await client.SetupAsync(deviceType, wrongDevicePath, false, cts.Token));
				}
				await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.WipeAsync(deviceType, null, cts.Token));
				await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SetupAsync(deviceType, null, false, cts.Token));
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
		public async Task HwiProcessBridgeTestAsync()
		{
			HwiProcessBridge pb = new HwiProcessBridge();

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var res = await pb.SendCommandAsync("enumerate", false, cts.Token);
			Assert.NotEmpty(res.response);
		}

		[Fact]
		public async Task OpenConsoleDoesntThrowAsync()
		{
			HwiProcessBridge pb = new HwiProcessBridge();

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var res = await pb.SendCommandAsync("enumerate", true, cts.Token);
				Assert.NotEmpty(res.response);
			}
			else
			{
				await Assert.ThrowsAsync<PlatformNotSupportedException>(async () => await pb.SendCommandAsync("enumerate", true, cts.Token));
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
