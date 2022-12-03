using System.IO;
using SevenZipExtractor;

namespace genshin_audio_exporter
{
    public static class AppResources
    {
        public static bool IsUnpacked { get; set; }

        public static void UnpackResources()
        {
            if (!IsUnpacked)
            {
                var zipPath = Path.Combine(Program.GetAppLocation(), "libs.zip");
                var unzipPath = Path.Combine(Program.GetAppLocation(), "7z.dll");
                File.WriteAllBytes(zipPath, Properties.Resources.libs);
                File.WriteAllBytes(unzipPath, Properties.Resources.svnzip);

                using (var archiveFile = new ArchiveFile(zipPath, unzipPath))
                {
                    archiveFile.Extract(Path.GetDirectoryName(zipPath));
                }
                File.Delete(zipPath);
                File.Delete(unzipPath);
            }
            IsUnpacked = true;
        }
    }
}