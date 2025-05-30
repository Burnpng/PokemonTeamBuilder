using System.Collections.Generic;
using System.Linq;

namespace PokemonTeamBuilder
{
    public static class PokemonStatAverages
    {
        private static readonly Dictionary<int, StatAverages> _cache = new();

        public static void PrecalculateForGame(PokemonDbContext dbContext, Game game)
        {
            if (_cache.ContainsKey(game.GameId))
                return;

            int gameGen = game.Generation;

            var candidates = dbContext.PokemonAvailability
                .Where(pa => pa.GameId == game.GameId)
                .Select(pa => pa.Pokemon)
                .Where(p =>
                    p != null &&
                    p.IsFinalEvolution &&
                    (p.FinalEvoFinalGen == null || p.FinalEvoFinalGen >= gameGen))
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
            {
                _cache[game.GameId] = new StatAverages(); // Prevent future recalculations
                return;
            }

            double avgHP = candidates.Average(p => p.BaseHP ?? 0);
            double avgAtk = candidates.Average(p => p.BaseAttack ?? 0);
            double avgDef = candidates.Average(p => p.BaseDefense ?? 0);
            double avgSpAtk = candidates.Average(p => p.BaseSpAttack ?? 0);
            double avgSpDef = candidates.Average(p => p.BaseSpDefense ?? 0);
            double avgSpeed = candidates.Average(p => p.BaseSpeed ?? 0);

            _cache[game.GameId] = new StatAverages
            {
                HP = avgHP,
                Atk = avgAtk,
                Def = avgDef,
                SpAtk = avgSpAtk,
                SpDef = avgSpDef,
                Speed = avgSpeed
            };
        }

        public static StatAverages Get(Game game)
        {
            return _cache.TryGetValue(game.GameId, out var avg) ? avg : new StatAverages();
        }
    }

    public class StatAverages
    {
        public double HP { get; set; }
        public double Atk { get; set; }
        public double Def { get; set; }
        public double SpAtk { get; set; }
        public double SpDef { get; set; }
        public double Speed { get; set; }
    }
}
