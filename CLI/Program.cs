// See https://aka.ms/new-console-template for more information

using DiscCopier;

var destPath = args.Length > 0 ? args[0] : "";

if (destPath == "")
{
    var destDrive = FileUtils.FindLastFixedDrive();
    if (destDrive == null)
    {
        Console.WriteLine("Cannot find fixed drive");
        Environment.Exit(1);
    }

    destPath = $@"{destDrive.Name[0]}:\";
}

if (!Directory.Exists(destPath))
{
    var reason = File.Exists(destPath) ? "is not a directory" : "does not exist";
    Console.WriteLine($"{destPath} {reason}");
    Environment.Exit(1);
}
if (!destPath.EndsWith(@"\"))
{
    destPath += @"\";
}

var sourceDrive = args.Length > 1 && args[1].Length > 0 ? $"{args[1][0]}:" : "";
var drive = FileUtils.FindFirstOpticalDrive(sourceDrive);

if (drive == null)
{
    Console.WriteLine("Cannot find optical drive{0}", sourceDrive == "" ? "" : $" {sourceDrive}");
    Environment.Exit(1);
}

if (!drive.IsReady)
{
    Console.WriteLine("Optical drive{0} is not ready", sourceDrive == "" ? "" : $" {sourceDrive}");
    Environment.Exit(1);
}

FileUtils.CopyDeviceToFile(drive, destPath, true);
