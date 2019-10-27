using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Tabs.LegalDocs
{
	[Export]
	[Shared]
	internal class LegalDocsViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		public ReactiveCommand<Unit, Unit> AcceptTermsCommand { get; }

		[ImportingConstructor]
		public LegalDocsViewModel(AvaloniaGlobalComponent global) : base(global.Global, "Legal documents")
		{
			Categories = new ObservableCollection<CategoryViewModel>
				{
					new LegalIssuesViewModel(Global),
					new PrivacyPolicyViewModel(Global),
					new TermsAndConditionsViewModel(Global)
				};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();

				CurrentView = category;
			});

			AcceptTermsCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				RuntimeParams.Instance.AgreedLegalDocsVersion = RuntimeParams.Instance.DownloadedLegalDocsVersion;
				await RuntimeParams.Instance.SaveAsync();
			});

			AcceptTermsCommand.ThrownExceptions.Subscribe((ex) => Logging.Logger.LogWarning(ex));
		}

		public ObservableCollection<CategoryViewModel> Categories
		{
			get => _categories;
			set => this.RaiseAndSetIfChanged(ref _categories, value);
		}

		public CategoryViewModel SelectedCategory
		{
			get => _selectedCategory;
			set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
		}

		public void SelectLegalIssues()
		{
			SelectedCategory = Categories.First(x => x is LegalIssuesViewModel);
		}

		public void SelectPrivacyPolicy()
		{
			SelectedCategory = Categories.First(x => x is PrivacyPolicyViewModel);
		}

		public void SelectTermsAndConditions()
		{
			SelectedCategory = Categories.First(x => x is TermsAndConditionsViewModel);
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}

		public override bool OnClose()
		{
			foreach (var category in Categories.OfType<TextResourceViewModelBase>())
			{
				category.Dispose();
			}
			return base.OnClose();
		}
	}
}
