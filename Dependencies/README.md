This directory contains dependencies which cannot be consumed via NuGet due to any reason. Each dependency **MUST** be consumed through `git subtree` not only to keep the source code, but also to include the exact commit which the subtree references. `--squash` option is preferred to avoid tracking unneeded history from an external repository.

### Add subtree

```sh
git subtree add --prefix <directory> <repository> <reference> --squash
```

### Update subtree

```sh
git subtree pull --prefix <directory> <repository> <reference> --squash
```
