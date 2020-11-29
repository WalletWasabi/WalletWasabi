using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public abstract class RoutableViewModel : ViewModelBase, INavigatable
	{
		private bool _isBusy;
		private CompositeDisposable? _currentDisposable;

		public NavigationTarget CurrentTarget { get; internal set; }

		public virtual NavigationTarget DefaultTarget => NavigationTarget.HomeScreen;

		protected RoutableViewModel()
		{
			BackCommand = ReactiveCommand.Create(()=>Navigate().Back());

			//CancelCommand = ReactiveCommand.Create(Clear);
		}

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public ICommand? NextCommand { get; protected set; }

		public ICommand BackCommand { get; protected set; }

		public ICommand CancelCommand { get; protected set; }

		private void DoNavigateTo(bool inStack)
		{
			if (_currentDisposable is { })
			{
				throw new Exception("Cant navigate to something that has already been navigated to.");
			}

			_currentDisposable = new CompositeDisposable();

			OnNavigatedTo(inStack, _currentDisposable);
		}

		protected virtual void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
		}

		private void DoNavigateFrom()
		{
			OnNavigatedFrom();

			_currentDisposable?.Dispose();
			_currentDisposable = null;
		}

		public INavigationManager<RoutableViewModel> Navigate()
		{
			var currentTarget = CurrentTarget == NavigationTarget.Default ? DefaultTarget : CurrentTarget;

			return Navigate(currentTarget);
		}

		public INavigationManager<RoutableViewModel> Navigate(NavigationTarget currentTarget)
		{
			switch (currentTarget)
			{
				case NavigationTarget.HomeScreen:
					return NavigationState.Instance.HomeScreenNavigation;

				case NavigationTarget.DialogScreen:
					return NavigationState.Instance.DialogScreenNavigation;
			}

			throw new NotSupportedException();
		}

		public void OnNavigatedTo(bool isInHistory)
		{
			DoNavigateTo(isInHistory);
		}

		void INavigatable.OnNavigatedFrom(bool isInHistory)
		{
			DoNavigateFrom();
		}

		protected virtual void OnNavigatedFrom()
		{
		}

		public async Task<TResult> NavigateDialog<TResult>(DialogViewModelBase<TResult> dialog)
			=> await NavigateDialog(dialog, CurrentTarget);

		public async Task<TResult> NavigateDialog<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target)
		{
			TResult result = default;

			Navigate(target).To(dialog);

			result = await dialog.GetDialogResultAsync();

			Navigate(target).Back();

			return result;
		}

		//public IDisposable NavigateTo(RoutableViewModel viewModel, bool resetNavigation = false) =>
		//	NavigateTo(viewModel, CurrentTarget, resetNavigation);

		/*public IDisposable NavigateTo(RoutableViewModel viewModel, NavigationTarget navigationTarget, bool resetNavigation = false)
		{
			if (navigationTarget == NavigationTarget.Default)
			{
				navigationTarget = DefaultTarget;
			}

			switch (navigationTarget)
			{
				case NavigationTarget.Default:
				case NavigationTarget.HomeScreen:
					{
						NavigateToScreen(NavigationState.Instance.HomeScreen(), NavigationTarget.HomeScreen, viewModel, resetNavigation);
					}
					break;

				case NavigationTarget.DialogScreen:
					{
						NavigateToScreen(NavigationState.Instance.DialogScreen(), navigationTarget, viewModel, resetNavigation);
					}
					break;

				case NavigationTarget.DialogHost:
					if (viewModel is DialogViewModelBase dialog)
					{
						NavigateToDialogHost(dialog);
					}
					break;

				default:
					break;
			}

			return Disposable.Create(()=>GoBack());
		}

		private void NavigateToScreen(IScreen screen, NavigationTarget target, RoutableViewModel viewModel, bool resetNavigation)
		{
			viewModel.CurrentTarget = target;

			if (resetNavigation)
			{
				ClearStack(screen.Router.NavigationStack.ToList());
			}
			else
			{
				if (screen.Router.GetCurrentViewModel() is RoutableViewModel rvm)
				{
					rvm.DoNavigateFrom();
				}
			}

			var command = resetNavigation ?
				screen.Router.NavigateAndReset :
				screen.Router.Navigate;

			var inStack = screen.Router.NavigationStack.Contains(viewModel);

			if (resetNavigation)
			{
				inStack = false;
			}

			command.Execute(viewModel);

			viewModel.DoNavigateTo(inStack);
		}*/

		private void NavigateToDialogHost(DialogViewModelBase dialog)
		{
			dialog.CurrentTarget = NavigationTarget.DialogHost;

			if (NavigationState.Instance.DialogHost() is IDialogHost dialogHost)
			{
				if (dialogHost.CurrentDialog is RoutableViewModel rvm)
				{
					rvm.DoNavigateFrom();
				}

				dialogHost.CurrentDialog = dialog;

				dialog.DoNavigateTo(false);
			}
		}

		//public void NavigateToSelf() => NavigateToSelf(NavigationTarget.Default);

		//public void NavigateToSelf(NavigationTarget target) => NavigateTo(this, target, resetNavigation: false);

		//public void NavigateToSelfAndReset(NavigationTarget target) => NavigateTo(this, target, resetNavigation: true);
	}
}
