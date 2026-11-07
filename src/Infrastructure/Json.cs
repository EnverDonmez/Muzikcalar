using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace SadeSes
{
    public static class Json
    {
        public static JavaScriptSerializer New() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 150 }; }
        public static string Write(object value) { return New().Serialize(value); }
        public static Dictionary<string, object> Read(string value) { return New().Deserialize<Dictionary<string, object>>(value.TrimStart('\uFEFF')); }
        public static object Get(Dictionary<string, object> d, string key) { object v; return d != null && d.TryGetValue(key, out v) ? v : null; }
        public static string Str(Dictionary<string, object> d, string key) { return Convert.ToString(Get(d, key), CultureInfo.InvariantCulture); }
        public static double Num(Dictionary<string, object> d, string key) { double v; return double.TryParse(Str(d, key), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0; }
    }
}
