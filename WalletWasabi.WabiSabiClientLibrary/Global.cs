using System.Linq;
using System.Reflection;

namespace WalletWasabi.WabiSabiClientLibrary;

public class Global
{
	public Global()
	{
		CommitHash = GetMetadataAttribute("CommitHash");
		Version = GetMetadataAttribute("Version");
		Debug = bool.Parse(GetMetadataAttribute("Debug"));
	}

	private static string GetMetadataAttribute(string name)
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		AssemblyMetadataAttribute[] metadataAttributes = (AssemblyMetadataAttribute[])assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
		return metadataAttributes.Where(x => x.Key == name).Select(x => x.Value).Single() ?? throw new NullReferenceException();
	}

	public string Version { get; }
	public string CommitHash { get; }
	public bool Debug { get; }
}
