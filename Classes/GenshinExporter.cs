using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace genshin_audio_exporter.Classes
{
    class GenshinExporter
    {
        private bool isBusy = false;
        private bool isAborted = false;
        public int exportedAudioFiles = 0;
        readonly Logger logger = LogManager.GetCurrentClassLogger();

        public async Task<int> ExportPcksToWem(IProgress<int> progress)
        {
            WriteStatus("Exporting PCK  =>  WEM  (Required)", prefix: false);
            WriteStatus("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string pckFile in AppVariables.PckFiles)
                {
                    if (!isAborted)
                    {
                        PckToWem.StartPckToWem(pckFile);
                        WriteStatus($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                        index += 1;
                        progress.Report(index);
                    }
                    else
                        break;
                }
            });
            return index;
        }

        public async Task ExportWemsToWavs(int overallIndex, IProgress<int> progress)
        {
            WriteStatus("");
            WriteStatus("Exporting WEM  =>  WAV  (Required)", prefix: false);
            WriteStatus("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string wemFile in AppVariables.WemFiles)
                {
                    if (!isAborted)
                    {
                        WemToWav.StartWemToWav(wemFile);
                        WriteStatus($"{Path.GetFileName(wemFile)}  =>  {Path.GetFileNameWithoutExtension(wemFile)}.wav");
                        index += 1;
                        overallIndex += 1;
                        progress.Report(index);
                    }
                    else
                        break;
                }
            });
        }

        public async Task ExportAudioFormat(string format, IProgress<int> progress)
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
                    if (!isAborted)
                    {
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
                    else
                        break;
                }
            });
        }

        public void WriteStatus(string text, bool prefix = true)
        {
            logger.Info($"{((text.Length > 0 && prefix) ? "> " + text : "  " + text)}");
        }
    }
}
