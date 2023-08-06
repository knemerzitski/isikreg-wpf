using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.SmartCards.Records {
  public class ApduException: Exception {

    public int Status { get; }

    public ApduException(int status, string message): base(message) {
      Status = status;
    }

  }
}
