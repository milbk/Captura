using System;
using System.Drawing;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace DesktopDuplication.Port
{
    public class PointerInfo
    {
        public IntPtr PtrShapeBuffer { get; set; }
        public OutputDuplicatePointerShapeInformation ShapeInfo { get; set; }
        public Point Position { get; set; }
        public bool Visible { get; set; }
        public int BufferSize { get; set; }
        public int WhoUpdatedPositionLast { get; set; }
        public long LastTimeStamp { get; set; }
    }

    public class DxResources
    {
        public Device Device { get; set; }
        public VertexShader VertexShader { get; set; }
        public PixelShader PixelShader { get; set; }
        public InputLayout InputLayout { get; set; }
        public SamplerState SamplerLinear { get; set; }
    }

    public class ThreadData
    {
        public event Action UnexpectedError;
        public event Action ExpectedError;
        public event Action TerminateThreads;

        public IntPtr TexSharedHandle { get; set; }
        public int Output { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public PointerInfo PointerInfo { get; set; }
        public DxResources DxResources { get; set; }
    }

    public class FrameData
    {
        public Texture2D Frame { get; set; }
        public OutputDuplicateFrameInformation FrameInfo { get; set; }
        public OutputDuplicateMoveRectangle[] MoveRects { get; set; }
        public RawRectangle[] DirtyRects { get; set; }
        public int DirtyCount { get; set; }
        public int MoveCount { get; set; }
    }

    public class Vertex
    {
        public RawVector3 Pos { get; set; }
        public RawVector2 TexCoord { get; set; }
    }

    public enum DuplReturn
    {
        Success,
        Expected,
        Unexpected
    }
}