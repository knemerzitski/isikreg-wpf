using System;

namespace IsikReg.SmartCards.Records {
  public class RecordParser : IRecordParser {

    public byte[] RecordNumbers { get; }

    private Func<byte[], string[], object> Parser { get; }

    public RecordParser(Func<byte[], string[], object> parser, params byte[] recordNumbers) {
      Parser = parser;
      RecordNumbers = recordNumbers;
    }
    public object Parse(string[] records) {
      return Parser(RecordNumbers, records);
    }
  }
}
