using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class WasabiSignerToolsTests
{
	private Key _privateKey = WasabiSignerTools.GenerateKey();
	private DirectoryInfo _tmpFolder = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "tmp"));

	[Fact]
	public void TestWritingAndSavingSHASums()
	{
		List<string> filenames = new();
		string destinationPath = Path.Combine(_tmpFolder.FullName, WasabiSignerTools.SHASumsFileName);
		//WasabiSignerTools.WriteAndSaveSHASumsFile(filenames, destinationPath, _privateKey);
		File.WriteAllText(Path.Combine(_tmpFolder.FullName, "tmp.txt"), "temp");

		string a = "10";
	}
}
