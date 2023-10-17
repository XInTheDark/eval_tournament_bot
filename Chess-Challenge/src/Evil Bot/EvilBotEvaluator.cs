using ChessChallenge.API;

namespace Chess_Challenge.Example
{

    /// <summary>
    /// This is the evaluation function used by the EvilBot.
    /// EvilBot uses the same search implementation as MyBot,
    /// so if you want to compare eval functions, simply
    /// copy the other eval function here.
    /// </summary>
    public class EvilBotEvaluator : IEvaluator
    {
        public int Evaluate(Board board, Timer timer)
        {
            return 42;
        }
    }
}