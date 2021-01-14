using Nito.AsyncEx;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class LegalChecker : IDisposable
	{
		public const string LegalFolderName = "Legal";
		public const string ProvisionalLegalFolderName = "Provisional";

		private bool _disposedValue;

		public LegalChecker(string dataDir)
		{
			LegalFolder = Path.Combine(dataDir, LegalFolderName);
			ProvisionalLegalFolder = Path.Combine(LegalFolder, ProvisionalLegalFolderName);
		}

		private AsyncLock LegalDocumentLock { get; } = new();
		private UpdateChecker? UpdateChecker { get; set; }
		public string LegalFolder { get; }
		public string ProvisionalLegalFolder { get; }
		public LegalDocuments? CurrentLegalDocument { get; private set; }
		private LegalDocuments? ProvisionalLegalDocument { get; set; }

		public async Task InitializeAsync(UpdateChecker updateChecker)
		{
			UpdateChecker = updateChecker;
			UpdateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChangedAsync;
			CurrentLegalDocument = await LegalDocuments.TryLoadAgreedAsync(LegalFolder).ConfigureAwait(false);
			ProvisionalLegalDocument = await LegalDocuments.TryLoadAgreedAsync(ProvisionalLegalFolder).ConfigureAwait(false);
		}

		public bool TryGetNewLegalDocs([NotNullWhen(true)] out LegalDocuments? legalDocuments)
		{
			legalDocuments = null;

			if (ProvisionalLegalDocument is { } legal)
			{
				legalDocuments = legal;
				return true;
			}

			return false;
		}

		private async void UpdateChecker_UpdateStatusChangedAsync(object? _, UpdateStatus updateStatus)
		{
			try
			{
				using (await LegalDocumentLock.LockAsync().ConfigureAwait(false))
				{
					// If we don't have it or there is a new one.
					if (CurrentLegalDocument is null || CurrentLegalDocument.Version < updateStatus.LegalDocumentsVersion)
					{
						// UpdateChecker cannot be null as the event called by it.
						var content = await UpdateChecker!.WasabiClient.GetLegalDocumentsAsync(CancellationToken.None).ConfigureAwait(false);

						// Save it as a provisional legal document.
						var prolegal = new LegalDocuments(updateStatus.LegalDocumentsVersion, content);
						await prolegal.ToFileAsync(ProvisionalLegalFolder).ConfigureAwait(false);

						ProvisionalLegalDocument = prolegal;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not get legal documents.", ex);
			}
		}

		public async Task AgreeAsync()
		{
			using (await LegalDocumentLock.LockAsync().ConfigureAwait(false))
			{
				if (ProvisionalLegalDocument is not { } provisionalLegalDocument || string.IsNullOrEmpty(provisionalLegalDocument.Content))
				{
					throw new InvalidOperationException("Cannot agree the new legal document.");
				}

				await provisionalLegalDocument.ToFileAsync(LegalFolder).ConfigureAwait(false);
				LegalDocuments.RemoveCandidates(ProvisionalLegalFolder);

				CurrentLegalDocument = ProvisionalLegalDocument;
				ProvisionalLegalDocument = null;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (UpdateChecker is { } updateChecker)
					{
						updateChecker.UpdateStatusChanged -= UpdateChecker_UpdateStatusChangedAsync;
					}
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
