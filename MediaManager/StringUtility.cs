using System;

namespace MediaManager {

    public static class StringUtility {

        public static string GetGUID(this string value) => Convert.ToString(GetIntGUID(value), 16);

        public static int GetIntGUID(this string value) {

            if (value == null) throw new ArgumentNullException("value");

            unchecked {

                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < value.Length; i += 2) {
                    hash1 = ((hash1 << 5) + hash1) ^ value[i];
                    if (i == value.Length - 1) break;
                    hash2 = ((hash2 << 5) + hash2) ^ value[i + 1];
                }

                return hash1 + (hash2 * 1566083941);

            }

        }

    }

}
