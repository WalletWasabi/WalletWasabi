using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public partial class SelectCharsetViewModel : RoutableViewModel
	{
		[AutoNotify] private Charset? _selectedCharset;

		public SelectCharsetViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";

			this.WhenAnyValue(x => x.SelectedCharset)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x is null)
					{
						return;
					}

					options.Charset = (Charset)x;
					Navigate().To(new ContainsNumbersViewModel(options));

					SelectedCharset = null;
				});
		}

		public IEnumerable<Charset> Charsets => Enum.GetValues(typeof(Charset)).Cast<Charset>();
	}
}