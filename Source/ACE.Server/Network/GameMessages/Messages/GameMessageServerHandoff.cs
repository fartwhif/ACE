using System;
using Newtonsoft.Json;

namespace ACE.Server.Network.GameMessages.Messages
{
    /// <summary>
    /// Data contract serialized to JSON, base64-encoded, and written as ANSI bytes
    /// into the ServerHandoff message payload.
    /// </summary>
    public class HandoffPayload
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }

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
            : base(GameMessageOpcode.ServerHandoff, GameMessageGroup.UIQueue, 256)
        {
            var payload = new HandoffPayload
            {
                Host = TargetHost,
                Port = TargetPort,
                Token = Token
            };

            string json = JsonConvert.SerializeObject(payload);
            byte[] base64Bytes = System.Text.Encoding.ASCII.GetBytes(
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)));

            Writer.Write(base64Bytes);
            Writer.Write((byte)0); // null terminator for safety
        }
    }
}
