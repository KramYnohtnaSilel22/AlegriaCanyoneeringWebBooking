// BatchCodeHub.cs
using Microsoft.AspNetCore.SignalR;

public class BatchCodeHub : Hub
{
    // Send batch code to all connected clients except the sender
    public async Task SendBatchCode(string batchCode)
    {
        await Clients.Others.SendAsync("ReceiveBatchCode", batchCode);
    }
}
