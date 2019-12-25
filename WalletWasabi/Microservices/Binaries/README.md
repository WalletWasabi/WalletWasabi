# Updating Executables

1. Replace executables.
2. Properties/Copy to Output: Copy if newer. (VSBUG: Sometimes Copy Always is needed. It's generally ok to do copy always then set it back to Copy if newer, so already running Bitcoin Core will not be tried to recopied all the time.)
3. Make sure the Linux and the OSX binaries are executable:
	`git update-index --chmod=+x hwi`
	`git update-index --chmod=+x bitcoind`
4. Commit, push.
5. Make sure CI passes.
