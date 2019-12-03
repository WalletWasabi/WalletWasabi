using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Services.LoadStatusReporting
{
	public class LoadStatusReport
	{
		public LoadStatusReport(LoadStatus status)
		{
			Status = status;
		}

		public LoadStatus Status { get; }
	}
}
