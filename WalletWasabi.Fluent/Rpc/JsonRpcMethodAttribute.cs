using System;

namespace WalletWasabi.Fluent.Rpc
{
	/// <summary>
	/// Class used to decorate service methods and map them with their rpc method name.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
	public sealed class JsonRpcMethodAttribute : Attribute
	{
		public JsonRpcMethodAttribute(string name)
		{
			Name = name;
		}

		public string Name { get; }
	}
}
