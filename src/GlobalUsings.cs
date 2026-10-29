// The UI namespaces every page uses. Avalonia.Controls.Shapes isn't here: its Path would
// clash with System.IO.Path, so the files that draw shapes import it themselves.
global using Avalonia;
global using Avalonia.Controls;
global using Avalonia.Controls.Primitives;
global using Avalonia.Input;
global using Avalonia.Layout;
global using Avalonia.Media;
global using Avalonia.Threading;
global using FontWeights = Avalonia.Media.FontWeight;
global using Avalonia.Styling;
