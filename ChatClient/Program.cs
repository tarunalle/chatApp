using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static async Task Main()
    {
        Console.Write("Enter your username: ");
        string username = Console.ReadLine()!;
        Console.Write("Enter password: ");
        string password = Console.ReadLine()!;

        TcpClient client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 5050);
        var stream = client.GetStream();

        var loginPacket = new { type = "LOGIN_REQ", username, password };
        await Send(stream, loginPacket);

        byte[] buffer = new byte[4096];
        int bytes = await stream.ReadAsync(buffer);
        string responseJson = Encoding.UTF8.GetString(buffer, 0, bytes);

        var response = JsonSerializer.Deserialize<Dictionary<string, string>>(responseJson);

        if (response != null && response["type"] == "LOGIN_RESP")
        {
            if (response["status"] == "ok")
            {
                Console.WriteLine(" Login successful");
            }
            else
            {
                Console.WriteLine($" Login failed: {response["reason"]}");
                client.Close();
                return;
            }
        }

        _ = Task.Run(() => ListenForMessages(stream));


        while (true)
        {
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.StartsWith("/dm "))
            {
                var parts = input.Split(' ', 3);
                var packet = new { type = "DM", to = parts[1], msg = parts[2] };
                await Send(stream, packet);
            }

            else if (input.StartsWith("/broadcast "))
            {
                var msg = input.Substring(11);
                var packet = new { type = "BROADCAST", msg };
                await Send(stream, packet);
            }
            else if (input.StartsWith("/group "))
            {
                var parts = input.Split(' ', 3);
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: /group user1,user2,... message");
                    continue;
                }
                var toList = parts[1].Split(',').Select(u => u.Trim()).ToList();
                var msg = parts[2];
                var packet = new { type = "GROUP_MSG", to = toList, msg };
                await Send(stream, packet);
            }

            else if (input == "/exit")
            {
                Console.WriteLine("Logging off");
                client.Close();
                break;
            }
        }
    }

    static async Task Send(NetworkStream stream, object packet)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] data = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(data);
    }

    static async Task ListenForMessages(NetworkStream stream)
    {
        try
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                int bytes = await stream.ReadAsync(buffer);
                if (bytes == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytes);
                var message = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (message != null && message.ContainsKey("msg"))
                    Console.WriteLine($"\n[{message["from"]}]: {message["msg"]}");
            }
        }
        catch
        {
            Console.WriteLine(" Disconnected from server");
        }
    }
}
