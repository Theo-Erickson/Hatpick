# Hatpick

Pick which Git account each repo commits and pushes as, e.g. work and personal
on the same machine. It's a sibling of DandyPicker and uses the same layout.
The name is a nod to a hat trick: each account is a hat, and Hatpick picks which
one each repo wears.

## How it works

Each account has three parts:

- **Commit name** and **commit email**, which go into `user.name` and `user.email`.
  These decide who commits are authored by, in both GitHub Desktop and command-line git.
- **GitHub username**, which goes into `credential.https://github.com.username`.
  This tells Git Credential Manager which saved sign-in to use when you push, so it
  doesn't show the "Select an account" window.

You can set an account in two ways:

- **Per repo.** Select one or more repos and click **Set account**. The account is
  written into that repo's own `.git/config`.
- **Per folder.** Under **Scan folders**, give a folder a default account. Hatpick
  adds an `[includeIf "gitdir/i:<folder>/"]` entry to your global `.gitconfig` that
  points at `~/.hatpick/<account>.gitconfig`. Every repo in that folder uses that
  account, including repos you create or clone there later, unless the repo has its
  own setting.

The **Set by** column shows where each repo's account comes from:

| Set by | Meaning |
|---|---|
| This repo | Set on the repo itself |
| Folder default | Comes from the scan folder's default |
| Global | Falls back to your global identity |
| Not set | No identity anywhere, so commits will fail |

**Use folder default / global** in the Set account menu clears a repo's own setting.

## Buttons

- **Set account ▾**: assign an account to the selected repos. Double-clicking a repo
  also opens this menu.
- **Open in GitHub Desktop** / **Open folder**: open the selected repo.
- **Accounts...**: add, edit or remove accounts. If you edit an account, every repo
  already set to it is updated. The GitHub username box suggests the accounts that
  Git Credential Manager already has a sign-in for.
- **Scan folders...**: choose the folders Hatpick searches for repos, and each
  folder's default account. It searches up to 4 levels deep and skips build and
  cache folders such as `Intermediate`, `Saved`, `Binaries`, `.godot` and `node_modules`.
- **Add repo... / Remove repo**: list a repo that lives outside your scan folders.
  Removing a repo only takes it off the list. Its Git settings stay as they are.
- **Rescan** (F5)

Settings are stored in `hatpick.json` next to the .exe.

## GitHub Desktop

GitHub Desktop uses the per-repo name and email when it commits, so commits are
authored by the right account. When it **pushes** to github.com, though, it uses the
account you're signed into in Desktop (File → Options → Accounts), not Git Credential
Manager. If Desktop is signed in as one account and a repo belongs to the other,
either push that repo from a terminal (`git push`, which goes through Git Credential
Manager and picks the right account) or add the signed-in account as a collaborator
on that repo.

## Building

`src\build.cmd` compiles `Hatpick.exe` with the C# compiler that ships with
Windows (.NET Framework 4.x), so there's nothing to install. Close Hatpick before
rebuilding. Stick to C# 5 syntax, because that's the newest version this compiler
understands.

`hatpick.ico` is drawn by `tools\make-icon.py` (needs Python with Pillow 10.1+):

```powershell
python tools\make-icon.py hatpick.ico hatpick.png
```

## History

Each milestone is tagged:

| Tag | Version |
|---|---|
| `v1.0-gitpicker` | First version, named GitPicker |
| `v1.1-hatpick` | Renamed to Hatpick (Gitpicky went to a separate project) |

To get any version as its own folder:

```powershell
git worktree add ..\Hatpick-v1.0 v1.0-gitpicker
```
