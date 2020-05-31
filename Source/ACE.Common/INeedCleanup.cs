namespace ACE.Common
{
    /// <summary>
    /// At the end of the object's lifetime cycle a deterministic cleanup operation is required to happen exactly one time.<para />
    /// This behavioral strategy is strongly recommended as it helps prevent memory leakage and other types of headaches.<para />
    /// Usage of &quot;IsReleased&quot; properties/conditionals in concrete classes is highly discouraged. - If it appears to fix something then it's shrouding the underlying problem(s)
    /// </summary>
    public interface INeedCleanup
    {
        /// <summary>
        /// Call this exactly one time at the end of the object's lifetime-cycle.
        /// </summary>
        void ReleaseResources();
    }
}
