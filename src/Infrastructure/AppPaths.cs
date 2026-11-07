using System;
using System.IO;

namespace SadeSes
{
    static class AppPaths
    {
        public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string Assets = Path.Combine(Root, "assets");
        public static readonly string Data = Path.Combine(Root, "data");
        public static readonly string Cache = Path.Combine(Data, "cache");
        public static readonly string Fixtures = Path.Combine(Root, "tests", "fixtures");
    }
}
