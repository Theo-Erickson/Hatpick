# Hatpick

Each of your GitHub accounts is a **hat**. Hatpick makes sure every repo wears the
right one - for the name on your commits and for the account you push with - shows
why a repo wears what it wears, and can fix the author of commits before (or after)
you push. The name is a nod to a hat trick.

## No sign-in, no secrets

Hatpick never asks for a password or token and never sees one. Your GitHub sign-ins
live in **Git Credential Manager** (GCM), which ships with Git for Windows. Hatpick
only writes plain git settings that say *which* sign-in to use:

```
user.name / user.email                       who commits are authored by
credential.https://github.com.username       which GCM sign-in is used to push
```

It reads the list of signed-in account *names* (`git credential-manager github list`)
and never runs `git credential fill`, the command that would return a token.

## The window

A slim rail on the left switches pages; theme and help sit at the bottom.

- **Overview** - your hats, a *Needs attention* list with one-click fixes, and every
  repo as a card showing the hat it commits with and the hat it pushes with. Two
  different hats means they don't match; a dashed circle means no hat.
- **Repos** - one repo in detail: who it commits and pushes as, a *Where each setting
  comes from* table (this repo, folder default, global - the winning value is
  highlighted), hat pickers, and its recent commits.
- **Hats** - add, edit and remove hats (label, commit name and email, GitHub username,
  and a hat picture), plus which accounts are signed in to GCM.
- **Folders** - watched folders and their default hats, single repos, and the optional
  Explorer right-click entry.
- **Reset** - undo everything Hatpick changed.

Every section has a **?** button explaining it. First launch runs a short setup, and
**Help** can rerun it or take the one-minute tour. Dark and light themes are built in;
each theme is just a palette in `Theme.cs`, so adding another is one more palette.

## Watched folders vs. single repos

- **Watched folders** hold your repos, like `Documents\GitHub`. Every repo inside is
  listed (up to 4 levels deep, skipping build folders such as `Intermediate`, `Saved`
  and `node_modules`). A folder's **default hat** applies to every repo in it,
  including repos you clone or create there later, through an
  `[includeIf "gitdir/i:<folder>/"]` entry in your global `.gitconfig` that points at
  `~/.hatpick/<hat>.gitconfig`.
- **Single repos** live somewhere else; you pick their hat directly.
- **Analyzing** any folder (drop it on the window, *Folders > Analyze a folder*, the
  command line or the Explorer entry) shows it without adding it; *Keep in list* adds it.

A repo's own setting beats its folder default, which beats your global settings.

## Fixing commits

On a repo's page, the **Commits** section lists recent commits on the current branch
with their author's hat, any `Co-authored-by` lines, and whether each is **Local** or
**Pushed**. If commits you haven't pushed yet are authored by a different hat than the
repo wears, Hatpick offers to fix them before you push.

Select commits, choose a new author and/or *Remove co-authors*, and Hatpick shows what
it's about to do before doing it:

- **Blocks** it if you have uncommitted changes, a rebase or merge is in progress,
  you're not on a branch, or the commits include a merge.
- **Warns** (and asks you to confirm) when commits are already pushed (you'll need to
  force push, and anyone who pulled must reset), when other people's commits sit on
  top of yours (they get new IDs), when you'd take credit for someone else's commit,
  when it's the default branch, when signed commits would lose their signatures, and
  when the commits are on other branches too.

It saves a backup ref (`refs/hatpick/backup/<branch>/<time>`) first, rewrites the
commits with the same files, and moves the branch - your working folder doesn't
change. **Undo last rewrite** puts the branch back exactly. Hatpick never pushes: use
*Force push* in GitHub Desktop (or `git push --force-with-lease`) for commits that
were already pushed.

## Resetting

The first time Hatpick changes a repo's settings it saves what the repo had (in a
`[hatpick]` section of that repo's `.git/config`). *Reset this repo* puts that back
exactly; the **Reset** page does it for every repo at once and can remove all folder
defaults. Commits, branches and GCM sign-ins are never touched.

## GitHub Desktop

GitHub Desktop uses each repo's commit name and email. When it **pushes** to github.com
it uses the account you're signed into in Desktop, not GCM, so for a repo that belongs
to your other account, push from Git Bash (`git push` goes through GCM) or add Desktop's
account as a collaborator.

## If something goes wrong

Unexpected errors are written to `hatpick-error.log` next to `Hatpick.exe`, and the
message says so.

## Building

`src\build.cmd` compiles every `.cs` file in `src` into `Hatpick.exe` with the C#
compiler that ships with Windows (.NET Framework 4.x, WPF), so there's nothing to
install. Close Hatpick before rebuilding, and stick to C# 5 syntax. Control looks are
XAML in `Styles.cs`, loaded at startup; colors are theme brushes in `Theme.cs`.

`hatpick.ico` is drawn by `tools\make-icon.py` (Python with Pillow 10.1+). At 64px and
up it shows the hat trick (hard hat, top hat, beanie); at 48px and below, which covers
the Start menu and taskbar, just the hard hat and the beanie.

```powershell
python tools\make-icon.py hatpick.ico hatpick.png
```

## History

| Tag | Version |
|---|---|
| `v1.0-gitpicker` | First version, named GitPicker |
| `v1.1-gitpicky` | Renamed to Gitpicky |
| `v1.2-pick-icon` | Guitar-pick icon |
| `v1.3-hatpick` | Renamed to Hatpick (Gitpicky went to a separate project) |
| `v1.4-hat-trick-icon` | Hat-trick icon |
| `v2.0-analyzer` | Repo analyzer, reset, first-run setup, tour and help |
| `v3.0-wpf` | New WPF interface with themes, hats everywhere, and commit author fixing |

To get any version as its own folder:

```powershell
git worktree add ..\Hatpick-v1.0 v1.0-gitpicker
```
