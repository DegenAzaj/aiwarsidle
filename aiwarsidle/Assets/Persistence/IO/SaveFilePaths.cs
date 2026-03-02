using System;
using System.IO;

namespace AIWarsIdle.Persistence.IO
{
    public readonly struct SaveFilePaths
    {
        public string DirectoryPath { get; }
        public string SavePath { get; }
        public string TempPath { get; }
        public string BackupPath { get; }

        public SaveFilePaths(string directoryPath, string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path must be non-empty.", nameof(directoryPath));
            }
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                throw new ArgumentException("File name must be non-empty.", nameof(fileNameWithoutExtension));
            }

            DirectoryPath = directoryPath;
            SavePath = Path.Combine(directoryPath, $"{fileNameWithoutExtension}.json");
            TempPath = Path.Combine(directoryPath, $"{fileNameWithoutExtension}.tmp");
            BackupPath = Path.Combine(directoryPath, $"{fileNameWithoutExtension}.bak");
        }
    }
}

