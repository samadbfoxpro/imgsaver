using System;
using System.IO;
using Newtonsoft.Json;

namespace imgsaver
{
    public class VersionData
    {
        public string Version { get; set; } = "1.6.8";
    }

    public static class VersionManager
    {
        public static string CurrentVersion => "1.7.0";

        public static void Load() { /* No longer needed */ }
        public static void Save() { /* No longer needed */ }
        public static void IncrementPatch() { /* No longer needed */ }
    }
}
