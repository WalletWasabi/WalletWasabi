using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;
using Xunit;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>
/// Tests to run without connecting any hardware wallet to the computer.
/// </summary>
/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
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
		_ = new HwiClient(network);
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
		Assert.Equal(Constants.HwiVersion, version);
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
		var wrongDevicePaths = new[] { null, "", " " };
		using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
		foreach (HardwareWalletModels deviceType in Enum.GetValues<HardwareWalletModels>().Cast<HardwareWalletModels>())
		{
			foreach (var wrongDevicePath in wrongDevicePaths)
			{
				await Assert.ThrowsAnyAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, wrongDevicePath, cts.Token));
				await Assert.ThrowsAnyAsync<ArgumentException>(async () => await client.SetupAsync(deviceType, wrongDevicePath, false, cts.Token));
			}
			await Assert.ThrowsAnyAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, null!, cts.Token));
			await Assert.ThrowsAnyAsync<ArgumentException>(async () => await client.SetupAsync(deviceType, null!, false, cts.Token));
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
		HwiProcessBridge pb = new();

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
		Version expectedVersion = new(1, 1, 2);
		Assert.Equal(isParsable, HwiParser.TryParseVersion(input, out Version? actualVersion));

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

	/// <summary>Verify that <c>--version</c> argument returns output as expected.</summary>
	[Fact]
	public async Task HwiVersionTestAsync()
	{
		using var cts = new CancellationTokenSource(ReasonableRequestTimeout);

		HwiProcessBridge pb = new();

		// Start HWI with "version" argument and test that we get non-empty response.
		(string response, int exitCode) result = await pb.SendCommandAsync("--version", openConsole: false, cts.Token);
		Assert.Contains(Constants.HwiVersion.ToString(), result.response);

		// Start HWI with "version" argument and test that we get non-empty response + verify that "standardInputWriter" is actually called.
		bool stdInputActionCalled = false;
		result = await pb.SendCommandAsync("--version", openConsole: false, cts.Token, (sw) => stdInputActionCalled = true);
		Assert.Contains(Constants.HwiVersion.ToString(), result.response);
		Assert.True(stdInputActionCalled);
	}

	/// <summary>Verify that <c>--help</c> returns output as expected.</summary>
	[Fact]
	public async Task HwiHelpTestAsync()
	{
		using var cts = new CancellationTokenSource(ReasonableRequestTimeout);

		HwiProcessBridge processBridge = new();
		(string response, int exitCode) result = await processBridge.SendCommandAsync("--help", openConsole: false, cts.Token);

		Assert.Equal(0, result.exitCode);
		Assert.Equal(@"{""error"": ""Help text requested"", ""code"": -17}" + Environment.NewLine, result.response);
	}
}
