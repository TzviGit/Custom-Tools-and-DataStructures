using System;
using System.Collections.Generic;
using System.Linq;

namespace S4C_bInfra
{
    public class MessageDistributer<TValue>
    {
        object RegisterUnregisterPadLock = new object();
        Dictionary<Action<TValue>, MessageQueueProcessor<TValue>> _UponEventDelegates = new Dictionary<Action<TValue>, MessageQueueProcessor<TValue>>();

        public void RegisterForEvents(Action<TValue> eventDelegate, MsgProcessingOptions<TValue> processingOptions = null)
        {
            if (processingOptions == null)
            {
                processingOptions = new MsgProcessingOptions<TValue>(); // use Default options
            }

            lock (RegisterUnregisterPadLock)
            {
                if (!_UponEventDelegates.ContainsKey(eventDelegate))
                {
                    var temp_DictionaryCopy = _UponEventDelegates.ToDictionary(keySelector: kvp => kvp.Key, elementSelector: kvp => kvp.Value);
                    temp_DictionaryCopy.Add(eventDelegate, new MessageQueueProcessor<TValue>(eventDelegate, processingOptions));

                    _UponEventDelegates = temp_DictionaryCopy;
                }
            }
        }

        public void UnRegisterForEvents(Action<TValue> eventDelegate)
        {
            lock (RegisterUnregisterPadLock)
            {
                if (_UponEventDelegates.TryGetValue(eventDelegate, out var message_queue))
                {                   
                    var temp_DictionaryCopy = _UponEventDelegates.ToDictionary(keySelector: kvp => kvp.Key, elementSelector: kvp => kvp.Value);

                    temp_DictionaryCopy.Remove(eventDelegate);

                    _UponEventDelegates = temp_DictionaryCopy;

                    if (message_queue != null)
                    {
                        message_queue.StopProcessing();
                    }
                }
            }
        }


        /// <summary>
        /// This internal function is to be called by inheriting classes 
        /// </summary>
        /// <param name="updated_item"></param>
        private void InvokeDelegates(TValue updated_item)
        {

            try
            {
                foreach (var message_queue in _UponEventDelegates.Values)
                {
                    message_queue.Enqueue(updated_item);
                }
            }
            catch (Exception ex)
            {

            }

        }

        public void DistributeEvent(TValue item)
        {
            InvokeDelegates(item);
        }
    }

}
