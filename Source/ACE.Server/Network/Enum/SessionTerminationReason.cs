namespace ACE.Server.Network.Enum
{
    public enum SessionTerminationReason
    {
        None,
        PacketHeaderDisconnect,
        AccountSelectCallbackException,
        NetworkTimeout,
        /// <summary>
        /// a trusted packet had PacketHeaderFlags.NetErrorDisconnect header flag
        /// </summary>
        ClientSentNetworkErrorDisconnect,
        AccountBooted,
        BadHandshake,
        PongSentClosingConnection,
        NotAuthorizedNoPasswordOrGlsTicketIncludedInLoginReq,
        NotAuthorizedAccountNotFound,
        AccountInUse,
        NotAuthorizedPasswordMismatch,
        NotAuthorizedGlsTicketNotImplementedToProcLoginReq,
        /// <summary>
        /// The client connection is no longer able to send us packets with encrypted CRC
        /// </summary>
        ClientConnectionFailure,
        SendToSocketException,
        WorldClosed,
        AbnormalSequenceReceived,
        AccountLoggedInAgain
    }
    public static class SessionTerminationReasonHelper
    {
        public static readonly string[] SessionTerminationReasonDescriptions =
        {
            "",
            "PacketHeader Disconnect",
            "AccountSelectCallback threw an exception.",
            "Network Timeout",
            "client sent network error disconnect",
            "Account Booted",
            "Bad handshake",
            "Pong sent, closing connection.",
            "Not Authorized: No password or GlsTicket included in login request",
            "Not Authorized: Account Not Found",
            "Account In Use: Found another session already logged in for this account.",
            "Not Authorized: Password does not match.",
            "Not Authorized: GlsTicket is not implemented to process login request",
            "Client connection failure",
            "MainSocket.SendTo exception occured",
            "World is closed",
            "Client supplied an abnormal sequence",
            "The account has started a new session"
        };
        public static string GetDescription(this SessionTerminationReason reason)
        {
            if ((int)reason > SessionTerminationReasonDescriptions.Length - 1)
            {
                return "<reason>";
            }
            return SessionTerminationReasonDescriptions[(int)reason];
        }
    }
}
