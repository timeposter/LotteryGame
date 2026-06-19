using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace LotteryCore.Utils
{
    public static class HttpHelper
    {
        private static readonly HttpClient _httpClient;

        static HttpHelper()
        {
            // 核心修复：全局忽略SSL证书校验，解决SSL握手失败
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, err) =>
            {
                // 全部放行证书，测试/采集服务专用
                return true;
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };

            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/125.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.manycailm.com/");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            
        }

        public static async Task<string> SafeGetAsync(string url)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                //Log.Error(ex, "网络/SSL请求异常");
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                //Log.Warning("接口请求超时");
                return string.Empty;
            }
            catch (Exception ex)
            {
               // Log.Error(ex, "未知请求异常");
                return string.Empty;
            }
        }
    }
}