using System.Text.RegularExpressions;

namespace DiscCopier;

public static class FileUtils
{
    private static readonly string IllegalChars = new string(Path.GetInvalidFileNameChars()) + "!";
    private const string IllegalFilenames = "^(?:PRN|AUX|CLOCK\\$|NUL|CON|COM\\d|LPT\\d)$";
    private const int BufferSize = 1048576;
    private const int WriterFlushInterval = 10485760;
    private const long DvdNamePosition = 0x8028;
    private const long DvdNameLength = 32;

    public static DriveInfo? FindDrive(string choice) =>
        DriveInfo.GetDrives().FirstOrDefault(aDrive => MatchesDrive(choice, aDrive));

    private static bool MatchesDrive(string choice, DriveInfo aDrive) =>
        aDrive.DriveType == DriveType.CDRom && (choice == "" || aDrive.Name[0] == choice[0]);

    public static void CopyDeviceToFile(DriveInfo drive, string destPath, bool verbose = false)
    {
        var device = $@"\\.\{drive.Name[0]}:";
        if (verbose)
        {
            Console.WriteLine($"Copying optical disc from device {device} to {destPath}");
        }

        var reader = new BinaryReader(new DeviceStream(device));
        BinaryWriter? writer = null;
        var buffer = new byte[BufferSize];

        var bytesRead = 0L;

        try
        {
            int count;
            while ((count = reader.Read(buffer, 0, BufferSize)) > 0)
            {
                bytesRead += count;
                if (writer == null)
                {
                    var targetFilename = GetTargetFilename(destPath, buffer, count);
                    if (verbose)
                    {
                        Console.WriteLine($"Writing to {targetFilename}");
                    }
                    writer = new BinaryWriter(new FileStream(targetFilename, FileMode.Create));
                }

                writer.Write(buffer, 0, count);
                if (verbose)
                {
                    Console.Write(
                        $"Written {bytesRead} / {drive.TotalSize} bytes ({100 * bytesRead / drive.TotalSize}%)\r");
                }

                if (bytesRead % WriterFlushInterval == 0)
                {
                    writer.Flush();
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            reader.Close();
            writer?.Flush();
            writer?.Close();
            if (verbose)
            {
                Console.WriteLine(
                    $"Written {bytesRead} / {drive.TotalSize} bytes ({100 * bytesRead / drive.TotalSize}%)");
            }
        }
    }

    private static string GetTargetFilename(string destPath, byte[] buffer, int bytesRead)
    {
        var isoNameBytes = new byte[32];
        if (bytesRead >= DvdNamePosition + DvdNameLength)
        {
            Array.Copy(buffer, DvdNamePosition, isoNameBytes, 0, DvdNameLength);
        }
        var isoName = System.Text.Encoding.UTF8.GetString(isoNameBytes).Trim();
        return TargetFilename(destPath, isoName);
    }
    
    private static string TargetFilename(string destPath, string discName, int clash = 0)
    {
        var filename = discName.Trim();
        var regex = new Regex($"[{Regex.Escape(IllegalChars)}]");
        filename = regex.Replace(filename, "");
        if (filename == "" || Regex.IsMatch(filename, IllegalFilenames, RegexOptions.IgnoreCase))
        {
            filename = $"DiscCopier_{DateTime.Now:yyyyMMddHHmmss}";
        }

        var suffix = clash == 0 ? "" : $" ({clash})";
        var targetFilename = $@"{destPath}{filename}{suffix}.iso";
        if (File.Exists(targetFilename) || Directory.Exists(targetFilename))
        {
            targetFilename = TargetFilename(destPath, discName, clash + 1);
        }

        return targetFilename;
    }

}