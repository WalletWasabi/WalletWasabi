using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Tabs
{
	internal class ReportBugViewModel : WasabiDocumentTabViewModel, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		public ReportBugViewModel() : base("Report Bug")
		{
			Disposables = new CompositeDisposable();

			CopyUrlCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					Application.Current.Clipboard.SetTextAsync(IssuesURL).GetAwaiter().GetResult();
				}
				catch (Exception)
				{
					// Apparently this exception sometimes happens randomly.
					// The MS controls just ignore it, so we'll do the same.
				}
			}).DisposeWith(Disposables);
		}

		public string IssuesURL => "http://github.com/zkSNACKs/WalletWasabi/issues";

		public ReactiveCommand CopyUrlCommand { get; }

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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
