﻿namespace IsikReg.Configuration.Columns {
  public class GroupedColumn : Column {

    public string Name { get; }
    public Column Source { get; }

    public GroupedColumn(string name, Column source) {
      Name = name;
      Source = source;
    }

  }
}
