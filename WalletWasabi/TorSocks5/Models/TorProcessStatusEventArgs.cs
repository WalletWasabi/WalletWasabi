using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;

namespace WalletWasabi.TorSocks5
{
	internal class TorProcessStatusEventArgs : EventArgs
	{
		public TorProcessState OldStatus { get; }
		public TorProcessState NewStatus { get; }

		public TorProcessStatusEventArgs(TorProcessState oldStatus, TorProcessState newStatus)
		{
			OldStatus = oldStatus;
			NewStatus = newStatus;
		}
	}
}
