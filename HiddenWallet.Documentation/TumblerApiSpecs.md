# Notifications

| Type | Response   |
|------| -----------|
| new-phase  | Phase: InputRegistration/ ConnectionConfirmation/ OutputRegistration/ Signing |
| new-registration  | NumberOfPeers |


# Wallet

|API | Description    | Request body    | Response body   |
|--- | ---- | ---- | ---- |
|GET /api/v1/tumbler/status | Gets dynamic information on the Tumbler | None  | phase, denomination, anonymitySet, timeSpentInInputRegistration, maximumInputsPerAlices, feePerInputs, feePerOutputs, version |
|POST /api/v1/tumbler/inputs  | Alice registers her inputs to the Tumbler | inputs[(input, proof)], blindedOutput, changeOutput | signedBlindedOutput, uniqueId |
|GET /api/v1/tumbler/input-registration-status  | Gets dynamic information on the status of InputRegistration phase | None | registeredPeerCount, requiredPeerCount, elapsedSeconds |
|POST /api/v1/tumbler/connection-confirmation  | Alice confirms she's still connected to the Tumbler | uniqueId | None |
|POST /api/v1/tumbler/output  | Bob registers his output to the Tumbler | output, signature | None |
|GET /api/v1/tumbler/coinjoin  | Alice gets the CoinJoin transaction from the Tumbler | uniqueId | transaction |
|POST /api/v1/tumbler/signature  | Alice sends her signature to the Tumbler | uniqueId, signatures[(witness, index)] | None |
