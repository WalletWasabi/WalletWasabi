
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
			//need to detect propertyChance at class member https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions
			this.WhenAnyValue(x => x.MyProperty)
				.Subscribe(myProperty =>
				{
				});

			//need to bind Model property in View
			_confirmed = Model.WhenAnyValue(x => x.Confirmed)
				.ToProperty(this, x => x.MyConfirmed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			//need to detect property change in Model
			Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				})
				.DisposeWith(Disposables);

			Dispatcher.UIThread.PostLogException(() =>
			{
				//do something on the UI thread. App-crash exception handler built-in.
			});

			//event subscription http://blog.functionalfun.net/2012/03/weak-events-in-net-easy-way.html
			Observable.FromEventPattern(
				Global.ChaumianClient,
				nameof(Global.ChaumianClient.StateUpdated))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				}).DisposeWith(Disposables);

			//Create Observable<bool> on MyProperty for canExecute
			IObservable<bool> isCoinListItemSelected = this.WhenAnyValue(x => x.MyProperty).Select(myProperty => myProperty != null);

			//create command
			MyCommand = ReactiveCommand.Create(() =>
			{

			}, isCoinListItemSelected);

			//catch command exceptions
			MyCommand.ThrownExceptions
				.Subscribe(ex => Logging.Logger.LogWarning<MyViewModel>(ex));

			//create command from Task
			MySecondCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await Task.Delay(100);
			}, isCoinListItemSelected);

			//Merge multiply events and Throttle stops the flow of events until no more events are produced for a specified period of time.
			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed)))			//http://rxwiki.wikidot.com/101samples#toc47
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed)))
				.Throttle(TimeSpan.FromSeconds(5))																					//http://rxwiki.wikidot.com/101samples#toc30
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => 
				{

				})
				.DisposeWith(Disposables);


		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}
				Disposables = null;
				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

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

You can put the following into the contructor of the ViewModel. MyProperty should use __this.RaiseAndSetIfChanged__ to notify the change. If you're a VM and you e.g. WhenAnyValue against your own property, there's [no need to clean that up because](https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions) that is manifested as the VM having a reference to itself.

```c#
	this.WhenAnyValue(x => x.MyProperty)
		.Subscribe(myProperty =>
		{
		});
```

If you are referencing other object you should dispose your subscription. Use __.DisposeWith__ method.

```c#
	Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
		.ObserveOn(RxApp.MainThreadScheduler)
		.Subscribe(_ =>
		{
		})
		.DisposeWith(Disposables);
```
    
## Event subsciptions

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

## Expose Model Property to View

When a property's value depends on another property, a set of properties, or an observable stream, rather than set the value explicitly, [use __ObservableAsPropertyHelper__ with __WhenAny__ wherever possible](https://reactiveui.net/docs/guidelines/framework/prefer-oaph-over-properties).


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

DO NOT do anything in set except RaiseAndSetIfChanged().

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

DO NOT do anything in set except RaiseAndSetIfChanged().

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

