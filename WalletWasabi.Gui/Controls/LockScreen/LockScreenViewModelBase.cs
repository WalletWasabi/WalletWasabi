using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class LockScreenViewModelBase : ViewModelBase
	{
		private bool _isLocked;
		private bool _canSlide;

		public LockScreenViewModelBase()
		{
			Disposables = new CompositeDisposable();

			this.WhenAnyValue(x => x.IsLocked)
				.Where(x => x)
				.Take(1)
				.Subscribe(x =>
				{
					OnInitialize(Disposables);
				});
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

		protected virtual void OnInitialize(CompositeDisposable disposables)
		{
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
