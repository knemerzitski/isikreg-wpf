using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.SmartCards.Records {

  public interface IRecordParser {

    public byte[] RecordNumbers { get; }

    public object Parse(string[] records);
  }
}
