namespace Carnac.Logic
{
    /// <summary>The application that owns the window the user is working in.</summary>
    public sealed class ForegroundProcess
    {
        public ForegroundProcess(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; private set; }

        /// <summary>Executable name without extension, for example "notepad".</summary>
        public string Name { get; private set; }
    }

    public interface IForegroundSource
    {
        /// <summary>The process of the foreground window, or null when there is none or it cannot be determined.</summary>
        ForegroundProcess GetForegroundProcess();
    }
}
