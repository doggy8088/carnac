using System.Diagnostics;

namespace Carnac.Logic
{
    /// <summary>Tells which process the user is typing into. A seam so the key pipeline can be tested without a desktop.</summary>
    public interface IProcessProvider
    {
        /// <summary>
        /// The process that owns the foreground window, or null when there is none. The process can exit at any moment,
        /// so reading its properties may throw.
        /// </summary>
        Process GetAssociatedProcess();
    }

    /// <summary>The real thing: asks Windows for the foreground window (see <see cref="AssociatedProcessUtilities"/>).</summary>
    public sealed class SystemProcessProvider : IProcessProvider
    {
        public Process GetAssociatedProcess()
        {
            return AssociatedProcessUtilities.GetAssociatedProcess();
        }
    }
}
