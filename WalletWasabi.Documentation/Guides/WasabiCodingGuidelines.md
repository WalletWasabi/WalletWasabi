
## Example ViewModel

```c#
  public class MyViewModel : ReactiveObject, IDisposable
	{
		private CompositeDisposable _disposables = new CompositeDisposable();
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
				.DisposeWith(_disposables);

			//need to detect property change in Model
			Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				})
				.DisposeWith(_disposables);

			Dispatcher.UIThread.PostLogException(() =>
			{
				//do something on the UI thread. App-crash exception handler built-in.
			});

			//event subscription
			Observable.FromEventPattern(
				Global.ChaumianClient,
				nameof(Global.ChaumianClient.StateUpdated))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
				}).DisposeWith(_disposables);

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
				.DisposeWith(_disposables);


		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_disposables?.Dispose();
				}
				_disposables = null;
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
    
## Event subsciptions

__traditional pattern DO NOT USE__

```c#
Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;

if (Global.ChaumianClient != null)
{
	Global.ChaumianClient.StateUpdated -= ChaumianClient_StateUpdated;
}
```
__the RX style__
```c#
Observable.FromEventPattern(CoinList, nameof(CoinList.SelectionChanged)).Subscribe(_ => SetFeesAndTexts());

Observable.FromEventPattern(
	Global.ChaumianClient,
	nameof(Global.ChaumianClient.StateUpdated))
	.ObserveOn(RxApp.MainThreadScheduler)
	.Subscribe(_ =>
	{
		RefreshSmartCoinStatus();
	}).DisposeWith(_disposables);
```
    
## Property definition and notifychange

#### WRONG!
```c#
private void Coin_PropertyChanged(object sender, PropertyChangedEventArgs e)
{			
	if (e.PropertyName == nameof(CoinViewModel.Unspent))	
	{	
	}
}
```
	
#### USE PropertyHelper!
```c#
//define class member
private ObservableAsPropertyHelper<bool> _confirmed;

//initilize PropertyHelper
_confirmed = Model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed).DisposeWith(_disposables);

//if invoked from other thread 
_confirmed = Model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, scheduler:RxApp.MainThreadScheduler).DisposeWith(_disposables);

//if we need to do something on change
this.WhenAnyValue(x => x.Confirmed, x => x.CoinJoinInProgress, x => x.Confirmations).Subscribe(_ => RefreshSmartCoinStatus());
```	




  
