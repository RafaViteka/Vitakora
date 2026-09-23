using System.Windows;
using System.Windows.Controls;
using Vitakora.Client.Services;
using Vitakora.Shared;

namespace Vitakora.Client;

public partial class MainWindow : Window
{
    readonly ChatConnection _chat = new();
    ApiClient? _api;
    LoginResponse? _login;
    string? _serverUrl;

    public MainWindow()
    {
        InitializeComponent();
        PresenceBox.ItemsSource = Enum.GetValues<PresenceStatus>();
        PresenceBox.SelectedItem = PresenceStatus.Available;
        Loaded += async (_, _) => await FindServerAsync();

        _chat.MessageReceived += m => Dispatcher.Invoke(async () =>
        {
            MessagesList.Items.Add($"{m.SenderName}: {m.Text}");
            await _chat.MarkDeliveredAsync(m.Id);
            if (IsActive) await _chat.MarkReadAsync(m.Id);
            ShowPopup(m);
        });

        _chat.SessionRevoked += reason => Dispatcher.Invoke(() =>
        {
            StatusText.Text = reason;
            _login = null;
            RecipientBox.ItemsSource = null;
        });
    }

    async Task FindServerAsync()
    {
        SearchButton.IsEnabled = false;
        LoginButton.IsEnabled = false;
        WorkstationBox.ItemsSource = null;
        ServerStatusText.Text = "Buscando servidor Vitakora en la red…";
        StatusText.Text = "";

        _serverUrl = await ServerDiscovery.FindAsync();
        if (_serverUrl is null)
        {
            _api = null;
            ServerStatusText.Text = "No se encontró ningún servidor Vitakora.";
            StatusText.Text = "Comprueba que Vitakora Servidor esté instalado y encendido en un equipo de esta red.";
            SearchButton.IsEnabled = true;
            return;
        }

        try
        {
            _api = new ApiClient(_serverUrl);
            WorkstationBox.ItemsSource = await _api.GetWorkstationsAsync();
            WorkstationBox.SelectedIndex = 0;
            ServerStatusText.Text = "Conectado a Vitakora";
            LoginButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _api = null;
            ServerStatusText.Text = "Servidor encontrado, pero no responde correctamente.";
            StatusText.Text = ex.Message;
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    async void Search_Click(object sender, RoutedEventArgs e) => await FindServerAsync();

    async void Login_Click(object s, RoutedEventArgs e)
    {
        if (_api is null || _serverUrl is null)
        {
            await FindServerAsync();
            if (_api is null || _serverUrl is null) return;
        }
        if (WorkstationBox.SelectedItem is not WorkstationDto ws) return;

        try
        {
            _login = await _api.LoginAsync(new LoginRequest(UsernameBox.Text, PasswordBox.Password, ws.Id));
            if (_login is null)
            {
                StatusText.Text = "Usuario o contraseña incorrectos.";
                return;
            }

            await _chat.ConnectAsync(_serverUrl, _login.AccessToken);
            RecipientBox.ItemsSource = await _api.GetDirectoryAsync(_login.AccessToken);
            RecipientBox.SelectedIndex = 0;
            StatusText.Text = $"Conectado como {_login.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    async void Send_Click(object s, RoutedEventArgs e)
    {
        if (_login is null || RecipientBox.SelectedItem is not DirectoryItemDto target ||
            string.IsNullOrWhiteSpace(MessageBox.Text)) return;

        var text = MessageBox.Text.Trim();
        await _chat.SendAsync(new SendMessageRequest(target.Kind, target.Id, text));
        MessagesList.Items.Add($"Yo → {target.Name}: {text}");
        MessageBox.Clear();
    }

    async void PresenceBox_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (_login is not null && PresenceBox.SelectedItem is PresenceStatus p)
            await _chat.SetPresenceAsync(p);
    }

    void ShowPopup(MessageDto m)
    {
        var p = new PopupWindow(m.SenderName, m.Text);
        p.ReplyRequested += async text =>
            await _chat.SendAsync(new SendMessageRequest(RecipientKind.User, m.SenderUserId, text));
        p.Show();
    }
}
