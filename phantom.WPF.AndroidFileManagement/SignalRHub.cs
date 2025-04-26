using Microsoft.AspNetCore.SignalR;

public class MyHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
public class MessageSender
{
    private readonly IHubContext<MyHub> _hubContext;

    public MessageSender(IHubContext<MyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendMessage(string message, string? user = "API", string? par1 = null, string? par2 = null, string? par3 = null
        , string? par4 = null)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", user, message, par1, par2, par3, par4);
    }
}