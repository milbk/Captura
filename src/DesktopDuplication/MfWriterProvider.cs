using System.Collections;
using System.Collections.Generic;

namespace Captura.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MfWriterProvider : IVideoWriterProvider
    {
        public string Name => "mf";

        public IEnumerator<IVideoWriterItem> GetEnumerator()
        {
            yield return new MfItem();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Name;

        public string Description => @"mf";
    }
}