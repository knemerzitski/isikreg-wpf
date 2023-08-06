using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IsikReg.Extensions {
  public static class StringExtension {

    public static string EscapedIndexer(this string key) {
      StringBuilder sb = new();
      for (int i = 0; i < key.Length; i++) {
        char c = key[i];
        if (c == '[' || c == ']')
          sb.Append('^').Append(c);
        else
          sb.Append(c);
      }
      return sb.ToString();
    }

    public static string FirstCharCapitalize(this string str) {
      if (str == null || str.Length == 0)
        return String.Empty;

      if (str.Length == 1)
        return str.ToUpper();

      return str[..1].ToUpper() + str[1..].ToLower();
    }
    public static Regex FormatToPattern(this string format, string match, string replace) {
      Regex pattern = new(match);
      Match matcher = pattern.Match(format);
      List<(int, int)> startEndList = new();
      while (matcher.Success) {
        startEndList.Add((matcher.Index, matcher.Index + matcher.Length));
        matcher = matcher.NextMatch();
      }
      StringBuilder sb = new(format);

      if (startEndList.Count > 0) {
        // Quote end
        int start = startEndList.ElementAt(startEndList.Count - 1).Item2;
        int end = sb.Length;
        if (end - start > 0)
          sb.Replace(start, end, Regex.Escape(sb.ToString(start, end - start)));
        for (int i = startEndList.Count - 1; i >= 0; i--) {
          if (i + 1 != startEndList.Count) {
            // Quote right but not end
            start = startEndList.ElementAt(i).Item2;
            end = startEndList.ElementAt(i + 1).Item1;
            if (end - start > 0)
              sb.Replace(start, end, Regex.Escape(sb.ToString(start, end - start)));
          }
          sb.Replace(startEndList.ElementAt(i).Item1, startEndList.ElementAt(i).Item2, replace);
        }

        // Quote Start
        start = 0;
        end = startEndList.ElementAt(0).Item1;
        if (end - start > 0)
          sb.Replace(start, end, Regex.Escape(sb.ToString(start, end - start)));
      }
      return new Regex(sb.ToString());
    }

  }
  public static class StringBuilderExtensions {
    public static StringBuilder Replace(this StringBuilder sb, int start, int end, string str) {
      int count = end - start;
      return sb.Replace(sb.ToString(start, count), str, start, count);
    }

  }
}
