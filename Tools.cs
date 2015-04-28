using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace userScript
{
    public class Tools
    {
        public static string TryGetDictValue(Dictionary<string, string> dictionary, string key)
        {
            string value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        public static string GetJsonValueNullable(JToken json, string key)
        {
            return json[key] == null ? null : json[key].Value<string>();
        }

        public static string GetJsonValue(JToken json, string key)
        {
            var val = GetJsonValueNullable(json, key);
            if (val == null)
            {
                throw new Exception("Failed to obtain key: " + key);
            }
            return val;
        }
    }
}