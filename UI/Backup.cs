using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace GitHubStats;

/// <summary>Backup = one zip with config.json and the data folder (stats, stars, traffic, ui settings).</summary>
internal static class Backup
{
    /// <summary>Writes the backup. Returns an error message or null.</summary>
    public static string? Create(string zipPath, string home, string dataDir)
    {
        try
        {
            string tmp = zipPath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Add(zip, Path.Combine(home, "config.json"), "config.json");
                if (Directory.Exists(dataDir))
                {
                    foreach (var f in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
                    {
                        if (f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                        Add(zip, f, "data/" + Path.GetRelativePath(dataDir, f).Replace('\\', '/'));
                    }
                }
            }
            File.Move(tmp, zipPath, true);
            return null;
        }
        catch (Exception ex) { return "The backup could not be made: " + ex.Message; }
    }

    private static void Add(ZipArchive zip, string file, string name)
    {
        if (!File.Exists(file)) return;
        // the program may be writing a file at this moment: open it shared
        using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        e.LastWriteTime = File.GetLastWriteTime(file);
        using var dst = e.Open();
        src.CopyTo(dst);
    }

    /// <summary>True when the zip looks like a GitHub Stats backup.</summary>
    public static bool IsBackup(string zipPath, out string? error)
    {
        error = null;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            if (zip.Entries.Any(e => e.FullName == "config.json" || e.FullName.StartsWith("data/", StringComparison.Ordinal))) return true;
            error = "This zip file is not a GitHub Stats backup (no config.json or data folder in it).";
        }
        catch (Exception ex) { error = "The file could not be opened: " + ex.Message; }
        return false;
    }

    /// <summary>
    /// Puts a backup in place (called at start, before anything is loaded). The current files are first saved
    /// as backup-before-restore-*.zip next to the program, so a restore can always be undone. Returns a note for the user.
    /// </summary>
    public static string Restore(string zipPath, string home, string dataDir)
    {
        try
        {
            string safety = Path.Combine(home, "backup-before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            string? err = Create(safety, home, dataDir);
            if (err != null) return "The backup was NOT restored, because the current data could not be saved first. " + err;

            string homeFull = Path.GetFullPath(home) + Path.DirectorySeparatorChar;
            string dataFull = Path.GetFullPath(dataDir) + Path.DirectorySeparatorChar;
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;
                string target;
                if (e.FullName == "config.json") target = Path.GetFullPath(Path.Combine(home, "config.json"));
                else if (e.FullName.StartsWith("data/", StringComparison.Ordinal)) target = Path.GetFullPath(Path.Combine(dataDir, e.FullName[5..]));
                else continue;
                // never write outside the program / data folder
                if (!target.StartsWith(homeFull, StringComparison.OrdinalIgnoreCase) && !target.StartsWith(dataFull, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                e.ExtractToFile(target, true);
            }
            return "Backup restored from " + Path.GetFileName(zipPath) + ". Your previous data was saved as " + Path.GetFileName(safety) + ".";
        }
        catch (Exception ex) { return "Restoring the backup failed: " + ex.Message; }
    }
}
