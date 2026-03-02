using System;
using AIWarsIdle.Persistence.Domain;
using Newtonsoft.Json;

namespace AIWarsIdle.Persistence.Services
{
    public sealed class SaveDataMigrator
    {
        private sealed class SaveVersionHeader
        {
            public int Version;
        }

        public SaveDataV1 DeserializeToLatest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON must be non-empty.", nameof(json));
            }

            var header = JsonConvert.DeserializeObject<SaveVersionHeader>(json);
            var version = header?.Version ?? 0;

            return version switch
            {
                1 => DeserializeV1(json),
                _ => throw new NotSupportedException($"Unsupported save version: {version}.")
            };
        }

        private static SaveDataV1 DeserializeV1(string json)
        {
            var data = JsonConvert.DeserializeObject<SaveDataV1>(json) ?? new SaveDataV1();
            data.Normalize();
            return data;
        }
    }
}

