using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.SmartCards {
  public class CardRecords : ReadOnlyDictionary<string, object> {

    private readonly CardReader reader;
    public CardRecords(CardReader reader, IDictionary<string, object> dictionary) : base(dictionary) {
      this.reader = reader;
    }
  }
}
