using System;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Windows.Markup;

namespace IsikReg.Configuration.Columns {

  public class CustomLanguage : CultureInfo {
    public CustomLanguage(string format)
        : base(CultureInfo.InvariantCulture.LCID, true) {
      DateTimeFormat = new() {
        //your format
        LongDatePattern = format,
        ShortDatePattern = format,
      };

      // get internal ctor and create XmlLanguage 
      var mi = typeof(XmlLanguage).GetConstructor(
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
          null, new Type[] { typeof(string) }, null);
      if (mi != null) {
        Language = (XmlLanguage)mi.Invoke(new[] { "" });

        // set our culture with our format
        var cu_fi = Language.GetType().GetField("_specificCulture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (cu_fi != null) {
          cu_fi.SetValue(Language, this);
        }
      }
    }
    public XmlLanguage? Language { get; private set; }

  }

  public class DateForm {

    public bool Editable { get; init; } = true;

  }

  public class DateColumn : Column {



    public override ColumnType Type { get; } = ColumnType.DATE;

    public string DateFormat { get; init; } = "dd.MM.yyyy";

    [JsonIgnore]
    public XmlLanguage? DateFormatLanguage { get; private set; } = null;

    public DateForm? Form { get; init; } = new();

    public DateColumn() {
    }

    public override void Init() {
      base.Init();

      CustomLanguage customLanguage = new(DateFormat);
      DateFormatLanguage = customLanguage.Language;
    }

    public override bool HasForm() {
      return Form != null; ;
    }
    public override Type GetValueType() {
      return typeof(DateTime);
    }

  }
}
