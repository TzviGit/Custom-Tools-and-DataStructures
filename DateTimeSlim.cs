using System;
using System.Threading;

namespace Infra
{
    /// <summary>
    /// Using <see cref="DateTime.Now"/> is 6 times more CPU intensive than <see cref="DateTime.UtcNow"/>.
    /// So it's a shame to use <see cref="DateTime.Now"/> all over the codebase if the timezone doesnt change.
    /// This class solves the problem by suplying local time without using <see cref="DateTime.Now"/> constanly.
    /// Rather it adds the time offset from UTC to <see cref="DateTime.UtcNow"/>
    /// </summary>
    public class DateTimeSlim
    {
        /****************************************************************************************************************
         *  EXPLANATION:
         *  Using DateTime.Now is 6 times more CPU intensive than DateTime.UtcNow.
         *  So it's a shame to use DateTime.Now all over the codebase if the timezone doesnt change.
         *  This class solves the problem by suplying local time without using DateTime.Now constanly.
         *  Rather it adds the time offset from UTC to DateTime.UtcNow      
         * ***************************************************************************************************************/

        /// <summary>
        /// Periodicly Check the time-offset between local and UTC time
        /// </summary>
        private readonly static Timer updateTimeOffsetTimer = new Timer(CheckTimeZoneOffset, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        /// <summary>
        /// time-offset minutes between local and UTC time
        /// </summary>
        private static int offsetMinutesFromUTC = (int)Math.Round((DateTime.Now - DateTime.UtcNow).TotalMinutes);

        private static bool SystemIsUTC => offsetMinutesFromUTC == 0;

        /// <summary>
        /// Returns LocalTime. If System time is not UTC then adds the time - offset from UTC
        /// </summary>
        public static DateTime Now
        {
            get
            {
                if (SystemIsUTC)
                {
                    return DateTime.UtcNow;
                }
                else
                {                    
                    return DateTime.UtcNow.AddMinutes(offsetMinutesFromUTC);
                }

            }
        }

        /// <summary>
        /// Periodicly Check the time-offset between local and UTC time
        /// </summary>
        private static void CheckTimeZoneOffset(object _)
        {
            int offset_minutes = (int)Math.Round((DateTime.Now - DateTime.UtcNow).TotalMinutes);
            if (offsetMinutesFromUTC != offset_minutes)
            {
                offsetMinutesFromUTC = offset_minutes;
            }
        }


        /// <summary>
        /// returns <see cref="DateTime.UtcNow"/>
        /// </summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// returns todays date (from system time)
        /// </summary>
        public static DateTime Today => Now.Date;
    }
}
