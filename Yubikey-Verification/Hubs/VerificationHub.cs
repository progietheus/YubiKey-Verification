using Microsoft.AspNetCore.SignalR;

namespace Yubikey_Verification.Hubs
{
    public class VerificationHub : Hub
    {
        public async Task JoinVerificationGroup(string jti)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, jti);
            // Send confirmation to the client
            await Clients.Caller.SendAsync("JoinedGroup", new { Group = jti });
        }

        public async Task LeaveVerificationGroup(string jti)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, jti);
        }

        // Add connection logging
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}. Exception: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}