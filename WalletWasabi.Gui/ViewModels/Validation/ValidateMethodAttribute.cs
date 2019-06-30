using System;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class ValidateMethodAttribute : Attribute
	{
		public ValidateMethodAttribute(string methodName)
		{
			MethodName = methodName;
		}

		public string MethodName { get; }
	}
}
