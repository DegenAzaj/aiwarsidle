using System;
using System.IO;
using AIWarsIdle.Persistence.Domain;
using AIWarsIdle.Persistence.IO;
using Newtonsoft.Json;

namespace AIWarsIdle.Persistence.Services
{
    public sealed class SaveService
    {
        private readonly SaveFilePaths _paths;
        private readonly SaveDataMigrator _migrator;
        private readonly JsonSerializerSettings _jsonSettings;

        public SaveService(SaveFilePaths paths, SaveDataMigrator migrator, JsonSerializerSettings? jsonSettings = null)
        {
            _paths = paths;
            _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
            _jsonSettings = jsonSettings ?? new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include
            };
        }

        public SaveDataV1 LoadOrCreate()
        {
            Directory.CreateDirectory(_paths.DirectoryPath);

            if (!File.Exists(_paths.SavePath))
            {
                var fresh = new SaveDataV1();
                fresh.Normalize();
                Save(fresh);
                return fresh;
            }

            if (TryLoadFile(_paths.SavePath, out var data))
            {
                return data!;
            }

            if (TryLoadFile(_paths.BackupPath, out var backupData))
            {
                Save(backupData!);
                return backupData!;
            }

            var fallback = new SaveDataV1();
            fallback.Normalize();
            Save(fallback);
            return fallback;
        }

        public void Save(SaveDataV1 data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(_paths.DirectoryPath);

            data.Normalize();
            var json = JsonConvert.SerializeObject(data, _jsonSettings);

            WriteAtomically(json);
        }

        private bool TryLoadFile(string path, out SaveDataV1? data)
        {
            data = null;

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var json = File.ReadAllText(path);
                data = _migrator.DeserializeToLatest(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void WriteAtomically(string content)
        {
            WriteToTemp(content);

            if (File.Exists(_paths.SavePath))
            {
                try
                {
                    File.Replace(_paths.TempPath, _paths.SavePath, _paths.BackupPath, ignoreMetadataErrors: true);
                    return;
                }
                catch
                {
                    try
                    {
                        File.Copy(_paths.SavePath, _paths.BackupPath, overwrite: true);
                    }
                    catch
                    {
                        // ignore best-effort backup failures
                    }
                }
            }

            try
            {
                if (File.Exists(_paths.SavePath))
                {
                    File.Delete(_paths.SavePath);
                }

                File.Move(_paths.TempPath, _paths.SavePath);
            }
            finally
            {
                try
                {
                    if (File.Exists(_paths.TempPath))
                    {
                        File.Delete(_paths.TempPath);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }

        private void WriteToTemp(string content)
        {
            using var stream = new FileStream(_paths.TempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }
}

