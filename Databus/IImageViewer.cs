using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus
{
    public interface IImageViewer : IDataViewer
    {
        Task SetSize(uint width, uint height);
    }
}
