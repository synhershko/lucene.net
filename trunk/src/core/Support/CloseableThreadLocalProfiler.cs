using System;

namespace Lucene.Net.Support
{
    /// <summary>
    /// For Debuging purposes.
    /// </summary>
    public class CloseableThreadLocalProfiler
    {
        public static bool _EnableCloseableThreadLocalProfiler = false;
        public static System.Collections.Generic.List<WeakReference> Instances = new System.Collections.Generic.List<WeakReference>();

        public static bool EnableCloseableThreadLocalProfiler
        {
            get { return _EnableCloseableThreadLocalProfiler; }
            set
            {
                _EnableCloseableThreadLocalProfiler = value;
                lock (Instances)
                    Instances.Clear();
            }
        }
    }
}