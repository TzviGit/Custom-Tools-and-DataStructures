

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace S4C_bInfra
{
    /// <summary>
    /// This class holds a thread safe Waiable queue of messages.<br></br>
    /// Messages are added to the queue, and a worker thread takes them out of the queue and processes them.<br></br>
    /// To add items to the queue, use <see cref="Enqueue(T)"></see> method or wrap it with your own logic.<br></br>
    /// If no items are in the queue, and Take() method is called, 
    /// The thread who called Take() awaits until a new item is inserted into the queue.
    /// </summary>
    /// <typeparam name="T">The message type to be processed by the queue.</typeparam>
    public class MessageQueueProcessor<T>
    {
        const string Default_Name = "Default_name";

        /// <summary>
        /// Represents an asynchronous waitable queue, implemented using the <see cref="Channel{T}"/> class.
        /// This class provides a high-performance, non-blocking, and asynchronous queue for inter-thread communication.
        /// </summary>
        private readonly AsyncWaitableQueue<T> _messageQueue;

        bool ContinueProcessing = true;

        #region Properties


        /// <summary>
        /// Whats max level of Parallel processing. Default is 1, which means no parralel processing
        /// </summary>
        public int MaxDegreeOfParallelism { get; private set; } = 1;

        /// <summary>
        /// Recieves a msg <typeparamref name="T"/> an returns if the message passes the filter, <br/>
        /// and should be handled. If the filter blocks the msg then it wont be queued for processing.  <br/> <br/>
        /// The Default for the filter is just to allow ALL messages
        /// </summary>
        /// <returns><see langword="true"/> if message should be handled (it passes the filter), else <see langword="false"/> </returns>
        private Func<T, bool> MsgFilter { get; set; } = (msg) => true;

        public string ProcessorName = Default_Name;

        /// <summary>
        /// Max Number of messages to be enqued at once. IF more than <see cref="Max_Capacity"/> are enqueued, then we will skip the older once until we reach the Max_Capacity.
        /// /// <br/> <br/> The defaut value is <see cref="int.MaxValue"/>, which means there is no limit
        /// </summary>
        public int Max_Capacity { get; private set; } = int.MaxValue;


        #endregion

        /// <summary>
        /// This event is invoked every time a message is taken out of the queue.
        /// Registration is possible via <see cref="MessageQueueProcessor{T}"></see> constructor, 
        /// or <see cref="RegisterMessageHandler(Action{T})"></see>.
        /// </summary>
        public event Action<T> MessageHandler;


        #region Ctors
        public MessageQueueProcessor(Action<T> messageHandler, MsgProcessingOptions<T> creationOptions, string processorName = null)
        {
            var asyncQueueOptions = new AsyncQueueOptions();
            asyncQueueOptions.Capacity = creationOptions.Max_Capacity;
            asyncQueueOptions.SingleReader = true;
            asyncQueueOptions.FullMode = BoundedChannelFullMode.DropOldest;

            this._messageQueue = new AsyncWaitableQueue<T>(asyncQueueOptions);

            this.MessageHandler = messageHandler;

            if (string.IsNullOrEmpty(processorName))
            { processorName = Default_Name; }

            this.ProcessorName = processorName;
            this.MsgFilter = new Func<T, bool>(creationOptions.MsgFilter);

            this.Max_Capacity = creationOptions.Max_Capacity;
            this.MaxDegreeOfParallelism = creationOptions.MaxDegreeOfParallelism;

            Task.Run(ProcessMessages);
        }

        public MessageQueueProcessor(Action<T> messageHandler, string processorName = null)
            : this(messageHandler, new MsgProcessingOptions<T>(), processorName) //take the default Creation Options
        {

        }

        #endregion

        #region Public Methods
        /// <summary>
        /// This method adds a message to the queue, for future handling.
        /// It can be used directly or wrapped in order to add logic to enqueue.
        /// </summary>
        /// <param name="message">The message to be added to the queue.</param>
        public bool Enqueue(T message)
        {
            bool wasAdded = false;
            try
            {
                //if this processor blocks this msg (it doesn't pass the filter), or if Processor was stopped
                //then don't Enqueue the Msg
                if (ContinueProcessing && MsgFilter(message))
                {
                    wasAdded = _messageQueue.TryEnqueue(message);
                    //_messageQueue.Enqueue(message);
                }
            }
            catch (Exception ex)
            {
                C4Logger.Instance.Write(EC4LoggerLevel.ERROR, $"Exception was caught while adding messages to MessageQueue of {ProcessorName}: {ex.Message}");
            }
            return wasAdded;
        }

        /// <summary>
        /// This method allows registration of message handlers outside of the constructor.
        /// </summary>
        /// <param name="messageHandler">The handler that will be invoked after taking message from the queue.</param>
        public void RegisterMessageHandler(Action<T> messageHandler)
        {
            this.MessageHandler += messageHandler;
        }

        /// <summary>
        /// Recieves a msg <see cref="Func{T, bool}"/> which checks if the message passes the filter, <br/>
        /// and should be handled. If the filter blocks the msg then it wont be queued for processing.  <br/> <br/>
        /// The Default for the filter is just to allow ALL messages
        /// </summary>
        /// /// <param name="msgFilter">The msg filter.</param>
        public void SetMessageFilter(Func<T, bool> msgFilter)
        {
            this.MsgFilter = new Func<T, bool>(msgFilter);
        }
        #endregion


        #region Private methods

        /// <summary>
        /// This method is the main worker thread's loop. 
        /// It takes messages out of the queue, and handles them using <see langword="abstract " cref="HandleMessage(T)"></see>
        /// </summary>
        private async ValueTask ProcessMessages()
        {
            bool allowParralelProcessing = MaxDegreeOfParallelism > 1;
            ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = this.MaxDegreeOfParallelism };

            while (ContinueProcessing)
            {
                try
                {
                    // var messages_list = _messageQueue.DequeueAll(TimeSpan.FromSeconds(10));
                    var messages_list = await _messageQueue.DequeuAllAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                    if (allowParralelProcessing)
                    {
                        Parallel.ForEach(messages_list, parallelOptions, ProcessSingleMessage);
                    }
                    else //this is the default: each msg is processed in order
                    {
                        foreach (T message in messages_list)
                        {
                            ProcessSingleMessage(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // just a precaution to catch any exceptions so we dont leave the loop
                    C4Logger.Instance.Write(EC4LoggerLevel.ERROR, $"Exception was caught while processing messages from the queue with name {ProcessorName}:  {ex.Message}");
                }

            }
        }


        void ProcessSingleMessage(T message)
        {
            try
            {
                MessageHandler?.Invoke(message);
            }
            catch (Exception ex)
            {
                //If HandleMessage does not handles exceptions, they will be caught here.
                C4Logger.Instance.Write(EC4LoggerLevel.ERROR, $"Exception was caught while processing a message from the queue with name {ProcessorName}:  {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the message processing Queue thread. 
        /// </summary>
        public void StopProcessing()
        {
            ContinueProcessing = false;
            _messageQueue.Stop();
        }
        #endregion




    }

    /// <summary>
    /// This class represent the Vaious Parameters that can be used to Initialize the <see cref="MessageQueueProcessor{T}"/>
    /// </summary>
    public class MsgProcessingOptions<T>
    {
        /// <summary>
        /// Whats max level of Parallel processing. Default is 1, which means no parralel-processing
        /// </summary>
        public int MaxDegreeOfParallelism = 1;

        /// <summary>
        /// Recieves a msg <typeparamref name="T"/> an returns if the message passes the filter, <br/>
        /// and should be handled. If the filter blocks the msg then it wont be queued for processing.  <br/> <br/>
        /// The Default for the filter is just to allow ALL messages
        /// </summary>
        /// <returns><see langword="true"/> if message should be handled (it passes the filter), else <see langword="false"/> </returns>
        public Func<T, bool> MsgFilter = (msg) => true;

        /// <summary>
        /// Max Number of messages to be enqued at once. IF more than <see cref="Max_Capacity"/> are enqueued, then we will skip the older once until we reach the Max_Capacity.
        /// <br/> <br/> The defaut value is <see cref="int.MaxValue"/>, which means there is no limit
        /// </summary>
        public int Max_Capacity = int.MaxValue;

    }    

}
