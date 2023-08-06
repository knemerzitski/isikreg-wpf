using IsikReg.Configuration;
using IsikReg.Extensions;
using IsikReg.Properties;
using IsikReg.SmartCards.Records;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;
using PCSC.Monitoring;
using PCSC.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IsikReg.SmartCards {


  public delegate void NewReaderMonitorListener(ReaderMonitor readerMonitor);

  public delegate void ReaderCardInsertedListener(ReaderMonitor reader, int card);

  public delegate void ReaderRecordsReadListener(ReaderMonitor reader, int card, IReadOnlyDictionary<string, object> records);

  public class ReadersMonitor : IDisposable {

    public event NewReaderMonitorListener? NewReaderMonitor;

    public event ReaderCardInsertedListener? CardInserted;

    public event ReaderRecordsReadListener? RecordsRead;


    private readonly List<ReaderMonitor> readerMonitors = new();

    private readonly IDeviceMonitor deviceMonitor;

    private readonly SemaphoreSlim recordsReadSemaphore = new(1, 1);

    private readonly Property<State> statusProperty = new(State.WAITING_CARD_READER);
    public IReadOnlyProperty<State> StatusProperty { get => statusProperty; }

    private readonly Property<bool> cardPresentProperty = new();
    public IReadOnlyProperty<bool> CardPresentProperty { get => cardPresentProperty; }

    public CardStatusText StatusText { get; } = new(Config.Instance.SmartCard.StatusFormat);

    private readonly CountdownEvent pauseLock = new(1);

    public ReadersMonitor() {
      pauseLock.Signal();

      StatusText.StatusProperty.Bind(statusProperty);
      StatusText.CardPresentProperty.Bind(cardPresentProperty);

      deviceMonitor = DeviceMonitorFactory.Instance.Create(SCardScope.System);
      deviceMonitor.Initialized += OnInitialized;
      deviceMonitor.StatusChanged += OnStatusChanged;
      deviceMonitor.MonitorException += OnDeviceMonitorException;
    }

    public void BindManualText(ReaderMonitor reader) {
      reader.StatusText.StatusProperty.Unbind(); // Prevent text changes from monitor status changed, setting text manually
      StatusText.Bind(reader.StatusText); // Main text focus will be current reader
    }

    public void UnbindManualText(ReaderMonitor reader) {
      reader.StatusText.StatusProperty.Bind(reader.StatusProperty); // Bind back to reader status, no longer manually setting text
      StatusText.Unbind(); // Unbind from reader
      StatusText.CardPresentProperty.Bind(cardPresentProperty); // Rebind main status
      StatusText.StatusProperty.Bind(statusProperty); // Rebind main status
    }

    public void Start() {
      deviceMonitor.Start();
    }

    public void Pause() {
      lock (pauseLock) {
        if (pauseLock.CurrentCount == 0) {
          pauseLock.Reset();
        }
      }
    }

    public void Resume() {
      lock (pauseLock) {
        if (pauseLock.CurrentCount == 1) {
          pauseLock.Signal();
        }
      }
    }

    private void PausedWait() {
      if (pauseLock.CurrentCount == 1) {
        Log("ReadersMonitor Paused");
        pauseLock.Wait();
      }
    }

    private void AddReader(string name) {
      ReaderMonitor? readerMonitor = null;
      lock (readerMonitors) {
        readerMonitor = readerMonitors.Where(r => r.Name.Equals(name)).FirstOrDefault();
      }
      if (readerMonitor == null) {
        readerMonitor = new(name);
        Log($"Created new reader monitor for {name}");
        lock (readerMonitors) {
          readerMonitors.Add(readerMonitor);
        }
        NewReaderMonitor?.Invoke(readerMonitor);

        readerMonitor.StatusProperty.PropertyChanged += (o, status) => {
          lock (readerMonitors) {
            if (readerMonitors.Count == 1) {
              statusProperty.Value = readerMonitors[0].StatusProperty.Value;
            } else {
              try {
                readerMonitors.ForEach(r => Monitor.Enter(r.StatusLock));

                List<ReaderMonitor> withCard = readerMonitors.Where(r => r.CardPresentProperty.Value).ToList();
                if (withCard.Count == 1) {
                  statusProperty.Value = withCard[0].StatusProperty.Value;
                } else {
                  if (readerMonitors.Any(r => r.StatusProperty.Value == State.READING_SUCCESS)) {
                    statusProperty.Value = State.READING_SUCCESS;
                  } else if (readerMonitors.Any(r => r.StatusProperty.Value == State.READING_CARD)) {
                    statusProperty.Value = State.READING_CARD;
                  } else if (readerMonitors.Any(r => r.StatusProperty.Value == State.WAITING_CARD)) {
                    statusProperty.Value = State.WAITING_CARD;
                  } else if (readerMonitors.All(r => r.StatusProperty.Value == State.WAITING_CARD_READER)) {
                    statusProperty.Value = State.WAITING_CARD_READER;
                  } else if (readerMonitors.Count > 0) {
                    statusProperty.Value = readerMonitors[0].StatusProperty.Value;
                  } else {
                    statusProperty.Value = State.WAITING_CARD_READER;
                  }
                }
              } finally {
                readerMonitors.ForEach(r => Monitor.Exit(r.StatusLock));
              }
            }
          }
        };

        readerMonitor.CardPresentProperty.PropertyChanged += (o, present) => {
          lock (readerMonitors) {
            if (present) {
              cardPresentProperty.Value = true;
            } else {
              try {
                readerMonitors.ForEach(r => Monitor.Enter(r.StatusLock));
                cardPresentProperty.Value = readerMonitors.Any(r => r.CardPresentProperty.Value);
              } finally {
                readerMonitors.ForEach(r => Monitor.Exit(r.StatusLock));
              }
            }
          }
        };

        readerMonitor.RecordsRead += (card, records) => {
          try {
            recordsReadSemaphore.Wait();

            PausedWait();
            if (!readerMonitor.IsCardAbsent(card)) {
              RecordsRead?.Invoke(readerMonitor, card, records);
            }

          } finally {
            recordsReadSemaphore.Release();
          }
        };

        readerMonitor.CardInserted += (card) => {
          CardInserted?.Invoke(readerMonitor, card);
        };

      } else {
        App.Log($"Started existing reader monitor {name}");
      }

      readerMonitor.Start();
    }

    private void OnInitialized(object sender, DeviceChangeEventArgs e) {
      foreach (string name in e.AllReaders) {
        AddReader(name);
      }
    }

    private void OnStatusChanged(object sender, DeviceChangeEventArgs e) {
      foreach (string name in e.AttachedReaders) {
        AddReader(name);
      }
    }

    private void OnDeviceMonitorException(object sender, DeviceMonitorExceptionEventArgs args) {
      bool quit = true;
      if (args.Exception is NoServiceException) {
        // Don't quit if there are no smartcard reader drivers
        App.Log(args.Exception.GetType().Name, args.Exception.Message);
        quit = false;
      }
      App.ShowException(args.Exception, quit);
    }

    public void Dispose() {
      deviceMonitor.Cancel();
      deviceMonitor.Dispose();

      lock (readerMonitors) {
        foreach (ReaderMonitor monitor in readerMonitors) {
          monitor.Dispose();
        }
        readerMonitors.Clear();
      }

      recordsReadSemaphore.Dispose();
      pauseLock.Dispose();

      GC.SuppressFinalize(this);
    }

    private static void Log(string message) {
      App.Log($"{Thread.CurrentThread.Name}:", message);
    }
  }

}
