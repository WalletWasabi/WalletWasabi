using System;
using System.Collections.Generic;
using System.Reflection;

namespace WalletWasabi.Gui.Rpc
{
	///<summary>
	/// Represents the collection of metadata needed to execute the remote procedure.
	///</summary>
	public class JsonRpcMethodMetadata
	{
		// The name of the remote procedure. This is NOT the name of the method to be invoked.
		public string Name { get; }

		public MethodInfo MethodInfo { get; }
		public List<(string name, Type type)> Parameters { get; }

		public JsonRpcMethodMetadata(string name, MethodInfo mi, List<(string name, Type type)> parameters)
		{
			Name = name;
			MethodInfo = mi;
			Parameters = parameters;
		}
	}
}
