using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Hatpick
{
    // The "?" buttons and the text behind them. One place for every explanation, so the
    // tour, the setup and the popups all say the same thing.
    static class HelpText
    {
        public class Topic { public string Title, Body; public Topic(string t, string b) { Title = t; Body = b; } }

        public static readonly Dictionary<string, Topic> Topics = new Dictionary<string, Topic>
        {
            { "overview", new Topic("How Hatpick works",
                "Each repo has two separate answers:\n\n" +
                "- Commits as: the name and email written into each commit. GitHub credits the commit to the account that owns that email.\n" +
                "- Pushes as: the GitHub account Git Credential Manager signs in as when you push.\n\n" +
                "Hatpick sets both with plain git settings. It never asks for a password or token - your sign-ins stay in Git Credential Manager, " +
                "and Hatpick only tells it which one to use.\n\n" +
                "Git looks for each setting in order: this repo first, then a folder default, then your global settings. The first one it finds wins.") },

            { "list", new Topic("Your repos",
                "Repos are grouped by where they come from:\n\n" +
                "- A watched folder: every repo inside a folder you watch, like Documents\\GitHub.\n" +
                "- Single repos: repos you added one at a time.\n" +
                "- Analyzed, not in your list: folders you dropped on the window or opened from Explorer.\n\n" +
                "The dot shows the status: green is all set, blue is fine with notes, yellow needs attention, red isn't set up.") },

            { "folders", new Topic("Watched folders vs. single repos",
                "Watch a folder when it holds several repos, like Documents\\GitHub. Hatpick lists every repo inside it, and you can give the " +
                "folder a default account. That default applies to every repo in the folder, including repos you clone or create there later.\n\n" +
                "Add a single repo when it lives somewhere else. It has no folder default, so you set its account directly.\n\n" +
                "Either way, a repo's own setting beats the folder default, and the folder default beats your global settings.") },

            { "remote", new Topic("Remote",
                "Where this repo pushes to (usually the remote called \"origin\").\n\n" +
                "Hatpick's push account only applies to https://github.com remotes. An SSH remote (git@github.com:...) uses your SSH key " +
                "instead, and other hosts use their own sign-in.") },

            { "commits", new Topic("Commits as",
                "The name and email git writes into every commit you make here. GitHub links each commit to the account that has that email.\n\n" +
                "\"Set by\" shows where it comes from: This repo, a Folder default from a watched folder, or Your global settings. " +
                "If none of them set it, commits fail.") },

            { "pushes", new Topic("Pushes as",
                "The GitHub account Git Credential Manager signs in as when you push, pull or fetch over https.\n\n" +
                "\"Set by\" shows where it comes from: This repo, a Folder default, Your global settings, or The remote URL (a username inside the URL, " +
                "like https://name@github.com/..., always wins).\n\n" +
                "If nothing sets it and more than one account is signed in, Git Credential Manager asks every time.") },

            { "signedin", new Topic("Signed in",
                "Whether Git Credential Manager already has a saved sign-in for the push account.\n\n" +
                "If it doesn't, the first push opens your browser to sign in, and after that it's saved. You can also sign in ahead of time with " +
                "Help > Sign in an account, or from Git Bash:\n\n    git credential-manager github login\n\n" +
                "Hatpick only reads the list of account names. It never sees the sign-in itself.") },

            { "changed", new Topic("Changed by Hatpick",
                "The first time Hatpick changes a repo, it saves whatever the repo had set itself, in a [hatpick] section of the repo's .git/config.\n\n" +
                "Reset this repo puts that back exactly and removes the [hatpick] section. Reset > Reset everything Hatpick changed does it for every repo at once.") },

            { "setcommit", new Topic("Commit as",
                "Pick an account to write its name and email into this repo's own settings.\n\n" +
                "\"Not set here\" removes the repo's own setting, so the repo falls back to its folder default or your global settings.") },

            { "setpush", new Topic("Push as",
                "Pick which GitHub account Git Credential Manager signs in as for this repo. Picking a Commit as account fills this in with that account's " +
                "GitHub username, but you can choose a different one.\n\n" +
                "The list shows your accounts' GitHub usernames plus every account already signed in to Git Credential Manager.") },

            { "accounts", new Topic("Accounts",
                "An account is who you commit and push as: a commit name and email, plus the GitHub username Git Credential Manager signs in as.\n\n" +
                "Use the email that's on that GitHub account (GitHub > Settings > Emails), or its private noreply address, so GitHub credits your commits.") },

            { "reset", new Topic("Resetting",
                "Reset only undoes what Hatpick changed. Each repo goes back to exactly what it had set before Hatpick first touched it, and folder defaults " +
                "can be removed from your global .gitconfig.\n\nYour commits, branches and Git Credential Manager sign-ins are never touched.") },
        };
    }

    static class HelpUi
    {
        static int S(int px) { return Program.S(px); }

        // A small round "?" that opens the topic's popup.
        public static Control Button(string topic)
        {
            HelpText.Topic t;
            if (!HelpText.Topics.TryGetValue(topic, out t)) return null;
            var b = new Label
            {
                Text = "?", Size = new Size(S(17), S(17)), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(90, 110, 150),
                Margin = new Padding(S(6), S(1), 0, 0)
            };
            b.Resize += delegate { Round(b); };
            Round(b);
            var tip = new ToolTip();
            tip.SetToolTip(b, "What's this?");
            b.Click += delegate { Show(b, t); };
            return b;
        }

        static void Round(Control c)
        {
            using (var p = new GraphicsPath()) { p.AddEllipse(0, 0, c.Width, c.Height); c.Region = new Region(p); }
        }

        public static void Show(Control anchor, string topic)
        {
            HelpText.Topic t;
            if (HelpText.Topics.TryGetValue(topic, out t)) Show(anchor, t);
        }

        public static void Show(Control anchor, HelpText.Topic t)
        {
            var at = anchor.PointToScreen(new Point(0, anchor.Height + S(4)));
            new HelpPopup(t.Title, t.Body).ShowAt(anchor.FindForm(), at);
        }

        // For menu items and other places without an anchor control: a normal dialog.
        public static void ShowDialog(IWin32Window owner, string topic)
        {
            HelpText.Topic t;
            if (HelpText.Topics.TryGetValue(topic, out t))
                MessageBox.Show(owner, t.Body, t.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // A light popup card that closes when you click elsewhere.
    class HelpPopup : Form
    {
        static int S(int px) { return Program.S(px); }

        public HelpPopup(string title, string body)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(252, 252, 255);
            Font = Program.UiFont;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(S(14), S(10), S(14), S(10));
            var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, BackColor = BackColor };
            t.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, S(6)) });
            t.Controls.Add(new Label { Text = body, AutoSize = true, MaximumSize = new Size(S(380), 0) });
            var ok = new LinkLabel { Text = "Got it", AutoSize = true, Margin = new Padding(0, S(10), 0, 0) };
            ok.LinkClicked += delegate { Close(); };
            t.Controls.Add(ok);
            Controls.Add(t);
            Deactivate += delegate { Close(); };
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Color.FromArgb(150, 160, 190))) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        public void ShowAt(Form owner, Point at)
        {
            Owner = owner;
            Location = at;
            Show(owner);
            // keep it on screen
            var wa = Screen.FromPoint(at).WorkingArea;
            int x = Math.Min(Left, wa.Right - Width - S(8)), y = Top;
            if (Bottom > wa.Bottom) y = Math.Max(wa.Top, at.Y - Height - S(30));
            Location = new Point(Math.Max(wa.Left, x), y);
            Activate();
        }
    }
}
