using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using Xamarin.Essentials;
using System.Diagnostics;

namespace SINTEF.AutoActive.UI.FileSystem
{
    public static class SessionDatabase
    {
        static SessionDatabase()
        {
            var appDataDir = Xamarin.Essentials.FileSystem.AppDataDirectory;
            Debug.WriteLine($"APP DATA DIR: {appDataDir}");

            /*

            // Load the state from the storage
            var all = new SessionDirectory();
            var task = storage.LoadAll(all);
            task.Wait();
            if (!task.IsCompleted) throw new Exception("Could not load the All directory from the SessionDatabaseStorage", task.Exception);
            All = all;

            // For dynamically populated directories
            var list = new List<SessionDescriptor>();
            all.ListAllDescriptors(list);

            // Populate the recently opened list
            var byOpened = new List<SessionDescriptor>(list);
            byOpened.Sort((a, b) => b.LastOpened.CompareTo(a.LastOpened));

            RecentlyOpened = new ReadOnlySessionDirectory();
            foreach (var recent in byOpened)
            {
                RecentlyOpened.AddSessionDescriptor(recent);
            }

            // TODO: Populate other lists
            */
        }

        public static SessionDirectory All { get; }

        public static ReadOnlySessionDirectory RecentlyOpened { get; }
    }
}
