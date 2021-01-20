# Productivity

Developers work with many branches at the same time because during any single day they review others' pull requests, try new crazy things, work on new features/bug-fixes and also fix somebody else's PRs. All this forces a combination of `git stash [pop|save]`, `git fetch`, `git checkout [-f]` and again `git stash [pop|apply]`. No matter if this is done by command line or with a tool the problem of having only one copy of the code is an ugly constrain.

## Wasabi Wallet folders organization (suggestion)

### Create main local bare repository

First clone your existing repository as a bare repo (a repository without files) and do it in a `.git` directory.

```
$ git clone --bare git@github.com:zkSNACKs/WalletWasabi.git .git
```

### Add devs remote repositories

Having the team members' repositories as remote makes the work easier: 

```
$ git remote add dan git@github.com:danwalmsley/WalletWasabi.git
$ git remote add jmacato git@github.com:jmacato/WalletWasabi.git
$ git remote add kiminuo git@github.com:kiminuo/WalletWasabi.git
$ git remote add max git@github.com:MaxHillebrand/WalletWasabi.git
$ git remote add molnard git@github.com:molnard/WalletWasabi.git
$ git remote add nopara73 git@github.com:nopara73/WalletWasabi.git
$ git remote add yahia git@github.com:yahiheb/WalletWasabi.git
$ git remote add yuval git@github.com:nothingmuch/WalletWasabi.git
```

Once we have all the remote repositories we fetch them all (this can be a bit slow but it takes time only the first time)

```
$ git fetch --all
```

After that we have absolutely everything locally.

### Folders organization

The suggestion is to have our master branch in its own folder.

```
$ git worktree add master
```

For each team member we can have a dedicated folder in the team folder and then, imagine you need to review two PRs, one from `Yahia` and other from `Kiminuo`, it is enough to do:

```
$ git worktree add team/yahia/bitcoind-tor-hashes yahia/bitcoind-tor-hashes
$ git worktree add team/kiminuo/tor-exceptions kiminuo/feature/2021-01-Tor-exceptions-2nd
```

Bellow you can see what you got after the previous commands. In this way it is possible to simply switch from PR to PR and from branch to branch as easy as simply `cd`.

We can go to the Kiminuo's tor-exceptions branch and work there, add files, review the changes, compile it and test it and we can immediately jump to the Yahia's bitcoind-tor-hashes branch, perform a `git pull` to have the latest changes and review it.

```
├── master
│   ├── WalletWasabi
│   ├── WalletWasabi.Backend
│   ├── WalletWasabi.Documentation
│   ├── WalletWasabi.Fluent
│   ├── WalletWasabi.Fluent.Desktop
│   ├── WalletWasabi.Fluent.Generators
│   ├── WalletWasabi.Gui
│   ├── WalletWasabi.Packager
│   ├── WalletWasabi.Tests
│   └── WalletWasabi.WindowsInstaller
└── team
    ├── dan
    ├── jmacato
    ├── kiminuo
    │   └── tor-exceptions
    │       ├── WalletWasabi
    │       ├── WalletWasabi.Backend
    │       ├── WalletWasabi.Documentation
    │       ├── WalletWasabi.Fluent
    │       ├── WalletWasabi.Fluent.Desktop
    │       ├── WalletWasabi.Fluent.Generators
    │       ├── WalletWasabi.Gui
    │       ├── WalletWasabi.Packager
    │       ├── WalletWasabi.Tests
    │       └── WalletWasabi.WindowsInstaller
    ├── max
    ├── lontivero
    ├── molnard
    ├── nopara73
    ├── yahia
    │   └── bitcoind-tor-hashes
    │       ├── team
    │       ├── WalletWasabi
    │       ├── WalletWasabi.Backend
    │       ├── WalletWasabi.Documentation
    │       ├── WalletWasabi.Fluent
    │       ├── WalletWasabi.Fluent.Desktop
    │       ├── WalletWasabi.Fluent.Generators
    │       ├── WalletWasabi.Gui
    │       ├── WalletWasabi.Packager
    │       ├── WalletWasabi.Tests
    │       └── WalletWasabi.WindowsInstaller
    └── yuval 
```

This is also useful for testing bugs in released versions. Imagine you are working in the middle of some refactoring for a new feature and someone reports a bug in `v1.1.12.2`. In that case you don't need to stash nor commit anything, instead you can simply do:

```
$ git worktree add releases/v1.1.12.2 v1.1.12.2
$ cd releases/v1.1.12.2
```

And that's all.