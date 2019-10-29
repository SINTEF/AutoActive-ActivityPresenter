using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SINTEF.AutoActive.Databus.AllocCheck
{
    public class AllocTrack
    {
        private readonly int hash;

        public AllocTrack(object obj, String instInfo = "")
        {
            String name = obj.GetType().Namespace + "." + obj.GetType().Name + "<" + instInfo + ">";
            hash = obj.GetHashCode();

            AllocLogger.RegConstruct(hash, name);
        }

        ~AllocTrack()
        {
            AllocLogger.RegDealloc(hash);
        }
    }

    public static class AllocLogger
    {
        private static readonly Dictionary<int, String> _memItems = new Dictionary<int, String>();
        private static Mutex _transactionMutex = new Mutex();

        public static void ResetAll()
        {
            lock (_transactionMutex)
            {
                _memItems.Clear();
            }
        }

        public static int GetTotalRegs()
        {
            lock (_transactionMutex)
            {
                return _memItems.Count;
            }
        }

        public static void RegConstruct(int hash, String name)
        {
            String locName = String.Copy(name);

            lock (_transactionMutex)
            {
                if (_memItems.ContainsKey(hash))
                {
                    var prevName = _memItems[hash];
                    if (prevName.Length < locName.Length)
                    {
                        _memItems[hash] = locName;
                    }
                }
                else
                {
                    _memItems.Add(hash, locName);
                }
            }

        }

        public static void RegDealloc(int hash)
        {
            lock (_transactionMutex)
            {
                if (_memItems.ContainsKey(hash))
                {
                    _memItems.Remove(hash);
                }
            }
        }

        public static void PrintRegs()
        {
            lock (_transactionMutex)
            {
                Debug.WriteLine($"+++++++++++++ Active regs <{GetTotalRegs()}>");
                var keys = _memItems.Keys;
                foreach (var key in keys)
                {
                    var name = _memItems[key];
                    Debug.WriteLine($"{name} ");
                }
                Debug.WriteLine($"------------- End regs <{GetTotalRegs()}>");
            }
        }

        public static long GetTotalMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return GC.GetTotalMemory(true);
        }

    }
}
