using Microsoft.AspNetCore.Mvc;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeManager.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TerminalController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] WslRequest request)
        {
            try
            {
                Console.WriteLine($"API Request received - Action: {request?.Action}, Terminal: {request?.TerminalNumber}");
                
                if (request == null)
                {
                    Console.WriteLine("Request is null");
                    return Ok(new WslResponse { Success = false, Message = "Invalid request format" });
                }

                if (string.IsNullOrWhiteSpace(request.Action))
                {
                    Console.WriteLine($"Action is empty, TerminalNumber: {request.TerminalNumber}");
                    return Ok(new WslResponse { Success = false, Message = "Action is required" });
                }

                Console.WriteLine($"Processing request - Action: {request.Action}, Terminal: {request.TerminalNumber}");
                var response = await SendRequestToServerAsync(request);
                Console.WriteLine($"API Response: Success={response.Success}, Message={response.Message}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex.Message}");
                return Ok(new WslResponse { Success = false, Message = $"Server error: {ex.Message}" });
            }
        }

        private async Task<WslResponse> SendRequestToServerAsync(WslRequest request)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".", 
                    "ClaudeCodeManagerPipe", 
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                
                Console.WriteLine("Connecting to server...");
                // 3秒でタイムアウト
                var cts = new CancellationTokenSource(3000);
                await client.ConnectAsync(cts.Token);
                Console.WriteLine("Connected to server");

                var requestJson = JsonSerializer.Serialize(request);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                
                Console.WriteLine($"Sending request: {requestJson}");
                await client.WriteAsync(requestBytes, 0, requestBytes.Length);
                await client.FlushAsync();

                var buffer = new byte[4096];
                var bytesRead = await client.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    Console.WriteLine("No response received from server");
                    return new WslResponse { Success = false, Message = "No response from server" };
                }
                
                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received response: {responseJson}");
                
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return new WslResponse { Success = false, Message = "Empty response from server" };
                }
                
                try
                {
                    var response = JsonSerializer.Deserialize<WslResponse>(responseJson);
                    return response ?? new WslResponse { Success = false, Message = "Invalid response from server" };
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON parse error: {ex.Message}");
                    return new WslResponse { Success = false, Message = $"Invalid JSON response: {responseJson.Substring(0, Math.Min(100, responseJson.Length))}" };
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Connection timeout");
                return new WslResponse { Success = false, Message = "Server connection timeout. Please ensure the server is running." };
            }
            catch (IOException ex) when (ex.Message.Contains("pipe") || ex.Message.Contains("broken"))
            {
                Console.WriteLine($"Pipe error: {ex.Message}");
                return new WslResponse { Success = false, Message = "Cannot connect to server. Please ensure the server application is running." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return new WslResponse { Success = false, Message = $"Communication error: {ex.Message}" };
            }
        }
    }

    public class WslRequest
    {
        public string Action { get; set; } = "";
        public int TerminalNumber { get; set; }
    }

    public class WslResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public TerminalStatus[]? Terminals { get; set; }
    }

    public class TerminalStatus
    {
        public int Number { get; set; }
        public bool IsRunning { get; set; }
        public int Port { get; set; }
    }
}