using PCSC.Monitoring;
using PCSC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCSC.Exceptions;
using PCSC.Utils;
using PCSC.Iso7816;
using IsikReg.SmartCards.Records;
using IsikReg.Extensions;
using IsikReg.Configuration;
using System.Xml.Linq;
using System.Threading;
using System.ComponentModel;
using System.Formats.Tar;
using IsikReg.Utils;
using PCSC.Extensions;

namespace IsikReg.SmartCards {

  public class CardReader : IDisposable {

    public ISCardContext Context { get; }

    public SCardReader Reader { get; }
    public IsoReader IsoReader { get; }
    public ISCardMonitor Monitor { get; }

    public CardReader() {
      Context = ContextFactory.Instance.Establish(SCardScope.System);
      Reader = new SCardReader(Context);
      IsoReader = new IsoReader(Reader, false);

      Monitor = MonitorFactory.Instance.Create(SCardScope.System);
    }

    public void Start(string name) {
      Monitor.Start(name);
    }

    public void Connect(string name) {
      IsoReader.Connect(name, SCardShareMode.Shared, SCardProtocol.T1);
      SCardError sc = Reader.BeginTransaction();
      sc.ThrowIfNotSuccess();
    }

    public void Disconnect() {
      SCardError sc = Reader.EndTransaction(SCardReaderDisposition.Leave);
      sc.ThrowIfNotSuccess();
      IsoReader.Disconnect(SCardReaderDisposition.Leave);
    }

    public void Dispose() {
      IsoReader.Dispose();
      Reader.Dispose();
      Context.Cancel();
      Context.Dispose();

      Monitor.Cancel();
      Monitor.Dispose();

      GC.SuppressFinalize(this);
    }

  }
}
