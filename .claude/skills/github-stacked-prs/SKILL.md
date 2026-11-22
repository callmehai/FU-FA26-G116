---
name: github-stacked-prs
description: Use whenever the user wants to stack pull requests, link existing PRs into a stack, merge a stack of PRs with one action, restack/rebase dependent branches, or asks "stack PR", "stack 2 PR vào nhau", "merge stack", "PR chồng", "gh stack". GitHub has had NATIVE stacked pull requests since 2026 (public preview 2026-07-30, generally available 2026-10-06) with the official `gh stack` CLI extension — newer than most model training data, so do not fall back to "just point the PR's base at the other branch and merge bottom-up by hand".
---

# GitHub native stacked pull requests

Native stacks make GitHub treat a chain of PRs as one unit: a stack map on every PR, per-layer diffs, branch rules
enforced against the stack's base for every layer, and **merging any layer merges every unmerged layer below it in one
all-or-nothing operation**, then rebases and retargets what is left. No repository setup is needed.

Pointing PR B's base at PR A's branch is only the **old manual stack**. GitHub does not see it as a stack and shows no
"merge stack" action until the PRs are linked (step 2 below). Don't tell the user that a base-branch change "stacks"
the PRs.

Docs: https://gh.io/stacks · CLI reference: https://docs.github.com/en/pull-requests/reference/stacked-prs-cli-commands

## 1. The CLI extension

```bash
gh extension list                      # is "github/gh-stack" there?
gh extension install github/gh-stack   # official extension, needs a current gh (2.101 worked)
gh stack --help
```

Installing an extension downloads and runs third-party code, so an auto-mode/permission classifier may refuse it.
If it does, don't work around it: give the user the install command, ask them to run it, then verify with
`gh extension list`.

## 2. Link PRs that already exist (the common case)

```bash
gh stack link 200 202          # bottom first, top last; PR numbers, URLs or branch names
gh stack link --base develop 10 20 30   # when the bottom PR targets something other than the default branch
```

- Creates the stack on GitHub, or adds to an existing one. Additive only: it never removes a PR from a stack.
- Fixes each PR's base branch to the expected chain automatically.
- "Stack with N PRs is already up to date" means the stack already exists (maybe the user or GitHub made it). Not an error.
- `link` creates **no local tracking**, so `gh stack view` then says the current branch "is not part of a stack".
  Expected; check the stack on the PR page, or `gh stack checkout <pr>` to track it locally.

New work from scratch: `gh stack init` → commit → `gh stack add <next-branch>` → commit → `gh stack submit`
(pushes and opens/updates the PRs); `gh stack init b1 b2 b3` turns existing local branches into a stack.

## 3. Prerequisite for merging: a linear stack

A stack can merge only when each branch contains the tip of the branch below it (bottom contains the trunk). Check:

```bash
git fetch origin
git merge-base --is-ancestor origin/app origin/<bottom-branch> && echo ok
git merge-base --is-ancestor origin/<bottom-branch> origin/<next-branch> && echo ok
```

If a lower layer moved (review fixes pushed to it), the layer above must pick it up. Two ways:

- **`gh stack rebase` + `gh stack push`** (needs local tracking: `gh stack checkout <pr>` first). Cascading rebase from
  the trunk upward, then pushes with `--force-with-lease`. Conflicts: fix, `git add`, `gh stack rebase --continue`, or
  `--abort` to restore everything.
- **Merge the lower branch into the upper one** (`git merge origin/<lower>`), then a normal push. No force-push. Use this
  when the upper branch belongs to someone else, or the merge needs hand-made resolution you must not lose.

**Danger: the "Rebase stack" button and `gh stack rebase` drop merge commits.** Anything you fixed only inside a merge
commit (a regenerated migration, a hand-resolved snapshot/lock file, a conflict resolution) disappears on rebase and
the conflict comes back. When a merge resolution has to survive, rebase by hand and put the fix in an ordinary commit.
Server-side rebase also makes unsigned commits, so a repo that requires signed commits needs the CLI rebase.

Code-first databases (EF Core, Django, Rails, Prisma…): when a lower layer adds or renames a migration, check that the
upper layer's migration still sorts **after** it and that its snapshot/designer includes the lower layer's model. If
not, delete the upper migration and generate it again on top of the merged model; diff the new Up/Down against the old
one to prove only the timestamp/designer changed. Tell the user that anyone who applied the old migration locally must
reset that database.

## 4. Merge

- **On github.com:** open the **top** PR you want merged. Its merge box lists every PR below it; merging it lands all of
  them. Merge commit, squash and rebase all work, and stacks respect merge queues.
- **CLI:**

  ```bash
  gh stack merge                 # current stack, interactive picker (how far up, merge method)
  gh stack merge 202 --merge     # everything up to and including PR #202, with merge commits
  gh stack merge --yes --squash  # whole current stack, no prompt
  ```

  A bare number is read **first as a stack number, then as a PR number**. Stack numbers and PR numbers can collide, so
  look at the stack number on the PR page before passing a number. Only open/non-draft is checked up front; branch
  protection is evaluated at merge time and bypass is not supported for stacks. Use the repo's usual merge method (look
  at `git log origin/app` for "Merge pull request #…" vs squash commits).

Before merging, report to the user anything still unfinished in any layer (open review points, pending checks). One
click merges every layer, so a gap in the bottom PR ships along with the top one.

## 5. After merging and other maintenance

```bash
gh stack sync --prune     # fetch, fast-forward trunk, rebase what's left, push, prune merged local branches
gh stack down | up | top | bottom | checkout <pr>   # move between layers
gh stack modify           # interactive: drop, fold, insert, reorder, rename layers (needs a clean, linear stack)
gh stack unstack [<stack-number>]   # dissolve; merged or queued PRs stay in the stack
```

## Limits and gotchas

- All branches must live in **one repository**; cross-fork stacks are not supported. A stack is a single linear chain.
- GitHub Desktop doesn't support stacks.
- After a restack push, review approvals on layers whose diff changed may be dismissed if the repo dismisses stale
  approvals; expect to ask for re-approval.
- Without native stacks, the manual fallback is: merge the bottom PR with a merge commit, let GitHub retarget the next PR
  (it does so when the merged branch is deleted), then merge the next one. After a **squash** merge, restack the upper
  branches with `git rebase --onto <trunk> <old-bottom-branch> <top-branch>` before merging them.
- Pushing to someone else's branch (merge or force-push) is an outward action: do it only when the user asked, and say
  which branch and commit you pushed.
