## Daily routine

1. Check with your team leader about tasks.
2. Check [open code review requests waiting for you](https://github.com/zkSNACKs/WalletWasabi/pulls/review-requested/@me).
3. Check [open priority issues and PRs](https://github.com/zkSNACKs/WalletWasabi/labels/priority). Take it or set Assignees and Labels if you can.
4. Check your [open PRs](https://github.com/zkSNACKs/WalletWasabi/pulls/@me) and answer if necessary.
   - If you need help use __@mention__.
   - If you need review set __Reviewers__.
5. Check [open issues assigned to you](https://github.com/zkSNACKs/WalletWasabi/issues/assigned/@me).
6. Check [open issues and PRs where you are mentioned](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aopen+mentions%3A%40me+).
7. Check the [notifications](https://github.com/notifications).
8. Work on anything you want - assign it to yourself.

## Weekly routine

- Team Meeting. Team members talk about their work during the last week.
- Company meeting. Team leaders report about the progress of their team.
- On-demand pair programming sessions.

## Monthly routine

- Nothing here yet.

## Yearly routine

- Working holiday with all colleagues.

## Roadmap

- There is no RoadMap.

## Code review (CR)

- Read the description of the Pull Request. If it is missing ask for it if necessary.
- Check Continuous Integration (CI) before code review. Only continue if it is passed.
- Do not review code longer than 60 minutes or more than 400 lines. Take a 10 minute pause at that point.
- Check for code smells. Give constructive feedback that helps (Not hurts).
  - Easy to read.
  - Maintainable.
- Checkout PR and test it if required.
- Request changes if needed.
- Approve if you are done.

__Tips__: if the PR is too big ask for breakdown.

__Fact__: code review is one of the few scientifically proven code improvement tools.

## How to fork a repository
- On Github navigate to the desired repository.
- In the top-right corner of the page, click Fork.

### Cloning your forked repository
- On GitHub.com, navigate to your fork. For Example: https://github.com/adampetho/WalletWasabi
- Above the list of files, click the Code button with the arrow pointing down.
- Use one of the 3 options to get your repository into your local machine. (See below)

#### Git Bash approach
- Get and Setup Git Bash on your local computer
- Copy the link
- Open Git Bash.
- Change the current working directory to the location where you want the cloned directory.
- Type git clone, and then paste the URL you copied earlier. Example: "git clone https://github.com/zkSNACKs/WalletWasabi.git"
- Press Enter. Your local clone will be created.
- Further reading about this approach or if you get stuck, please read this https://docs.github.com/en/get-started/quickstart/fork-a-repo (or ask the devs)

#### Zip approach
- Self-explanatory: Download -> Unzip -> Ready to go

#### GitHub Desktop approach
- Install GitHub Desktop and Login
- Choose the Open with GitHub Desktop option
- Choose where to clone
- Click OK

## Creating Pull Request
- Fork the repo (See above)
- Clone the forked repo (See above)
- Make a new branch from master by "git switch -c newbranch" (Replace newbranch with your own comprehensive names.)
- Make changes 
- Add necessary files ("git add ." will add every changed file, this is the most commonly used, but you can add the changes one-by-one "git add <filename>")
- Commit your changes: git commit -m "A simple commit message"
- Push your changes: git push
- Raise a PR on github
- Fill the title.
- Fill the description.
  - If it fixes issues [add keyword](https://help.github.com/en/articles/closing-issues-using-keywords).
  - Add related issues and PR links.
  - Add Sub-Tasks with checkboxes.
- If the PR is not ready or Work in progress (WIP) create a Draft Pull Request - so nobody will spend time to review a temporary code, but can see the progress there.
- Implement feature.
- Always fix CI.
- Set Reviewers.
- If it is UX related always ask Jumar or Dan to review.

## Visual Studio Tips (Windows)

- Visual Studio 2019 errors:
  - If you get an error about Visual Studio does not support `WalletWasabi.WindowsInstaller` then you can fix this by installing `WiX Toolset Visual Studio 2019 Extension`. After the the installation reload the `WalletWasabi.WindowsInstaller` project by right click (on it) -> `Reload project`.

### Clever Shortcut settings

- Open Power Shell with hotkey in the project folder is a good feature you should have. Go to Tools / Options / Keyboard and look for Tools.DeveloperPowerShell -> click into the field   'Press Shortcut Keys' and press ALT-SPACE then press Assign button. 

## Git tips

- Scripts  
  Add these lines to your `C:\Users\{YourUser}\.gitconfig` file. (Windows)
   ```
   [alias]
      upd = "!f(){ git fetch upstream && git checkout master && git rebase upstream/master && git push -f origin master && git branch --merged; };f"
      res = "!f(){ git reset --hard; };f"
      hres = "!f(){ git res; git upd; };f"
      del = "!f(){ git checkout master && for arg; do (git branch -D \"$arg\"; git push origin --delete \"$arg\"); done; git branch; };f"
      pr = "!f(){ git checkout -b $(date +'%s') && git add . && git commit -S -m \"$1\" && git push -u origin $(git branch --show) && start \"$(echo $(git config --get remote.upstream.url) | sed 's/.git$//g')/compare/master...USERNAME:$(git branch --show)?expand=1\"; };f" // Replace USERNAME with your GitHub username here.
   ```
  You can use it with the console in the project library by the following commands:  
   - `git upd` -> Update the forked repository
   - `git res` -> Discard your current changes
   - `git hres` -> Hard reset, resets your current changes, update and checkout master.
   - `git del` -> Delete the current branch remotely and locally  
   - `git pr "Commit message"` -> Create a new branch, commit then push your work, open your browser with a new PR waiting for you to fill the title and the description.

# Productivity tips

Developers work with many branches at the same time because during any single day they review others' pull requests, try new crazy things, work on new features/bug-fixes and also fix somebody else's PRs. All this forces a combination of `git stash [pop|save]`, `git fetch`, `git checkout [-f]` and again `git stash [pop|apply]`. No matter if this is done by command line or with a tool, the problem of having only one copy of the code is an ugly constrain.

## Wasabi Wallet folders organization (suggestion)

### Create main local bare repository

First clone your existing repository as a bare repo (a repository without files) and do it in a `.git` directory.

```
$ git clone --bare git@github.com:zkSNACKs/WalletWasabi.git .git
```

## Working with remote repositories

There are two non-mutually exclusive alternatives here. One is to work with the other developers' repositories directly or work with the `origin` repository only.

### Work with devs remote repositories (alternative 1)

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

### Work with origin remote repository only (alternative 2)

Edit the .git/config file and add this line in the `origin` repository section
```
fetch = +refs/pull/*/head:refs/remotes/origin/pr/*
```

Once we have this fetch the repo:

```
$ git fetch origin
```

### Folders organization

The suggestion is to have our master branch in its own folder.

```
$ git worktree add master
```

For each team member we can have a dedicated folder in the team folder and then, imagine you need to *review* two PRs, one from `Yahia` and other from `Kiminuo`, depending on how you decided to config your repositories it is enough to do:

**With developers remote repositories:**
```
$ git worktree add kiminuo/pr-1234 kiminuo/feature/2021-01-Tor-exceptions-2nd
$ git worktree add yahia/pr-2534 yahia/bitcoind-tor-hashes
```

**With origin only remote repository:**
```
$ git worktree add kiminuo/pr-1234 origin/pr/1234
$ git worktree add yahia/pr-2534 origin/pr/2534
```

Bellow you can see what you got after the previous commands. In this way it is possible to simply switch from PR to PR and from branch to branch as easy as simply `cd`.

We can go to the Kiminuo's tor-exceptions branch and work there, add files, review the changes, compile it and test it and we can immediately jump to the Yahia's bitcoind-tor-hashes branch, perform a `git pull` to have the latest changes and review it.

```
├── master
│   ├── WalletWasabi
│   ├── WalletWasabi.Backend
│   ├── WalletWasabi.Documentation
│   ├── WalletWasabi.Fluent
│   ├── WalletWasabi.Fluent.Desktop
│   ├── WalletWasabi.Fluent.Generators
│   ├── WalletWasabi.Gui
│   ├── WalletWasabi.Packager
│   ├── WalletWasabi.Tests
│   └── WalletWasabi.WindowsInstaller
└── pr
    ├── kiminuo
    │   └── pr-1234
    │       ├── WalletWasabi
    │       ├── WalletWasabi.Backend
    │       ├── WalletWasabi.Documentation
    │       ├── WalletWasabi.Fluent
    │       ├── WalletWasabi.Fluent.Desktop
    │       ├── WalletWasabi.Fluent.Generators
    │       ├── WalletWasabi.Gui
    │       ├── WalletWasabi.Packager
    │       ├── WalletWasabi.Tests
    │       └── WalletWasabi.WindowsInstaller
    └── yahia
        └── pr-2534
            ├── team
            ├── WalletWasabi
            ├── WalletWasabi.Backend
            ├── WalletWasabi.Documentation
            ├── WalletWasabi.Fluent
            ├── WalletWasabi.Fluent.Desktop
            ├── WalletWasabi.Fluent.Generators
            ├── WalletWasabi.Gui
            ├── WalletWasabi.Packager
            ├── WalletWasabi.Tests
            └── WalletWasabi.WindowsInstaller

```

This is also useful for testing bugs in released versions. Imagine you are working in the middle of some refactoring for a new feature and someone reports a bug in `v1.1.12.2`. In that case you don't need to stash nor commit anything, instead you can simply do:

```
$ git worktree add releases/v1.1.12.2 v1.1.12.2
$ cd releases/v1.1.12.2
```
