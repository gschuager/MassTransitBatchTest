using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using GreenPipes.Internals.Extensions;
using GreenPipes.Internals.Reflection;
using GreenPipes.Introspection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MassTransitTest
{
    public static class IntrospectionExtensions
    {
        public static string ToJsonString(this ProbeResult result)
        {
            var encoding = new UTF8Encoding(false, true);

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, encoding, 1024, true);
            using var jsonWriter = new JsonTextWriter(writer) {Formatting = Formatting.Indented};

            SerializerCache.Serializer.Serialize(jsonWriter, result, typeof(ProbeResult));

            jsonWriter.Flush();
            writer.Flush();

            return encoding.GetString(stream.ToArray());
        }
    }

    internal static class SerializerCache
    {
        private static readonly Lazy<JsonSerializer> serializer = new Lazy<JsonSerializer>(CreateSerializer, LazyThreadSafetyMode.PublicationOnly);

        public static JsonSerializer Serializer => serializer.Value;

        private static JsonSerializer CreateSerializer()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TypeNameHandling = TypeNameHandling.None,
                DateParseHandling = DateParseHandling.None,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter>(new JsonConverter[]
                {
                    new StringEnumConverter(),
                    new InterfaceProxyConverter(TypeCache.ImplementationBuilder)
                })
            };

            return JsonSerializer.Create(settings);
        }

        private class InterfaceProxyConverter :
            JsonConverter
        {
            private readonly IImplementationBuilder builder;

            public InterfaceProxyConverter(IImplementationBuilder builder)
            {
                this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var proxyType = builder.GetImplementationType(objectType);

                return serializer.Deserialize(reader, proxyType);
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType.GetTypeInfo().IsInterface;
            }
        }
    }
}
