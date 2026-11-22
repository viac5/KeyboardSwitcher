using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Avalonia.Threading;
using KeyboardSwitcher.Domain;
using SharpHook;
using SharpHook.Native;
using TextCopy;

namespace KeyboardSwitcher.Infrastructure.Clipboard;

internal sealed class ClipboardSelectedTextService : ISelectedTextService
{
    private readonly EventSimulator _simulator = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<Avalonia.Input.Platform.IClipboard?> _clipboardProvider;

    public ClipboardSelectedTextService(Func<Avalonia.Input.Platform.IClipboard?> clipboardProvider)
    {
        _clipboardProvider = clipboardProvider;
    }

    public async Task TransformSelectionAsync(TextAction action, ITextTransformer transformer)
    {
        await _gate.WaitAsync();
        string? previousClipboard = null;
        var clipboardCaptured = false;
        var selectionMarker = $"__KeyboardSwitcherSelection_{Guid.NewGuid():N}__";

        try
        {
            previousClipboard = await GetClipboardTextAsync();
            clipboardCaptured = true;
            await SetClipboardTextAsync(selectionMarker);

            SimulateCopy();
            await Task.Delay(OperatingSystem.IsLinux() ? 120 : 80);

            var selectedText = await GetClipboardTextAsync();
            if (!string.IsNullOrEmpty(selectedText) && selectedText != selectionMarker)
            {
                await SetClipboardTextAsync(transformer.Transform(selectedText, action));
                await Task.Delay(60);
                SimulatePaste();
                Console.Error.WriteLine($"KeyboardSwitcher: transformed selected text ({action}).");
                await Task.Delay(OperatingSystem.IsLinux() ? 180 : 80);
            }
            else
            {
                Console.Error.WriteLine("KeyboardSwitcher: no selected text was copied; clipboard still contains the marker.");
            }
        }
        finally
        {
            if (clipboardCaptured)
            {
                await SetClipboardTextAsync(previousClipboard ?? string.Empty);
            }

            _gate.Release();
        }
    }

    private Task<string?> GetClipboardTextAsync()
    {
        if (!OperatingSystem.IsLinux())
            return ClipboardService.GetTextAsync();

        var clipboard = _clipboardProvider()
            ?? throw new InvalidOperationException("Avalonia clipboard is not available yet.");
        return Dispatcher.UIThread.InvokeAsync(
            () => Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard));
    }

    private Task SetClipboardTextAsync(string text)
    {
        if (!OperatingSystem.IsLinux())
            return ClipboardService.SetTextAsync(text);

        var clipboard = _clipboardProvider()
            ?? throw new InvalidOperationException("Avalonia clipboard is not available yet.");
        return Dispatcher.UIThread.InvokeAsync(() => clipboard.SetTextAsync(text));
    }

    private void SimulateCopy()
    {
        if (OperatingSystem.IsLinux())
        {
            SimulateLinuxChord(29, 46); // Ctrl+C
        }
        else
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
            _simulator.SimulateKeyPress(KeyCode.VcC);
            _simulator.SimulateKeyRelease(KeyCode.VcC);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
        }
    }

    private void SimulatePaste()
    {
        if (OperatingSystem.IsLinux())
        {
            SimulateLinuxChord(29, 47); // Ctrl+V
        }
        else
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
            _simulator.SimulateKeyPress(KeyCode.VcV);
            _simulator.SimulateKeyRelease(KeyCode.VcV);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
        }
    }

    [SupportedOSPlatform("linux")]
    private static void SimulateLinuxChord(ushort modifier, ushort key)
    {
        try
        {
            using var file = new FileStream("/dev/uinput", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            EnsureIoctl(file.SafeFileHandle, UI_SET_EVBIT, EV_KEY, "UI_SET_EVBIT");
            EnsureIoctl(file.SafeFileHandle, UI_SET_KEYBIT, modifier, "UI_SET_KEYBIT modifier");
            EnsureIoctl(file.SafeFileHandle, UI_SET_KEYBIT, key, "UI_SET_KEYBIT key");

            var setup = new UinputSetup
            {
                Id = new UinputId { Bustype = 3, Vendor = 0x1234, Product = 0x5678, Version = 1 },
                Name = new byte[80],
                FfEffectsMax = 0
            };
            var name = System.Text.Encoding.UTF8.GetBytes("Keyboard Switcher");
            Array.Copy(name, setup.Name, Math.Min(name.Length, setup.Name.Length - 1));

            var setupPointer = Marshal.AllocHGlobal(Marshal.SizeOf<UinputSetup>());
            try
            {
                Marshal.StructureToPtr(setup, setupPointer, false);
                EnsureIoctl(file.SafeFileHandle, UI_DEV_SETUP, setupPointer, "UI_DEV_SETUP");
            }
            finally
            {
                Marshal.DestroyStructure<UinputSetup>(setupPointer);
                Marshal.FreeHGlobal(setupPointer);
            }

            EnsureIoctl(file.SafeFileHandle, UI_DEV_CREATE, IntPtr.Zero, "UI_DEV_CREATE");

            Thread.Sleep(30);
            Emit(file, EV_KEY, modifier, 1);
            Emit(file, EV_KEY, key, 1);
            Emit(file, EV_SYN, SYN_REPORT, 0);
            Emit(file, EV_KEY, key, 0);
            Emit(file, EV_KEY, modifier, 0);
            Emit(file, EV_SYN, SYN_REPORT, 0);
            Thread.Sleep(30);
            Ioctl(file.SafeFileHandle, UI_DEV_DESTROY, IntPtr.Zero);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"KeyboardSwitcher uinput failed: {exception}");
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UinputId
    {
        public ushort Bustype;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UinputSetup
    {
        public UinputId Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
        public byte[] Name;
        public uint FfEffectsMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        public long Seconds;
        public long Microseconds;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    [SupportedOSPlatform("linux")]
    private static void Emit(FileStream file, ushort type, ushort code, int value)
    {
        var bytes = new byte[Marshal.SizeOf<InputEvent>()];
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.StructureToPtr(new InputEvent { Type = type, Code = code, Value = value }, pointer, false);
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
            file.Write(bytes);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int Ioctl(SafeHandle fd, nuint request, nint argument);

    private const ushort EV_SYN = 0;
    private const ushort EV_KEY = 1;
    private const ushort SYN_REPORT = 0;
    private const nuint UI_SET_EVBIT = 0x40045564;
    private const nuint UI_SET_KEYBIT = 0x40045565;
    private const nuint UI_DEV_SETUP = 0x405c5503;
    private const nuint UI_DEV_CREATE = 0x5501;
    private const nuint UI_DEV_DESTROY = 0x5502;

    private static void EnsureIoctl(SafeHandle file, nuint request, nint argument, string operation)
    {
        if (Ioctl(file, request, argument) != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(error, $"{operation} failed");
        }
    }
}
