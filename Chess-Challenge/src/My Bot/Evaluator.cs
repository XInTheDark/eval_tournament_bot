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
        bool stm = board.IsWhiteToMove,
            endgame = BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) <= 16;
        
        // check for draw since search function's draw detection isn't complete
        if (board.IsDraw()) return 0;
        
        // int materialScore = 0, mobilityScore = 0, psqtScore = 0; 
        int score = 0;

        // Material, PSQT & mobility
        foreach (bool color in new[] { true, false })
            for (k = 1; k < 7; k++) // piece type
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)k, color), 
                    pawnBB = board.GetPieceBitboard(PieceType.Pawn, !color); // opponent's pawns
                
                while (bitboard != 0) // iterate over every piece of that type
                {
                    /* Material */
                    score += Values.pieceValues[k] * ColorV(color);
                    
                    i = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard); // square
                    
                    /* Mobility */
                    // The more squares you are able to attack, the more flexible your position is.
                    if (k > 2)
                    {
                        // skip pawns and knights
                        ulong mob = BitboardHelper.GetPieceAttacks((PieceType)k, new Square(i), board, color) &
                                    ~(color ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
                        score += Values.mobilityValues[k - 3] * BitboardHelper.GetNumberOfSetBits(mob) *
                                         ColorV(color);
                        // // King attacks
                        // + board.PlyCount < 30 ? 0 :
                        //     Values.mobilityValues[k-3] + 1 >> 1 * BitboardHelper.GetNumberOfSetBits(mob & BitboardHelper.GetKingAttacks(board.GetKingSquare(!color)))
                        //        * ColorV(color);
                    }

                    /* PSQT */
                    if (!color) i ^= 56; // flip square if black
                    score += psqts[k - 1, // piece type
                                     i / 8 * 4 + Math.Min(i % 8, 7 - i % 8) // map square to psqt index
                                 ]
                                 * ColorV(color);
                    
                    /* Passed Pawn */
                    // basic detection
                    // this is mainly to guide the engine to push pawns in the endgame.
                    if (k != 1 || !endgame) continue; // non-pawn or not in endgame
                    // Observe: if we get the bit 8 bits from the current pawn, and it's set, then it's not a passed pawn.
                    bool is_passed = true;
                    for (j = i+8; j < 64; j += 8) // j + 8 < 64
                        if (BitboardHelper.SquareIsSet(pawnBB, new Square(j)))
                            is_passed = false; // removed break statement to save tokens

                    if (is_passed)
                        // this is a passed pawn!
                        // note how i has already been flipped based on stm, in PSQT.
                        // value passed pawns less if we have a rook.
                        score += i / 8 * (8 >> (board.GetPieceBitboard(PieceType.Rook, color) > 0 ? 1 : 0)) *
                                         ColorV(color);
                }
            }
        
        // int score = materialScore + mobilityScore + psqtScore;
        
        // // Optimism
        // if (score > 0) score = score * 11 / 10;
        
        return stm ? score : -score;
    }
}
