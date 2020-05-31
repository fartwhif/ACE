namespace ACE.Server.Network.Enum
{
    public enum SessionTerminationPhase
    {
        Initialized = 0,
        SessionWorkCompleted = 1,
        WorldManagerWorkCompleted = 2,
        FinalTerminationStarted = 3,
        TerminationCompleted = 4
    }
}
