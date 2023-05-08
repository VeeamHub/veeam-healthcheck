using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Collection.REST
{
    internal class RestInvoker
    {
        private readonly IHttpClientFactory _httpClientFactory = null!;
        private string _token;

        public RestInvoker()
        {
        }
        public async void Run()
        {
            var t =  SetRestToken().Result;

        }
        public void GetVbrVersion()
        {
            //string token = RestLoginToken();

            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            //await CollectServerVersion(client);
            CollectServerVersion(client);
        }
        static async Task CollectServerVersion(HttpClient client)
        {

        }

        public async Task<string> SetRestToken()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-version", "1.1-rev0");

            List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
            postData.Add(new KeyValuePair<string, string>("grant_type", "password"));
            postData.Add(new KeyValuePair<string, string>("username", "administrator"));
            postData.Add(new KeyValuePair<string, string>("password", "Veeam123!"));
            //postData.Add(new KeyValuePair<string, string>("refresh_token", "string"));
            //postData.Add(new KeyValuePair<string, string>("code", "string"));
            postData.Add(new KeyValuePair<string, string>("use_short_term_refresh", "true"));
            postData.Add(new KeyValuePair<string, string>("vbr_token", "string"));

            var request = await client.PostAsync("https://vbr-12-beta3/api/oauth2/token", new FormUrlEncodedContent(postData));
            var response = await request.Content.ReadAsStringAsync();

            Console.WriteLine(response);
            return response;
        }
    }
}
