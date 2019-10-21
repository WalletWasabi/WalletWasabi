# Updating Bitcoin Core

1. Replace executables.
2. Properties/Copy to Output: Copy always
3. Make sure the Linux and the OSX binaries are executable: `git update-index --chmod=+x bitcoind`.
4. Commit, push.
5. Make sure CI passes.