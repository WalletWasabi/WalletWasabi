# Publish

A command line tool to announce releases on nostr.

# How to play with it

```bash
$ dotnet run <release-version> <announcement-content-file-path> <secret-key-hex>
```

Example:
```bash
dotnet run "2.46.5" "../../../WalletWasabi/Announcements/ReleaseHighlights.md" "b76950102f2cf9df470474a344621d6f25d10946aef0451f7ff3cb30152ebedd"
```
