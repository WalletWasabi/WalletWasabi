using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.QualityGate.Git.Processes
{
	public class GitException : Exception
	{
		public GitException(string message) : base(message)
		{
		}
	}
}
