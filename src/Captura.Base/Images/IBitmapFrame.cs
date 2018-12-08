using System;
using System.IO;

namespace Captura
{
    public interface IBitmapFrame : IDisposable
    {
        void SaveGif(Stream Stream);

        int Width { get; }

        int Height { get; }

        void CopyTo(byte[] Buffer, int Length);

        IBitmapEditor GetEditor();
    }

    public interface IDirectBufferAccess
    {
        byte[] ImageData { get; }
    }

    public interface IFrameWrapper
    {
        IBitmapFrame Frame { get; }
    }
}