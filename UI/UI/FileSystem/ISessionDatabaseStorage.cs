using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.UI.FileSystem
{
    public interface ISessionDatabaseStorage
    {
        Task LoadAll(SessionDirectory all);
        Task SaveAll(ReadOnlySessionDirectory all);
    }
}
