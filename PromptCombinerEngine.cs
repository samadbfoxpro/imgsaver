using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace imgsaver
{
    public static class PromptCombinerEngine
    {
        public static string Combine(string originalPrompt, List<string> snippetTexts, CombinerPlacementMode mode, int commaIndex = 1, string separator = ", ")
        {
            if (snippetTexts == null || snippetTexts.Count == 0) return originalPrompt ?? "";
            string basePrompt = (originalPrompt ?? "").Trim();
            
            // Filter out empty snippets
            var validSnippets = snippetTexts
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (validSnippets.Count == 0) return basePrompt;

            if (string.IsNullOrWhiteSpace(separator)) separator = ", ";

            string combinedSnippets = string.Join(separator, validSnippets);

            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                return combinedSnippets;
            }

            switch (mode)
            {
                case CombinerPlacementMode.AtBeginning:
                    return CleanUpCommas($"{combinedSnippets}{separator}{basePrompt}");

                case CombinerPlacementMode.AtEnd:
                    return CleanUpCommas($"{basePrompt}{separator}{combinedSnippets}");

                case CombinerPlacementMode.AfterComma:
                default:
                    return InsertAfterComma(basePrompt, combinedSnippets, commaIndex, separator);
            }
        }

        public static string CombinePerFolder(string originalPrompt, PromptCombinerData combinerData)
        {
            if (combinerData == null || combinerData.ActiveItemIds == null || combinerData.ActiveItemIds.Count == 0)
                return originalPrompt ?? "";

            string basePrompt = (originalPrompt ?? "").Trim();
            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                var allActiveTexts = combinerData.Items
                    .Where(i => combinerData.ActiveItemIds.Contains(i.Id))
                    .Select(i => i.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
                string sep = string.IsNullOrWhiteSpace(combinerData.Separator) ? ", " : combinerData.Separator;
                return string.Join(sep, allActiveTexts);
            }

            string separator = string.IsNullOrWhiteSpace(combinerData.Separator) ? ", " : combinerData.Separator;

            var activeItems = combinerData.Items
                .Where(i => combinerData.ActiveItemIds.Contains(i.Id))
                .ToList();

            if (activeItems.Count == 0) return basePrompt;

            var folderGroups = activeItems
                .GroupBy(i => i.FolderId)
                .Select(g => new
                {
                    Folder = combinerData.Folders.FirstOrDefault(f => f.Id == g.Key),
                    Items = g.OrderBy(i => i.Order).Select(i => i.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                })
                .Where(g => g.Items.Count > 0)
                .OrderBy(g => g.Folder?.Order ?? 0)
                .ToList();

            // Categorize into AtBeginning, AfterComma (by commaIndex), and AtEnd
            var atBeginningTexts = new List<string>();
            var afterCommaMap = new Dictionary<int, List<string>>();
            var atEndTexts = new List<string>();

            foreach (var group in folderGroups)
            {
                var mode = group.Folder?.PlacementMode ?? CombinerPlacementMode.AfterComma;
                int cIdx = group.Folder?.CommaIndex ?? 1;

                if (mode == CombinerPlacementMode.AtBeginning)
                {
                    atBeginningTexts.AddRange(group.Items);
                }
                else if (mode == CombinerPlacementMode.AtEnd)
                {
                    atEndTexts.AddRange(group.Items);
                }
                else // AfterComma
                {
                    if (!afterCommaMap.ContainsKey(cIdx))
                    {
                        afterCommaMap[cIdx] = new List<string>();
                    }
                    afterCommaMap[cIdx].AddRange(group.Items);
                }
            }

            // Step 1: Prepend AtBeginning items to the original base prompt
            string currentPrompt = basePrompt;
            if (atBeginningTexts.Count > 0)
            {
                string prepended = string.Join(separator, atBeginningTexts);
                currentPrompt = $"{prepended}{separator}{currentPrompt}";
            }

            // Step 2: Calculate original comma indices based on the ORIGINAL basePrompt position offset
            int originalOffset = atBeginningTexts.Count > 0 ? (string.Join(separator, atBeginningTexts).Length + separator.Length) : 0;

            // Process AfterComma insertions starting from highest commaIndex to lowest to preserve positions
            foreach (var kvp in afterCommaMap.OrderByDescending(k => k.Key))
            {
                int targetCommaIndex = kvp.Key;
                string snippetsText = string.Join(separator, kvp.Value);

                currentPrompt = InsertAfterCommaRelativeToOffset(currentPrompt, snippetsText, targetCommaIndex, originalOffset, separator);
            }

            // Step 3: Append AtEnd items
            if (atEndTexts.Count > 0)
            {
                string appended = string.Join(separator, atEndTexts);
                currentPrompt = $"{currentPrompt}{separator}{appended}";
            }

            return CleanUpCommas(currentPrompt);
        }

        private static string InsertAfterCommaRelativeToOffset(string fullPrompt, string snippetsText, int commaIndex, int offset, string separator)
        {
            if (commaIndex <= 0) commaIndex = 1;

            int currentCommaCount = 0;
            int insertPos = -1;

            for (int i = offset; i < fullPrompt.Length; i++)
            {
                if (fullPrompt[i] == ',')
                {
                    currentCommaCount++;
                    if (currentCommaCount == commaIndex)
                    {
                        insertPos = i;
                        break;
                    }
                }
            }

            if (insertPos == -1)
            {
                // Fewer commas in original prompt than commaIndex, insert after offset or end
                return CleanUpCommas($"{fullPrompt}{separator}{snippetsText}");
            }

            string part1 = fullPrompt.Substring(0, insertPos + 1).TrimEnd();
            string part2 = fullPrompt.Substring(insertPos + 1).TrimStart();

            string result;
            if (!string.IsNullOrWhiteSpace(part2))
            {
                result = $"{part1} {snippetsText}{separator}{part2}";
            }
            else
            {
                result = $"{part1} {snippetsText}";
            }

            return CleanUpCommas(result);
        }

        private static string InsertAfterComma(string basePrompt, string snippetsText, int commaIndex, string separator)
        {
            return InsertAfterCommaRelativeToOffset(basePrompt, snippetsText, commaIndex, 0, separator);
        }

        public static string CleanUpCommas(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            // Fix double or multiple commas like ", ," or ",,"
            string cleaned = Regex.Replace(input, @"\s*,\s*,+", ", ");
            // Fix leading commas
            cleaned = Regex.Replace(cleaned, @"^\s*,\s*", "");
            // Fix trailing commas
            cleaned = Regex.Replace(cleaned, @"\s*,\s*$", "");
            // Fix multiple spaces
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
            return cleaned.Trim();
        }
    }
}
