using System;

namespace IsikReg.SmartCards.Records {
  public class ApduException: Exception {

    public int Status { get; }

    public ApduException(int status, string message): base(message) {
      Status = status;
    }

  }
}
