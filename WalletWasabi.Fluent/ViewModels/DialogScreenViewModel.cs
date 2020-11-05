using ReactiveUI;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.ViewModels
{
	public class DialogScreenViewModel : ViewModelBase, IScreen
	{
		private bool _isDialogVisible;

		public DialogScreenViewModel()
		{
			Observable.FromEventPattern(Router.NavigationStack, nameof(Router.NavigationStack.CollectionChanged))
				.Subscribe(_ => IsDialogVisible = Router.NavigationStack.Count >= 1);
		}

		public RoutingState Router { get; } = new RoutingState();

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		public bool IsDialogVisible
		{
			get => _isDialogVisible;
			set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
		}

		public void Close()
		{
			Router.NavigationStack.Clear();
		}
	}
}
