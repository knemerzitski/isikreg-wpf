using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Model;
using IsikReg.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IsikReg.Controls {



  public partial class StatisticsGrid : Grid {

    public class TextCounter {

      public event Action<string>? TextChanged;
      public event Action<string>? PercentTextChanged;

      private int count = 0;
      public int Count {
        get => count;
        set {
          if (count == value) return;
          count = value;
          Update();
        }
      }

      private int total = 0;
      public int Total {
        get => total;
        set {
          if (total == value) return;
          total = value;
          Update();
        }
      }

      public string Text { get; private set; } = "0";
      public string PercentText { get; private set; } = "(0%)";

      public bool ShowTotalInText { get; set; }

      public void Update() {
        Text = count.ToString();
        if (ShowTotalInText) {
          Text += "/" + Total;
        }
        int percent = Total == 0 ? 0 : (int)Math.Round(((double)Count / Total * 100));
        PercentText = "(" + percent + "%)";

        TextChanged?.Invoke(Text);
        PercentTextChanged?.Invoke(PercentText);
      }

    }

    private static readonly string REGISTERED_KEY = Config.Instance.Columns[ColumnId.REGISTERED].Key;

    private readonly List<string> registrationTypes = Config.Instance.GetRegistrationTypes();

    private readonly Dictionary<Column, Dictionary<string, Dictionary<object, TextCounter>>> counterMap = new();
    private readonly Dictionary<Column, Dictionary<object, Property<int>>> totalMap = new();
    private readonly Dictionary<Column, Grid> columnGridMap = new();

    private readonly Property<int> personCountProperty = new();

    private readonly int horizontalGap = 2;
    private readonly int verticalGap = 2;

    public StatisticsGrid() {
      InitializeComponent();

      if (Config.Instance.Columns.All(c => c.Statistics == null)) {
        Visibility = Visibility.Collapsed;
        return;
      }

      InitColumns();

      ResetCounters();

      foreach (Person p in PersonDictionary.Instance.Values) {
        AddPerson(p);
      }
      PersonDictionary.Instance.Added += AddPerson;
      PersonDictionary.Instance.Removed += RemovePerson;
      PersonDictionary.Instance.Cleared += ResetCounters;
    }

    private void InitColumns() {
      Color borderColor = Resources["BorderLightColor"] is Color bc ? bc : Color.FromRgb(204, 204, 204);

      // Create initial labels based on option values, and listeners do dynamically update visuals
      foreach (Column column in Config.Instance.Columns) {
        if (column.Statistics == null) continue;

        counterMap[column] = new();
        totalMap[column] = new();
        foreach (string type in registrationTypes) {
          counterMap[column][type] = new();
        }

        Grid grid = new();
        columnGridMap[column] = grid;

        Grid.SetRow(grid, 1);
        Children.Add(grid);

        if (ColumnDefinitions.Count > 0) {
          Border border = new() {
            BorderThickness = new Thickness(1, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(borderColor),
          };
          Grid.SetRowSpan(border, 3);
          Grid.SetColumn(border, ColumnDefinitions.Count);
          Children.Add(border);

          Grid.SetColumn(grid, ColumnDefinitions.Count + 1);

          ColumnDefinitions.Add(new() { Width = new GridLength(10) });
        } else {
          Grid.SetColumn(grid, 0);
        }
        ColumnDefinitions.Add(new() { Width = GridLength.Auto });



        // Rows for registration types
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new GridLength(verticalGap) });
        for (int i = 0; i < registrationTypes.Count; i++) {
          grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
          grid.RowDefinitions.Add(new() { Height = new GridLength(verticalGap) });
        }
        grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);

        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        TextBlock title = new() {
          Text = column.Label,
          TextDecorations = TextDecorations.Underline,
        };
        grid.Children.Add(title);

        for (int i = 0; i < registrationTypes.Count; i++) {
          string type = registrationTypes[i];

          TextBlock typeBlock = new() { Text = type };
          grid.Children.Add(typeBlock);
          Grid.SetRow(typeBlock, 2 * (i + 1));
        }

        if (column is OptionsColumn optionsColumn) {
          foreach (string option in optionsColumn.GetOptionValues()) {
            AddText(column, option);
          }
        } else if (column is CheckBoxColumn) {
          Grid.SetColumnSpan(title, column.Statistics.Percent ? 5 : 3);
          AddText(column, "");
        }
      }

    }

    private void AddText(Column column, string text) {
      if (column.Statistics == null) return;

      if (column is CheckBoxColumn) {
        totalMap[column][text] = personCountProperty;
      } else {
        totalMap[column][text] = new(0);
      }

      // column count is 1, must add it to index 2
      Grid grid = columnGridMap[column];

      TextBlock optionBlock = new() { Text = text };
      grid.Children.Add(optionBlock);
      Grid.SetColumn(optionBlock, grid.ColumnDefinitions.Count + 1);

      if (column.Statistics.Percent) {
        Grid.SetColumnSpan(optionBlock, 3);
        // With Percent
        for (int i = 0; i < registrationTypes.Count; i++) {
          string type = registrationTypes[i];

          TextCounter counter = new() {
            ShowTotalInText = column.Statistics.Total
          };
          totalMap[column][text].PropertyChanged += (_, total) => counter.Total = total;
          counterMap[column][type][text] = counter;

          TextBlock countBlock = new() {
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left,
          };
          counter.TextChanged += (text) => countBlock.Text = text;

          grid.Children.Add(countBlock);
          Grid.SetColumn(countBlock, grid.ColumnDefinitions.Count + 1);
          Grid.SetRow(countBlock, 2 * (i + 1));

          TextBlock percentBlock = new() {
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left,
          };
          counter.PercentTextChanged += (text) => percentBlock.Text = text;

          grid.Children.Add(percentBlock);
          Grid.SetColumn(percentBlock, grid.ColumnDefinitions.Count + 3);
          Grid.SetRow(percentBlock, 2 * (i + 1));
        }
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(horizontalGap) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(horizontalGap) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
      } else {
        // Without percent
        for (int i = 0; i < registrationTypes.Count; i++) {
          string type = registrationTypes[i];

          TextCounter counter = new();
          if (column.Statistics.Total) {
            counter.ShowTotalInText = true;
            totalMap[column][text].PropertyChanged += (_, total) => counter.Total = total;
          }
          counterMap[column][type][text] = counter;

          TextBlock countBlock = new() {
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left,
          };
          counter.TextChanged += (text) => countBlock.Text = text;

          grid.Children.Add(countBlock);
          Grid.SetColumn(countBlock, grid.ColumnDefinitions.Count + 1);
          Grid.SetRow(countBlock, 2 * (i + 1));
        }
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(horizontalGap) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
      }
    }

    private void OnPropertyChanged(string? oldType, string? newType, string key, object? oldValue, object? newValue, Registration? registration = null) {
      if (Config.Instance.Columns.TryGetValue(key, out Column? column)) {
        if (column.Statistics != null) {
          if (counterMap.TryGetValue(column, out var columnDict)) {
            if (column is CheckBoxColumn) {
              if (oldValue is bool ob && ob) {
                UpdateCount(column, columnDict, oldType, "", -1);
              }
              if (newValue is bool nb && nb) {
                UpdateCount(column, columnDict, newType, "", 1);
              }
            } else {
              UpdateCount(column, columnDict, oldType, oldValue, -1);
              UpdateCount(column, columnDict, newType, newValue, 1);
            }
          }

          if (column is not CheckBoxColumn) {
            UpdateTotal(column, oldValue, -1);
            UpdateTotal(column, newValue, 1);
          }
        }

        switch (column.Id) {
          case ColumnId.REGISTER_DATE:
            OnPropertyChanged(oldType, newType, REGISTERED_KEY, oldValue is DateTime, newValue is DateTime);
            break;
          case ColumnId.REGISTRATION_TYPE:
            if (registration != null
                && registration == registration.Person.LatestRegisteredRegistration
                && oldValue != newValue) {
              string? oldType2 = oldValue as string;
              string? newType2 = newValue as string;
              foreach ((string k, object v) in registration) {
                OnPropertyChanged(oldType2, newType2, k, v, v);
              }
            }
            break;
        }
      }
    }

    private void AddPerson(Person person) {
      personCountProperty.Value++;

      // Count totals
      foreach ((string key, object value) in person.LatestRegisteredRegistration ?? person.GetLatestRegistration()) {
        OnPropertyChanged(null, null, key, null, value);
      }

      person.Properties.PropertyChanged += (key, oldValue, newValue, a) => {
        OnPropertyChanged(person.RegisteredType, person.RegisteredType, key, oldValue, newValue);
      };

      Dictionary<Registration, DictionaryChangedEventHandler<string, object>> listeners = new();
      void latestRegisteredChanged(Registration? oldReg, Registration? newReg) {
        if (oldReg != null) {
          oldReg.Properties.PropertyChanged -= listeners[oldReg];
          listeners.Remove(oldReg);

          foreach ((string key, object value) in oldReg) {
            OnPropertyChanged(oldReg.RegistrationType, null, key, value, value);
          }
        } else {
          foreach (Registration r in person.Registrations) {
            if (listeners.TryGetValue(r, out var listener)) {
              r.Properties.PropertyChanged -= listener;
              listeners.Remove(r);
            }
          }
        }

        if (newReg != null) {
          foreach ((string key, object value) in newReg) {
            OnPropertyChanged(null, newReg.RegistrationType, key, value, value);
          }

          listeners[newReg] = (k, o, n, a) => {
            OnPropertyChanged(newReg.RegistrationType, newReg.RegistrationType, k, o, n, newReg);
          };
          newReg.Properties.PropertyChanged += listeners[newReg];
        } else {
          Registration anyReg = person.GetLatestRegistration();
          listeners[anyReg] = (k, o, n, a) => {
            OnPropertyChanged(null, null, k, o, n);
          };
          anyReg.Properties.PropertyChanged += listeners[anyReg];
        }
      }

      latestRegisteredChanged(null, person.LatestRegisteredRegistration);

      person.LatestRegisteredRegistrationProperty.PropertyChanged += latestRegisteredChanged;
    }

    private void RemovePerson(Person person) {
      personCountProperty.Value--;
    }

    private void ResetCounters() {
      personCountProperty.Value = 0;

      foreach (var a in counterMap.Values) {
        foreach (var b in a.Values) {
          foreach (var c in b.Values) {
            c.Count = 0;
            c.Total = 0;
            c.Update();
          }
        }
      }
      foreach (var a in totalMap.Values) {
        foreach (var b in a.Keys) {
          a[b].Value = 0;
        }
      }
    }

    private void UpdateCount(Column column, Dictionary<string, Dictionary<object, TextCounter>> columnDict, string? type, object? option, int amount) {
      if (type != null && option != null && columnDict.TryGetValue(type, out var newObjDict)) {
        if (newObjDict.TryGetValue(option, out TextCounter? count)) {
          count.Count += amount;
        } else if (option is string newString) {
          AddText(column, newString);
          if (newObjDict.TryGetValue(newString, out TextCounter? count2)) {
            count2.Count += amount;
          }
        }
      }
    }

    private void UpdateTotal(Column column, object? value, int amount) {
      if (value != null && totalMap.TryGetValue(column, out var dict)) {
        if (dict.TryGetValue(value, out Property<int>? prop)) {
          prop.Value += amount;
        } else if (value is string valueString) {
          AddText(column, valueString);
          if (dict.TryGetValue(value, out Property<int>? prop2)) {
            prop2.Value += amount;
          }
        }
      }
    }
  }
}
