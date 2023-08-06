using NPOI.SS.UserModel;
using System;

namespace IsikReg.Extensions {
  public static class ExcelExtension {

    public static bool IsEmpty(this ICell cell, IFormulaEvaluator fe) {
      return cell.CellType switch {
        CellType.String => string.IsNullOrWhiteSpace(cell.StringCellValue),
        CellType.Numeric or CellType.Boolean => false,
        CellType.Formula => fe.Evaluate(cell).IsEmpty(),
        _ => true,
      };
    }

    public static bool IsEmpty(this CellValue cell) {
      return cell.CellType switch {
        CellType.String => string.IsNullOrWhiteSpace(cell.StringValue),
        CellType.Numeric or CellType.Boolean => false,
        _ => true,
      };
    }

    public static string? GetCellString(this ICell cell, IFormulaEvaluator fe) {
      switch (cell.CellType) {
        case CellType.String:
          return cell.StringCellValue.Trim();
        case CellType.Numeric:
          if(cell.NumericCellValue == (long)cell.NumericCellValue) {
            return ((long)cell.NumericCellValue).ToString();
          } else {
            return cell.NumericCellValue.ToString();
          }
        case CellType.Boolean:
          return cell.BooleanCellValue ? "TRUE" : "FALSE";
        case CellType.Formula:
          return fe.Evaluate(cell).GetCellString();
        default:
          return null;
      }
    }

    public static string? GetCellString(this CellValue cell) {
      switch (cell.CellType) {
        case CellType.String:
          return cell.StringValue.Trim();
        case CellType.Numeric:
          if (cell.NumberValue == (long)cell.NumberValue) {
            return ((long)cell.NumberValue).ToString();
          } else {
            return cell.NumberValue.ToString();
          }
        case CellType.Boolean:
          return cell.BooleanValue ? "TRUE" : "FALSE";
        default:
          return null;
      }
    }


    public static bool? GetCellBoolean(this ICell cell, IFormulaEvaluator fe) {
      return cell.CellType switch {
        CellType.String => !string.IsNullOrWhiteSpace(cell.StringCellValue),
        CellType.Numeric => cell.NumericCellValue != 0,
        CellType.Boolean => cell.BooleanCellValue,
        CellType.Formula => fe.Evaluate(cell).GetCellBoolean(),
        _ => null,
      };
    }

    public static bool? GetCellBoolean(this CellValue cell) {
      return cell.CellType switch {
        CellType.String => !string.IsNullOrWhiteSpace(cell.StringValue),
        CellType.Numeric => cell.NumberValue != 0,
        CellType.Boolean => cell.BooleanValue,
        _ => null,
      };
    }

    public static DateTime? GetCellDate(this ICell cell, IFormulaEvaluator fe) {
      return cell.CellType switch {
        CellType.Numeric => cell.DateCellValue,
        CellType.Formula => fe.Evaluate(cell).GetCellDate(),
        _ => null,
      };
    }

    public static DateTime? GetCellDate(this CellValue cell) {
      return cell.CellType switch {
        CellType.Numeric => DateUtil.GetJavaDate(cell.NumberValue),
        _ => null,
      };
    }

    //public static bool IsDateTimeStyle(this ICellStyle style) {
    //  string s = style.GetDataFormatString();
    //  return s.Contains("h") || s.Contains("s");
    //}

  }
}
