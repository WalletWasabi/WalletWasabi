# Updating HWI

1. Replace executables.
2. Properties/Copy to Output: Copy if newer.
3. Make sure the Linux and the OSX binaries are executable: `git update-index --chmod=+x hwi/bitcoind`.
4. Commit, push.
5. Make sure CI passes.
