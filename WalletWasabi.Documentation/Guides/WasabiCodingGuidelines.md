# Wasabi Coding Guidelines for UI

## Example ViewModel Class

```c#
  public class MyViewModel : ReactiveObject, IDisposable
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();
		private string _myProperty;
		private bool _myConfirmed;
		private ObservableAsPropertyHelper<bool> _confirmed;
		private SmartCoin Model { get; }

		public string MyProperty
		{
			get => _myProperty;
			set => this.RaiseAndSetIfChanged(ref _myProperty, value);
		}

		public bool MyConfirmed
		{
			get => _myConfirmed;
			set => this.RaiseAndSetIfChanged(ref _myConfirmed, value);
		}

		public ReactiveCommand MyCommand { get; }
		public ReactiveCommand MySecondCommand { get; }

		public MyViewModel()
		{

			this.WhenAnyValue(x => x.MyProperty)
				.Subscribe(myProperty =>
				{
				});

			_confirmed = Model.WhenAnyValue(x => x.Confirmed)
				.ToProperty(this, x => x.MyConfirmed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				})
				.DisposeWith(Disposables);

			Dispatcher.UIThread.PostLogException(() =>
			{
				// Do something on the UI thread. App-crash exception handler built-in.
			});

			Observable.FromEventPattern(
				Global.ChaumianClient,
				nameof(Global.ChaumianClient.StateUpdated))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				}).DisposeWith(Disposables);

			IObservable<bool> isCoinListItemSelected = this.WhenAnyValue(x => x.MyProperty).Select(myProperty => myProperty != null);

			MyCommand = ReactiveCommand.Create(() =>
			{

			}, isCoinListItemSelected);

			MyCommand.ThrownExceptions
				.Subscribe(ex => Logging.Logger.LogWarning<MyViewModel>(ex));

			MySecondCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await Task.Delay(100);
			}, isCoinListItemSelected);

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed)))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed)))
				.Throttle(TimeSpan.FromSeconds(5))									
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => 
				{

				})
				.DisposeWith(Disposables);


		}

		#region IDisposable Support

		private volatile bool _disposedValue = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}
				Disposables = null;

				_disposedValue = true;
			}
		}

		~MyViewModel()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
```
      
      
## Detecting the change of a Property

You can put the following into the constructor of the ViewModel. `MyProperty` should use `this.RaiseAndSetIfChanged()` to notify the change. If you're a VM and you e.g. `WhenAnyValue` against your own property, there's [no need to clean that up because](https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions) that is manifested as the VM having a reference to itself.

```c#
	this.WhenAnyValue(x => x.MyProperty)
		.Subscribe(myProperty =>
		{
		});
```

If you are referencing other object you should dispose your subscription. Use `.DisposeWith()` method.

```c#
	Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
		.ObserveOn(RxApp.MainThreadScheduler)
		.Subscribe(_ =>
		{
		})
		.DisposeWith(Disposables);
```
    
## Event subscriptions

Reactive generates an observable from the event and from then you can use all the toolset of Rx. Regarding the disposal it is pretty similar. If a component exposes an event and also subscribes to it itself, it doesn't need to unsubscribe. That's because the subscription is manifested as the component having a reference to itself.

```c#
	Observable.FromEventPattern(CoinList, nameof(CoinList.SelectionChanged)).Subscribe(_ => SetFeesAndTexts());

	Observable.FromEventPattern(
		Global.ChaumianClient,
		nameof(Global.ChaumianClient.StateUpdated))
		.ObserveOn(RxApp.MainThreadScheduler)
		.Subscribe(_ =>
		{
			RefreshSmartCoinStatus();
		}).DisposeWith(Disposables);
```

