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

		private static GroupElementVector[] ProofOfParametersGenerators(GroupElement U, GroupElement Ma, Scalar t) =>
			new GroupElementVector[]
			{
				new GroupElementVector( Generators.Gw, Generators.Gwp, Inf, Inf, Inf, Inf ),
				new GroupElementVector( Inf, Inf, Generators.Gx0, Generators.Gx1, Inf, Generators.Ga ),
				new GroupElementVector( Generators.Gw, Inf, U, t*U, Inf, Ma )
			};

		public static LinearRelation.Statement CreateStatement(GroupElement Cw, GroupElement I, GroupElement V, GroupElement U, GroupElement Ma, Scalar t) =>
			new LinearRelation.Statement(new[] {Cw, Generators.GV - I, V}.Zip(ProofOfParametersGenerators(U, Ma, t), (publicPoint, groupElement) 
				=> new Equation (publicPoint, groupElement)));

		public static NonInteractive.FiatShamirTransform.VerifierCommitToNonces CreateVerifier(LinearRelation.Statement statement) =>
			new NonInteractive.FiatShamirTransform.Verifier(statement).CommitToStatements(Transcript);

		public static NonInteractive.FiatShamirTransform.ProverCommitToNonces CreateProver(LinearRelation.Knowledge knowledge) =>
			new NonInteractive.FiatShamirTransform.Prover(knowledge).CommitToStatements(Transcript);
	}
}