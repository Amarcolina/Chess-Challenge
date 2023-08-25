using System;
using System.Linq;
using ChessChallenge.API;

namespace ChessChallenge.Example {

  public class EvilBot : IChessBot {

    public Move Think(Board board, Timer timer) {
      if (timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 10000f) {
        return board.GetLegalMoves()[0];
      } else {
        return default;
      }
    }
  }
}
