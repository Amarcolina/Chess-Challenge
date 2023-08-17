using ChessChallenge.API;
using System.Numerics;
using System.IO;
using System.Collections.Generic;
using static System.Numerics.BitOperations;
using static ChessChallenge.API.PieceType;
using System;
using System.Diagnostics;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot, IComparer<Move>
    {
        public static PieceType[] Pieces = new PieceType[] { PieceType.Pawn, PieceType.Bishop, PieceType.Knight, PieceType.Rook, PieceType.King, PieceType.Queen };
        public static int[] PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 100 };

        public Dictionary<long, (int, Move, long)> Table = new();
        public Dictionary<long, int> Memory = new();
        public Dictionary<long, string> HashToFen = new();
        public Dictionary<long, string> HashToRep = new();

        Random rng = new();
        public int PositionsSearched = 0;

        private long[,,] PieceHashes;
        private long IsWhiteToMoveHash;

        private long HashInQuestion;
        private int MultInQuestion;
        private bool WhiteInQuestion;

        public EvilBot()
        {
            PieceHashes = new long[14, 8, 8];
            for (int i = 0; i < 6; i++)
            {
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        //0 123456 7 890123
                        PieceHashes[i, x, y] = rng.NextInt64();
                        PieceHashes[i, x, y] = rng.NextInt64();
                    }
                }
            }

            IsWhiteToMoveHash = rng.NextInt64();
        }

        public Move Think(Board board, Timer timer)
        {
            Table.Clear();
            PositionsSearched = 0;

            long hash = board.IsWhiteToMove ? IsWhiteToMoveHash : 0;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var piece = board.GetPiece(new Square(x, y));
                    var index = (int)(piece.PieceType) + (piece.IsWhite ? 0 : 7);
                    hash ^= PieceHashes[index, x, y];
                }
            }

            (int eval, Move move, long hash) result = default;
            for (int depth = 1; depth <= 4; depth++)
            {
                Table.Clear();

                File.Delete("debug.txt");

                Console.WriteLine("Eval at depth: " + depth);
                var result2 = EvalRecursive(timer, board, hash, int.MinValue, int.MaxValue, depth);

                //if (timer.MillisecondsElapsedThisTurn > 3000)
                //{
                //    break;
                //}

                result = result2;
            }


            Console.WriteLine("Eval: " + result.eval + " : " + Eval(board, 0) + " : " + PositionsSearched);


            //Console.WriteLine(HashToFen[result.hash]);

            //var pawns = board.GetPieceBitboard(Pawn, false);
            // BitboardHelper.VisualizeBitboard(((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7));

            return result.move;
        }

        public int Compare(Move x, Move y)
        {
            long hashDeltaX = GetMoveDelta(x, WhiteInQuestion);
            long hashDeltaY = GetMoveDelta(y, WhiteInQuestion);

            Memory.TryGetValue(HashInQuestion ^ hashDeltaX, out var memoryX);
            Memory.TryGetValue(HashInQuestion ^ hashDeltaY, out var memoryY);

            return (memoryY - memoryX) * MultInQuestion;
        }

        public string BuildCanonicalId(Board board)
        {
            string id = board.IsWhiteToMove ? "w" : "b";
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var piece = board.GetPiece(new Square(x, y));
                    char c = " pkbrqk"[(int)piece.PieceType];
                    if (piece.IsWhite)
                    {
                        c = char.ToUpper(c);
                    }
                    id += c;
                }
                id += "\n";
            }
            return id;
        }

        public long HashBoard(Board board)
        {
            return board.GetFenString().GetHashCode();
        }

        public (int eval, Move move, long result) EvalRecursive(Timer timer, Board board, long hash, int alpha, int beta, int depth)
        {
            hash = HashBoard(board);

            //if (!HashToFen.ContainsKey(hash))
            //{
            //    HashToFen[hash] = board.GetFenString();
            //}

            if (Table.TryGetValue(hash, out var value))
            {
                return value;
            }

            PositionsSearched++;
            if ((PositionsSearched % (1024 * 256)) == 0)
            {
                Console.WriteLine(PositionsSearched + " : " + depth);
            }

            int eval = Eval(board, depth);
            bool isWinning = board.IsWhiteToMove ? (eval > 0) : (eval < 0);
            Move bestMove = default;
            long chosenHash = hash;

            Span<Move> moves = stackalloc Move[128];
            board.GetLegalMovesNonAlloc(ref moves, depth <= 0);

            bool shouldRecurse = true;
            if (moves.Length == 0 || depth <= 0)
            {
                shouldRecurse = false;
                chosenHash = hash;

                if (moves.Length > 0)
                {
                    //If the current board is better than the best we've seen, keep searching
                    //if (board.IsWhiteToMove)
                    //{
                    //    if (eval > alpha)
                    //    {
                    //        shouldRecurse = true;
                    //    }
                    //}
                    //else
                    //{
                    //    if (eval < beta)
                    //    {
                    //        shouldRecurse = true;
                    //    }
                    //}
                    shouldRecurse = true;
                }
            }

            if (shouldRecurse)
            {
                HashInQuestion = hash;
                MultInQuestion = board.IsWhiteToMove ? 1 : -1;
                moves.Sort(this);

                bestMove = moves[0];

                for (int i = 0; i < moves.Length; i++)
                {
                    //if (timer.MillisecondsElapsedThisTurn > 3000)
                    //{
                    //    break;
                    //}

                    long hashDelta = GetMoveDelta(moves[i], board.IsWhiteToMove);

                    board.MakeMove(moves[i]);
                    hash ^= hashDelta;

                    //File.AppendAllText("debug.txt", "{" + depth.ToString() + "".PadLeft(4 - depth) + moves[i].ToString() + "\n");

                    //Never consider a repeated position if we are winning
                    if (board.IsRepeatedPosition() && isWinning)
                    {
                        board.UndoMove(moves[i]);
                        continue;
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

                    //If we are past the horizon, don't consider moves where higher-value pieces capture low-value pieces
                    if (depth <= 0 && moves[i].IsCapture)
                    {
                        int srcValue = PieceValue[(int)moves[i].MovePieceType];
                        int dstValue = PieceValue[(int)moves[i].CapturePieceType];
                        if (dstValue < srcValue)
                        {
                            board.UndoMove(moves[i]);
                            continue;
                        }
                    }

                    (var subEval, var subMove, var subHash) = EvalRecursive(timer, board, hash, alpha, beta, depth - 1);

                    //File.AppendAllText("debug.txt", "}" + depth.ToString() + "".PadLeft(4 - depth) + moves[i].ToString() + " : " + subEval.ToString() + "\n");


                    hash ^= hashDelta;
                    board.UndoMove(moves[i]);

                    if (i == 0 && depth > 0)
                    {
                        eval = subEval;
                        bestMove = moves[i];
                        chosenHash = subHash;
                    }
                    else
                    {
                        if (board.IsWhiteToMove)
                        {
                            if (subEval > eval)
                            {
                                eval = subEval;
                                bestMove = moves[i];
                                chosenHash = subHash;
                            }
                        }
                        else
                        {
                            if (subEval < eval)
                            {
                                eval = subEval;
                                bestMove = moves[i];
                                chosenHash = subHash;
                            }
                        }
                    }

                    if (board.IsWhiteToMove)
                    {
                        alpha = Math.Max(alpha, eval);
                        if (beta <= alpha)
                        {
                            break;
                        }
                    }
                    else
                    {
                        beta = Math.Min(beta, eval);
                        if (beta <= alpha)
                        {
                            break;
                        }
                    }

                    if (Math.Abs(eval) > 1000000)
                    {
                        break;
                    }
                }
            }

            Table[hash] = (eval, bestMove, chosenHash);
            Memory[hash] = eval;
            return (eval, bestMove, chosenHash);
        }

        public long GetMoveDelta(Move move, bool isWhite)
        {
            long delta = IsWhiteToMoveHash;
            int wDelta = isWhite ? 7 : 0;

            delta ^= PieceHashes[(int)move.MovePieceType + wDelta, move.StartSquare.Rank, move.StartSquare.File];

            if (move.IsPromotion)
            {
                delta ^= PieceHashes[(int)move.PromotionPieceType + wDelta, move.StartSquare.Rank, move.StartSquare.File];
            }

            if (move.IsCapture)
            {
                delta ^= PieceHashes[(int)move.CapturePieceType + (7 - wDelta), move.TargetSquare.Rank, move.TargetSquare.File];
            }

            return delta;
        }

        public int Eval(Board board, int depth)
        {
            if (board.IsRepeatedPosition())
            {
                return 0;
            }

            int result;
            if (board.IsInCheckmate())
            {
                result = (100000 + depth) * (board.IsWhiteToMove ? -1 : 1);
            }
            else
            {
                int whiteVal = Eval(board, true);
                int blackVal = Eval(board, false);
                //File.AppendAllText("debug.txt", "          " + board.GetFenString() + "\n");
                //File.AppendAllText("debug.txt", "          " + whiteVal + " : " + blackVal + " : " + (whiteVal - blackVal) + "\n");

                result = whiteVal - blackVal;
            }

            //if (board.GetFenString() == "rnbqkb1r/pppppppp/5n2/4P3/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 2")
            //{
            //    Console.WriteLine("wat " + result);
            //}

            return result;
        }

        public int Eval(Board board, bool isWhite)
        {
            var piece = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
            var pawns = board.GetPieceBitboard(Pawn, isWhite);
            var oppPawn = board.GetPieceBitboard(Pawn, !isWhite);
            var bisho = board.GetPieceBitboard(Bishop, isWhite);
            var knigh = board.GetPieceBitboard(Knight, isWhite);
            var rooks = board.GetPieceBitboard(Rook, isWhite);
            var queen = board.GetPieceBitboard(Queen, isWhite);

            var king = board.GetKingSquare(isWhite);
            var enemyKing = board.GetKingSquare(!isWhite);

            int eval = 0;

            int pawnsPop = PopCount(piece);
            int minorPop = PopCount(bisho | knigh);
            int rooksPop = PopCount(rooks);

            int kingSafety = king.File <= 2 || king.File >= 6 ? 50 : 0 +
                             king.Rank == (isWhite ? 0 : 7) ? 0 : -200;

            int development = PopCount((bisho | knigh) & 0x007E7E7E7E7E7E00);

            if ((minorPop + rooksPop) >= 4)
            {
                //Bonus to king in corner of board
                eval += kingSafety;
                eval += development * 10;
            }
            else if (minorPop >= 2 && rooksPop > 0 && pawnsPop >= 6)
            {
                //Mid game
                eval += kingSafety;
                eval += development * 5;
            }
            else
            {
                //Prefer putting enemy king on edge of board
                int enemyKingEdgeDist = Math.Min(enemyKing.Rank, 7 - enemyKing.Rank) + Math.Min(enemyKing.File, 7 - enemyKing.File);
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

            //Passed pawns
            {
                //TODO
            }

            //Pieces supported by pawns
            {
                ulong supported;
                if (isWhite)
                {
                    supported = ((pawns & 0x7F7F7F7F7F7F7F7F) << 9) | ((pawns & 0xFEFEFEFEFEFEFEFE) << 7);
                }
                else
                {
                    supported = ((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7);
                }
                supported &= piece;
                eval += PopCount(supported) * 5;
            }

            //Piece counts
            {
                eval += pawnsPop * 100 +
                        minorPop * 300 +
                        rooksPop * 500 +
                        PopCount(queen) * 900;
            }

            return eval;
        }
    }
}
