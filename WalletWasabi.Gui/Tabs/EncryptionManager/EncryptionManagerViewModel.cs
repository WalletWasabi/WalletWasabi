using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using System;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class EncryptionManagerViewModel : WasabiDocumentTabViewModel, IDisposable
	{
		public enum Tabs
		{
			Sign,
			Verify,
			Encrypt,
			Decrypt
		}

		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;

		public EncryptionManagerViewModel() : base("Encryption Manager")
		{
			Categories = new ObservableCollection<CategoryViewModel>
			{
				new SignMessageViewModel(this),
				new VerifyMessageViewModel(this),
				new EncryptMessageViewModel(this),
				new DecryptMessageViewModel(this),
			};

			SelectedCategory = Categories.FirstOrDefault();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();

				CurrentView = category;
			});
		}

		public ObservableCollection<CategoryViewModel> Categories
		{
			get { return _categories; }
			set { this.RaiseAndSetIfChanged(ref _categories, value); }
		}

		public CategoryViewModel SelectedCategory
		{
			get { return _selectedCategory; }
			set { this.RaiseAndSetIfChanged(ref _selectedCategory, value); }
		}

		public void SelectTab(Tabs toSelect, string content = null)
		{
			switch (toSelect)
			{
				case Tabs.Sign:
					{
						var vmsign = Categories.First(x => x is SignMessageViewModel) as SignMessageViewModel;
						vmsign.Address = content;
						SelectedCategory = vmsign;
					}
					break;

				case Tabs.Verify:
					{
						var vmverify = Categories.First(x => x is VerifyMessageViewModel) as VerifyMessageViewModel;
						vmverify.Address = content;
						SelectedCategory = vmverify;
					}
					break;

				case Tabs.Encrypt:
					{
						var vmencrypt = Categories.First(x => x is EncryptMessageViewModel) as EncryptMessageViewModel;
						vmencrypt.PublicKey = content;
						SelectedCategory = vmencrypt;
					}
					break;

				case Tabs.Decrypt:
					{
						var vmdecrypt = Categories.First(x => x is DecryptMessageViewModel) as DecryptMessageViewModel;
						vmdecrypt.MyPublicKey = content;
						SelectedCategory = vmdecrypt;
					}
					break;
			}
		}

		public ViewModelBase CurrentView
		{
			get { return _currentView; }
			set { this.RaiseAndSetIfChanged(ref _currentView, value); }
		}

		public override bool OnClose()
		{
			Dispose();
			return base.OnClose();
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					foreach (var cat in Categories.OfType<IDisposable>())
					{
						cat.Dispose();
					}
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
