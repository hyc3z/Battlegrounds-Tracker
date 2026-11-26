using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobsBuddy.Simulation;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Automation
{
	internal sealed class PositioningOptimizationResult
	{
		public int[] CurrentEntityIds { get; }
		public int[] BestEntityIds { get; }
		public Output BaselineOutput { get; }
		public Output BestOutput { get; }
		public double ScoreGain { get; }
		public bool ShouldReorder { get; }

		public PositioningOptimizationResult(
			int[] currentEntityIds,
			int[] bestEntityIds,
			Output baselineOutput,
			Output bestOutput,
			double scoreGain,
			bool shouldReorder)
		{
			CurrentEntityIds = currentEntityIds;
			BestEntityIds = bestEntityIds;
			BaselineOutput = baselineOutput;
			BestOutput = bestOutput;
			ScoreGain = scoreGain;
			ShouldReorder = shouldReorder;
		}
	}

	internal readonly struct MinionDragMove
	{
		public int EntityId { get; }
		public int FromIndex { get; }
		public int TargetIndex { get; }

		public MinionDragMove(int entityId, int fromIndex, int targetIndex)
		{
			EntityId = entityId;
			FromIndex = fromIndex;
			TargetIndex = targetIndex;
		}
	}

	/// <summary>
	/// Uses the local BobsBuddy simulator to find the highest-EV player minion ordering against
	/// the last combat's opponent, and plans the minimal number of drags via LIS.
	/// </summary>
	internal static class PositioningOptimizer
	{
		private const int CoarseIterations = 300;
		private const int CoarseMaxDurationMs = 350;
		private const int RefineIterations = 3_000;
		private const int RefineMaxDurationMs = 1_200;
		private const int TopCandidatesToRefine = 4;
		private const double MinimumMeaningfulScoreGain = 0.015;

		internal static double Score(Output output)
			=> output.winRate + 0.5 * output.tieRate - 0.15 * output.myDeathRate + 0.05 * output.theirDeathRate;

		internal static async Task<PositioningOptimizationResult?> OptimizeAsync(
			BobsBuddyInvoker.RerunContext context,
			CancellationToken ct)
		{
			var n = context.PlayerEntityIds.Length;
			if(n == 0)
				return null;

			var identity = Enumerable.Range(0, n).ToArray();
			if(n == 1)
			{
				var single = await SimulateOrderAsync(context, identity, RefineIterations, RefineMaxDurationMs, ct);
				if(single == null)
					return null;
				return new PositioningOptimizationResult(
					context.PlayerEntityIds, context.PlayerEntityIds, single, single, 0, false);
			}

			var evaluated = new Dictionary<string, (int[] Order, Output Output, double Score)>();

			async Task<(int[] Order, Output Output, double Score)?> EvalCoarseAsync(int[] order)
			{
				ct.ThrowIfCancellationRequested();
				var key = string.Join(",", order);
				if(evaluated.TryGetValue(key, out var cached))
					return cached;

				var output = await SimulateOrderAsync(context, order, CoarseIterations, CoarseMaxDurationMs, ct);
				if(output == null)
					return null;

				var entry = (order, output, Score(output));
				evaluated[key] = entry;
				return entry;
			}

			// Always evaluate the current ordering first.
			var baselineCoarse = await EvalCoarseAsync(identity);
			if(baselineCoarse == null)
				return null;

			// Already a clean 100% win with no lethal risk: no need to waste time shuffling.
			if(baselineCoarse.Value.Output.winRate >= 0.999 && baselineCoarse.Value.Output.theirDeathRate >= 0.999)
			{
				var refinedBase = await SimulateOrderAsync(context, identity, RefineIterations, RefineMaxDurationMs, ct)
					?? baselineCoarse.Value.Output;
				return new PositioningOptimizationResult(
					context.PlayerEntityIds, context.PlayerEntityIds, refinedBase, refinedBase, 0, false);
			}

			if(n <= 4)
			{
				foreach(var perm in GenerateAllPermutations(n))
					await EvalCoarseAsync(perm);
			}
			else
			{
				// Stage 1: pairwise swaps, single-minion moves to front/back, and common heuristic seeds.
				var seeds = BuildHeuristicSeeds(context, n);
				foreach(var seed in seeds)
					await EvalCoarseAsync(seed);

				foreach(var neighbor in GeneratePairwiseSwaps(identity))
					await EvalCoarseAsync(neighbor);

				foreach(var neighbor in GenerateEdgeMoves(identity))
					await EvalCoarseAsync(neighbor);

				// Stage 2: 2-opt local polish around the top 2 distinct coarse orderings.
				var topSeeds = evaluated.Values
					.OrderByDescending(x => x.Score)
					.Select(x => x.Order)
					.Take(2)
					.ToList();

				foreach(var seed in topSeeds)
				{
					foreach(var neighbor in GeneratePairwiseSwaps(seed))
						await EvalCoarseAsync(neighbor);
				}
			}

			// Stage 3: high-iteration verification of baseline + top candidates to eliminate MC noise.
			var refineList = new List<int[]> { identity };
			var identityKey = string.Join(",", identity);
			foreach(var candidate in evaluated.Values.OrderByDescending(x => x.Score))
			{
				if(refineList.Count >= TopCandidatesToRefine + 1)
					break;
				if(string.Join(",", candidate.Order) == identityKey)
					continue;
				refineList.Add(candidate.Order);
			}

			Output? refinedBaseline = null;
			var bestOrder = identity;
			Output? bestOutput = null;
			var bestScore = double.NegativeInfinity;

			foreach(var order in refineList)
			{
				ct.ThrowIfCancellationRequested();
				var output = await SimulateOrderAsync(context, order, RefineIterations, RefineMaxDurationMs, ct);
				if(output == null)
					continue;
				var score = Score(output);
				if(order == identity)
					refinedBaseline = output;
				if(score > bestScore)
				{
					bestScore = score;
					bestOrder = order;
					bestOutput = output;
				}
			}

			if(refinedBaseline == null || bestOutput == null)
				return null;

			var baselineScore = Score(refinedBaseline);
			var gain = bestScore - baselineScore;
			var isDifferent = !bestOrder.SequenceEqual(identity);
			var shouldReorder = isDifferent && gain >= MinimumMeaningfulScoreGain;

			var bestEntityIds = bestOrder.Select(i => context.PlayerEntityIds[i]).ToArray();
			Log.Info($"PositioningOptimizer: evaluated {evaluated.Count} coarse + {refineList.Count} refined orderings. "
				+ $"Baseline Win={refinedBaseline.winRate * 100:0.#}%/Tie={refinedBaseline.tieRate * 100:0.#}% -> "
				+ $"Best Win={bestOutput.winRate * 100:0.#}%/Tie={bestOutput.tieRate * 100:0.#}% (gain={gain * 100:+0.#;-0.#}%, reorder={shouldReorder})");

			return new PositioningOptimizationResult(
				context.PlayerEntityIds,
				shouldReorder ? bestEntityIds : context.PlayerEntityIds,
				refinedBaseline,
				shouldReorder ? bestOutput : refinedBaseline,
				gain,
				shouldReorder);
		}

		/// <summary>
		/// Computes the minimal sequence of single-minion drag moves to transform <paramref name="currentIds"/>
		/// into <paramref name="targetIds"/> using Longest Increasing Subsequence (LIS).
		/// </summary>
		internal static List<MinionDragMove> PlanMinimalMoves(IReadOnlyList<int> currentIds, IReadOnlyList<int> targetIds)
		{
			var moves = new List<MinionDragMove>();
			if(currentIds.Count != targetIds.Count || currentIds.Count <= 1)
				return moves;

			var targetPos = new Dictionary<int, int>(targetIds.Count);
			for(var i = 0; i < targetIds.Count; i++)
				targetPos[targetIds[i]] = i;

			var currentTargetIndices = new int[currentIds.Count];
			for(var i = 0; i < currentIds.Count; i++)
			{
				if(!targetPos.TryGetValue(currentIds[i], out var pos))
					return moves;
				currentTargetIndices[i] = pos;
			}

			var lisIndices = ComputeLongestIncreasingSubsequenceIndices(currentTargetIndices);
			var placed = new HashSet<int>(lisIndices.Select(idx => currentIds[idx]));

			var working = new List<int>(currentIds);
			var toMove = targetIds.Where(id => !placed.Contains(id)).ToList();

			foreach(var entityId in toMove)
			{
				var fromIndex = working.IndexOf(entityId);
				if(fromIndex < 0)
					continue;

				working.RemoveAt(fromIndex);

				// Insert right before the first already-placed minion whose final rank is greater than ours.
				var insertIndex = -1;
				var entityRank = targetPos[entityId];
				for(var i = 0; i < working.Count; i++)
				{
					if(placed.Contains(working[i]) && targetPos[working[i]] > entityRank)
					{
						insertIndex = i;
						break;
					}
				}

				if(insertIndex < 0)
				{
					var lastPlaced = -1;
					for(var i = working.Count - 1; i >= 0; i--)
					{
						if(placed.Contains(working[i]))
						{
							lastPlaced = i;
							break;
						}
					}
					insertIndex = lastPlaced >= 0 ? lastPlaced + 1 : working.Count;
				}

				working.Insert(insertIndex, entityId);
				placed.Add(entityId);

				if(fromIndex != insertIndex)
					moves.Add(new MinionDragMove(entityId, fromIndex, insertIndex));
			}

			return moves;
		}

		private static List<int> ComputeLongestIncreasingSubsequenceIndices(int[] values)
		{
			var n = values.Length;
			var dp = new int[n];
			var prev = new int[n];
			var bestLen = 0;
			var bestEnd = 0;

			for(var i = 0; i < n; i++)
			{
				dp[i] = 1;
				prev[i] = -1;
				for(var j = 0; j < i; j++)
				{
					if(values[j] < values[i] && dp[j] + 1 > dp[i])
					{
						dp[i] = dp[j] + 1;
						prev[i] = j;
					}
				}
				if(dp[i] > bestLen)
				{
					bestLen = dp[i];
					bestEnd = i;
				}
			}

			var result = new List<int>(bestLen);
			for(var cur = bestEnd; cur >= 0; cur = prev[cur])
				result.Add(cur);
			result.Reverse();
			return result;
		}

		private static async Task<Output?> SimulateOrderAsync(
			BobsBuddyInvoker.RerunContext context,
			int[] order,
			int iterations,
			int maxDurationMs,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			var input = BobsBuddyInvoker.BuildReorderedInput(context, order);
			return await new SimulationRunner().SimulateMultiThreaded(
				input, iterations, BobsBuddyInvoker.ThreadCount, maxDurationMs);
		}

		private static IEnumerable<int[]> BuildHeuristicSeeds(BobsBuddyInvoker.RerunContext context, int n)
		{
			var side = context.BasePlayer.Side;
			var indices = Enumerable.Range(0, n).ToArray();

			// Reverse order
			yield return indices.Reverse().ToArray();

			// Highest attack first, taunts pushed to the right
			yield return indices
				.OrderBy(i => side[i].taunt ? 1 : 0)
				.ThenByDescending(i => side[i].cleave ? 1 : 0)
				.ThenByDescending(i => side[i].div > 0 ? 1 : 0)
				.ThenByDescending(i => side[i].baseAttack)
				.ToArray();

			// Lowest attack / shield-bumper first, biggest stats next, taunts last
			yield return indices
				.OrderBy(i => side[i].taunt ? 1 : 0)
				.ThenBy(i => side[i].baseAttack)
				.ToArray();

			// Biggest stats first regardless of taunt
			yield return indices
				.OrderByDescending(i => side[i].baseAttack + side[i].baseHealth)
				.ToArray();
		}

		private static IEnumerable<int[]> GeneratePairwiseSwaps(int[] baseOrder)
		{
			var n = baseOrder.Length;
			for(var i = 0; i < n; i++)
			{
				for(var j = i + 1; j < n; j++)
				{
					var copy = (int[])baseOrder.Clone();
					(copy[i], copy[j]) = (copy[j], copy[i]);
					yield return copy;
				}
			}
		}

		private static IEnumerable<int[]> GenerateEdgeMoves(int[] baseOrder)
		{
			var n = baseOrder.Length;
			for(var i = 0; i < n; i++)
			{
				if(i > 0)
				{
					var toFront = new List<int>(baseOrder);
					var val = toFront[i];
					toFront.RemoveAt(i);
					toFront.Insert(0, val);
					yield return toFront.ToArray();
				}
				if(i < n - 1)
				{
					var toBack = new List<int>(baseOrder);
					var val = toBack[i];
					toBack.RemoveAt(i);
					toBack.Add(val);
					yield return toBack.ToArray();
				}
			}
		}

		private static IEnumerable<int[]> GenerateAllPermutations(int n)
		{
			var arr = Enumerable.Range(0, n).ToArray();
			return Permute(arr, 0);
		}

		private static IEnumerable<int[]> Permute(int[] arr, int start)
		{
			if(start == arr.Length - 1)
			{
				yield return (int[])arr.Clone();
				yield break;
			}
			for(var i = start; i < arr.Length; i++)
			{
				(arr[start], arr[i]) = (arr[i], arr[start]);
				foreach(var p in Permute(arr, start + 1))
					yield return p;
				(arr[start], arr[i]) = (arr[i], arr[start]);
			}
		}
	}
}
