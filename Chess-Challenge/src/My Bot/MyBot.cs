using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot {

  Move BestMove;

  //int[] Values = { 0, 100, 290, 300, 500, 1000, 0 };

  public Move Think(Board board, Timer timer) {
    int maxDepth = 99999996;

    double NegaMax(double alpha, double beta, int depth) {
      //if (depth == 0) {
      //  Console.WriteLine(maxDepth);
      //}

      double bestFound = depth;

      //if (board.IsInStalemate() || board.IsRepeatedPosition()) return 0;
      //if (board.IsInCheckmate()) return bestFound;

      var moves = board.GetLegalMoves();
      if (depth == maxDepth)
        return board.GetAllPieceLists().
                     SelectMany(p => p).
                     Sum(p => (1078477616 << (int)p.PieceType * 1306960869) * (p.IsWhite == board.IsWhiteToMove ? -0.00001 : 0.00001)) -
                     moves.Length;

      foreach (var move in moves.OrderByDescending(t => t.IsCapture)) {
        board.MakeMove(move);
        //int subEval = -NegaMax(-beta, -alpha, depth - 1);
        double subEval = board.IsDraw() ? 0 : -NegaMax(-beta, -alpha, depth - 1);
        board.UndoMove(move);

        if (subEval < bestFound) {
          bestFound = subEval;
          if (depth == 100000000)
            BestMove = move;

          alpha = Math.Min(alpha, bestFound);
          if (alpha <= beta)
            break;
        }
      }

      return bestFound;
    }

    for (; timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 1600f; maxDepth -= 2)
      NegaMax(1000000000, -1000000000, 100000000);

    return BestMove;
  }
}
