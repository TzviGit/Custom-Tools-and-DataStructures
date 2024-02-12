
using System.Threading;

namespace S4C_bInfra
{
    /****    made by Tzvi   ***/




    // based on stackoverflow

    /// <summary>
    /// Thread-safe implementation for a bool, copys exactly the behaviour of a bool
    /// </summary>
    public struct ThreadSafeBool
    {

        /// <summary>
        /// this is the underlying int which we can change and read with Interlocked funcs
        /// </summary>
        private int _internal;

        public bool Value
        {
            // get => (Interlocked.Read(ref _internal) == 1);

            //dont need interlocked for read because since we use Interlocked for write this gaurentees that any reads see the updated value.
            // (remember that reading an Int is always an Atomic Operation)
            get => _internal == 1;

            set
            {
                //only do a write if new_value is different. This avoids unnecessary use of memmory-Barriors
                int new_internal_value = value ? 1 : 0;
                if (_internal != new_internal_value)
                {
                    Interlocked.Exchange(ref _internal, new_internal_value);
                }
            }
        }

        public ThreadSafeBool(bool value = false) //init to false
        {
            _internal = (value ? 1 : 0);
        }

        /// <summary>
        /// Compares this ThreadSafeBool to the ifComparedTo param, only if they're equal Then change curent value to newVal. 
        /// Behaves  like Interlocked.CompareExchange
        /// </summary>
        /// <param name="newVal">the new value to set to this IF current value equals the second param</param>
        /// <param name="ifComparedTo">if current value of this equals ifComparedTo THEN we change this to newVal</param>
        /// <returns>Returns the ORIGINAL value of this</returns>
        public bool CompareAndExchange(bool newVal, bool ifComparedTo)
        {
            int newInternalAsInt = newVal ? 1 : 0;
            int compareToAsInt = ifComparedTo ? 1 : 0;
            return (Interlocked.CompareExchange(ref _internal, newInternalAsInt, compareToAsInt) == 1);
        }

        public static bool operator ==(ThreadSafeBool b1, ThreadSafeBool b2) => b1.Value == b2.Value;

        public static bool operator ==(ThreadSafeBool b1, bool boolVal) => b1.Value == boolVal;

        public static bool operator !=(ThreadSafeBool b1, bool boolVal) => b1.Value == boolVal;
        public static bool operator !=(ThreadSafeBool b1, ThreadSafeBool b2) => b1.Value != b2.Value;

        public static implicit operator ThreadSafeBool(bool v)
        {
            ThreadSafeBool b = new ThreadSafeBool(v);
            return b;
        }

        public static implicit operator bool(ThreadSafeBool b) => b.Value;

        public override bool Equals(object other)
        {

            return this.Value == ((ThreadSafeBool)other).Value;
        }

        public override int GetHashCode()
        {
            bool b = this.Value;
            return b.GetHashCode();
        }

    }
}
