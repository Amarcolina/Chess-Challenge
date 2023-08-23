using System;
using System.Linq;
using ChessChallenge.API;

namespace ChessChallenge.Example {
  // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
  // Plays randomly otherwise.
  public class EvilBot : IChessBot {

    Move BestMove;

    public Move Think(Board board, Timer timer) {
      int maxDepth = 4;

      int NegaMax(int alpha, int beta, int depth) {
        //if (depth == 0) {
        //  Console.WriteLine(maxDepth);
        //}

        int bestFound = 100000000;

        if (board.IsInStalemate() || board.IsRepeatedPosition()) return 0;
        //if (board.IsInCheckmate()) return bestFound;

        var moves = board.GetLegalMoves();
        if (depth == maxDepth)
          return board.GetAllPieceLists().
                       SelectMany(p => p).
                       Sum(p => (p.IsWhite == board.IsWhiteToMove ? -100 : 100) * (int)p.PieceType) -
                       moves.Length;

        foreach (var move in moves.OrderByDescending(t => t.IsCapture)) {
          board.MakeMove(move);
          int subEval = -NegaMax(-beta, -alpha, depth + 1);
          board.UndoMove(move);

          if (subEval < bestFound) {
            bestFound = subEval;
            if (depth == 0)
              BestMove = move;

            alpha = Math.Min(alpha, bestFound);
            if (alpha <= beta)
              break;
          }
        }

        return bestFound;
      }

      for (; timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 1600f; maxDepth += 2)
        NegaMax(1000000000, -1000000000, 0);

      return BestMove;
    }

  }
}
