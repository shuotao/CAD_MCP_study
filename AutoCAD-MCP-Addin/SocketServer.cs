using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Newtonsoft.Json;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AutoCADMCP.Server
{
    public class SocketServer
    {
        private static TcpListener _listener;
        private static bool _isRunning;
        private static CancellationTokenSource _cts;
        private const int Port = 8964; // Same as Revit MCP for consistency

        public static bool IsRunning => _isRunning;

        public static async void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _isRunning = true;
                _cts = new CancellationTokenSource();

                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[MCP] Server started on port {Port}");

                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = HandleClientAsync(client, _cts.Token);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Listener stopped
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[MCP] Accept error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[MCP] Server start failed: {ex.Message}");
                _isRunning = false;
            }
        }

        public static void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[MCP] Server stopped");
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                var receivedData = new List<byte>();

                while (_isRunning && client.Connected && !token.IsCancellationRequested)
                {
                    try
                    {
                        // Set a timeout for reading to detect end of current "packet" 
                        // Note: For a more robust protocol, we should send length first
                        // but for simplicity we assume the whole message arrives since it's localhost
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead == 0) break;

                        string jsonPart = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        // Basic JSON completeness check (simplified for this use case)
                        var request = JsonConvert.DeserializeObject<McpRequest>(jsonPart);

                        if (request != null)
                        {
                            var response = await ProcessRequest(request);
                            var responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                        }
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task<McpResponse> ProcessRequest(McpRequest request)
        {
            var tcs = new TaskCompletionSource<McpResponse>();

            // Must execute on AutoCAD main thread
            if (App.MainThreadContext == null)
            {
                tcs.SetResult(new McpResponse { Success = false, Message = "AutoCAD Main Thread Context not initialized." });
                return await tcs.Task;
            }

            App.MainThreadContext.Post(_ =>
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    tcs.SetResult(new McpResponse { Success = false, Message = "No active document found in AutoCAD." });
                    return;
                }

                // IMPORTANT: Must lock document when operating from non-command context
                try
                {
                    using (doc.LockDocument())
                    {
                        string result = CommandHandler.Execute(doc, request.Command, request.Args);
                        tcs.SetResult(new McpResponse { Success = true, Message = result });
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new McpResponse { Success = false, Message = ex.Message });
                }
            }, null);

            return await tcs.Task;
        }
    }

    public class McpRequest
    {
        public string Command { get; set; }
        public Dictionary<string, object> Args { get; set; }
    }

    public class McpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
