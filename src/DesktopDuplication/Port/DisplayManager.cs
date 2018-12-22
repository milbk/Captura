using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace DesktopDuplication.Port
{
    public class DisplayManager : IDisposable
    {
        public const int NUMVERTICES = 6;
        public const int BPP = 4;

        public Device Device { get; private set; }

        Texture2D _moveSurface;
        VertexShader _vertexShader;
        PixelShader _pixelShader;
        InputLayout _inputLayout;
        RenderTargetView _renderTargetView;
        SamplerState _samplerLinear;
        Vertex[][] _dirtyVertexBuffer;

        public void InitD3D(DxResources Data)
        {
            Device = Data.Device;
            _vertexShader = Data.VertexShader;
            _pixelShader = Data.PixelShader;
            _inputLayout = Data.InputLayout;
            _samplerLinear = Data.SamplerLinear;
        }

        public void ProcessFrame(FrameData Data,
            Texture2D SharedSurface,
            int OffsetX,
            int OffsetY,
            OutputDescription DeskDesc)
        {
            if (Data.FrameInfo.TotalMetadataBufferSize != 0)
            {
                var desc = Data.Frame.Description;

                if (Data.MoveCount > 0)
                {
                    CopyMove(SharedSurface,
                        Data.MoveRects,
                        Data.MoveCount,
                        OffsetX,
                        OffsetY,
                        DeskDesc,
                        desc.Width,
                        desc.Height);
                }

                if (Data.DirtyCount > 0)
                {
                    CopyDirty(
                        Data.Frame,
                        SharedSurface,
                        Data.DirtyRects,
                        Data.DirtyCount,
                        OffsetX,
                        OffsetY,
                        DeskDesc);
                }
            }
        }

        void SetMoveRect(out RawRectangle SrcRect,
            out RawRectangle DestRect,
            OutputDescription DeskDesc,
            OutputDuplicateMoveRectangle MoveRect,
            int TexWidth,
            int TexHeight)
        {
            var srcX = MoveRect.SourcePoint.X;
            var srcY = MoveRect.SourcePoint.Y;
            var dest = MoveRect.DestinationRect;
            var destW = dest.Right - dest.Left;
            var destH = dest.Bottom - dest.Top;

            switch (DeskDesc.Rotation)
            {
                case DisplayModeRotation.Unspecified:
                case DisplayModeRotation.Identity:
                    SrcRect = new RawRectangle(
                        srcX,
                        srcY,
                        srcX + destW,
                        srcY + destH);

                    DestRect = dest;
                    break;

                case DisplayModeRotation.Rotate90:
                    SrcRect = new RawRectangle(
                        TexHeight - (srcY + destH),
                        srcX,
                        TexHeight - srcY,
                        srcX + destW);

                    DestRect = new RawRectangle(
                        TexHeight - dest.Bottom,
                        dest.Left,
                        TexHeight - dest.Top,
                        dest.Right);
                    break;

                case DisplayModeRotation.Rotate180:
                    SrcRect = new RawRectangle(
                        TexWidth - (srcX + destW),
                        TexHeight - (srcY + destH),
                        TexWidth - srcX,
                        TexHeight - srcY);

                    DestRect = new RawRectangle(
                        TexWidth - dest.Right,
                        TexHeight - dest.Bottom,
                        TexWidth - dest.Left,
                        TexHeight - dest.Top);
                    break;

                case DisplayModeRotation.Rotate270:
                    SrcRect = new RawRectangle(
                        srcX,
                        TexWidth - (srcX + destW),
                        srcY + destH,
                        TexWidth - srcX);

                    DestRect = new RawRectangle(
                        dest.Top,
                        TexWidth - dest.Right,
                        dest.Bottom,
                        TexWidth - dest.Left);
                    break;

                default:
                    SrcRect = new RawRectangle();
                    DestRect = new RawRectangle();
                    break;
            }
        }

        public void Dispose()
        {
            _vertexShader.Dispose();
            _pixelShader.Dispose();
            _renderTargetView.Dispose();
        }

        void CopyMove(Texture2D SharedSurface,
            OutputDuplicateMoveRectangle[] MoveBuffer,
            int MoveCount,
            int OffsetX,
            int OffsetY,
            OutputDescription DeskDesc,
            int TexWidth,
            int TexHeight)
        {
            var fullDesc = SharedSurface.Description;

            if (_moveSurface == null)
            {
                var moveDesc = fullDesc;
                moveDesc.Width = DeskDesc.DesktopBounds.Right - DeskDesc.DesktopBounds.Left;
                moveDesc.Height = DeskDesc.DesktopBounds.Bottom - DeskDesc.DesktopBounds.Top;
                moveDesc.BindFlags = BindFlags.RenderTarget;

                _moveSurface = new Texture2D(Device, moveDesc);
            }

            for (var i = 0; i < MoveCount; ++i)
            {
                SetMoveRect(out var srcRect,
                    out var destRect,
                    DeskDesc,
                    MoveBuffer[i],
                    TexWidth,
                    TexHeight);

                var box = new ResourceRegion(
                    srcRect.Left + DeskDesc.DesktopBounds.Left - OffsetX,
                    srcRect.Top + DeskDesc.DesktopBounds.Top - OffsetY,
                    0,
                    srcRect.Right + DeskDesc.DesktopBounds.Left - OffsetX,
                    srcRect.Bottom + DeskDesc.DesktopBounds.Top - OffsetY,
                    1);

                // Copy rect out of shared surface
                Device.ImmediateContext.CopySubresourceRegion(SharedSurface,
                    0,
                    box,
                    _moveSurface,
                    0,
                    srcRect.Left,
                    srcRect.Top);

                box = new ResourceRegion(
                    srcRect.Left,
                    srcRect.Top,
                    0,
                    srcRect.Right,
                    srcRect.Bottom,
                    1);

                // Copy back to shared surface
                Device.ImmediateContext.CopySubresourceRegion(_moveSurface,
                    0,
                    box,
                    SharedSurface,
                    0,
                    destRect.Left + DeskDesc.DesktopBounds.Left - OffsetX,
                    destRect.Top + DeskDesc.DesktopBounds.Top - OffsetY);
            }
        }

        void SetDirtyVert(Vertex[] Vertices,
            RawRectangle Dirty,
            int OffsetX,
            int OffsetY,
            OutputDescription DeskDesc,
            Texture2DDescription FullDesc,
            Texture2DDescription ThisDesc)
        {
            var centerX = FullDesc.Width / 2f;
            var centerY = FullDesc.Height / 2f;

            var width = DeskDesc.DesktopBounds.Right - DeskDesc.DesktopBounds.Left;
            var height = DeskDesc.DesktopBounds.Bottom - DeskDesc.DesktopBounds.Top;

            var destDirty = Dirty;

            float thisWidthF = ThisDesc.Width, thisHeightF = ThisDesc.Height;

            switch (DeskDesc.Rotation)
            {
                case DisplayModeRotation.Rotate90:
                    destDirty = new RawRectangle(width - Dirty.Bottom,
                        Dirty.Left,
                        width - Dirty.Top,
                        Dirty.Right);

                    Vertices[0].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[1].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[2].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[5].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Top / thisHeightF);
                    break;

                case DisplayModeRotation.Rotate180:
                    destDirty = new RawRectangle(width - Dirty.Right,
                        height - Dirty.Bottom,
                        width - Dirty.Left,
                        height - Dirty.Top);

                    Vertices[0].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[1].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[2].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[5].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Bottom / thisHeightF);
                    break;

                case DisplayModeRotation.Rotate270:
                    destDirty = new RawRectangle(Dirty.Top,
                        height - Dirty.Right,
                        Dirty.Bottom,
                        height - Dirty.Left);

                    Vertices[0].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[1].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[2].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[5].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Bottom / thisHeightF);
                    break;

                case DisplayModeRotation.Unspecified:
                case DisplayModeRotation.Identity:
                    Vertices[0].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[1].TexCoord = new RawVector2(Dirty.Left / thisWidthF, Dirty.Top / thisHeightF);
                    Vertices[2].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Bottom / thisHeightF);
                    Vertices[5].TexCoord = new RawVector2(Dirty.Right / thisWidthF, Dirty.Top / thisHeightF);
                    break;
            }

            Vertices[0].Pos = new RawVector3(
                (destDirty.Left + DeskDesc.DesktopBounds.Left - OffsetX - centerX) / centerX,
                -1 * (destDirty.Bottom + DeskDesc.DesktopBounds.Top - OffsetY - centerY) / centerY,
                0);

            Vertices[1].Pos = new RawVector3(
                (destDirty.Left + DeskDesc.DesktopBounds.Left - OffsetX - centerX) / centerX,
                -1 * (destDirty.Top + DeskDesc.DesktopBounds.Top - OffsetY - centerY) / centerY,
                0);

            Vertices[2].Pos = new RawVector3(
                (destDirty.Right + DeskDesc.DesktopBounds.Left - OffsetX - centerX) / centerX,
                -1 * (destDirty.Bottom + DeskDesc.DesktopBounds.Top - OffsetY - centerY) / centerY,
                0);

            Vertices[3].Pos = Vertices[2].Pos;
            Vertices[4].Pos = Vertices[1].Pos;

            Vertices[0].Pos = new RawVector3(
                (destDirty.Right + DeskDesc.DesktopBounds.Left - OffsetX - centerX) / centerX,
                -1 * (destDirty.Top + DeskDesc.DesktopBounds.Top - OffsetY - centerY) / centerY,
                0);

            Vertices[3].TexCoord = Vertices[2].TexCoord;
            Vertices[4].TexCoord = Vertices[1].TexCoord;
        }

        void CopyDirty(Texture2D SrcSurface,
            Texture2D SharedSurface,
            RawRectangle[] DirtyBuffer,
            int DirtyCount,
            int OffsetX,
            int OffsetY,
            OutputDescription DeskDesc)
        {
            var fullDesc = SharedSurface.Description;
            var thisDesc = SrcSurface.Description;

            if (_renderTargetView == null)
            {
                _renderTargetView = new RenderTargetView(Device, SharedSurface);
            }

            var shaderDesc = new ShaderResourceViewDescription
            {
                Format = thisDesc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D =
                {
                    MostDetailedMip = thisDesc.MipLevels - 1,
                    MipLevels = thisDesc.MipLevels
                }
            };

            var shaderResource = new ShaderResourceView(Device, SrcSurface, shaderDesc);

            Device.ImmediateContext.OutputMerger.SetBlendState(null);
            Device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);
            Device.ImmediateContext.VertexShader.Set(_vertexShader);
            Device.ImmediateContext.PixelShader.Set(_pixelShader);
            Device.ImmediateContext.PixelShader.SetShaderResource(0, shaderResource);
            Device.ImmediateContext.PixelShader.SetSampler(0, _samplerLinear);
            Device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            if (_dirtyVertexBuffer == null || _dirtyVertexBuffer.Length < DirtyCount)
            {
                _dirtyVertexBuffer = new Vertex[DirtyCount][];
            }

            for (var i = 0; i < DirtyCount; ++i)
            {
                if (_dirtyVertexBuffer[i] == null)
                    _dirtyVertexBuffer[i] = new Vertex[NUMVERTICES];

                SetDirtyVert(_dirtyVertexBuffer[i],
                    DirtyBuffer[i],
                    OffsetX, OffsetY,
                    DeskDesc, fullDesc, thisDesc);
            }

            var bytesNeeded = Marshal.SizeOf<Vertex>() * NUMVERTICES * DirtyCount;

            var bufferDesc = new BufferDescription(bytesNeeded,
                ResourceUsage.Default,
                BindFlags.VertexBuffer,
                0, 0, 0);

            var gcPin = GCHandle.Alloc(_dirtyVertexBuffer, GCHandleType.Pinned);

            try
            {
                using (var initData = new DataStream(gcPin.AddrOfPinnedObject(),
                    Marshal.SizeOf(_dirtyVertexBuffer),
                    true, true))
                {
                    using (var vertBuf = new Buffer(Device, initData, bufferDesc))
                    {
                        var stride = Marshal.SizeOf<Vertex>();

                        Device.ImmediateContext.InputAssembler.SetVertexBuffers(0,
                            new VertexBufferBinding(vertBuf, stride, 0));

                        var viewport = new RawViewportF
                        {
                            Width = fullDesc.Width,
                            Height = fullDesc.Height,
                            MinDepth = 0,
                            MaxDepth = 1,
                            X = 0,
                            Y = 0
                        };

                        Device.ImmediateContext.Rasterizer.SetViewport(viewport);

                        Device.ImmediateContext.Draw(NUMVERTICES * DirtyCount, 0);
                    }
                }
            }
            finally
            {
                gcPin.Free();
            }

            shaderResource.Dispose();
        }
    }
}