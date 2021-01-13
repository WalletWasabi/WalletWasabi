using System;

namespace WalletWasabi.Hwi.Models
{
	/// <summary>
	/// This attribute is used to represent command names as expected to be received by HWI.
	/// </summary>
	/// <example>For <see cref="HwiCommands.GetMasterXpub"/>, we want to send <c>getmasterxpub</c> to HWI.</example>
	public class CommandNameAttribute : Attribute
	{
		public CommandNameAttribute(string value)
		{
			CommandName = value;
		}

		public string CommandName { get; }
	}
}