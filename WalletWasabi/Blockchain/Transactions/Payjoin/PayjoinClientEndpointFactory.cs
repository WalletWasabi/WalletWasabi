using Microsoft.AspNetCore.WebUtilities;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Blockchain.Transactions.Payjoin
{
	public class PayjoinClientEndpointFactory
	{
		/// <summary>
		/// Construct final payjoin endpoint according to <see href="https://github.com/bitcoin/bips/blob/master/bip-0078.mediawiki"/>.
		/// </summary>
		/// <param name="baseUri">Base URI.</param>
		/// <param name="optionalParameters">Optional parameters as specified in <see cref="https://github.com/bitcoin/bips/blob/master/bip-0078.mediawiki#optional-parameters"/>.</param>
		/// <returns>An instance of <see cref="Uri"/> whose original query is overwritten by <paramref name="optionalParameters"/>.</returns>
		public Uri ConstructEndpoint(Uri baseUri, PayjoinClientParameters optionalParameters)
		{
			var parameters = new Dictionary<string, string>
			{
				{ "v", optionalParameters.Version.ToString() }
			};

			if (optionalParameters.AdditionalFeeOutputIndex is int additionalFeeOutputIndex)
			{
				parameters.Add("additionalfeeoutputindex", additionalFeeOutputIndex.ToString(CultureInfo.InvariantCulture));
			}
			if (optionalParameters.DisableOutputSubstitution is bool disableoutputsubstitution)
			{
				parameters.Add("disableoutputsubstitution", disableoutputsubstitution ? "true" : "false");
			}
			if (optionalParameters.MaxAdditionalFeeContribution is Money maxAdditionalFeeContribution)
			{
				parameters.Add("maxadditionalfeecontribution", maxAdditionalFeeContribution.Satoshi.ToString(CultureInfo.InvariantCulture));
			}
			if (optionalParameters.MinFeeRate is FeeRate minFeeRate)
			{
				parameters.Add("minfeerate", minFeeRate.SatoshiPerByte.ToString(CultureInfo.InvariantCulture));
			}

			// Remove query from endpoint.
			var builder = new UriBuilder(baseUri);
			builder.Query = "";

			// Construct final URI.
			return new Uri(QueryHelpers.AddQueryString(builder.Uri.AbsoluteUri, parameters));
		}

		public PayjoinClientParameters BuildOptionalParameters(PSBT psbt, HdPubKey changeHdPubKey)
		{
			var parameters = new PayjoinClientParameters();
			if (changeHdPubKey is { })
			{
				var changeOutput = psbt.Outputs.FirstOrDefault(x => x.ScriptPubKey == changeHdPubKey.P2wpkhScript);

				if (changeOutput is PSBTOutput o)
				{
					parameters.AdditionalFeeOutputIndex = (int)o.Index;
				}
			}

			if (!psbt.TryGetEstimatedFeeRate(out var originalFeeRate))
			{
				throw new ArgumentException("Original transaction should have UTXO information.", nameof(psbt));
			}

			// By default, we want to keep same fee rate and a single additional input.
			parameters.MaxAdditionalFeeContribution = originalFeeRate.GetFee(Constants.P2wpkhInputVirtualSize);
			parameters.DisableOutputSubstitution = false;

			return parameters;
		}
	}
}
