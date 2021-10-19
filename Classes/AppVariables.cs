﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace genshin_audio_exporter
{
    public static class AppVariables
    {
        private static string pckFileDir = "";
        private static string outputDir = "";
        private static string processingDir = "";
        private static string libsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "libs");
        private static bool overwriteExisting = true;

        private static List<string> pckFiles = new List<string>();
        private static List<string> wemFiles = new List<string>();
        private static List<string> wavFiles = new List<string>();
        public static void UpdateProcessingFolder()
        {
            ProcessingDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "processing");
        }

        public static string PckFileDir { get => pckFileDir; set => pckFileDir = value; }
        public static string OutputDir { get => outputDir; set => outputDir = value; }
        public static string ProcessingDir { get => processingDir; set => processingDir = value; }
        public static string LibsDir { get => libsDir; set => libsDir = value; }
        public static Dictionary<string, bool> ExportFormats = new Dictionary<string, bool> { 
            { "wav", false },
            { "mp3", false },
            { "ogg", false },
            { "flac", false }
        };
        public static bool OverwriteExisting { get => overwriteExisting; set => overwriteExisting = value; }
        public static List<string> PckFiles { get => pckFiles; set => pckFiles = value; }
        public static List<string> WemFiles { get => wemFiles; set => wemFiles = value; }
        public static List<string> WavFiles { get => wavFiles; set => wavFiles = value; }
    }
}
