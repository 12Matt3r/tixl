using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using TiXL.Editor.Core.Models;

namespace TiXL.Editor.Core.Commands
{
    /// <summary>
    /// Interface for fuzzy search functionality
    /// </summary>
    public interface IFuzzySearchEngine
    {
        /// <summary>
        /// Searches commands using fuzzy matching
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="commands">Commands to search through</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <returns>Search results sorted by relevance</returns>
        ImmutableArray<CommandSearchResult> Search(string query, ImmutableArray<CommandDefinition> commands, int maxResults = 50);
    }

    /// <summary>
    /// Advanced fuzzy search engine with scoring algorithms
    /// </summary>
    public class AdvancedFuzzySearchEngine : IFuzzySearchEngine
    {
        /// <summary>
        /// Minimum score threshold for results
        /// </summary>
        private const float MinScore = 0.1f;

        /// <summary>
        /// Weights for different match types
        /// </summary>
        private const float ExactMatchWeight = 1.0f;
        private const float PrefixMatchWeight = 0.8f;
        private const float FuzzyMatchWeight = 0.6f;
        private const float KeywordMatchWeight = 0.7f;

        /// <summary>
        /// Character mismatch penalty
        /// </summary>
        private const float MismatchPenalty = 0.1f;

        public ImmutableArray<CommandSearchResult> Search(string query, ImmutableArray<CommandDefinition> commands, int maxResults = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Return recent or highly prioritized commands when no query
                return GetDefaultResults(commands, maxResults);
            }

            var queryLower = query.ToLowerInvariant();
            var results = new List<(CommandDefinition Command, float Score, List<MatchRange> NameMatches, List<MatchRange> DescriptionMatches, List<KeywordMatch> KeywordMatches)>();

            foreach (var command in commands)
            {
                if (!command.IsEnabled) continue;

                var scoreResult = CalculateScore(queryLower, command);
                if (scoreResult.Score >= MinScore)
                {
                    results.Add((command, scoreResult.Score, scoreResult.NameMatches, scoreResult.DescriptionMatches, scoreResult.KeywordMatches));
                }
            }

            // Sort by score (descending) and take top results
            var sortedResults = results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Command.Name)
                .Take(maxResults)
                .ToList();

