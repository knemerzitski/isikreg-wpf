using IsikReg.Control;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using IsikReg.Ui;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Markup;
#if DEBUG
using IsikReg.Log;
#endif

namespace IsikReg {

  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {

    private static readonly Action NULL_ACTION = () => { };

    public static void Log(params object?[] messages) {
#if DEBUG
      AppLogger.Instance.Log(messages);
#endif
    }

    protected override void OnStartup(StartupEventArgs e) {
      // Register Global Exception Handling
      AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
      Dispatcher.UnhandledException += OnDispatcherUnhandledException;
      Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
      TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }
    
    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) {
      Exception exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject.ToString());
      ShowException(exception);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
      e.Handled = true;
      ShowException(e.Exception.InnerException ?? e.Exception);
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
      e.SetObserved();
      ShowException(e.Exception.GetBaseException());
    }


    public static void ShowException(Exception e, bool quit = true) {
      if(quit) {
        WriteException(e);
        Run(() => {
          ShowExceptionDialog(e.GetType().Name, e.Message);
          Quit();
        });
      } else {
        RunAsync(() => {
          ShowErrorDialog(e.GetType().Name, e.Message);
        });
      }
    }

    public static void Quit() {
      Current.Shutdown();
    }

    private static void WriteException(Exception e) {
      File.AppendAllText("./error.log", $"{DateTime.Now:s} ERROR - {e.StackTrace}\n");
    }

    public static bool ShowConfirmDialog(string header, UIElement content) {
      Dialog dialog = new(Window()) {
        Title = "Kinnitus",
        HeaderText = header,
        Content = content,
        //Buttons = {
        //  ("JAH", true),
        //  ("EI", false)
        //}
      };
      dialog.AddButton("JAH", true);
      dialog.AddButton("EI", false);
      return dialog.ShowAndWait();
    }

    public static bool ShowConfirmDialog(string header, string content) {
      Dialog dialog = new(Window()) {
        Title = "Kinnitus",
        Type = Dialog.DialogType.Confirmation,
        HeaderText = header,
        ContentText = content,
        //Buttons = {
        //  ("JAH", true),
        //  ("EI", false)
        //}
      };
      dialog.AddButton("JAH", true);
      dialog.AddButton("EI", false);
      return dialog.ShowAndWait();
    }

    public static void ShowWarningDialog(string header, string content) {
      Dialog dialog = new(Window()) {
        Title = "Hoiatus",
        Type = Dialog.DialogType.Warning,
        HeaderText = header,
        ContentText = content,
        //Buttons = {
        //  ("OK", true),
        //}
      };
      dialog.AddButton("OK", true);
      dialog.ShowAndWait();
    }

    public static void ShowErrorDialog(string header, string content) {
      Dialog dialog = new(Window()) {
        Title = "Viga",
        Type = Dialog.DialogType.Warning,
        HeaderText = header,
        ContentText = content,
        //Buttons = {
        //  ("OK", true),
        //}
      };
      dialog.AddButton("OK", true);
      dialog.ShowAndWait();
    }

    public static void ShowExceptionDialog(string header, string content) {
      Dialog dialog = new(Window()) {
        Title = "Kriitiline Viga",
        Type = Dialog.DialogType.Error,
        HeaderText = header,
        ContentText = content,
        //Buttons = {
        //  ("Sulge Programm", true),
        //}
      };
      dialog.AddButton("Sulge Programm", true);
      dialog.ShowAndWait();
    }

    public static Window Window() {
      return Current.MainWindow;
    }
    public static void Run(Action action) {
      Current.Dispatcher.Invoke(action);
    }

    public static Task RunAsync(Action action) {
      return Current.Dispatcher.BeginInvoke(action).Task;
    }

    public static async void WaitCycle() {
      await RunAsync(NULL_ACTION);
    }

    public static bool IsApplicationThread() {
      return Dispatcher.FromThread(Thread.CurrentThread) != null;
    }

  }
}
