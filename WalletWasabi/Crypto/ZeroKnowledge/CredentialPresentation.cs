using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	/// <summary>
	/// Represents a randomized credential that can be presented to the coordinator.
	/// A randomized credential is a tuple that consists of four group elements (Ca, Cx0, Cx1, CV).
	/// </summary>
	public class CredentialPresentation
	{
		[JsonConstructor]
		internal CredentialPresentation(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV, GroupElement S)
		{
			this.Ca = Ca;
			this.Cx0 = Cx0;
			this.Cx1 = Cx1;
			this.CV = CV;
			this.S = S;
		}

		/// <summary>
		/// Randomized amount commitment component.
		/// </summary>
		public GroupElement Ca { get; }

		/// <summary>
		/// Randomized MAC's U component.
		/// </summary>
		public GroupElement Cx0 { get; }

		/// <summary>
		/// Randomized MAC's (t * U) component.
		/// </summary>
		public GroupElement Cx1 { get; }

		/// <summary>
		/// Randomized MAC's V component.
		/// </summary>
		public GroupElement CV { get; }

		/// <summary>
		/// Credential's randomness hidden behind DL component.
		/// </summary>
		public GroupElement S { get; }

		/// <summary>
		/// Computes the Z element.
		/// </summary>
		/// <param name="sk">The coordinator's secret key.</param>
		/// <returns>The Z element needed to verify that a randomized credential's proof is valid.</returns>
		public GroupElement ComputeZ(CoordinatorSecretKey sk)
			=> CV - (sk.W * Generators.Gw + sk.X0 * Cx0 + sk.X1 * Cx1 + sk.Ya * Ca);
	}
}
