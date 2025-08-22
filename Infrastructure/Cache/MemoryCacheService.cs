using Application.Abstraction;
using Microsoft.Extensions.Caching.Memory;


namespace Infrastructure.Cache
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;

        public MemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (_cache.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan ttl)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            _cache.Set(key, value!, options);
        }
    }
}
