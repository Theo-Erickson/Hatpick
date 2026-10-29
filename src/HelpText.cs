using System.Collections.Generic;

namespace Hatpick
{
    // Every explanation in one place, so the ? buttons, the tour and setup all say the same thing.
    static class HelpText
    {
        public class Topic { public string Title, Body; public Topic(string t, string b) { Title = t; Body = b; } }

        public static readonly Dictionary<string, Topic> Topics = new Dictionary<string, Topic>
        {
            { "overview", new Topic("How Hatpick works",
                "Each account is a hat. Every repo answers two questions:\n\n" +
                "- Commits as: the name and email written into each commit. GitHub credits the commit to the account that owns that email.\n" +
                "- Pushes as: the GitHub account Git Credential Manager signs in as when you push.\n\n" +
                "When both use the same account, the repo is wearing one hat. Hatpick sets both with plain git settings. It never asks for a " +
                "password or token - your sign-ins stay in Git Credential Manager, and Hatpick only tells it which one to use.\n\n" +
                "Git looks for each setting in order: this repo first, then a folder default, then your global settings. The first one it finds wins.") },

            { "hats", new Topic("Your hats",
                "A hat is one of your accounts: a commit name and email, plus the GitHub username Git Credential Manager signs in as. " +
                "Pick a hat picture for each so you can tell them apart at a glance.\n\n" +
                "Use the email that's on that GitHub account (GitHub > Settings > Emails), or its private noreply address, so GitHub credits your commits.") },

            { "attention", new Topic("Needs attention",
                "Repos where something is off: two different hats (commits and pushes use different accounts), no hat at all (commits will fail), " +
                "or no push account while several are signed in (Git Credential Manager will ask every time). The button next to each fixes the most common case in one click.") },

            { "list", new Topic("Your repos",
                "Repos are grouped by where they come from: a watched folder, single repos you added one at a time, and folders you analyzed without adding.\n\n" +
                "Each card shows the hat a repo commits with and the hat it pushes with. Two different hats means they don't match; a dashed circle means no hat.") },

            { "folders", new Topic("Watched folders vs. single repos",
                "Watch a folder when it holds several repos, like " + Platform.ReposFolder + ". Hatpick lists every repo inside it, and you can give the " +
                "folder a default hat. That default applies to every repo in the folder, including repos you clone or create there later.\n\n" +
                "Add a single repo when it lives somewhere else. It has no folder default, so you pick its hat directly.\n\n" +
                "Either way, a repo's own setting beats the folder default, and the folder default beats your global settings.") },

            { "remote", new Topic("Remote",
                "Where this repo pushes to (usually the remote called \"origin\").\n\n" +
                "Hatpick's push account only applies to https://github.com remotes. An SSH remote (git@github.com:...) uses your SSH key " +
                "instead, and other hosts use their own sign-in.") },

            { "commits", new Topic("Commits as",
                "The name and email git writes into every commit you make here. GitHub links each commit to the account that has that email.") },

            { "pushes", new Topic("Pushes as",
                "The GitHub account Git Credential Manager signs in as when you push, pull or fetch over https.\n\n" +
                "A username inside the remote URL (https://name@github.com/...) always wins. If nothing sets it and more than one account is signed in, " +
                "Git Credential Manager asks every time.") },

            { "layers", new Topic("Where each setting comes from",
                "Git reads its settings in layers: this repo's own settings, then a folder default from a watched folder, then your global settings. " +
                "For each setting, the first layer that has a value wins - highlighted here. Values in lower layers are crossed out because they're ignored.") },

            { "set", new Topic("Picking hats for this repo",
                "Commit hat writes that account's name and email into this repo's own settings. Push hat sets which GitHub account Git Credential Manager signs in as.\n\n" +
                "\"Not set here\" removes the repo's own setting, so it falls back to its folder default or your global settings.\n\n" +
                "Only this repo's .git/config changes. Reset puts back whatever the repo had before Hatpick first changed it.") },

            { "signin", new Topic("Signing in",
                "Git Credential Manager keeps your GitHub sign-ins. If it doesn't have one for a push account yet, the first push opens your browser to sign in, and after that it's saved.\n\n" +
                "You can sign in ahead of time from Hats > Sign in another account, or from a terminal:\n\n    git credential-manager github login\n\n" +
                "Hatpick only reads the list of account names. It never sees the sign-in itself.") },

            { "reset", new Topic("Resetting",
                "The first time Hatpick changes a repo, it saves whatever the repo had set itself. Reset puts that back exactly - for one repo, or every repo at once - " +
                "and can also remove the folder defaults from your global .gitconfig.\n\nYour commits, branches and Git Credential Manager sign-ins are never touched.") },

            { "uncommit", new Topic("Undo commits, keep the changes",
                "Takes commits off the branch without touching your files: their changes come back as uncommitted changes, so you can commit them again " +
                "differently - leave out a file, fix the message, or split them up. It's the same as \"git reset --soft\".\n\n" +
                "It works back from the newest commit, so undoing an older commit also undoes everything after it. If the commits were already pushed, " +
                "you'll need to force push once you've committed again.\n\n" +
                "Hatpick saves a backup first. \"Undo last change\" above the commit list puts the commits back, and keeps any changes you've made since.") },
        };
    }
}
