using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Controls;
using IsikReg.Excel;
using IsikReg.Extensions;
using IsikReg.Model;
using IsikReg.Properties;
using IsikReg.SmartCards;
using IsikReg.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace IsikReg {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {

    private bool closing;

    private readonly PersonDictionary personList;

    private readonly ReadersMonitor readersMonitor;
    private readonly MenuItem cardTerminalsMenu;

    private readonly Property<bool> loadingProperty = new(false);

    private IReadOnlyDictionary<string, object>? currentCardNotRegisteredRecords;

    private readonly PersonExcelWriter personExcelWriter = new();

    public MainWindow() {
      InitializeComponent();
      new WindowInteropHelper(this).EnsureHandle();
      App.Log("Application started!");

      if (!IOUtils.HasWritePermissions()) {
        App.ShowExceptionDialog("Kirjutusviga", "Programmi kaustas puudub kirjutusõigus!");
        App.Quit();
      }

      personList = PersonDictionary.Instance;

      cardTerminalsMenu = new() { Header = "Terminali aknad" };

      readersMonitor = new ReadersMonitor();
      readersMonitor.Pause();

      MainCardStatusGrid.BindStatusText(readersMonitor.StatusText);

      Closing += OnClosing;

      DataGridFilterTextBox.TextChanged += (sender, e) => {
        string rawText = DataGridFilterTextBox.Text;
        string text = rawText.Trim().ToLower();
        RegistrationDataGrid.Filter(text);
      };

      if (Config.Instance.General.TableContextMenu) {
        ContextMenu tableContextMenu = InitTableContextMenu();
        RegistrationDataGrid.SelectionChanged += (s, e) => {
          if (e.Source is DataGrid g) {
            g.ContextMenu = g.SelectedItems.Count > 0 ? tableContextMenu : null;
          }
        };
      }

      InitMenuBar();

      AddRegistrationButton.Click += (s, e) => {
        ShowNewRegistrationForm();
      };

      InitQuickRegistrationButtons();

      InitCardReader();

      personList.Read(() => StartLoading(), () => StopLoading());
    }

    private void InitQuickRegistrationButtons() {
      if (Config.Instance.General.QuickRegistrationButtons != null) {
        QuickRegistrationActionsContainer.Visibility = Visibility.Visible;
        QuickRegistrationActionsContainerBorder.Visibility = Visibility.Visible;
        Button delReg = new() {
          Content = "Tühista",
          VerticalAlignment = VerticalAlignment.Center,
          HorizontalAlignment = HorizontalAlignment.Center,
          IsEnabled = false,
          Style = (Style)FindResource("DangerButtonStyle"),
        };
        delReg.Click += (s, e) => DeleteSelectedRegistrations();
        QuickRegistrationButtonsPanel.Children.Add(delReg);

        Registration? currentReg = null;
        RegistrationDataGrid.SelectionChanged += (s, e) => {
          void currentRegListener(string key, object? oldValue, object? newValue, DictionaryAction action) {
            if (Config.Instance.Columns.GetValueOrDefault(key)?.Id != ColumnId.REGISTERED) return;
            delReg.IsEnabled = currentReg != null && (!currentReg.IsOnlyRegistration() || !currentReg.IsReset());
          }

          if (currentReg != null) {
            // Clear listener after selection changes
            currentReg.Properties.PropertyChanged -= currentRegListener;
            currentReg = null;
          }
          if (RegistrationDataGrid.SelectedItems.Count == 1 && RegistrationDataGrid.SelectedItem is Registration r) {
            delReg.IsEnabled = !r.IsOnlyRegistration() || !r.IsReset();
            currentReg = r;

            // Listen for any registered changes no matter what
            currentReg.Properties.PropertyChanged += currentRegListener;
          } else {
            delReg.IsEnabled = false;
          }
        };

        Config.Instance.GetRegistrationTypes().ForEach(type => {
          Button regBtn = new() {
            Content = "Registreeri " + type.ToLower(),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsEnabled = false,
          };
          regBtn.Click += (s, e) => SelectedPersonNewRegistration(type);
          QuickRegistrationButtonsPanel.Children.Add(regBtn);

          // Enable | Disable Register buttons
          CancellationTokenSource? graceCancel = null;
          Person? currentPerson = null;

          void updateRegisterButton(Registration? registration) {
            if (graceCancel != null) {
              graceCancel.Cancel();
              graceCancel = null;
            }
            bool disable = false;
            if (Config.Instance.General.RegisterSameTypeInRow == Config.Rule.DENY) {
              disable = registration != null && registration.RegistrationType.Equals(type);
            }
            if (!disable && Config.Instance.General.RegisterDuringGracePeriod == Config.Rule.DENY && registration != null) {
              double? remainingGracePeriodMillis = registration.RemainingGracePeriodMillis();
              if (remainingGracePeriodMillis != null && remainingGracePeriodMillis > 0) {
                disable = true;
                CancellationTokenSource cancel = new();
                graceCancel = cancel;
                Task.Delay((int)remainingGracePeriodMillis, cancel.Token).ContinueWith(delayTask => {
                  App.Run(() => {
                    cancel.Dispose();

                    if (!delayTask.IsCanceled && cancel == graceCancel) {
                      regBtn.IsEnabled = true;
                    }

                    if (cancel == graceCancel) {
                      graceCancel = null;
                    }
                  });

                }).OnExceptionQuit();
              }
            }
            regBtn.IsEnabled = !disable;
          };

          void registrationChangedListener(Registration? oldReg, Registration? newReg) => updateRegisterButton(newReg);

          RegistrationDataGrid.SelectionChanged += (s, e) => {
            if (graceCancel != null) {
              graceCancel.Cancel();
              graceCancel = null;
            }
            if (currentPerson != null) {
              currentPerson.LatestRegisteredRegistrationProperty.PropertyChanged -= registrationChangedListener;
              currentPerson = null;
            }
            if (RegistrationDataGrid.SelectedItems.Count == 1 && RegistrationDataGrid.SelectedItem is Registration registration) {
              currentPerson = registration.Person;
              updateRegisterButton(currentPerson.LatestRegisteredRegistration);
              currentPerson.LatestRegisteredRegistrationProperty.PropertyChanged += registrationChangedListener;
            } else {
              regBtn.IsEnabled = false;
            }
          };


        });

        QuickRegistrationButtonsPanel.Children.SetGap(5);

        if (Config.Instance.General.QuickRegistrationButtons.ShowSelectedPerson) {
          string noneSelectedText = "-";
          QuickRegistrationLabel.Visibility = Visibility.Visible;
          QuickRegistrationLabel.Text = noneSelectedText;
          Registration? currentSelectedReg = null;
          bool updating = false;
          void onPropertyChanged(string key, object? oldValue, object? newValue, DictionaryAction action) {
            if (updating) return;
            updating = true;
            App.RunAsync(() => {
              if (currentSelectedReg != null) {
                QuickRegistrationLabel.Text = currentSelectedReg.Person.GetDisplayInfo();
              }
              updating = false;
            });
          }
          RegistrationDataGrid.SelectionChanged += (s, e) => {
            if (currentSelectedReg != null) {
              currentSelectedReg.Properties.PropertyChanged -= onPropertyChanged;
              currentSelectedReg.Person.Properties.PropertyChanged -= onPropertyChanged;
            }
            if (RegistrationDataGrid.SelectedItems.Count == 1 && RegistrationDataGrid.SelectedItem is Registration r) {
              currentSelectedReg = r;
              currentSelectedReg.Properties.PropertyChanged += onPropertyChanged;
              currentSelectedReg.Person.Properties.PropertyChanged += onPropertyChanged;
              QuickRegistrationLabel.Text = r.Person.GetDisplayInfo();
            } else {
              QuickRegistrationLabel.Text = noneSelectedText;
            }
          };
        }
      }
    }

    private void InitNewCardTerminalMenuItem(string name, CardStatusGrid cardStatusGrid) {
      MenuItem menuItem = new() {
        Header = name,
        IsCheckable = true
      };

      Window window = new() {
        //Owner = this,
        Title = name,
        Content = cardStatusGrid,
        Width = Width,
        Height = Height,
        Visibility = Visibility.Collapsed,
        Background = new SolidColorBrush((Color)FindResource("WindowBackgroundColor")),
      };

      //otherStages.add(stage); // TODO add it?

      window.StateChanged += (s, e) => {
        switch (window.WindowState) {
          case WindowState.Maximized:
            window.WindowStyle = WindowStyle.None; // Fullscreen
            break;
          case WindowState.Normal:
            window.WindowStyle = WindowStyle.ThreeDBorderWindow;
            break;
          case WindowState.Minimized:
            window.WindowState = WindowState.Normal;
            menuItem.IsChecked = false;
            break;
        }
      };

      window.PreviewKeyDown += (s, e) => {
        if (e.Key == Key.Escape) {
          window.WindowState = WindowState.Normal;
        }
      };

      window.Closing += (s, e) => {
        e.Cancel = true;
        menuItem.IsChecked = false;
      };

      menuItem.Checked += (s, e) => {
        window.Visibility = Visibility.Visible;
      };
      menuItem.Unchecked += (s, e) => {
        window.Visibility = Visibility.Collapsed;
      };

      cardTerminalsMenu.Items.Add(menuItem);
    }

    private void InitCardReader() {
      readersMonitor.RecordsRead += (reader, card, records) => {
        try {
          App.Run(() => {
            CardStatusText statusText = reader.StatusText;

            readersMonitor.BindManualText(reader);

            statusText.WaitUserInput(records);

            currentCardNotRegisteredRecords = null;
            string personDisplayText = records.GetPersonDisplayInfo();

            // Check Expiry Date
            if (Config.Instance.SmartCard.RegisterExpiredCards == Config.Rule.CONFIRM ||
                Config.Instance.SmartCard.RegisterExpiredCards == Config.Rule.DENY) {
              DateTime? expireDate = records.GetExpiryDate();
              if (expireDate == null) {
                if (!App.ShowConfirmDialog(
                    "Ei suutnud lugeda millal ID-kaart aegub! Kas jätkan registreerimisega?", personDisplayText)) {
                  statusText.NotRegistered(records);
                  return;
                }
              } else {
                if (expireDate.Value.Date < DateTime.Now.Date) {
                  if (Config.Instance.SmartCard.RegisterExpiredCards == Config.Rule.DENY ||
                      (Config.Instance.SmartCard.RegisterExpiredCards == Config.Rule.CONFIRM && !App.ShowConfirmDialog(
                          "ID-kaart on aegunud! Kas jätkan registreerimisega?", personDisplayText))) {
                    statusText.Expired(records);
                    return;
                  }
                }
              }
            }

            // Find existing person
            Person? existingPerson = personList.GetValueOrDefault(records.GetPersonalCode());
            if (existingPerson != null) {
              existingPerson.Properties.Merge(records);

              if (Config.Instance.SmartCard.QuickExistingPersonRegistration) {
                Registration? latestRegisteredRegistration = existingPerson.LatestRegisteredRegistration;
                if (latestRegisteredRegistration != null) {
                  string? nextType = existingPerson.GetNextRegistrationType();
                  if (latestRegisteredRegistration.RegistrationType.Equals(nextType)) {
                    // Already same type of registration
                    statusText.AlreadyRegistered(latestRegisteredRegistration.RegistrationType,
                      latestRegisteredRegistration);
                  } else {
                    // Different new registration
                    if (existingPerson.CheckRegistrationGracePeriod(nextType)) {
                      Registration r = existingPerson.NewNextRegistration();
                      r.RegisteredDate = DateTime.Now;
                      statusText.Registered(r.RegistrationType, r);
                    } else {
                      statusText.AlreadyRegistered(latestRegisteredRegistration.RegistrationType,
                        latestRegisteredRegistration);
                    }
                  }
                } else { // not registered at all
                  Registration r = existingPerson.NewTypeRegistration();
                  r.RegisteredDate = DateTime.Now;
                  statusText.Registered(r.RegistrationType, r);
                }
              } else {
                // Show registration form
                Registration? reg = existingPerson.InsertRegistrationShowForm(existingPerson.Properties); // TODO insertregistrationshowfrom inside person..
                if (reg != null) {
                  statusText.Registered(reg.RegistrationType, reg);
                  personList.Save(reg.Person);
                } else if (existingPerson.LatestRegisteredRegistration != null) {
                  statusText.AlreadyRegistered(existingPerson.LatestRegisteredRegistration.RegistrationType,
                    existingPerson.LatestRegisteredRegistration);
                } else {
                  statusText.NotRegistered(existingPerson.Properties);
                }
              }
              FocusPersonRegistration(existingPerson);
            } else {
              // New person
              if (!Config.Instance.General.InsertPerson) {
                statusText.NotOnTheList(records);
              } else {
                if ((Config.Instance.SmartCard.RegisterPersonNotInList == Config.Rule.ALLOW || (
                    Config.Instance.SmartCard.RegisterPersonNotInList == Config.Rule.CONFIRM &&
                        App.ShowConfirmDialog("ID-kaart pole nimekirjas. Kas jätkan registreerimisega?", personDisplayText))
                )) {
                  Person? newPerson = Config.Instance.SmartCard.QuickNewPersonRegistration ?
                      AddNewPerson(records) :
                      ShowNewRegistrationForm(records);
                  if (newPerson != null) {
                    IReadOnlyDictionary<string, object> props = newPerson.LatestRegisteredRegistration != null ? newPerson.LatestRegisteredRegistration : newPerson.Properties;
                    statusText.Registered(props.GetRegistrationType(), props);
                  } else {
                    statusText.NotRegistered(records);
                    currentCardNotRegisteredRecords = records;
                  }
                } else {
                  statusText.NotOnTheList(records);
                  currentCardNotRegisteredRecords = records;
                }
              }
            }
          });
        } finally {
          DateTime startShowSuccess = DateTime.Now;

          reader.WaitForCardAbsent(card);

          if (currentCardNotRegisteredRecords != null) {
            App.RunAsync(() => {
              currentCardNotRegisteredRecords = null;
            });
          }

          lock (reader.StatusLock) {
            if (!reader.CardPresentProperty.Value) {
              int showSuccessTime = Config.Instance.SmartCard.ShowSuccessStatusDuration - (int)(DateTime.Now - startShowSuccess).TotalMilliseconds;
              if (showSuccessTime > 0) {
                CancellationTokenSource cancelShowSuccess = new();
                void NewCardInserted(ReaderMonitor newReader, int newCard) {
                  readersMonitor.CardInserted -= NewCardInserted;
                  if (reader != newReader || card != newCard) {
                    cancelShowSuccess.Cancel();
                  }
                }
                readersMonitor.CardInserted += NewCardInserted;
                Task.Delay(showSuccessTime, cancelShowSuccess.Token).ContinueWith(_ => {
                  App.Log($"Reader {reader.Name} Cleared showing message (waited {(int)(DateTime.Now - startShowSuccess).TotalMilliseconds}ms)");
                  readersMonitor.UnbindManualText(reader);
                }).OnExceptionQuit();
              } else {
                App.Log($"Reader {reader.Name} Cleared showing message (wait time already passed)");
                readersMonitor.UnbindManualText(reader);
              }
            } else {
              App.Log($"Reader {reader.Name} Cleared showing message (new card present)");
              readersMonitor.UnbindManualText(reader);
            }
          }
        }
      };

      readersMonitor.NewReaderMonitor += (reader) => {
        App.RunAsync(() => {
          CardStatusGrid cardStatusGrid = new();
          cardStatusGrid.CardText.FontSize = Config.Instance.SmartCard.ExternalTerminalFontSize; // TODO dynamic size?
          cardStatusGrid.BindStatusText(reader.StatusText);

          InitNewCardTerminalMenuItem(reader.Name, cardStatusGrid);
        });
      };

      readersMonitor.Start();
    }

    private void InitMenuBar() {
      MenuBar.IsMainMenu = true;

      MenuItem fileMenu = new() { Header = "Fail" };
      MenuBar.Items.Add(fileMenu);

      // Import
      MenuItem importExcel = new() { Header = "Import" };
      importExcel.Click += (s, e) => ShowImportExcelDialog();
      fileMenu.Items.Add(importExcel);

      // Eksport
      MenuItem exportExcel = new() { Header = "Eksport" };
      exportExcel.Click += (s, e) => ShowExportExcelDialog(false);
      fileMenu.Items.Add(exportExcel);

      // Eksport Group by Registration Type
      MenuItem exportExcelGroupByRegistrationType = new() { Header = "Eksport (Grupeeri registreerimise tüüp)" };
      exportExcelGroupByRegistrationType.Click += (s, e) => ShowExportExcelDialog(true);
      fileMenu.Items.Add(exportExcelGroupByRegistrationType);


      MenuItem personMenu = new() { Header = "Nimekiri" };
      MenuBar.Items.Add(personMenu);

      // Registreerimine
      MenuItem newRegistration = new() { Header = "Uus registreerimine" };
      newRegistration.Click += (s, e) => ShowNewRegistrationForm();
      personMenu.Items.Add(newRegistration);

      MenuItem clearAllRegistrations = new() { Header = "Tühista kõik registreerimised" };
      clearAllRegistrations.Click += (s, e) => DeleteAllRegistrationsConfirm(() => {
        RegistrationDataGrid.UnselectAll();
      });
      personMenu.Items.Add(clearAllRegistrations);

      if (Config.Instance.General.DeletePerson) {
        // Nimekiri
        MenuItem clearPersonList = new() { Header = "Kustuta kõik isikud ja registreerimised" };
        clearPersonList.Click += (s, e) => DeletePeopleAndRegistrationsConfirm(() => {
          RegistrationDataGrid.UnselectAll();
        });
        personMenu.Items.Add(clearPersonList);
      }


      // Valitud read
      MenuItem selectedRowsMenu = new() { Header = "Valitud read" };
      MenuBar.Items.Add(selectedRowsMenu);
      CreateRegistrationMenuItems().ForEach(menuItem => selectedRowsMenu.Items.Add(menuItem));

      selectedRowsMenu.IsEnabled = false;
      RegistrationDataGrid.SelectionChanged += (s, e) => {
        selectedRowsMenu.IsEnabled = RegistrationDataGrid.SelectedItems.Count > 0;
      };


      CardStatusGrid cardStatusGrid = new();
      cardStatusGrid.CardText.FontSize = Config.Instance.SmartCard.ExternalTerminalFontSize; // TODO dynamic size?
      cardStatusGrid.BindStatusText(readersMonitor.StatusText);

      InitNewCardTerminalMenuItem("Kõik terminalid", cardStatusGrid);
      MenuBar.Items.Add(cardTerminalsMenu);
    }

    private ContextMenu InitTableContextMenu() {
      ContextMenu contextMenu = new();

      CreateRegistrationMenuItems().ForEach(menuItem => contextMenu.Items.Add(menuItem));

      return contextMenu;
    }

    private List<MenuItem> CreateRegistrationMenuItems() {
      List<MenuItem> items = new();

      MenuItem newRegistrationType = new() { Header = "Uut tüüpi registreerimine" };
      newRegistrationType.Click += (s, e) => SelectedPersonNewRegistration();
      items.Add(newRegistrationType);

      List<string> labelList = new();
      if (Config.Instance.General.UpdatePerson) {
        labelList.Add("isikut");
      }
      labelList.Add("registreeringut");

      MenuItem updatePerson = new() { Header = "Muuda " + string.Join("/", labelList) };
      updatePerson.Click += (s, e) => UpdateSelectedPersonOrRegistration();
      items.Add(updatePerson);

      MenuItem removeSelectedRegistrations = new() { Header = "Tühista registreerimised" };
      removeSelectedRegistrations.Click += (s, e) => DeleteSelectedRegistrations();
      items.Add(removeSelectedRegistrations);

      if (Config.Instance.General.DeletePerson) {
        MenuItem deleteSelectedPeople = new() { Header = "Kustuta isikud" };
        deleteSelectedPeople.Click += (s, e) => DeleteSelectedPeople();
        items.Add(deleteSelectedPeople);
      }

      return items;
    }

    public void OnClosing(object? sender, CancelEventArgs e) {
      if (closing) {
        if (App.ShowConfirmDialog("Programm sulgub hetkel ohutult.\nOled kindel, et tahad programmi sunniviisiliselt sulgeda?", "")) {
          App.Quit();
        } else {
          e.Cancel = true;
        }
        return;
      }

      e.Cancel = true;
      closing = true;
      StartLoading();

      Task.Run(() => {
        readersMonitor.Pause();
        readersMonitor.Dispose();

        personList.WaitSaved();

        personExcelWriter.WaitWritingCompleted();

        App.RunAsync(() => {
          App.Quit();
        });
      }).OnExceptionQuit();
    }


    #region HELPERS

    private void SelectedPersonNewRegistration(string? registrationType = null) {
      if (RegistrationDataGrid.SelectedItem is Registration reg) {
        Registration? newReg;
        if (registrationType != null) {
          newReg = reg.Person.InsertRegistrationConfirm(registrationType);
        } else {
          newReg = reg.Person.InsertRegistrationShowForm(reg);
        }
        if (newReg != null) {
          FocusRegistration(newReg);
          personList.Save(newReg.Person);
        }
      }
    }

    private void UpdateSelectedPersonOrRegistration() {
      if (RegistrationDataGrid.SelectedItem is not Registration reg)
        return;

      if (reg.UpdatePersonOrRegistrationShowForm()) {
        personList.Save(reg.Person);
      }
    }

    private void DeleteSelectedRegistrations() {
      List<Registration> regList = new(RegistrationDataGrid.SelectedItems.Count);
      foreach (Registration r in RegistrationDataGrid.SelectedItems) {
        regList.Add(r);
      }

      if (regList.Count == 0)
        return;

      Person? p = null;
      if (regList.Count == 1) {
        p = regList[0].Person;
      }

      if (DeleteRegistrationsConfirm(regList, () => {
        // Clear selection before delete so that clipboard isn't updated after every delete
        RegistrationDataGrid.UnselectAll();
      })) {
        if (p != null) {
          FocusPersonRegistration(p);
        }
      }
    }

    private void DeleteSelectedPeople() {
      Dictionary<string, Person> people = new();
      foreach (Registration r in RegistrationDataGrid.SelectedItems) {
        people[r.Person.PersonalCode] = r.Person;
      }

      if (people.Count == 0) return;

      DeletePeopleConfirm(people.Values, () => {
        // Clear selection before delete so that clipboard isn't updated after every delete
        RegistrationDataGrid.UnselectAll();
      });
    }

    public bool DeletePeopleConfirm(IEnumerable<Person> people, Action beforeDelete) {
      if (!Config.Instance.General.DeletePerson)
        return false;

      if (!people.Any()) return true;

      string labelText = people.Skip(1).Any() ?
          "Oled kindel, et tahad valitud isikud kustutada?" :
          "Oled kindel, et tahad valitud isiku kustutada?";

      Grid gridPane = new() {
        Margin = new Thickness(5),
        ColumnDefinitions = {
          new(),new(){Width= new GridLength(5)},
          new(),new(){Width= new GridLength(5)},
          new(),
        },
      };

      void addCol(object? obj, int col) {
        string? text = obj?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;
        TextBlock textBlock = new() { Text = text };
        Grid.SetRow(textBlock, gridPane.RowDefinitions.Count);
        Grid.SetColumn(textBlock, col);
        gridPane.Children.Add(textBlock);
      };

      foreach (Person p in people) {
        addCol(p.PersonalCode, 0);
        addCol(p.LastName, 2);
        addCol(p.FirstName, 4);

        gridPane.RowDefinitions.Add(new());
        gridPane.RowDefinitions.Add(new() { Height = new GridLength(2) });
      }
      if (gridPane.RowDefinitions.Count > 0) {
        gridPane.RowDefinitions.RemoveAt(gridPane.RowDefinitions.Count - 1);
      }

      if (App.ShowConfirmDialog(labelText, gridPane)) {
        beforeDelete.Invoke();
        foreach (Person p in people) {
          personList.Remove(p);
        }
        return true;
      }
      return false;
    }

    public bool DeleteRegistrationsConfirm(IEnumerable<Registration> registrationList, Action beforeDelete) {
      if (!registrationList.Any()) return false;

      string labelText = registrationList.Skip(1).Any() ?
          "Oled kindel, et tahad valitud registreerimisi tühistada?" :
          "Oled kindel, et tahad valitud registreerimist tühistada?";

      Grid gridPane = new() {
        Margin = new Thickness(5),
        ColumnDefinitions = {
          new(),new(){Width= new GridLength(5)},
          new(),new(){Width= new GridLength(5)},
          new(),new(){Width= new GridLength(5)},
          new(),new(){Width= new GridLength(5)},
          new(),
        },
      };

      void addCol(object? obj, int col) {
        string? text = obj?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;
        TextBlock textBlock = new() { Text = text };
        Grid.SetRow(textBlock, gridPane.RowDefinitions.Count);
        Grid.SetColumn(textBlock, col);
        gridPane.Children.Add(textBlock);
      };

      foreach (Registration r in registrationList) {
        Person p = r.Person;
        string type = r.RegistrationType;
        addCol(r.RegistrationType, 0);
        addCol(r.RegisteredDate, 2);
        addCol(p.PersonalCode, 4);
        addCol(p.LastName, 6);
        addCol(p.FirstName, 8);

        gridPane.RowDefinitions.Add(new());
        gridPane.RowDefinitions.Add(new() { Height = new GridLength(2) });
      }
      if (gridPane.RowDefinitions.Count > 0) {
        gridPane.RowDefinitions.RemoveAt(gridPane.RowDefinitions.Count - 1);
      }


      if (App.ShowConfirmDialog(labelText, gridPane)) {
        beforeDelete.Invoke();
        HashSet<string> saveSet = new();
        foreach (Registration r in registrationList) {
          Person p = r.Person;
          if (p.Registrations.Count > 1) {
            // remove only if it's not the last one
            r.Remove();
          } else if (p.Registrations.Count == 1) {
            // clear registration and set default type
            r.Reset();
          } else {
            p.CleanUpRegistrations();  // Adds new empty registration
          }
          saveSet.Add(p.PersonalCode);
        }
        foreach (string s in saveSet) {
          personList.Save(s);
        }
        return true;
      }
      return false;
    }

    public bool DeleteAllRegistrationsConfirm(Action beforeDelete) {
      if (App.ShowConfirmDialog("Oled kindel, et tahad registreerimised tühistada?", "Tühistan registreerimised?")) {
        beforeDelete.Invoke();
        foreach (Person p in personList.Values) {
          p.RemoveRegistrations();
          p.CleanUpRegistrations();
        }
        foreach (Person p in personList.Values) {
          personList.Save(p);
        }
        return true;
      }
      return false;
    }

    public bool DeletePeopleAndRegistrationsConfirm(Action beforeDelete) {
      if (!Config.Instance.General.DeletePerson)
        return false;
      if (personList.Count == 0) return true;

      if (App.ShowConfirmDialog("Oled kindel, et tahad nimekirja ära kustutada?", "Kustutan nimekirja?")) {
        beforeDelete.Invoke();
        personList.Clear();
        return true;
      }
      return false;
    }

    public bool FocusPersonRegistration(Person p) {
      if (p.LatestRegisteredRegistration != null) {
        return FocusRegistration(p.LatestRegisteredRegistration);
      } else {
        Registration? reg = p.GetLatestRegistration();
        if (reg != null) {
          return FocusRegistration(reg);
        }
      }
      return false;

    }

    public bool FocusRegistration(Registration r) {
      // Make sure registration is in table and not filtered
      if (!RegistrationDataGrid.Items.Contains(r)) {
        DataGridFilterTextBox.Text = string.Empty;
      }
      if (!RegistrationDataGrid.Items.Contains(r)) {
        throw new Exception("What");
      }

      // Select the registration
      RegistrationDataGrid.UnselectAll();
      RegistrationDataGrid.SelectedItem = r;

      // Scroll only if not in view
      RegistrationDataGrid.Focus();
      RegistrationDataGrid.UpdateLayout();
      RegistrationDataGrid.ScrollIntoView(RegistrationDataGrid.SelectedItem);

      return true;
    }

    private Person? AddNewPerson(IReadOnlyDictionary<string, object> properties) {
      Person? p = AddNewRegistration(properties);
      if (p != null) {
        FocusPersonRegistration(p);
      }
      return p;
    }

    public Person? AddNewRegistration(IReadOnlyDictionary<string, object> properties) {
      string personalCode = properties.GetPersonalCode();
      string registrationType = properties.GetRegistrationType();

      Person? person = personList.GetValueOrDefault(personalCode);
      if (person != null) {
        if (!person.CheckRegistrationAllowed(registrationType)) return null;
      } else {
        if (!Config.Instance.General.InsertPerson) {
          App.ShowWarningDialog("Uue isiku lisamine ei ole lubatud!", properties.GetPersonDisplayInfo());
          return null;
        }
      }

      if (person != null) {
        person.Properties.Merge(properties); // Update person properties if required
        Registration newRegistration = person.NewRegistration(properties);
        newRegistration.Registered = true;
      } else {
        person = new(properties);
        Registration newRegistration = person.GetLatestRegistration();
        newRegistration.Registered = true;
        person = personList.Add(person); // Will be saved permanently
        if (person != null) personList.Save(person);
      }

      return person;
    }


    private Person? ShowNewRegistrationForm(IReadOnlyDictionary<string, object>? defaultValues = null) {
      RegistrationFormDialog regForm = new();

      //App.RunAsync(() => {
      // Set first available registration type
      Property<string?>? registrationTypeProperty = regForm.RegistrationTypeProperty();
      if (registrationTypeProperty != null) {
        registrationTypeProperty.Value = Config.Instance.GetDefaultRegistrationType();
      }
      //});

      // If card is present but person was not on the list then remember values when opening form dialog
      if (currentCardNotRegisteredRecords != null) {
        regForm.SetValues(currentCardNotRegisteredRecords);
        var props = regForm.ShowAndWait();
        if (props == null) {
          readersMonitor.StatusText.NotRegistered(currentCardNotRegisteredRecords);
          return null;
        }
        Person? newPerson = AddNewPerson(props);
        if (newPerson != null) {
          readersMonitor.StatusText.Registered(newPerson.LatestRegisteredRegistration?.RegistrationType ?? "", newPerson.Properties);
        } else {
          readersMonitor.StatusText.NotRegistered(currentCardNotRegisteredRecords);
        }
        return newPerson;
      } else {
        if (defaultValues != null) {
          regForm.SetValues(defaultValues);
        }

        var props = regForm.ShowAndWait();
        return props != null ? AddNewPerson(props) : null;
      }
    }

    private void StartLoading() {
      lock (loadingProperty) {
        if (loadingProperty.Value) return;
        loadingProperty.Value = true;
      }

      readersMonitor.Pause();
      App.Run(() => {
        MainGrid.IsEnabled = false;
        MainGrid.Opacity = .5;
        MainGrid.IsHitTestVisible = false;

        LoadingProgressBar.Visibility = Visibility.Visible;
        LoadingProgressBar.IsIndeterminate = true;

        personList.Paused = true;
        App.Log("Loading...");
      });
    }

    private void LoadingProgress(double progress) {
      if (!loadingProperty.Value) return;
      if (progress < 0) {
        App.RunAsync(() => {
          LoadingProgressBar.IsIndeterminate = true;
        });
      } else {
        App.RunAsync(() => {
          LoadingProgressBar.IsIndeterminate = false;
          LoadingProgressBar.Value = progress;
          // 0 to 100
        });
      }
    }

    private void StopLoading() {
      if (closing) {
        // Loading can't stop once App is being closed
        LoadingProgressBar.IsIndeterminate = true;
        return;
      }
      lock (loadingProperty) {
        if (!loadingProperty.Value) return;
      }

      App.Run(() => {
        personList.Paused = false;

        MainGrid.IsEnabled = true;
        MainGrid.Opacity = 1;
        MainGrid.IsHitTestVisible = true;

        LoadingProgressBar.Visibility = Visibility.Hidden;
        App.Log("Loading Done");
      });

      readersMonitor.Resume();
      lock (loadingProperty) {
        loadingProperty.Value = false;
      }
    }

    private void ShowImportExcelDialog() {
      if (closing) return;

      OpenFileDialog dialog = new() {
        FileName = "isikreg", // TODO add it to settings
        DefaultExt = ".xlsx",
        Filter = "Excel Workbook (*.xlsx)|*.xlsx",
        InitialDirectory = Path.GetFullPath("./"),
        Title = "Exceli faili importimine",
        Multiselect = true,
      };

      if (dialog.ShowDialog() == true) {
        PersonExcelReader.Read(dialog.FileNames, p => {
          Person? p2 = personList.Add(p);
          personList.Save(p.GetPersonalCode());
        }, StartLoading, StopLoading);
      }
    }

    private void ShowExportExcelDialog(bool groupByRegistrationType) {
      if (closing) return;

      SaveFileDialog dialog = new() {
        FileName = "isikreg", // TODO add it to settings
        DefaultExt = ".xlsx",
        Filter = "Excel Workbook (*.xlsx)|*.xlsx",
        InitialDirectory = Path.GetFullPath("./"),
        Title = "Exceli failiks eksportimine" + (groupByRegistrationType ? " (Grupeeri registreerimise tüübi järgi)" : ""),
      };

      if (dialog.ShowDialog() == true) {
        personExcelWriter.Write(dialog.FileName, personList.Values, groupByRegistrationType, StartLoading, StopLoading);
      }
    }

    #endregion

  }


}


