using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SINTEF.AutoActive.UI.Helpers
{
    public static class NotifyPropertyChangedExtensions
    {
        public static bool SetProperty<T>(this INotifyPropertyChanged owner, ref T property, T value, PropertyChangedEventHandler handler, [CallerMemberName] string propertyName = null)
        {
            // Check if the value has changed
            if (EqualityComparer<T>.Default.Equals(property, value)) return false;

            property = value;
            handler?.Invoke(owner, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
