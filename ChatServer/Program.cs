using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static Dictionary<string, TcpClient> onlineUsers = new();
    static Dictionary<string, string> credentials = new()
    {
        { "tarun", "1234" },
        { "rahul", "1234" },
        { "admin", "admin" }
    };

    static async Task Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 5050);
        server.Start();
        Console.WriteLine("Server started on port 5050");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            _ = HandleClient(client);
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        string? username = null;

        try
        {
            while (true)
            {
                byte[] buffer = new byte[4096];
                int bytes = await stream.ReadAsync(buffer);
                if (bytes == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytes);

                var packet = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (packet == null || !packet.ContainsKey("type"))
                    throw new Exception("Malformed JSON");

                string type = packet["type"].ToString()!;

                switch (type)
                {
                    case "LOGIN_REQ":
                        username = packet["username"].ToString()!;
                        string password = packet["password"].ToString()!;
                        if (credentials.TryGetValue(username, out var pass) && pass == password)
                        {
                            onlineUsers[username] = client;
                            await Send(stream, new { type = "LOGIN_RESP", status = "ok" });
                            Console.WriteLine($"{username} logged in");
                        }
                        else
                        {
                            await Send(stream, new { type = "LOGIN_RESP", status = "err", reason = "invalid credentials" });
                            client.Close();
                            return;
                        }
                        break;

                    case "DM":
                        string to = packet["to"].ToString()!;
                        string msg = packet["msg"].ToString()!;
                        if (onlineUsers.TryGetValue(to, out var target))
                            await Send(target.GetStream(), new { from = username, msg });
                        break;


                    case "BROADCAST":
                        string bmsg = packet["msg"].ToString()!;
                        foreach (var kv in onlineUsers)
                        {
                            if (kv.Key != username)
                                await Send(kv.Value.GetStream(), new { from = username, msg = bmsg });
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown type: {type}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            if (username != null)
            {
                onlineUsers.Remove(username);
                Console.WriteLine($"{username} disconnected");
            }
            client.Close();
        }
    }

    static async Task Send(NetworkStream stream, object packet)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] data = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(data);
        await stream.FlushAsync();
    }
}
