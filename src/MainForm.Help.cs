using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Hatpick
{
    // Help menu, the guided tour and first-time setup.
    partial class MainForm
    {
        ToolStripDropDownButton helpMenu;
        ToolStripButton accountsButton, foldersButton;

        void BuildHelpMenu()
        {
            helpMenu = new ToolStripDropDownButton("? Help") { Alignment = ToolStripItemAlignment.Right };
            helpMenu.DropDownItems.Add("Take the tour", null, delegate { StartTour(); });
            helpMenu.DropDownItems.Add("Run setup again...", null, delegate { RunSetup(); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("How Hatpick works", null, delegate { HelpUi.ShowDialog(this, "overview"); });
            helpMenu.DropDownItems.Add("Watched folders vs. single repos", null, delegate { HelpUi.ShowDialog(this, "folders"); });
            helpMenu.DropDownItems.Add("What the status dots mean", null, delegate { HelpUi.ShowDialog(this, "list"); });
            helpMenu.DropDownItems.Add("Resetting", null, delegate { HelpUi.ShowDialog(this, "reset"); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("Sign in an account to Git Credential Manager...", null, delegate { SignInAccount(); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("About Hatpick", null, delegate
            {
                MessageBox.Show(this, "Hatpick " + Application.ProductVersion + "\n\nPicks which Git account each repo commits and pushes as.\n" +
                    "It never signs in or sees a password or token; Git Credential Manager keeps your sign-ins.\n\nUsing git at:\n" + Git.Exe,
                    "About Hatpick", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            // Right-aligned items are laid out right to left, so this lands left of Options.
            toolbar.Items.Insert(toolbar.Items.IndexOf(optionsMenu), helpMenu);
        }

        void SignInAccount()
        {
            if (MessageBox.Show(this, "This opens Git Credential Manager's own sign-in: a small console window and your browser, where you sign in to GitHub.\n\n" +
                "Hatpick doesn't see anything you enter. When the console closes, Hatpick refreshes the list of signed-in accounts.",
                Program.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            try
            {
                var p = Git.StartGcmLogin();
                p.EnableRaisingEvents = true;
                p.Exited += delegate { try { BeginInvoke((Action)Rescan); } catch { } };
            }
            catch (Exception e) { MessageBox.Show(this, "Couldn't start Git Credential Manager:\n" + e.Message, Program.Title); }
        }

        // ---------- first-time setup ----------

        bool NeedsSetup { get { return !settings.setupDone; } }

        void RunSetup()
        {
            bool startTour = false;
            using (var w = new SetupWizard(settings))
            {
                var result = w.ShowDialog(this);
                settings.setupDone = true;
                settings.Save();
                SyncFolders();
                if (result == DialogResult.OK)
                {
                    try
                    {
                        if (w.AddExplorerMenu && !ExplorerMenu.Installed) ExplorerMenu.Install(Program.ExePath);
                        else if (!w.AddExplorerMenu && ExplorerMenu.Installed) ExplorerMenu.Remove();
                    }
                    catch { }
                    startTour = w.StartTour;
                }
            }
            Rescan();
            if (startTour) BeginInvoke((Action)StartTour);
        }

        // ---------- guided tour ----------

        Rectangle ScreenRect(Control c) { return c.RectangleToScreen(c.ClientRectangle); }
        Rectangle ScreenRect(ToolStripItem i) { return toolbar.RectangleToScreen(i.Bounds); }

        void StartTour()
        {
            var steps = new List<Walkthrough.Step>
            {
                new Walkthrough.Step { Title = "Your repos", Target = () => ScreenRect(split.Panel1),
                    Body = "Every repo Hatpick knows about, grouped by watched folder. The dot is its status: green is all set, blue is fine with notes, " +
                           "yellow needs attention, red isn't set up." },
                new Walkthrough.Step { Title = "Search", Target = () => ScreenRect(search),
                    Body = "Type part of a repo's name, folder or account to filter the list." },
                new Walkthrough.Step { Title = "The report", Target = () => ScreenRect(reportHost),
                    Before = delegate { if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = true; },
                    Body = "For the selected repo: who it commits as, who it pushes as, where each setting comes from, and anything that's off. " +
                           "The ? buttons explain each line." },
                new Walkthrough.Step { Title = "Set this repo", Target = ChangeBoxRect,
                    Body = "Pick who this repo commits and pushes as, then Apply. It only changes this repo, and Reset this repo puts back what it had before." },
                new Walkthrough.Step { Title = "Add repos", Target = () => ScreenRect(addMenu),
                    Body = "Watch a folder to list every repo inside it, add a single repo that lives somewhere else, or analyze any folder without adding it. " +
                           "You can also drop a folder onto the window." },
                new Walkthrough.Step { Title = "Accounts", Target = () => ScreenRect(accountsButton),
                    Body = "Your accounts: a commit name and email plus the GitHub username Git Credential Manager signs in as." },
                new Walkthrough.Step { Title = "Watched folders", Target = () => ScreenRect(foldersButton),
                    Body = "The folders Hatpick scans, and each one's default account. A default applies to every repo in the folder, including new ones." },
                new Walkthrough.Step { Title = "Reset", Target = () => ScreenRect(resetMenu),
                    Body = "Undo what Hatpick changed - for the selected repo, or everything at once, including folder defaults." },
                new Walkthrough.Step { Title = "Help", Target = () => ScreenRect(helpMenu),
                    Body = "Take this tour again, rerun setup, read how Hatpick works, or sign another account in to Git Credential Manager." },
            };
            new Walkthrough(this, steps).Start();
        }

        Rectangle ChangeBoxRect()
        {
            var box = report.Controls.OfType<GroupBox>().FirstOrDefault();
            return box != null ? ScreenRect(box) : ScreenRect(reportHost);
        }
    }
}
