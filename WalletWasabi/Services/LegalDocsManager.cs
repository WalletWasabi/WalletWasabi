using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Services
{
	public class LegalDocsManager : NotifyPropertyChangedBase
	{
		public readonly string LegalIssuesPath;
		public readonly string PrivacyPolicyPath;
		public readonly string TermsAndConditionsPath;

		public const string EmbeddedResourceLegalIssues = "WalletWasabi.Assets.LegalIssues.txt";
		public const string EmbeddedResourcePrivacyPolicy = "WalletWasabi.Assets.PrivacyPolicy.txt";
		public const string EmbeddedResourceTermsAndConditions = "WalletWasabi.Assets.TermsAndConditions.txt";

		public Version DownloadedLegalDocsVersion
		{
			get => RuntimeParams.Instance.DownloadedLegalDocsVersion;
			private set
			{
				if (RuntimeParams.Instance.DownloadedLegalDocsVersion != value)
				{
					RuntimeParams.Instance.DownloadedLegalDocsVersion = value;
					OnPropertyChanged(nameof(DownloadedLegalDocsVersion));
					OnPropertyChanged(nameof(IsLegalDocsAgreed));
				}
			}
		}

		public Version AgreedLegalDocsVersion
		{
			get => RuntimeParams.Instance.AgreedLegalDocsVersion;
			private set
			{
				if (RuntimeParams.Instance.AgreedLegalDocsVersion != value)
				{
					RuntimeParams.Instance.AgreedLegalDocsVersion = value;
					OnPropertyChanged(nameof(AgreedLegalDocsVersion));
					OnPropertyChanged(nameof(IsLegalDocsAgreed));
				}
			}
		}

		public bool IsLegalDocsAgreed
		{
			get
			{
				if (RuntimeParams.Instance.AgreedLegalDocsVersion == new Version(0, 0, 0, 0))
				{
					// LegalDocs was never agreed.
					return false;
				}

				// Check version except the Revision.
				return RuntimeParams.Instance.DownloadedLegalDocsVersion.ToVersion(3) <= RuntimeParams.Instance.AgreedLegalDocsVersion.ToVersion(3);
			}
		}

		public async Task SetDownloadedLegalDocsVersionAsync(Version newVersion)
		{
			DownloadedLegalDocsVersion = newVersion;

			if (RuntimeParams.Instance.AgreedLegalDocsVersion == AgreeFirstAutomatically)
			{
				RuntimeParams.Instance.AgreedLegalDocsVersion = RuntimeParams.Instance.DownloadedLegalDocsVersion;
			}

			await RuntimeParams.Instance.SaveAsync();
		}

		public async Task SetAgreedLegalDocsVersionAsync(Version newVersion)
		{
			AgreedLegalDocsVersion = newVersion;
			await RuntimeParams.Instance.SaveAsync();
		}

		public static async Task EnsureCompatiblityAsync(string jsonString)
		{
			// Ensure that users who already Agreed the legal docs won't be bothered after updating.
			if (JsonHelpers.TryParseJToken(jsonString, out JToken token))
			{
				var addressString = token["AgreedLegalDocsVersion"]?.ToString()?.Trim() ?? null;
				if (addressString is null)
				{
					// The file is there but the string is missing so the client was installed before and legal docs was agreed.
					RuntimeParams.Instance.AgreedLegalDocsVersion = AgreeFirstAutomatically;
					await RuntimeParams.Instance.SaveAsync();
				}
			}
		}

		public async Task EnsureLegalDocsExistAsync()
		{
			IoHelpers.EnsureContainingDirectoryExists(LegalIssuesPath);
			if (File.Exists(LegalIssuesPath) && File.Exists(TermsAndConditionsPath) && File.Exists(PrivacyPolicyPath))
			{
				return;
			}

			using (FileStream fs = new FileStream(LegalIssuesPath, FileMode.Create))
			{
				await EmbeddedResourceHelper.GetResourceAsync(EmbeddedResourceLegalIssues, fs);
			}
			using (FileStream fs = new FileStream(PrivacyPolicyPath, FileMode.Create))
			{
				await EmbeddedResourceHelper.GetResourceAsync(EmbeddedResourcePrivacyPolicy, fs);
			}
			using (FileStream fs = new FileStream(TermsAndConditionsPath, FileMode.Create))
			{
				await EmbeddedResourceHelper.GetResourceAsync(EmbeddedResourceTermsAndConditions, fs);
			}

			await SetDownloadedLegalDocsVersionAsync(Constants.LegalDocsVersion);
		}

		private static readonly Version AgreeFirstAutomatically = new Version(9999, 9999, 9999, 9999);

		public LegalDocsManager(string workFolderPath)
		{
			LegalIssuesPath = Path.Combine(workFolderPath, "LegalIssues.txt");
			PrivacyPolicyPath = Path.Combine(workFolderPath, "PrivacyPolicy.txt");
			TermsAndConditionsPath = Path.Combine(workFolderPath, "TermsAndConditions.txt");
		}
	}
}
