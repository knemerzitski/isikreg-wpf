using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Extensions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.Excel {
  public static class PersonExcelReader {

    public static void Read(IEnumerable<string> paths, Action<IReadOnlyDictionary<string, object>> process,
      Action start, Action done) {
      start.Invoke();

      Task.Run(() => {
        try {
          Read(paths.ToList(), process);
        } finally {
          done.Invoke();
        }
      }).OnExceptionQuit();
    }

    private static void Read(List<string> paths, Action<IReadOnlyDictionary<string, object>> process) {
      Parallel.ForEach(paths, path => {
        Read(path, process);
      });
    }

    private static void Read(string path, Action<IReadOnlyDictionary<string, object>> process) {
      App.Log($"Reading {path}");

      using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
      using IWorkbook workbook = new XSSFWorkbook(fs);
      fs.Close();
      App.Log($"Done reading {path}");

      IFormulaEvaluator evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();

      ISheet? sheet = workbook.GetSheet(Config.Instance.Excel.SheetName);
      sheet ??= workbook.GetSheetAt(0);

      // Find header, first cell that has value
      IRow? headerRow = null;
      int headerRowIndex, headerStartCellIndex = 0;
      for (headerRowIndex = sheet.FirstRowNum; headerRowIndex <= sheet.LastRowNum; headerRowIndex++) {
        IRow row = sheet.GetRow(headerRowIndex);
        for (headerStartCellIndex = row.FirstCellNum; headerStartCellIndex < row.LastCellNum; headerStartCellIndex++) {
          ICell cell = row.GetCell(headerStartCellIndex);
          if (!cell.IsEmpty(evaluator)) {
            headerRow = row;
            break;
          }
        }
        if (headerRow != null) {
          break;
        }
      }
      if (headerRow == null) {
        return;
      }

      // Columns + columns with registration type
      ColumnDictionary extendedColumns = new(Config.Instance.Columns.Concat(Config.Instance.RegistrationTypeGroupColumns()));

      // Find columns by header name
      Dictionary<int, Column> colIndexToColumn = new();
      for (int j = headerStartCellIndex; j < headerRow.LastCellNum; j++) {
        ICell? cell = headerRow.GetCell(j);
        if (cell == null) continue;
        string? value = cell.GetCellString(evaluator);
        if (!string.IsNullOrWhiteSpace(value)) {
          // determine column for index
          if (extendedColumns.TryGetValue(ColumnDictionary.ToKey(value), out Column? column)) {
            colIndexToColumn[j] = column;
          }
        }
      }

      string regTypeKey = Config.Instance.Columns[ColumnId.REGISTRATION_TYPE].Key;

      for (int i = headerRowIndex + 1; i <= sheet.LastRowNum; i++) {
        IRow? row = sheet.GetRow(i);
        if (row == null) continue;

        List<Dictionary<string, object>> registrations = new();
        Dictionary<string, object> registration = new();
        registrations.Add(registration);

        foreach ((int index, Column column) in colIndexToColumn) {
          ICell? cell = row.GetCell(index);
          if (cell == null) continue;
          Column? valueColumn = null;
          if (column is GroupedColumn groupedColumn) {
            Dictionary<string, object>? regWithType = registrations
              .Where(r => r.GetString(ColumnId.REGISTRATION_TYPE).Equals(groupedColumn.Name))
              .FirstOrDefault();
            if (regWithType == null) {
              registration = new();
              registrations.Add(registration);
              registration.Set(ColumnId.REGISTRATION_TYPE, groupedColumn.Name);
            } else {
              registration = regWithType;
            }
            //properties = registration.WithPersonProperties;
            valueColumn = groupedColumn.Source; // TODO replace column with key
          } else {
            valueColumn = column; // TODO replace column with key
          }

          if (valueColumn.GetValueType().Equals(typeof(DateTime))) {
            // TODO this enough? dont need to deal with timezone?
            DateTime? dateTime = cell.GetCellDate(evaluator);
            if (dateTime != null) {
              registration[valueColumn.Key] = dateTime;
            }
          } else if (valueColumn.GetValueType().Equals(typeof(string))) {
            string? str = cell.GetCellString(evaluator);
            if (str != null) {
              registration[valueColumn.Key] = str;
            }
          } else if (valueColumn.GetValueType().Equals(typeof(bool))) {
            bool? b = cell.GetCellBoolean(evaluator);
            if (b != null) {
              registration[valueColumn.Key] = b;
            }
          }
        }

        lock (process) {
          foreach (Dictionary<string, object> r in registrations) {
            if (!string.IsNullOrWhiteSpace(r.GetPersonalCode())) {
              process.Invoke(r);
            }
          }
        }
      }
    }

  }
}
