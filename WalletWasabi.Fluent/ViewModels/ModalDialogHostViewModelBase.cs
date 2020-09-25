using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Popup;

namespace WalletWasabi.Fluent.ViewModels
{
	public class ModalDialogHostViewModelBase : ReactiveObject, IPopupHost
	{
		private static IPopupHost _host;
		private IPopupView _currentPopupView;
		public IPopupHost DialogHost => _host;

		private readonly ObservableAsPropertyHelper<bool> _canDisplayDialog;
		public bool CanDisplayDialog => _canDisplayDialog?.Value ?? false;

		public void SetHost(IPopupHost host)
		{
			if (_host is null)
			{
				_host = host;
			}
			else
			{
				throw new InvalidOperationException("The popup host has already been set.");
			}
		}

		public ModalDialogHostViewModelBase()
		{
			_canDisplayDialog = this
				.WhenAnyValue(x => x.CurrentPopupView)
				.Select(x => !(x is null))
				.ToProperty(this, x => x.CanDisplayDialog);
		}

		public IPopupView CurrentPopupView
		{
			get => _currentPopupView;
			set => this.RaiseAndSetIfChanged(ref _currentPopupView, value, nameof(CurrentPopupView));
		}

		public void SetDialog(IPopupView targetView)
		{
			if (CanDisplayDialog)
			{
				targetView.Parent = this;
				CurrentPopupView = targetView;
			}
		}

		public void Close()
		{
			CurrentPopupView = null;
		}
	}
}
