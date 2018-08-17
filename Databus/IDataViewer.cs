using System;
using System.Collections.Generic;
using System.Text;

namespace Databus
{
    public delegate void DataViewWasChangedHandler();
    //public delegate void DataViewChanged<T>(Span<T> data);

    public interface IDataViewer
    {
        event DataViewWasChangedHandler Changed;
        //event DataViewChanged<T> Updated;
        //Span<T> GetCurrent<T>();
        Span<float> GetCurrentFloat();
    }
}
