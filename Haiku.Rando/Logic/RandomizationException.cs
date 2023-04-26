namespace Haiku.Rando.Logic
{
    // Denotes randomization errors that may be resolved by retrying with a new seed.
    internal class RandomizationException : System.Exception
    {
        public RandomizationException(string message) : base(message) {}
    }
}