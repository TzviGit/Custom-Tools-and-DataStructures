using System;

namespace InfraTools
{
    /// <summary>
    /// This class generates non-repeating random numbers within a specified range.
    /// The generator guarantees that consecutive calls to Next() will never repeat the same number twice.
    /// Cyclic here means that when the numbers run out, then we start the process again.
    /// The Next() method has O(1) time complexity.
    /// </summary>
    public class CyclicRandomIndexGenerator
    {
        private int[] shuffledIndices; // Array to hold the shuffled indices
        private int currentIndex;      // Current index used for generating numbers
        private Random random;         // Random object used for random number generation

        private readonly object _locker = new object();

        /// <summary>
        /// Initializes a new instance of the RandomIndexGenerator class with the given range.
        /// </summary>
        /// <param name="min_Value">The minimum value of the range (inclusive).</param>
        /// <param name="max_Value">The maximum value of the range (inclusive).</param>
        /// <exception cref="ArgumentException">Thrown when min_Value is greater than max_Value.</exception>
        public CyclicRandomIndexGenerator(int min_Value, int max_Value)
        {
            if (min_Value > max_Value)
                throw new ArgumentException("min_Value must be less than or equal to max_Value.");

            int size = max_Value - min_Value + 1;
            shuffledIndices = new int[size];

            // Initialize the shuffledIndices array with indices starting from min_Value.
            for (int i = 0; i < size; i++)
            {
                shuffledIndices[i] = i + min_Value;
            }

            currentIndex = size - 1; // Set the initial currentIndex to the last element of the array.

            //use unique seed every time
            random = new Random(Guid.NewGuid().GetHashCode());
        }

        /// <summary>
        /// Generates the next non-repeating random number from the specified range.
        /// </summary>
        /// <returns>A random number within the specified range.</returns>
        public int Next()
        {
            //only one thread allowed inside at once
            lock (_locker)
            {
                // If the currentIndex is less than 0, it means all numbers within the range have been generated.
                // In that case, we need to restart the generator by setting currentIndex back to the last element.
                if (currentIndex < 0)
                {
                    currentIndex = shuffledIndices.Length - 1;
                }

                // Generate a random index within the range [0, currentIndex] to select a random number.
                int randomIndex = random.Next(0, currentIndex + 1);
                int result = shuffledIndices[randomIndex];

                // Swap the selected index with the current index to avoid repeating the number in future calls.
                Swap(currentIndex, randomIndex);

                // Decrement the currentIndex to get ready for the next call.
                currentIndex--;
                return result;
            }
        }

        /// <summary>
        /// Swaps two elements in the shuffledIndices array given their indices.
        /// </summary>
        /// <param name="ind1">The index of the first element to be swapped.</param>
        /// <param name="ind2">The index of the second element to be swapped.</param>
        void Swap(int ind1, int ind2)
        {
            int temp = shuffledIndices[ind1];
            shuffledIndices[ind1] = shuffledIndices[ind2];
            shuffledIndices[ind2] = temp;
        }
    }
}

