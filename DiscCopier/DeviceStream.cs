using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiscCopier;

public class DeviceStream: Stream, IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagRandomAccess = 0x10000000;

    private const int FileDeviceCdrom = 0x00000002;
    private const int IoctlCdromSetSpeed = 0x0018;
    private const int BufferedMethod = 0;
    private const int ReadAccess = 1;

    private const int CdromRequestSetSpeed = 0;
    private const int CdromDefaultRotation = 0;
    private const int DvdSpeed = 11_080; // 8x DVD

    private struct CdromSetSpeed
    {
        public int RequestType;
        public ushort ReadSpeed;
        public ushort WriteSpeed;
        public int RotationControl;
    }

    private SafeFileHandle? _handleValue;
    private FileStream? _fs;
    private bool _disposed;

    // Use interop to call the CreateFile function.
    // For more information about CreateFile,
    // see the unmanaged MSDN reference library.
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        int dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        ref int lpBytesReturned,
        IntPtr lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        IntPtr hFile,
        byte[] lpBuffer,
        int nNumberOfBytesToRead,
        ref int lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    private static int DeviceIoControlCode(int deviceType, int function, int method, int access)
    {
        return (deviceType << 16) | (access << 14) | (function << 2) | method;
    }
    
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
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagRandomAccess,
            IntPtr.Zero);

        _handleValue = new SafeFileHandle(ptr, true);

        // If the handle is invalid,
        // get the last Win32 error 
        // and throw a Win32Exception.
        if (_handleValue.IsInvalid)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        // Set optical drive speed
        var cdromSetSpeed = new CdromSetSpeed {
            RequestType = CdromRequestSetSpeed,
            ReadSpeed = DvdSpeed,
            WriteSpeed = DvdSpeed,
            RotationControl = CdromDefaultRotation
        };
        var ioctlSize = Marshal.SizeOf<CdromSetSpeed>();
        var ioctlPtr = Marshal.AllocHGlobal(ioctlSize);
        Marshal.StructureToPtr(cdromSetSpeed, ioctlPtr, true);
        
        var bytesReturned = 0;
        var ok = DeviceIoControl(_handleValue.DangerousGetHandle(),
            DeviceIoControlCode(FileDeviceCdrom, IoctlCdromSetSpeed, BufferedMethod, ReadAccess),
            ioctlPtr, ioctlSize,
            IntPtr.Zero, 0,
            ref bytesReturned, IntPtr.Zero);
        Marshal.FreeHGlobal(ioctlPtr);
        if (!ok)
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
            var errorCode = Marshal.GetHRForLastWin32Error();
            Console.WriteLine($"\nhandle:{_handleValue?.DangerousGetHandle()} bufferSize:{count} bytesRead:{bytesRead} errorCode:{errorCode:x8}");
            Marshal.ThrowExceptionForHR(errorCode);
        }

        if (bytesRead > 0)
        {
            Buffer.BlockCopy(bufBytes, 0, buffer, offset, bytesRead);
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
