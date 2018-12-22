using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace DesktopDuplication.Port
{
    public class DuplicationManager : IDisposable
    {
        OutputDuplication _deskDupl;
        Texture2D _acquiredDesktopImage;
        OutputDuplicateMoveRectangle[] _moveBuffer;
        RawRectangle[] _dirtyBuffer;
        int _metadataSize;
        int _outputNumber;

        public OutputDescription OutputDescription { get; private set; }

        public void InitDupl(Device Device, int Output)
        {
            _outputNumber = Output;

            using (var dxgiDevice = Device.QueryInterface<SharpDX.DXGI.Device>())
            {
                var adapter = dxgiDevice.Adapter;
                var output = adapter.Outputs[Output];

                OutputDescription = output.Description;

                using (var output1 = output.QueryInterface<Output1>())
                {
                    _deskDupl = output1.DuplicateOutput(Device);
                }
            }
        }

        public void GetMouse(PointerInfo PointerInfo,
            OutputDuplicateFrameInformation FrameInfo,
            int OffsetX,
            int OffsetY)
        {
            if (FrameInfo.LastMouseUpdateTime == 0)
                return;

            // ReSharper disable once ReplaceWithSingleAssignment.True
            var updatePosition = true;

            // Make sure we don't update pointer position wrongly
            // If pointer is invisible, make sure we did not get an update from another output that the last time that said pointer
            // was visible, if so, don't set it to invisible or update.
            if (!FrameInfo.PointerPosition.Visible && (PointerInfo.WhoUpdatedPositionLast != _outputNumber))
            {
                updatePosition = false;
            }

            // If two outputs both say they have a visible, only update if new update has newer timestamp
            if (FrameInfo.PointerPosition.Visible && PointerInfo.Visible && (PointerInfo.WhoUpdatedPositionLast != _outputNumber) && (PointerInfo.LastTimeStamp > FrameInfo.LastMouseUpdateTime))
            {
                updatePosition = false;
            }

            // Update position
            if (updatePosition)
            {
                PointerInfo.Position = new Point(FrameInfo.PointerPosition.Position.X + OutputDescription.DesktopBounds.Left - OffsetX,
                    FrameInfo.PointerPosition.Position.Y + OutputDescription.DesktopBounds.Top - OffsetY);
                PointerInfo.WhoUpdatedPositionLast = _outputNumber;
                PointerInfo.LastTimeStamp = FrameInfo.LastMouseUpdateTime;
                PointerInfo.Visible = FrameInfo.PointerPosition.Visible;
            }

            if (FrameInfo.PointerShapeBufferSize == 0)
                return;

            // Old buffer too small
            if (FrameInfo.PointerShapeBufferSize > PointerInfo.BufferSize)
            {
                if (PointerInfo.PtrShapeBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(PointerInfo.PtrShapeBuffer);
                    PointerInfo.PtrShapeBuffer = IntPtr.Zero;
                }

                PointerInfo.PtrShapeBuffer = Marshal.AllocHGlobal(FrameInfo.PointerShapeBufferSize);
                PointerInfo.BufferSize = FrameInfo.PointerShapeBufferSize;
            }

            // Get shape
            _deskDupl.GetFramePointerShape(FrameInfo.PointerShapeBufferSize,
                PointerInfo.PtrShapeBuffer,
                out var bufferSizeRequired,
                out var shapeInfo);

            PointerInfo.ShapeInfo = shapeInfo;
        }

        public void GetFrame(FrameData Data, out bool Timeout)
        {
            var result = _deskDupl.TryAcquireNextFrame(500, out var frameInfo, out var desktopResource);

            if (result == Result.WaitTimeout)
            {
                Timeout = true;
                return;
            }

            Timeout = false;

            if (result.Failure)
            {
                throw new Exception("Failed to acquire frame");
            }

            if (_acquiredDesktopImage != null)
            {
                _acquiredDesktopImage.Dispose();
                _acquiredDesktopImage = null;
            }

            using (desktopResource)
            {
                _acquiredDesktopImage = desktopResource.QueryInterface<Texture2D>();
            }

            if (frameInfo.TotalMetadataBufferSize > 0)
            {
                var sizeOfMoveRect = Marshal.SizeOf<OutputDuplicateMoveRectangle>();
                var sizeOfDirtyRect = Marshal.SizeOf<RawRectangle>();

                // Old buffer too small
                if (frameInfo.TotalMetadataBufferSize > _metadataSize)
                {
                    var moveBufferSize = frameInfo.TotalMetadataBufferSize / sizeOfMoveRect;
                    var dirtyBufferSize = frameInfo.TotalMetadataBufferSize / sizeOfDirtyRect;

                    _moveBuffer = new OutputDuplicateMoveRectangle[moveBufferSize];
                    _dirtyBuffer = new RawRectangle[dirtyBufferSize];
                    _metadataSize = frameInfo.TotalMetadataBufferSize;
                }

                _deskDupl.GetFrameMoveRects(frameInfo.TotalMetadataBufferSize, _moveBuffer, out var bufferSizeRequired);

                Data.MoveCount = bufferSizeRequired / sizeOfMoveRect;
                Data.MoveRects = _moveBuffer;

                _deskDupl.GetFrameDirtyRects(frameInfo.TotalMetadataBufferSize, _dirtyBuffer, out bufferSizeRequired);

                Data.DirtyCount = bufferSizeRequired / sizeOfDirtyRect;
                Data.DirtyRects = _dirtyBuffer;
            }
        }

        public void DoneWithFrame()
        {
            _deskDupl.ReleaseFrame();

            _acquiredDesktopImage.Dispose();

            _acquiredDesktopImage = null;
        }

        public void Dispose()
        {
            _deskDupl.Dispose();
        }
    }
}