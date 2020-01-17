using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class LockScreenViewModelBase : ViewModelBase
	{
		private bool _isLocked;
		private bool _canSlide;

		private CompositeDisposable Disposables { get; }

		public LockScreenViewModelBase()
		{
			Disposables = new CompositeDisposable();

			var global = Locator.Current.GetService<Global>();

			global.UiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(this, y => y.IsLocked)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.BindTo(global.UiConfig, y => y.LockScreenActive)
				.DisposeWith(Disposables);

			IsLocked = global.UiConfig.LockScreenActive;

			OnInitialise(Disposables);
		}

		public bool CanSlide
		{
			get { return _canSlide; }
			set { this.RaiseAndSetIfChanged(ref _canSlide, value); }
		}

		public bool IsLocked
		{
			get => _isLocked;
			set => this.RaiseAndSetIfChanged(ref _isLocked, value);
		}

		protected virtual void OnInitialise(CompositeDisposable disposables)
		{

		}
	}
}
