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

    public async Task SendNotificationToAll(string message)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
    }
}