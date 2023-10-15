using ChessChallenge.API;
using System;

public static class Values
{
    // null, pawn, knight, bishop, rook, queen, king
    public static readonly int[] pieceValues = { 0, 126, 825, 781, 1276, 2538, 40000 };
}

public class Evaluator : IEvaluator
{
    public int Evaluate(Board board, Timer timer)
    {
        void nullMove()
        {
            board.ForceSkipTurn();
        }
        void undoNullMove()
        {
            board.UndoSkipTurn();
        }
        
        bool stm = board.IsWhiteToMove;
        int score = 0, materialScore = 0;
        // Material
        var pieceList = board.GetAllPieceLists();
        foreach (var list in pieceList)
        {
            foreach (var piece in list)
            {
                materialScore += Values.pieceValues[(int)piece.PieceType] * (piece.IsWhite ? 1 : -1);
            }
        }
        
        // Mobility
        int mobilityScore = 0;
        mobilityScore += board.GetLegalMoves().Length >> 2;
        nullMove();
        mobilityScore += board.GetLegalMoves().Length >> 2;
        undoNullMove();
        
        // Space
        int spaceScore = 0;
        if (Math.Abs(materialScore) < 2000) // skip space if material eval is high
        {
            // bitboard representing C file
            const ulong CDEFfiles = 0x3c3c3c3c3c3c3c3c,
                        Rank123 = 0xffffff0000000000,
                        Rank678 = 0xffffff;
            ulong spaceMaskWhite = CDEFfiles & Rank123, spaceMaskBlack = CDEFfiles & Rank678;
            // get info about whether square is attacked
            // we only have board.IsSquareAttacked(Square square, bool isWhite) so we need to iterate over all squares
            // and check if they are attacked by white or black
            for (int i = 0; i < 64; i++)
            {
                Square square = new Square(i);
                if (!stm) nullMove(); // set stm to white
                if (board.SquareIsAttackedByOpponent(square))
                {
                    BitboardHelper.ClearSquare(ref spaceMaskWhite, square); // set to 0 if unsafe
                }

                if (stm) nullMove(); // set stm to black
                if (board.SquareIsAttackedByOpponent(square))
                {
                    BitboardHelper.ClearSquare(ref spaceMaskBlack, square); // set to 0 if unsafe
                }
                
                undoNullMove();
            }
            
            // count number of safe squares
            spaceScore = BitboardHelper.GetNumberOfSetBits(spaceMaskWhite) - BitboardHelper.GetNumberOfSetBits(spaceMaskBlack);
            
        }
        
        score = materialScore + mobilityScore + spaceScore;
        
        // Rule50
        score = score * (200 - board.FiftyMoveCounter) / 200;
        
        return stm ? score : -score;
    }
}