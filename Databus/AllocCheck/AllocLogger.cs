using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SINTEF.AutoActive.Databus.AllocCheck
{
    public class AllocTrack
    {
        private int hash;

        public AllocTrack(object obj, String instInfo = "")
        {
            String name = obj.GetType().Namespace + "." + obj.GetType().Name;
            hash = obj.GetHashCode();

            AllocLogger.RegConstruct(hash, name, instInfo);
        }

        ~AllocTrack()
        {
            AllocLogger.RegDealloc(hash);
        }

    }

    public static class AllocLogger
    {
        private static readonly Dictionary<int, String> _memItems = new Dictionary<int, String>();

        public static void RegConstruct(int hash, String name, String instInfo = "")
        {
            String instName = name + "<" + instInfo + ">";
            if (_memItems.ContainsKey(hash))
            {
                Debug.WriteLine($"Multiple regs of {instName}");
            }
            else
            {
                _memItems.Add(hash, instName);
            }

        }

        public static void RegDealloc(int hash)
        {
            if (_memItems.ContainsKey(hash))
            {
                _memItems.Remove(hash);
            }
            else
            {
                Debug.WriteLine($"Unknown reg");
            }
        }

        public static void PrintRegs()
        {
            Debug.WriteLine($"+++++++++++++ Active regs");
            var keys = _memItems.Keys;
            foreach(var key in keys)
            {
                var name = _memItems[key];
                Debug.WriteLine($"{name} ");
            }
            Debug.WriteLine($"------------- End regs");
        }
    }
}
