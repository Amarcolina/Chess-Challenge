using System;
using System.IO;
using System.Linq;
using ChessChallenge.API;
using System.Collections.Generic;
using static System.Numerics.BitOperations;
using static ChessChallenge.API.PieceType;
using static System.Math;

public class MyBot : IChessBot {

    public static int[] PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 100 };

    //System.Random r = new Random();

    public int[] Memory = new int[262144];

    Transposition[] TranspositionTable = new Transposition[262144];

    public int PositionsSearched = 0;
    public Board MyBoard;
    public int Color => MyBoard.IsWhiteToMove ? 1 : -1;

    public Move BestMove;

    Timer MyTimer;
    int MaxAllowedTime;

    int ExactHits;
    int SoftHits;

    record struct Transposition(ulong Hash, int Eval, Move Killer, int Depth, int Flag);

    public Move Think(Board board, Timer timer) {
        MyBoard = board;
        MyTimer = timer;

        BestMove = default;
        int depth = 1;
        MaxAllowedTime = MyTimer.MillisecondsRemaining / 45;

        ExactHits = 0;
        PositionsSearched = 0;
        SoftHits = 0;

        int eval = 0;

        File.Delete("debug.txt");

        Move toUse = default;
        do {
            //File.AppendAllText("debug.txt", "\n\n\n" + depth + "\n\n\n");

            //Console.WriteLine("  Eval at depth " + depth);
            toUse = BestMove;
            eval = NegaMax(-1000000, 1000000, depth++, true);
        } while (MyTimer.MillisecondsElapsedThisTurn < MaxAllowedTime && depth < 16);

        //NegaMax(-1000000, 1000000, 2, true);

        //Console.WriteLine("Curr Eval: " + Eval(0) + "       Est. Eval: " + eval);
        //Console.WriteLine(PositionsSearched + " : " + SoftHits + " : " + ExactHits);

        return toUse.IsNull ? (BestMove.IsNull ? MyBoard.GetLegalMoves()[0] : BestMove) : toUse;
    }

    public Stack<Move> debugMoves = new Stack<Move>();

    public int NegaMax(int alpha, int beta, int depth, bool root) {
        ulong hash = MyBoard.ZobristKey;
        ulong hashIndex = hash & 262143;
        var alphaBeforeSearch = alpha;

        //Only use the transposition table if the depth is shallower, else deeper depths
        //are considered less reliable
        var ttEntry = TranspositionTable[hashIndex];
        if (ttEntry.Hash == hash && ttEntry.Depth >= depth && (
            ttEntry.Flag == 1 ||
            (ttEntry.Flag == 0 && ttEntry.Eval <= alpha) ||
            (ttEntry.Flag == 2 && ttEntry.Eval >= beta))) {
            ExactHits++;
            return ttEntry.Eval;
        }

        if (ttEntry.Hash == hash) SoftHits++;

        //PositionsSearched++;
        //if ((PositionsSearched % (1024 * 256)) == 0) {
        //    Console.WriteLine(PositionsSearched + " : " + depth);
        //}

        int bestFound = -987654321;

        int staticEval = Eval(depth) * Color;

        Span<Move> moves = stackalloc Move[128];
        MyBoard.GetLegalMovesNonAlloc(ref moves, depth <= 0 && !MyBoard.IsInCheck());

        if (root && moves.Length == 1) {
            BestMove = moves[0];
            return 0;
        }

        if (depth <= 0) {
            bestFound = staticEval;
            if (bestFound >= beta) return bestFound;
            alpha = Max(alpha, bestFound);
        }

        Move bestMove = default;

        if (moves.Length > 0) {
            var sortKeys = stackalloc int[128].Slice(0, moves.Length);
            for (int i = 0; i < moves.Length; i++) {
                if (moves[i] == ttEntry.Killer) {
                    sortKeys[i] = int.MinValue;
                } else if (moves[i].IsCapture) {
                    sortKeys[i] = (int)moves[i].MovePieceType - 100 * (int)moves[i].CapturePieceType;
                } else {
                    sortKeys[i] = 100 - (int)moves[i].MovePieceType;
                }
            }

            sortKeys.Sort(moves);

            bool isInCheck = MyBoard.IsInCheck();

            for (int i = 0; i < moves.Length; i++) {
                var move = moves[i];

                MyBoard.MakeMove(move);
                debugMoves.Push(move);

                //File.AppendAllText("debug.txt", "{" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + "\n");

                int subEval;

                if (MyBoard.IsRepeatedPosition() && staticEval > 0) {
                    //Never consider a repeated position if we are winning
                    subEval = 0;
                } else if (depth <= 0 && move.IsCapture && !isInCheck &&
                           (PieceValue[(int)move.MovePieceType] > PieceValue[(int)move.CapturePieceType] ||
                            (PieceValue[(int)move.CapturePieceType] + 150) < -alpha)) {
                    //We want to prune captures past the horizon as aggressively as possible (unless we are in check)
                    //Ignore captures where a high-value piece captures a lower value piece
                    //Ignores captures of pieces whoes value is smaller than the amount we are losing by
                    subEval = staticEval;
                } else {
                    subEval = -NegaMax(-beta, -alpha, depth - 1, false);
                }

                //File.AppendAllText("debug.txt", string.Join(" - ", debugMoves.Reverse().Select(f => f.ToString().Replace("Move: ", "").Replace("'", ""))) + " : " + subEval + "\n");

                MyBoard.UndoMove(move);
                debugMoves.Pop();

                //File.AppendAllText("debug.txt", "}" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + " : " + subEval.ToString() + "\n");

                if (subEval > bestFound) {
                    bestFound = subEval;
                    bestMove = move;
                    if (root) {
                        BestMove = move;
                    }

                    alpha = Max(alpha, bestFound);
                    if (alpha >= beta) break;
                }

                if (MyTimer.MillisecondsElapsedThisTurn > MaxAllowedTime) return 6661666;
            }
        }

        Memory[hash & 262143] = staticEval * Color;

        TranspositionTable[hashIndex] = new Transposition(hash, bestFound, bestMove, depth, bestFound >= beta ? 2 : bestFound > alphaBeforeSearch ? 1 : 0);

        return bestFound;
    }

    public int Eval(int depth) {
        if (MyBoard.IsRepeatedPosition() || MyBoard.IsInStalemate()) {
            return 0;
        }

        if (MyBoard.IsInCheckmate()) {
            return (100000 + depth) * (MyBoard.IsWhiteToMove ? -1 : 1);
        } else {
            var result = Eval(true) - Eval(false);

            //var king = Board.GetKingSquare(true);
            //var enemyKing = Board.GetKingSquare(false);
            //
            //result += Sign(result) * (8 - Max(Abs(enemyKing.Rank - king.Rank), Abs(enemyKing.File - king.File))) * 10;

            return result;
        }
    }

    public int Eval(bool isWhite) {
        var piece = isWhite ? MyBoard.WhitePiecesBitboard : MyBoard.BlackPiecesBitboard;
        var pawns = MyBoard.GetPieceBitboard(Pawn, isWhite);
        var bisho = MyBoard.GetPieceBitboard(Bishop, isWhite);
        var knigh = MyBoard.GetPieceBitboard(Knight, isWhite);
        var rooks = MyBoard.GetPieceBitboard(Rook, isWhite);
        var queen = MyBoard.GetPieceBitboard(Queen, isWhite);
        var kings = MyBoard.GetPieceBitboard(King, isWhite);

        var king = MyBoard.GetKingSquare(isWhite);
        var enemyKing = MyBoard.GetKingSquare(!isWhite);

        int eval = 0;

        int pawnsPop = PopCount(piece);
        int minorPop = PopCount(bisho | knigh);
        int rooksPop = PopCount(rooks);

        int kingSafety = (king.File <= 2 || king.File >= 6) ? 50 : 0 +
                         king.Rank == (isWhite ? 0 : 7) ? 0 : -120;

        int development = PopCount((bisho | knigh) & 0x007E7E7E7E7E7E00);

        int kingProtected = ((kings >> 8 | kings << 8) & piece) != 0 ? 1 : 0;

        int knightsActive = PopCount(knigh & 0x00003C3C3C3C0000);

        if (minorPop >= 2 && rooksPop > 0 && pawnsPop >= 4) {
            //Early/Mid game
            eval += kingSafety;
            eval += development * 15;
            eval += kingProtected * 40;
            eval += knightsActive * 15;
        } else {
            //Prefer putting enemy king on edge of board
            int enemyKingEdgeDist = Min(enemyKing.Rank, 7 - enemyKing.Rank) + Min(enemyKing.File, 7 - enemyKing.File);
            eval -= enemyKingEdgeDist;

            //Prefer having pawns close to queening
            if (isWhite) {
                eval -= LeadingZeroCount(pawns);
            } else {
                eval -= TrailingZeroCount(pawns);
            }
        }

        ulong pawnAttacks;
        if (isWhite) {
            pawnAttacks = ((pawns & 0x7F7F7F7F7F7F7F7F) << 9) | ((pawns & 0xFEFEFEFEFEFEFEFE) << 7);
        } else {
            pawnAttacks = ((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7);
        }

        int sliderMoves = 0;
        ulong sliders = bisho | rooks | queen;
        while (sliders != 0) {
            int sliderI = BitboardHelper.ClearAndGetIndexOfLSB(ref sliders);
            var square = new Square(sliderI % 8, sliderI / 8);
            ulong mooo = BitboardHelper.GetSliderAttacks(MyBoard.GetPiece(square).PieceType, square, MyBoard);
            sliderMoves += PopCount(mooo);
        }

        eval += sliderMoves * 4;

        eval += PopCount(pawnAttacks & piece) * 3;


        //Piece counts
        eval += pawnsPop * 100 +
                minorPop * 300 +
                rooksPop * 500 +
                PopCount(queen) * 1100;

        return eval;
    }
}