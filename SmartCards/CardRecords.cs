using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IsikReg.SmartCards {
  public class CardRecords : ReadOnlyDictionary<string, object> {

    private readonly CardReader reader;
    public CardRecords(CardReader reader, IDictionary<string, object> dictionary) : base(dictionary) {
      this.reader = reader;
    }
  }
}
