using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonTeamBuilder
{
    public class TeamSuggestionEngine
    {
        private readonly PokemonDbContext _dbContext;
        private readonly Game _selectedGame;
        private readonly List<Pokemon?> _currentTeam;
        private readonly bool _includeLegendaries;
        private readonly bool _includeMythicals;
        private readonly Dictionary<string, List<string>> _typeSuperEffectiveMap;
        private readonly List<TypeEffectiveness> _typeEffectivenessList;
        private readonly List<PokemonAvailability> _pokemonAvailabilityList;
        private readonly List<ExclusivityGroupMember> _exclusivityGroupMembersList;
        private readonly List<ExclusivityGroup> _exclusivityGroupsList;

        public TeamSuggestionEngine(
            PokemonDbContext dbContext,
            Game selectedGame,
            List<Pokemon?> currentTeam,
            bool includeLegendaries,
            bool includeMythicals)
        {
            _dbContext = dbContext;
            _selectedGame = selectedGame;
            _currentTeam = currentTeam;
            _includeLegendaries = includeLegendaries;
            _includeMythicals = includeMythicals;

            // Cache all needed data in memory
            _typeEffectivenessList = [.. _dbContext.TypeEffectiveness];
            _pokemonAvailabilityList = [.. _dbContext.PokemonAvailability.Where(pa => pa.GameId == selectedGame.GameId)];
            _exclusivityGroupMembersList = [.. _dbContext.ExclusivityGroupMembers];
            _exclusivityGroupsList = [.. _dbContext.ExclusivityGroups];

            _typeSuperEffectiveMap = _typeEffectivenessList
                .Where(te => te.EffectivenessMultiplier > 1.0)
                .GroupBy(te => te.AttackingType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(te => te.DefendingType).Distinct().ToList()
                );
        }

        public List<Pokemon> SuggestTeam()
        {
            return BuildSuggestedTeam(); // No parameters
        }


        private List<Pokemon> GetValidCandidates()
        {
            int gameGen = _selectedGame.Generation;
            int gameId = _selectedGame.GameId;

            // Step 1: Get current team Pokémon IDs
            var teamPokemonIds = _currentTeam
                .Where(p => p != null)
                .Select(p => p!.PokemonId)
                .ToList();

            // Step 2: Determine mutually exclusive Pokémon IDs
            HashSet<int> mutuallyExclusiveIds = [];

            if (teamPokemonIds.Count != 0)
            {
                var groupIds = _exclusivityGroupMembersList
                    .Where(m => teamPokemonIds.Contains(m.PokemonId))
                    .Join(_exclusivityGroupsList,
                          m => m.GroupId,
                          g => g.GroupId,
                          (m, g) => new { m.PokemonId, g.GroupId, g.GameId })
                    .Where(x => x.GameId == gameId)
                    .Select(x => x.GroupId)
                    .Distinct()
                    .ToList();

                if (groupIds.Count != 0)
                {
                    mutuallyExclusiveIds = [.. _exclusivityGroupMembersList
                        .Where(m => groupIds.Contains(m.GroupId) && !teamPokemonIds.Contains(m.PokemonId))
                        .Join(_exclusivityGroupsList,
                            m => m.GroupId,
                            g => g.GroupId,
                            (m, g) => new { m.PokemonId, g.GameId })
                        .Where(x => x.GameId == gameId)
                        .Select(x => x.PokemonId)];
                }
            }

            // Step 3: Filter valid candidates
            var candidates = _pokemonAvailabilityList
                .Select(pa => pa.Pokemon)
                .Where(p => p is not null
                    && p.IsFinalEvolution
                    && (p.FinalEvoFinalGen == null || p.FinalEvoFinalGen >= gameGen)
                    && (_includeLegendaries || !p.IsLegendary)
                    && (_includeMythicals || !p.IsMythical)
                    && !mutuallyExclusiveIds.Contains(p.PokemonId))
                .Distinct()
                .ToList();


            return candidates!;
        }



        private List<(Pokemon Pokemon, double Score)> RankCandidates(List<Pokemon> candidates, List<Pokemon> currentTeam)
        {
            var scoredList = new List<(Pokemon Pokemon, double Score)>();
            var avgStats = PokemonStatAverages.Get(_selectedGame);

            foreach (var candidate in candidates)
            {
                double score = 0;

                // 1. Offensive type coverage boost
                double offensiveCoverage = CalculateOffensiveCoverageScore(candidate);
                score += offensiveCoverage * 1.0;

                // 2. Defensive type diversity bonus (existing)
                double defensiveCoverage = CalculateDefensiveCoverageScore(candidate);
                score += defensiveCoverage * 1.0;

                // 3. BST weight with synergy multiplier based on stat averages (offensive/defensive)
                int atk = candidate.BaseAttack ?? 0;
                int spAtk = candidate.BaseSpAttack ?? 0;
                int speed = candidate.BaseSpeed ?? 0;
                int hp = candidate.BaseHP ?? 0;
                int def = candidate.BaseDefense ?? 0;
                int spDef = candidate.BaseSpDefense ?? 0;

                bool isOffensiveStatline =
                    (atk > avgStats.Atk || spAtk > avgStats.SpAtk)
                    && speed > avgStats.Speed;
                int candidateEffectiveTypesCount = GetCandidateEffectiveTypesCount(candidate);
                bool hasHighOffensiveCoverage = candidateEffectiveTypesCount >= 6; // adjust as needed

                bool isDefensiveStatline =
                    (hp > avgStats.HP || def > avgStats.Def || spDef > avgStats.SpDef);
                double defensiveEffectiveTypesCount = CalculateIntrinsicDefensiveTypingScore(candidate);
                bool hasHighDefensiveCoverage = defensiveEffectiveTypesCount >= 7; // adjust as needed

                double synergyMultiplier = 1.0;
                if (isOffensiveStatline && hasHighOffensiveCoverage)
                    synergyMultiplier = 1.1;
                else if (isDefensiveStatline && hasHighDefensiveCoverage)
                    synergyMultiplier = 1.1;
                else if (isOffensiveStatline || hasHighDefensiveCoverage)
                    synergyMultiplier = 0.9;
                else if (isDefensiveStatline || hasHighOffensiveCoverage)
                    synergyMultiplier = 0.9;
                else
                    synergyMultiplier = 1.0;

                score += CalculateDynamicBSTScore(candidate) * synergyMultiplier * 1.0;

                // 4. Debut generation match boost
                if (GetGenerationFromDexId(candidate.DexID) == _selectedGame.Generation)
                    score *= 1.1;

                scoredList.Add((candidate, score));
            }

            return [.. scoredList.OrderByDescending(entry => entry.Score)];
        }

        private static int GetGenerationFromDexId(int DexID)
        {
            if (DexID >= 1 && DexID <= 151)
                return 1;
            if (DexID >= 152 && DexID <= 251)
                return 2;
            if (DexID >= 252 && DexID <= 386)
                return 3;
            if (DexID >= 387 && DexID <= 493)
                return 4;
            if (DexID >= 494 && DexID <= 649)
                return 5;
            if (DexID >= 650 && DexID <= 721)
                return 6;
            if (DexID >= 722 && DexID <= 809)
                return 7;
            if (DexID >= 810 && DexID <= 905)
                return 8;
            if (DexID >= 906 && DexID <= 1025)
                return 9;

            return 0; // fallback if something's wrong
        }


        private double CalculateDynamicBSTScore(Pokemon candidate)
        {
            static int Safe(int? val) => val ?? 0;

            // Get cached averages for the selected game
            var avgStats = PokemonStatAverages.Get(_selectedGame);

            double avgHP = avgStats.HP;
            double avgAtk = avgStats.Atk;
            double avgDef = avgStats.Def;
            double avgSpAtk = avgStats.SpAtk;
            double avgSpDef = avgStats.SpDef;
            double avgSpeed = avgStats.Speed;

            // Calculate current team stat totals
            int teamCount = 0;
            double teamHP = 0, teamAtk = 0, teamDef = 0, teamSpAtk = 0, teamSpDef = 0, teamSpeed = 0;

            foreach (var p in _currentTeam.Where(p => p != null))
            {
                teamHP += Safe(p!.BaseHP);
                teamAtk += Safe(p!.BaseAttack);
                teamDef += Safe(p!.BaseDefense);
                teamSpAtk += Safe(p!.BaseSpAttack);
                teamSpDef += Safe(p!.BaseSpDefense);
                teamSpeed += Safe(p!.BaseSpeed);
                teamCount++;
            }

            int candAtk = Safe(candidate.BaseAttack);
            int candSpAtk = Safe(candidate.BaseSpAttack);
            int candSpeed = Safe(candidate.BaseSpeed);
            int candHP = Safe(candidate.BaseHP);
            int candDef = Safe(candidate.BaseDefense);
            int candSpDef = Safe(candidate.BaseSpDefense);

            // Default: balanced weights
            double wHP = 1, wAtk = 1, wDef = 1, wSpAtk = 1, wSpDef = 1, wSpeed = 1;

            if (teamCount > 0)
            {
                double teamAvgHP = teamHP / teamCount;
                double teamAvgAtk = teamAtk / teamCount;
                double teamAvgDef = teamDef / teamCount;
                double teamAvgSpAtk = teamSpAtk / teamCount;
                double teamAvgSpDef = teamSpDef / teamCount;
                double teamAvgSpeed = teamSpeed / teamCount;

                // Compare team stats to generational average
                double defGap = (teamAvgHP - avgHP) + (teamAvgDef - avgDef) + (teamAvgSpDef - avgSpDef);
                double offGap = (teamAvgAtk - avgAtk) + (teamAvgSpAtk - avgSpAtk) + (teamAvgSpeed - avgSpeed);

                if (defGap > offGap + 10) // Defense-heavy team: emphasize offense
                {
                    // Emphasize Atk/SpAtk/Speed, de-emphasize defensive stats
                    wAtk = 1.3; wSpAtk = 1.3; wSpeed = 1.3;
                }
                else if (offGap > defGap + 10) // Offense-heavy team: emphasize defense
                {
                    // Emphasize HP/Def/SpDef, de-emphasize offensive stats
                    wHP = 1.3; wDef = 1.3; wSpDef = 1.3;
                }
                // else: keep all weights at 1 (balanced)
            }

            // Weighted BST calculation
            double weightedBST =
                candHP * wHP +
                candAtk * wAtk +
                candDef * wDef +
                candSpAtk * wSpAtk +
                candSpDef * wSpDef +
                candSpeed * wSpeed;

            // Normalize (optional: adjust scaling as needed)
            return weightedBST * 0.01;
        }

        private int GetCandidateEffectiveTypesCount(Pokemon candidate)
        {
            var candidateTypes = new[] { candidate.Type1, candidate.Type2 }.Where(t => t != null);
            var candidateEffectiveTypes = new HashSet<string>();
            foreach (var type in candidateTypes)
            {
                var effective = _typeEffectivenessList
                    .Where(te => te.AttackingType == type && te.EffectivenessMultiplier > 1.0)
                    .Select(te => te.DefendingType)
                    .Distinct();
                foreach (var t in effective)
                    candidateEffectiveTypes.Add(t);
            }
            return candidateEffectiveTypes.Count;
        }


        private double CalculateOffensiveCoverageScore(Pokemon candidate)
        {
            // Step 1: Determine already-covered offensive types
            var coveredTypes = new HashSet<string>();

            foreach (var teamMember in _currentTeam.Where(p => p != null))
            {
                var memberTypes = new[] { teamMember!.Type1, teamMember.Type2 }.Where(t => t != null);
                foreach (var type in memberTypes)
                {
                    var effectiveTypes = _typeEffectivenessList
                        .Where(te => te.AttackingType == type && te.EffectivenessMultiplier > 1.0)
                        .Select(te => te.DefendingType)
                        .Distinct();

                    foreach (var effType in effectiveTypes)
                        coveredTypes.Add(effType);
                }
            }

            // Step 2: Get types candidate can hit super-effectively
            var candidateTypes = new[] { candidate.Type1, candidate.Type2 }.Where(t => t != null);
            var candidateEffectiveTypes = new HashSet<string>();

            foreach (var type in candidateTypes)
            {
                var effective = _typeEffectivenessList
                    .Where(te => te.AttackingType == type && te.EffectivenessMultiplier > 1.0)
                    .Select(te => te.DefendingType)
                    .Distinct();

                foreach (var t in effective)
                    candidateEffectiveTypes.Add(t);
            }

            // Step 3: Calculate score with diminishing returns for overlap
            double score = 0.0;

            foreach (var type in candidateEffectiveTypes)
            {
                if (!coveredTypes.Contains(type))
                    score += 1.0;           // Full point for new type coverage
                else
                    score += 0.25;          // Reduced score for overlapping coverage
            }

            return score;
        }

        private double CalculateIntrinsicDefensiveTypingScore(Pokemon candidate)
        {
            // Get all types in the game
            var allTypes = _typeEffectivenessList
                .Select(te => te.AttackingType)
                .Distinct()
                .ToList();

            var candidateTypes = new[] { candidate.Type1, candidate.Type2 }.Where(t => t != null).ToList();

            int weaknessCount = 0;
            int resistanceCount = 0;
            int immunityCount = 0;

            foreach (var attackingType in allTypes)
            {
                double multiplier = 1.0;
                foreach (var defendingType in candidateTypes)
                {
                    var eff = _typeEffectivenessList
                        .FirstOrDefault(te => te.AttackingType == attackingType && te.DefendingType == defendingType);
                    if (eff != null)
                        multiplier *= eff.EffectivenessMultiplier;
                }

                if (multiplier > 1.0)
                    weaknessCount++;
                else if (multiplier == 0.0)
                    immunityCount++;
                else if (multiplier < 1.0)
                    resistanceCount++;
            }


            // Scoring: reward resistances/immunities, penalize weaknesses
            // You can tune these weights as needed
            double score = (resistanceCount * 1.0) + (immunityCount * 2.0) - (weaknessCount * 1.5);

            // Normalize to a positive score
            return Math.Max(0, score);
        }


        /// <summary>
        /// Calculates the defensive diversity score and applies a penalty for high weaknesses for a candidate Pokémon.
        /// </summary>
        /// <param name="candidate">The candidate <see cref="Pokemon"/> to evaluate.</param>
        /// <returns>
        /// A score representing the defensive coverage and diversity the candidate adds to the team.
        /// Higher scores indicate better defensive diversity and fewer severe weaknesses.
        /// </returns>
        private double CalculateDefensiveCoverageScore(Pokemon candidate)
        {
            // Step 1: Defensive diversity score (your existing method)
            var typeCounts = new Dictionary<string, int>();
            foreach (var member in _currentTeam.Where(p => p != null))
            {
                var types = new[] { member!.Type1, member.Type2 }.Where(t => t != null);
                foreach (var type in types)
                {
                    if (!typeCounts.ContainsKey(type!)) typeCounts[type!] = 0;
                    typeCounts[type!]++;
                }
            }

            var candidateTypes = new[] { candidate.Type1, candidate.Type2 }.Where(t => t != null);
            double diversityScore = 0.0;
            foreach (var type in candidateTypes)
            {
                typeCounts.TryGetValue(type!, out int count);
                diversityScore += 1.0 / Math.Pow(2, count);
            }

            // Step 2: Calculate combined weaknesses (multiply effectiveness)
            // Use _typeEffectivenessList for effectiveness multipliers
            var allTypes = _typeEffectivenessList
                .Select(te => te.DefendingType)
                .Distinct()
                .ToList();
            double maxWeaknessMultiplier = 1.0;

            foreach (var atkType in allTypes)
            {
                double multiplier = 1.0;
                foreach (var defType in candidateTypes)
                {
                    var eff = _typeEffectivenessList
                                .FirstOrDefault(te => te.AttackingType == atkType && te.DefendingType == defType);
                    if (eff != null)
                        multiplier *= eff.EffectivenessMultiplier;
                    else
                        multiplier *= 1.0;
                }
                if (multiplier > maxWeaknessMultiplier)
                    maxWeaknessMultiplier = multiplier;
            }

            // Step 3: Apply penalty for high weakness (e.g., 4x weakness)
            // Example: reduce score by penalty proportional to excess weakness beyond 2x
            double weaknessPenalty = 0.0;
            if (maxWeaknessMultiplier > 2.0)
                weaknessPenalty = (maxWeaknessMultiplier - 2.0) * 0.5;  // tune this factor as needed

            double finalScore = diversityScore - weaknessPenalty;
            if (finalScore < 0) finalScore = 0;  // avoid negative scores

            return finalScore;
        }



        /// <summary>
        /// Builds a suggested team of Pokémon by iteratively selecting the best candidates to fill empty team slots.
        /// </summary>
        /// <returns>
        /// A list of <see cref="Pokemon"/> representing the suggested team, up to a maximum of 6 members.
        /// </returns>
        private List<Pokemon> BuildSuggestedTeam()
        {
            // Instead of copying, work directly with the field
            var suggestedTeam = _currentTeam;

            while (suggestedTeam.Count(p => p != null) < 6)
            {
                var nonNullTeam = suggestedTeam.Where(p => p != null).Cast<Pokemon>().ToList();

                var candidates = GetValidCandidates()
                    .Where(p => !nonNullTeam.Contains(p))
                    .ToList();

                if (candidates.Count == 0)
                    break;

                var scored = RankCandidates(candidates, nonNullTeam);
                var best = scored.FirstOrDefault();

                if (best.Pokemon == null)
                    break;

                for (int i = 0; i < suggestedTeam.Count; i++)
                {
                    if (suggestedTeam[i] == null)
                    {
                        suggestedTeam[i] = best.Pokemon;
                        break;
                    }
                }
            }

            return [.. suggestedTeam.Where(p => p != null).Cast<Pokemon>()];
        }

    }

}
