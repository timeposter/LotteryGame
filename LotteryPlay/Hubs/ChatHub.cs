using Microsoft.AspNetCore.SignalR;

namespace LotteryPlay.Hubs
{
    public class ChatHub : Hub
    {
        public const string AdminGroup = "AdminGroup";

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await Clients.Group(AdminGroup).SendAsync("UserOnline", Context.ConnectionId);
        }

        public async Task JoinAdmin()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        public async Task SendToAdmin(string msg)
        {
            await Clients.Group(AdminGroup).SendAsync("ReceiveMsg", Context.ConnectionId, "访客", msg);
        }

        public async Task ReplyUser(string userConnId, string msg)
        {
            await Clients.Client(userConnId).SendAsync("ReceiveMsg", "客服", msg);
        }
    }
}