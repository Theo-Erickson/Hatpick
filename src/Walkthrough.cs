using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Hatpick
{
    // A short guided tour: outlines one part of the window at a time and explains it
    // in a card next to it, with Back / Next / Done.
    class Walkthrough
    {
        public class Step
        {
            public string Title, Body;
            public Func<Rectangle> Target;   // screen rectangle to outline
            public Action Before;            // optional: prepare the window for this step
        }

        static int S(int px) { return Program.S(px); }

        readonly Form owner;
        readonly List<Step> steps;
        int index;
        Form[] bars;   // four thin windows that together outline the target
        Form card;
        Label title, body, counter;
        Button back, next;

        public Walkthrough(Form owner, List<Step> steps)
        {
            this.owner = owner; this.steps = steps;
        }

        public void Start()
        {
            bars = new Form[4];
            for (int i = 0; i < 4; i++) bars[i] = new Bar();
            card = new Form
            {
                FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                BackColor = Color.FromArgb(34, 40, 66), ForeColor = Color.White, Font = Program.UiFont,
                Size = new Size(S(360), S(200)), KeyPreview = true
            };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(S(16), S(12), S(16), S(12)) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            title = new Label { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(0, 0, 0, S(6)) };
            body = new Label { AutoSize = true, MaximumSize = new Size(S(328), 0) };
            t.Controls.Add(title); t.Controls.Add(body);
            var row = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            counter = new Label { AutoSize = true, ForeColor = Color.FromArgb(180, 190, 215), Anchor = AnchorStyles.Left };
            var skip = new LinkLabel { Text = "Skip tour", TabStop = false, AutoSize = true, LinkColor = Color.FromArgb(180, 200, 240), Anchor = AnchorStyles.Right, Margin = new Padding(0, S(6), S(10), 0) };
            skip.LinkClicked += delegate { Stop(); };
            back = new Button { Text = "Back", Width = S(70), Height = S(28), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            next = new Button { Text = "Next", Width = S(70), Height = S(28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(236, 122, 48), ForeColor = Color.White };
            back.Click += delegate { Go(index - 1); };
            next.Click += delegate { if (index >= steps.Count - 1) Stop(); else Go(index + 1); };
            row.Controls.Add(counter, 0, 0); row.Controls.Add(skip, 1, 0); row.Controls.Add(back, 2, 0); row.Controls.Add(next, 3, 0);
            t.Controls.Add(row);
            card.Controls.Add(t);
            card.AcceptButton = next;   // Enter = Next
            card.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Stop();
                else if (e.KeyCode == Keys.Right) next.PerformClick();
                else if (e.KeyCode == Keys.Left && back.Enabled) back.PerformClick();
            };
            owner.Move += Reposition; owner.Resize += Reposition;
            foreach (var b in bars) b.Show(owner);
            card.Show(owner);
            Go(0);
        }

        void Go(int i)
        {
            index = Math.Max(0, Math.Min(i, steps.Count - 1));
            var st = steps[index];
            if (st.Before != null) st.Before();
            title.Text = st.Title;
            body.Text = st.Body;
            counter.Text = (index + 1) + " of " + steps.Count;
            back.Enabled = index > 0;
            next.Text = index == steps.Count - 1 ? "Done" : "Next";
            card.Height = Math.Max(S(170), title.Height + body.PreferredSize.Height + S(100));
            Reposition(null, EventArgs.Empty);
            card.Activate();
            card.ActiveControl = next;
        }

        void Reposition(object sender, EventArgs e)
        {
            if (card == null || card.IsDisposed) return;
            Rectangle r = steps[index].Target();
            r.Inflate(S(4), S(4));
            int w = Math.Max(3, S(3));
            bars[0].Bounds = new Rectangle(r.Left, r.Top, r.Width, w);              // top
            bars[1].Bounds = new Rectangle(r.Left, r.Bottom - w, r.Width, w);       // bottom
            bars[2].Bounds = new Rectangle(r.Left, r.Top, w, r.Height);             // left
            bars[3].Bounds = new Rectangle(r.Right - w, r.Top, w, r.Height);        // right
            // Put the card to the right of the target, else below, else to the left; keep it on screen.
            var wa = Screen.FromControl(owner).WorkingArea;
            Point p;
            if (r.Right + S(12) + card.Width <= wa.Right) p = new Point(r.Right + S(12), r.Top);
            else if (r.Bottom + S(12) + card.Height <= wa.Bottom) p = new Point(r.Left, r.Bottom + S(12));
            else p = new Point(r.Left - S(12) - card.Width, r.Top);
            p.X = Math.Max(wa.Left, Math.Min(p.X, wa.Right - card.Width));
            p.Y = Math.Max(wa.Top, Math.Min(p.Y, wa.Bottom - card.Height));
            card.Location = p;
        }

        void Stop()
        {
            owner.Move -= Reposition; owner.Resize -= Reposition;
            if (bars != null) foreach (var b in bars) b.Close();
            if (card != null) card.Close();
            card = null;
            owner.Activate();
        }

        // One orange edge of the outline. Clicks pass through to the window underneath.
        class Bar : Form
        {
            public Bar()
            {
                FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
                BackColor = Color.FromArgb(236, 122, 48);
                MinimumSize = new Size(1, 1);
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1;
                if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
                base.WndProc(ref m);
            }
        }
    }
}
