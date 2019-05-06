using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

			Assert.False(File.Exists(ioman1.DigestFilePath));

			// Write the data to the file.

			await ioman1.WriteAllLinesAsync(lines);
			Assert.True(ioman1.Exists());

			// Check if the digest file is created.

			Assert.True(File.Exists(ioman1.DigestFilePath));

			// Read back the content and check.

			var readLines = await ioman1.ReadAllLinesAsync();

			Assert.Equal(readLines.Length, lines.Count);

			for (int i = 0; i < lines.Count; i++)
			{
				string line = lines[i];
				var readline = readLines[i];

				Assert.Equal(readline, line);
			}

			// Check digest file, and write only differ logic.

			using (File.OpenWrite(ioman1.OriginalFilePath))
			{
				// Should be OK because the same data is written.
				await ioman1.WriteAllLinesAsync(lines);
			}

			lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

			using (File.OpenWrite(ioman1.OriginalFilePath))
			{
				// Should fail because different data is written.
				await Assert.ThrowsAsync<IOException>(async () => await ioman1.WriteAllLinesAsync(lines));
			}

			await ioman1.WriteAllLinesAsync(lines);

			// Mutex tests.

			// Acquire the Mutex.

			var myTask = Task.Run(async () =>
			{
				await ioman1.WrapInMutexAsync(async () =>
				{
					await Task.Delay(3000);
				});
			});

			// Wait for the Task.Run to Acquire the Mutex.
			await Task.Delay(100);

			// Try to get the Mutex and save the time.
			DateTime timeOfstart = DateTime.Now;
			DateTime timeOfAcquired = default;

			await ioman1.WrapInMutexAsync(() =>
			{
				timeOfAcquired = DateTime.Now;
			});

			var elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));
		}
	}
}
