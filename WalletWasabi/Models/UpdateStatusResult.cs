using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class UpdateStatusResult
	{
		public bool ClientUpToDate { get; private set; }
		public bool BackendCompatible { get; private set; }

		public UpdateStatusResult(bool backendCompatible, bool clientUpToDate)
		{
			BackendCompatible = backendCompatible;
			ClientUpToDate = clientUpToDate;
		}
	}
}
