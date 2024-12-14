namespace Rocket.AutoInstaller.Installation
{
    public class ReleaseEntry
    {
        public ReleaseEntry(string name, string fullName, string fileNameWithoutExtension, string fileExtension,
            string directoryName, byte[] content)
        {
            Name = name;
            FullName = fullName;
            FileNameWithoutExtension = fileNameWithoutExtension;
            FileExtension = fileExtension;
            DirectoryName = directoryName;
            Content = content;
        }

        public string Name { get; }
        public string FullName { get; }
        public string FileExtension { get; }
        public string FileNameWithoutExtension { get; }
        public string DirectoryName { get; set; }
        public byte[] Content { get; }
    }
}