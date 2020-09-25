using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	public class ModalDialogHostViewModelBase : ReactiveObject, IDialogHost
	{
		private static IDialogHost _host;
		private IDialogView _currentPopupView;
		public static IDialogHost DialogHost => _host;

		private readonly ObservableAsPropertyHelper<bool> _canDisplayDialog;
		public bool CanDisplayDialog => _canDisplayDialog?.Value ?? false;

		public void SetHost(IDialogHost host)
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

		public IDialogView CurrentPopupView
		{
			get => _currentPopupView;
			set => this.RaiseAndSetIfChanged(ref _currentPopupView, value, nameof(CurrentPopupView));
		}

		public void SetDialog(IDialogView targetView)
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
