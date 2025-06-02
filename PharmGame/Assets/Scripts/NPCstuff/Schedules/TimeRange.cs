// --- START OF FILE TimeRange.cs ---

using UnityEngine; // Needed for Debug.LogWarning (optional validation)
using System; // Needed for System.Serializable and TimeSpan

namespace Game.Utilities // Place in a suitable utilities namespace
{
    /// <summary>
    /// Represents a time range within a 24-hour period, defined by a start and end time.
    /// Useful for scheduling daily events or activities.
    /// </summary>
    [System.Serializable]
    public struct TimeRange
    {
        // --- Serialized Fields ---
        // Store times as hours and minutes for simple serialization
        [Tooltip("The hour (0-23) the time range starts.")]
        [SerializeField] private int startHour;
        [Tooltip("The minute (0-59) the time range starts.")]
        [SerializeField] private int startMinute;
        [Tooltip("The hour (0-23) the time range ends.")]
        [SerializeField] private int endHour;
        [Tooltip("The minute (0-59) the time range ends.")]
        [SerializeField] private int endMinute;

        // --- Public Properties (Accessing/Setting Fields with Basic Validation) ---
        public int StartHour
        {
            get => startHour;
            set => startHour = Mathf.Clamp(value, 0, 23); // Clamp to valid hour range
        }
        public int StartMinute
        {
            get => startMinute;
            set => startMinute = Mathf.Clamp(value, 0, 59); // Clamp to valid minute range
        }
        public int EndHour
        {
            get => endHour;
            set => endHour = Mathf.Clamp(value, 0, 23); // Clamp to valid hour range
        }
        public int EndMinute
        {
            get => endMinute;
            set => endMinute = Mathf.Clamp(value, 0, 59); // Clamp to valid minute range
        }

        // --- Constructor ---
        /// <summary>
        /// Creates a new TimeRange instance.
        /// </summary>
        /// <param name="startHour">The starting hour (0-23).</param>
        /// <param name="startMinute">The starting minute (0-59).</param>
        /// <param name="endHour">The ending hour (0-23).</param>
        /// <param name="endMinute">The ending minute (0-59).</param>
        public TimeRange(int startHour, int startMinute, int endHour, int endMinute)
        {
            // Use properties to apply clamping during construction
            this.startHour = Mathf.Clamp(startHour, 0, 23);
            this.startMinute = Mathf.Clamp(startMinute, 0, 59);
            this.endHour = Mathf.Clamp(endHour, 0, 23);
            this.endMinute = Mathf.Clamp(endMinute, 0, 59);

            // Optional: Add a warning if the range is invalid (e.g., end is before start)
            // However, the IsWithinRange method will handle this logic.
            // if (TotalMinutes(endHour, endMinute) < TotalMinutes(startHour, startMinute))
            // {
            //     Debug.LogWarning($"TimeRange created with end time before start time: {startHour:D2}:{startMinute:D2} - {endHour:D2}:{endMinute:D2}. This represents a range that crosses midnight.");
            // }
        }

        // --- Helper Methods ---

        /// <summary>
        /// Calculates the total minutes from midnight for a given hour and minute.
        /// </summary>
        private static int TotalMinutes(int hour, int minute)
        {
            return hour * 60 + minute;
        }

        /// <summary>
        /// Checks if a given DateTime falls within this time range.
        /// Handles ranges that cross midnight.
        /// </summary>
        /// <param name="dateTime">The DateTime to check (only hour and minute are considered).</param>
        /// <returns>True if the DateTime's time falls within the range, false otherwise.</returns>
        public bool IsWithinRange(DateTime dateTime)
        {
            // Get the time of the DateTime as total minutes from midnight
            int currentTotalMinutes = TotalMinutes(dateTime.Hour, dateTime.Minute);

            // Get the start and end times as total minutes from midnight
            int startTotalMinutes = TotalMinutes(startHour, startMinute);
            int endTotalMinutes = TotalMinutes(endHour, endMinute);

            // Case 1: The range does NOT cross midnight (e.g., 08:00 to 17:00)
            if (startTotalMinutes <= endTotalMinutes)
            {
                return currentTotalMinutes >= startTotalMinutes && currentTotalMinutes <= endTotalMinutes;
            }
            // Case 2: The range DOES cross midnight (e.g., 22:00 to 06:00)
            else
            {
                // The time is within the range if it's between the start and midnight (inclusive)
                // OR between midnight and the end (inclusive)
                return currentTotalMinutes >= startTotalMinutes || currentTotalMinutes <= endTotalMinutes;
            }
        }

        /// <summary>
        /// Returns a string representation of the TimeRange (e.g., "08:30 - 17:00").
        /// </summary>
        public override string ToString()
        {
            return $"{startHour:D2}:{startMinute:D2} - {endHour:D2}:{endMinute:D2}";
        }

         // Optional: Implicit conversion from (int, int, int, int) tuple for convenience
         // public static implicit operator TimeRange((int sh, int sm, int eh, int em) t)
         // {
         //      return new TimeRange(t.sh, t.sm, t.eh, t.em);
         // }
    }
}
// --- END OF FILE TimeRange.cs ---