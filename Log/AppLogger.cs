#if DEBUG
using System;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Linq;

namespace IsikReg.Log {
  public class AppLogger {

    private static readonly Lazy<AppLogger> lazyInstance = new(() => new AppLogger(Application.Current.MainWindow));
    public static AppLogger Instance { get => lazyInstance.Value; }

    private readonly TextBlock logText;
    private readonly ScrollViewer scrollViewer;

    public AppLogger(Window owner) {
      Window window = new() {
        Title = "Console",
        Width = SystemParameters.WorkArea.Width,
        Height = 256,
        Left = SystemParameters.WorkArea.Left,
        Top = SystemParameters.WorkArea.Bottom - 256,
        //Owner = owner,
      };
      owner.Closed += (s, e) => {
        window.Close();
      };

      scrollViewer = new() {
        Padding = new Thickness(5),
      };
      scrollViewer.Background = new SolidColorBrush {
        Color = Color.FromRgb(224, 224, 224),

      };
      window.Content = scrollViewer;

      logText = new() {
        FontSize = 16,
        TextWrapping = TextWrapping.WrapWithOverflow,
      };
      scrollViewer.Content = logText;
      window.Show();
    }

    public void Log(params object?[] messages) {
      string line = string.Join(' ', messages) + "\n";
      logText.Dispatcher.Invoke(() => {
        bool atEnd = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;
        logText.Text += line;
        if (atEnd) {
          scrollViewer.ScrollToEnd();
        }
      });
    }

  }
}
#endif
