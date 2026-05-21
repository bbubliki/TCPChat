// ╔══════════════════════════════════════════╗
// ║      TCP ЧАТ — КЛИЕНТ (логика)           ║
// ║  .NET MAUI, файл: MainPage.xaml.cs       ║
// ╚══════════════════════════════════════════╝

using System.Net.Sockets;
using System.Text;

namespace ChatCLient;

public partial class MainPage : ContentPage
{
    TcpClient? _client;
    NetworkStream? _stream;
    string _myName = "Аноним";
    bool _isConnected = false;

    public MainPage()
    {
        InitializeComponent();
    }

    // ── Кнопка "Войти" ──────────────────────────────────
    private async void OnConnectClicked(object sender, EventArgs e)
    {
        if (_isConnected)
        {
            Disconnect();
            return;
        }

        string ip = ServerEntry.Text?.Trim() ?? "127.0.0.1";
        _myName = NameEntry.Text?.Trim() is { Length: > 0 } n ? n : "Аноним";

        try
        {
            AddSystemMessage($"Подключаемся к {ip}:5000...");

            _client = new TcpClient();
            await _client.ConnectAsync(ip, 5000);
            _stream = _client.GetStream();

            _isConnected = true;
            ConnectBtn.Text = "Выйти";
            ConnectBtn.BackgroundColor = Color.FromArgb("#F44336");
            MessageEntry.IsEnabled = true;
            SendBtn.IsEnabled = true;

            AddSystemMessage($"✅ Вы вошли как «{_myName}»");

            // Запускаем фоновое чтение сообщений
            _ = Task.Run(ReceiveLoopAsync);
        }
        catch (Exception ex)
        {
            AddSystemMessage($"❌ Ошибка подключения: {ex.Message}");
        }
    }

    // ── Кнопка "Отправить" ───────────────────────────────
    private async void OnSendClicked(object sender, EventArgs e)
    {
        await SendMessageAsync();
    }

    private async Task SendMessageAsync()
    {
        if (_stream == null || !_isConnected) return;

        string text = MessageEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        // Формат: "Ник: сообщение"
        string fullMessage = $"{_myName}: {text}";
        byte[] data = Encoding.UTF8.GetBytes(fullMessage);

        try
        {
            await _stream.WriteAsync(data);
            // Показываем своё сообщение справа (синий пузырь)
            AddMyMessage(text);
            MessageEntry.Text = "";
        }
        catch (Exception ex)
        {
            AddSystemMessage($"⚠️ Ошибка отправки: {ex.Message}");
        }
    }

    // ── Бесконечный приём сообщений от сервера ───────────
    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[4096];

        try
        {
            while (_isConnected && _stream != null)
            {
                int bytesRead = await _stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // UI обновляем в главном потоке
                MainThread.BeginInvokeOnMainThread(() =>
                    AddOtherMessage(message));
            }
        }
        catch
        {
            // Соединение разорвано
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AddSystemMessage("🔴 Соединение закрыто");
                Disconnect();
            });
        }
    }

    // ── Отключение ──────────────────────────────────────
    private void Disconnect()
    {
        _isConnected = false;
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;

        ConnectBtn.Text = "Войти";
        ConnectBtn.BackgroundColor = Color.FromArgb("#4CAF50");
        MessageEntry.IsEnabled = false;
        SendBtn.IsEnabled = false;
    }

    // ── Вспомогательные методы для добавления сообщений ─

    // Своё сообщение — справа, синее
    void AddMyMessage(string text)
    {
        var bubble = new Label
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.End,
            MaximumWidthRequest = 280
        };
        ApplyBubbleStyle(bubble);
        MessagesStack.Add(bubble);
        ScrollToBottom();
    }

    // Чужое сообщение — слева, серое
    void AddOtherMessage(string text)
    {
        var bubble = new Label
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Colors.Black,
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.Start,
            MaximumWidthRequest = 280
        };
        ApplyBubbleStyle(bubble);
        MessagesStack.Add(bubble);
        ScrollToBottom();
    }

    // Системное сообщение — по центру, курсивом
    void AddSystemMessage(string text)
    {
        MessagesStack.Add(new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Italic,
            TextColor = Colors.Gray,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Center
        });
        ScrollToBottom();
    }

    static void ApplyBubbleStyle(Label label)
    {
        // Скруглённые углы через обёртку
        label.MaximumWidthRequest = 280;
    }

    void ScrollToBottom()
    {
        // Прокручиваем вниз после добавления сообщения
        _ = MessagesScroll.ScrollToAsync(0, MessagesStack.Height, animated: true);
    }
}