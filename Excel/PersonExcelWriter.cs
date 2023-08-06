using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Extensions;
using IsikReg.Model;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IsikReg.Excel {
  public class PersonExcelWriter {

    private readonly CountdownEvent writingCompleteEvent = new(1);

    public PersonExcelWriter() {
      writingCompleteEvent.Signal(); // 1 => 0
    }

    ~PersonExcelWriter() {
      writingCompleteEvent.Dispose();
    }

    public void WaitWritingCompleted() {
      if (writingCompleteEvent.CurrentCount == 1) {
        writingCompleteEvent.Wait(); // Wait until count is 0
      }
    }

    public void Write(string path, IEnumerable<Person> personList, bool groupByRegistrationType, Action start, Action done) {
      WaitWritingCompleted(); // Prevent writing twice at the same time
      writingCompleteEvent.Reset(); // 0 => 1
      start.Invoke();
      Task.Run(() => {
        try {
          Write(path, personList, groupByRegistrationType);
        } finally {
          done.Invoke();
          writingCompleteEvent.Signal(); // 1 => 0
        }
      }).OnExceptionQuit();
    }

    private static void Write(string path, IEnumerable<Person> personList, bool groupByRegistrationType) {
      try {
        using FileStream fs = new(path, FileMode.OpenOrCreate, FileAccess.Write);
        using IWorkbook workbook = new XSSFWorkbook();
        Write(workbook, personList, groupByRegistrationType);
        workbook.Write(fs, false);
      } catch (IOException ex) {
        App.ShowException(ex, false);
      }
    }

    private static void Write(IWorkbook workbook, IEnumerable<Person> personList, bool groupByRegistrationType) {
      ICreationHelper createHelper = workbook.GetCreationHelper();

      ICellStyle dateTimeStyle = workbook.CreateCellStyle();
      dateTimeStyle.DataFormat = createHelper.CreateDataFormat().GetFormat(Config.Instance.Excel.ExportDateTimeFormat);

      ICellStyle dateStyle = workbook.CreateCellStyle();
      dateStyle.DataFormat = createHelper.CreateDataFormat().GetFormat(Config.Instance.Excel.ExportDateFormat);

      ISheet sheet = workbook.CreateSheet(Config.Instance.Excel.SheetName);

      int rowIndex = 0;
      IRow headerRow = sheet.CreateRow(rowIndex++);
      // Don't save registered column, can be seen from date column also ignore columns without label
      List<Column> columns = Config.Instance.Columns.Values.Where(c => !string.IsNullOrWhiteSpace(c.Label) && c.Id != ColumnId.REGISTERED).ToList();

      // GROUP BY REGISTRATION TYPE
      Column? regDateColumn = columns.Where(c => c.Group == ColumnGroup.REGISTRATION && c.Id == ColumnId.REGISTER_DATE).FirstOrDefault();
      Column? regTypeColumn = columns.Where(c => c.Group == ColumnGroup.REGISTRATION && c.Id == ColumnId.REGISTRATION_TYPE).FirstOrDefault();
      List<GroupedColumn>? groupedColumns = groupByRegistrationType ? Config.Instance.RegistrationTypeGroupColumns() : null;
      if (groupedColumns != null) {
        groupedColumns.ForEach(c => {
          int index = columns.IndexOf(c.Source);
          if (index != -1) {
            columns.Insert(index, c);
          } else {
            columns.Add(c);
          }
        });
        if (regTypeColumn != null) {
          columns.Remove(regTypeColumn);
        }
        groupedColumns.ForEach(c => columns.Remove(c.Source));
      }

      for (int i = 0; i < columns.Count; i++) {
        Column column = columns[i];
        ICell cell = headerRow.CreateCell(i);
        cell.SetCellValue(column.Label.Replace("\n", " "));
      }

      // Autosize by headers
      for (int i = 0; i < columns.Count; i++) {
        sheet.AutoSizeColumn(i);
      }

      List<string> registrationTypes = Config.Instance.GetRegistrationTypes();
      foreach (Person person in personList) {
        // Sort registrations by date
        ImmutableSortedSet<Registration> registrations = person.Registrations.ToImmutableSortedSet();

        // Group registrations by type (put them on same row) if enabled in settings
        List<IEnumerable<Registration>> groupedRegistrations = new();
        if (groupedColumns != null && groupedColumns.Count > 0) {
          int counter = 0;
          while (counter < registrations.Count) {
            List<Registration> sameRowRegistrations = new();
            foreach (GroupedColumn groupedColumn in groupedColumns) {
              if (counter >= registrations.Count) break;
              if (groupedColumn.Source != regDateColumn) continue;
              Registration r = registrations[counter];
              if (r.RegistrationType.Equals(groupedColumn.Name)) {
                sameRowRegistrations.Add(r);
                counter++;
              } else if (!registrationTypes.Contains(r.RegistrationType)) { // Invalid registration type
                counter++;
              }
            }
            if (sameRowRegistrations.Count > 0) {
              groupedRegistrations.Add(sameRowRegistrations);
            }
          }
        } else {
          // Wraps each registration to a list with one element
          groupedRegistrations.AddRange(registrations.Select(r => Enumerable.Repeat(r, 1)));
        }
        foreach (IEnumerable<Registration> registrationGroup in groupedRegistrations) {
          IRow row = sheet.CreateRow(rowIndex++);

          // Write all person columns
          IReadOnlyDictionary<string, object> personProps = person.Properties;
          foreach ((string key, object? prop) in personProps) {
            Column? column = Config.Instance.Columns.GetValueOrDefault(key);
            if (column == null || !columns.Contains(column)) continue;
            int k = columns.IndexOf(column);
            if (k == -1) continue;
            ICell cell = row.CreateCell(k);
            WriteValueToCell(prop, cell, dateTimeStyle, dateStyle);
          }

          // Write all registration columns
          foreach (Registration registration in registrationGroup) {
            IReadOnlyDictionary<string, object> regProps = registration.Properties;
            columns.ForEach(column => {
              object? prop = null;
              if (column is GroupedColumn groupedColumn) {
                if (registration.RegistrationType.Equals(groupedColumn.Name)) {
                  prop = regProps.GetValueOrDefault(groupedColumn.Source.Key); // TODO source as string
                }
              } else {
                prop = regProps.GetValueOrDefault(column.Key); // TODO key as strign?
              }
              if (prop != null) {
                int k = columns.IndexOf(column);
                if (k != -1) {
                  ICell cell = row.CreateCell(k);
                  WriteValueToCell(prop, cell, dateTimeStyle, dateStyle);
                }
              }
            });
          }
        }
      }

      // TODO remove
      //if (Config.Instance.Excel.ExportAutoSizeColumns) {
      //  for (int i = 0; i < columns.Count; i++) {
      //    sheet.AutoSizeColumn(i);
      //  }
      //}
    }

    private static void WriteValueToCell(object? value, ICell cell, ICellStyle dateTimeStyle, ICellStyle dateStyle) {
      if (value == null) return;
      if (value is DateTime date) {
        if (date != date.Date) {
          cell.CellStyle = dateTimeStyle;
          cell.SetCellValue(date);
        } else {
          cell.CellStyle = dateStyle;
          cell.SetCellValue((int)DateUtil.GetExcelDate(date));
        }
      } else if (value is string str) {
        if (double.TryParse(str, out double result)) {
          cell.SetCellValue(result);
        } else {
          cell.SetCellValue(str);
        }
      } else if (value is bool b) {
        cell.SetCellValue(b);
      }
    }

  }
}
