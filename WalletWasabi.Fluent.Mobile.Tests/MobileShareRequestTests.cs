using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Headless.XUnit;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Services;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileShareRequestTests
{
	[Fact]
	public void UnavailableServiceAndInvalidRequestsCannotOpenAShareSheet()
	{
		using var requests = new BehaviorSubject<string>("bitcoin:regtest?amount=0.01");
		using var unsupported = new MobileShareRequestState(null, requests, ImmediateScheduler.Instance);
		Assert.False(unsupported.IsSupported);
		Assert.False(unsupported.Command.CanExecute(null));
		var service = new ShareSpy();
		using var state = new MobileShareRequestState(service, requests, ImmediateScheduler.Instance);
		Assert.True(state.Command.CanExecute(null));
		requests.OnNext("");
		Assert.False(state.Command.CanExecute(null));
		Assert.Empty(service.Payloads);
	}

	[Fact]
	public async Task ShareUsesOnlyTheCurrentPayloadAndNeverCopiesPrivateLabels()
	{
		using var requests = new BehaviorSubject<string>("bitcoin:first");
		var service = new ShareSpy();
		using var state = new MobileShareRequestState(service, requests, ImmediateScheduler.Instance);
		requests.OnNext("bitcoin:second?amount=0.00000001");
		await Execute(state);
		Assert.Equal(new[] { "bitcoin:second?amount=0.00000001" }, service.Payloads);
		Assert.Empty(state.Error);
	}

	[Fact]
	public async Task PlatformFailureAllowsRetryWithoutLosingThePayload()
	{
		using var requests = new BehaviorSubject<string>("bitcoin:regtest");
		var service = new ShareSpy { Handler = (_, _) => throw new InvalidOperationException("fixture error") };
		using var state = new MobileShareRequestState(service, requests, ImmediateScheduler.Instance);
		await Execute(state);
		Assert.True(state.HasError);
		Assert.True(state.Command.CanExecute(null));
		service.Handler = (_, _) => Task.CompletedTask;
		await Execute(state);
		Assert.False(state.HasError);
		Assert.Equal(2, service.Payloads.Count);
	}

	[Fact]
	public async Task AStaleFailureCannotOverwriteANewerRequest()
	{
		using var requests = new BehaviorSubject<string>("bitcoin:first");
		var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var service = new ShareSpy { Handler = (_, _) => pending.Task };
		using var state = new MobileShareRequestState(service, requests, ImmediateScheduler.Instance);
		var operation = Execute(state);
		requests.OnNext("bitcoin:second");
		pending.SetException(new InvalidOperationException("old request failed"));
		await operation;
		Assert.Equal("bitcoin:second", state.Payload);
		Assert.Empty(state.Error);
	}

	[Fact]
	public void DisposalCancelsPlatformWorkAndUnsubscribesFromRequestChanges()
	{
		using var requests = new BehaviorSubject<string>("bitcoin:regtest");
		CancellationToken cancellation = default;
		var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var service = new ShareSpy { Handler = (_, token) => { cancellation = token; return pending.Task; } };
		var state = new MobileShareRequestState(service, requests, ImmediateScheduler.Instance);
		using var execution = ((ReactiveCommand<Unit, Unit>)state.Command).Execute().Subscribe();
		Assert.True(requests.HasObservers);
		state.Dispose();
		Assert.True(cancellation.IsCancellationRequested);
		Assert.False(requests.HasObservers);
		pending.TrySetCanceled();
	}

	[AvaloniaFact]
	public async Task ApplicationRegistrationIsIsolatedAndFollowsTheCurrentHost()
	{
		var firstApp = new Application();
		var secondApp = new Application();
		var first = new ShareSpy();
		var replacement = new ShareSpy();
		Assert.Null(MobileSharing.GetService(firstApp));
		Assert.Null(MobileSharing.GetService(null));
		MobileSharing.Register(firstApp, first);
		var service = MobileSharing.GetService(firstApp);
		Assert.NotNull(service);
		Assert.Null(MobileSharing.GetService(secondApp));
		MobileSharing.Register(firstApp, replacement);
		await service.PresentAsync("bitcoin:regtest", CancellationToken.None);
		Assert.Empty(first.Payloads);
		Assert.Single(replacement.Payloads);
	}

	private static Task Execute(MobileShareRequestState state) => ((ReactiveCommand<Unit, Unit>)state.Command).Execute().ToTask();
	internal sealed class ShareSpy : IMobileShareService
	{
		public List<string> Payloads { get; } = new();
		public Func<string, CancellationToken, Task> Handler { get; set; } = (_, _) => Task.CompletedTask;
		public Task PresentAsync(string payload, CancellationToken cancellationToken)
		{
			Payloads.Add(payload);
			return Handler(payload, cancellationToken);
		}
	}
}
