using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Legal;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class LegalTests
	{
		[Fact]
		public async Task CanCreateLegalDocumentsAsync()
		{
			var version = new Version(1, 1);
			var legal = new LegalDocuments(version, "");
			var dir = Common.GetWorkDir();
			await legal.ToFileAsync(dir);
			var filePath = Directory.EnumerateFiles(Path.Combine(dir, LegalChecker.LegalFolderName), "*.*", SearchOption.TopDirectoryOnly).Single();
			Assert.Equal("1.1.txt", Path.GetFileName(filePath));
			Assert.Equal(version, legal.Version);
		}

		[Fact]
		public async Task CantLoadNotAgreedAsync()
		{
			var dir = Common.GetWorkDir();
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			Assert.True(Directory.Exists(dir));
			Assert.Empty(Directory.GetFiles(dir));
			var legalDir = Assert.Single(Directory.GetDirectories(dir));
			Assert.Empty(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task LeavesTrashAloneAsync()
		{
			var dir = Common.GetWorkDir();
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			// Leaves one trash alone.
			var trash1 = File.Create(Path.Combine(legalDir, "foo"));
			await trash1.DisposeAsync();
			res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));

			// Leaves 3 trash alone.
			var trash2 = File.Create(Path.Combine(legalDir, "foo.txt"));
			var trash3 = File.Create(Path.Combine(legalDir, "foo2"));
			await trash2.DisposeAsync();
			await trash3.DisposeAsync();
			res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			Assert.Equal(3, Directory.GetFiles(legalDir).Length);
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task ResolvesConflictsAsync()
		{
			var dir = Common.GetWorkDir();
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			// Deletes them if multiple legal docs found.
			var candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
			var candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
			await candidate1.DisposeAsync();
			await candidate2.DisposeAsync();
			res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			Assert.Empty(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));

			// Only the candidates are deleted.
			var trash = File.Create(Path.Combine(legalDir, "1.txt"));
			candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
			candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
			await trash.DisposeAsync();
			await candidate1.DisposeAsync();
			await candidate2.DisposeAsync();
			res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
		}

		[Fact]
		public async Task CanLoadLegalDocsAsync()
		{
			var dir = Path.Combine(Common.GetWorkDir(), LegalChecker.LegalFolderName);
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			var res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.Null(res);
			var legalDir = Assert.Single(Directory.GetDirectories(dir));

			var version = new Version(1, 1);

			// Deletes them if multiple legal docs found.
			var candidate = File.Create(Path.Combine(legalDir, $"{version}.txt"));
			await candidate.DisposeAsync();
			res = await LegalDocuments.TryLoadAgreedAsync(dir);
			Assert.NotNull(res);
			Assert.Single(Directory.GetFiles(legalDir));
			Assert.Empty(Directory.GetDirectories(legalDir));
			Assert.Equal(version, res?.Version);
		}

		[Fact]
		public async Task CanSerializeFileAsync()
		{
			Version version = new(1, 1);
			LegalDocuments legal = new(version, "Don't trust, verify!");
			string legalFolderPath = Path.Combine(Common.GetWorkDir(), LegalChecker.LegalFolderName);
			await legal.ToFileAsync(legalFolderPath);

			string expectedFilePath = $"{Path.Combine(legalFolderPath, version.ToString())}.txt";
			Assert.True(File.Exists(expectedFilePath));
		}
	}
}
