using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Logic
{
    /// <summary>
    /// Pins sample messages into the collection the overlay shows. The messages are put there directly, never through the
    /// message stream of <see cref="KeysController"/>, so they are not faded out and removed like typed ones.
    /// Like the collection it works on, it belongs to the UI thread.
    /// </summary>
    public class PreviewService : IPreviewService
    {
        const string SampleProcessName = "Carnac";
        const string SampleShortcutName = "Command Palette";
        const string SampleText = "Preview: type anything";

        readonly ObservableCollection<Message> messages;
        readonly ProcessInfo sampleProcess;
        readonly List<Message> pinned = new List<Message>();
        int visitors;

        public PreviewService(ObservableCollection<Message> messages, ProcessInfo sampleProcess)
        {
            if (messages == null) throw new ArgumentNullException("messages");
            if (sampleProcess == null) throw new ArgumentNullException("sampleProcess");

            this.messages = messages;
            this.sampleProcess = sampleProcess;
        }

        public IDisposable Show()
        {
            if (visitors == 0)
                Pin();

            visitors++;
            return new Visit(this);
        }

        /// <summary>The process the sample messages come from: Carnac itself, with its own icon when that can be read.</summary>
        public static ProcessInfo CreateSampleProcess()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    return new ProcessInfo(SampleProcessName, IconUtilities.GetProcessIconAsImageSource(process.MainModule.FileName));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Carnac: the sample popups have no icon: {0}", ex);
                return new ProcessInfo(SampleProcessName);
            }
        }

        /// <summary>The sample messages: a shortcut with its description, and a line of plain text.</summary>
        public static IList<Message> CreateSampleMessages(ProcessInfo process)
        {
            if (process == null) throw new ArgumentNullException("process");

            var shortcut = new KeyPress(
                process,
                new InterceptKeyEventArgs(Keys.P, KeyDirection.Down, false, true, true),
                false,
                new[] { "Ctrl", "Shift", "P" });
            var typed = SampleText.Select(c => new KeyPress(
                process,
                new InterceptKeyEventArgs(ToKey(c), KeyDirection.Down, false, false, false),
                false,
                new[] { c.ToString() }));

            return new List<Message>
            {
                new Message(typed, new KeyShortcut(string.Empty)),
                new Message(new[] { shortcut }, new KeyShortcut(SampleShortcutName), true)
            };
        }

        static Keys ToKey(char c)
        {
            if (c == ' ')
                return Keys.Space;

            var key = (Keys)char.ToUpperInvariant(c);
            return key >= Keys.A && key <= Keys.Z ? key : Keys.None;
        }

        void Pin()
        {
            foreach (var message in CreateSampleMessages(sampleProcess))
            {
                pinned.Add(message);
                messages.Add(message);
            }
        }

        void Unpin()
        {
            foreach (var message in pinned)
            {
                // By reference: the messages are equal to no other message, but nothing else may go with them.
                var index = IndexOfReference(message);
                if (index >= 0)
                    messages.RemoveAt(index);
            }
            pinned.Clear();
        }

        int IndexOfReference(Message message)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (ReferenceEquals(messages[i], message))
                    return i;
            }
            return -1;
        }

        void Leave()
        {
            visitors--;
            if (visitors == 0)
                Unpin();
        }

        class Visit : IDisposable
        {
            readonly PreviewService owner;
            bool disposed;

            public Visit(PreviewService owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                owner.Leave();
            }
        }
    }
}
