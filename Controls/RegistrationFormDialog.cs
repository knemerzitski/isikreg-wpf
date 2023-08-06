using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Control;
using IsikReg.Converters;
using IsikReg.Extensions;
using IsikReg.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IsikReg.Controls {

  //Dialog dialog = new(Current.MainWindow) {
  //  Title = "Kinnitus",
  //  Type = Dialog.DialogType.Confirmation,
  //  HeaderText = header,
  //  ContentText = content,
  //  Buttons = {
  //    ("JAH", true),
  //    ("EI", false)
  //  }
  //};
  //return dialog.ShowAndWait();

  public class RegistrationFormDialog : Dialog {

    private static UIElement? FindFocusNode(Panel parent) {
      foreach (object child in parent.Children) {
        if (child is UIElement element) {
          if (element is CheckBox || element is RadioButton || element is TextBox || element is DatePicker || element is ComboBox) {
            return element;
          }
        }
      }
      return null;
    }

    private readonly Dictionary<string, IProperty> formProperties;
    private readonly Dictionary<Column, UIElement> formNodes = new();

    public RegistrationFormDialog(string title = "Uus registreerimine", string saveLabel = "Registreeri", bool editing = false) : base(App.Window()) {
      Dictionary<string, IProperty> createFormProperties = new();
      Title = title;

      Button addButton = AddButton(saveLabel, true);
      addButton.IsEnabled = false;
      AddButton("Tühista", false);

      Grid grid = new() {
      };
      grid.SetValue(Grid.MarginProperty, new Thickness(0, 5, 130, 0));
      grid.ColumnDefinitions.Add(new() {
        Width = GridLength.Auto,
      });
      grid.ColumnDefinitions.Add(new() {
        Width = new GridLength(10),
      });
      grid.ColumnDefinitions.Add(new());
      int rowIndex = 0;

      ValueChangedEventHandler formValidatorListener = CreateFormValidationListener(addButton);

      RowDefinition? horizontalGapRowDef = null;
      foreach (Column column in Config.Instance.Columns) {
        if (!column.HasForm()) continue;

        Label label = new() {
          Content = column.Label.Replace("\n", " ") + (column.Required ? " *" : ""),
          VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(label);
        Grid.SetColumn(label, 0);
        Grid.SetRow(label, rowIndex);

        IProperty? property = null;

        if (column is TextColumn textColumn) {
          TextBox textBox = new() {
            Text = !string.IsNullOrWhiteSpace(textColumn.Form!.Initial) ? textColumn.Form.Initial : string.Empty,
            IsEnabled = textColumn.Form.Editable,
            //Placeholder = textColumn.Label.Replace("\n", " "),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 197,
            Padding = new Thickness(3, 2, 3, 2),
          };
          formNodes[textColumn] = textBox;
          grid.Children.Add(textBox);
          Grid.SetColumn(textBox, 2);
          Grid.SetRow(textBox, rowIndex++);
          Property<string?> stringProperty = new(textBox.Text);
          textBox.SetBinding(TextBox.TextProperty, new Binding("Value") {
            Source = stringProperty,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
          });
          property = stringProperty;
        } else if (column is DateColumn dateColumn) {
          DatePicker datePicker = new() {
            IsEnabled = dateColumn.Form!.Editable,
            //Placeholder = dateColumn.Label.Replace("\n", " "),
            Language = dateColumn.DateFormatLanguage,
          };
          // TODO datepicker customize?
          formNodes[dateColumn] = datePicker;
          grid.Children.Add(datePicker);
          Grid.SetColumn(datePicker, 2);
          Grid.SetRow(datePicker, rowIndex++);
          Property<DateTime?> dateProperty = new();
          datePicker.SetBinding(DatePicker.SelectedDateProperty, new Binding("Value") {
            Source = dateProperty,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
          });
          property = dateProperty;
        } else if (column is CheckBoxColumn checkBoxColumn) {
          CheckBox checkBox = new() {
            IsChecked = checkBoxColumn.Form!.Initial,
            VerticalAlignment = VerticalAlignment.Center,
          };
          formNodes[checkBoxColumn] = checkBox;
          grid.Children.Add(checkBox);
          Grid.SetColumn(checkBox, 2);
          Grid.SetRow(checkBox, rowIndex++);
          Property<bool> boolProperty = new(checkBox.IsChecked ?? false);
          checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding("Value") {
            Source = boolProperty,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
          });
          property = boolProperty;
        } else if (column is ComboBoxColumn comboBoxColumn) {
          // TODO review this code!
          List<string> constantValues = comboBoxColumn.GetOptionValues();
          if (constantValues.Count > 0
            || comboBoxColumn.Form!.Autofill != null
            || comboBoxColumn.Form!.Editable) {
            ComboBox comboBox = new() {
              IsEditable = comboBoxColumn.Form!.Editable,
              // TODO create placeholder?
              // comboBox.setPromptText("... " + (!comboBoxColumn.form.editable ? "Vali " + column.label.toLowerCase() : column.label) + " ...");
            };
            constantValues.ForEach(v => comboBox.Items.Add(v));
            formNodes[comboBoxColumn] = comboBox;
            UIElement addNode = comboBox;

            Property<string> stringProperty = new(comboBoxColumn.Form.Initial);
            Binding stringPropertyBinding = new("Value") {
              Source = stringProperty,
              Mode = BindingMode.TwoWay,
              UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
            comboBox.SetBinding(ComboBox.TextProperty, stringPropertyBinding);

            if (comboBoxColumn.Form.autofillPattern != null) {
              comboBoxColumn.Form.autoFillUpdateUsePrevious = !editing;

              Grid subGrid = new() {
                RowDefinitions = {
                  new(),
                  new(){Height= new GridLength(3)},
                  new()
                },
                ColumnDefinitions = {
                  new(){Width = GridLength.Auto,},
                  new(){Width= new GridLength(3)},
                  new()
                },
              };
              addNode = subGrid;
              formNodes[comboBoxColumn] = subGrid; // TODO is this fine? already set before


              CheckBox comboCheck = new() {
                VerticalAlignment = VerticalAlignment.Center,
              };
              comboBox.SetBinding(ComboBox.IsEnabledProperty, new Binding("IsChecked") {
                Source = comboCheck,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
              });
              subGrid.Children.Add(comboCheck); // 0 0

              CheckBox labelCheck = new() {
                VerticalAlignment = VerticalAlignment.Center,
              };
              Grid.SetRow(labelCheck, 2); // 1 0
              subGrid.Children.Add(labelCheck);
              comboBoxColumn.Form.autofillValues.ForEach(v => comboBox.Items.Add(v));
              Grid.SetColumn(comboBox, 2); // 0 1
              subGrid.Children.Add(comboBox);

              TextBlock labelAutoFill = new() {
                Text = string.Format(comboBoxColumn.Form.Autofill!.GetString() ?? "", comboBoxColumn.Form.autofillIndex),
              };
              subGrid.Children.Add(labelAutoFill);
              Grid.SetRow(labelAutoFill, 2);
              Grid.SetColumn(labelAutoFill, 2);

              if (editing) {
                void autoFillListener(string? o, string? n) {
                  if ((comboCheck.IsChecked ?? false) || (labelCheck.IsChecked ?? false)) {
                    stringProperty.PropertyChanged -= autoFillListener;
                    return;
                  }
                  if (!string.IsNullOrWhiteSpace(n)) {
                    comboCheck.IsChecked = true;
                    comboBox.Text = n;
                  }
                }
                stringProperty.PropertyChanged += autoFillListener;
              }

              comboCheck.Checked += (s, o) => {
                labelCheck.IsChecked = false;
                stringProperty.Value = comboBox.Text;
                comboBox.SetBinding(ComboBox.TextProperty, stringPropertyBinding);
                if (!editing) {
                  comboBoxColumn.Form.autoFillSelected = 1;
                }
              };
              comboCheck.Unchecked += (s, o) => {
                string lastText = comboBox.Text;
                comboBox.ClearValue(ComboBox.TextProperty);
                comboBox.Text = lastText;
                stringProperty.Value = null;
                if (!editing) {
                  comboBoxColumn.Form.autoFillSelected = 0;
                }
              };
              labelCheck.Checked += (s, o) => {
                comboCheck.IsChecked = false;
                string lastText = comboBox.Text;
                comboBox.ClearValue(ComboBox.TextProperty);
                comboBox.Text = lastText;
                stringProperty.Value = labelAutoFill.Text;
                if (!editing) {
                  comboBoxColumn.Form.autoFillSelected = 2;
                }
              };
              labelCheck.Unchecked += (s, o) => {
                string lastText = comboBox.Text;
                comboBox.ClearValue(ComboBox.TextProperty);
                comboBox.Text = lastText;
                stringProperty.Value = null;
                if (!editing) {
                  comboBoxColumn.Form.autoFillSelected = 0;
                }
              };

              if (comboBox.Items.Count > 0) {
                if (!editing && comboBoxColumn.Form.autoFillSelected == 1) {
                  comboCheck.IsChecked = true;
                }
                if (string.IsNullOrWhiteSpace(comboBoxColumn.Form.autoFillPrevious)) {
                  comboBox.SelectedIndex = 0;
                } else {
                  comboBox.Text = comboBoxColumn.Form.autoFillPrevious;
                }
              }
            } else if (!string.IsNullOrWhiteSpace(comboBoxColumn.Form.Initial)) {
              if (comboBoxColumn.Form.Editable || comboBox.Items.Contains(comboBoxColumn.Form.Initial)) {
                comboBox.Text = comboBoxColumn.Form.Initial;
              }
            } else if (constantValues.Count > 0) {
              comboBox.Text = constantValues[0];
            }

            // Simple autofill
            if (comboBoxColumn.Form.IsSimpleAutofill()) {
              foreach (var v in comboBoxColumn.Form.autofillValues.Reverse()) { // TODO only reverses the enumeration?
                comboBox.Items.Insert(0, v);
              }
            }

            grid.Children.Add(addNode);
            Grid.SetColumn(addNode, 2);
            Grid.SetRow(addNode, rowIndex++);

            property = stringProperty;
          }
        } else if (column is RadioColumn radioColumn) {
          List<string> optionLabels = radioColumn.GetOptionValues();
          if (optionLabels.Count != 0) {
            string initial = optionLabels.Contains(radioColumn.Form!.Initial) ?
              radioColumn.Form!.Initial : (optionLabels.Count > 0 ? optionLabels[0] : string.Empty);

            Grid radioPane = new();

            bool horizontal = radioColumn.Form!.Layout == Config.Orientation.HORIZONTAL;

            RowDefinition? rowDef = null;
            ColumnDefinition? colDef = null;
            Border? gap = null;
            List<RadioButton> radioButtons = new();
            foreach (string radioLabel in optionLabels) {
              RadioButton radioButton = new() {
                Content = radioLabel,
                GroupName = radioColumn.Key,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = radioLabel == initial,
              };
              radioButtons.Add(radioButton);
              radioPane.Children.Add(radioButton);
              if (horizontal) {
                radioPane.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

                Grid.SetColumn(radioButton, radioPane.ColumnDefinitions.Count - 1);

                // Add gap
                colDef = new() { Width = new GridLength(10) };
                radioPane.ColumnDefinitions.Add(colDef);
              } else {
                radioPane.RowDefinitions.Add(new() { Height = GridLength.Auto });

                Grid.SetRow(radioButton, radioPane.RowDefinitions.Count - 1);

                // Add gap
                rowDef = new() { Height = new GridLength(10) };
                radioPane.RowDefinitions.Add(rowDef);
              }
            }
            // Remove last gap
            if (gap != null) {
              radioPane.Children.Remove(gap);
            }
            if (rowDef != null) {
              radioPane.RowDefinitions.Remove(rowDef);
            }
            if (colDef != null) {
              radioPane.ColumnDefinitions.Remove(colDef);
            }

            formNodes[radioColumn] = radioPane;
            grid.Children.Add(radioPane);
            Grid.SetColumn(radioPane, 2);
            Grid.SetRow(radioPane, rowIndex++);

            Property<string> stringProperty = new(initial);
            foreach (RadioButton b in radioButtons) {
              b.SetBinding(RadioButton.IsCheckedProperty, new Binding("Value") {
                Source = stringProperty,
                Mode = BindingMode.TwoWay,
                Converter = new RadioGroupConverter(b),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
              });
            }
            property = stringProperty;
          }
        }
        if (property != null) {
          createFormProperties[column.Key] = property;
          property.PropertyChanged += formValidatorListener;
          grid.RowDefinitions.Add(new());

          // Add gap
          horizontalGapRowDef = new() { Height = new GridLength(10) };
          grid.RowDefinitions.Add(horizontalGapRowDef);
          rowIndex++;
        } else {
          grid.Children.Remove(label);
        }
      }

      formProperties = new(createFormProperties);

      // Remove last gap
      if (horizontalGapRowDef != null) {
        grid.RowDefinitions.Remove(horizontalGapRowDef);
      }

      Content = grid;

      ContentRendered += (s, o) => {
        UIElement? focusNode = FindFocusNode(grid);
        if (focusNode != null) {
          focusNode.Focus();
          if (focusNode is TextBox textBox) {
            textBox.CaretIndex = textBox.Text.Length;
          }
        }
      };
    }



    public new Dictionary<string, object>? ShowAndWait() {
      if (!base.ShowAndWait()) return null;

      Dictionary<string, object> result = new();
      foreach ((string key, IProperty property) in formProperties) {
        if (property.Value == null) continue;
        result[key] = property.Value;
      }
      return result;
    }

    private ValueChangedEventHandler CreateFormValidationListener(Button button) {
      return (o, n) => {
        bool formFilled = formProperties.All(entry => {
          Column? column = Config.Instance.Columns.GetValueOrDefault(entry.Key);
          if (column != null) {
            if (column.Required) {
              object? val = entry.Value.Value;
              if (val == null) // if date empty then its null
                return false;
              if (val is string strVal) {
                return !string.IsNullOrWhiteSpace(strVal);
              } else if (val is bool valBool) {
                return valBool;
              }
            }
          }
          return true;
        });
        button.SetValue(Button.IsEnabledProperty, formFilled);
      };
    }

    public void SetValues<T>(IReadOnlyDictionary<string, T> properties) {
      formProperties.SetIfExists(properties);
    }

    public void SetValues(IReadOnlyDictionary<string, IProperty> properties) {
      formProperties.SetIfExists(properties);
    }

    public void SetValue(ColumnId id, object newProp) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key != null) {
        IProperty? existingProp = formProperties.GetValueOrDefault(key);
        existingProp?.TrySet(newProp);
      }
    }

    public void SetDisable(ColumnId id, bool disable) {
      SetDisable(Config.Instance.Columns.GetValueOrDefault(id), disable);
    }

    public void SetDisable(Column? column, bool disable) {
      if (column == null) return;
      UIElement? node = formNodes.GetValueOrDefault(column);
      if (node == null)
        return;
      node.IsEnabled = !disable;
    }

    public Dictionary<Column, UIElement> GetFormNodes() {
      return formNodes;
    }

    private Property<string?>? StringProperty(ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if(key == null) return null;
      IProperty? property = formProperties.GetValueOrDefault(key);
      if (property is Property<string?> stringProeprty) {
        return stringProeprty;
      }
      return null;
    }

    public Property<string?>? RegistrationTypeProperty() {
      return StringProperty(ColumnId.REGISTRATION_TYPE);
    }

    public Property<string?>? PersonalCodeProperty() {
      return StringProperty(ColumnId.PERSONAL_CODE);
    }

  }
}
