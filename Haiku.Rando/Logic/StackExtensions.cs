using System.Collections.Generic;

namespace Haiku.Rando.Logic
{
    internal static class StackExtensions
    {
        public static bool TryPop<T>(this Stack<T> s, out T val)
        {
            if (s.Count == 0)
            {
                val = default;
                return false;
            }
            val = s.Pop();
            return true;
        }

        public static bool TryPeek<T>(this Stack<T> s, out T val)
        {
            if (s.Count == 0)
            {
                val = default;
                return false;
            }
            val = s.Peek();
            return true;
        }

        public static bool TryPopOperands<T, U>(this Stack<object> s, out T left, out U right)
        {
            if (s.TryPop(out var leftObj) && leftObj is T leftVal && 
                s.TryPop(out var rightObj) && rightObj is U rightVal)
            {
                left = leftVal;
                right = rightVal;
                return true;
            }
            left = default;
            right = default;
            return false;
        }
    }
}