using System.IO;

namespace Rocket.AutoInstaller.Installation
{
    public static class StreamExtensions
    {
        public static byte[] CopyToArray(this Stream source)
        {
            var memoryStream = new MemoryStream();
            source.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}