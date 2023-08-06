using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IsikReg.Json
{
    public class StringOrBoolean
    {

        public class JsonConverter : JsonConverter<StringOrBoolean>
        {
            public override StringOrBoolean? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        return new StringOrBoolean(reader.GetString()!);
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        return new StringOrBoolean(reader.GetBoolean());
                }
                return null;
            }

            public override void Write(Utf8JsonWriter writer, StringOrBoolean stringOrBoolean, JsonSerializerOptions options)
            {
                if (stringOrBoolean.IsString())
                    writer.WriteStringValue(stringOrBoolean.GetString());
                else
                    writer.WriteBooleanValue(stringOrBoolean.GetBoolean());
            }
        }

        private readonly string? str;
        private readonly bool boolean;

        public StringOrBoolean(string str)
        {
            this.str = str;
            boolean = false;
        }

        public StringOrBoolean(bool boolean)
        {
            str = null;
            this.boolean = boolean;
        }

        public bool IsString()
        {
            return str != null;
        }

        public bool IsBoolean()
        {
            return !IsString();
        }

        public string? GetString()
        {
            return str;
        }

        public bool GetBoolean()
        {
            return boolean;
        }
    }
}
