﻿using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Reactive.Disposables;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase, IDisposable
	{
		private TransactionInfo _model;
		private CompositeDisposable Disposables { get; }

		public TransactionViewModel(TransactionInfo model)
		{
			Disposables = new CompositeDisposable();
			_model = model;

			_confirmed = model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, model.Confirmed).DisposeWith(Disposables);
		}

		private readonly ObservableAsPropertyHelper<bool> _confirmed;

		public string DateTime
		{
			get { return _model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
		}

		public bool Confirmed
		{
			get { return _confirmed.Value; }
		}

		public string AmountBtc
		{
			get => _model.AmountBtc;
		}

		public Money Amount
		{
			get => Money.TryParse(_model.AmountBtc, out Money money) ? money : Money.Zero;
		}

		public string Label
		{
			get => _model.Label;
		}

		public string TransactionId
		{
			get => _model.TransactionId;
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

				_model = null;
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
