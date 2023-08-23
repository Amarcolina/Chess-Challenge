using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot {

  Move BestMove;

  public Move Think(Board board, Timer timer) {
    int maxDepth = 4;

    int NegaMax(int alpha, int beta, int depth) {
      //if (depth == 0) {
      //  Console.WriteLine(maxDepth);
      //}

      int bestFound = 100000000;

      if (board.IsInStalemate()) return 0;
      if (board.IsInCheckmate()) return bestFound;

      var moves = board.GetLegalMoves().OrderByDescending(t => t.IsCapture);
      if (depth == maxDepth)
        return board.GetAllPieceLists().
                     SelectMany(p => p).
                     Sum(p => (p.IsWhite == board.IsWhiteToMove ? -1 : 1) * (int)p.PieceType * 100) -
                     moves.Count();

      foreach (var move in moves) {
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

    for (; timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 1600; maxDepth += 2)
      NegaMax(1000000000, -1000000000, 0);

    return BestMove;
  }
}
