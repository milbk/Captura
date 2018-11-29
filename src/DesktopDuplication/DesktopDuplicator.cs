// Adapted from https://github.com/jasonpang/desktop-duplication-net

using Screna;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Captura;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace DesktopDuplication
{
    public class DesktopDuplicator : IDisposable
    {
        #region Fields
        readonly Device _device;
        readonly OutputDuplication _deskDupl;

        readonly Texture2D _desktopImageTexture;
        OutputDuplicateFrameInformation _frameInfo;

        readonly Rectangle _rect;

        readonly bool _includeCursor;
        #endregion

        int Timeout { get; } = 5000;

        readonly ImagePool _imagePool;

        public DesktopDuplicator(Rectangle Rect, bool IncludeCursor, Adapter Adapter, Output1 Output)
        {
            _rect = Rect;
            _includeCursor = IncludeCursor;

            _imagePool = new ImagePool(Rect.Width, Rect.Height);
            
            _device = new Device(Adapter);

            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = _rect.Width,
                Height = _rect.Height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };

            try
            {
                _deskDupl = Output.DuplicateOutput(_device);
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.NotCurrentlyAvailable)
            {
                throw new Exception("There is already the maximum number of applications using the Desktop Duplication API running, please close one of the applications and try again.", e);
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.Unsupported)
            {
                throw new NotSupportedException("Desktop Duplication is not supported on this system.\nIf you have multiple graphic cards, try running Captura on integrated graphics.", e);
            }

            _desktopImageTexture = new Texture2D(_device, textureDesc);
        }

        SharpDX.DXGI.Resource _desktopResource;

        Task _acquireTask;

        void BeginAcquireTask()
        {
            _acquireTask = Task.Run(() => _deskDupl.AcquireNextFrame(Timeout, out _frameInfo, out _desktopResource));
        }

        public IBitmapFrame Capture()
        {
            if (_acquireTask == null)
            {
                BeginAcquireTask();

                return RepeatFrame.Instance;
            }

            try
            {
                _acquireTask.GetAwaiter().GetResult();
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.WaitTimeout)
            {
                return RepeatFrame.Instance;
            }
            catch (SharpDXException e) when (e.ResultCode.Failure)
            {
                throw new Exception("Failed to acquire next frame.", e);
            }
            
            using (_desktopResource)
            {
                using (var tempTexture = _desktopResource.QueryInterface<Texture2D>())
                {
                    var resourceRegion = new ResourceRegion(_rect.Left, _rect.Top, 0, _rect.Right, _rect.Bottom, 1);

                    _device.ImmediateContext.CopySubresourceRegion(tempTexture, 0, resourceRegion, _desktopImageTexture, 0);
                }
            }

            ReleaseFrame();
            BeginAcquireTask();

            var mapSource = _device.ImmediateContext.MapSubresource(_desktopImageTexture, 0, MapMode.Read, MapFlags.None);

            try
            {
                return ProcessFrame(mapSource.DataPointer);
            }
            finally
            {
                _device.ImmediateContext.UnmapSubresource(_desktopImageTexture, 0);
            }
        }

        IBitmapFrame ProcessFrame(IntPtr SourcePtr)
        {
            var img = _imagePool.Get();
            
            Marshal.Copy(SourcePtr, img.ImageData, 0, _rect.Width * _rect.Height * 4);

            //if (_includeCursor && (_frameInfo.LastMouseUpdateTime == 0 || _frameInfo.PointerPosition.Visible))
            //{
            //    using (var g = Graphics.FromImage(frame))
            //        MouseCursor.Draw(g, P => new Point(P.X - _rect.X, P.Y - _rect.Y));
            //}

            return img;
        }
        
        void ReleaseFrame()
        {
            try
            {
                _deskDupl.ReleaseFrame();
            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Failure)
                {
                    throw new Exception("Failed to release frame.", e);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _deskDupl?.Dispose();
                _desktopImageTexture?.Dispose();
                _device?.Dispose();
            }
            catch { }
        }
    }
}
