using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepParams
	{
		public KnowledgeOfRepParams(IEnumerable<Scalar> secrets, LegacyStatement statement)
		{
			Guard.NotNullOrEmpty(nameof(secrets), secrets);
			var secretsCount = secrets.Count();
			IEnumerable<GroupElement> generators = statement.Generators;
			var generatorsCount = generators.Count();
			if (secretsCount != generatorsCount)
			{
				const string NameofGenerators = nameof(generators);
				const string NameofSecrets = nameof(secrets);
				throw new InvalidOperationException($"Must provide exactly as many {NameofGenerators} as {NameofSecrets}. {NameofGenerators}: {generatorsCount}, {NameofSecrets}: {secretsCount}.");
			}

			var publicPointSanity = statement.PublicPoint;
			foreach (var (secret, generator) in secrets.ZipForceEqualLength<Scalar, GroupElement>(generators))
			{
				Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
				Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
				publicPointSanity -= secret * generator;
			}

			if (publicPointSanity != GroupElement.Infinity)
			{
				throw new InvalidOperationException($"{nameof(statement.PublicPoint)} was incorrectly constructed.");
			}

			Secrets = secrets;
			Statement = statement;
		}

		public IEnumerable<Scalar> Secrets { get; }
		public LegacyStatement Statement { get; }

		public IEnumerable<(Scalar, GroupElement)> SecretGeneratorPairs => Secrets.ZipForceEqualLength(Statement.Generators);
	}
}
