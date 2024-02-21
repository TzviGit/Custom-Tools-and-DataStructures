using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra
{
    /// <summary>
    /// This class manages the unique ID generation for anything
    /// </summary>
    public class UniqueIDManager
    {
        object SYNC_BLOCK = new object();

        SortedSet<int> _AvailableIDs_container = new SortedSet<int>();

        int m_MAX_ID;
        int m_Min_ID;


        // This is used so we can to Lazy initialazation of the container. 
        const int Capacity_Step = 1000;
        private int Start_Id_For_next_Capacity_Factoring;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="Max_id">Max allowed ID</param>
        /// <param name="include_ID_zero">Is lowest ID zero, or do we start from 1</param>
        public UniqueIDManager(int Min_id, int Max_id)
        {
            if (Min_id > Max_id)
            {
                throw new ArgumentOutOfRangeException("Min_id must be less than Max_id!");
            }

            m_Min_ID = Min_id;
            m_MAX_ID = Max_id;

            //one time init for Start_Id_For_next_Capacity_Factoring
            Start_Id_For_next_Capacity_Factoring = m_Min_ID;

            VerifyContainerCapacity();

        }

        public void SetIDAsInUse(int ID)
        {
            lock (SYNC_BLOCK)
            {
                if (ID <= m_MAX_ID && ID >= m_Min_ID)
                {
                    _AvailableIDs_container.Remove(ID);
                }
            }
        }


        public int GetNextAvailableID()
        {
            lock (SYNC_BLOCK)
            {
                VerifyContainerCapacity();

                var lowest_available_id = _AvailableIDs_container.Min;
                _AvailableIDs_container.Remove(lowest_available_id);
                return lowest_available_id;
            }
        }

        /// <summary>
        /// This function is for Lzy allocation of space for the container
        /// </summary>
        private void VerifyContainerCapacity()
        {
            lock (SYNC_BLOCK)
            {
                if (_AvailableIDs_container.Count == 0)
                {
                    if (Start_Id_For_next_Capacity_Factoring < m_MAX_ID)
                    {
                        var max_allowed_added = (m_MAX_ID - Start_Id_For_next_Capacity_Factoring) + 1;
                        var how_many_to_add = Math.Min(Capacity_Step, max_allowed_added);
                        IEnumerable<int> range = Enumerable.Range(Start_Id_For_next_Capacity_Factoring, how_many_to_add);
                        _AvailableIDs_container.UnionWith(range);

                        Start_Id_For_next_Capacity_Factoring = range.Max() + 1;
                    }

                }
            }
        }

        public void FreeUpID(int ID)
        {
            lock (SYNC_BLOCK)
            {
                if (ID <= m_MAX_ID && ID >= m_Min_ID)
                {
                    _AvailableIDs_container.Add(ID);
                }
            }

        }


        /// <summary>
        /// This function is just to give functionality to the class. <br/>
        /// However IT SHOULD NOT BE USED by itself rather use the <see cref="GetNextAvailableID"/> function.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool ID_IsInUse(int id)
        {
            lock (SYNC_BLOCK)
            {
                if (id > m_MAX_ID || id < m_Min_ID)
                {
                    return false;
                }

                return _AvailableIDs_container.Contains(id);
            }
        }
    }
}
