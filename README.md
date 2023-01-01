# DiscCopier

Simple utility to copy the contents of an optical disc to a file using the Win32 API.

## Compatibility

Because this uses the Win32 API, it will only compile and run on Windows operating systems. At the time of writing, it has only been tested on
Windows 10.

## How to run

```shell
dotnet run --project CLI [target-folder [source-drive-letter]]
```

### Behaviour

This program reads the disk in the drive denoted by `source-drive-letter`, and creates an ISO image of the disc in the folder `target-folder`.

The default `target-folder` is the root folder of the last fixed disk advertised by the operating system.

The default `source-drive-letter` is that of the first optical drive advertised by the operating system.

The target filename is derived from the disc label, with illegal characters removed.

If the target filename would clash with an existing file in the target folder, the program modifies the target filename to avoid a clash,
so (for instance) `ELFEN_LIED_VOL_4.iso` might become `ELFEN_LIED_VOL_4 (1).iso`.

If the target filename would be blank, then the program generates a timestamp-based filename (e.g. `DiscCopier_20230101124734.iso`).

### Limitations

This program may not be able to read some discs containing copy protection features.
