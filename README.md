# Hatpick

Pick which Git account each repo commits and pushes as, e.g. work and personal
on the same machine, and see at a glance what any repo is set to and why.
The name is a nod to a hat trick: each account is a hat, and Hatpick picks which
one each repo wears.

## No sign-in, no secrets

Hatpick never asks for a password or token and never sees one. Your GitHub
sign-ins live in **Git Credential Manager** (GCM), which ships with Git for
Windows. Hatpick only writes plain settings that tell git *which* saved sign-in
to use:

```
user.name / user.email                       who commits are authored by
credential.https://github.com.username       which GCM sign-in is used to push
```

When you push, git passes that username to GCM, and GCM picks the matching
saved sign-in instead of showing its "Select an account" window. Hatpick reads
the list of signed-in account *names* (`git credential-manager github list`) and
never runs `git credential fill`, the command that would return a token.

## Getting started

The first time Hatpick opens, a short setup walks you through:

1. **Signing in to Git Credential Manager.** It lists the GitHub accounts GCM
   already has, and *Sign in an account* runs GCM's own sign-in (a console window
   plus your browser) for any that are missing. You can also do this from Git
   Bash, once per account:
   ```
   git credential-manager github login
   ```
2. **Adding your accounts.** *Add from signed-in accounts* fills in the GitHub
   usernames; you add the commit email for each.
3. **Choosing watched folders** and a default account for each.

At the end it offers a one-minute tour of the window and the Explorer
right-click entry. Both setup and the tour can be run again from **? Help**.

The **?** buttons next to each part of the report, the repo list and the
"Set this repo" box explain what that line means. **? Help** also has short
explainers on how Hatpick works, watched folders vs. single repos, the status
dots and resetting.

## The window

**Left:** your repos, grouped by where they come from, with a status dot:

| Dot | Meaning |
|---|---|
| Green | All set |
| Blue | Fine, with notes (e.g. SSH remote, repo owned by an organization) |
| Yellow | Needs attention (e.g. GCM will ask which account, or commits and pushes use different accounts) |
| Red | Not set up (no commit identity, so commits fail) |

**Right:** the report for the selected repo: its remote, who it **commits as**,
who it **pushes as**, where each setting comes from (*This repo*, *Folder
default*, *Your global settings*, *The remote URL*), whether GCM has a sign-in
for that account, and whether Hatpick has changed the repo. Below that you can
set the repo's commit and push accounts.

## Watched folders vs. single repos

- **Watched folders** are folders that hold your repos, like `Documents\GitHub`.
  Hatpick lists every repo inside (up to 4 levels deep, skipping build folders
  like `Intermediate`, `Saved` and `node_modules`). Give a watched folder a
  **default account** and every repo in it uses that account, *including repos
  you clone or create there later*. This works through an
  `[includeIf "gitdir/i:<folder>/"]` entry in your global `.gitconfig` that
  points at `~/.hatpick/<account>.gitconfig`.
- **Single repos** are repos that live somewhere else. Add them one at a time;
  they don't get a folder default, so set their account directly.
- **Analyzing** a folder (drag it onto the window, *Add > Analyze any folder*, or
  the Explorer right-click menu) shows its report without adding it to your list.
  *Keep in list* turns it into a single repo.

A repo's own setting always wins over its folder default, and the folder default
wins over your global settings.

## Resetting

The first time Hatpick changes a repo, it saves what the repo had set itself
(in a `[hatpick]` section of that repo's `.git/config`).

- **Reset this repo** puts back exactly what was there before and removes the
  `[hatpick]` section.
- **Reset > Reset everything Hatpick changed...** does that for every repo it
  changed, and can also remove all folder defaults from your global `.gitconfig`.

Commits, branches and GCM sign-ins are never touched.

## Other ways in

- `Hatpick.exe <folder>` opens straight to that folder's report. If Hatpick is
  already open, it switches to that folder.
- **Options > Add "Analyze with Hatpick" to Explorer's right-click menu** adds a
  per-user entry (no admin rights needed). On Windows 11 it's under *Show more
  options*.

## GitHub Desktop

GitHub Desktop uses each repo's commit name and email, so commits are authored by
the right account. When it **pushes** to github.com, though, it uses the account
you're signed into in Desktop (File > Options > Accounts), not GCM. If a repo
belongs to the other account, push it from Git Bash (`git push`, which goes
through GCM and picks the right account), or add Desktop's account as a
collaborator on that repo.

## Building

`src\build.cmd` compiles every `.cs` file in `src` into `Hatpick.exe` with the C#
compiler that ships with Windows (.NET Framework 4.x), so there's nothing to
install. Close Hatpick before rebuilding. Stick to C# 5 syntax, because that's the
newest version this compiler understands.

`hatpick.ico` is drawn by `tools\make-icon.py` (needs Python with Pillow 10.1+).
At 64px and up it shows a "hat trick": a branch splitting three ways, with a hard
hat (work), a top hat and a beanie (personal) on the tips. At 48px and below,
which covers the Start menu and taskbar, it shows just the two-way fork with the
hard hat and the beanie, so each hat stays readable.

```powershell
python tools\make-icon.py hatpick.ico hatpick.png
```

## History

Each milestone is tagged:

| Tag | Version |
|---|---|
| `v1.0-gitpicker` | First version, named GitPicker |
| `v1.1-hatpick` | Renamed to Hatpick (Gitpicky went to a separate project) |
| `v1.2-hat-trick-icon` | Hat-trick icon |
| `v2.0-analyzer` | Repo analyzer, reset, first-run setup, tour and help |

To get any version as its own folder:

```powershell
git worktree add ..\Hatpick-v1.0 v1.0-gitpicker
```
