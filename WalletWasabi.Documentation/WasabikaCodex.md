## Daily routine

1. Check [Valhalla board](https://github.com/orgs/zkSNACKs/projects/4) that contains issues and priorities.

    | Developer | Assigned |
    | :-------: | :-------: |
    | molnard | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Amolnard) |
    | jmacato | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Ajmacato) |
    | danwalmsley | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Adanwalmsley) |
    | kiminuo | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Akiminuo) |
    | lontivero | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Alontivero) |
    | MaxHillebrand | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3AMaxHillebrand) |
    | nopara73 | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Anopara73) |
    | yahiheb | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Ayahiheb) |
    | soosr | [Show](https://github.com/orgs/zkSNACKs/projects/4?card_filter_query=assignee%3Asoosr) |

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

- Wednesday 13:00 UTC: Developer meeting.
- Pair programming hour.

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

## Creating Pull Request

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

### Clever Extensions

- Open [Power Shell or Command Line with hotkey](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.OpenCommandLine). By pressing Alt-Space a cmd prompt will be opened. You can change the default command line tool under Tools/Options/Command Line => Select preset. `Always open at solution` level option is also a useful feature.

## Git tips

- Scripts  
  Add these lines to your `C:\Users\{YourUser}\.gitconfig` file. (Windows)
   ```
   [alias]
      upd = "!f(){ git fetch upstream && git checkout master && git rebase upstream/master && git push -f origin master && git branch --merged; };f"
      del = "!f(){ git checkout master && (git branch -D \"$1\"; git push origin --delete \"$1\"); git branch; };f"
   ```
  You can use it with the console in the project library by the following commands:  
   - `git upd` -> Update the forked repository  
   - `git del` -> Delete the current branch remotely and locally  
