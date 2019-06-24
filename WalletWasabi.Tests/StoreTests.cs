using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Stores;
using Xunit;
using Xunit.Sdk;

namespace WalletWasabi.Tests
{
	public class StoreTests
	{
		[Fact]
		public void HashChainTests()
		{
			var hashChain = new HashChain();

			// ASSERT PROPERTIES

			// Assert everything is default value.
			AssertEverythingDefault(hashChain);

			// ASSERT EVENTS

			// Assert some functions doesn't raise any events when default.
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashCount),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast doesn't modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast doesn't modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast doesn't modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHash),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast doesn't modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert RemoveLast doesn't modify nor throw anything when nothing is added.
						hashChain.RemoveLast();
						AssertEverythingDefault(hashChain);
					}));

			// Assert the correct events are thrown and not thrown when applicable.
			var newServerHeight = hashChain.ServerTipHeight + 1;
			Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					() =>
					{
						// ASSERT FUNCTION
						// Assert update server height raises.
						hashChain.UpdateServerTipHeight(newServerHeight);
					});
			newServerHeight++;
			Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					() =>
					{
						// ASSERT FUNCTION
						// Assert update server height raises.
						hashChain.UpdateServerTipHeight(newServerHeight);
					});

			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashCount),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert update server height doesn't raise unnecessary events.
						hashChain.UpdateServerTipHeight(newServerHeight);
					}));
			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHash),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert update server height doesn't raise unnecessary events.
						hashChain.UpdateServerTipHeight(newServerHeight);
					}));
			newServerHeight++;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.TipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert update server height doesn't raise unnecessary events.
						hashChain.UpdateServerTipHeight(newServerHeight);
					}));
			var sameServerheight = newServerHeight;
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.ServerTipHeight),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert update server height doesn't raise without actually changing.
						hashChain.UpdateServerTipHeight(sameServerheight);
					}));
			Assert.Throws<PropertyChangedException>(() =>
				Assert.PropertyChanged(hashChain,
					nameof(hashChain.HashesLeft),
					() =>
					{
						// ASSERT FUNCTIONS
						// Assert update server height doesn't raise without actually changing.
						hashChain.UpdateServerTipHeight(sameServerheight);
					}));

			// ASSERT PROPERTIES
			Assert.Equal(0, hashChain.HashCount);
			var hashesLeft = sameServerheight;
			Assert.Equal(hashesLeft, hashChain.HashesLeft);
			Assert.Equal(hashesLeft, hashChain.ServerTipHeight);
			Assert.Null(hashChain.TipHash);
			Assert.Equal(0, hashChain.TipHeight);
		}

		private static void AssertEverythingDefault(HashChain hashChain)
		{
			Assert.Equal(0, hashChain.HashCount);
			Assert.Equal(0, hashChain.HashesLeft);
			Assert.Equal(0, hashChain.ServerTipHeight);
			Assert.Null(hashChain.TipHash);
			Assert.Equal(0, hashChain.TipHeight);
		}

		[Fact]
		public async Task IndexStoreTestsAsync()
		{
			var indexStore = new IndexStore();

			var dir = Path.Combine(Global.Instance.DataDir, nameof(IndexStoreTestsAsync));
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network, new HashChain());
		}

		[Fact]
		public async Task AsyncMutexTestsAsync()
		{
			AsyncMutex asyncMutex = new AsyncMutex("mutex1");

			// Cannot be IDisposable because the pattern is like Nito's AsyncLock.
			Assert.False(asyncMutex is IDisposable);

			// Use the mutex two times after each other.
			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			// Release the Mutex from another thread.

			var disposable = await asyncMutex.LockAsync();

			var myThread = new Thread(new ThreadStart(() =>
			 {
				 disposable.Dispose();
			 }));
			myThread.Start();
			myThread.Join();

			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			// Acquire the Mutex with a background thread.

			var myTask = Task.Run(async () =>
			{
				using (await asyncMutex.LockAsync())
				{
					await Task.Delay(3000);
				}
			});

			// Wait for the Task.Run to Acquire the Mutex.
			await Task.Delay(100);

			// Try to get the Mutex and save the time.
			DateTime timeOfstart = DateTime.Now;
			DateTime timeOfAcquired = default;

			using (await asyncMutex.LockAsync())
			{
				timeOfAcquired = DateTime.Now;
			};

			Assert.True(myTask.IsCompletedSuccessfully);

			var elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));

			// Standard Mutex test.
			int cnt = 0;
			List<int> numbers = new List<int>();
			var rand = new Random();
			async Task TestLockAsync()
			{
				using (await asyncMutex.LockAsync())
				{
					cnt++;

					await Task.Delay(rand.Next(5));
					numbers.Add(cnt);
				}
			}

			var tasks = new List<Task>();

			for (int i = 0; i < 100; i++)
			{
				var task = TestLockAsync();

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			Assert.Equal(100, numbers.Count);
			for (int i = 1; i < 100; i++)
			{
				var prevnum = numbers[i - 1];
				var num = numbers[i];
				Assert.Equal(prevnum + 1, num);
			}

			// Test that asynclock cancellation is going to throw IOException.
			var mutex = new AsyncMutex("foo");
			using (await mutex.LockAsync())
			{
				using (var cts = new CancellationTokenSource(100))
				{
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await mutex.LockAsync(cts.Token))
						{
						}
					});
				}
			}

			// Test same mutex gets same asynclock.
			var mutex1 = new AsyncMutex("foo");
			using (await mutex1.LockAsync())
			{
				using (var cts = new CancellationTokenSource(100))
				{
					var mutex2 = new AsyncMutex("foo");
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await mutex2.LockAsync(cts.Token))
						{
						}
					});
				}
			}

			// Different AsyncMutex object but same name.
			AsyncMutex asyncMutex2 = new AsyncMutex("mutex1");

			// Acquire the first mutex with a background thread and hold it for a while.
			var myTask2 = Task.Run(async () =>
			{
				using (await asyncMutex.LockAsync())
				{
					await Task.Delay(3000);
				}
			});

			// Make sure the task started.
			await Task.Delay(100);

			timeOfstart = DateTime.Now;
			timeOfAcquired = default;
			// Now try to acquire another AsyncMutex object but with the same name!
			using (await asyncMutex2.LockAsync())
			{
				timeOfAcquired = DateTime.Now;
			}

			await myTask2;
			Assert.True(myTask2.IsCompletedSuccessfully);

			elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));
		}

		[Fact]
		public async Task IoManagerTestsAsync()
		{
			var file1 = Path.Combine(Global.Instance.DataDir, nameof(IoManagerTestsAsync), $"file1.dat");
			var file2 = Path.Combine(Global.Instance.DataDir, nameof(IoManagerTestsAsync), $"file2.dat");

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

			bool IsStringArraysEqual(string[] lines1, string[] lines2)
			{
				if (lines1.Length != lines2.Length) return false;

				for (int i = 0; i < lines1.Length; i++)
				{
					string line = lines2[i];
					var readline = lines1[i];

					if (!line.Equals(readline)) return false;
				}
				return true;
			}

			var readLines = await ioman1.ReadAllLinesAsync();

			Assert.True(IsStringArraysEqual(readLines, lines.ToArray()));

			// Check digest file, and write only differ logic.

			// Write the same content, file should not be written.
			var currentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			await Task.Delay(500);
			await ioman1.WriteAllLinesAsync(lines);
			var noChangeDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			Assert.Equal(currentDate, noChangeDate);

			// Write different content, file should be written.
			currentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			await Task.Delay(500);
			lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
			await ioman1.WriteAllLinesAsync(lines);
			var newContentDate = File.GetLastWriteTimeUtc(ioman1.FilePath);
			Assert.NotEqual(currentDate, newContentDate);

			/* The next test is commented out because on mac and on linux File.Open does not lock the file
			 * it can be still written by the ioman1.WriteAllLinesAsync(). Tried with FileShare.None FileShare.Delete
			 * FileStream.Lock none of them are working or caused not supported on this platform exception.
			 * So there is no OP system way to garantee that the file won't be written during another write operation.
			 * For example git is using lock files to solve this problem. We are using system wide mutexes.
			 * For now there is no other way to do this. Some useful links :
			 * https://stackoverflow.com/questions/2751734/how-do-filesystems-handle-concurrent-read-write
			 * https://github.com/dotnet/corefx/issues/5964
			 */

			//using (File.OpenWrite(ioman1.OriginalFilePath))
			//{
			//	// Should be OK because the same data is written.
			//	await ioman1.WriteAllLinesAsync(lines);
			//}
			//using (File.OpenWrite(ioman1.OriginalFilePath))
			//{
			//	// Should fail because different data is written.
			//	await Assert.ThrowsAsync<IOException>(async () => await ioman1.WriteAllLinesAsync(lines));
			//}

			await ioman1.WriteAllLinesAsync(lines);

			// Mutex tests.

			// Acquire the Mutex with a background thread.

			var myTask = Task.Run(async () =>
			{
				using (await ioman1.Mutex.LockAsync())
				{
					await Task.Delay(3000);
				}
			});

			// Wait for the Task.Run to Acquire the Mutex.
			await Task.Delay(100);

			// Try to get the Mutex and save the time.
			DateTime timeOfstart = DateTime.Now;
			DateTime timeOfAcquired = default;

			using (await ioman1.Mutex.LockAsync())
			{
				timeOfAcquired = DateTime.Now;
			}

			Assert.True(myTask.IsCompletedSuccessfully);

			var elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));

			// Simulate file write error and recovery logic.

			// We have only *.new and *.old files.
			File.Copy(ioman1.FilePath, ioman1.OldFilePath);
			File.Move(ioman1.FilePath, ioman1.NewFilePath);

			// At this point there is now OriginalFile.

			var newFile = await ioman1.ReadAllLinesAsync();

			Assert.True(IsStringArraysEqual(newFile, lines.ToArray()));

			// Add one more line to have different data.
			lines.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");

			await ioman1.WriteAllLinesAsync(lines);

			// Check recovery mechanism.

			Assert.True(
				File.Exists(ioman1.FilePath) &&
				!File.Exists(ioman1.OldFilePath) &&
				!File.Exists(ioman1.NewFilePath));

			ioman1.DeleteMe();

			Assert.False(ioman1.Exists());

			// Check if directory is empty.

			var fileCount = Directory.EnumerateFiles(Path.GetDirectoryName(ioman1.FilePath)).Count();
			Assert.Equal(0, fileCount);

			// Check Mutex usage on simultaneous file writes.

			IoManager ioman2 = new IoManager(file2);

			await Task.Run(async () =>
			{
				using (await ioman1.Mutex.LockAsync())
				{
					// Should not be a problem because they using different Mutexes.
					using (await ioman2.Mutex.LockAsync())
					{
						await ioman1.WriteAllLinesAsync(lines);
						await ioman2.WriteAllLinesAsync(lines);
						ioman1.DeleteMe();
						ioman2.DeleteMe();
					}
				}
			});

			// TryReplace test.
			var dummyFilePath = $"{ioman1.FilePath}dummy";
			var dummyContent = new string[]
			{
				"banana",
				"peach"
			};
			await File.WriteAllLinesAsync(dummyFilePath, dummyContent);

			await ioman1.WriteAllLinesAsync(lines);

			ioman1.TryReplaceMeWith(dummyFilePath);

			var fruits = await ioman1.ReadAllLinesAsync();

			Assert.True(IsStringArraysEqual(dummyContent, fruits));

			Assert.False(File.Exists(dummyFilePath));

			ioman1.DeleteMe();
		}

		[Fact]
		public async Task IoTestsAsync()
		{
			var file = Path.Combine(Global.Instance.DataDir, nameof(IoTestsAsync), $"file.dat");

			IoManager ioman = new IoManager(file);
			ioman.DeleteMe();
			await ioman.WriteAllLinesAsync(new string[0], dismissNullOrEmptyContent: false);

			string RandomString()
			{
				StringBuilder builder = new StringBuilder();
				var rnd = new Random();
				char ch;
				for (int i = 0; i < rnd.Next(10, 100); i++)
				{
					ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * rnd.NextDouble() + 65)));
					builder.Append(ch);
				}
				return builder.ToString();
			};

			var list = new List<string>();
			async Task WriteNextLineAsync()
			{
				var next = RandomString();
				lock (list)
				{
					list.Add(next);
				}
				using (await ioman.Mutex.LockAsync())
				{
					var lines = (await ioman.ReadAllLinesAsync()).ToList();
					lines.Add(next);
					await ioman.WriteAllLinesAsync(lines);
				}
			};

			var t1 = new Thread(() =>
			{
				for (var i = 0; i < 500; i++)
				{
					/* We have to block the Thread.
					 * If we use async/await pattern then Join() function at the end will indicate that the Thread is finished -
					 * which is not true bacause the WriteNextLineAsync() is not yet finished. The reason is that await will return execution
					 * the to the calling thread it is detected as the thread is done. t1 and t2 and t3 will still run in parallel!
					 */
					WriteNextLineAsync().Wait();
				}
			});
			var t2 = new Thread(() =>
			{
				for (var i = 0; i < 500; i++)
				{
					WriteNextLineAsync().Wait();
				}
			});
			var t3 = new Thread(() =>
			{
				for (var i = 0; i < 500; i++)
				{
					WriteNextLineAsync().Wait();
				}
			});

			t1.Start();
			t2.Start();
			t3.Start();
			await Task.Delay(100);
			t1.Join();
			t2.Join();
			t3.Join();
			Assert.False(t1.IsAlive);
			Assert.False(t2.IsAlive);
			Assert.False(t3.IsAlive);

			var allLines = File.ReadAllLines(file);
			Assert.NotEmpty(allLines);

			/* Lines were added to the list and to the file parallel so the two data should be equal.
			 * If we "substract" them from each other we should get empty array.
			 */

			var diff = allLines.Except(list);
			Assert.Empty(diff);
		}
	}
}
