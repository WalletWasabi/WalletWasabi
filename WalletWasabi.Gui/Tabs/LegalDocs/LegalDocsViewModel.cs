using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Tabs.LegalDocs
{
	[Export]
	[Shared]
	internal class LegalDocsViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		public ReactiveCommand<Unit, Unit> AcceptTermsCommand { get; }
		private ObservableAsPropertyHelper<bool> _isLegalDocsAgreed;
		public bool IsLegalDocsAgreed => _isLegalDocsAgreed?.Value ?? false;

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
				await RuntimeParams.Instance.SaveAsync(); //TODO: remove comment.
				OnClose();
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

		public override void OnOpen()
		{
			base.OnOpen();
			Disposables =
				Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			_isLegalDocsAgreed = RuntimeParams.Instance
				.WhenAnyValue(x => x.IsLegalDocsAgreed)
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.IsLegalDocsAgreed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);
		}

		public override bool OnClose()
		{
			foreach (var category in Categories.OfType<TextResourceViewModelBase>())
			{
				category.Dispose();
			}

			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectTermsAndConditions();
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectPrivacyPolicy();
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectLegalIssues();
		}
	}
}
