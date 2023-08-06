using System.Text.Json.Serialization;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IsikReg.Json {

  public delegate Type TypeDiscriminatorConverter(JsonNode? jsonNode);
  public abstract class DefaultConverterFactory<T> : JsonConverterFactory {

    class NullDefaultConverter : DefaultConverter {
      public override bool HandleNull => true;

      public NullDefaultConverter(DefaultConverterFactory<T> factory, JsonSerializerOptions options) : base(factory, options) {
      }

    }
    class DefaultConverter : JsonConverter<T> {

      private readonly DefaultConverterFactory<T> factory;
      private readonly JsonSerializerOptions modifiedOptions;
      public DefaultConverter(DefaultConverterFactory<T> factory, JsonSerializerOptions options) {
        this.factory = factory;

        // Remove converter for this type
        Type type = factory.GetType();
        modifiedOptions = new(options);
        IList<JsonConverter> converters = modifiedOptions.Converters;
        for (int i = converters.Count - 1; i >= 0; i--) {
          if (converters[i].GetType() == type) {
            converters.RemoveAt(i);
          }
        }
      }

      public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions? options) {
        if (factory.typeConverter != null) {
          Utf8JsonReader typeReader = reader;
          JsonNode? jsonNode = JsonSerializer.Deserialize<JsonNode>(ref typeReader, modifiedOptions);
          typeToConvert = factory.typeConverter(jsonNode);
        }

        return factory.Read(ref reader, typeToConvert, modifiedOptions);
      }

      public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions? options) {
        factory.Write(writer, value, modifiedOptions);
      }

    }



    protected TypeDiscriminatorConverter? typeConverter;

    public virtual bool HandleNull => true;

    protected DefaultConverterFactory() {
    }

    protected DefaultConverterFactory(TypeDiscriminatorConverter? typeConverter) {
      this.typeConverter = typeConverter;
    }

    public virtual T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) {
      return (T?)JsonSerializer.Deserialize(ref reader, typeToConvert, modifiedOptions);
    }
    public virtual void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions modifiedOptions) {
      JsonSerializer.Serialize(writer, (object?)value, modifiedOptions);
    }

    public override bool CanConvert(Type typeToConvert) {
      return typeof(T) == typeToConvert;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
      return HandleNull ? new NullDefaultConverter(this, options) : new DefaultConverter(this, options);
    }

  }

}
