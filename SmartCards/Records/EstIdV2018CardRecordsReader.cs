using IsikReg.Configuration.Columns;
using IsikReg.Configuration;
using PCSC.Iso7816;
using PCSC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using IsikReg.Extensions;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using IsikReg.Utils;

namespace IsikReg.SmartCards.Records {

  /**
   * Source TD-ID1-Chip-App: https://installer.id.ee/media/id2019/TD-ID1-Chip-App.pdf
   */
  public class EstIdV2018CardRecordsReader : CardRecordsReader {

    private static readonly byte[] ATR_PROTOCOL = { 0x3B, 0xDB, 0x96, 0x00, 0x80, 0xB1, 0xFE, 0x45, 0x1F, 0x83 };

    // Select Main AID
    private static readonly CommandApdu SELECT_FILE_AID = new(IsoCase.Case4Short, SCardProtocol.T1) {
      CLA = 0x00,
      Instruction = InstructionCode.SelectFile,
      P1 = 0x04,
      P2 = 0x00,
      Data = new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x77, 0x01, 0x08, 0x00, 0x07, 0x00, 0x00, 0xFE, 0x00, 0x00, 0x01, 0x00 },
    };

    // Personal Data transparent files DF ID 5000hex
    private static readonly CommandApdu SELECT_FILE_5000 = new(IsoCase.Case4Short, SCardProtocol.T1) {
      CLA = 0x00,
      Instruction = InstructionCode.SelectFile,
      P1 = 0x01,
      P2 = 0x0c,
      Data = new byte[] { 0x50, 0x00 },
    };

    private static readonly IReadOnlyDictionary<byte, CommandApdu> SELECT_RECORD;


    private static readonly CommandApdu READ_BYTES = new(IsoCase.Case2Short, SCardProtocol.T1) { // TODO case1?
      CLA = 0x00,
      Instruction = InstructionCode.ReadBinary,
      P1 = 0x00,
      P2 = 0x00,
    };

    private static readonly Encoding CARD_ENCODING = Encoding.UTF8;

    private static readonly IReadOnlyDictionary<string, IRecordParser> RECORDS_PARSERS;

