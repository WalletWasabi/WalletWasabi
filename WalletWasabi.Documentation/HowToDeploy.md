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

1. run `./build-wasabi <REVISION>`
2. run `./deploy-wasabi`

# Templates for communication

**Deploy to TestNet example**

Hello there, we are deploying this commit to the `TestNet` server.
- Latest commit on indexer: 167c81be80d8d3de9deaf8d306017c5403593c89
- Planning to deploy to indexer: 460e21ce71738d3cc1560a3d4fc1984cc4beb725
- PRs with affiliate label: https://github.com/WalletWasabi/WalletWasabi/pulls?q=is%3Apr+is%3Aclosed+label%3Aaffiliate

Please ack and test.

-------

## Scripts details

### build-wasabi script

This script builds wasabi indexer and it is defined as follow:

```bash
$ echo "#!/usr/bin/env bash" > build-wasabi
$ echo "nix build -o wasabi-indexer github:walletwasabi/walletwasabi/\$1" >> build-wasabi
```

### deploy-wasabi script

This script deploys the already built wasabi indexer and it is defined as follow:

```bash
$ echo "#!/usr/bin/env bash" > deploy-wasabi
$ echo "./wasabi-indexer/deploy" >> deploy-wasabi
```
