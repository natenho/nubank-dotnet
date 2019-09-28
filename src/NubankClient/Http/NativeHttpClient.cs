using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NubankClient.Http
{
    internal class NativeHttpClient : IHttpClient
    {
        private readonly HttpClient _httpClient;
        public NativeHttpClient()
        {
            _httpClient = new HttpClient();            
        }

        public Task<T> GetAsync<T>(string url) where T : new()
        {
            return GetAsync<T>(url, new Dictionary<string, string>());
        }

        public async Task<T> GetAsync<T>(string url, Dictionary<string, string> headers) where T : new()
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(url);

                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsJsonAsync<T>();
                }
            }

            return default;
        }

        public Task<T> PostAsync<T>(string url, object body) where T : new()
        {
            return PostAsync<T>(url, body, new Dictionary<string, string>());
        }

        public async Task<T> PostAsync<T>(string url, object body, Dictionary<string, string> headers) where T : new()
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(url);
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsJsonAsync<T>();
                }
            }

            return default;
        }
    }
}
