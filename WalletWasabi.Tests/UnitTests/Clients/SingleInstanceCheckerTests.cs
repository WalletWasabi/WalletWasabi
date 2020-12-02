using NBitcoin;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class SingleInstanceCheckerTests
	{
		private static Random Random { get; } = new();

		/// <summary>
		/// Global port may collide when several PRs are being tested on CI at the same time,
		/// so we need some sort of non-determinism here (e.g. random numbers).
		/// </summary>
		private static int GenerateRandomPort()
		{
			return Random.Next(37128, 37168);
		}

		[Fact]
		public async Task SingleInstanceTestsAsync()
		{
			int mainNetPort = GenerateRandomPort();
			int testNetPort = GenerateRandomPort();
			int regTestPort = GenerateRandomPort();

			// Disposal test.
			await using (SingleInstanceChecker sic = new(mainNetPort))
			{
				await sic.EnsureSingleOrThrowAsync();
			}

			// Check different networks.
			await using (SingleInstanceChecker sic = new(mainNetPort))
			{
				await sic.EnsureSingleOrThrowAsync();
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await sic.EnsureSingleOrThrowAsync());

				await using SingleInstanceChecker sicMainNet2 = new(mainNetPort);
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicMainNet2.EnsureSingleOrThrowAsync());

				await using SingleInstanceChecker sicTestNet = new(testNetPort);
				await sicTestNet.EnsureSingleOrThrowAsync();
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicTestNet.EnsureSingleOrThrowAsync());

				await using SingleInstanceChecker sicRegTest = new(regTestPort);
				await sicRegTest.EnsureSingleOrThrowAsync();
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await sicRegTest.EnsureSingleOrThrowAsync());
			}
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
				using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));

				// Simulate a portscan operation.
				using (TcpClient client = new TcpClient())
				{
					// This should not be counted.
					await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
					using NetworkStream networkStream = client.GetStream();
					networkStream.WriteTimeout = (int)SingleInstanceChecker.ClientTimeOut.TotalMilliseconds;
					using var writer = new StreamWriter(networkStream, Encoding.UTF8);
					await writer.WriteAsync("fake message");
				}

				// Simulate a portscan operation.
				using (TcpClient client = new TcpClient())
				{
					// This should not be counted.
					await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
					await using NetworkStream networkStream = client.GetStream();

					// This should throw as the first instance should disconnect the clients after the timeout.
					await using var writer = new StreamWriter(networkStream, Encoding.UTF8);
					await Task.Delay(SingleInstanceChecker.ClientTimeOut + TimeSpan.FromMilliseconds(500), cts.Token);
					await writer.WriteAsync("late message");

					// The stream must be flushed to be able to detect conneciton loss.
					Assert.Throws<IOException>(() => writer.Flush());
				}

				// One more to check of the first instance was able to recover from the portscan operation
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
}
