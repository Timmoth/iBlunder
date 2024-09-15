﻿using System.Runtime.CompilerServices;
using Sapling.Engine.MoveGen;
using Sapling.Engine.Transpositions;

namespace Sapling.Engine.Search;

public class ParallelSearcher
{
    public readonly List<Searcher> Searchers = new();
    public readonly Transposition[] Transpositions;

    // Used to prevent a previous searches timeout cancelling a new search
    private Guid _prevSearchId = Guid.NewGuid();
    public ParallelSearcher(Transposition[] transpositions)
    {
        Transpositions = transpositions;

        // Default to one thread
        Searchers.Add(new Searcher(Transpositions));
    }   
    
    public ParallelSearcher()
    {
        const int transpositionSize = 0b1111_1111_1111_1111_1111_1111;
        Transpositions = GC.AllocateArray<Transposition>(transpositionSize, true);

        // Default to one thread
        Searchers.Add(new Searcher(Transpositions));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveFromToIndex(uint move)
    {
        return MoveExtensions.BitFieldExtract(move, 4, 6) * 64 +
               MoveExtensions.BitFieldExtract(move, 10, 6);
    }

    public static int ThreadValue(int score, int worstScore, int depth)
    {
        return (score - worstScore) * depth;
    }

    public void Stop()
    {
        foreach (var searcher in Searchers)
        {
            searcher.Stop();
        }
    }

    public (uint move, int depthSearched, int score, uint ponder, int nodes, TimeSpan duration) NodeBoundSearch(
        BoardState state, int nodeLimit, int maxDepth)
    {
        Searchers[0].Board = state;

        var start = DateTime.Now;
        var searchResult = Searchers[0].Search(nodeLimit, maxDepth);
        return (searchResult.move, searchResult.depthSearched, searchResult.score, searchResult.ponder,
            searchResult.nodes, DateTime.Now - start);
    }

    public (uint move, int depthSearched, int score, uint ponder, int nodes, TimeSpan duration) TimeBoundSearch(
        BoardState state, int thinkTime)
    {
        var newSearchId = Guid.NewGuid();
        _prevSearchId = newSearchId;

        Task.Delay(thinkTime).ContinueWith(t =>
        {
            if (_prevSearchId != newSearchId)
            {
                // Prevent a previous searches timeout cancelling a new search
                return;
            }

            // Stop all searchers once think time has been reached
            foreach (var searcher in Searchers)
            {
                searcher.Stop();
            }
        });

        // Initialize each searcher with its own copy of the board state
        foreach (var searcher in Searchers)
        {
            searcher.Init(0, state.Clone());
        }

        var start = DateTime.Now;

        if (Searchers.Count == 1)
        {
            var searchResult = Searchers[0].Search();
            return (searchResult.move, searchResult.depthSearched, searchResult.score, searchResult.ponder,
                searchResult.nodes, DateTime.Now - start);
        }

        // Thread-local storage for best move in each thread
        var results =
            new ThreadLocal<(uint move, int depthSearched, int score, uint ponder, int nodes)>(
                () => (0, 0, int.MinValue, 0, 0), true);


        // Parallel search, with thread-local best move
        Parallel.For(0, Searchers.Count,
            i => { results.Value = Searchers[i].Search(); });

        var dt = DateTime.Now - start;

        Span<int> voteMap = stackalloc int[64 * 64];
        var worstScore = int.MaxValue;
        var nodes = 0;
        // First pass: Initialize the worst score and reset vote map
        foreach (var result in results.Values)
        {
            worstScore = Math.Min(worstScore, result.score);
            nodes += result.nodes;
        }

        // Second pass: Accumulate votes
        foreach (var result in results.Values)
        {
            voteMap[MoveFromToIndex(result.move)] += ThreadValue(result.score, worstScore, result.depthSearched);
        }

        // Initialize best thread and best scores
        var bestMove = results.Values[0].move;
        var bestScore = results.Values[0].score;
        var bestDepth = results.Values[0].depthSearched;
        var bestPonder = results.Values[0].ponder;
        var bestVoteScore = voteMap[MoveFromToIndex(results.Values[0].move)];

        // Find the best thread
        for (var i = 1; i < results.Values.Count; i++)
        {
            var currentVoteScore = voteMap[MoveFromToIndex(results.Values[i].move)];
            if (currentVoteScore <= bestVoteScore)
            {
                continue;
            }

            bestMove = results.Values[i].move;
            bestScore = results.Values[i].score;
            bestDepth = results.Values[i].depthSearched;
            bestPonder = results.Values[i].ponder;
            bestVoteScore = currentVoteScore;
        }

        return (bestMove, bestDepth, bestScore, bestPonder, nodes, dt);
    }

    public (uint move, int depthSearched, int score, uint ponder, int nodes, TimeSpan duration) DepthBoundSearch(
        BoardState state, int depth)
    {
        var searchId = Guid.NewGuid();
        _prevSearchId = searchId;

        // Initialize each searcher with its own copy of the board state
        foreach (var searcher in Searchers)
        {
            searcher.Init(0, state.Clone());
        }

        // Thread-local storage for best move in each thread
        var results =
            new ThreadLocal<(uint move, int depthSearched, int score, uint ponder, int nodes)>(
                () => (0, 0, int.MinValue, 0, 0), true);

        var start = DateTime.Now;

        // Parallel search, with thread-local best move
        Parallel.For(0, Searchers.Count, i => { results.Value = Searchers[i].DepthBoundSearch(depth); });
        var dt = DateTime.Now - start;

        Span<int> voteMap = stackalloc int[64 * 64];
        var worstScore = int.MaxValue;
        var nodes = 0;
        // First pass: Initialize the worst score and reset vote map
        foreach (var result in results.Values)
        {
            worstScore = Math.Min(worstScore, result.score);
            nodes += result.nodes;
        }

        // Second pass: Accumulate votes
        foreach (var result in results.Values)
        {
            voteMap[MoveFromToIndex(result.move)] += ThreadValue(result.score, worstScore, result.depthSearched);
        }

        // Initialize best thread and best scores
        var bestMove = results.Values[0].move;
        var bestScore = results.Values[0].score;
        var bestDepth = results.Values[0].depthSearched;
        var bestPonder = results.Values[0].ponder;
        var bestVoteScore = voteMap[MoveFromToIndex(results.Values[0].move)];

        // Find the best thread
        for (var i = 1; i < results.Values.Count; i++)
        {
            var currentVoteScore = voteMap[MoveFromToIndex(results.Values[i].move)];
            if (currentVoteScore > bestVoteScore)
            {
                bestMove = results.Values[i].move;
                bestScore = results.Values[i].score;
                bestDepth = results.Values[i].depthSearched;
                bestPonder = results.Values[i].ponder;
                bestVoteScore = currentVoteScore;
            }
        }

        return (bestMove, bestDepth, bestScore, bestPonder, nodes, dt);
    }

    public void SetThreads(int searchThreads)
    {
        // Clamp the number of threads to be between 1 and the number of available processor cores
        searchThreads = Math.Clamp(searchThreads, 1, Environment.ProcessorCount);

        // Get the current number of searchers
        var currentSearcherCount = Searchers.Count;

        // If the current number of searchers is equal to the desired number, do nothing
        if (currentSearcherCount == searchThreads)
        {
            return;
        }

        // If there are more searchers than needed, remove the excess ones
        if (currentSearcherCount > searchThreads)
        {
            for (var i = currentSearcherCount - 1; i >= searchThreads; i--)
            {
                Searchers.RemoveAt(i);
            }
        }
        else
        {
            // If there are fewer searchers than needed, add the required number
            for (var i = currentSearcherCount; i < searchThreads; i++)
            {
                Searchers.Add(new Searcher(Transpositions));
            }
        }
    }
}