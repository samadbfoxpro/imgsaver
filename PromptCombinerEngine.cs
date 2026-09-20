using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace imgsaver
{
    public static class PromptCombinerEngine
    {
        public static bool IsPersianText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Regex.IsMatch(text, @"[\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFF]");
        }

        public static string Combine(string originalPrompt, List<string> snippetTexts, CombinerPlacementMode mode, int commaIndex = 1, string separator = ", ")
        {
            if (snippetTexts == null || snippetTexts.Count == 0) return originalPrompt ?? "";
            string basePrompt = (originalPrompt ?? "").Trim();
            
            if (string.IsNullOrWhiteSpace(separator)) separator = ", ";

            var seenSnippetTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var validSnippets = new List<string>();
            foreach (var snippet in snippetTexts)
            {
                if (string.IsNullOrWhiteSpace(snippet)) continue;
                var newTags = ExtractUniqueTags(snippet, seenSnippetTags, separator);
                if (newTags.Count > 0)
                {
                    validSnippets.Add(string.Join(separator, newTags));
                }
            }

            if (validSnippets.Count == 0) return basePrompt;

            string combinedSnippets = string.Join(separator, validSnippets);

            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                return CleanUpCommas(combinedSnippets);
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
            if (combinerData == null) return originalPrompt ?? "";

            string basePrompt = (originalPrompt ?? "").Trim();
            string separator = string.IsNullOrWhiteSpace(combinerData.Separator) ? ", " : combinerData.Separator;

            // Structured item holder for deterministic mapping
            var atBeginningItems = new List<string>();
            var afterCommaMap = new SortedDictionary<int, List<string>>();
            var atEndItems = new List<string>();

            // Intra-snippet tag deduplicator
            var seenSnippetTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect active items in stable folder order
            var sortedFolders = combinerData.Folders != null 
                ? combinerData.Folders.OrderBy(f => f.Order).ToList() 
                : new List<PromptCombinerFolder>();

            foreach (var folder in sortedFolders)
            {
                var folderSnippetList = new List<string>();

                if (folder.IsCustomInput)
                {
                    if (!string.IsNullOrWhiteSpace(folder.CustomInputText))
                    {
                        var newTags = ExtractUniqueTags(folder.CustomInputText, seenSnippetTags, separator);
                        if (newTags.Count > 0)
                        {
                            folderSnippetList.Add(string.Join(separator, newTags));
                        }
                    }
                }
                else
                {
                    var items = (combinerData.Items ?? new List<PromptCombinerItem>())
                        .Where(i => i.FolderId == folder.Id && combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id))
                        .OrderBy(i => i.Order)
                        .Select(i => i.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    foreach (var itemText in items)
                    {
                        var newTags = ExtractUniqueTags(itemText, seenSnippetTags, separator);
                        if (newTags.Count > 0)
                        {
                            folderSnippetList.Add(string.Join(separator, newTags));
                        }
                    }
                }

                if (folderSnippetList.Count == 0) continue;

                // Determine folder placement rule
                var mode = (combinerData.PlacementMode == CombinerPlacementMode.PerFolder) 
                    ? folder.PlacementMode 
                    : combinerData.PlacementMode;

                int cIdx = (combinerData.PlacementMode == CombinerPlacementMode.PerFolder) 
                    ? (folder.CommaIndex > 0 ? folder.CommaIndex : 1) 
                    : (combinerData.CommaIndex > 0 ? combinerData.CommaIndex : 1);

                if (mode == CombinerPlacementMode.AtBeginning)
                {
                    atBeginningItems.AddRange(folderSnippetList);
                }
                else if (mode == CombinerPlacementMode.AtEnd)
                {
                    atEndItems.AddRange(folderSnippetList);
                }
                else // AfterComma (Default)
                {
                    if (!afterCommaMap.ContainsKey(cIdx))
                    {
                        afterCommaMap[cIdx] = new List<string>();
                    }
                    afterCommaMap[cIdx].AddRange(folderSnippetList);
                }
            }

            // Collect any active items whose FolderId is missing or not matching any folder
            var folderIdSet = new HashSet<string>(sortedFolders.Select(f => f.Id));
            var remainingItems = (combinerData.Items ?? new List<PromptCombinerItem>())
                .Where(i => (string.IsNullOrEmpty(i.FolderId) || !folderIdSet.Contains(i.FolderId)) 
                         && combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id))
                .OrderBy(i => i.Order)
                .Select(i => i.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (remainingItems.Count > 0)
            {
                var remainingSnippetList = new List<string>();
                foreach (var itemText in remainingItems)
                {
                    var newTags = ExtractUniqueTags(itemText, seenSnippetTags, separator);
                    if (newTags.Count > 0)
                    {
                        remainingSnippetList.Add(string.Join(separator, newTags));
                    }
                }

                if (remainingSnippetList.Count > 0)
                {
                    int cIdx = combinerData.CommaIndex > 0 ? combinerData.CommaIndex : 1;
                    if (combinerData.PlacementMode == CombinerPlacementMode.AtBeginning)
                    {
                        atBeginningItems.AddRange(remainingSnippetList);
                    }
                    else if (combinerData.PlacementMode == CombinerPlacementMode.AtEnd)
                    {
                        atEndItems.AddRange(remainingSnippetList);
                    }
                    else
                    {
                        if (!afterCommaMap.ContainsKey(cIdx))
                        {
                            afterCommaMap[cIdx] = new List<string>();
                        }
                        afterCommaMap[cIdx].AddRange(remainingSnippetList);
                    }
                }
            }

            // Check if there is anything to inject
            bool hasAnyItems = atBeginningItems.Count > 0 || afterCommaMap.Count > 0 || atEndItems.Count > 0;
            if (!hasAnyItems) return basePrompt;

            // If base prompt is empty, join all items in logical order
            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                var allOrdered = new List<string>();
                allOrdered.AddRange(atBeginningItems);
                foreach (var kvp in afterCommaMap)
                {
                    allOrdered.AddRange(kvp.Value);
                }
                allOrdered.AddRange(atEndItems);
                return CleanUpCommas(string.Join(separator, allOrdered));
            }

            // Split base prompt into clean comma segments (1-indexed slots)
            var baseSegments = basePrompt.Split(',')
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s))
                                         .ToList();

            var finalSegments = new List<string>();

            // Phase 1: Prepend AtBeginning items
            if (atBeginningItems.Count > 0)
            {
                finalSegments.Add(string.Join(separator, atBeginningItems));
            }

            // Phase 2: Interleave base segments with comma slots in exact ascending order
            int maxTargetComma = afterCommaMap.Count > 0 ? afterCommaMap.Keys.Max() : 0;
            int totalSlots = Math.Max(baseSegments.Count, maxTargetComma);

            for (int slot = 1; slot <= totalSlots; slot++)
            {
                // Add the base prompt segment at this position if it exists
                if (slot <= baseSegments.Count)
                {
                    finalSegments.Add(baseSegments[slot - 1]);
                }

                // Inject snippets assigned specifically to Comma #slot
                if (afterCommaMap.TryGetValue(slot, out var commaSnippets) && commaSnippets.Count > 0)
                {
                    finalSegments.Add(string.Join(separator, commaSnippets));
                }
            }

            // Phase 3: Append AtEnd items
            if (atEndItems.Count > 0)
            {
                finalSegments.Add(string.Join(separator, atEndItems));
            }

            string result = string.Join(separator, finalSegments);
            return CleanUpCommas(result);
        }

        private static string InsertAfterComma(string basePrompt, string snippetsText, int commaIndex, string separator)
        {
            if (string.IsNullOrWhiteSpace(basePrompt)) return snippetsText ?? "";
            if (string.IsNullOrWhiteSpace(snippetsText)) return basePrompt;
            if (commaIndex <= 0) commaIndex = 1;
            if (string.IsNullOrWhiteSpace(separator)) separator = ", ";

            var baseSegments = basePrompt.Split(',')
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s))
                                         .ToList();

            var finalSegments = new List<string>();
            int totalSlots = Math.Max(baseSegments.Count, commaIndex);

            for (int slot = 1; slot <= totalSlots; slot++)
            {
                if (slot <= baseSegments.Count)
                {
                    finalSegments.Add(baseSegments[slot - 1]);
                }

                if (slot == commaIndex)
                {
                    finalSegments.Add(snippetsText);
                }
            }

            return CleanUpCommas(string.Join(separator, finalSegments));
        }

        public static string CleanUpCommas(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            // Fix spaces before commas e.g. "word ," -> "word,"
            string cleaned = Regex.Replace(input, @"[ \t]+,", ",");
            // Fix double or multiple commas like ", ," or ",," without affecting newlines
            cleaned = Regex.Replace(cleaned, @"[ \t]*,[ \t]*,+", ", ");
            // Fix leading commas at start of lines
            cleaned = Regex.Replace(cleaned, @"(?m)^[ \t]*,[ \t]*", "");
            // Fix trailing commas at end of lines
            cleaned = Regex.Replace(cleaned, @"(?m)[ \t]*,[ \t]*$", "");
            // Fix multiple spaces
            cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
            return cleaned.Trim();
        }

        private static List<string> ExtractUniqueTags(string text, HashSet<string> seenTags, string separator = ", ")
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var tags = text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim())
                           .Where(t => !string.IsNullOrWhiteSpace(t))
                           .ToList();

            var result = new List<string>();
            foreach (var tag in tags)
            {
                if (seenTags.Add(tag))
                {
                    result.Add(tag);
                }
            }
            return result;
        }

        private static bool PromptContainsSnippet(string prompt, string snippet)
        {
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(snippet)) return false;

            string p = prompt.Trim();
            string s = snippet.Trim();

            // 1. Exact match
            if (p.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;

            // 2. Tag-level match
            var pTags = p.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim())
                         .ToList();

            var sTags = s.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim())
                         .Where(t => !string.IsNullOrEmpty(t))
                         .ToList();

            if (sTags.Count == 0) return false;

            // Check if all tags in snippet already exist in prompt
            return sTags.All(st => pTags.Any(pt => string.Equals(pt, st, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Legacy wrapper forwarding to CursorBadgeNotification
    /// </summary>
    public static class CursorCombinerBadge
    {
        public static void Show(string message = "⚡ Combined!", string borderHex = "#00E5FF", string textHex = "#00E5FF", string bgHex = "#F0181C24")
        {
            CursorBadgeNotification.Show(message, borderHex, textHex, bgHex);
        }

        public static void ShowTagReplaced(string message = "🧩 Tag Replaced!")
        {
            CursorBadgeNotification.ShowTagReplaced(message);
        }

        public static void ShowExtraReplaced(string message = "✨ Extra Applied!")
        {
            CursorBadgeNotification.ShowExtraApplied(message);
        }
    }
}
