using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ManagedIdentitySample.Client.Controllers
{
    [ApiController]
    [Route("/")]
    public class Controller : ControllerBase
    {
        private readonly ILogger<Controller> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public Controller(ILogger<Controller> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok("success");
        }

        [HttpPost]
        [Route("~/setstate")]
        public async Task<IActionResult> SetState([FromBody] Dictionary<string, string> payload)
        {
            try
            {
                string daprPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
                string targetDaprAppId = Environment.GetEnvironmentVariable("TARGET_DAPR_APP_ID");
                string setStateUri = $"http://localhost:{daprPort}/v1.0/invoke/{targetDaprAppId}/method/setstate";

                Console.WriteLine($"[Client] setStateUri: {setStateUri}");

                HttpClient httpClient = _httpClientFactory.CreateClient();
                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(setStateUri),
                    Method = HttpMethod.Post,
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                HttpResponseMessage response = await httpClient.SendAsync(request);
                
                Console.WriteLine($"[Client] Request to service has been sent, the status code is {response.StatusCode}");
                return Ok("success");                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Exception:{ex.Message}");
                return BadRequest(ex);
            }
        }

        [HttpPost]
        [Route("~/setstatewithhttp")]
        public async Task<IActionResult> SetStateWithoutDapr(string usetoken, [FromBody] Dictionary<string, string> payload)
        {
            try
            {
                string targetAppName = Environment.GetEnvironmentVariable("TARGET_APP_NAME") ?? "order";
                string envDNSSuffix = Environment.GetEnvironmentVariable("CONTAINER_APP_ENV_DNS_SUFFIX");
                string umiClientId = Environment.GetEnvironmentVariable("USER_ASSIGNED_CLIENT_ID");
                string targetUri = $"https://{targetAppName}.{envDNSSuffix}/setstate";

                Console.WriteLine($"[Client] target url: {targetUri}");

                HttpClient httpClient = _httpClientFactory.CreateClient();

                if (!String.IsNullOrEmpty(usetoken) && usetoken.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                {
                    Console.WriteLine($"[Client] start requesting token");
                    Console.WriteLine($"[Client] umiClientId {umiClientId}");

                    var credential = new ManagedIdentityCredential(umiClientId); // user-assigned identity
                    var accessToken = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "api://01fb19f9-f9d0-44cd-993f-fb41e5a7a756/.default" })).Token;

                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    Console.WriteLine($"[Client] finish requesting token.");
                }

                Console.WriteLine($"[Client] start sending request with token");
                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(targetUri),
                    Method = HttpMethod.Post,
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                   
                };
                HttpResponseMessage response = await httpClient.SendAsync(request);

                var streamReader = new StreamReader(response.Content.ReadAsStream());
                string content = streamReader.ReadToEnd();

                Console.WriteLine($"[Client] Request to service has been sent, the status code is {response.StatusCode}, response content is: {content}");

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"[Client] Request to backend succeed: response: {response}");
                    return Ok("success");
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine($"[Client] Request to backend with status code: {response.StatusCode}");
                    return Unauthorized("unauthorized");
                }
                else
                {
                    Console.WriteLine($"[Client] Request to backend failed: response content: {content}, headers: {response.Headers}");
                    return BadRequest($"Response status code {response.StatusCode}");
                }                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] SetStateWithoutDapr Exception:{ex}");
                Console.WriteLine($"[Client] SetStateWithoutDapr Exception.Message:{ex.Message}");
                return BadRequest(ex.Message);
            }
        }


        [HttpPost]
        [Route("~/writeblob")]
        public async Task<IActionResult> WriteBlobWithUserAssignedIdentity(string time)
        {
            string accountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? "cappsintteststorage";
            string containerName = Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAME") ?? "daprtestcontainer";
            string umiClientId = Environment.GetEnvironmentVariable("USER_ASSIGNED_CLIENT_ID") ?? "";

            // Construct the blob container endpoint from the arguments.
            string containerEndpoint = $"https://{accountName}.blob.core.windows.net/{containerName}";

            try
            {
                // Get a credential and create a service client object for the blob container.
                BlobContainerClient containerClient = new BlobContainerClient(
                new Uri(containerEndpoint),
                new ManagedIdentityCredential(umiClientId));

                Console.WriteLine($"[Client] Managed identity client id: {umiClientId}");

                // Create the container if it does not exist.
                await containerClient.CreateIfNotExistsAsync();

                // Upload text to a new block blob.
                string blobContents = $"writeblob-content-{time}";
                byte[] byteArray = Encoding.ASCII.GetBytes(blobContents);

                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    await containerClient.UploadBlobAsync($"writeblob-name-{time}", stream);
                }

                return Ok($"success");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] WriteBlobWithUserAssignedIdentity Exception: {ex.Message}, Managed identity client id: {umiClientId}");
                return BadRequest($"[Client] Managed identity client id: {umiClientId}, {ex.Message}");
            }            
        }

        [HttpPost]
        [Route("~/writevolume")]
        public IActionResult WriteVolume(string time)
        {
            try
            {
                string path = $"/etc/volume/write-volume-name-{time}.txt";
                System.IO.File.Create(path);

                var content = $"write-volume-content-{time}";
                System.IO.File.WriteAllText(path, content);

                return Ok("success");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] WriteVolume Exception: {ex.Message}");
                return BadRequest($"[Client] WriteVolume Exception {ex.Message}");
            }
        }

    }
}