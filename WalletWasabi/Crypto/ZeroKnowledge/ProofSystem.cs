using NBitcoin.Secp256k1;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ProofSystem
	{
		private static GroupElement Inf = GroupElement.Infinity;
		private static Transcript Transcript => new Transcript(Encoding.UTF8.GetBytes("proof-of-parameters"));

		public static LinearRelation.Statement CreateStatement(CoordinatorParameters coordinatorParameters, GroupElement V, GroupElement Ma, Scalar t) =>
			new LinearRelation.Statement(
				new GroupElementVector(coordinatorParameters.Cw, Generators.GV - coordinatorParameters.I, V).Zip(ProofOfParametersGenerators(MAC.GenerateU(t), Ma, t), 
					(publicPoint, groupElement) => new Equation (publicPoint, groupElement)));

		public static NonInteractive.FiatShamirTransform.VerifierCommitToNonces CreateVerifier(LinearRelation.Statement statement) =>
			new NonInteractive.FiatShamirTransform.Verifier(statement).CommitToStatements(Transcript);

		public static NonInteractive.FiatShamirTransform.ProverCommitToNonces CreateProver(LinearRelation.Statement statement, CoordinatorSecretKey coordinatorSecretKey) =>
			new NonInteractive.FiatShamirTransform.Prover(statement.ToKnowledge(coordinatorSecretKey)).CommitToStatements(Transcript);

		private static GroupElementVector[] ProofOfParametersGenerators(GroupElement U, GroupElement Ma, Scalar t) =>
			new GroupElementVector[]
			{
				// coordinator's iparams W, Wp, X0, X1, Ya
				new GroupElementVector( Generators.Gw, Generators.Gwp, Inf, Inf, Inf ),
				new GroupElementVector( Inf, Inf, Generators.Gx0, Generators.Gx1, Generators.Ga ),
				new GroupElementVector( Generators.Gw, Inf, U, t * U, Ma )
			};

		private static LinearRelation.Knowledge ToKnowledge(this LinearRelation.Statement statement, CoordinatorSecretKey coordinatorSecretKey) =>
			statement.ToKnowledge(new ScalarVector(
				coordinatorSecretKey.W,
				coordinatorSecretKey.Wp,
				coordinatorSecretKey.X0,
				coordinatorSecretKey.X1,
				coordinatorSecretKey.Ya));
	}
}