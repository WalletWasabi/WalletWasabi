# The agreed procedure to deploy something to production

1. Create a summary of what is going to be deployed. A link to the PRs labeled as `Affiliate` could be enough.
2. Announce in the Integrations Slack channel the deployment of one specific commit to testnet, using the @here tag, and share the summary.
3. Affiliates must acknowledge the notification.
4. Affiliates must express whether they are willing to test what has been deployed or not.
   1. In case Affiliates **are not** interested in testing it, then the Wasabi team will notify the planned deployment date to production, and that's it.
   2. In case Affiliates **are** interested in testing it, they must:
      1. Let the Wasabi team know how much time they need to test it.
      2. Share their findings. In case there is any, then iterate.
      3. Give the Wasabi team the final approval.
      4. The Wasabi team and the Affiliates must agree on a release date for production deployment.
5. Don't forget to make the discussion in the maintenance repository with a short summary.

# How to deploy

1. Create a tag that identifies the commit to be deployed. The naming format is `version-codename-iteration`, where `version` is the official version number (for example, v2.0.3.1), `codename` an identifier of what is being deployed (for example, `wgpt`, `wabisabi`, `affsvr` or any other), and `iteration` a sequence number to identify a sequence of deployments (first iteration doesn't need to have this number). For example:
   - `v2.0.3-wabisabi` is the tag that was used to deploy the backend with the external WabiSabi library.
   - `v2.0.2.1-as` is the tag that was used to deploy the backend containing the **a**filiation **s**erver integration.
   - `v2.0.2.1-as-1` is the tag that was used to deploy the affiliation server integration again.
   - `v2.0.2.1-as-2` is the tag that was used to deploy the affiliation server integration once more.
   - `v2.0.3wgpt` (now it should be named as `v2.0.3-wgpt`) is the tag that was used to deploy the version containing the gpt-compatible version.

   Note: As you can see, the deployer can choose the `codename` freely.

   - Creating a tag: `git tag v2.0.3-myexampletag`
   - Push the tag to upstream: `git push upstream v2.0.3-myexampletag`

3. Deploy using the `deploy.sh` script.

# Templates for communication

**Deploy to TestNet example**

Hello there, we are deploying this commit to backend servers.
- Latest commit on backend: 167c81be80d8d3de9deaf8d306017c5403593c89
- Planning to deploy to backend: 460e21ce71738d3cc1560a3d4fc1984cc4beb725
- PRs with affiliate label: https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+is%3Aclosed+label%3Aaffiliate

Please ack and test.

@Kuba Ondracek
