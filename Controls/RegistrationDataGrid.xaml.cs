using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Converters;
using IsikReg.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

namespace IsikReg.Controls {
  /// <summary>
  /// Interaction logic for RegistrationDataGrid.xaml
  /// </summary>
  public partial class RegistrationDataGrid : DataGrid {


    private static readonly DependencyProperty PlaceholderProperty =
      DependencyProperty.Register("Placeholder", typeof(string),
        typeof(RegistrationDataGrid), new PropertyMetadata(default(string)));

    public string Placeholder {
      get { return (string)GetValue(PlaceholderProperty); }
      set { SetValue(PlaceholderProperty, value); }
    }


    private readonly RangeObservableCollection<Registration> registrations = new();
    private readonly RangeObservableCollection<Person> persons = new();
    private readonly ICollectionView filteredPersons;

    public RegistrationDataGrid() {
      InitializeComponent();

      CreateDataGridColumns();

      filteredPersons = CollectionViewSource.GetDefaultView(persons);
      filteredPersons.CollectionChanged += OnFilteredPersonCollectionChanged;

      foreach (Person p in PersonDictionary.Instance.Values) {
        AddPerson(p);
      }

      PersonDictionary.Instance.Added += AddPerson;
      PersonDictionary.Instance.Removed += RemovePerson;
      PersonDictionary.Instance.Cleared += Clear;

      UpdateRegistrations();

      ItemsSource = registrations;

      //    if (settings.general.columnResizePolicy == Settings.ColumnResizePolicy.CONSTRAINED) {
      //      table.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY);
      //    } else if (settings.general.columnResizePolicy == Settings.ColumnResizePolicy.UNCONSTRAINED) {
      //      table.setColumnResizePolicy(TableView.UNCONSTRAINED_RESIZE_POLICY);
      //    }

      //Copy row personalCode to clipboard on selection
      SelectionChanged += (s, e) => {
        if (!App.IsApplicationThread()) return;
        if (e.Source is DataGrid g && g.SelectedItem is Registration r && r != null) {
          string code = r.Person.PersonalCode;
          Clipboard.SetText(code);
        }
      };
    }

    public void Filter(string text) {
      Predicate<object> filterPredicate = text.Length == 0 ? o => true : o => {
        if (o is Person p) {
          return p.PersonalCode.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            p.LastName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            p.FirstName.Contains(text, StringComparison.OrdinalIgnoreCase);
        } else {
          return false;
        }
      };

      UnselectAll();
      filteredPersons.CollectionChanged -= OnFilteredPersonCollectionChanged;
      filteredPersons.Filter = filterPredicate;
      filteredPersons.CollectionChanged += OnFilteredPersonCollectionChanged;

      UpdateRegistrations();
    }

    private void OnFilteredPersonCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      switch (e.Action) {
        case NotifyCollectionChangedAction.Reset:
          UpdateRegistrations();
          break;
        default:
          if (e.NewItems != null) {
            foreach (Person p in e.NewItems) {
              AddRegistrations(p.Registrations);
            }
          }
          if (e.OldItems != null) {
            foreach (Person p in e.OldItems) {
              RemoveRegistrations(p.Registrations);
            };
          }
          break;
      }
    }

