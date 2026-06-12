namespace ACE.Server.Network.GameMessages.Messages
{
    /// <summary>
    /// Tells the client to disconnect and reconnect to a different server.
    /// Intercepted by the Veilrend Decal plugin for automatic cross-server reconnection.
    /// Without the decal plugin, the client ignores this message and the subsequent boot handles disconnect.
    /// </summary>
    public class GameMessageServerHandoff : GameMessage
    {
        /// <summary>
        /// Tells the client the player should reconnect to a different server.
        /// </summary>
        /// <param name="TargetHost">Host address of the target server (e.g., "127.0.0.1")</param>
        /// <param name="TargetPort">UDP port of the target server game protocol</param>
        /// <param name="Token">Migration token for the target server to validate the incoming connection</param>
        public GameMessageServerHandoff(string TargetHost, ushort TargetPort, string Token)
            : base(GameMessageOpcode.ServerHandoff, GameMessageGroup.UIQueue, 128)
        {
            Writer.WriteString16L(TargetHost);
            Writer.Write(TargetPort);
            Writer.WriteString16L(Token);
        }
    }
}
