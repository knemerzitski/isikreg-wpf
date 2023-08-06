using IsikReg.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IsikReg.Ui.Status {
  public partial class VariableStatusMessage {

    private interface IPart {
      string Get(IReadOnlyDictionary<string, string> variableMapper, string evnt);
    }

    private class StringPart : IPart {

      private readonly string str;

      public StringPart(string str) {
        this.str = str;
      }

      public string Get(IReadOnlyDictionary<string, string> variableMapper, string evnt) {
        return str;
      }

    }

    private class VariablePart : IPart {

      public readonly string key;

      public VariablePart(string key) {
        this.key = ColumnDictionary.ToKey(key);
      }

      public string Get(IReadOnlyDictionary<string, string> variableMapper, string evnt) {
        return variableMapper.GetValueOrDefault(key, "");
      }
    }

    private class EventPart : IPart {

      public string Get(IReadOnlyDictionary<string, string> variableMapper, string evnt) {
        return evnt;
      }
    }

    private static readonly Regex pattern = VariablePattern();
    //\$(?<id>[\w\d_-]+)|@(?<event>event)
    //\$\{(?<id>.+)}|@(?<event>event)
    //\$\{(?<id>.+?[^\\])}|@(?<event>event)

    public static VariableStatusMessage Parse(string format) {
      VariableStatusMessage sb = new(format);
      int startIndex = 0;
      foreach (Match m in pattern.Matches(format).Cast<Match>()) {
        if (m.Groups.TryGetValue("id", out Group? id) && id != null && !string.IsNullOrEmpty(id.Value)) {
          sb.AddConstant(format[startIndex..m.Index]);
          sb.AddVariable(id.Value);
          startIndex = m.Index + m.Length;
        } else if (m.Groups.TryGetValue("event", out Group? evnt) && evnt != null && !string.IsNullOrEmpty(evnt.Value)) {
          sb.AddConstant(format[startIndex..m.Index]);
          sb.AddEvent();
          startIndex = m.Index + m.Length;
        }
      }
      sb.AddConstant(format[startIndex..]);

      return sb;
    }

    private readonly List<IPart> parts = new();
    private readonly string format;

    public VariableStatusMessage(string format) {
      this.format = format;
    }

    public void AddConstant(string str) {
      if (!string.IsNullOrEmpty(str)) {
        parts.Add(new StringPart(str));
      }
    }
    public void AddVariable(string name) {
      parts.Add(new VariablePart(name));
    }

    public void AddEvent() {
      parts.Add(new EventPart());
    }

    public bool HasAllVariables(IReadOnlyDictionary<string, string> variableMapper) {
      return parts.All(delegate (IPart p) {
        if (p is VariablePart vp) {
          string? val = variableMapper.GetValueOrDefault(vp.key);
          return !string.IsNullOrWhiteSpace(val);
        } else {
          return true;
        }
      });
    }

    //public List<string> GetPossibleVariables() {
    //  return parts.Select(delegate (IPart p) {
    //    if (p is VariablePart vp) {
    //      return vp.key;
    //    } else {
    //      return "";
    //    }
    //  }).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
    //}

    public string GetFormat() {
      return format;
    }

    public string ToString(IReadOnlyDictionary<string, string> variableMapper, string evnt) {
      return string.Join("", parts.Select(p => p.Get(variableMapper, evnt)).ToList());
    }

    [GeneratedRegex("\\{(?<id>.+?)}|@(?<event>event)")]
    private static partial Regex VariablePattern();
  }
}
