using IsikReg.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace IsikReg.Json {
  public static class TypeInfoResolverModifiers {

    public static void SkipEmptyCollections(JsonTypeInfo typeInfo) {
      foreach (JsonPropertyInfo prop in typeInfo.Properties) {
        if (typeof(ICollection).IsAssignableFrom(prop.PropertyType)) {
          prop.ShouldSerialize = delegate (object _, object? val) {
            return val is ICollection col && col.Count > 0;
          };
        }
      }
    }

    public static void SkipNullOrWhitespaceStrings(JsonTypeInfo typeInfo) {
      foreach (JsonPropertyInfo prop in typeInfo.Properties) {
        if (typeof(string).IsAssignableFrom(prop.PropertyType)) {
          prop.ShouldSerialize = delegate (object _, object? val) {
            return val is string str && !string.IsNullOrWhiteSpace(str);
          };
        }
      }
    }

    public static void SkipNullProperties(JsonTypeInfo typeInfo) {
      foreach (JsonPropertyInfo prop in typeInfo.Properties) {
        if (typeof(IProperty).IsAssignableFrom(prop.PropertyType)) {
          prop.ShouldSerialize = delegate (object _, object? val) {
            return val is IProperty prop && prop.Value != null;
          };
        }
      }
    }

  }
}
