using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiscCopier;

public class DeviceStream: Stream, IDisposable
{
    // public const short FILE_ATTRIBUTE_NORMAL = 0x80;
    // public const short INVALID_HANDLE_VALUE = -1;
    private const uint GenericRead = 0x80000000;
    // public const uint GENERIC_WRITE = 0x40000000;
    // public const uint CREATE_NEW = 1;
    // public const uint CREATE_ALWAYS = 2;
    private const uint OpenExisting = 3;

    private SafeFileHandle? _handleValue = null;
    private FileStream? _fs = null;
    private bool _disposed = false;

    // Use interop to call the CreateFile function.
    // For more information about CreateFile,
    // see the unmanaged MSDN reference library.
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        IntPtr hFile,                        // handle to file
        byte[] lpBuffer,                // data buffer
        int nNumberOfBytesToRead,        // number of bytes to read
        ref int lpNumberOfBytesRead,    // number of bytes read
        IntPtr lpOverlapped
        //
        // ref OVERLAPPED lpOverlapped        // overlapped buffer
    );

    public DeviceStream(string device)
    {
        Load(device);
    }

    private void Load(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Try to open the file.
        var ptr = CreateFile(path,
            GenericRead,
            0,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        _handleValue = new SafeFileHandle(ptr, true);

        // If the handle is invalid,
        // get the last Win32 error 
        // and throw a Win32Exception.
        if (_handleValue.IsInvalid)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        _fs = new FileStream(_handleValue, FileAccess.Read);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush()
    {
    }

    public override long Length => -1;

    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
    
    /// <summary>
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and 
    /// (offset + count - 1) replaced by the bytes read from the current source. </param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. </param>
    /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
    /// <returns></returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;
        var bufBytes = new byte[count];
        if (_handleValue == null
            || !ReadFile(_handleValue.DangerousGetHandle(),
                bufBytes,
                count,
                ref bytesRead,
                IntPtr.Zero))
        {
            Console.WriteLine($"handle:{_handleValue?.DangerousGetHandle()} bufferSize:{count} bytesRead:{bytesRead}");
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
        for (var i = 0; i < bytesRead; i++)
        {
            buffer[offset + i] = bufBytes[i];
        }
        return bytesRead;
    }
    public override int ReadByte()
    {
        var bytesRead = 0;
        var lpBuffer = new byte[1];
        if (_handleValue == null
            || !ReadFile(_handleValue.DangerousGetHandle(), // handle to file
                lpBuffer, // data buffer
                1, // number of bytes to read
                ref bytesRead, // number of bytes read
                IntPtr.Zero))
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
        return lpBuffer[0];
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void Close()
    {
        if (_handleValue != null)
        {
            _handleValue.Close();
            _handleValue.Dispose();
        }
        _handleValue = null;
        base.Close();
    }

    public new void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private new void Dispose(bool disposing)
    {
        // Check to see if Dispose has already been called.
        if (_disposed) return;
        if (disposing)
        {
            _fs?.Dispose();
            _handleValue?.Close();
            _handleValue?.Dispose();
            _handleValue = null;
        }
        // Note disposing has been done.
        _disposed = true;
    }

}
