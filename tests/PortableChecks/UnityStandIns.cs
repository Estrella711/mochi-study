using System;
using System.Text.Encodings.Web;
using System.Text.Json;

// This adapter is only for standalone domain checks, outside Unity's Assets folder.
// System.Text.Json does not reproduce every Unity JsonUtility behavior.
namespace UnityEngine
{
    public static class JsonUtility
    {
        static JsonSerializerOptions Options(bool pretty)
        {
            return new JsonSerializerOptions {
                IncludeFields = true,
                WriteIndented = pretty,
                IgnoreReadOnlyProperties = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        public static T FromJson<T>(string text)
        { return JsonSerializer.Deserialize<T>(text, Options(false)); }

        public static string ToJson(object data, bool pretty = false)
        { return JsonSerializer.Serialize(data, Options(pretty)); }
    }

    public static class Debug
    {
        public static void Log(object value) { Console.WriteLine(value); }
    }
}
