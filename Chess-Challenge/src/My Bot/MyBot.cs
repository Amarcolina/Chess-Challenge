using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot {

  Move BestMove;

  public Move Think(Board board, Timer timer) {
    long maxDepth = 19999999996;

    //for(var k=PieceType.Pawn; k<=PieceType.Queen; k++) {
    //  Console.WriteLine((1078477616 << (int)k * 1306960869));
    //}

    double NegaMax(double alpha, double beta, long depth, double eval) {
      double bestFound = depth;

      var moves = board.GetLegalMoves();
      if (depth == maxDepth)
        return eval - moves.Length;

      foreach (var move in moves.OrderByDescending(t => t.IsCapture)) {
        board.MakeMove(move);
        double subEval = board.IsDraw() ?
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

    //for(int i=0; i<3; i++)
    for (; timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 1000f; maxDepth -= 2)
      NegaMax(3000000000, -3000000000, 20000000000, 0);

    return BestMove;
  }
}
