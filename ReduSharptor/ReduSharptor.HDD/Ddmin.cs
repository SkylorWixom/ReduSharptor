namespace ReduSharptor.HDD
{
    /// <summary>
    /// The ddmin algorithm (Zeller and Hildebrandt), the same procedure the
    /// baseline ReduSharptor uses: split the list into sections, test each
    /// section and each complement, keep whatever still fails, double the
    /// granularity when nothing does, stop when granularity exceeds the list.
    /// The splitting here distributes items evenly; otherwise the procedure
    /// mirrors the baseline's FindSmallestFailingInput.
    /// </summary>
    public static class Ddmin
    {
        /// <summary>
        /// Reduces items to a smaller list for which isPreserved stays true.
        /// isPreserved receives a candidate subset (in original order) and answers
        /// whether the original failure still occurs with only those items present.
        /// </summary>
        public static List<T> Reduce<T>(List<T> items, Func<List<T>, bool> isPreserved)
        {
            List<T> current = items;
            int sections = 2;

            while (current.Count > 1)
            {
                List<List<T>> parts = Split(current, sections);
                bool shrunk = false;

                // Try each section alone.
                foreach (List<T> part in parts)
                {
                    if (part.Count > 0 && part.Count < current.Count && isPreserved(part))
                    {
                        current = part;
                        sections = 2;
                        shrunk = true;
                        break;
                    }
                }
                if (shrunk) continue;

                // Try each complement (everything except one section).
                foreach (List<T> part in parts)
                {
                    List<T> complement = current.Except(part).ToList();
                    if (complement.Count > 0 && complement.Count < current.Count && isPreserved(complement))
                    {
                        current = complement;
                        sections = Math.Max(sections - 1, 2);
                        shrunk = true;
                        break;
                    }
                }
                if (shrunk) continue;

                // Nothing failed at this granularity; refine it.
                if (sections >= current.Count)
                {
                    break;
                }
                sections = Math.Min(sections * 2, current.Count);
            }

            return current;
        }

        /// <summary>
        /// Splits the list into the requested number of sections, preserving order
        /// and distributing items as evenly as possible.
        /// </summary>
        private static List<List<T>> Split<T>(List<T> items, int sections)
        {
            var result = new List<List<T>>();
            int baseSize = items.Count / sections;
            int remainder = items.Count % sections;
            int index = 0;

            for (int i = 0; i < sections; i++)
            {
                int size = baseSize + (i < remainder ? 1 : 0);
                result.Add(items.GetRange(index, Math.Min(size, items.Count - index)));
                index += size;
            }

            return result;
        }
    }
}
