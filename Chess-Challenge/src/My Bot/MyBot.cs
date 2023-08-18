using System;
using ChessChallenge.API;
using System.Collections.Generic;
using static System.Numerics.BitOperations;
using static ChessChallenge.API.PieceType;
using static System.Math;

public class MyBot : IChessBot
{

    public static int[] PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 100 };

    public Dictionary<ulong, (int eval, Move move, int depth, ulong next)> Table = new();
    //public Dictionary<(ulong, Move), int> Memory = new();
    //public Dictionary<ulong, string> HashToFen = new();
    //public Dictionary<ulong, string> HashToRep = new();

    //System.Random r = new Random();

    public int[] Memory = new int[262144];

    public int PositionsSearched = 0;
    public Board Board;
    public int Color => Board.IsWhiteToMove ? 1 : -1;
    public Timer Timer;

    public Move Think(Board board, Timer timer)
    {
        Board = board;
        this.Timer = timer;

        //PositionsSearched = 0;

        Move move = default;
        int maxDepth = timer.MillisecondsRemaining > 10000 ? 4 : 2;
        for (int depth = 2; depth <= 4; depth += 2)
        {
            Table.Clear();
            NegaMax(-1000000, 1000000, depth, true, out move);
        }

        //logIt = true;
        Console.WriteLine("Eval: " + Eval(0) + " : " + PositionsSearched);
        //logIt = false;

        //
        //var hash = board.ZobristKey;
        //while (true)
        //{
        //    if (Table.TryGetValue(hash, out var toDo))
        //    {
        //        if (hash == toDo.next) break;
        //        hash = toDo.next;
        //        if (HashToFen.TryGetValue(hash, out var fen))
        //        {
        //            Console.WriteLine(fen);
        //            Console.WriteLine(hash);
        //        }
        //        else
        //        {
        //            Console.WriteLine("A");
        //            break;
        //        }
        //    }
        //    else
        //    {
        //        Console.WriteLine("B");
        //        break;
        //    }
        //}

        //Console.WriteLine("Eval: " + eval + " : " + Eval(0) + " : " + PositionsSearched);

        return move;

        //hash = (long)board.ZobristKey;
        //while (true) {
        //    if (Table.TryGetValue(hash, out var toDo)) {
        //        if (hash == toDo.Item3) break;
        //        hash = toDo.Item3;
        //        if (HashToFen.TryGetValue(hash, out var fen)) {
        //            Console.WriteLine(fen);
        //            Console.WriteLine(hash);
        //        } else {
        //            Console.WriteLine("A");
        //            break;
        //        }
        //    } else {
        //        Console.WriteLine("B");
        //        break;
        //    }
        //}

        //Console.WriteLine(HashToFen[result.hash]);

        //var pawns = board.GetPieceBitboard(Pawn, false);
        // BitboardHelper.VisualizeBitboard(((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7));

        //return result.move;
    }

    //public string BuildCanonicalId(Board board) {
    //    string id = board.IsWhiteToMove ? "w" : "b";
    //    for (int x = 0; x < 8; x++) {
    //        for (int y = 0; y < 8; y++) {
    //            var piece = board.GetPiece(new Square(x, y));
    //            char c = "-pkbrqk"[(int)piece.PieceType];
    //            if (piece.IsWhite) {
    //                c = char.ToUpper(c);
    //            }
    //            id += c;
    //        }
    //        id += "\n";
    //    }
    //    return id;
    //}

    //public Stack<Move> debugMoves = new Stack<Move>();

    public int NegaMax(int alpha, int beta, int depth, bool root, out Move bestMove)
    {
        ulong hash = Board.ZobristKey;

        //if (HashToRep.TryGetValue(hash, out var rep)) {
        //    var rep2 = BuildCanonicalId(board);
        //    if (rep != rep2) {
        //        throw new InvalidOperationException(rep + "         " + rep2);
        //    }
        //} else {
        //    HashToRep[hash] = BuildCanonicalId(board);
        //}

        //if (!HashToFen.ContainsKey(hash))
        //{
        //    HashToFen[hash] = Board.GetFenString();
        //}

        if (Table.TryGetValue(hash, out var value))
        {
            //Only use the transposition table if the depth is shallower, else deeper depths
            //are considered less reliable
            if (value.depth >= depth)
            {
                bestMove = value.move;
                return value.eval * Color;
            }
        }

        PositionsSearched++;
        //if ((PositionsSearched % (1024 * 256)) == 0)
        //{
        //    Console.WriteLine(PositionsSearched + " : " + depth);
        //}

        int eval = Eval(depth) * Color;
        bestMove = default;
        ulong chosenHash = 0;

        Span<Move> moves = stackalloc Move[128];
        Board.GetLegalMovesNonAlloc(ref moves, depth <= 0 && !Board.IsInCheck());

        if (root && moves.Length == 1)
        {
            bestMove = moves[0];
            return 0;
        }

        //if (depth <= -12)
        //{
        //Console.WriteLine(string.Join(",  ", debugMoves));
        //}

        if (moves.Length > 0)
        {
            var sortKeys = stackalloc int[128].Slice(0, moves.Length);
            for (int i = 0; i < moves.Length; i++)
            {
                Board.MakeMove(moves[i]);
                //sortKeys[i] = Memory[Board.ZobristKey & 262143] * Color;
                //if (sortKeys[i] == 0)
                //{
                //    sortKeys[i] = Eval(depth);
                //}
                //sortKeys[i] = r.Next();

                if (moves[i].IsCapture)
                {
                    sortKeys[i] = PieceValue[(int)moves[i].MovePieceType] - PieceValue[(int)moves[i].MovePieceType];
                }
                else
                {
                    sortKeys[i] = 100 - (int)moves[i].MovePieceType;
                }

                Board.UndoMove(moves[i]);
            }

            sortKeys.Sort(moves);

            bool isInCheck = Board.IsInCheck();

            //List<int> evals2 = new List<int>();
            //for (int i = 0; i < sortKeys.Length; i++) {
            //    evals2.Add(sortKeys[i]*-1);
            //}
            //
            //List<int> evals = new List<int>();
            //List<string> names = new List<string>();

            for (int i = 0; i < moves.Length; i++)
            {
                var move = moves[i];

                Board.MakeMove(move);
                //debugMoves.Push(move);

                //File.AppendAllText("debug.txt", "{" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + "\n");

                int subEval;

                if (Board.IsRepeatedPosition() && eval > 0)
                {
                    //Never consider a repeated position if we are winning
                    subEval = 0;
                }
                else if (depth <= 0 && move.IsCapture && !isInCheck &&
                         (PieceValue[(int)move.MovePieceType] > PieceValue[(int)move.CapturePieceType] ||
                          (PieceValue[(int)move.CapturePieceType] + 150) < -alpha))
                {
                    //We want to prune captures past the horizon as aggressively as possible (unless we are in check)
                    //Ignore captures where a high-value piece captures a lower value piece
                    //Ignores captures of pieces whoes value is smaller than the amount we are losing by
                    subEval = eval;
                }
                else
                {
                    subEval = -NegaMax(-beta, -alpha, depth - 1, false, out _);
                }

                //string moveText;
                //if (moves[i].IsCapture)
                //{
                //    moveText = " PNBRQK"[(int)moves[i].MovePieceType] + "x" + " PNBRQK"[(int)moves[i].CapturePieceType];
                //}
                //else
                //{
                //    moveText = moves[i].ToString();
                //}
                //File.AppendAllText("debug.txt", depth.ToString().PadLeft(3, '|') + "".PadLeft(4 - depth) + moveText + "\n");

                //evals.Add(subEval);
                //names.Add(moves[i].ToString());

                if (subEval > eval || i == 0)
                {
                    eval = subEval;
                    bestMove = move;
                    chosenHash = Board.ZobristKey;
                }

                Board.UndoMove(move);
                //debugMoves.Pop();

                alpha = Max(alpha, eval);
                if (alpha >= beta)
                {
                    break;
                }

                //if (Timer.MillisecondsElapsedThisTurn > 1000 && root)
                //{
                //    break;
                //}

                //if (Timer.MillisecondsElapsedThisTurn > 1000 && root)
                //{
                //    break;
                //}

                //File.AppendAllText("debug.txt", "}" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + " : " + subEval.ToString() + "\n");
            }

            //if (evals2.Count(r => r != 0) > 10)
            //{
            //    Console.WriteLine();
            //    Console.WriteLine((Board.IsWhiteToMove ? "white" : "black") + " :   " + Board.GetFenString());
            //    Console.WriteLine(string.Join(", ", evals2));
            //    Console.WriteLine(string.Join(", ", evals));
            //    Console.WriteLine(string.Join(", ", names));
            //}
        }

        Memory[hash & 262143] = eval * Color;
        Table[hash] = (eval * Color, bestMove, depth, chosenHash);

        //if (root && bestMove.IsNull)
        //{
        //    throw new Exception();
        //}

        return eval;
    }

    public int Eval(int depth)
    {
        if (Board.IsRepeatedPosition() || Board.IsInStalemate())
        {
            return 0;
        }

        if (Board.IsInCheckmate())
        {
            return (100000 + depth) * (Board.IsWhiteToMove ? -1 : 1);
        }
        else
        {
            var result = Eval(true) - Eval(false);

            //var king = Board.GetKingSquare(true);
            //var enemyKing = Board.GetKingSquare(false);
            //
            //result += Sign(result) * (8 - Max(Abs(enemyKing.Rank - king.Rank), Abs(enemyKing.File - king.File))) * 10;

            return result;
        }
    }

    bool logIt = false;

    public int Eval(bool isWhite)
    {
        var piece = isWhite ? Board.WhitePiecesBitboard : Board.BlackPiecesBitboard;
        var pawns = Board.GetPieceBitboard(Pawn, isWhite);
        var bisho = Board.GetPieceBitboard(Bishop, isWhite);
        var knigh = Board.GetPieceBitboard(Knight, isWhite);
        var rooks = Board.GetPieceBitboard(Rook, isWhite);
        var queen = Board.GetPieceBitboard(Queen, isWhite);
        var kings = Board.GetPieceBitboard(King, isWhite);

        var king = Board.GetKingSquare(isWhite);
        var enemyKing = Board.GetKingSquare(!isWhite);

        int eval = 0;

        int pawnsPop = PopCount(piece);
        int minorPop = PopCount(bisho | knigh);
        int rooksPop = PopCount(rooks);

        int kingSafety = king.File <= 2 || king.File >= 6 ? 50 : 0 +
                         king.Rank == (isWhite ? 0 : 7) ? 0 : -120;

        int development = PopCount((bisho | knigh) & 0x007E7E7E7E7E7E00);

        int kingProtected = ((kings >> 8 | kings << 8) & piece) != 0 ? 1 : 0;

        int knightsActive = PopCount(knigh & 0x00003C3C3C3C0000);


        //if (logIt)
        //{
        //    BitboardHelper.VisualizeBitboard(totalSliderMovers);
        //    Console.WriteLine(sliderMoves);
        //}

        if (minorPop >= 2 && rooksPop > 0 && pawnsPop >= 4)
        {
            //Early/Mid game
            eval += kingSafety;
            eval += development * 15;
            eval += kingProtected * 50;
            eval += knightsActive * 20;
        }
        else
        {
            //Prefer putting enemy king on edge of board
            int enemyKingEdgeDist = Min(enemyKing.Rank, 7 - enemyKing.Rank) + Min(enemyKing.File, 7 - enemyKing.File);
            eval -= enemyKingEdgeDist;

            //Prefer having pawns close to queening
            if (isWhite)
            {
                eval -= LeadingZeroCount(pawns);
            }
            else
            {
                eval -= TrailingZeroCount(pawns);
            }
        }

        ulong pawnAttacks;
        if (isWhite)
        {
            pawnAttacks = ((pawns & 0x7F7F7F7F7F7F7F7F) << 9) | ((pawns & 0xFEFEFEFEFEFEFEFE) << 7);
        }
        else
        {
            pawnAttacks = ((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7);
        }

        int sliderMoves = 0;
        ulong sliders = bisho | rooks | queen;
        while (sliders != 0)
        {
            int sliderI = BitboardHelper.ClearAndGetIndexOfLSB(ref sliders);
            var square = new Square(sliderI % 8, sliderI / 8);
            ulong mooo = BitboardHelper.GetSliderAttacks(Board.GetPiece(square).PieceType, square, Board);
            sliderMoves += PopCount(mooo);
        }

        eval += sliderMoves * 4;

        //Pieces supported by pawns
        eval += PopCount(pawnAttacks & piece) * 5;

        //Piece counts
        eval += pawnsPop * 100 +
                minorPop * 300 +
                rooksPop * 500 +
                PopCount(queen) * 1100;

        return eval;
    }
}