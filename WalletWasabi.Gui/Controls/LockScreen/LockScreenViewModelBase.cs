using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class LockScreenViewModelBase : ViewModelBase
	{
		private bool _isAnimating;
		private bool _isLocked;
		private bool _canSlide;
		private volatile bool _disposedValue = false; // To detect redundant calls

		public LockScreenViewModelBase()
		{
			Disposables = new CompositeDisposable();

			IsLocked = true;
		}

		protected CompositeDisposable Disposables { get; }

		public bool CanSlide
		{
			get => _canSlide;
			set => this.RaiseAndSetIfChanged(ref _canSlide, value);
		}

		public bool IsLocked
		{
			get => _isLocked;
			set => this.RaiseAndSetIfChanged(ref _isLocked, value);
		}

		public bool IsAnimating
		{
			get => _isAnimating;
			set => this.RaiseAndSetIfChanged(ref _isAnimating, value);
		}

		public void Initialize()
		{
			OnInitialize(Disposables);
		}

		protected virtual void OnInitialize(CompositeDisposable disposables)
		{
		}

		protected void Close()
		{
			IsLocked = false;

			this.WhenAnyValue(x => x.IsAnimating)
				.Where(x => !x)
				.Take(1)
				.Subscribe(x => MainWindowViewModel.Instance?.CloseLockScreen(this));
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
