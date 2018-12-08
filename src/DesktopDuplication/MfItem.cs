using DesktopDuplication;

namespace Captura.Models
{
    public class MfItem : IVideoWriterItem
    {
        public string Extension => ".mp4";
        public string Description { get; } = "mp4";

        readonly string _name = "mf";

        public override string ToString() => _name;

        public virtual IVideoFileWriter GetVideoFileWriter(VideoWriterArgs Args)
        {
            return new MfWriter(Args.FrameRate, Args.ImageProvider.Width, Args.ImageProvider.Height, Args.FileName);
        }
    }
}
