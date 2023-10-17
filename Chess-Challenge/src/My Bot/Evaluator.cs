using ChessChallenge.API;
using System;

public static class Values
{
    // null, pawn, knight, bishop, rook, queen, king
    public static int[] pieceValues = { 0, 126, 781, 781, 1276, 2538, 40000 };
    
    // Psqt
    public static int[,] allPsqt = new int[,]
    {
        // 0 - Pawn
        {
            0, 0, 0, 0,
            -4, -1, 10, 16,
            -9, -15, 17, 23,
            2, -15, 8, 29,
            11, 2, -4, 7,
            14, 4, 7, 18,
            -5, 8, -7, -3,
            0, 0, 0, 0
        },
        
        // 1 - Knight
        {
            -136, -78, -61, -47,
            -72, -47, 22, -3,
            -50, -22, -1, 20,
            -35, 3, 27, 39,
            -40, -2, 31, 45,
            -25, 15, 58, 42,
            -68, -37, -5, 30,
            -150, -84, -56, -24
        },
        
        // 2 - Rook
        // {
        //     -21, -17, -12, -7,
        //     -16, -11, -4, 2,
        //     -7, -9, -1, 1,
        //     -9, -2, -6, 0,
        //     -14, -5, 2, -2,
        //     -8, 0, 0, 11,
        //     1, 8, 18, 14,
        //     -10, -18, 9, 11
        // },
        
        // 2 - King
        {
            271, 327, 271, 198,
            278, 303, 234, 164,
            194, 233, 164, 91,
            164, 202, 133, 61,
            154, 179, 105, 70,
            123, 145, 81, 31,
            88, 120, 65, 13,
            0, 0, 0, 0
        }
    };
}

public class Evaluator : IEvaluator
{
    public int Evaluate(Board board, Timer timer)
    {
        int psqIndex(int i)
        {
            int y = i % 8;
            if (y >= 4) y = 7 - y;
            return i / 8 * 4 + y;
        }
        
        int ColorV(bool color)
        {
            return color ? 1 : -1;
        }
        
        // init variables
        var pieceList = board.GetAllPieceLists();
        bool stm = board.IsWhiteToMove, endgame = pieceList.Length <= 12;
        int i = 0; // temp variable used to save tokens
        
        int materialScore = 0, mobilityScore = 0, spaceScore = 0, psqtScore = 0; 
        // init everything here to save tokens
        
        // Material
        foreach (var list in pieceList)
            foreach (var piece in list)
                materialScore += Values.pieceValues[(int)piece.PieceType] * ColorV(piece.IsWhite);
        
        
        // PSQT
        foreach (bool color in new[] { true, false })
        foreach (PieceType pc in new[] { PieceType.Pawn, PieceType.Knight, PieceType.King})
        foreach (var x in board.GetPieceList(pc, color))
        {
            i = x.Square.Index;
            // j = color ? i : 63 - i;
            psqtScore += Values.allPsqt[Math.Min((int)pc, 4) >> 1, psqIndex(color ? i : 63 - i)] * ColorV(color);
            // psqt types: 1 (pawn) -> 0, 2 (knight) -> 1, 6 (king) -> 2
        }
        
        
        // Mobility -> skip in endgames
        if (!endgame)
        {
            mobilityScore += board.GetLegalMoves().Length >> 2;
            board.ForceSkipTurn();
            mobilityScore += board.GetLegalMoves().Length >> 2;
            board.UndoSkipTurn();
        }

        // Passed pawns (todo)
        
        // Space -> skip in endgames
        if (Math.Abs(materialScore) < 2000 && !endgame) // skip space if material eval is high
        {
            // bitboard representing C file
            // ulong CDEFfiles = 0x3c3c3c3c3c3c3c3c,
            //     Rank123 = 0xffffff0000000000,
            //     Rank678 = 0xffffff; 
            ulong spaceMaskWhite = 0x3c3c3c3c3c3c3c3c & 0xffffff0000000000,
                spaceMaskBlack = 0x3c3c3c3c3c3c3c3c & 0xffffff;
            
            // get info about whether square is attacked
            // we only have board.IsSquareAttacked(Square square, bool isWhite) so we need to iterate over all squares
            // and check if they are attacked by white or black
            while (i++ < 63)
            {
                Square square = new Square(i);
                if (!stm) board.ForceSkipTurn(); // set stm to white
                if (board.SquareIsAttackedByOpponent(square))
                    BitboardHelper.ClearSquare(ref spaceMaskWhite, square); // set to 0 if unsafe

                if (stm) board.ForceSkipTurn(); // set stm to black
                if (board.SquareIsAttackedByOpponent(square))
                    BitboardHelper.ClearSquare(ref spaceMaskBlack, square); // set to 0 if unsafe
                
                board.UndoSkipTurn();
            }
            
            // count number of safe squares
            spaceScore = 2 * (BitboardHelper.GetNumberOfSetBits(spaceMaskWhite) - BitboardHelper.GetNumberOfSetBits(spaceMaskBlack));
        }

        int score = materialScore + mobilityScore + spaceScore + psqtScore;
        
        // // Rule50
        // score = score * (200 - board.FiftyMoveCounter) / 200;
        
        // Optimism
        if (score > 0) score = score * 11 / 10;
        
        return stm ? score : -score;
    }
}