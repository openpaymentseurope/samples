using Newtonsoft.Json;

namespace Shared
{
    public static class StringExtensions
    {
        public static string FormatAsIndentedJson(this string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
    }
}
