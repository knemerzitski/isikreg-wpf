using PCSC.Iso7816;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.SmartCards.Records {

  public interface ICardRecordsReader {

    bool CanRead(byte[] atr);

    IReadOnlyDictionary<string, object> Read(IsoReader reader);

  }
}
