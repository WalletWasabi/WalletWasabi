using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests
{
	public class StoreTests
	{
		[Fact]
		public async Task IndexStoreTestsAsync()
		{
			var indexStore = new IndexStore();

			var dir = Path.Combine(Global.DataDir, nameof(IndexStoreTestsAsync));
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network);
		}

		[Fact]
		public async Task IoManagerTestsAsync()
		{
			var file1 = Path.Combine(Global.DataDir, nameof(IoManagerTestsAsync), $"file1.dat");
			var file2 = Path.Combine(Global.DataDir, nameof(IoManagerTestsAsync), $"file2.dat");

			Random random = new Random();
			List<string> lines = new List<string>();
			for (int i = 0; i < 1000; i++)
			{
				const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

				string line = new string(Enumerable.Repeat(chars, 100)
				  .Select(s => s[random.Next(s.Length)]).ToArray());

				lines.Add(line);
			}

			// Single thread file operations.

			IoManager ioman1 = new IoManager(file1);

			// Delete the file if Exist.

			ioman1.DeleteMe();
			Assert.False(ioman1.Exists());

			// Write the data to the file.

			await ioman1.WriteAllLinesAsync(lines);
			Assert.True(ioman1.Exists());

			// Read back the content and check.

			var readLines = await ioman1.ReadAllLinesAsync();

			Assert.Equal(readLines.Length, lines.Count);

			for (int i = 0; i < lines.Count; i++)
			{
				string line = lines[i];
				var readline = readLines[i];

				Assert.Equal(readline, line);
			}
		}
	}
}
