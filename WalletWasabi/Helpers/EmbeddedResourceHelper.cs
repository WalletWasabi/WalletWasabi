using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class EmbeddedResourceHelper
	{
		private static Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
		}

		public static async Task GetResourceAsync(string resourceName, Stream destinationStream, string assemblyName = "WalletWasabi")
		{
			using var stream = GetAssemblyByName(assemblyName).GetManifestResourceStream(resourceName);

			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(destinationStream);
		}
	}
}
