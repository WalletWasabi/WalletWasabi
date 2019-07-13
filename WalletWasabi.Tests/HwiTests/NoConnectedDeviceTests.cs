using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2;
using WalletWasabi.Hwi2.Exceptions;
using Xunit;

namespace WalletWasabi.Tests.HwiTests
{
	/// <summary>
	/// Tests to run without connecting any hardware wallet to the computer.
	/// </summary>
	public class NoConnectedDeviceTests
	{
		#region SharedVariables

		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(3);

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
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				Version version = await client.GetVersionAsync(cts.Token);
				Assert.Equal(new Version("1.0.1"), version);
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task GetHelpTestsAsync(HwiClient client)
		{
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				string help = await client.GetHelpAsync(cts.Token);
				Assert.NotEmpty(help);
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task CanEnumerateTestsAsync(HwiClient client)
		{
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<string> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Empty(enumerate);
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task ThrowOperationCanceledExceptionsAsync(HwiClient client)
		{
			using (var cts = new CancellationTokenSource())
			{
				cts.Cancel();
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetVersionAsync(cts.Token));
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetHelpAsync(cts.Token));
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.EnumerateAsync(cts.Token));
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.SetupAsync(cts.Token));
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.DisplayAddressAsync(cts.Token));
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task CanCallAsynchronouslyAsync(HwiClient client)
		{
			using (var cts = new CancellationTokenSource())
			{
				var tasks = new List<Task>
				{
					client.GetVersionAsync(cts.Token),
					client.GetVersionAsync(cts.Token),
					client.GetHelpAsync(cts.Token),
					client.GetHelpAsync(cts.Token),
					client.EnumerateAsync(cts.Token),
					client.EnumerateAsync(cts.Token),
					client.SetupAsync(cts.Token),
					client.SetupAsync(cts.Token),
					client.DisplayAddressAsync(cts.Token),
					client.DisplayAddressAsync(cts.Token)
				};

				cts.CancelAfter(ReasonableRequestTimeout * tasks.Count);

				await Task.WhenAny(tasks);
			}
		}

		[Theory]
		[MemberData(nameof(GetHwiClientConfigurationCombinationValues))]
		public async Task ThrowsHwiExceptionsAsync(HwiClient client)
		{
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(cts.Token));
			}

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				await Assert.ThrowsAsync<HwiException>(async () => await client.DisplayAddressAsync(cts.Token));
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
