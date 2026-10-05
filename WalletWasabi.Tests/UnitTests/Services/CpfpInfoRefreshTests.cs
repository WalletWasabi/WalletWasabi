using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class CpfpInfoRefreshTests
{
	[Fact]
	public async Task ConcurrentFetchesForSameTransactionBothSucceedAsync()
	{
		var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var count = 0;
		var factory = new MockHttpClientFactory
		{
			OnCreateClient = _ => new MockHttpClient
			{
				OnSendAsync = async _ =>
				{
					if (Interlocked.Increment(ref count) == 2) { bothStarted.SetResult(); }
					await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
					return HttpResponseMessageEx.Ok("""{"effectiveFeePerVsize":10.5,"fee":1000,"adjustedVsize":100,"ancestors":[]}""");
				}
			}
		};
		var handler = CpfpInfoUpdater.Create(factory, Network.Main, new EventBus());
		var transaction = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var first = new TestReplyChannel<Result<CpfpInfo, string>>();
		var second = new TestReplyChannel<Result<CpfpInfo, string>>();
		await Task.WhenAll(handler(new CpfpInfoMessage.GetInfoForTransaction(transaction, first), Unit.Instance, CancellationToken.None),
			handler(new CpfpInfoMessage.GetInfoForTransaction(transaction, second), Unit.Instance, CancellationToken.None));
		Assert.True(first.Result!.IsOk, first.Result.IsOk ? null : first.Result.Error);
		Assert.True(second.Result!.IsOk, second.Result.IsOk ? null : second.Result.Error);
		var cache = new TestReplyChannel<CachedCpfpInfo[]>();
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(cache), Unit.Instance, CancellationToken.None);
		Assert.Single(cache.Result!);
	}

	[Fact]
	public async Task PeriodicUpdateRefreshesUnconfirmedFeeInformationAsync()
	{
		var refresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var count = 0;
		var factory = new MockHttpClientFactory
		{
			OnCreateClient = _ => new MockHttpClient
			{
				OnSendAsync = _ =>
				{
					var call = Interlocked.Increment(ref count);
					return Task.FromResult(HttpResponseMessageEx.Ok(call == 1
						? """{"effectiveFeePerVsize":10,"fee":1000,"adjustedVsize":100,"ancestors":[]}"""
						: """{"effectiveFeePerVsize":20,"fee":1000,"adjustedVsize":100,"ancestors":[{"txid":"0000000000000000000000000000000000000000000000000000000000000001","fee":3000,"weight":400}]}"""));
				}
			}
		};
		var events = new EventBus();
		using var subscription = events.Subscribe<CpfpInfoArrived>(_ => { if (Volatile.Read(ref count) > 1) { refresh.TrySetResult(); } });
		var handler = CpfpInfoUpdater.Create(factory, Network.Main, events);
		var transaction = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var first = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(transaction, first), Unit.Instance, CancellationToken.None);
		Assert.True(first.Result!.IsOk);
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		await handler(new CpfpInfoMessage.UpdateMessage(), Unit.Instance, stop.Token);
		await refresh.Task.WaitAsync(TimeSpan.FromSeconds(12));
		var cache = new TestReplyChannel<CachedCpfpInfo[]>();
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(cache), Unit.Instance, CancellationToken.None);
		Assert.Equal(20m, Assert.Single(cache.Result!).CpfpInfo.EffectiveFeePerVSize);
		Assert.Single(Assert.Single(cache.Result!).CpfpInfo.Ancestors);
	}
}
