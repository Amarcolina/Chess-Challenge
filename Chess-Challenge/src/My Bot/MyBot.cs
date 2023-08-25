using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot {

  Move BestMove;

  public Move Think(Board board, Timer timer) {
    long maxDepth = 19999999996;

    long NegaMax(long alpha, long beta, long depth, long eval) {
      long bestFound = depth;

      var moves = board.GetLegalMoves();
      if (depth == maxDepth)
        return eval - moves.Length;

      foreach (var move in moves.OrderBy(t => t.IsCapture)) {
        board.MakeMove(move);
        long subEval = board.IsDraw() ?
                         0 :
                        -NegaMax(-beta, -alpha, depth - 1,
                        -eval + (move.IsPromotion ? 900 : move.IsCapture ? 1078477616 << (int)move.CapturePieceType * 1306960869 : 0));
        board.UndoMove(move);

        if (subEval < bestFound) {
          bestFound = subEval;
          if (depth == 20000000000)
            BestMove = move;

          alpha = Math.Min(alpha, bestFound);
          if (alpha <= beta)
            break;
        }
      }

      return bestFound;
    }

    for (; timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 500f; maxDepth -= 2)
      NegaMax(30000000000, -30000000000, 20000000000, 0);

    return BestMove;
  }
}
