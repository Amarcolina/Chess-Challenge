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
        public static int[] PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 100 };

        public Dictionary<ulong, (int eval, Move move, int depth)> Table = new();
        public Dictionary<(ulong, Move), int> Memory = new();
        //public Dictionary<ulong, string> HashToFen = new();
        //public Dictionary<ulong, string> HashToRep = new();

        public int PositionsSearched = 0;
        public Board Board;
        public Timer Timer;

        private ulong HashInQuestion;
        private int MultInQuestion;

        public Move Think(Board board, Timer timer) {
            Board = board;
            Timer = timer;

            Table.Clear();
            Memory.Clear();
            PositionsSearched = 0;

            (int eval, Move move) result = default;
            for (int depth = 1; ; depth++) {
                Table.Clear();

                //File.Delete("debug.txt");

                //Console.WriteLine("Eval at depth: " + depth);
                var result2 = EvalRecursive(int.MinValue, int.MaxValue, depth, true);

                

                if (timer.MillisecondsElapsedThisTurn > 1000) {
                    return result.move;
                }
                result = result2;

            }

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

            //Console.WriteLine("Eval: " + result.eval + " : " + Eval(0) + " : " + PositionsSearched);


            //Console.WriteLine(HashToFen[result.hash]);

            //var pawns = board.GetPieceBitboard(Pawn, false);
            // BitboardHelper.VisualizeBitboard(((pawns & 0xFEFEFEFEFEFEFEFE) >> 9) | ((pawns & 0x7F7F7F7F7F7F7F7F) >> 7));

            //return result.move;
        }

        public int Compare(Move x, Move y) {
            Memory.TryGetValue((HashInQuestion, x), out var memoryX);
            Memory.TryGetValue((HashInQuestion, y), out var memoryY);

            return (memoryY - memoryX) * MultInQuestion;
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

        public (int eval, Move move) EvalRecursive(int alpha, int beta, int depth, bool root) {
            ulong hash = Board.ZobristKey;

            //if (HashToRep.TryGetValue(hash, out var rep)) {
            //    var rep2 = BuildCanonicalId(board);
            //    if (rep != rep2) {
            //        throw new InvalidOperationException(rep + "         " + rep2);
            //    }
            //} else {
            //    HashToRep[hash] = BuildCanonicalId(board);
            //}

            //if (!HashToFen.ContainsKey(hash)) {
            //    HashToFen[hash] = board.GetFenString();
            //}

            if (Table.TryGetValue(hash, out var value)) {
                //Only use the transposition table if the depth is shallower, else deeper depths
                //are considered less reliable
                if (value.depth >= depth) {
                    return (value.eval, value.move);
                }
            }

            //PositionsSearched++;
            //if ((PositionsSearched % (1024 * 256)) == 0) {
            //    Console.WriteLine(PositionsSearched + " : " + depth);
            //}

            int eval = Eval(depth);
            bool isWinning = Board.IsWhiteToMove ? (eval > 0) : (eval < 0);
            Move bestMove = default;
            ulong chosenHash = 0;

            Span<Move> moves = stackalloc Move[128];
            Board.GetLegalMovesNonAlloc(ref moves, depth <= 0 && !Board.IsInCheck());

            if (root && moves.Length == 1) {
                return (0, moves[0]);
            }

            bool shouldRecurse = true;
            if (moves.Length == 0 || depth <= 0) {
                shouldRecurse = false;
                chosenHash = hash;

                if (moves.Length > 0) {
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

            if (shouldRecurse) {
                HashInQuestion = hash;
                MultInQuestion = Board.IsWhiteToMove ? 1 : -1;
                moves.Sort(this);

                bestMove = moves[0];

                bool isInCheck = Board.IsInCheck();

                for (int i = 0; i < moves.Length; i++) {
                    if (Timer.MillisecondsElapsedThisTurn > 1000 && root) {
                        break;
                    }

                    Board.MakeMove(moves[i]);

                    //hash ^= hashDelta;

                    //File.AppendAllText("debug.txt", "{" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + "\n");

                    //Never consider a repeated position if we are winning
                    if (Board.IsRepeatedPosition() && isWinning) {
                        //File.AppendAllText("debug.txt", "repeat\n");
                        Board.UndoMove(moves[i]);
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
                    //UNLESS the king is in check, in which case we consider all options
                    if (depth <= 0 && moves[i].IsCapture && !isInCheck) {
                        int srcValue = PieceValue[(int)moves[i].MovePieceType];
                        int dstValue = PieceValue[(int)moves[i].CapturePieceType];
                        if (dstValue < srcValue) {
                            Board.UndoMove(moves[i]);
                            //File.AppendAllText("debug.txt", "undone\n");
                            continue;
                        }
                    }

                    (var subEval, var subMove) = EvalRecursive(alpha, beta, depth - 1, false);
                    ulong subHash = Board.ZobristKey;
                    Memory[(hash, moves[i])] = subEval;

                    //File.AppendAllText("debug.txt", "}" + depth.ToString() + "".PadLeft(5 - depth) + moves[i].ToString() + " : " + subEval.ToString() + "\n");

                    //hash ^= hashDelta;
                    Board.UndoMove(moves[i]);

                    if (i == 0) {
                        eval = subEval;
                        bestMove = moves[i];
                        chosenHash = subHash;
                    } else {
                        if (Board.IsWhiteToMove) {
                            if (subEval > eval) {
                                eval = subEval;
                                bestMove = moves[i];
                                chosenHash = subHash;
                            }
                        } else {
                            if (subEval < eval) {
                                eval = subEval;
                                bestMove = moves[i];
                                chosenHash = subHash;
                            }
                        }
                    }

                    if (Board.IsWhiteToMove) {
                        alpha = Math.Max(alpha, eval);
                        if (beta <= alpha) {
                            break;
                        }
                    } else {
                        beta = Math.Min(beta, eval);
                        if (beta <= alpha) {
                            break;
                        }
                    }

                    if (Math.Abs(eval) > 1000000) {
                        break;
                    }
                }
            }

            //if (hash == 8064761405595400716) {
            //    Console.WriteLine("What");
            //}

            Table[hash] = (eval, bestMove, depth);
            //Memory[hash] = eval;
            return (eval, bestMove);
        }

        public int Eval(int depth) {
            if (Board.IsRepeatedPosition()) {
                return 0;
            }

            if (Board.IsInCheckmate()) {
                return (100000 + depth) * (Board.IsWhiteToMove ? -1 : 1);
            } else {
                return Eval(true) - Eval(false);
            }
        }

        public int Eval(bool isWhite) {
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
                             king.Rank == (isWhite ? 0 : 7) ? 0 : -200;

            int development = PopCount((bisho | knigh) & 0x007E7E7E7E7E7E00);

            int kingProtected = ((kings >> 8 | kings << 8) & piece) != 0 ? 1 : 0;

            int knightsActive = PopCount(knigh & 0x00003C3C3C3C0000);

            int doubledPawns = Math.Max(0, PopCount(pawns & 0x80) - 1) +
                               Math.Max(0, PopCount(pawns & 0x40) - 1) +
                               Math.Max(0, PopCount(pawns & 0x20) - 1) +
                               Math.Max(0, PopCount(pawns & 0x10) - 1) +
                               Math.Max(0, PopCount(pawns & 0x08) - 1) +
                               Math.Max(0, PopCount(pawns & 0x04) - 1) +
                               Math.Max(0, PopCount(pawns & 0x02) - 1) +
                               Math.Max(0, PopCount(pawns & 0x01) - 1);

            if ((minorPop + rooksPop) >= 4) {
                //Bonus to king in corner of board
                eval += kingSafety;
                eval += development * 10;
                eval += kingProtected * 50;
                eval -= doubledPawns * 40;
            } else if (minorPop >= 2 && rooksPop > 0 && pawnsPop >= 6) {
                //Mid game
                eval += kingSafety;
                eval += development * 15;
                eval += kingProtected * 50;
                eval += knightsActive * 20;
                eval -= doubledPawns * 40;
            } else {
                //Prefer putting enemy king on edge of board
                int enemyKingEdgeDist = Math.Min(enemyKing.Rank, 7 - enemyKing.Rank) + Math.Min(enemyKing.File, 7 - enemyKing.File);
                eval -= enemyKingEdgeDist;

                //Prefer having pawns close to queening
                if (isWhite) {
                    eval -= LeadingZeroCount(pawns);
                } else {
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
                if (isWhite) {
                    supported = ((pawns & 0x7F7F7F7F7F7F7F7F) << 9) | ((pawns & 0xFEFEFEFEFEFEFEFE) << 7);
                } else {
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
                        PopCount(queen) * 1100;
            }

            return eval;
        }
    }
}
