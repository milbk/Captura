using System;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;

namespace DesktopDuplication
{
    public class AllocatedTexture : IDisposable
    {
        readonly MediaBuffer _mediaBuffer;

        public AllocatedTexture(Texture2D Texture)
        {
            this.Texture = Texture;
            Sample = CreateSample(Texture, out _mediaBuffer);
        }

        static Sample CreateSample(Texture2D Texture, out MediaBuffer MediaBuffer)
        {
            // Create the video sample. This function returns an IMFTrackedSample per MSDN
            var sample = MediaFactory.CreateVideoSampleFromSurface(null);
            // Query the IMFSample to see if it implements IMFTrackedSample
            //var trackedSample = sample.QueryInterface<TrackedSample>();
            // Create the media buffer from the texture
            MediaFactory.CreateDXGISurfaceBuffer(typeof(Texture2D).GUID, Texture, 0, false, out MediaBuffer);
            // Set the owning instance of this class as the allocator
            // for IMFTrackedSample to notify when the sample is released
            //trackedSample.SetAllocator(this, null);
            MediaBuffer.CurrentLength = MediaBuffer.QueryInterface<Buffer2D>().ContiguousLength;
            // Attach the created buffer to the sample
            sample.AddBuffer(MediaBuffer);
            return sample;
        }

        public Texture2D Texture { get; }
        public Sample Sample { get; }

        public void Dispose()
        {
            Sample.RemoveAllBuffers();
            _mediaBuffer.Dispose();
            Sample.Dispose();
            Texture.Dispose();
        }
    }
}
