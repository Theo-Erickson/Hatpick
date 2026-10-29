using System;
using System.Collections.Generic;
using Avalonia.Controls.Shapes;

namespace Hatpick
{
    // The guided tour: dims the window except one part, and explains it in a card next to it.
    partial class MainWindow
    {
        class TourStep { public string Page, Mark, Title, Body; }

        List<TourStep> tourSteps;
        int tourIndex;

        void StartTour()
        {
            tourSteps = new List<TourStep>
            {
                new TourStep { Page = "overview", Mark = "rail-overview", Title = "The rail", Body = "Everything is one click away: Overview, Repos, Hats, Folders and Reset. Help and the theme switch are at the bottom." },
                new TourStep { Page = "overview", Mark = "hats", Title = "Your hats", Body = "Each hat is one of your accounts. Hard hat for Work, beanie for Personal - pick whatever helps you tell them apart." },
                new TourStep { Page = "overview", Mark = "attention", Title = "Needs attention", Body = "Repos where something's off show up here, with a one-click fix for the common cases." },
                new TourStep { Page = "overview", Mark = "repos", Title = "Every repo at a glance", Body = "Each card shows the hat a repo commits with and the hat it pushes with. Two different hats means they don't match. Click a card for details." },
                new TourStep { Page = "repos", Mark = "layers", Title = "Why a repo wears what it wears", Body = "Git reads settings in layers: this repo, then the folder default, then your global settings. The highlighted value is the one that wins." },
                new TourStep { Page = "repos", Mark = "set", Title = "Pick hats", Body = "Change who this repo commits and pushes as. Only this repo changes, and Reset puts it back." },
                new TourStep { Page = "repos", Mark = "commits", Title = "Fix commits", Body = "Change the author or remove co-authors on recent commits. Hatpick warns you before touching anything already pushed, and Undo puts it back." },
                new TourStep { Page = "folders", Mark = "rail-folders", Title = "Folders", Body = "Watch the folders where you keep repos and give each a default hat. You can also drop any folder onto the window." },
                new TourStep { Page = "overview", Mark = "help", Title = "Help is here", Body = "Take this tour again, rerun setup, or read how things work. The ? buttons around the app explain each part." },
            };
            tourIndex = 0;
            ShowTourStep();
        }

        void ShowTourStep()
        {
            // skip steps whose target isn't on screen (e.g. nothing needs attention)
            while (tourIndex < tourSteps.Count)
            {
                var st = tourSteps[tourIndex];
                if (page != st.Page) Navigate(st.Page);
                UpdateLayout();
                if (Marks.ContainsKey(st.Mark) && Marks[st.Mark].IsEffectivelyVisible && TopLevel.GetTopLevel(Marks[st.Mark]) != null) break;
                tourIndex++;
            }
            if (tourIndex >= tourSteps.Count) { EndTour(); return; }
            var step = tourSteps[tourIndex];
            var target = Marks[step.Mark];
            target.BringIntoView();
            UpdateLayout();

            var at = target.TranslatePoint(new Point(0, 0), root);
            if (at == null) { tourIndex++; ShowTourStep(); return; }
            var full = new Rect(0, 0, root.Bounds.Width, root.Bounds.Height);
            var r = new Rect(at.Value, target.Bounds.Size).Inflate(6).Intersect(full);

            overlay.Children.Clear();
            var shade = new Path { Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(r, 10, 10)) };
            shade.SetResourceReference(Shape.FillProperty, "Shade");
            shade.PointerPressed += delegate { EndTour(); };
            overlay.Children.Add(shade);
            var ring = new Border { Width = r.Width, Height = r.Height, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(r.Left, r.Top, 0, 0), IsHitTestVisible = false };
            ring.SetResourceReference(Border.BorderBrushProperty, "Accent");
            overlay.Children.Add(ring);

            // the card
            var body = new StackPanel();
            body.Children.Add(Ui.Text(step.Title, 16, "TourText", FontWeights.SemiBold));
            var text = Ui.Text(step.Body, 13.5, "TourText");
            text.Opacity = 0.9;
            text.Margin = new Thickness(0, 6, 0, 14);
            body.Children.Add(text);
            var bar = new DockPanel();
            var counter = Ui.Text((tourIndex + 1) + " of " + tourSteps.Count, 12, "TourText");
            counter.Opacity = 0.7;
            counter.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(counter, Dock.Left);
            bar.Children.Add(counter);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var skip = Ui.Btn("Skip", EndTour, "BtnLink");
            skip.SetResourceReference(TemplatedControl.ForegroundProperty, "TourText");
            var back = Ui.Btn("Back", delegate { tourIndex = Math.Max(0, tourIndex - 1); ShowTourStep(); }, "Btn");
            back.IsEnabled = tourIndex > 0;
            var next = Ui.Btn(tourIndex == tourSteps.Count - 1 ? "Done" : "Next", delegate { tourIndex++; if (tourIndex >= tourSteps.Count) EndTour(); else ShowTourStep(); }, "BtnPrimary");
            next.IsDefault = true;
            foreach (var b in new[] { skip, back, next }) { b.Margin = new Thickness(6, 0, 0, 0); btns.Children.Add(b); }
            bar.Children.Add(btns);
            body.Children.Add(bar);
            var card = new Border { Child = body, Padding = new Thickness(18, 16, 18, 14), CornerRadius = new CornerRadius(12), Width = 340, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            card.SetResourceReference(Border.BackgroundProperty, "TourCard");
            card.BoxShadow = Ui.Shadow(24, 6, 102);

            // right of the target if it fits, else below, else above
            double x, y;
            if (r.Right + 16 + 340 <= full.Width) { x = r.Right + 16; y = r.Top; }
            else if (r.Bottom + 16 + 190 <= full.Height) { x = r.Left; y = r.Bottom + 16; }
            else { x = r.Left; y = r.Top - 16 - 190; }
            x = Math.Max(12, Math.Min(x, full.Width - 352));
            y = Math.Max(12, Math.Min(y, full.Height - 200));
            card.Margin = new Thickness(x, y, 0, 0);
            overlay.Children.Add(card);
            overlay.IsVisible = true;
            next.Focus();
            overlay.RemoveHandler(KeyDownEvent, TourKeys);
            overlay.AddHandler(KeyDownEvent, TourKeys, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        void TourKeys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { EndTour(); e.Handled = true; }
        }

        void EndTour()
        {
            overlay.Children.Clear();
            overlay.IsVisible = false;
            tourSteps = null;
        }
    }
}
