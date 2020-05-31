using ACE.Common;
using ACE.Server.Network.Enum;
using System;

namespace ACE.Server.Network
{
    public class SessionTerminationDetails : INeedCleanup
    {
        public string ExtraReason { get; set; } = null;
        public SessionTerminationPhase TerminationStatus { get; set; } = SessionTerminationPhase.Initialized;
        public SessionTerminationReason Reason { get; set; } = SessionTerminationReason.None;
        public long TerminationStartTicks { get; set; } = DateTime.UtcNow.Ticks;
        public long TerminationEndTicks { get; set; } = new DateTime(DateTime.UtcNow.Ticks).AddSeconds(2).Ticks;
        public DateTime WorldManagerWorkCompletedAt { get; set; } = DateTime.MaxValue;
        public TimeSpan FinalTerminationDelay = TimeSpan.Zero;
        public Action FinalTerminationAction { get; set; } = null;

        public bool DoFinalTermination()
        {
            if (TerminationStatus == SessionTerminationPhase.WorldManagerWorkCompleted && DateTime.Now - WorldManagerWorkCompletedAt > FinalTerminationDelay)
            {
                TerminationStatus = SessionTerminationPhase.FinalTerminationStarted;
                FinalTerminationAction?.Invoke();
                TerminationStatus = SessionTerminationPhase.TerminationCompleted;
                return true;
            }
            return false;
        }

        public void ReleaseResources()
        {
            ExtraReason = null;
            FinalTerminationAction = null;
        }
    }
}
