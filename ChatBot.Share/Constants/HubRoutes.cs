namespace ChatBot.Share.Constants;

/// <summary>
/// SignalR Hub endpoint URL constants.
/// Both Server (MapHub) and Client (HubConnectionBuilder) must use the same route.
/// </summary>
public static class HubRoutes
{
    public const string Chat = "/hubs/chat";
}