            // Convert to CommandSearchResult objects
            return sortedResults
                .Select(r => new CommandSearchResult
                {
                    Command = r.Command,
                    Score = r.Score,
                    NameMatches = r.NameMatches.ToImmutableArray(),
                    DescriptionMatches = r.DescriptionMatches.ToImmutableArray(),
                    KeywordMatches = r.KeywordMatches.ToImmutableArray()
                })
                .ToImmutableArray();
        }

        /// <summary>
        /// Calculates search score for a command
        /// </summary>
        private SearchScoreResult CalculateScore(string query, CommandDefinition command)
        {
            var nameLower = command.Name.ToLowerInvariant();
            var descriptionLower = command.Description.ToLowerInvariant();

            float totalScore = 0f;
            var nameMatches = new List<MatchRange>();
            var descriptionMatches = new List<MatchRange>();
            var keywordMatches = new List<KeywordMatch>();

            // Check exact match
            if (nameLower == query)
            {
                totalScore += ExactMatchWeight * 2f; // Double weight for exact name match
                nameMatches.Add(new MatchRange { Start = 0, Length = command.Name.Length });
                return new SearchScoreResult(totalScore, nameMatches, descriptionMatches, keywordMatches);
            }

            // Check prefix match
            if (nameLower.StartsWith(query))
            {
                totalScore += PrefixMatchWeight * 1.5f;
                nameMatches.Add(new MatchRange { Start = 0, Length = query.Length });
            }

            // Check for query words in name (substring search)
            var nameScore = SearchString(query, nameLower);
            if (nameScore.Score > 0)
            {
                totalScore += nameScore.Score;
                nameMatches.AddRange(nameScore.Matches);
            }

            // Check description for matches
            var descScore = SearchString(query, descriptionLower);
            if (descScore.Score > 0)
            {
                totalScore += descScore.Score * 0.7f; // Lower weight for description matches
                descriptionMatches.AddRange(descScore.Matches);
            }

            // Check keywords
            foreach (var keyword in command.Keywords)
            {
                var keywordLower = keyword.ToLowerInvariant();
                
                // Exact keyword match
                if (keywordLower == query)
                {
                    totalScore += KeywordMatchWeight * 1.5f;
                    keywordMatches.Add(new KeywordMatch
                    {
                        Keyword = keyword,
                        Score = KeywordMatchWeight * 1.5f,
                        Range = new MatchRange { Start = 0, Length = keyword.Length }
                    });
                }
                // Keyword contains query
                else if (keywordLower.Contains(query))
                {
                    var keywordScore = SearchString(query, keywordLower);
                    if (keywordScore.Score > 0)
                    {
                        totalScore += keywordScore.Score * KeywordMatchWeight;
                        keywordMatches.Add(new KeywordMatch
                        {
                            Keyword = keyword,
                            Score = keywordScore.Score * KeywordMatchWeight,
                            Range = keywordScore.Matches.FirstOrDefault() ?? new MatchRange()
                        });
                    }
                }
            }

            // Apply usage count bonus (boost popular commands)
            var usageBonus = MathF.Log10(command.UsageCount + 1) * 0.1f;
            totalScore += usageBonus;

            // Apply priority bonus
            totalScore += command.Priority * 0.05f;

            return new SearchScoreResult(totalScore, nameMatches, descriptionMatches, keywordMatches);
        }

        /// <summary>
        /// Searches for query within a string using fuzzy matching
        /// </summary>
        private SearchStringResult SearchString(string query, string target)
        {
            float score = 0f;
            var matches = new List<MatchRange>();

            // Simple substring search with fuzzy matching
            for (int i = 0; i <= target.Length - query.Length; i++)
            {
                float stringScore = 0f;
                int matchedChars = 0;

                for (int j = 0; j < query.Length; j++)
                {
                    if (i + j < target.Length && target[i + j] == query[j])
                    {
                        matchedChars++;
                        stringScore += 1f - (j * 0.1f); // Earlier characters are worth more
                    }
                    else
                    {
                        stringScore -= MismatchPenalty;
                    }
                }

                if (matchedChars > 0)
                {
                    var stringMatchScore = (matchedChars / (float)query.Length) * stringScore;
                    if (stringMatchScore > score * 0.7f) // Only take significantly better matches
                    {
                        score = stringMatchScore;
                        matches.Clear();
                        matches.Add(new MatchRange { Start = i, Length = matchedChars });
                    }
                }
            }

            return new SearchStringResult(score, matches);
        }

        /// <summary>
        /// Gets default results when no query is provided
        /// </summary>
        private ImmutableArray<CommandSearchResult> GetDefaultResults(ImmutableArray<CommandDefinition> commands, int maxResults)
        {
            // Sort by usage count, priority, and name
            var defaultResults = commands
                .Where(c => c.IsEnabled)
                .OrderByDescending(c => c.UsageCount)
                .ThenByDescending(c => c.Priority)
                .ThenBy(c => c.Name)
                .Take(maxResults)
                .Select(command => new CommandSearchResult
                {
                    Command = command,
                    Score = 1.0f, // Default score for no query
                    NameMatches = ImmutableArray<MatchRange>.Empty,
                    DescriptionMatches = ImmutableArray<MatchRange>.Empty,
                    KeywordMatches = ImmutableArray<KeywordMatch>.Empty
                })
                .ToImmutableArray();

            return defaultResults;
        }

        /// <summary>
        /// Result of search score calculation
        /// </summary>
        private record SearchScoreResult(
            float Score,
            List<MatchRange> NameMatches,
            List<MatchRange> DescriptionMatches,
            List<KeywordMatch> KeywordMatches);

        /// <summary>
        /// Result of string search
        /// </summary>
        private record SearchStringResult(
            float Score,
            List<MatchRange> Matches);

        /// <summary>
        /// Factory method to create a new fuzzy search engine instance
        /// </summary>
        public static IFuzzySearchEngine Create()
        {
            return new AdvancedFuzzySearchEngine();
        }
    }
}
