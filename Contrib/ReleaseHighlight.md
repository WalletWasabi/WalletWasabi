## Release Highlights
🔒 Set minimum coinjoin input count
🔒 Stricter absolute limits for maximum coinjoin mining fee rate and minimum coinjoin input count
🔒 Prevent solo coinjoining
👀 Coordinator Connection String
👀 Advanced send Workflow
## Release Summary

Wasabi Wallet v2.1.0.0 adds safeguards for coinjoin participants.
Clients can now set their own policies regarding minimum input count and maximum coordination fee to protect from malicious coordinators which tries to charge more than they announce.

### Apply coinjoin coordination policies to all rounds:

It was possible for a malicious coordinator to bypass the client policies for blame round and set different coordination fees for those.
v2.1.0.0 fixes this issue by enforcing the policies to blame rounds too.

### Stricter safeguards:

Wasabi clients use more restrictive policies that guarantee that even in presence of a malicious coordinator, the risk of losing money is mitigated. These are: absolute maximum coordination fee rate changed from 0.01 to 0.005, absolute minimum input count changed from 2 to 5.

### Prevent signing coinjoin where there is only one participant:

A malicious coordinator could set the minimum input count as a very low number to create coinjoins where all the inputs belong to the same user. In v2.1.0.0 clients don't sign transaction where there are no other participants.

### Coordinator Connection String

Changing coordinator has never been easier! Coordinators can create a connection string that once in the clipboard will be recognized by Wasabi and allow the client to be quickly configured to coinjoin with it.

### Advanced send Workflow

The sending whole coin feature has been replaced by a new Manual Control feature to allow users to specify which coins can be used for sending.
