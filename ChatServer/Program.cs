// ╔══════════════════════════════════════════╗
// ║         TCP ЧАТ — СЕРВЕР                 ║
// ║  Консольное приложение (.NET 8)          ║
// ╚══════════════════════════════════════════╝
//
// Запуск: dotnet run  (в папке ChatServer)
// Сервер принимает подключения и рассылает
// сообщения всем участникам чата.

using System.Net;
using System.Net.Sockets;
using System.Text;

class ChatServer
{
    // Список всех подключённых клиентов
    static readonly List<TcpClient> clients = new();
    // Блокировка для безопасного доступа из нескольких потоков
    static readonly object lockObj = new();

    static async Task Main()
    {
        int port = 5000;
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        Console.WriteLine($"✅ Сервер запущен на порту {port}");
        Console.WriteLine("Ожидание подключений...\n");

        // Бесконечно принимаем новых клиентов
        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();

            lock (lockObj)
                clients.Add(client);

            Console.WriteLine($"🟢 Новый клиент: {client.Client.RemoteEndPoint}");

            // Каждый клиент обрабатывается в отдельной задаче
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[4096];
        string clientAddress = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            while (true)
            {
                // Читаем данные от клиента
                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break; // Клиент отключился

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"💬 [{clientAddress}]: {message}");

                // Рассылаем сообщение всем остальным клиентам
                await BroadcastAsync(message, sender: client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Ошибка клиента {clientAddress}: {ex.Message}");
        }
        finally
        {
            // Удаляем отключившегося клиента
            lock (lockObj)
                clients.Remove(client);

            client.Close();
            Console.WriteLine($"🔴 Клиент отключился: {clientAddress}");
        }
    }

    // Отправляем сообщение всем, кроме отправителя
    static async Task BroadcastAsync(string message, TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);

        List<TcpClient> snapshot;
        lock (lockObj)
            snapshot = new List<TcpClient>(clients);

        foreach (var client in snapshot)
        {
            if (client == sender) continue; // Не отправляем обратно себе
            try
            {
                await client.GetStream().WriteAsync(data);
            }
            catch
            {
                // Клиент мог отключиться — пропускаем
            }
        }
    }
}
