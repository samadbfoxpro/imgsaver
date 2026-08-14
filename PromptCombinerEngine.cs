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
            
            if (string.IsNullOrWhiteSpace(separator)) separator = ", ";

            // Filter out empty snippets and snippets that already exist in basePrompt
            var validSnippets = snippetTexts
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !PromptContainsSnippet(basePrompt, s))
                .ToList();

            if (validSnippets.Count == 0) return basePrompt;

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
            if (combinerData == null) return originalPrompt ?? "";

            string basePrompt = (originalPrompt ?? "").Trim();
            string separator = string.IsNullOrWhiteSpace(combinerData.Separator) ? ", " : combinerData.Separator;

            // Collect active snippets per folder in folder order
            var folderGroups = new List<(PromptCombinerFolder Folder, List<string> Items)>();

            foreach (var folder in combinerData.Folders.OrderBy(f => f.Order))
            {
                var folderTexts = new List<string>();
                if (folder.IsCustomInput)
                {
                    if (folder.IsCustomInputActive && !string.IsNullOrWhiteSpace(folder.CustomInputText))
                    {
                        string customText = folder.CustomInputText.Trim();
                        if (!PromptContainsSnippet(basePrompt, customText))
                        {
                            folderTexts.Add(customText);
                        }
                    }
                }
                else
                {
                    var items = combinerData.Items
                        .Where(i => i.FolderId == folder.Id && combinerData.ActiveItemIds.Contains(i.Id))
                        .OrderBy(i => i.Order)
                        .Select(i => i.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => t.Trim())
                        .Where(t => !PromptContainsSnippet(basePrompt, t))
                        .ToList();
                    folderTexts.AddRange(items);
                }

                if (folderTexts.Count > 0)
                {
                    folderGroups.Add((folder, folderTexts));
                }
            }

            if (folderGroups.Count == 0) return basePrompt;

            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                var allActiveTexts = folderGroups.SelectMany(g => g.Items).ToList();
                return string.Join(separator, allActiveTexts);
            }

            // Categorize items by PlacementMode
            var atBeginningTexts = new List<string>();
            var afterCommaMap = new Dictionary<int, List<string>>();
            var atEndTexts = new List<string>();

            foreach (var group in folderGroups)
            {
                var mode = group.Folder?.PlacementMode ?? CombinerPlacementMode.AfterComma;
                int cIdx = group.Folder?.CommaIndex ?? 1;
                if (cIdx <= 0) cIdx = 1;

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

            // Find all comma positions in basePrompt
            var commaPositions = new List<int>();
            for (int i = 0; i < basePrompt.Length; i++)
            {
                if (basePrompt[i] == ',')
                {
                    commaPositions.Add(i);
                }
            }

            // Map each target comma index (1-indexed) to its list of snippet strings.
            // If target comma index > available commas in basePrompt, map to the last available comma (commaPositions.Count)
            var insertionMap = new Dictionary<int, List<string>>();

            foreach (var kvp in afterCommaMap)
            {
                int targetIdx = kvp.Key;
                int effectiveIdx = targetIdx;

                if (commaPositions.Count > 0)
                {
                    if (effectiveIdx > commaPositions.Count)
                    {
                        effectiveIdx = commaPositions.Count; // Place after the last available comma
                    }
                }

                if (!insertionMap.ContainsKey(effectiveIdx))
                {
                    insertionMap[effectiveIdx] = new List<string>();
                }
                insertionMap[effectiveIdx].AddRange(kvp.Value);
            }

            // Reconstruct prompt cleanly
            string result = "";

            // Step 1: Prepend AtBeginning items
            if (atBeginningTexts.Count > 0)
            {
                result = string.Join(separator, atBeginningTexts) + separator;
            }

            // Step 2: Base prompt with AfterComma insertions
            if (commaPositions.Count == 0)
            {
                // No commas in basePrompt at all
                result += basePrompt;

                // Any AfterComma items attach right after basePrompt
                var allAfterCommaItems = insertionMap.Values.SelectMany(x => x).ToList();
                if (allAfterCommaItems.Count > 0)
                {
                    result += separator + string.Join(separator, allAfterCommaItems);
                }
            }
            else
            {
                // Insert after commas based on commaPositions
                int lastPos = 0;
                for (int c = 0; c < commaPositions.Count; c++)
                {
                    int commaPos = commaPositions[c];
                    int commaNumber = c + 1;

                    // Append basePrompt segment up to and including comma
                    result += basePrompt.Substring(lastPos, commaPos - lastPos + 1);
                    lastPos = commaPos + 1;

                    // Check if there are snippets to insert after this comma
                    if (insertionMap.TryGetValue(commaNumber, out var snippetsToInsert) && snippetsToInsert.Count > 0)
                    {
                        result += " " + string.Join(separator, snippetsToInsert) + separator;
                    }
                }

                // Append remaining basePrompt after last comma
                if (lastPos < basePrompt.Length)
                {
                    result += basePrompt.Substring(lastPos);
                }
            }

            // Step 3: Append AtEnd items
            if (atEndTexts.Count > 0)
            {
                result += separator + string.Join(separator, atEndTexts);
            }

            return CleanUpCommas(result);
        }

        private static string InsertAfterComma(string basePrompt, string snippetsText, int commaIndex, string separator)
        {
            if (string.IsNullOrWhiteSpace(basePrompt)) return snippetsText ?? "";
            if (string.IsNullOrWhiteSpace(snippetsText)) return basePrompt;
            if (commaIndex <= 0) commaIndex = 1;
            if (string.IsNullOrWhiteSpace(separator)) separator = ", ";

            var commaPositions = new List<int>();
            for (int i = 0; i < basePrompt.Length; i++)
            {
                if (basePrompt[i] == ',')
                {
                    commaPositions.Add(i);
                }
            }

            if (commaPositions.Count == 0)
            {
                return CleanUpCommas($"{basePrompt}{separator}{snippetsText}");
            }

            int targetIdx = commaIndex - 1; // 0-based index
            if (targetIdx >= commaPositions.Count)
            {
                targetIdx = commaPositions.Count - 1; // Fallback to last existing comma
            }

            int insertPos = commaPositions[targetIdx];
            string part1 = basePrompt.Substring(0, insertPos + 1).TrimEnd();
            string part2 = basePrompt.Substring(insertPos + 1).TrimStart();

            if (!string.IsNullOrWhiteSpace(part2))
            {
                return CleanUpCommas($"{part1} {snippetsText}{separator}{part2}");
            }
            else
            {
                return CleanUpCommas($"{part1} {snippetsText}");
            }
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
            return cleaned.Trim();
        }

        private static bool PromptContainsSnippet(string prompt, string snippet)
        {
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(snippet)) return false;

            string p = prompt.Trim();
            string s = snippet.Trim();

            // 1. Exact match
            if (p.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;

            // 2. Tag-level match
            var pTags = p.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim())
                         .ToList();

            var sTags = s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
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
