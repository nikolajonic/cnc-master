using System.Text;

namespace CNC.Infrastructure.Persistence;

internal static class AtomicFile
{
    /// <summary>
    /// Writes to a temporary file in the same folder, keeps the previous version as <c>.bak</c>,
    /// then swaps the new file into place. A crash mid-write never leaves a truncated file behind.
    /// </summary>
    public static async Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
