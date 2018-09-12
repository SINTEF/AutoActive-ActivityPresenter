using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.UI.FileSystem
{
    public class ReadOnlySessionDirectory
    {
        readonly LinkedList<ReadOnlySessionDirectory> directories;
        readonly LinkedList<SessionDescriptor> sessions;

        internal ReadOnlySessionDirectory()
        {
            
        }

        internal void ListAllDescriptors(List<SessionDescriptor> list)
        {
            foreach (var directory in directories)
            {
                directory.ListAllDescriptors(list);
            }
            list.AddRange(sessions);
        }

        internal void AddSessionDescriptor(SessionDescriptor session, bool first = false)
        {
            if (first) sessions.AddFirst(session);
            else sessions.AddLast(session);
            // FIXME: Trigger event
        }
    }

    public class SessionDirectory : ReadOnlySessionDirectory
    {
        internal SessionDirectory()
        {

        }
    }
}
