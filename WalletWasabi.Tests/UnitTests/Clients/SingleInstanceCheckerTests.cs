using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class SingleInstanceCheckerTests
{
	private static Random Random { get; } = new();

	/// <summary>
	/// Global port may collide when several PRs are being tested on CI at the same time,
	/// so we need some sort of non-determinism here (i.e. random numbers).
	/// </summary>
	private static int GenerateRandomPort()
	{
		return Random.Next(37128, 50000);
	}

	[Fact]
	public async Task SingleInstanceTestsAsync()
	{
		int mainNetPort = GenerateRandomPort();
		int testNetPort = mainNetPort + 1;
		int regTestPort = testNetPort + 1;

		// Disposal test.
		await using (SingleInstanceChecker sic = new(mainNetPort))
		{
			await sic.EnsureSingleOrThrowAsync();
		}

		// Check different networks.
		await using SingleInstanceChecker sicMainNet = new(mainNetPort);
		await sicMainNet.EnsureSingleOrThrowAsync();
		await using SingleInstanceChecker sicMainNet2 = new(mainNetPort);
		await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicMainNet2.EnsureSingleOrThrowAsync());

		await using SingleInstanceChecker sicTestNet = new(testNetPort);
		await sicTestNet.EnsureSingleOrThrowAsync();
		await using SingleInstanceChecker sicTestNet2 = new(testNetPort);
		await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicTestNet2.EnsureSingleOrThrowAsync());

		await using SingleInstanceChecker sicRegTest = new(regTestPort);
		await sicRegTest.EnsureSingleOrThrowAsync();
		await using SingleInstanceChecker sicRegTest2 = new(regTestPort);
		await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicRegTest2.EnsureSingleOrThrowAsync());
	}

	[Fact]
	public async Task OtherInstanceStartedTestsAsync()
	{
		int mainNetPort = GenerateRandomPort();

		// Disposal test.
		await using SingleInstanceChecker firstInstance = new(mainNetPort);
		long eventCalled = 0;

		firstInstance.OtherInstanceStarted += SetCalled;

		try
		{
			// I am the first instance this should be fine.
			await firstInstance.EnsureSingleOrThrowAsync();

			await using SingleInstanceChecker secondInstance = new(mainNetPort);

			for (int i = 0; i < 2; i++)
			{
				// I am the second one.
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await secondInstance.EnsureSingleOrThrowAsync());
			}

			// Overall Timeout.
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

			// Simulate a port scan operation.
			using (TcpClient client = new()
			{
				NoDelay = true
			})
			{
				// This should not be counted.
				await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
				await using NetworkStream networkStream = client.GetStream();
				networkStream.WriteTimeout = (int)SingleInstanceChecker.ClientTimeOut.TotalMilliseconds;
				await using var writer = new StreamWriter(networkStream, Encoding.UTF8);
				await writer.WriteAsync(new StringBuilder("fake message"), cts.Token);
				await writer.FlushAsync().WithAwaitCancellationAsync(cts.Token);
				await networkStream.FlushAsync(cts.Token);
			}

			// Simulate a port scan operation.
			try
			{
				using TcpClient client = new()
				{
					NoDelay = true
				};

				// This should not be counted.
				await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
				await using NetworkStream networkStream = client.GetStream();

				// This should throw as the first instance should disconnect the clients after the timeout.
				await using var writer = new StreamWriter(networkStream, Encoding.UTF8);

				// Wait until timeout on Server side, so the client will be disconnected.
				await Task.Delay(SingleInstanceChecker.ClientTimeOut + TimeSpan.FromMilliseconds(500), cts.Token);

				// This won't throw if the connection is lost, just continues.
				await writer.WriteAsync(new StringBuilder("late message"), cts.Token);
				await writer.FlushAsync().WithAwaitCancellationAsync(cts.Token);
				await networkStream.FlushAsync(cts.Token);
			}
			catch (IOException)
			{
				// If the underlying connection is lost and there is something in the send buffer NetworkStream dispose will throw.
			}

			// One more to check if the first instance was able to recover from the port scan operation.
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await secondInstance.EnsureSingleOrThrowAsync());

			while (Interlocked.Read(ref eventCalled) != 3)
			{
				cts.Token.ThrowIfCancellationRequested();
			}

			// There should be the same number of events as the number of tries from the second instance.
			Assert.Equal(3, Interlocked.Read(ref eventCalled));
		}
		finally
		{
			firstInstance.OtherInstanceStarted -= SetCalled;
		}

		void SetCalled(object? sender, EventArgs args)
		{
			Interlocked.Increment(ref eventCalled);
		}
	}
}
