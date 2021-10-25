using System.Diagnostics;

namespace GenshinAudioExportLib
{
    class WavConverter
    {
        private readonly string ffmpegPath;

        public WavConverter(string ffmpegPath)
        {
            this.ffmpegPath = ffmpegPath;
        }

        public void ConvertWav(string inputFilePath, string outputFilePath, string format)
        {
            Process wavConvertProc;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            switch (format)
            {
                case "mp3":
                    startInfo = new ProcessStartInfo(ffmpegPath)
                    {
                        Arguments = $"-i \"{inputFilePath}\" -y -b:a 320k \"{outputFilePath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    };
                    break;
                case "ogg":
                    startInfo = new ProcessStartInfo(ffmpegPath)
                    {
                        Arguments = $"-i \"{inputFilePath}\" -y -acodec libvorbis -qscale:a 10 \"{outputFilePath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    };
                    break;
                case "flac":
                    startInfo = new ProcessStartInfo(ffmpegPath)
                    {
                        Arguments = $"-i \"{inputFilePath}\" -y -af aformat=s16:44100 \"{outputFilePath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    };
                    break;
            }
            using (wavConvertProc = new Process())
            {
                wavConvertProc.StartInfo = startInfo;
                wavConvertProc.Start();
                wavConvertProc.WaitForExit();
            }
        }
    }
}
