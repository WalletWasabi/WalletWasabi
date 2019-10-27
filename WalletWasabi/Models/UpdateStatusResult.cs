using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	public class UpdateStatusResult
	{
		public bool ClientUpToDate { get; private set; }
		public bool LegalDocsRevisionUpToDate { get; }
		public bool LegalDocsUpToDate { get; }
		public bool BackendCompatible { get; private set; }
		public Version LegalDocsBackendVersion { get; private set; }

		public UpdateStatusResult(Version clientVersion, int backendMajorVersion, Version legalDocsVersion)
		{
			// If the client version locally is greater than or equal to the backend's reported client version, then good.
			ClientUpToDate = Constants.ClientVersion >= clientVersion;

			// If the backend major and the client major are equal, then our softwares are compatible.
			BackendCompatible = int.Parse(Constants.BackendMajorVersion) == backendMajorVersion;

			// If the legal documents updated with small fixes like typos - the user won't be bothered with the agreement procedure.
			LegalDocsRevisionUpToDate = RuntimeParams.Instance.DownloadedLegalDocsVersion >= legalDocsVersion;

			// If the legal documents updated and need to be agreed again.
			LegalDocsUpToDate = RuntimeParams.Instance.DownloadedLegalDocsVersion.ToVersion(3) >= legalDocsVersion.ToVersion(3);

			LegalDocsBackendVersion = legalDocsVersion;
		}
	}
}
