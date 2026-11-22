using System.Diagnostics;

namespace KeyboardSwitcher.Infrastructure.Hotkeys;

internal sealed class LinuxEvdevKeyboardHook
{
    private const ushort EventKey = 1;
    private readonly HashSet<string> devicePaths = [];

    public event Action<ushort, bool>? KeyChanged;

    public void Start()
    {
        var paths = Directory.EnumerateFiles("/dev/input", "event*").Order().ToArray();
        Console.Error.WriteLine($"KeyboardSwitcher: starting Linux input listener ({paths.Length} devices).");

        foreach (var path in paths)
        {
            lock (devicePaths)
            {
                if (!devicePaths.Add(path))
                    continue;
            }

            var thread = new Thread(() => ReadDevice(path))
            {
                IsBackground = true,
                Name = $"KeyboardSwitcher {Path.GetFileName(path)}"
            };
            thread.Start();
        }
    }

    private void ReadDevice(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 24);
            var buffer = new byte[24];

            while (ReadEvent(stream, buffer))
            {
                var type = BitConverter.ToUInt16(buffer, 16);
                var code = BitConverter.ToUInt16(buffer, 18);
                var value = BitConverter.ToInt32(buffer, 20);

                if (type == EventKey && code < 256 && value is 0 or 1)
                    KeyChanged?.Invoke(code, value == 1);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"KeyboardSwitcher: cannot read {path}: {exception.Message}");
        }
    }

    private static bool ReadEvent(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var bytesRead = stream.Read(buffer, offset, buffer.Length - offset);
            if (bytesRead == 0)
                return false;
            offset += bytesRead;
        }

        return true;
    }
}
