using System;

namespace imgsaver
{
    public static class PromptTaggerStore
    {
        public static string Template { get; set; } = "";
        public static string Values { get; set; } = "";
        public static string Prefix { get; set; } = "PH_";
    }
}
