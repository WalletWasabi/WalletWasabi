# Supplemental Android trust anchor

`isrgrootx1.pem` is the public, self-signed ISRG Root X1 certificate downloaded
from [the issuing CA](https://letsencrypt.org/certs/isrgrootx1.pem). Its DER SHA-256
fingerprint is:

```text
96bcec06264976f37460779acf28c5a7cfe8a3c0aae11a8ffcee05c0bddf08c6
```

The Android adapter verifies this fingerprint and supplements system validation
only on API 24/25 when the sole failure is a missing trust anchor. It rebuilds
the full chain with normal verification flags and preserved server-authentication
usage. Hostname mismatch, expiration, signature errors and unrelated roots remain
rejected. It does not install a device certificate or change the platform's
preexisting trust anchors.

Android's declarative network-security configuration does not supplement the
pinned .NET chain builder: its native code explicitly reads `AndroidCAStore`.
The adapter therefore applies the supplement to the managed HTTP factories,
including coordinator clients and factories recreated during recovery.
SOCKS isolation remains in those factories. No AIA/CRL network fetch is added.

The supplement stops on 2030-06-04, the CA's currently published root-program
retirement date, rather than the PEM's later notAfter. Reassess in a signed app
update before that date. See the CA's [current chains](https://letsencrypt.org/certificates/)
and [platform compatibility](https://letsencrypt.org/docs/certificate-compatibility/).
