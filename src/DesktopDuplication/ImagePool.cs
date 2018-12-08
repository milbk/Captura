using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Captura;

namespace Screna
{
    public class DummyBitmapEditor : IBitmapEditor
    {
        DummyBitmapEditor()
        {
            var bitmap = new Bitmap((int) Width, (int) Height);
            Graphics = Graphics.FromImage(bitmap);
        }

        public static IBitmapEditor Instance { get; } = new DummyBitmapEditor();


        public void Dispose() { }

        public Graphics Graphics { get; }

        public float Width => 100;
        public float Height => 100;

        public void FillRectangle(Brush Brush, RectangleF Rectangle) { }

        public void FillEllipse(Brush Brush, RectangleF Rectangle) { }

        public void DrawEllipse(Pen Pen, RectangleF Rectangle) { }
    }

    public class ReusableFrame : IBitmapFrame, IDirectBufferAccess
    {
        public byte[] ImageData { get; }

        public ReusableFrame(int Width, int Height)
        {
            this.Width = Width;
            this.Height = Height;

            ImageData = new byte[Width * Height * 4];
        }

        public void Dispose()
        {
            Released?.Invoke();
        }

        public void SaveGif(Stream Stream)
        {
            throw new NotImplementedException();
        }

        public int Width { get; }

        public int Height { get; }

        public void CopyTo(byte[] Buffer, int Length)
        {
            Array.Copy(ImageData, Buffer, Length);
        }

        public IBitmapEditor GetEditor() => DummyBitmapEditor.Instance;

        public void Destroy()
        {
        }

        public event Action Released;
    }

    public class ImagePool : IDisposable
    {
        public ImagePool(int Width, int Height)
        {
            this.Width = Width;
            this.Height = Height;
        }

        public int Width { get; }

        public int Height { get; }

        readonly List<ReusableFrame> _frames = new List<ReusableFrame>();
        readonly Queue<ReusableFrame> _pool = new Queue<ReusableFrame>();

        public ReusableFrame Get()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                    return _pool.Dequeue();

                var frame = new ReusableFrame(Width, Height);

                frame.Released += () =>
                {
                    lock (_pool)
                    {
                        _pool.Enqueue(frame);
                    }
                };

                _frames.Add(frame);

                return frame;
            }
        }

        public void Dispose()
        {
            foreach (var frame in _frames)
            {
                frame.Destroy();
            }
        }
    }
}