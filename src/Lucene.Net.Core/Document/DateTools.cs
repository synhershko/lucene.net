namespace Lucene.Net.Document
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    using System;

    // for javadocs
    // for javadocs

    /// <summary>
    /// Provides support for converting dates to strings and vice-versa.
    /// The strings are structured so that lexicographic sorting orders
    /// them by date, which makes them suitable for use as field values
    /// and search terms.
    ///
    /// <P>this class also helps you to limit the resolution of your dates. Do not
    /// save dates with a finer resolution than you really need, as then
    /// <seealso cref="TermRangeQuery"/> and <seealso cref="PrefixQuery"/> will require more memory and become slower.
    ///
    /// <P>
    /// Another approach is <seealso cref="NumericUtils"/>, which provides
    /// a sortable binary representation (prefix encoded) of numeric values, which
    /// date/time are.
    /// For indexing a <seealso cref="Date"/> or <seealso cref="Calendar"/>, just get the unix timestamp as
    /// <code>long</code> using <seealso cref="Date#getTime"/> or <seealso cref="Calendar#getTimeInMillis"/> and
    /// index this as a numeric value with <seealso cref="LongField"/>
    /// and use <seealso cref="NumericRangeQuery"/> to query it.
    /// </summary>
    public static class DateTools
    {
        private static readonly String YEAR_FORMAT = "yyyy";
        private static readonly String MONTH_FORMAT = "yyyyMM";
        private static readonly String DAY_FORMAT = "yyyyMMdd";
        private static readonly String HOUR_FORMAT = "yyyyMMddHH";
        private static readonly String MINUTE_FORMAT = "yyyyMMddHHmm";
        private static readonly String SECOND_FORMAT = "yyyyMMddHHmmss";
        private static readonly String MILLISECOND_FORMAT = "yyyyMMddHHmmssfff";

        private static readonly System.Globalization.Calendar calInstance = new System.Globalization.GregorianCalendar();

        /// <summary>
        /// Converts a Date to a string suitable for indexing.
        /// </summary>
        /// <param name="date"> the date to be converted </param>
        /// <param name="resolution"> the desired resolution, see
        ///  <seealso cref="#round(Date, DateTools.Resolution)"/> </param>
        /// <returns> a string in format <code>yyyyMMddHHmmssSSS</code> or shorter,
        ///  depending on <code>resolution</code>; using GMT as timezone  </returns>
        public static string DateToString(DateTime date, Resolution resolution)
        {
            return TimeToString(date.Ticks / TimeSpan.TicksPerMillisecond, resolution);
        }

        /// <summary>
        /// Converts a millisecond time to a string suitable for indexing.
        /// </summary>
        /// <param name="time"> the date expressed as milliseconds since January 1, 1970, 00:00:00 GMT </param>
        /// <param name="resolution"> the desired resolution, see
        ///  <seealso cref="#round(long, DateTools.Resolution)"/> </param>
        /// <returns> a string in format <code>yyyyMMddHHmmssSSS</code> or shorter,
        ///  depending on <code>resolution</code>; using GMT as timezone </returns>
        public static string TimeToString(long time, Resolution resolution)
        {
            DateTime date = new DateTime(Round(time, resolution));

            if (resolution == Resolution.YEAR)
            {
                return date.ToString(YEAR_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.MONTH)
            {
                return date.ToString(MONTH_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.DAY)
            {
                return date.ToString(DAY_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.HOUR)
            {
                return date.ToString(HOUR_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.MINUTE)
            {
                return date.ToString(MINUTE_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.SECOND)
            {
                return date.ToString(SECOND_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (resolution == Resolution.MILLISECOND)
            {
                return date.ToString(MILLISECOND_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            }

            throw new ArgumentException("unknown resolution " + resolution);
        }

        /// <summary>
        /// Converts a string produced by <code>timeToString</code> or
        /// <code>dateToString</code> back to a time, represented as the
        /// number of milliseconds since January 1, 1970, 00:00:00 GMT.
        /// </summary>
        /// <param name="dateString"> the date string to be converted </param>
        /// <returns> the number of milliseconds since January 1, 1970, 00:00:00 GMT </returns>
        /// <exception cref="ParseException"> if <code>dateString</code> is not in the
        ///  expected format  </exception>
        public static long StringToTime(string dateString)
        {
            return StringToDate(dateString).Ticks;
        }

        /// <summary>
        /// Converts a string produced by <code>timeToString</code> or
        /// <code>dateToString</code> back to a time, represented as a
        /// Date object.
        /// </summary>
        /// <param name="dateString"> the date string to be converted </param>
        /// <returns> the parsed time as a Date object </returns>
        /// <exception cref="ParseException"> if <code>dateString</code> is not in the
        ///  expected format  </exception>
        public static DateTime StringToDate(string dateString)
        {
            DateTime date;
            if (dateString.Length == 4)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    1, 1, 0, 0, 0, 0);
            }
            else if (dateString.Length == 6)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    1, 0, 0, 0, 0);
            }
            else if (dateString.Length == 8)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    Convert.ToInt16(dateString.Substring(6, 2)),
                    0, 0, 0, 0);
            }
            else if (dateString.Length == 10)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    Convert.ToInt16(dateString.Substring(6, 2)),
                    Convert.ToInt16(dateString.Substring(8, 2)),
                    0, 0, 0);
            }
            else if (dateString.Length == 12)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    Convert.ToInt16(dateString.Substring(6, 2)),
                    Convert.ToInt16(dateString.Substring(8, 2)),
                    Convert.ToInt16(dateString.Substring(10, 2)),
                    0, 0);
            }
            else if (dateString.Length == 14)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    Convert.ToInt16(dateString.Substring(6, 2)),
                    Convert.ToInt16(dateString.Substring(8, 2)),
                    Convert.ToInt16(dateString.Substring(10, 2)),
                    Convert.ToInt16(dateString.Substring(12, 2)),
                    0);
            }
            else if (dateString.Length == 17)
            {
                date = new DateTime(Convert.ToInt16(dateString.Substring(0, 4)),
                    Convert.ToInt16(dateString.Substring(4, 2)),
                    Convert.ToInt16(dateString.Substring(6, 2)),
                    Convert.ToInt16(dateString.Substring(8, 2)),
                    Convert.ToInt16(dateString.Substring(10, 2)),
                    Convert.ToInt16(dateString.Substring(12, 2)),
                    Convert.ToInt16(dateString.Substring(14, 3)));
            }
            else
            {
                throw new FormatException("Input is not valid date string: " + dateString);
            }
            return date;
        }

        /// <summary>
        /// Limit a date's resolution. For example, the date <code>2004-09-21 13:50:11</code>
        /// will be changed to <code>2004-09-01 00:00:00</code> when using
        /// <code>Resolution.MONTH</code>.
        /// </summary>
        /// <param name="resolution"> The desired resolution of the date to be returned </param>
        /// <returns> the date with all values more precise than <code>resolution</code>
        ///  set to 0 or 1 </returns>
        public static DateTime Round(DateTime date, Resolution resolution)
        {
            return new DateTime(Round(date.Ticks / TimeSpan.TicksPerMillisecond, resolution));
        }

        /// <summary>
        /// Limit a date's resolution. For example, the date <code>1095767411000</code>
        /// (which represents 2004-09-21 13:50:11) will be changed to
        /// <code>1093989600000</code> (2004-09-01 00:00:00) when using
        /// <code>Resolution.MONTH</code>.
        /// </summary>
        /// <param name="resolution"> The desired resolution of the date to be returned </param>
        /// <returns> the date with all values more precise than <code>resolution</code>
        ///  set to 0 or 1, expressed as milliseconds since January 1, 1970, 00:00:00 GMT </returns>
        public static long Round(long time, Resolution resolution)
        {
            DateTime dt = new DateTime(time * TimeSpan.TicksPerMillisecond);

            if (resolution == Resolution.YEAR)
            {
                dt = dt.AddMonths(1 - dt.Month);
                dt = dt.AddDays(1 - dt.Day);
                dt = dt.AddHours(0 - dt.Hour);
                dt = dt.AddMinutes(0 - dt.Minute);
                dt = dt.AddSeconds(0 - dt.Second);
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.MONTH)
            {
                dt = dt.AddDays(1 - dt.Day);
                dt = dt.AddHours(0 - dt.Hour);
                dt = dt.AddMinutes(0 - dt.Minute);
                dt = dt.AddSeconds(0 - dt.Second);
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.DAY)
            {
                dt = dt.AddHours(0 - dt.Hour);
                dt = dt.AddMinutes(0 - dt.Minute);
                dt = dt.AddSeconds(0 - dt.Second);
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.HOUR)
            {
                dt = dt.AddMinutes(0 - dt.Minute);
                dt = dt.AddSeconds(0 - dt.Second);
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.MINUTE)
            {
                dt = dt.AddSeconds(0 - dt.Second);
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.SECOND)
            {
                dt = dt.AddMilliseconds(0 - dt.Millisecond);
            }
            else if (resolution == Resolution.MILLISECOND)
            {
                // don't cut off anything
            }
            else
            {
                throw new System.ArgumentException("unknown resolution " + resolution);
            }
            return dt.Ticks;
        }

        /// <summary>
        /// Specifies the time granularity. </summary>
        public enum Resolution
        {
            /// <summary>
            /// Limit a date's resolution to year granularity. </summary>
            YEAR = 4,

            /// <summary>
            /// Limit a date's resolution to month granularity. </summary>
            MONTH = 6,

            /// <summary>
            /// Limit a date's resolution to day granularity. </summary>
            DAY = 8,

            /// <summary>
            /// Limit a date's resolution to hour granularity. </summary>
            HOUR = 10,

            /// <summary>
            /// Limit a date's resolution to minute granularity. </summary>
            MINUTE = 12,

            /// <summary>
            /// Limit a date's resolution to second granularity. </summary>
            SECOND = 14,

            /// <summary>
            /// Limit a date's resolution to millisecond granularity. </summary>
            MILLISECOND = 17
        }
    }
}