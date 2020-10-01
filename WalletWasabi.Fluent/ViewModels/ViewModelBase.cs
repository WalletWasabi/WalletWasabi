using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
    public class ViewModelBase : ReactiveObject, IRoutableViewModel
	{
		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen { get; set; }
	}
}
