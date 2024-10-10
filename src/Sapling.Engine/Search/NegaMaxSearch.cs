using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Sapling.Engine.MoveGen;
using Sapling.Engine.Transpositions;
using Sapling.Engine.Tuning;
using Sapling.Engine.Evaluation;

namespace Sapling.Engine.Search;

public partial class Searcher
{
    public unsafe int
        NegaMaxSearch(BoardStateData* currentBoardState, AccumulatorState* currentAccumulatorState, int depthFromRoot, int depth,
            int alpha, int beta)
    {
        NodesVisited++;

        if (depthFromRoot >= Constants.MaxSearchDepth)
        {
            // Max depth reached, return evaluation
            return Evaluate(currentBoardState, currentAccumulatorState, depthFromRoot);
        }

        var newAccumulatorState = currentAccumulatorState + 1;
        var newBoardState = currentBoardState + 1;

        var pvIndex = *(PVTable.Indexes + depthFromRoot);
        var nextPvIndex = *(PVTable.Indexes + depthFromRoot + 1);
        _pVTable[pvIndex] = 0;

        var pvNode = beta - alpha > 1;
        var parentInCheck = currentBoardState->InCheck;
        var pHash = currentBoardState->Hash;

        var canPrune = false;
        uint transpositionBestMove = default;
        TranspositionTableFlag transpositionType = default;
        var transpositionEvaluation = TranspositionTableExtensions.NoHashEntry;
        ref var ttEntry = ref *(Transpositions + (pHash & TtMask));

        if (depthFromRoot > 0)
        {
            if (_searchCancelled)
            {
                // Search was cancelled, return
                return 0;
            }

            var mateScore = Constants.ImmediateMateScore - depthFromRoot;
            alpha = Math.Max(alpha, -mateScore);
            beta = Math.Min(beta, mateScore);
            if (alpha >= beta)
            {
                // Faster mating sequence found
                return alpha;
            }

            if (currentBoardState->HalfMoveClock >= 100 ||
                currentBoardState->InsufficientMatingMaterial() ||
                RepetitionDetector.IsThreefoldRepetition(currentBoardState->TurnCount, currentBoardState->HalfMoveClock, HashHistory))
            {
                // Detect draw by Fifty move counter or repetition
                return 0;
            }

            if (alpha < 0 && currentBoardState->HasRepetition(HashHistory, depthFromRoot))
            {
                alpha = 0;
                if (alpha >= beta)
                    return alpha;
            }

            if (pHash == ttEntry.FullHash)
            {
                transpositionBestMove = ttEntry.Move;
                transpositionType = ttEntry.Flag;
                if (ttEntry.Depth >= depth)
                {
                    var score = TranspositionTableExtensions.RecalculateMateScores(ttEntry.Evaluation, depthFromRoot);

                    transpositionEvaluation = ttEntry.Flag switch
                    {
                        TranspositionTableFlag.Exact => score,
                        TranspositionTableFlag.Alpha when score <= alpha => alpha,
                        TranspositionTableFlag.Beta when score >= beta => beta,
                        _ => TranspositionTableExtensions.NoHashEntry
                    };

                    if (!pvNode && transpositionEvaluation != TranspositionTableExtensions.NoHashEntry)
                    {
                        // Transposition table hit
                        return transpositionEvaluation;
                    }
                }
            }
        }

        var pawnChIndex = CorrectionIndex(currentBoardState->PawnHash, currentBoardState->WhiteToMove);
        var whiteMaterialChIndex = CorrectionIndex(currentBoardState->WhiteMaterialHash, currentBoardState->WhiteToMove);
        var blackMaterialChIndex = CorrectionIndex(currentBoardState->BlackMaterialHash, currentBoardState->WhiteToMove);

        var staticEval = AdjustEval(pawnChIndex, whiteMaterialChIndex, blackMaterialChIndex,
            Evaluate(currentBoardState, currentAccumulatorState, depthFromRoot));

        if (depthFromRoot > 0)
        {
            if (parentInCheck)
            {
                // Extend searches when in check
                depth++;
            }
            else if (!pvNode)
            {
                // Reverse futility pruning
                var margin = depth * SpsaOptions.ReverseFutilityPruningMargin;
                if (depth <= SpsaOptions.ReverseFutilityPruningDepth && staticEval >= beta + margin)
                {
                    return staticEval - margin;
                }

                // Null move pruning
                if (staticEval >= beta &&
                    currentAccumulatorState->Move != default &&
                    (transpositionType != TranspositionTableFlag.Alpha || transpositionEvaluation >= beta) &&
                    depth > SpsaOptions.NullMovePruningDepth && currentBoardState->HasMajorPieces())
                {
                    var reduction = *(SpsaOptions.NullMovePruningReductionTable + depth);

                    Unsafe.CopyBlock(newBoardState, currentBoardState, BoardStateData.BoardStateSize);

                    newBoardState->ApplyNullMove();

                    newAccumulatorState->UpdateTo(newBoardState);
                    newAccumulatorState->Move = default;

                    var nullMoveScore = -NegaMaxSearch(newBoardState, newAccumulatorState, depthFromRoot + 1,
                        Math.Max(depth - reduction - 1, 0), -beta,
                        -beta + 1);

                    if (nullMoveScore >= beta)
                    {
                        // Beta cutoff
                        return beta;
                    }
                }

                // Razoring
                if (depth is > 0 and <= 3)
                {
                    var score = staticEval + SpsaOptions.RazorMarginA;
                    if (depth == 1 && score < beta)
                    {
                        NodesVisited--;
                        var qScore = QuiescenceSearch(currentBoardState, currentAccumulatorState, depthFromRoot, alpha, beta);

                        return int.Max(qScore, score);
                    }

                    score = staticEval + SpsaOptions.RazorMarginB;
                    if (score < beta)
                    {
                        NodesVisited--;
                        var qScore = QuiescenceSearch(currentBoardState, currentAccumulatorState, depthFromRoot, alpha, beta);
                        if (qScore < beta)
                        {
                            return int.Max(qScore, score);
                        }
                    }

                    canPrune = true;
                }
            }
        }

        if (depth <= 0)
        {
            // Max depth reached, return evaluation of quiet position
            NodesVisited--;
            return QuiescenceSearch(currentBoardState, currentAccumulatorState, depthFromRoot, alpha, beta);
        }

        if (transpositionType == default && depth > SpsaOptions.InternalIterativeDeepeningDepth)
        {
            // Internal iterative deepening
            depth--;
        }

        var whiteToMove = currentBoardState->WhiteToMove;

        var moves = stackalloc uint[218];

        // Generate pseudo legal moves from this position
        var psuedoMoveCount = currentBoardState->GeneratePseudoLegalMoves(moves, false);

        if (psuedoMoveCount == 0)
        {
            // No available moves, either stalemate or checkmate
            var eval = MoveScoring.EvaluateFinalPosition(depthFromRoot, parentInCheck);

            if (!currentBoardState->InCheck)
            {
                var diff = eval - staticEval;
                UpdateCorrectionHistory(pawnChIndex, whiteMaterialChIndex, blackMaterialChIndex, diff, depth);
            }

            ttEntry.Set(pHash, (byte)depth, depthFromRoot, eval,
                TranspositionTableFlag.Exact);

            return eval;
        }

        // Best move seen so far used in move ordering.
        var moveOrderingBestMove = depthFromRoot == 0
            ? BestSoFar
            : transpositionBestMove;

        // Get counter move
        var counterMoveIndex = currentAccumulatorState->Move.GetCounterMoveIndex();
        var counterMove = counterMoveIndex == default ? default : *(Counters + counterMoveIndex);

        // Get killer move
        var killerA = *(killers + (depthFromRoot << 1));

        // Data used in move ordering
        var scores = stackalloc int[psuedoMoveCount];

        var captures = stackalloc short[currentBoardState->PieceCount];

        var occupancyBitBoards = stackalloc ulong[8]
        {
            currentBoardState->Occupancy[Constants.WhitePieces],
            currentBoardState->Occupancy[Constants.BlackPieces],
            currentBoardState->Occupancy[Constants.BlackPawn] | currentBoardState->Occupancy[Constants.WhitePawn],
            currentBoardState->Occupancy[Constants.BlackKnight] | currentBoardState->Occupancy[Constants.WhiteKnight],
            currentBoardState->Occupancy[Constants.BlackBishop] | currentBoardState->Occupancy[Constants.WhiteBishop],
            currentBoardState->Occupancy[Constants.BlackRook] | currentBoardState->Occupancy[Constants.WhiteRook],
            currentBoardState->Occupancy[Constants.BlackQueen] | currentBoardState->Occupancy[Constants.WhiteQueen],
            currentBoardState->Occupancy[Constants.BlackKing] | currentBoardState->Occupancy[Constants.WhiteKing]
        };

        for (var i = 0; i < psuedoMoveCount; i++)
        {
            var move = *(moves + i);
            if (moveOrderingBestMove == move)
            {
                *(scores + i) = SpsaOptions.MoveOrderingBestMoveBias;
                continue;
            }

            if (move.IsCapture())
            {
                if (move.IsEnPassant())
                {
                    *(scores + i) = SpsaOptions.MoveOrderingEnPassantMoveBias;
                    continue;
                }

                var captureDelta = currentBoardState->StaticExchangeEvaluation(occupancyBitBoards, captures, move);

                *(scores + i) = (captureDelta >= 0 ? SpsaOptions.MoveOrderingWinningCaptureBias : SpsaOptions.MoveOrderingLosingCaptureBias) +
                                captureDelta + (move.IsPromotion() ? SpsaOptions.MoveOrderingCapturePromoteBias : 0);
                continue;
            }

            if (move.IsPromotion())
            {
                *(scores + i) = SpsaOptions.MoveOrderingPromoteBias;
                continue;
            }

            if (killerA == move)
            {
                *(scores + i) = SpsaOptions.MoveOrderingKillerABias;
                continue;
            }

            if (counterMove == move)
            {
                *(scores + i) = SpsaOptions.MoveOrderingCounterMoveBias;
                continue;
            }

            *(scores + i) = *(History + move.GetCounterMoveIndex());
        }

        var nextHashHistoryEntry = HashHistory + currentBoardState->TurnCount;
        var probCutSortedUpTo = 0;

        // Probcut
        int probBeta = beta + SpsaOptions.ProbCutBetaMargin;
        if (!pvNode && !parentInCheck
            && currentAccumulatorState->Move != default
            && depth >= SpsaOptions.ProbCutMinDepth
            && Math.Abs(beta) < TranspositionTableExtensions.PositiveCheckmateDetectionLimit
            && (pHash != ttEntry.FullHash || ttEntry.Depth < depth - SpsaOptions.ProbCutMinDepth || (ttEntry.Evaluation != TranspositionTableExtensions.NoHashEntry && ttEntry.Evaluation >= probBeta)))
        {

            for (; probCutSortedUpTo < psuedoMoveCount; ++probCutSortedUpTo)
            {
                var currentScorePtr = scores + probCutSortedUpTo;
                var currentMovePtr = moves + probCutSortedUpTo;

                // Incremental move sorting
                for (var j = probCutSortedUpTo + 1; j < psuedoMoveCount; j++)
                {
                    // Pointer to the score and move being compared
                    var compareScorePtr = scores + j;
                    var compareMovePtr = moves + j;

                    if (*compareScorePtr > *currentScorePtr)
                    {
                        // Swap the scores and moves directly using pointers
                        // Using temporary variables for clarity
                        int tempScore = *currentScorePtr;
                        uint tempMove = *currentMovePtr;

                        *currentScorePtr = *compareScorePtr;
                        *currentMovePtr = *compareMovePtr;

                        *compareScorePtr = tempScore;
                        *compareMovePtr = tempMove;
                    }
                }

                if (*currentScorePtr < SpsaOptions.MoveOrderingPromoteBias)
                {
                    break;
                }

                var m = *currentMovePtr;
                if (m.IsQuiet())
                {
                    continue;
                }

                Unsafe.CopyBlock(newBoardState, currentBoardState, BoardStateData.BoardStateSize);

                if (whiteToMove ? !newBoardState->PartialApplyWhite(m) : !newBoardState->PartialApplyBlack(m))
                {
                    *currentMovePtr = default;
                    // Illegal move, undo
                    continue;
                }

                newBoardState->UpdateCheckStatus();
                newAccumulatorState->UpdateToParent(currentAccumulatorState, newBoardState);
                if (whiteToMove)
                {
                    newBoardState->FinishApplyWhite(ref *newAccumulatorState, m, currentBoardState->EnPassantFile, currentBoardState->CastleRights);
                }
                else
                {
                    newBoardState->FinishApplyBlack(ref *newAccumulatorState, m, currentBoardState->EnPassantFile, currentBoardState->CastleRights);
                }
                *nextHashHistoryEntry = newBoardState->Hash;

                Sse.Prefetch0(Transpositions + (newBoardState->Hash & TtMask));

                NodesVisited--;
                var score = -QuiescenceSearch(newBoardState, newAccumulatorState, depthFromRoot + 1, -probBeta, -probBeta + 1);

                if (score >= probBeta)
                {
                    score = -NegaMaxSearch(newBoardState, newAccumulatorState, depthFromRoot + 1, depth - SpsaOptions.ProbCutMinDepth,
                        -probBeta, -probBeta + 1);

                }

                if (score >= probBeta)
                {
                    ttEntry.Set(pHash, (byte)(depth - SpsaOptions.ProbCutMinDepth), depthFromRoot,
                        score,
                        TranspositionTableFlag.Beta,
                        default);

                    return score;
                }
            }
        }

        var lmpDepth = depth * depth + SpsaOptions.LateMovePruningConstant;
        var searchedMoves = 0;

        uint bestMove = default;
        var evaluationBound = TranspositionTableFlag.Alpha;

        var bestScore = int.MinValue;
        // Evaluate each move
        for (var moveIndex = 0; moveIndex < psuedoMoveCount; ++moveIndex)
        {
            var currentScorePtr = scores + moveIndex;
            var currentMovePtr = moves + moveIndex;

            if (moveIndex >= probCutSortedUpTo)
            {
                // Incremental move sorting
                for (var j = moveIndex + 1; j < psuedoMoveCount; j++)
                {
                    // Pointer to the score and move being compared
                    var compareScorePtr = scores + j;
                    var compareMovePtr = moves + j;

                    if (*compareScorePtr > *currentScorePtr)
                    {
                        // Swap the scores and moves directly using pointers
                        // Using temporary variables for clarity
                        int tempScore = *currentScorePtr;
                        uint tempMove = *currentMovePtr;

                        *currentScorePtr = *compareScorePtr;
                        *currentMovePtr = *compareMovePtr;

                        *compareScorePtr = tempScore;
                        *compareMovePtr = tempMove;
                    }
                }
            }

            var m = *currentMovePtr;

            if (m == default)
            {
                continue;
            }

            Unsafe.CopyBlock(newBoardState, currentBoardState, BoardStateData.BoardStateSize);

            if (whiteToMove ? !newBoardState->PartialApplyWhite(m) : !newBoardState->PartialApplyBlack(m))
            {
                *currentMovePtr = default;

                // Illegal move, undo
                continue;
            }

            newBoardState->UpdateCheckStatus();

            var isPromotionThreat = m.IsPromotionThreat();
            var isInteresting = parentInCheck || newBoardState->InCheck || isPromotionThreat ||
                                (*currentScorePtr) > SpsaOptions.InterestingNegaMaxMoveScore;

            if (canPrune &&
                !isInteresting &&
                searchedMoves > lmpDepth)
            {
                // Late move pruning
                continue;
            }

            newAccumulatorState->UpdateToParent(currentAccumulatorState, newBoardState);
            if (whiteToMove)
            {
                newBoardState->FinishApplyWhite(ref *newAccumulatorState, m, currentBoardState->EnPassantFile, currentBoardState->CastleRights);
            }
            else
            {
                newBoardState->FinishApplyBlack(ref *newAccumulatorState, m, currentBoardState->EnPassantFile, currentBoardState->CastleRights);
            }
            *nextHashHistoryEntry = newBoardState->Hash;

            Sse.Prefetch0(Transpositions + (newBoardState->Hash & TtMask));

            var needsFullSearch = true;
            var score = 0;

            if (searchedMoves > 0)
            {
                if (depth >= SpsaOptions.LateMoveReductionMinDepth && searchedMoves >= SpsaOptions.LateMoveReductionMinMoves)
                {
                    // LMR: Move ordering should ensure a better move has already been found by now so do a shallow search
                    var reduction = isInteresting
                        ? *(SpsaOptions.LateMovePruningInterestingReductionTable + depth * 218 + searchedMoves)
                        : *(SpsaOptions.LateMovePruningReductionTable + depth * 218 + searchedMoves);

                    if (reduction > 0)
                    {
                        score = -NegaMaxSearch(newBoardState, newAccumulatorState, depthFromRoot + 1, depth - reduction - 1,
                            -alpha - 1, -alpha);
                        needsFullSearch = score > alpha;
                    }
                }

                if (needsFullSearch)
                {
                    // PVS
                    score = -NegaMaxSearch(newBoardState, newAccumulatorState, depthFromRoot + 1, depth - 1, -alpha - 1, -alpha);
                    needsFullSearch = score > alpha && score < beta;
                }
            }

            if (needsFullSearch)
            {
                // Full search
                score = -NegaMaxSearch(newBoardState, newAccumulatorState, depthFromRoot + 1, depth - 1, -beta, -alpha);
            }

            // Revert the move

            searchedMoves++;

            if (_searchCancelled && depthFromRoot > 0)
            {
                // Search was cancelled
                break;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = m;
            }

            if (score <= alpha)
            {
                // Move didn't cause an alpha cut off, continue searching
                continue;
            }

            // Update best move seen so far
            alpha = score;
            evaluationBound = TranspositionTableFlag.Exact;

            if (score >= beta)
            {
                if (!parentInCheck && (bestMove == default || bestMove.IsQuiet())
                                   && (score > staticEval))
                {
                    var diff = bestScore - staticEval;
                    UpdateCorrectionHistory(pawnChIndex, whiteMaterialChIndex, blackMaterialChIndex, diff, depth);
                }

                // Cache in transposition table
                ttEntry.Set(pHash, (byte)depth, depthFromRoot,
                    score,
                    TranspositionTableFlag.Beta,
                    bestMove);

                if (m.IsQuiet())
                {
                    // Update move ordering heuristics for quiet moves that lead to a beta cut off

                    // History
                    HistoryHeuristicExtensions.UpdateMovesHistory(History, moves, moveIndex, m, depth);

                    *(killers + (depthFromRoot << 1)) = m;


                    if (counterMoveIndex != 0)
                    {
                        *(Counters + counterMoveIndex) = m;
                    }
                }

                return score;
            }

            if (!_searchCancelled || depthFromRoot > 0)
            {
                // update pv table
                *(_pVTable + pvIndex) = m;
                ShiftPvMoves(pvIndex + 1, nextPvIndex, Constants.MaxSearchDepth - depthFromRoot - 1);
            }
        }

        if (_searchCancelled && depthFromRoot > 0)
        {
            // Search was cancelled
            return 0;
        }

        if (searchedMoves == 0)
        {
            // No available moves, either stalemate or checkmate
            var eval = MoveScoring.EvaluateFinalPosition(depthFromRoot, parentInCheck);

            if (!parentInCheck)
            {
                var diff = eval - staticEval;
                UpdateCorrectionHistory(pawnChIndex, whiteMaterialChIndex, blackMaterialChIndex, diff, depth);
            }

            ttEntry.Set(pHash, (byte)depth, depthFromRoot, eval,
                TranspositionTableFlag.Exact);

            return eval;
        }


        if (!parentInCheck
                            && (bestMove == default || bestMove.IsQuiet())
                            && (evaluationBound == TranspositionTableFlag.Exact || bestScore < staticEval))
        {
            var diff = bestScore - staticEval;
            UpdateCorrectionHistory(pawnChIndex, whiteMaterialChIndex, blackMaterialChIndex, diff, depth);
        }

        // Cache in transposition table
        ttEntry.Set(pHash, (byte)depth, depthFromRoot, alpha,
            evaluationBound, bestMove);

        return alpha;
    }
}