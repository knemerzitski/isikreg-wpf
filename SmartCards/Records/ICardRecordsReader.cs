using PCSC.Iso7816;
using System.Collections.Generic;

namespace IsikReg.SmartCards.Records {

  public interface ICardRecordsReader {

    bool CanRead(byte[] atr);

    IReadOnlyDictionary<string, object> Read(IsoReader reader);

  }
}
