using System;
using ReactiveUI;
using System.Reactive.Disposables;
using WalletWasabi.Helpers;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using Splat;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class LockScreenViewModelBase : ViewModelBase
	{
		private bool _isLocked;
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
		}
		
		public bool IsLocked
		{
			get => _isLocked;
			set => this.RaiseAndSetIfChanged(ref _isLocked, value);
		}

		protected abstract void OnInitialise(CompositeDisposable disposables);
	}
}
