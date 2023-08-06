using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using IsikReg.Extensions;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using System.Windows.Media;

namespace IsikReg.Control {

  public class Dialog: Window {

    public enum DialogType {
      None,
      Confirmation,
      Warning,
      Error
    }

    private readonly DockPanel windowPanel = new() {
    };

    private readonly Border headerBorder = new() {
      BorderThickness = new Thickness(0, 0, 0, 1),
      BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
      Padding = new Thickness(10),
    };
    private readonly DockPanel headerPanel = new() {
      VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock headerTextBlock = new() {
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(0),
      Padding = new Thickness(0),
      FontSize = 14,
      TextWrapping = TextWrapping.WrapWithOverflow,
    };

    private readonly Border iconTextBlockBorder = new() {
      HorizontalAlignment = HorizontalAlignment.Right,
      VerticalAlignment = VerticalAlignment.Center,
      MinWidth = 42,
      MinHeight = 42,
      Margin = new Thickness(20, 0, 0, 0),
    };
    private readonly TextBlock iconTextBlock = new() {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      FontSize = 42,
      Margin = new Thickness(0),
      Padding = new Thickness(0),
      FontWeight = FontWeights.ExtraBold,
    };

    private readonly Border contentFooterBorder = new() {
      Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
      Padding = new Thickness(10),
    };
    private readonly DockPanel contentFooterPanel = new() {
    };
    private readonly DockPanel contentPanel = new() {
    };
    private readonly StackPanel footerPanel = new() {
      HorizontalAlignment = HorizontalAlignment.Right,
      VerticalAlignment = VerticalAlignment.Bottom,
      Orientation = Orientation.Horizontal,
      Margin = new Thickness(20, 0, 0, 0),
    };

    private UIElement? content;

    public string? HeaderText {
      get {
        return headerTextBlock?.Text.ToString();
      }
      set {
        headerTextBlock.Text = value;
      }
    }

    private DialogType dialogType = DialogType.None;
    public DialogType Type {
      get => dialogType;
      set {
        dialogType = value;
        switch (value) {
          case DialogType.None:
            iconTextBlock.Text = String.Empty;
            break;
          case DialogType.Confirmation:
            iconTextBlock.Text = "?";
            iconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(48, 113, 242));
            break;
          case DialogType.Warning:
            iconTextBlock.Text = "!";
            iconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(242, 187, 48));
            break;
          case DialogType.Error:
            iconTextBlock.Text = "X";
            iconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(242, 48, 48));
            break;
        }
      }
    }

    public string? ContentText {
      get {
        if (content is TextBlock label) {
          return label.Text;
        } else {
          return null;
        }
      }
      set {
        if (!string.IsNullOrWhiteSpace(value)) {
          content = new TextBlock {
            Text = value,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWithOverflow,
          };
        } else {
          content = null;
        }
      }
    }

    public new UIElement? Content {
      get => content;
      set {
        if (value != null) {
          content = new ScrollViewer() {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = value,
          };
        } else {
          content = null;
        }
      }
    }

    public Dialog(Window owner) {
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      SizeToContent = SizeToContent.WidthAndHeight;
      ResizeMode = ResizeMode.NoResize;
      MinWidth = 360;
      MinHeight = 120;

      Owner = owner;
      base.Content = windowPanel;

      MaxWidth = Math.Min(owner.Width, SystemParameters.WorkArea.Width - 256);
      MaxHeight = Math.Min(owner.Height, SystemParameters.WorkArea.Height - 256);
    }

    public Button AddButton(string name, bool result) {
      Button button = new() {
        Content = name,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        //Padding = new Thickness(8, 4, 8, 4),
        MinWidth = 75,
        IsDefault = footerPanel.Children.Count == 0,
      };
      button.Click += (s, e) => {
        DialogResult = result;
        Close();
      };

      footerPanel.Children.Add(button);
      return button;
    }

    /// <summary>
    ///  Can only be called once!
    /// </summary>
    /// <returns></returns>
    public bool ShowAndWait() {
      if (!string.IsNullOrWhiteSpace(HeaderText)) {
        headerPanel.Children.Add(headerTextBlock);

        if(dialogType != DialogType.None) {
          iconTextBlockBorder.Child = iconTextBlock;
          headerPanel.Children.Add(iconTextBlockBorder);
        }

        headerBorder.Child = headerPanel;
        windowPanel.Children.Add(headerBorder);
        DockPanel.SetDock(headerBorder, Dock.Top);
      }

      if (content != null) {
        contentPanel.Children.Add(content);
        contentFooterPanel.Children.Add(contentPanel);
        DockPanel.SetDock(contentPanel, Dock.Top);
      }

      footerPanel.Children.SetGap(10);

      contentFooterPanel.Children.Add(footerPanel);
      DockPanel.SetDock(footerPanel, Dock.Bottom);
      contentFooterPanel.Children.SetGap(0, 10);

      contentFooterBorder.Child = contentFooterPanel;
      windowPanel.Children.Add(contentFooterBorder);
      DockPanel.SetDock(contentFooterBorder, Dock.Bottom);

      return ShowDialog() ?? false;
    }


  }
}