[More examples](http://blog.functionalfun.net/2012/03/weak-events-in-net-easy-way.html)

## Expose Model Property to View

When a property's value depends on another property, a set of properties, or an observable stream, rather than set the value explicitly, [use `ObservableAsPropertyHelper` with `WhenAny` wherever possible](https://reactiveui.net/docs/guidelines/framework/prefer-oaph-over-properties).


```c#
	//Define class members.
	private ObservableAsPropertyHelper<bool> _confirmed;
	public bool Confirmed => _confirmed?.Value ?? false;

	//Initialize PropertyHelper.
	_confirmed = Model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed).DisposeWith(Disposables);
	
	//Initialize PropertyHelper if invoked from not UI thread
	_confirmed = Model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, scheduler:RxApp.MainThreadScheduler).DisposeWith(Disposables);
```


## ReactiveCommand

Prefer binding user interactions to [commands](https://reactiveui.net/docs/guidelines/framework/commands) rather than methods.

```c#
	//Create Observable<bool> on MyProperty for CanExecute. Null detection always tricky but following code will handle that.
	IObservable<bool> isCoinListItemSelected = this.WhenAnyValue(x => x.MyProperty).Select(myProperty => myProperty != null);

	//Define command with CanExecute. 
	MyCommand = ReactiveCommand.Create(() =>
	{

	}, isCoinListItemSelected);

	//Define command from Task
	MySecondCommand = ReactiveCommand.CreateFromTask(async () =>
	{
		await Task.Delay(100);
	}, isCoinListItemSelected);
```


Reactive command doesnt have any unmanaged resources.
Dispose in Reactive command doesnt mean release unmanaged resources, it simply means unsubscribe.
Reactive command is on the same object that is subscribing, so GC will handle everything.
So no memory leak here.
[See this comment from the author of RxUI...](https://github.com/reactiveui/ReactiveUI/issues/20#issuecomment-1324201)

## AsyncLock

Use awaitable locks if possible. The library `Nito.AsyncEx` contains tools to do this.

```c#
	private readonly AsyncLock _myLock = new AsyncLock();

	using (await _myLock.LockAsync())
	{

	}
```

## Null check

```c#
	if (FeeService is null) return;
```

Use `is null` instead of `==null`. It was a performace consideration in the past but from C# 7.0 two operators behave the same. The Roslyn compiler has been updated to make the behavior of the two operator the same __when there is no overloaded equality operator__. [Please see the code in the current compiler results (M1 and M2 in the code)](http://tryroslyn.azurewebsites.net/#b:master/f:%3Eilr/K4Zwlgdg5gBAygTxAFwKYFsDcAoADsAIwBswBjGUogQxBBgGEYBvbGNmAge06JgFkAjAApOBAFapSyGAA8AlDAC8APlkwwdCMCJEcASC49+AJhHjJ0+UtUylimFp04AvkA==) that shows what happens when there is no overloaded equality comparer. They both now have the better performing == behavior. If there is an overloaded equality comparer, [the code still differs](https://stackoverflow.com/questions/40676426/what-is-the-difference-between-x-is-null-and-x-null).


## !!!WANTED!!! 10000 Dollar Reward Dead or Alive! 

DO NOT use PropertyChanged.

```c#
	private void Coin_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{			
		if (e.PropertyName == nameof(CoinViewModel.Unspent))	
		{	
		}
	}
```

DO NOT use event subscriptions.

```c#
	Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;

	if (Global.ChaumianClient != null)
	{
		Global.ChaumianClient.StateUpdated -= ChaumianClient_StateUpdated;
	}
```

DO NOT do anything in set except `RaiseAndSetIfChanged()`.

```c#
	public int FeeTarget
	{
		get => _feeTarget;
		set
		{
			this.RaiseAndSetIfChanged(ref _feeTarget, value);
			Global.UiConfig.FeeTarget = value;
		}
	}
```

DO NOT use [async void Method()](https://msdn.microsoft.com/en-us/magazine/jj991977.aspx).

```c#
	async void DoSomething()
	{
	
	}
```

DO NOT block with `.Result, .Wait(), .GetAwaiter().GetResult()` unless they are absolutely necessary.

```c#
	IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder).GetAwaiter().GetResult();
```

DO NOT use `==null`

```c#
	if (FeeService == null) return;
```

DO NOT use local variables in `.Subscribe()`.

```c#

	public MyConstructor(NodesCollection nodes)
	{
	
		Observable.FromEventPattern<NodeEventArgs>(myList, nameof(nodes.Removed))
			.Subscribe(x =>
			{
				Refresh(nodes.Count); //Bad code. Nodes should be a class member!
			}).DisposeWith(Disposables);
			
	}

```
