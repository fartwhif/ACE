using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Managers;
using ACE.Server.Network.Packets;
using System.Collections.ObjectModel;
using System.Threading;
using ACE.Server.WorldObjects;
using ACE.Database.Models.World;
using ACE.Server.Factories;

namespace ACE.Server.Network.Handlers
{
    public static class AuthenticationHandler
    {
        /// <summary>
        /// Seconds until an authentication request will timeout/expire.
        /// </summary>
        public const int DefaultAuthTimeout = 15;

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        public static void HandleConnectResponse(Session session)
        {
            if (WorldManager.WorldStatus == WorldManager.WorldStatusState.Open || session.AccessLevel > AccessLevel.Player)
            {
                DatabaseManager.Shard.GetCharacters(session.AccountId, false, result =>
                {
                    // If you want to create default characters for accounts that have none, here is where you would do it.

                    SendConnectResponse(session, result);
                });
            }
            else
            {
                session.Terminate(SessionTerminationReason.WorldClosed, new GameMessageCharacterError(CharacterError.LogonServerFull));
            }
        }
        private static void SendConnectResponse(Session session, List<Character> characters)
        {
            characters = characters.OrderByDescending(o => o.LastLoginTimestamp).ToList(); // The client highlights the first character in the list. We sort so the first character sent is the one we last logged in
            session.UpdateCharacters(characters);

            GameMessageCharacterList characterListMessage = new GameMessageCharacterList(session.Characters, session);
            GameMessageServerName serverNameMessage = new GameMessageServerName(ConfigManager.Config.Server.WorldName, PlayerManager.GetOnlineCount(), (int)ConfigManager.Config.Server.Network.MaximumAllowedSessions);
            GameMessageDDDInterrogation dddInterrogation = new GameMessageDDDInterrogation();

            session.Network.EnqueueSend(characterListMessage, serverNameMessage, dddInterrogation);
        }
    }
}