    static EstIdV2018CardRecordsReader() {
      Func<byte[], string[], string> selectRecord = (indices, records) =>
        records[indices[0] - 1];

      DateTimeFormat dateFormat = new("dd MM yyyy");
      Func<string, object> toDate = (str) =>
        DateTime.Parse(str, dateFormat.FormatProvider);
      Func<byte[], string[], object> selectDateRecord = selectRecord.Compose(toDate);

      Regex datePlaceRegex = new("(.{2}\\s.{2}\\s.{4})\\s?(.*)");
      Func<string, string> dateString = (str) => datePlaceRegex.Match(str).Groups[1].Value;
      Func<string, string> placeString = (str) => datePlaceRegex.Match(str).Groups[2].Value;
      Func<byte[], string[], object> selectOnlyDateRecord = selectRecord.Compose(dateString).Compose(toDate);
      Func<byte[], string[], object> selectOnlyPlaceRecord = selectRecord.Compose(placeString);

      // Records
      //| |-- PD1 (Surname)
      //| |-- PD2 (First Name)
      //| |-- PD3 (Sex)
      //| |-- PD4 (Citizenship ISO3166 alpha-3)
      //| |-- PD5 (Date and place of birth)
      //| |-- PD6 (Personal Identification Code)
      //| |-- PD7 (Document Number)
      //| |-- PD8 (Expiry Date)
      //| |-- PD9 (Date and place of Issuance)
      //| |-- PD10 (Type of residence permit)
      //| |-- PD11 (Notes Line 1)
      //| |-- PD12 (Notes Line 2)
      //| |-- PD13 (Notes Line 3)
      //| |-- PD14 (Notes Line 4)
      //| |-- PD15 (Notes Line 5)
      Dictionary<ColumnId, IRecordParser> parsers = new() {
        { ColumnId.LAST_NAME, new RecordParser(selectRecord, 1) },
        { ColumnId.FIRST_NAME, new RecordParser(selectRecord, 2) },
        { ColumnId.SEX, new RecordParser(selectRecord, 3) },
        { ColumnId.CITIZENSHIP, new RecordParser(selectRecord, 4) },
        { ColumnId.DATE_OF_BIRTH, new RecordParser(selectOnlyDateRecord, 5) },
        { ColumnId.PLACE_OF_BIRTH, new RecordParser(selectOnlyPlaceRecord, 5) },
        { ColumnId.PERSONAL_CODE, new RecordParser(selectRecord, 6) },
        { ColumnId.DOCUMENT_NR, new RecordParser(selectRecord, 7) },
        { ColumnId.EXPIRY_DATE, new RecordParser(selectDateRecord, 8) },
        { ColumnId.DATE_OF_ISSUANCE, new RecordParser(selectOnlyDateRecord, 9) },
        { ColumnId.PLACE_OF_ISSUANCE, new RecordParser(selectOnlyPlaceRecord, 9) },
        { ColumnId.TYPE_OF_RESIDENCE_PERMIT, new RecordParser(selectRecord, 10) },
        { ColumnId.NOTES_LINE1, new RecordParser(selectRecord, 11) },
        { ColumnId.NOTES_LINE2, new RecordParser(selectRecord, 12) },
        { ColumnId.NOTES_LINE3, new RecordParser(selectRecord, 13) },
        { ColumnId.NOTES_LINE4, new RecordParser(selectRecord, 14) },
        { ColumnId.NOTES_LINE5, new RecordParser(selectRecord, 15) },
      };

      // Use only records that are defined in settings
      Dictionary<string, IRecordParser> definedParsers = new();
      foreach (var parser in parsers) {
        if (Config.Instance.Columns.TryGetValue(parser.Key, out Column? column)) {
          definedParsers.Add(column.Key, parser.Value);
        }
      }
      RECORDS_PARSERS = definedParsers;

      SELECT_RECORD = RECORDS_PARSERS
        .SelectMany(r => r.Value.RecordNumbers)
        .Distinct()
        .ToImmutableDictionary(b => b, b => new CommandApdu(IsoCase.Case4Short, SCardProtocol.T1) {
          CLA = 0x00,
          Instruction = InstructionCode.SelectFile,
          P1 = 0x01,
          P2 = 0x0c,
          Data = new byte[] { 0x50, b },
        });
    }

    public override bool CanRead(byte[] atr) {
      if (atr.Length < ATR_PROTOCOL.Length) {
        return false;
      }
      return ATR_PROTOCOL.SequenceEqual(atr.Take(10));
    }

    protected override void SendCommandsToReadRecords(IsoReader reader) {
      // '6282' - End of file reached before reading ‘Ne’ bytes
      // '6981' - Command incompatible with file structure
      // '6982' - Security status not satisfied
      // ‘6985’ - Current DF is "deactivated" or "terminated" / MF was not created
      // ‘6A80’ - Wrong data
      // '6A82' - File not found(no current EF)
      // '6B00' - Wrong parameters P1-P2 : Offset + length is beyond the end of file

      // Navigate to personal data file
      //App.Log("SELECT_FILE_AID");
      App.Log($">> SELECT_FILE_AID {Measure.ElapsedMillis()}ms");
      SendCommand(reader, SELECT_FILE_AID);
      //App.Log("SELECT_FILE_5000");
      App.Log($">> SELECT_FILE_5000 {Measure.ElapsedMillis()}ms");
      SendCommand(reader, SELECT_FILE_5000);
    }

    protected override string ReadRecord(IsoReader reader, byte recordNumber) {
      App.Log($">> SELECT_RECORD {recordNumber} {Measure.ElapsedMillis()}ms");
      SendCommand(reader, SELECT_RECORD[recordNumber]);
      App.Log($">> READ_BYTES {Measure.ElapsedMillis()}ms");
      return CARD_ENCODING.GetString(SendCommand(reader, READ_BYTES));
    }


    protected override IReadOnlyDictionary<string, IRecordParser> GetRecordParsers() {
      return RECORDS_PARSERS;
    }

  }
}
