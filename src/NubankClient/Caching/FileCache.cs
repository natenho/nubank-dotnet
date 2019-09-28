using Newtonsoft.Json;
using System;
using System.IO;

namespace NubankClient.Caching
{
    public static class FileCache
    {
        private const string FileCacheExtension = ".cache.json";

        public static T Get<T>(string key)
        {
            if (IsExpired())
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(File.ReadAllText($"{key}{FileCacheExtension}"));
        }

        private static bool IsExpired()
        {
            if (File.Exists(FileCacheExtension))
            {
                var cacheExpirationDateTime = JsonConvert.DeserializeObject<DateTimeOffset>(File.ReadAllText(FileCacheExtension));

                return DateTimeOffset.UtcNow > cacheExpirationDateTime;
            }

            return true;
        }

        public static void Set<T>(string key, T content)
        {
            File.WriteAllText($"{key}{FileCacheExtension}", JsonConvert.SerializeObject(content));
        }

        public static void ExpiresAfter(TimeSpan expirationTime)
        {
            if (IsExpired())
            {
                File.WriteAllText(FileCacheExtension, JsonConvert.SerializeObject(DateTimeOffset.UtcNow.Add(expirationTime)));
            }
        }
    }
}
