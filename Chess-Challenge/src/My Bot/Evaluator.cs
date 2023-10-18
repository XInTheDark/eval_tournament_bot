using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class Values
{
    // null, pawn, knight, bishop, rook, queen, king
    public static int[] pieceValues = { 0, 126, 781, 781, 1276, 2538, 40000 };
    // bishop, rook, queen
    public static int[] mobilityValues  = { 6, 5, 3, 0 };
    
    // Packed Psqt
    public static decimal[,] packedPsqt =
    {
        // x2 quantisation
        {4034157052646293636858773504m, 2789001439998792313019365633m, 4277994750m}, // pawn (1)
        {3404287441579403956711512508m, 6534281595731031271123255791m, 17646465313090236126m}, // knight (2)
        {2164014922328260742594755565m, 1859341949334155696276046588m, 17868308529803295480m}, // bishop (3)
        {1194703523763084064389366m, 1547429716061480218496204796m, 361686527623365632m}, // rook (4)
        {935717900348897334573004544m, 1240376670144116481419706882m, 17504344892242065654m}, // queen (5)
        {7167995988612654671579075140m, 2500229339330604076760118057m, 51387926m}, // king (6)
    };
}

public class Evaluator : IEvaluator
{
    public static int[,] psqts = new int[6, 32]; // piece type, square
    public int i, j, k; // temp variable used to save tokens
    public bool b1;

    public Evaluator() // init
    {
        // init psqts - extract from packed
        for (i = 0; i < 6; i++)
        {
            var psqt = new sbyte[32];
            extract(i, out psqt);
            for (j = 0; j < 32; j++)
                psqts[i, j] = psqt[j] * 2; // 2x quantisation
        }
    }

    public void extract(int pc, out sbyte[] psqt)
    {
        var decimals = new List<decimal>();
        for (k = 0; k < 3; k++) // can't use i here since it's used in main init function
            decimals.Add(Values.packedPsqt[pc, k]);
        psqt = decimals.SelectMany(x => decimal.GetBits(x).Take(3).SelectMany(y => BitConverter.GetBytes(y).Select(z => (sbyte)z))).ToArray();
    }
    
    public int Evaluate(Board board, Timer timer)
    {
        int ColorV(bool color)
        {
            return color ? 1 : -1;
        }
        
        // init variables
        var pieceList = board.GetAllPieceLists();
        bool stm = board.IsWhiteToMove, endgame = pieceList.Length <= 12;
        
        int materialScore = 0, mobilityScore = 0, psqtScore = 0; 
        // init everything here to save tokens
        
        // Material and mobility
        foreach (var list in pieceList)
        foreach (var piece in list)
        {
            b1 = piece.IsWhite;
            j = (int)piece.PieceType;
            materialScore += Values.pieceValues[j] * ColorV(b1);
            
            // Mobility
            // The more squares you are able to attack, the more flexible your position is.
            if (j < 3) continue; // skip pawns and knights
            var mob = BitboardHelper.GetPieceAttacks((PieceType)j, piece.Square, board, b1) & 
                      ~(b1 ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
            mobilityScore += Values.mobilityValues[j-3] * BitboardHelper.GetNumberOfSetBits(mob) * ColorV(b1)
            // King attacks
            + 2 * Values.mobilityValues[j-3] * BitboardHelper.GetNumberOfSetBits(mob & BitboardHelper.GetKingAttacks(board.GetKingSquare(!b1)));
        }


        // PSQT
        foreach (bool color in new[] { true, false })
           for (k = 1; k < 7; k++) // piece type
              foreach (var x in board.GetPieceList((PieceType)k, color))
        {
            i = x.Square.Index;
            if (!color) i = 63 - i; // flip square if black
            // Math.Min((int)pc, 4) >> 1, psqIndex(color ? i : 63 - i)
            psqtScore += psqts[k - 1, // piece type
                             i / 8 * 4 + Math.Min(i % 8, 7 - i % 8) // map square to psqt index
                             ]
                         * ColorV(color);
        }
        
        // Passed pawns (todo)

        int score = materialScore + mobilityScore + psqtScore;
        
        // // Rule50
        // score = score * (200 - board.FiftyMoveCounter) / 200;
        
        // Optimism
        if (score > 0) score = score * 11 / 10;
        
        return stm ? score : -score;
    }
}