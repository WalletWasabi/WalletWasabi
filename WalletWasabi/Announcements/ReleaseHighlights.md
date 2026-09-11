## Release Highlights

#### 🔗 P2P synchronization fixes after chain reorganizations
#### 🔐 Security improvements for coinjoin
#### 🍎 Native macOS Apple Silicon support for hardware wallets

## Release Summary
Wasabi Wallet v2.8.2 fixes P2P synchronization issues after blockchain reorganizations and addresses security vulnerabilities in coinjoin.

### 🔗 P2P synchronization fixes after chain reorganizations
Fixed several issues that caused synchronization failures when the blockchain experienced reorganizations. The wallet now correctly handles orphaned tips and shorter reorg chains without throwing errors, ensuring reliable sync recovery.

### 🔐 Security improvements for coinjoin
Enhanced coinjoin security by verifying other participants' inputs before signing. This adds an extra layer of protection against malicious coordinators or participants attempting to manipulate transactions.

### 🍎 Native macOS Apple Silicon support for hardware wallets
Hardware Wallet Interface (HWI) now runs natively on Apple Silicon Macs, eliminating the need for Rosetta emulation and improving performance when using hardware wallets on M1/M2/M3 machines.
