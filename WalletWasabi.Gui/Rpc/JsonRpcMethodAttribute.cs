using System;

namespace WalletWasabi.Gui.Rpc
{
	/// <summary>
	/// Class used to decorate service methods and map them with their rpc method name.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
	public sealed class JsonRpcMethodAttribute : Attribute
	{
		public string Name { get; }

		public JsonRpcMethodAttribute(string name)
		{
			Name = name;
		}
	}
}
