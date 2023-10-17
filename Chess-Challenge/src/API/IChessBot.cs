
namespace ChessChallenge.API
{
    public interface IChessBot
    {
        (Move, int) Think(Board board, Timer timer);
        
    }
}
