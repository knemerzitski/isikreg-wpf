using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Extensions;
using PCSC;
using PCSC.Iso7816;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace IsikReg.SmartCards.Records {

  /**
   * Code was written using the following website as a guide: https://eid.eesti.ee/index.php/Creating_new_eID_applications#Direct_communication_with_the_card
   * Source: https://www.id.ee/public/EstEID_kaardi_kasutusjuhend.pdf
   */
  public class EstIdV2011CardRecordsReader : CardRecordsReader {

    private static readonly byte[] ATR_PROTOCOL = { 0x3B, 0xFA, 0x18, 0x00, 0x00, 0x80, 0x31, 0xFE, 0x45, 0xFE };

    // The command to choose root folder
    private static readonly CommandApdu SELECT_FILE_MF = new(IsoCase.Case2Short, SCardProtocol.T1) {
      CLA = 0x00,
      Instruction = InstructionCode.SelectFile,
      P1 = 0x00,
      P2 = 0x0c,
    };

    // The command to choose folder EEEE which contains personal data
    private static readonly CommandApdu SELECT_FILE_EEEE = new(IsoCase.Case4Short, SCardProtocol.T1) {
      CLA = 0x00,
      Instruction = InstructionCode.SelectFile,
      P1 = 0x01,
      P2 = 0x0c,
      Data = new byte[] { 0xee, 0xee },
    };

    // The command to choose file 5044.
    private static readonly CommandApdu SELECT_FILE_5044 = new(IsoCase.Case4Short, SCardProtocol.T1) {
      CLA = 0x00,
      Instruction = InstructionCode.SelectFile,
      P1 = 0x02,
      P2 = 0x04,
      Data = new byte[] { 0x50, 0x44 },
    };

    private static readonly IReadOnlyDictionary<byte, CommandApdu> READ_RECORD;

    private static readonly Encoding CARD_ENCODING;

    private static readonly IReadOnlyDictionary<string, IRecordParser> RECORDS_PARSERS;

    static EstIdV2011CardRecordsReader() {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      CARD_ENCODING = Encoding.GetEncoding(1252);

      Func<byte[], string[], string> selectRecord = (indices, records) =>
        records[indices[0] - 1];

      Func<byte[], string[], string> selectDashJoinRecords = (indices, records) =>
        string.Join("-", indices.Select(nr => records[nr - 1]).Where(rec => !string.IsNullOrWhiteSpace(rec)));

      DateTimeFormat dateFormat = new("dd.MM.yyyy");
      Func<string, object> toDate = (str) => DateTime.Parse(str, dateFormat.FormatProvider);
      Func<byte[], string[], object> selectDateRecord = selectRecord.Compose(toDate);

      // Records
      //    1 Perenimi 28 Xn
      //    2 Eesnimede rida 1 15 Xn
      //    3 Eesnimede rida 2 15 Xn
      //    4 Sugu 1 X?
      //    5 Kodakondsus (3 tähte, alati EST) 3 XXX?
      //    6 Sünnikuupäev (pp.kk.aaaa) 10 DD.MM.YYYY
      //    7 Isikukood 11 99999999999
      //    8 Dokumendi number 8 XX999999
      //    9 Kehtivuse viimane päev (pp.kk.aaaa) 10 DD.MM.YYYY
      //    10 Sünnikoht 35 XXX
      //    11 Väljaandmise kuupäev (pp.kk.aaaa) 10 DD.MM.YYYY
      //    12 Elamisloa tüüp 50 Xn?
      //    13 Märkuste rida 1 50 Xn?
      //    14 Märkuste rida 2 50 Xn?
      //    15 Märkuste rida 3 50 Xn?
      //    16 Märkuste rida 4 50 Xn?
      Dictionary<ColumnId, IRecordParser> parsers = new() {
        { ColumnId.LAST_NAME, new RecordParser(selectRecord, 1) },
        { ColumnId.FIRST_NAME, new RecordParser(selectDashJoinRecords, 2, 3) },
        { ColumnId.SEX, new RecordParser(selectRecord, 4) },
        { ColumnId.CITIZENSHIP, new RecordParser(selectRecord, 5) },
        { ColumnId.DATE_OF_BIRTH, new RecordParser(selectDateRecord, 6) },
        { ColumnId.PERSONAL_CODE, new RecordParser(selectRecord, 7) },
        { ColumnId.DOCUMENT_NR, new RecordParser(selectRecord, 8) },
        { ColumnId.EXPIRY_DATE, new RecordParser(selectDateRecord, 9) },
        { ColumnId.PLACE_OF_BIRTH, new RecordParser(selectRecord, 10) },
        { ColumnId.DATE_OF_ISSUANCE, new RecordParser(selectDateRecord, 11) },
        { ColumnId.TYPE_OF_RESIDENCE_PERMIT, new RecordParser(selectRecord, 12) },
        { ColumnId.NOTES_LINE1, new RecordParser(selectRecord, 13) },
        { ColumnId.NOTES_LINE2, new RecordParser(selectRecord, 14) },
        { ColumnId.NOTES_LINE3, new RecordParser(selectRecord, 15) },
        { ColumnId.NOTES_LINE4, new RecordParser(selectRecord, 16) },
      };

      // Use only records that are defined in settings
      Dictionary<string, IRecordParser> definedParsers = new();
      foreach (var parser in parsers) {
        if (Config.Instance.Columns.TryGetValue(parser.Key, out Column? column)) {
          definedParsers.Add(column.Key, parser.Value);
        }
      }
      RECORDS_PARSERS = definedParsers;

      READ_RECORD = RECORDS_PARSERS
        .SelectMany(r => r.Value.RecordNumbers)
        .Distinct()
        .ToImmutableDictionary(b => b, b => new CommandApdu(IsoCase.Case2Short, SCardProtocol.T1) {
          CLA = 0x00,
          Instruction = InstructionCode.ReadRecord,
          P1 = b,
          P2 = 0x04,
        });
    }

    public override bool CanRead(byte[] atr) {
      if (atr.Length < ATR_PROTOCOL.Length) {
        return false;
      }
      return ATR_PROTOCOL.SequenceEqual(atr.Take(10));
    }

    protected override void SendCommandsToReadRecords(IsoReader reader) {
      // Navigate to personal data file
      App.Log("SELECT_FILE_MF");
      SendCommand(reader, SELECT_FILE_MF);
      App.Log("SELECT_FILE_EEEE");
      SendCommand(reader, SELECT_FILE_EEEE);
      App.Log("SELECT_FILE_5044");
      SendCommand(reader, SELECT_FILE_5044);
    }

    protected override string ReadRecord(IsoReader reader, byte recordNumber) {
      App.Log("SELECT RECORD " + recordNumber.ToString("X"));
      byte[] recordResult = SendCommand(reader, READ_RECORD[recordNumber]);
      return CARD_ENCODING.GetString(recordResult);
    }

    protected override IReadOnlyDictionary<string, IRecordParser> GetRecordParsers() {
      return RECORDS_PARSERS;
    }

  }
}

