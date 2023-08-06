using IsikReg.Configuration;
using IsikReg.Extensions;
using IsikReg.Properties;
using IsikReg.SmartCards.Records;
using IsikReg.Utils;
using PCSC;
using PCSC.Exceptions;
using PCSC.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IsikReg.SmartCards {


  public delegate void RecordsReadListener(int card, IReadOnlyDictionary<string, object> records);

  public delegate void CardInsertedListener(int card);


  public class ReaderMonitor : IDisposable {

    private static readonly CardRecordsReader[] CARD_RECORDS_READERS = new CardRecordsReader[] {
        new EstIdV2018CardRecordsReader(),
        new EstIdV2011CardRecordsReader(),
    };
    private static CardRecordsReader? FindValidRecordsReader(byte[] atr) {
      return CARD_RECORDS_READERS.Where(r => r.CanRead(atr)).FirstOrDefault();
    }

    public event CardInsertedListener? CardInserted;

    public event RecordsReadListener? RecordsRead;


    public string Name { get; }

    public readonly object StatusLock = new();
    private readonly Property<State> statusProperty = new(State.WAITING_CARD_READER);
    public IReadOnlyProperty<State> StatusProperty { get => statusProperty; }

    private readonly Property<bool> cardPresentProperty = new();
    public IReadOnlyProperty<bool> CardPresentProperty { get => cardPresentProperty; }

    public CardStatusText StatusText { get; } = new(Config.Instance.SmartCard.StatusFormat);

    private CardReader? reader;
    private readonly object readingLock = new();

    private int card = 0;

    private readonly CountdownEvent cardPresentLock = new(1);

    public ReaderMonitor(string name) {
      Name = name;

      cardPresentLock.Signal(); // Set current count to zero

      StatusText.StatusProperty.Bind(statusProperty);
      StatusText.CardPresentProperty.Bind(cardPresentProperty);

      // Derive card present from status
      statusProperty.PropertyChanged += (o, status) => {
        switch (status) {
          case State.WAITING_CARD_READER:
          case State.WAITING_CARD:
            cardPresentProperty.Value = false;
            break;
          case State.UNRESPONSIVE_CARD:
          case State.CARD_PRESENT:
            cardPresentProperty.Value = true;
            break;
        }
      };

      // Update card counter whenever new card is present
      cardPresentProperty.PropertyChanged += (o, present) => {
        if (present) {
          card += 1; // Every new card insertion will have a unique number stored in this variable
          CardInserted?.Invoke(card);
        }
      };

      var secondMonitor = MonitorFactory.Instance.Create(SCardScope.System);

      secondMonitor.StatusChanged += (s, e) => {
        Log("CUSTOM: " + e.LastState + " => " + e.NewState);
      };

      secondMonitor.Start(name);
    }

    public void Start() {
      DisposeInvalidReader();

      if (reader == null) {
        cardPresentProperty.Value = false;
        statusProperty.Value = State.WAITING_CARD_READER;

        reader = new();

        reader.Monitor.Initialized += OnInitialized;
        reader.Monitor.StatusChanged += OnStatusChanged;
        reader.Monitor.MonitorException += OnMonitorException;

        Log("New reader started monitoring");
        reader.Start(Name);
      }
    }

    private void DisposeInvalidReader() {
      if (reader != null && !reader.Context.IsValid()) {
        Log("Disposed reader");
        reader.Dispose();
        reader = null;
      }
    }

    public void ReadRecords(byte[] atr, int card) {
      CardRecordsReader? recordsReader = FindValidRecordsReader(atr);
      if (recordsReader == null) {
        SetStatusIfCardPresent(State.PROTOCOL_MISMATCH);
        return;
      }

      CardReader? reader = this.reader;
      if (reader == null) {
        SetStatusIfCardPresent(State.WAITING_CARD_READER);
        return;
      }

      Task.Run(() => {
        ReadRecords(recordsReader, reader, card, Config.Instance.SmartCard.CardReadingAttemptsUntilGiveUp);
      }).OnExceptionQuit();
    }

    private void ReadRecords(CardRecordsReader recordsReader, CardReader reader, int card, int readingAttempts) {
      try {
        Log("Ready to connect to the card");
        lock (readingLock) { // Prevent reading twice at the same time
          try {
            Measure.Start();
            Log("Connecting...");
            SetStatusIfCardPresent(State.READING_CARD);
            reader.Connect(Name);
            Log($"Connected {Measure.ElapsedMillis()}ms");

            IReadOnlyDictionary<string, object> records = recordsReader.Read(reader.IsoReader);
            SetStatus(State.READING_SUCCESS);
            Log($"Reading done {Measure.ElapsedMillis()}ms");
            Task.Run(() => {
              Log($"RecordsRead: {records.ToDisplayString()}");
              RecordsRead?.Invoke(card, records);
            }).OnExceptionQuit();

            Log($"Disconnecting... {Measure.ElapsedMillis()}ms");
            reader.Disconnect();
            Log($"Disconnected {Measure.ElapsedMillis()}ms");
          } finally {
            Measure.Stop();
          }
        }
      } catch (PCSCException e) {
        Log($"Failed to communicate with the card ({e.Message}, {e.SCardError:X})");
        SetStatusIfCardPresent(State.READING_FAILED);
      } catch (ApduException e) {
        ReattemptReadRecords(recordsReader, reader, card, readingAttempts, e);
      }
    }

    private async void ReattemptReadRecords(CardRecordsReader recordsReader, CardReader reader, int card, int readingAttempts, Exception e) {
      if (readingAttempts > 0) {
        int sleepTime = Config.Instance.SmartCard.CardReadingFailedRetryInterval;
        Log($"Failed to communicate with the card. Retrying in {sleepTime}ms... ({e.Message})");

        await Task.Delay(sleepTime).OnExceptionQuit();

        ReadRecords(recordsReader, reader, card, readingAttempts - 1);
      } else {
        Log($"Failed to communicate with the card ({e.Message})");
        SetStatusIfCardPresent(State.APDU_EXCEPTION);
      }
    }

    public void WaitForCardAbsent(int card) {

      lock (StatusLock) {
        if (this.card == card && cardPresentProperty.Value) {
          void ReleaseCardPresentSemaphoreListener(bool o, bool present) {
            cardPresentProperty.PropertyChanged -= ReleaseCardPresentSemaphoreListener;
            cardPresentLock.Signal(); // count 1 => 0
          }
          cardPresentLock.Reset(); // count 0 => 1
          cardPresentProperty.PropertyChanged += ReleaseCardPresentSemaphoreListener;
        }
      }

      if(cardPresentLock.CurrentCount == 1) {
        Log($"Waiting card to be absent");
        cardPresentLock.Wait(); // wait for count 1 => 0
        Log("Waiting is over. Card removed");
      }
    }

    public bool IsCardAbsent(int card) {
      lock (StatusLock) {
        return this.card != card || !cardPresentProperty.Value;
      }
    }

    private void ProcessState(SCRState state, byte[] atr) {
      Log(state);
      // Unaware, Ignore, Changed, (Unknown, Ignore), Unavailable
      // Empty, Present, (AtrMatch, Present), (Exclusive, Present)
      // (InUse, Present), Mute, Unpowered
      switch (state) {
        case SCRState.Ignore:
        case SCRState.Unavailable:
        case SCRState.Ignore | SCRState.Unavailable:
          SetStatus(State.WAITING_CARD_READER);
          break;
        case SCRState.Empty:
          SetStatus(State.WAITING_CARD);
          break;
        case SCRState.Present:
        case SCRState.Present | SCRState.Unpowered:
        case SCRState.Present | SCRState.InUse:
        case SCRState.Present | SCRState.Exclusive | SCRState.InUse:
          bool cardInserted = false;
          lock (StatusLock) {
            if (!cardPresentProperty.Value) {
              statusProperty.Value = State.CARD_PRESENT;
              cardInserted = true;
            }
          }
          if (cardInserted) {
            ReadRecords(atr, card);
          }
          break;
        case SCRState.Present | SCRState.Mute:
          SetStatus(State.UNRESPONSIVE_CARD);
          break;
      }
    }

    private void SetStatus(State status) {
      lock (StatusLock) {
        statusProperty.Value = status;
      }
    }

    private void SetStatusIfCardPresent(State status) {
      lock (StatusLock) {
        if (cardPresentProperty.Value) {
          statusProperty.Value = status;
        }
      }
    }

    private void OnInitialized(object sender, CardStatusEventArgs args) {
      ProcessState(args.State, args.Atr);
    }

    private void OnStatusChanged(object sender, StatusChangeEventArgs args) {
      ProcessState(args.NewState, args.Atr ?? Array.Empty<byte>());
    }

    private void OnMonitorException(object sender, PCSCException e) {
      App.ShowException(e);
    }

    public void Dispose() {
      reader?.Dispose();
      reader = null;
      cardPresentLock.Dispose();
      GC.SuppressFinalize(this);
    }

    private void Log(object message) {
      App.Log($"{Name} ({Thread.CurrentThread.Name}):", message);
    }
  }
}
