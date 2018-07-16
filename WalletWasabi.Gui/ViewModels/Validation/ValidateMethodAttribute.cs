using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class ValidateMethodAttribute : Attribute
	{
		private readonly string _methodName;

		public ValidateMethodAttribute(string methodName)
		{
			_methodName = methodName;
		}

		public string MethodName
		{
			get { return _methodName; }
		}
	}
}
