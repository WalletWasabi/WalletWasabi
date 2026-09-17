using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class RequestStateTests
{
	[Fact]
	public void QueuedOldQrCannotReplaceANewerRequest()
	{
		var scheduler = new HistoricalScheduler();
		var streams = new List<Subject<bool[,]>>();
		using var state = new MobilePaymentRequestState("wallet-address", _ =>
		{
			var stream = new Subject<bool[,]>();
			streams.Add(stream);
			return stream;
		}, _ => Task.CompletedTask, scheduler);
		var old = new bool[21, 21];
		streams[0].OnNext(old);
		state.Amount = "0.01";
		var current = new bool[25, 25];
		streams[1].OnNext(current);
		scheduler.Start();
		Assert.Same(current, state.Matrix);
		Assert.Equal("bitcoin:wallet-address?amount=0.01", state.PaymentRequest);
		Assert.False(state.IsGenerating);
		foreach (var stream in streams) stream.Dispose();
	}

	[Fact]
	public void InvalidAmountCancelsPendingQrAndDisablesCopy()
	{
		var scheduler = new HistoricalScheduler();
		using var source = new Subject<bool[,]>();
		using var state = new MobilePaymentRequestState("wallet-address", _ => source, _ => Task.CompletedTask, scheduler);
		source.OnNext(new bool[21, 21]);
		state.Amount = "0.000000001";
		scheduler.Start();
		Assert.Null(state.Matrix);
		Assert.Empty(state.PaymentRequest);
		Assert.False(state.CopyRequestCommand.CanExecute(null));
		Assert.Contains("eight decimal", state.Error);
	}

	[Fact]
	public void SynchronousGeneratorFailureDoesNotBreakValidCopy()
	{
		var scheduler = new HistoricalScheduler();
		using var state = new MobilePaymentRequestState("wallet-address", _ => throw new InvalidOperationException(), _ => Task.CompletedTask, scheduler);
		scheduler.Start();
		Assert.Contains("QR generation failed", state.Error);
		Assert.True(state.CopyRequestCommand.CanExecute(null));
		Assert.False(state.IsGenerating);
	}

	[Fact]
	public void EmptyGenerationIsReportedRatherThanSpinningForever()
	{
		var scheduler = new HistoricalScheduler();
		using var state = new MobilePaymentRequestState("wallet-address", _ => Observable.Empty<bool[,]>(), _ => Task.CompletedTask, scheduler);
		scheduler.Start();
		Assert.False(state.IsGenerating);
		Assert.Contains("QR generation failed", state.Error);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(21, 20)]
	public void InvalidMatrixIsNeverDisplayed(int width, int height)
	{
		var scheduler = new HistoricalScheduler();
		using var state = new MobilePaymentRequestState("wallet-address", _ => Observable.Return(new bool[width, height]), _ => Task.CompletedTask, scheduler);
		scheduler.Start();
		Assert.Null(state.Matrix);
		Assert.NotEmpty(state.Error);
	}

	[Fact]
	public void DisposalUnsubscribesTheGeneratorAndQueuedDelivery()
	{
		var scheduler = new HistoricalScheduler();
		using var source = new Subject<bool[,]>();
		var state = new MobilePaymentRequestState("wallet-address", _ => source, _ => Task.CompletedTask, scheduler);
		Assert.True(source.HasObservers);
		source.OnNext(new bool[21, 21]);
		state.Dispose();
		scheduler.Start();
		Assert.False(source.HasObservers);
		Assert.Null(state.Matrix);
		Assert.Throws<ObjectDisposedException>(() => state.Amount = "1");
		state.Dispose();
	}
}
