using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Values
{
    // null, pawn, knight, bishop, rook, queen, king
    public static int[] pieceValues = { 0, 126, 781, 781, 1276, 2538, 40000 };
    
    // Packed Psqt
    public static decimal[,] packedPsqt = new decimal[,]
    {
        {4034157052646293636858773504m, 2789001439998792313019365633m, 4277994750m}, // pawn (1)
        {3404287441579403956711512508m, 6534281595731031271123255791m, 17646465313090236126m}, // knight (2)
        {1194703523763084064389366m, 1547429716061480218496204796m, 361686527623365632m}, // rook (4)
        {7167995988612654671579075140m, 2500229339330604076760118057m, 51387926m}, // king (6)
    };
}

public class Evaluator : IEvaluator
{
    public sbyte[] PawnPsqt, KnightPsqt, RookPsqt, KingPsqt;
    public int i, j; // temp variable used to save tokens

    public void extract(int pc, ref sbyte[] psqt)
    {
        var decimals = new List<decimal>();
        for (i = 0; i < 3; i++)
            decimals.Add(Values.packedPsqt[pc, i]);
        psqt = decimals.SelectMany(x => decimal.GetBits(x).Take(3).SelectMany(y => BitConverter.GetBytes(y).Select(z => (sbyte)z))).ToArray();
    }
    public void Init()
    {
        // init PSQT
        // PSQT: extract from packed
        extract(0, ref PawnPsqt);
        extract(1, ref KnightPsqt);
        extract(2, ref RookPsqt);
        extract(3, ref KingPsqt);
    }
    
    public int Query(int pc, int sq)
    {
        sbyte[] psqt = pc switch
        {
            0 => PawnPsqt,
            1 => KnightPsqt,
            2 => RookPsqt,
            3 => KingPsqt,
        };
        return psqt[sq];
    }
    
    public int Evaluate(Board board, Timer timer)
    {
        Init();
        int ColorV(bool color)
        {
            return color ? 1 : -1;
        }
        
        // init variables
        var pieceList = board.GetAllPieceLists();
        bool stm = board.IsWhiteToMove, endgame = pieceList.Length <= 12;
        
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
            j = color ? i : 63 - i;
            // Math.Min((int)pc, 4) >> 1, psqIndex(color ? i : 63 - i)
            psqtScore += Query((int)pc == 6 ? 3 : (int)pc >> 1, // piece type
                             j / 8 * 4 + Math.Min(j % 8, 7 - j % 8) // map square to psqt index
                             )
                         * ColorV(color);
        }
        
        // // Mobility -> skip in endgames
        // if (!endgame)
        // {
        //     mobilityScore += board.GetLegalMoves().Length >> 2;
        //     board.ForceSkipTurn();
        //     mobilityScore += board.GetLegalMoves().Length >> 2;
        //     board.UndoSkipTurn();
        // }

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
            i = 0;
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