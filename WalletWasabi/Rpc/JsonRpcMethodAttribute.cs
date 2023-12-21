namespace WalletWasabi.Rpc;

/// <summary>
/// Class used to decorate service methods and map them with their rpc method name.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class JsonRpcMethodAttribute : Attribute
{
	public JsonRpcMethodAttribute(string name, bool initializable = true)
	{
		Name = name;
		Initializable = initializable;
	}

	public string Name { get; }
	public bool Initializable { get; }
}
