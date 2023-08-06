using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.SmartCards {
  public enum State {
    NULL,

    DRIVER_MISSING,

    WAITING_CARD_READER,
    WAITING_CARD,

    UNRESPONSIVE_CARD,
    PROTOCOL_MISMATCH,

    CARD_PRESENT,

    READING_CARD,

    APDU_EXCEPTION,

    READING_FAILED,
    READING_SUCCESS,
  }

}
