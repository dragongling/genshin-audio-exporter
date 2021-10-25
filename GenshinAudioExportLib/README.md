# Genshin Impact Audio Export Library

A .Net standard 2.0 library for exporting Genshin Impact Audio

## Usage

To use this library you should have a directory that contains `quickbms`, `vgmstream`, `ffmpeg` executables and a `wavescan.bms` script

```cs
// Prepare a directory with everything needed
string LibsDir = "C:\\libs";

// Create GenshinExporter instance
GenshinExporter exporter = new GenshinExporter(LibsDir);

// Set a directory for export output
exporter.OutputDir = "C:\\Genshin audio";

// Set a temp directory path for processing
exporter.ProcessingDir = "C:\\Genshin audio\\processing";

// Optionally set a CancellationToken to cancel export task
exportTokenSource = new CancellationTokenSource();
exporter.CancelToken = exportTokenSource.Token;
exportTokenSource.Cancel(); // use this to asynchronously cancel export task

// Optionally kill hanging processes
exporter.KillProcesses();

// Optionally set current export task progress
exporter.progress = new Progress<int>(value =>
{
    someProgressBar.Value = value;
});

// Export PCK files to WEM files
await exporter.ExportPcksToWem(PckFiles);

// Export WEM files to WAV files
int overallIndex = 0;
int wavCount = await exporter.ExportWemsToWavs(overallIndex);

// Export WAV files to mp3, ogg or flac files
await exporter.ExportAudioFormat("mp3");

```