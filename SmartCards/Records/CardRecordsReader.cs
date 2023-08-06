using PCSC.Iso7816;
using System.Collections.Generic;
using System.Linq;

namespace IsikReg.SmartCards.Records {

  public abstract class CardRecordsReader : ICardRecordsReader {

    // Card response status
    private static readonly int ResponseOK = 0x9000;

    protected static byte[] SendCommand(IsoReader reader, CommandApdu cmd) {
      Response r = reader.Transmit(cmd);
      if (r.StatusWord != ResponseOK) {
        throw new ApduException(r.StatusWord, "Card response not OK " + r.StatusWord.ToString("X"));
      }
      return r.GetData();
    }

    public abstract bool CanRead(byte[] atr);

    public IReadOnlyDictionary<string, object> Read(IsoReader reader) {
      IReadOnlyDictionary<string, IRecordParser> parsers = GetRecordParsers();
      List<byte> recordNumbers = parsers.Values.SelectMany(p => p.RecordNumbers).Distinct().ToList();

      string[] records = new string[recordNumbers.Max()];
      SendCommandsToReadRecords(reader);
      foreach (byte recordNumber in recordNumbers) {
        records[recordNumber - 1] = ReadRecord(reader, recordNumber);
      }

      return parsers.ToDictionary(e => e.Key, e => e.Value.Parse(records));
    }

    protected abstract void SendCommandsToReadRecords(IsoReader reader);

    protected abstract string ReadRecord(IsoReader reader, byte recordNumber);

    protected abstract IReadOnlyDictionary<string, IRecordParser> GetRecordParsers();


  }
}
