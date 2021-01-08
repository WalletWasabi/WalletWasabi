using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class LegalChecker : IDisposable
	{
		private bool _disposedValue;

		public LegalChecker(string dataDir)
		{
			DataDir = dataDir;
		}

		private AsyncLock LegalDocumentLock { get; } = new();
		private UpdateChecker? UpdateChecker { get; set; }
		private string DataDir { get; }
		public LegalDocuments? CurrentLegalDocument { get; private set; }
		private LegalDocuments? NewLegalDocument { get; set; }
		private string? NewLegalContent { get; set; }

		public void Initialize(UpdateChecker updateChecker)
		{
			UpdateChecker = updateChecker;
			UpdateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChangedAsync;
			CurrentLegalDocument = LegalDocuments.TryLoadAgreed(DataDir);
		}

		public bool IsAgreementRequired([NotNullWhen(true)] out LegalDocuments? legalDocuments)
		{
			legalDocuments = null;

			if (NewLegalDocument is { } legal)
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
				if (UpdateChecker is null)
				{
					return;
				}

				using (await LegalDocumentLock.LockAsync().ConfigureAwait(false))
				{
					// If we don't have it or there is a new one.
					if (CurrentLegalDocument is null || CurrentLegalDocument.Version < updateStatus.LegalDocumentsVersion)
					{
						var legalFolderPath = Path.Combine(DataDir, LegalDocuments.LegalFolderName);
						var filePath = Path.Combine(legalFolderPath, $"{updateStatus.LegalDocumentsVersion}.txt");

						// Store the content, in case of agreement it will be saved to file.
						NewLegalContent = await UpdateChecker.WasabiClient.GetLegalDocumentsAsync(CancellationToken.None).ConfigureAwait(false);

						NewLegalDocument = new LegalDocuments(filePath);
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
				if (NewLegalDocument is not { } newLegalDocument || string.IsNullOrEmpty(NewLegalContent))
				{
					throw new InvalidOperationException("Cannot agree the new legal document.");
				}

				await newLegalDocument.ToFileAsync(NewLegalContent).ConfigureAwait(false);

				CurrentLegalDocument = NewLegalDocument;
				NewLegalDocument = null;
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
