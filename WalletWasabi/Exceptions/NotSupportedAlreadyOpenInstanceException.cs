using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Exceptions
{
	public class NotSupportedAlreadyOpenInstanceException : NotSupportedException
	{
		public NotSupportedAlreadyOpenInstanceException(string typeName)
			: base($"Cannot open {typeName} before closing it.")
		{
		}
	}
}
