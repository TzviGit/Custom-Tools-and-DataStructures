using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace AsyncInfra
{
   

    /// <summary>
    /// Represents an asynchronous waitable queue, implemented using the <see cref="Channel{T}"/> class.
    /// This class provides a high-performance, non-blocking, and asynchronous queue for inter-thread communication.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    public class AsyncWaitableQueue<T>
    {

        /// <summary>
        /// Represents the underlying channel used for implementing the asynchronous waitable queue.
        /// The <see cref="Channel{T}"/> class provides a non-blocking, efficient mechanism for enqueueing and dequeueing elements.
        /// </summary>
        private readonly Channel<T> _innerChannel;


        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncWaitableQueue_Old{T}"/> class with the specified options.
        /// </summary>
        /// <param name="asyncQueueOptions">Options for configuring the behavior of the queue.</param>
        public AsyncWaitableQueue(AsyncQueueOptions asyncQueueOptions)
        {
            // Check if the queue has a capacity bound
            bool isCapacityBound = asyncQueueOptions.Capacity < int.MaxValue;

            if (isCapacityBound)
            {
                // Bounded channel with specified capacity
                BoundedChannelOptions boundedChannelOptions = new BoundedChannelOptions(asyncQueueOptions.Capacity)
                {
                    AllowSynchronousContinuations = false,
                    FullMode = asyncQueueOptions.FullMode,
                    SingleWriter = asyncQueueOptions.SingleWriter,
                    SingleReader = asyncQueueOptions.SingleReader
                };

                // Create a bounded channel with the specified options
                _innerChannel = Channel.CreateBounded<T>(boundedChannelOptions);
            }
            else
            {
                // Unbounded channel
                UnboundedChannelOptions unboundedChannelOptions = new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = asyncQueueOptions.SingleReader,
                    SingleWriter = asyncQueueOptions.SingleWriter
                };

                // Create an unbounded channel with the specified options
                _innerChannel = Channel.CreateUnbounded<T>(unboundedChannelOptions);
            }
        }


        #region Enqueue Methods


        /// <summary>
        /// Asynchronously adds an item to the end of the queue.
        /// This method utilizes the non-blocking and asynchronous nature of the underlying <see cref="Channel{T}"/>.
        /// </summary>
        /// <param name="data">The data to enqueue.</param>
        /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
        /// <returns>A task representing the asynchronous operation. The task result indicates whether the enqueue operation was successful.</returns>
        public async ValueTask<bool> EnqueueAsync(T data, TimeSpan timeout)
        {
            // Create a cancellation token source with the specified timeout
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    // Asynchronously write the data to the channel using a cancellation token
                    await _innerChannel.Writer.WriteAsync(data, cts.Token);

                    // Return true to indicate that the enqueue operation was successful
                    return true;
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred: Catch the OperationCanceledException and handle it
                    return false;
                }
            }
        }



        /// <summary>
        /// Tries to add an item to the end of the queue synchronously within the specified timeout.
        /// This method provides a non-blocking alternative to synchronous enqueue operations.
        /// </summary>
        /// <param name="data">The data to enqueue.</param>
        /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
        /// <returns>True if the enqueue operation was successful within the specified timeout; otherwise, false.</returns>
        public async Task<bool> TryEnqueue(T data, TimeSpan timeout)
        {
            // Use the asynchronous EnqueueAsync method with the specified timeout
            return await EnqueueAsync(data, timeout);
        }



        /// <summary>
        /// Tries to add an item to the end of the queue without waiting.
        /// This method offers a non-blocking alternative to synchronous enqueue operations.
        /// </summary>
        /// <param name="data">The data to enqueue.</param>
        /// <returns>True if the enqueue operation was successful; otherwise, false.</returns>
        public bool TryEnqueue(T data)
        {
            // Use the non-blocking TryWrite operation of the underlying Channel<T> to attempt enqueueing
            return _innerChannel.Writer.TryWrite(data);
        }


        #endregion

        #region Dequeue Methods



        /// <summary>
        /// Asynchronously removes and returns an item from the beginning of the queue.
        /// This method takes advantage of the non-blocking and asynchronous nature of the underlying <see cref="Channel{T}"/> for efficient dequeue operations.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the result of the dequeue operation.</returns>
        public async ValueTask<DequeAsyncResult<T>> DequeueAsync(TimeSpan timeout)
        {
            // Create a cancellation token source with the specified timeout
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    // If the channel is ready, asynchronously read the data
                    var data = await _innerChannel.Reader.ReadAsync(cts.Token);

                    // Return a DequeAsyncResult with success and the dequeued data
                    return new DequeAsyncResult<T> { Success = true, DataResult = data };

                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred: Catch the OperationCanceledException and handle it
                }

                // Return a DequeAsyncResult with failure (timeout or no data available)
                return new DequeAsyncResult<T> { Success = false, DataResult = default };
            }
        }

        /// <summary>
        /// Asynchronously dequeues an item from the queue.
        /// The method asynchronously waits for an item to become available in the channel.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> representing the asynchronous operation.
        /// The task result contains the dequeued item from the queue.
        /// </returns>
        public async ValueTask<T> DequeueAsync()
        {
            // Use the ReadAsync method of the inner channel's reader to asynchronously wait for an item to become available.
            return await _innerChannel.Reader.ReadAsync();
        }



        /// <summary>
        /// Tries to remove and return an item from the beginning of the queue without waiting.
        /// This method leverages the non-blocking <see cref="ChannelReader{T}.TryRead(out T)"/> operation of the underlying <see cref="Channel{T}"/>.
        /// </summary>
        /// <param name="result">When this method returns, contains the dequeued data if the operation was successful, or the default value of T if the operation failed.</param>
        /// <returns>True if the dequeue operation was successful; otherwise, false.</returns>
        public bool TryDequeue(out T result)
        {
            // Use the non-blocking TryRead operation of the underlying Channel<T> to attempt dequeueing
            return _innerChannel.Reader.TryRead(out result);
        }



        /// <summary>
        /// Tries to remove and return an item from the beginning of the queue within the specified timeout.
        /// This method combines the asynchronous <see cref="DequeueAsync"/> operation and a timeout mechanism for efficient, non-blocking dequeue operations.
        /// </summary>
        /// <param name="result">When this method returns, contains the dequeued data if the operation was successful, or the default value of T if the operation failed.</param>
        /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
        /// <returns>True if the dequeue operation was successful within the specified timeout; otherwise, false.</returns>
        public bool TryDequeue(out T result, TimeSpan timeout)
        {
            result = default;

            try
            {
                // Attempt to dequeue an item asynchronously with the specified timeout
                var valueTask = DequeueAsync(timeout);

                // Wait for the completion of the asynchronous operation with a timeout
                if (valueTask.AsTask().Wait(timeout))
                {
                    // Retrieve the dequeued data from the asynchronous operation
                    result = valueTask.Result.DataResult;

                    // Return true to indicate that the dequeue operation was successful
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout occurred: Set the result to the default value of T and return false
            }

            return false;
        }


        /// <summary>
        /// Asynchronously dequeues all available items from the queue within the specified timeout.
        /// This method efficiently utilizes the underlying <see cref="Channel{T}"/> for non-blocking and asynchronous dequeuing of multiple items.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of dequeued items.</returns>
        public async ValueTask<List<T>> DequeuAllAsync(TimeSpan timeout)
        {
            // Create a list to store the dequeued items
            List<T> result = new List<T>();

            // Attempt to dequeue the first item with the specified timeout
            var firstVal = await DequeueAsync(timeout);

            // Check if the first dequeue operation was successful
            if (firstVal.Success)
            {
                // Add the data from the first successful dequeue to the result list
                result.Add(firstVal.DataResult);
            }

            // Continue dequeuing items while the TryDequeue method succeeds
            while (TryDequeue(out var data))
            {
                // Add the dequeued data to the result list
                result.Add(data);
            }

            // Return the list containing all dequeued items
            return result;
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Options for configuring the behavior of the <see cref="AsyncWaitableQueue{T}"/>.
    /// </summary>
    public class AsyncQueueOptions
    {
        /// <summary>
        /// Gets or sets the maximum capacity of the queue. Default is int.MaxValue.
        /// </summary>
        public int Capacity { get; set; } = int.MaxValue;

        /// <summary>
        /// <see langword="true"/> if writers to the channel guarantee that there will only ever be at most one write operation
        /// at a time; <see langword="false"/> if no such constraint is guaranteed.
        /// </summary>
        /// <remarks>
        /// If true, the channel may be able to optimize certain operations based on knowing about the single-writer guarantee.
        /// The default is false.
        /// </remarks>
        public bool SingleWriter { get; set; } = false;

        /// <summary>
        /// <see langword="true"/> if readers from the channel guarantee that there will only ever be at most one read operation
        /// at a time; <see langword="false"/> if no such constraint is guaranteed.
        /// </summary>
        /// <remarks>
        /// If true, the channel may be able to optimize certain operations based on knowing about the single-reader guarantee.
        /// The default is false.
        /// </remarks>
        public bool SingleReader { get; set; } = false;

        /// <summary>
        /// Gets or sets the behavior when the queue is full. Default is DropOldest.
        /// </summary>
        public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;
    }

    /// <summary>
    /// Represents the result of a dequeue operation.
    /// </summary>
    public class DequeAsyncResult<T>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the dequeue operation was successful.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Gets or sets the dequeued data.
        /// </summary>
        public T DataResult { get; set; } = default;
    }

    #endregion
}
