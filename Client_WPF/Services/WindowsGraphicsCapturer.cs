#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiDevice = SharpDX.DXGI.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RemoteDesktopClient.Services
{
    internal sealed class WindowsGraphicsCapturer : IDisposable
    {
        [DllImport("d3d11.dll")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("combase.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("combase.dll", ExactSpelling = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int RoGetActivationFactory(
            IntPtr hstrRuntimeClass,
            ref Guid iid,
            out IntPtr factory);

        private const uint MonitorDefaultToPrimary = 1;
        private const string GraphicsCaptureItemRuntimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";

        private static readonly Guid GraphicsCaptureItemInteropId =
            new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        private static readonly Guid Direct3DDxgiInterfaceAccessId =
            new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

        private readonly D3DDevice _device;
        private readonly IDirect3DDevice _winRtDevice;
        private readonly GraphicsCaptureItem _captureItem;
        private readonly Direct3D11CaptureFramePool _framePool;
        private readonly GraphicsCaptureSession _captureSession;
        private Texture2D? _stagingTexture;
        private int _stagingWidth;
        private int _stagingHeight;

        public WindowsGraphicsCapturer()
        {
            if (!GraphicsCaptureSession.IsSupported())
                throw new PlatformNotSupportedException("Windows Graphics Capture không được hỗ trợ.");

            _device = new D3DDevice(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _winRtDevice = CreateWinRtDevice(_device);
            _captureItem = CreatePrimaryMonitorItem();
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winRtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureItem.Size);
            _captureSession = _framePool.CreateCaptureSession(_captureItem);
            _captureSession.IsCursorCaptureEnabled = true;
            try
            {
                var prop = _captureSession.GetType().GetProperty("IsBorderRequired");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(_captureSession, false);
                }
            }
            catch { }
            _captureSession.StartCapture();
        }

        public Bitmap? TryGetFrame(int targetWidth, int targetHeight)
        {
            using Direct3D11CaptureFrame? frame = _framePool.TryGetNextFrame();
            if (frame == null)
                return null;

            int sourceWidth = frame.ContentSize.Width;
            int sourceHeight = frame.ContentSize.Height;
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return null;

            using Texture2D sourceTexture = GetTexture(frame.Surface);
            EnsureStagingTexture(sourceWidth, sourceHeight);
            _device.ImmediateContext.CopyResource(sourceTexture, _stagingTexture);

            DataBox mapped = _device.ImmediateContext.MapSubresource(
                _stagingTexture,
                0,
                MapMode.Read,
                MapFlags.None);

            try
            {
                return ScaleBgraNearest(
                    mapped.DataPointer,
                    mapped.RowPitch,
                    sourceWidth,
                    sourceHeight,
                    targetWidth,
                    targetHeight);
            }
            finally
            {
                _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
            }
        }

        private void EnsureStagingTexture(int width, int height)
        {
            if (_stagingTexture != null && _stagingWidth == width && _stagingHeight == height)
                return;

            _stagingTexture?.Dispose();
            _stagingTexture = new Texture2D(
                _device,
                new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CpuAccessFlags = CpuAccessFlags.Read,
                    OptionFlags = ResourceOptionFlags.None
                });
            _stagingWidth = width;
            _stagingHeight = height;
        }

        private static unsafe Bitmap ScaleBgraNearest(
            IntPtr sourcePointer,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight)
        {
            Bitmap target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            BitmapData targetData = target.LockBits(
                new Rectangle(0, 0, targetWidth, targetHeight),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                byte* sourceBase = (byte*)sourcePointer;
                byte* targetBase = (byte*)targetData.Scan0;

                for (int targetY = 0; targetY < targetHeight; targetY++)
                {
                    int sourceY = targetY * sourceHeight / targetHeight;
                    uint* sourceRow = (uint*)(sourceBase + (sourceY * sourceStride));
                    uint* targetRow = (uint*)(targetBase + (targetY * targetData.Stride));

                    for (int targetX = 0; targetX < targetWidth; targetX++)
                    {
                        int sourceX = targetX * sourceWidth / targetWidth;
                        targetRow[targetX] = sourceRow[sourceX];
                    }
                }
            }
            finally
            {
                target.UnlockBits(targetData);
            }

            return target;
        }

        private static IDirect3DDevice CreateWinRtDevice(D3DDevice device)
        {
            using DxgiDevice dxgiDevice = device.QueryInterface<DxgiDevice>();
            int result = CreateDirect3D11DeviceFromDXGIDevice(
                dxgiDevice.NativePointer,
                out IntPtr inspectablePointer);
            Marshal.ThrowExceptionForHR(result);

            try
            {
                return MarshalInterface<IDirect3DDevice>.FromAbi(inspectablePointer);
            }
            finally
            {
                Marshal.Release(inspectablePointer);
            }
        }

        private static GraphicsCaptureItem CreatePrimaryMonitorItem()
        {
            IntPtr monitor = MonitorFromPoint(new NativePoint(0, 0), MonitorDefaultToPrimary);
            if (monitor == IntPtr.Zero)
                throw new InvalidOperationException("Không tìm thấy màn hình chính.");

            int hr = WindowsCreateString(GraphicsCaptureItemRuntimeClass, GraphicsCaptureItemRuntimeClass.Length, out IntPtr hstring);
            Marshal.ThrowExceptionForHR(hr);

            IntPtr interopPointer = IntPtr.Zero;
            try
            {
                Guid interopId = GraphicsCaptureItemInteropId;
                hr = RoGetActivationFactory(hstring, ref interopId, out interopPointer);
                Marshal.ThrowExceptionForHR(hr);

                IntPtr vtable = Marshal.ReadIntPtr(interopPointer);
                IntPtr methodPointer = Marshal.ReadIntPtr(vtable, IntPtr.Size * 4);
                CreateForMonitorDelegate createForMonitor =
                    Marshal.GetDelegateForFunctionPointer<CreateForMonitorDelegate>(methodPointer);
                Guid itemId = GuidGenerator.GetIID(typeof(GraphicsCaptureItem));

                int result = createForMonitor(
                    interopPointer,
                    monitor,
                    ref itemId,
                    out IntPtr itemPointer);
                Marshal.ThrowExceptionForHR(result);

                try
                {
                    return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
                }
                finally
                {
                    Marshal.Release(itemPointer);
                }
            }
            finally
            {
                if (interopPointer != IntPtr.Zero)
                    Marshal.Release(interopPointer);

                WindowsDeleteString(hstring);
            }
        }

        private static Texture2D GetTexture(IDirect3DSurface surface)
        {
            IntPtr surfacePointer = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
            IntPtr accessPointer = IntPtr.Zero;

            try
            {
                Guid accessId = Direct3DDxgiInterfaceAccessId;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(
                    surfacePointer,
                    ref accessId,
                    out accessPointer));

                IntPtr vtable = Marshal.ReadIntPtr(accessPointer);
                IntPtr methodPointer = Marshal.ReadIntPtr(vtable, IntPtr.Size * 3);
                GetInterfaceDelegate getInterface =
                    Marshal.GetDelegateForFunctionPointer<GetInterfaceDelegate>(methodPointer);
                Guid textureId = typeof(Texture2D).GUID;
                Marshal.ThrowExceptionForHR(getInterface(
                    accessPointer,
                    ref textureId,
                    out IntPtr texturePointer));

                return new Texture2D(texturePointer);
            }
            finally
            {
                if (accessPointer != IntPtr.Zero)
                    Marshal.Release(accessPointer);
                MarshalInterface<IDirect3DSurface>.DisposeAbi(surfacePointer);
            }
        }

        public void Dispose()
        {
            _captureSession.Dispose();
            _framePool.Dispose();
            _stagingTexture?.Dispose();
            (_winRtDevice as IDisposable)?.Dispose();
            _device.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct NativePoint
        {
            public readonly int X;
            public readonly int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateForMonitorDelegate(
            IntPtr thisPointer,
            IntPtr monitor,
            ref Guid itemId,
            out IntPtr itemPointer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetInterfaceDelegate(
            IntPtr thisPointer,
            ref Guid interfaceId,
            out IntPtr interfacePointer);
    }
}
