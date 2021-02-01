# Updating Executables

1. Replace executables.
2. Properties/Copy to Output: Copy if newer. (VSBUG: Sometimes Copy Always is needed. It's generally ok to do copy always then set it back to Copy if newer, so already running Bitcoin Core will not be tried to get recopied all the time.)
3. Make sure the Linux and the OSX binaries are executable:
	`git update-index --chmod=+x hwi`
	`git update-index --chmod=+x bitcoind`
	`git update-index --chmod=+x .\lin64\Tor\tor`
	`git update-index --chmod=+x .\win64\Tor\tor.exe`
	`git update-index --chmod=+x .\osx64\Tor\tor`
	`git update-index --chmod=+x .\osx64\Tor\tor.real`
4. Update the binary hashes of each executable and the text documentation in `*BinaryHashesTests.cs` test files.
5. Commit, push.
6. Make sure CI passes.
