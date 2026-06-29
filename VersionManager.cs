using System;
using System.IO;
using Newtonsoft.Json;

namespace imgsaver
{
    public class VersionData
    {
        public string Version { get; set; } = "1.8.2";
    }

    public static class VersionManager
    {
        public static string CurrentVersion => "1.8.2";

        public static void Load() { /* No longer needed */ }
        public static void Save() { /* No longer needed */ }
        public static void IncrementPatch() { /* No longer needed */ }
    }
}
