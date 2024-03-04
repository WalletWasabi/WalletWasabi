using System.Collections.Generic;
using System.Reflection;

namespace WalletWasabi.Rpc;

/// <summary>
/// Represents the collection of metadata needed to execute the remote procedure.
/// </summary>
public class JsonRpcMethodMetadata
{
	public JsonRpcMethodMetadata(string name, MethodInfo mi, bool ri, List<(string name, Type type, bool isOptional, object defaultValue)> parameters)
	{
		Name = name;
		RequiresInitialization = ri;
		MethodInfo = mi;
		Parameters = parameters;
	}

	// The name of the remote procedure. This is NOT the name of the method to be invoked.
	public string Name { get; }
	public MethodInfo MethodInfo { get; }
	public bool RequiresInitialization { get; }
	public List<(string name, Type type, bool isOptional, object defaultValue)> Parameters { get; }
}
