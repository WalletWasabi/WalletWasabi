using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Helpers
{
	public struct ShellExecuteResult
	{
		public int ExitCode { get; set; }
		public string Output { get; set; }
		public string ErrorOutput { get; set; }
	}
}
