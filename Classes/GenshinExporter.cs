using NLog;
using System;
using System.IO;
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
            logger.Info("Exporting PCK  =>  WEM  (Required)");
            logger.Info("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string pckFile in AppVariables.PckFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    PckToWem.StartPckToWem(pckFile);
                    logger.Debug($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                    index += 1;
                    progress?.Report(index);
                }
            });
            return index;
        }

        public async Task ExportWemsToWavs(int overallIndex, IProgress<int> progress, CancellationToken? ct = null)
        {
            logger.Info("");
            logger.Info("Exporting WEM  =>  WAV  (Required)");
            logger.Info("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string wemFile in AppVariables.WemFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    WemToWav.StartWemToWav(wemFile);
                    logger.Debug($"{Path.GetFileName(wemFile)}  =>  {Path.GetFileNameWithoutExtension(wemFile)}.wav");
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

            logger.Info("");
            if (format == "wav")
                logger.Info("Copying WAV Files to destination directory");
            else
                logger.Info($"Exporting WAV  =>  {format.ToUpper()}");
            logger.Info("");

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
                    logger.Debug($"{Path.GetFileName(wavFile)}  =>  {Path.GetFileName(srcFile)}");
                    exportedAudioFiles += 1;
                    index += 1;
                    progress.Report(index);
                }
            });
        }
    }
}
