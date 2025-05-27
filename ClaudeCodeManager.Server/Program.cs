using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

var server = new WslManagerServer();
await server.StartAsync();

public class WslManagerServer
{
    private readonly Dictionary<int, Process> _activeWslProcesses = new();
    private readonly Dictionary<int, Process> _activeTtydProcesses = new();
    private bool _isRunning = true;

    public async Task StartAsync()
    {
        Console.WriteLine("WSL Manager Server starting...");
        
        // Windows環境チェック
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Warning: This application is designed for Windows environment.");
            Console.WriteLine("Current OS: " + RuntimeInformation.OSDescription);
            Console.WriteLine("WSL commands may not work as expected.");
        }
        else
        {
            Console.WriteLine("Running on Windows - WSL integration available");
        }
        
        while (_isRunning)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    "ClaudeCodeManagerPipe", 
                    PipeDirection.InOut, 
                    1, // maxNumberOfServerInstances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                    
                Console.WriteLine("Waiting for client connection...");
                
                await server.WaitForConnectionAsync();
                Console.WriteLine("Client connected.");
                
                await HandleClientAsync(server);
                Console.WriteLine("Client disconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server)
    {
        try
        {
            Console.WriteLine("Reading request from client...");
            var buffer = new byte[1024];
            var bytesRead = await server.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead == 0)
            {
                Console.WriteLine("No data received from client");
                return;
            }
            
            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Received request ({bytesRead} bytes): {request}");
            
            if (string.IsNullOrWhiteSpace(request))
            {
                Console.WriteLine("Empty request received");
                return;
            }
            
            var requestData = JsonSerializer.Deserialize<WslRequest>(request);
            if (requestData == null)
            {
                Console.WriteLine("Failed to deserialize request");
                var errorResponse = new WslResponse { Success = false, Message = "Invalid request format" };
                await SendResponseAsync(server, errorResponse);
                return;
            }
            
            Console.WriteLine($"Processing action: {requestData.Action}, Terminal: {requestData.TerminalNumber}");
            var response = await ProcessRequestAsync(requestData);
            
            await SendResponseAsync(server, response);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON error: {ex.Message}");
            var errorResponse = new WslResponse { Success = false, Message = "Invalid JSON format" };
            await SendResponseAsync(server, errorResponse);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"IO error (client likely disconnected): {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client handling error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task SendResponseAsync(NamedPipeServerStream server, WslResponse response)
    {
        try
        {
            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            
            Console.WriteLine($"Sending response ({responseBytes.Length} bytes): {responseJson}");
            await server.WriteAsync(responseBytes, 0, responseBytes.Length);
            await server.FlushAsync();
            Console.WriteLine("Response sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending response: {ex.Message}");
        }
    }

    private async Task<WslResponse> ProcessRequestAsync(WslRequest request)
    {
        try
        {
            switch (request.Action)
            {
                case "status":
                    return await GetTerminalStatus();
                
                case "start":
                    return await StartTerminalAsync(request.TerminalNumber);
                
                case "stop":
                    return await StopTerminalAsync(request.TerminalNumber);
                
                default:
                    return new WslResponse { Success = false, Message = "Unknown action" };
            }
        }
        catch (Exception ex)
        {
            return new WslResponse { Success = false, Message = ex.Message };
        }
    }

    private Task<WslResponse> GetTerminalStatus()
    {
        var terminals = new List<TerminalStatus>();
        
        for (int i = 1; i <= 5; i++)
        {
            var isWslRunning = _activeWslProcesses.ContainsKey(i) && !_activeWslProcesses[i].HasExited;
            var isTtydRunning = _activeTtydProcesses.ContainsKey(i) && !_activeTtydProcesses[i].HasExited;
            
            terminals.Add(new TerminalStatus
            {
                Number = i,
                IsRunning = isWslRunning && isTtydRunning,
                Port = 7680 + i
            });
        }
        
        return Task.FromResult(new WslResponse 
        { 
            Success = true, 
            Terminals = terminals.ToArray()
        });
    }

    private async Task<WslResponse> StartTerminalAsync(int terminalNumber)
    {
        if (terminalNumber < 1 || terminalNumber > 5)
        {
            return new WslResponse { Success = false, Message = "Terminal number must be between 1 and 5" };
        }

        if (_activeWslProcesses.ContainsKey(terminalNumber) && !_activeWslProcesses[terminalNumber].HasExited)
        {
            return new WslResponse { Success = true, Message = $"Terminal {terminalNumber} is already running" };
        }

        try
        {
            Console.WriteLine($"Starting terminal {terminalNumber}...");
            
            // WSLコマンドを Windows/Linux 環境に応じて調整
            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wsl.exe" : "wsl";
            
            // エイリアスが読み込まれないため、ttydコマンドを直接実行
            var port = 7680 + terminalNumber;
            var sessionName = $"ttysession{terminalNumber}";
            var bashScript = $"echo 'Starting ttyd on port {port}...' && ttyd -p {port} -t fontSize=20 --writable tmux new -A -s {sessionName}";
            var arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? $"-e bash -c \"{bashScript.Replace("\"", "\\\"")}\"" 
                : $"-e bash -c '{bashScript}'";
            
            Console.WriteLine($"Executing: {fileName} {arguments}");
            
            // ttydプロセスを直接起動（WSLインスタンスとttydプロセスを統合）
            var ttydProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            if (!ttydProcess.Start())
            {
                return new WslResponse 
                { 
                    Success = false, 
                    Message = $"Failed to start process for terminal {terminalNumber}" 
                };
            }
            
            Console.WriteLine($"Terminal {terminalNumber} process started with PID: {ttydProcess.Id}");
            
            // 短時間待機してプロセスの初期状態をチェック
            await Task.Delay(3000);
            
            if (ttydProcess.HasExited)
            {
                var exitCode = ttydProcess.ExitCode;
                string output = "";
                string error = "";
                
                try
                {
                    output = await ttydProcess.StandardOutput.ReadToEndAsync();
                    error = await ttydProcess.StandardError.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading process output: {ex.Message}");
                }
                
                Console.WriteLine($"Terminal {terminalNumber} process finished:");
                Console.WriteLine($"Exit code: {exitCode}");
                Console.WriteLine($"Standard output: {output}");
                Console.WriteLine($"Standard error: {error}");
                
                return new WslResponse 
                { 
                    Success = false, 
                    Message = $"Terminal {terminalNumber} failed to start (exit code: {exitCode}). Output: {output}. Error: {error}" 
                };
            }
            
            // プロセスがまだ実行中の場合は成功とみなす
            _activeWslProcesses[terminalNumber] = ttydProcess;
            _activeTtydProcesses[terminalNumber] = ttydProcess;
            
            // 非同期でプロセス出力を監視
            _ = Task.Run(async () =>
            {
                try
                {
                    await ttydProcess.WaitForExitAsync();
                    Console.WriteLine($"Terminal {terminalNumber} process exited with code: {ttydProcess.ExitCode}");
                    _activeWslProcesses.Remove(terminalNumber);
                    _activeTtydProcesses.Remove(terminalNumber);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring terminal {terminalNumber}: {ex.Message}");
                }
            });
            
            Console.WriteLine($"Terminal {terminalNumber} started successfully on port {7680 + terminalNumber}");
            
            return new WslResponse 
            { 
                Success = true, 
                Message = $"Terminal {terminalNumber} started successfully" 
            };
        }
        catch (Exception ex)
        {
            return new WslResponse 
            { 
                Success = false, 
                Message = $"Failed to start terminal {terminalNumber}: {ex.Message}" 
            };
        }
    }

    private Task<WslResponse> StopTerminalAsync(int terminalNumber)
    {
        try
        {
            Console.WriteLine($"Stopping terminal {terminalNumber}...");
            
            bool processStopped = false;
            
            if (_activeTtydProcesses.ContainsKey(terminalNumber))
            {
                var process = _activeTtydProcesses[terminalNumber];
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000); // 5秒待機
                        processStopped = true;
                        Console.WriteLine($"Terminal {terminalNumber} process killed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error killing terminal {terminalNumber}: {ex.Message}");
                    }
                }
                _activeTtydProcesses.Remove(terminalNumber);
            }

            if (_activeWslProcesses.ContainsKey(terminalNumber))
            {
                _activeWslProcesses.Remove(terminalNumber);
            }

            return Task.FromResult(new WslResponse 
            { 
                Success = true, 
                Message = processStopped ? 
                    $"Terminal {terminalNumber} stopped successfully" : 
                    $"Terminal {terminalNumber} was not running"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping terminal {terminalNumber}: {ex.Message}");
            return Task.FromResult(new WslResponse 
            { 
                Success = false, 
                Message = $"Failed to stop terminal {terminalNumber}: {ex.Message}" 
            });
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
