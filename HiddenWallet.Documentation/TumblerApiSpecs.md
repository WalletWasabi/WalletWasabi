# Notifications

| Type | Response   |
|------| -----------|
| new-phase  | Phase: InputRegistration/ InputConfirmation/ OutputRegistration/ Signing |


# Wallet

|API | Description    | Request body    | Response body   |
|--- | ---- | ---- | ---- |
|GET /api/v1/tumbler/status/{public key hash} | Gets dynamic information on the Tumbler | None  | pubKey, phase, denomination, anonymitySet, timeSpentInInputRegistrationLastTime |
|POST /api/v1/tumbler/inputs  | Alice registers her inputs to the Tumbler | inputs[(input, proof)], blindedOutput, changeOutput | signedBlindedOutput, uniqueId |
|GET /api/v1/tumbler/input-registration-status  | Gets dynamic information on the status of InputRegistration phase | None | registeredPeerCount, requiredPeerCount, elapsedTime |
|POST /api/v1/tumbler/connection-confirmation  | Alice confirms she's still connected to the Tumbler | uniqueId | None |
|POST /api/v1/tumbler/output  | Bob registers his output to the Tumbler | signedOutput | None |
|GET /api/v1/tumbler/coinjoin  | Alice gets the CoinJoin transaction from the Tumbler |  | transaction |
|POST /api/v1/tumbler/signature  | Alice sends her signature to the Tumbler | signature | None |