    private void OnRegistrationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      if (e.Action == NotifyCollectionChangedAction.Reset) {
        registrations.Where(r => r.Removed).ToList().ForEach(r => registrations.Remove(r));
      } else {
        if (e.NewItems != null) {
          foreach (Registration r in e.NewItems) {
            Person p = r.Person;
            Registration? r2 = p.GetLatestRegistration(r);
            int index = r2 != null ? registrations.IndexOf(r2) : -1;
            if (index != -1) {
              registrations.Insert(index + 1, r);
            } else {
              registrations.Add(r);
            }
          }
        }
        if (e.OldItems != null) {
          foreach (Registration r in e.OldItems) {
            registrations.Remove(r);
          };
        }
      }
    }

    private void UpdateRegistrations() {
      registrations.SuppressCollectionChanges = true;

      registrations.Clear();
      foreach (Person p in filteredPersons) {
        AddRegistrations(p.Registrations);
      }

      registrations.SuppressCollectionChanges = false;
    }

    private void Clear() {
      registrations.Clear();
      persons.Clear();
    }

    private void AddPerson(Person person) {
      persons.Add(person);
      person.Registrations.CollectionChanged += OnRegistrationCollectionChanged;
    }

    private void RemovePerson(Person person) {
      persons.Remove(person);
      person.Registrations.CollectionChanged -= OnRegistrationCollectionChanged;
    }

    private void AddRegistrations(IEnumerable<Registration> rs) {
      foreach (Registration r in rs) {
        registrations.Add(r);
      }
    }

    private void RemoveRegistrations(IEnumerable<Registration> rs) {
      foreach (Registration r in rs) {
        registrations.Remove(r);
      }
    }


    private void CreateDataGridColumns() {
      Columns.Clear();

      List<DataGridColumn> dataGridColumns = new();
      foreach (Column column in Config.Instance.Columns) {
        if (column.Table == null || string.IsNullOrWhiteSpace(column.Label))
          continue;

        DataGridColumn dataGridColumn;
        if (column is DateColumn dateColumn) {
          dataGridColumn = CreateDataGridDateColumn(dateColumn);
        } else if (column is RadioColumn radioColumn) {
          dataGridColumn = CreateDataGridRadioColumn(radioColumn);
        } else if (column is ComboBoxColumn comboBoxColumn) {
          dataGridColumn = CreateDataGridComboBoxColumn(comboBoxColumn);
        } else if (column is CheckBoxColumn checkBoxColumn) {
          dataGridColumn = CreateDataGridCheckBoxColumn(checkBoxColumn);
        } else {
          dataGridColumn = CreateDataGridTextColumn(column);
        }
        dataGridColumns.Add(dataGridColumn);
      }

      foreach (DataGridColumn dataGridColumn in dataGridColumns) {
        Columns.Add(dataGridColumn);
      }
    }

    private static DataGridTemplateColumn CreateDataGridDateColumn(DateColumn dateColumn) {
      if (dateColumn.Table?.Editable != true) {
        return CreateDataGridTextColumn(dateColumn, dateColumn.DateFormat);
      }

      string bindingPath = Registration.GetBindingPath(dateColumn);

      DataGridTemplateColumn gridColumn = new() {
        Header = dateColumn.Label,
        CanUserSort = true,
        SortMemberPath = bindingPath,
      };

      FrameworkElementFactory textBlockFactory = new(typeof(TextBlock));
      textBlockFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
      textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
      textBlockFactory.SetBinding(TextBlock.TextProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.OneWay,
          StringFormat = dateColumn.DateFormat,
          //IsAsync = true,
        });
      gridColumn.CellTemplate = new DataTemplate {
        VisualTree = textBlockFactory,
      };

      FrameworkElementFactory datePickerFactory = new(typeof(DatePicker));
      datePickerFactory.SetValue(DatePicker.VerticalContentAlignmentProperty, VerticalAlignment.Center);
      datePickerFactory.SetValue(DatePicker.LanguageProperty, dateColumn.DateFormatLanguage);
      datePickerFactory.SetBinding(DatePicker.SelectedDateProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.TwoWay,
          UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
        });
      gridColumn.CellEditingTemplate = new DataTemplate {
        VisualTree = datePickerFactory,
      };

      return gridColumn;
    }

    private static DataGridTemplateColumn CreateDataGridRadioColumn(RadioColumn radioColumn) {
      if (radioColumn.Table?.Editable != true) {
        return CreateDataGridTextColumn(radioColumn);
      }

      string bindingPath = Registration.GetBindingPath(radioColumn);

      DataGridTemplateColumn gridColumn = new() {
        Header = radioColumn.Label,
        CanUserSort = true,
        SortMemberPath = bindingPath,
      };

      FrameworkElementFactory textBlockFactory = new(typeof(TextBlock));
      textBlockFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
      textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
      textBlockFactory.SetBinding(TextBlock.TextProperty,
      new Binding(bindingPath) {
        Mode = BindingMode.OneWay,
        //IsAsync = true,
      });
      gridColumn.CellTemplate = new DataTemplate {
        VisualTree = textBlockFactory,
      };

      FrameworkElementFactory comboBoxFactory = new(typeof(ComboBox));
      comboBoxFactory.SetValue(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
      comboBoxFactory.SetValue(ComboBox.ItemsSourceProperty, radioColumn.GetOptionValues());
      comboBoxFactory.SetBinding(ComboBox.SelectedValueProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.TwoWay,
          UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
      gridColumn.CellEditingTemplate = new DataTemplate {
        VisualTree = comboBoxFactory,
      };

      return gridColumn;
    }

    private static DataGridTemplateColumn CreateDataGridComboBoxColumn(ComboBoxColumn comboBoxColumn) {
      if (comboBoxColumn.Table?.Editable != true) {
        return CreateDataGridTextColumn(comboBoxColumn);
      }

      string bindingPath = Registration.GetBindingPath(comboBoxColumn);

      ObservableCollection<string> values = new(comboBoxColumn.GetOptionValues());
      if (comboBoxColumn.Form != null && (comboBoxColumn.Form.autofillPattern != null || comboBoxColumn.Form.IsSimpleAutofill())) {
        foreach (string value in comboBoxColumn.Form.autofillValues) {
          values.Add(value);
        }
        comboBoxColumn.Form.autofillValues.CollectionChanged += (s, e) => {
          if (e.NewItems != null) {
            foreach (string value in e.NewItems) {
              values.Add(value);
            }
          }
          if (e.OldItems != null) {
            foreach (string value in e.OldItems) {
              values.Remove(value);
            }
          }
        };
      }

      DataGridTemplateColumn gridColumn = new() {
        Header = comboBoxColumn.Label,
        CanUserSort = true,
        SortMemberPath = bindingPath,
      };

      FrameworkElementFactory textBlockFactory = new(typeof(TextBlock));
      textBlockFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
      textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
      textBlockFactory.SetBinding(TextBlock.TextProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.OneWay,
          //IsAsync = true,
        });
      gridColumn.CellTemplate = new DataTemplate {
        VisualTree = textBlockFactory,
      };

      FrameworkElementFactory comboBoxFactory = new(typeof(ComboBox));
      comboBoxFactory.SetValue(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
      comboBoxFactory.SetValue(ComboBox.ItemsSourceProperty, values);
      comboBoxFactory.SetBinding(ComboBox.TextProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.TwoWay,
          UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
      gridColumn.CellEditingTemplate = new DataTemplate {
        VisualTree = comboBoxFactory,
      };

      return gridColumn;
    }

    private static DataGridTemplateColumn CreateDataGridCheckBoxColumn(CheckBoxColumn checkBoxColumn) {
      bool readOnly = !checkBoxColumn.Table?.Editable ?? true;

      string bindingPath = Registration.GetBindingPath(checkBoxColumn);

      DataGridTemplateColumn gridColumn = new() {
        Header = checkBoxColumn.Label,
        CanUserSort = true,
        SortMemberPath = bindingPath,
      };

      FrameworkElementFactory checkBoxFactory = new(typeof(CheckBox));
      checkBoxFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
      checkBoxFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
      checkBoxFactory.SetBinding(CheckBox.IsCheckedProperty,
        new Binding(bindingPath) {
          UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
          Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay,
          //IsAsync = true,
        });

      if (readOnly) {
        checkBoxFactory.SetValue(CheckBox.FocusableProperty, false);
        checkBoxFactory.SetValue(CheckBox.IsHitTestVisibleProperty, false);
        checkBoxFactory.SetValue(CheckBox.BorderBrushProperty, Brushes.Transparent);
        checkBoxFactory.SetValue(CheckBox.BackgroundProperty, Brushes.Transparent);
      }

      gridColumn.CellTemplate = new DataTemplate {
        VisualTree = checkBoxFactory,
      };

      return gridColumn;
    }

    private static DataGridTemplateColumn CreateDataGridTextColumn(Column column, string stringFormat = "") {
      bool readOnly = !column.Table?.Editable ?? true;

      string bindingPath = Registration.GetBindingPath(column);

      DataGridTemplateColumn gridColumn = new() {
        Header = column.Label,
        CanUserSort = true,
        SortMemberPath = bindingPath,
      };

      FrameworkElementFactory textBlockFactory = new(typeof(TextBlock));
      textBlockFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
      textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
      textBlockFactory.SetBinding(TextBlock.TextProperty,
        new Binding(bindingPath) {
          Mode = BindingMode.OneWay,
          StringFormat = stringFormat,
        });
      gridColumn.CellTemplate = new DataTemplate {
        VisualTree = textBlockFactory,
      };

      if (!readOnly) {
        FrameworkElementFactory textBoxFactory = new(typeof(TextBox));
        textBoxFactory.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        textBoxFactory.SetBinding(TextBox.TextProperty,
          new Binding(bindingPath) {
            Mode = BindingMode.TwoWay,
            StringFormat = stringFormat,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
          });
        gridColumn.CellEditingTemplate = new DataTemplate {
          VisualTree = textBoxFactory,
        };
      }

      return gridColumn;
    }

  }
}
