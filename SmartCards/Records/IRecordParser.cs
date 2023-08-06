namespace IsikReg.SmartCards.Records {

  public interface IRecordParser {

    public byte[] RecordNumbers { get; }

    public object Parse(string[] records);
  }
}
