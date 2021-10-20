using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace genshin_audio_exporter.Classes
{
    class GenshinExporter
    {
        public int exportedAudioFiles = 0;
        readonly Logger logger = LogManager.GetCurrentClassLogger();

        public async Task<int> ExportPcksToWem(IProgress<int> progress, CancellationToken? ct = null)
        {
            WriteStatus("Exporting PCK  =>  WEM  (Required)", prefix: false);
            WriteStatus("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string pckFile in AppVariables.PckFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    PckToWem.StartPckToWem(pckFile);
                    WriteStatus($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                    index += 1;
                    progress?.Report(index);
                }
            });
            return index;
        }

        public async Task ExportWemsToWavs(int overallIndex, IProgress<int> progress, CancellationToken? ct = null)
        {
            WriteStatus("");
            WriteStatus("Exporting WEM  =>  WAV  (Required)", prefix: false);
            WriteStatus("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string wemFile in AppVariables.WemFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    WemToWav.StartWemToWav(wemFile);
                    WriteStatus($"{Path.GetFileName(wemFile)}  =>  {Path.GetFileNameWithoutExtension(wemFile)}.wav");
                    index += 1;
                    overallIndex += 1;
                    progress.Report(index);
                }
            });
        }

        public async Task ExportAudioFormat(string format, IProgress<int> progress, CancellationToken? ct)
        {
            Directory.CreateDirectory(Path.Combine(AppVariables.OutputDir, format));
            int index = 0;

            WriteStatus("");
            if (format == "wav")
                WriteStatus("Copying WAV Files to destination directory", prefix: false);
            else
                WriteStatus($"Exporting WAV  =>  {format.ToUpper()}", prefix: false);
            WriteStatus("");

            await Task.Run(() =>
            {
                foreach (string wavFile in AppVariables.WavFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    if (format != "wav")
                        WavConverter.ConvertWav(wavFile, format);
                    string srcFile = Path.Combine(AppVariables.ProcessingDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");
                    string destFile = Path.Combine(AppVariables.OutputDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");

                    File.Copy(srcFile, destFile, true);
                    WriteStatus($"{Path.GetFileName(wavFile)}  =>  {Path.GetFileName(srcFile)}");
                    exportedAudioFiles += 1;
                    index += 1;
                    progress.Report(index);
                }
            });
        }

        public void WriteStatus(string text, bool prefix = true)
        {
            logger.Info($"{((text.Length > 0 && prefix) ? "> " + text : "  " + text)}");
        }
    }
}
