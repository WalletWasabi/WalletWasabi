using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileCommandReplacementTests
{
	[Fact]
	public void ObservingAnAlreadyExecutingCommandNeverEmitsIdleFirst()
	{
		using var gate = new Subject<Unit>();
		using var command = ReactiveCommand.CreateFromObservable(() => gate, outputScheduler: ImmediateScheduler.Instance);
		using var running = command.Execute().Subscribe();
		var states = new List<bool>();
		using var observation = MobileCommandActivity.Observe(Observable.Return(false), command, null).Subscribe(states.Add);
		Assert.Equal(new[] { true }, states);
		gate.OnCompleted();
		Assert.Equal(new[] { true, false }, states);
	}

	[Fact]
	public void CommandInstalledAfterAttachmentIsObserved()
	{
		using var confirm = new BehaviorSubject<ICommand?>(null);
		using var alternate = new BehaviorSubject<ICommand?>(null);
		using var gate = new Subject<Unit>();
		using var command = ReactiveCommand.CreateFromObservable(() => gate, outputScheduler: ImmediateScheduler.Instance);
		var states = new List<bool>();
		using var observation = MobileCommandActivity.ObserveCommands(Observable.Return(false), confirm, alternate).Subscribe(states.Add);
		confirm.OnNext(command);
		using var running = command.Execute().Subscribe();
		Assert.True(states[^1]);
		gate.OnCompleted();
		Assert.Equal(new[] { false, true, false }, states);
	}

	[Fact]
	public void ReplacingARunningCommandWithAnotherRunningCommandDoesNotUnlock()
	{
		using var firstGate = new Subject<Unit>();
		using var secondGate = new Subject<Unit>();
		using var first = ReactiveCommand.CreateFromObservable(() => firstGate, outputScheduler: ImmediateScheduler.Instance);
		using var second = ReactiveCommand.CreateFromObservable(() => secondGate, outputScheduler: ImmediateScheduler.Instance);
		using var confirm = new BehaviorSubject<ICommand?>(first);
		using var alternate = new BehaviorSubject<ICommand?>(null);
		var states = new List<bool>();
		using var observation = MobileCommandActivity.ObserveCommands(Observable.Return(false), confirm, alternate).Subscribe(states.Add);
		using var firstRun = first.Execute().Subscribe();
		using var secondRun = second.Execute().Subscribe();
		confirm.OnNext(second);
		firstGate.OnCompleted();
		Assert.Equal(new[] { false, true }, states);
		secondGate.OnCompleted();
		Assert.Equal(new[] { false, true, false }, states);
	}

	[Fact]
	public void DisposingActivityStopsFollowingCommandReplacements()
	{
		using var confirm = new BehaviorSubject<ICommand?>(null);
		using var alternate = new BehaviorSubject<ICommand?>(null);
		var states = new List<bool>();
		var observation = MobileCommandActivity.ObserveCommands(Observable.Return(false), confirm, alternate).Subscribe(states.Add);
		observation.Dispose();
		Assert.False(confirm.HasObservers);
		Assert.False(alternate.HasObservers);
		Assert.Equal(new[] { false }, states);
	}

	[Fact]
	public void ActualRouteCommandPropertiesNotifyOnlyOnReplacement()
	{
		using var route = new NavigationFreeRoute();
		using var command = ReactiveCommand.Create(() => { }, outputScheduler: ImmediateScheduler.Instance);
		var properties = new List<string?>();
		route.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
		route.Replace(command);
		route.Replace(command);
		Assert.Equal(new[] { "NextCommand", "SkipCommand", "BackCommand", "CancelCommand" }, properties);
	}

	// These tests never navigate or invoke wallet services. The base constructor only
	// stores UiContext; supplying no context makes accidental service access fail visibly.
	private sealed class NavigationFreeRoute : RoutableViewModel, IDisposable
	{
		private readonly IDisposable? _initialBack;
		private readonly IDisposable? _initialCancel;
		public NavigationFreeRoute() : base(null!)
		{
			_initialBack = BackCommand as IDisposable;
			_initialCancel = CancelCommand as IDisposable;
		}
		public override string Title { get; protected set; } = "Command replacement test";
		public void Replace(ICommand command) { NextCommand = command; SkipCommand = command; BackCommand = command; CancelCommand = command; }
		public void Dispose() { _initialBack?.Dispose(); _initialCancel?.Dispose(); }
	}
}
