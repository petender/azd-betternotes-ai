using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterNotes.Services
{
    public class AzureAIService
    {
        private readonly string _endpoint;
        private readonly string _key;
        private readonly TokenCredential _credential;
        private readonly bool _useManagedIdentity;
        private readonly ILogger<AzureAIService> _logger;
        private readonly HttpClient _httpClient;

        public AzureAIService(IConfiguration configuration, ILogger<AzureAIService> logger, HttpClient httpClient)
        {
            _endpoint = configuration["AzureAI:Endpoint"];
            _key = configuration["AzureAI:Key"];
            _logger = logger;
            _httpClient = httpClient;

            // Use Managed Identity if no key is provided
            _useManagedIdentity = string.IsNullOrEmpty(_key);
            if (_useManagedIdentity)
            {
                _credential = new DefaultAzureCredential();
                _logger.LogInformation("Using Managed Identity for authentication");
            }
            else
            {
                _logger.LogInformation("Using API Key for authentication");
            }
        }

        public async Task<string> AnalyzeFileAsync(Stream fileStream, string fileName)
        {
            _logger.LogInformation("Starting analysis for file: {FileName}", fileName);
            _logger.LogInformation("Authentication mode: {Mode}", _useManagedIdentity ? "Managed Identity" : "API Key");
            _logger.LogInformation("Endpoint: {Endpoint}", _endpoint);

            try
            {
                // Determine content type based on file extension
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".doc" => "application/msword",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".bmp" => "image/bmp",
                    ".tiff" or ".tif" => "image/tiff",
                    ".heif" or ".heic" => "image/heif",
                    _ => "application/octet-stream"
                };

                // Build the Document Intelligence API endpoint
                var apiVersion = "2023-07-31";
                var modelId = "prebuilt-document"; // Use prebuilt-document for comprehensive analysis
                var analyzeEndpoint = $"{_endpoint.TrimEnd('/')}/formrecognizer/documentModels/{modelId}:analyze?api-version={apiVersion}";

                _logger.LogInformation("Using Document Intelligence endpoint: {Endpoint}", analyzeEndpoint);

                var content = new StreamContent(fileStream);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                // Create the HTTP request message
                var request = new HttpRequestMessage(HttpMethod.Post, analyzeEndpoint)
                {
                    Content = content
                };
                
                // Add authentication header based on auth method
                if (_useManagedIdentity)
                {
                    try
                    {
                        var tokenRequestContext = new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" });
                        _logger.LogInformation("Requesting token for scope: https://cognitiveservices.azure.com/.default");
                        var token = await _credential.GetTokenAsync(tokenRequestContext, default);
                        _logger.LogInformation("Successfully obtained token with Managed Identity");
                        request.Headers.Add("Authorization", $"Bearer {token.Token}");
                    }
                    catch (Exception authEx)
                    {
                        _logger.LogError(authEx, "Failed to obtain token with Managed Identity. This could be due to role assignment not being propagated yet.");
                        throw new Exception("Failed to authenticate with Managed Identity. Please wait a few minutes for role assignments to propagate.", authEx);
                    }
                }
                else
                {
                    request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
                }

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure AI analysis failed with status code: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        return "Error: Access forbidden (403). The managed identity may not have the required permissions yet. Please wait 5-10 minutes for role assignments to propagate, then try again.";
                    }
                    
                    return $"Error: Unable to analyze file. Status: {response.StatusCode}";
                }

                // Document Intelligence returns a 202 Accepted with an Operation-Location header
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                    if (string.IsNullOrEmpty(operationLocation))
                    {
                        _logger.LogError("No Operation-Location header found in response.");
                        return "Error: No Operation-Location header found.";
                    }

                    _logger.LogInformation("Document analysis started. Polling results from: {OperationLocation}", operationLocation);

                    // Poll for results
                    var analysisResult = await PollForResultsAsync(operationLocation);
                    return analysisResult;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Azure AI Response: {ResponseContent}", responseContent);

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogError("Azure AI returned an empty response.");
                    return "Error: Empty response from Azure AI.";
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file: {FileName}", fileName);
                throw;
            }
        }

        private async Task<string> PollForResultsAsync(string operationLocation)
        {
            var maxAttempts = 30; // Poll for up to 30 seconds
            var delay = TimeSpan.FromSeconds(1);

            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(delay);

                var request = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                
                // Add authentication header based on auth method
                if (_useManagedIdentity)
                {
                    var tokenRequestContext = new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" });
                    var token = await _credential.GetTokenAsync(tokenRequestContext, default);
                    request.Headers.Add("Authorization", $"Bearer {token.Token}");
                }
                else
                {
                    request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
                }

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Polling failed with status code: {StatusCode}", response.StatusCode);
                    return $"Error: Polling failed. Status: {response.StatusCode}";
                }

                try
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var status = result.GetProperty("status").GetString();

                    _logger.LogInformation("Analysis status: {Status}", status);

                    if (status == "succeeded")
                    {
                        // Extract the text content from the analysis result
                        var analyzeResult = result.GetProperty("analyzeResult");
                        var content = ExtractTextFromAnalyzeResult(analyzeResult);
                        return content;
                    }
                    else if (status == "failed")
                    {
                        var error = result.GetProperty("error");
                        _logger.LogError("Analysis failed: {Error}", error.ToString());
                        return $"Error: Analysis failed. {error}";
                    }
                    // If status is "running", continue polling
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse polling response.");
                    return "Error: Failed to parse polling response.";
                }
            }

            return "Error: Analysis timeout. Please try again.";
        }

        private string ExtractTextFromAnalyzeResult(JsonElement analyzeResult)
        {
            var sb = new StringBuilder();

            try
            {
                // Extract content (text with reading order)
                if (analyzeResult.TryGetProperty("content", out var content))
                {
                    sb.AppendLine("=== Document Content ===");
                    sb.AppendLine(content.GetString());
                    sb.AppendLine();
                }

                // Extract key-value pairs
                if (analyzeResult.TryGetProperty("keyValuePairs", out var keyValuePairs))
                {
                    sb.AppendLine("=== Key-Value Pairs ===");
                    foreach (var kvp in keyValuePairs.EnumerateArray())
                    {
                        if (kvp.TryGetProperty("key", out var key) && 
                            key.TryGetProperty("content", out var keyContent))
                        {
                            var keyText = keyContent.GetString();
                            var valueText = "";

                            if (kvp.TryGetProperty("value", out var value) && 
                                value.TryGetProperty("content", out var valueContent))
                            {
                                valueText = valueContent.GetString();
                            }

                            sb.AppendLine($"{keyText}: {valueText}");
                        }
                    }
                    sb.AppendLine();
                }

                // Extract entities if available
                if (analyzeResult.TryGetProperty("entities", out var entities))
                {
                    sb.AppendLine("=== Entities ===");
                    foreach (var entity in entities.EnumerateArray())
                    {
                        if (entity.TryGetProperty("content", out var entityContent) &&
                            entity.TryGetProperty("category", out var category))
                        {
                            sb.AppendLine($"{category.GetString()}: {entityContent.GetString()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from analyze result.");
                sb.AppendLine("Error: Failed to extract all information from the analysis result.");
            }

            return sb.ToString();
        }
    }
}