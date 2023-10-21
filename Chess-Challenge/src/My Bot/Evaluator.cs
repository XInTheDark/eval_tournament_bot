using ChessChallenge.API;
using static ChessChallenge.API.BitboardHelper;
using System;
using static System.Math;
using System.Collections.Generic;
using System.Linq;

public class Evaluator : IEvaluator
{
    public static int[,] psqts = new int[6, 32]; // piece type, square
    public int i, j, k, scoreAccum, rank, file; // temp variable used to save tokens
    
    /* VALUES */
    // null, pawn, knight, bishop, rook, queen, king
    public static readonly int[] pieceValues = { 0, 126, 781, 781, 1276, 2538, 40000 };
    // bishop, rook, queen
    public static readonly int[] mobilityValues  = { 6, 5, 3, 0 };
    
    // Packed Psqt
    public static readonly decimal[,] packedPsqt =
    {
        // x2 quantisation
        {4034157052646293636858773504m, 2789001439998792313019365633m, 4277994750m}, // pawn (1)
        {3404287441579403956711512508m, 6534281595731031271123255791m, 17646465313090236126m}, // knight (2)
        {2164014922328260742594755565m, 1859341949334155696276046588m, 17868308529803295480m}, // bishop (3)
        {1194703523763084064389366m, 1547429716061480218496204796m, 361686527623365632m}, // rook (4)
        {935717900348897334573004544m, 1240376670144116481419706882m, 17504344892242065654m}, // queen (5)
        {7167995988612654671579075140m, 2500229339330604076760118057m, 51387926m}, // king (6)
    };
    
    public Evaluator() // init
    {
        // init psqts - extract from packed
        for (; i < 6; i++) // i=0 since this is the constructor
        {
            /* extract */
            var decimals = new List<decimal>();
            for (k = 0; k < 3; k++) // can't use i here since it's used in main init function
                decimals.Add(packedPsqt[i, k]);
            var psqt = decimals.SelectMany(x => decimal.GetBits(x).Take(3).SelectMany(y => 
                BitConverter.GetBytes(y).Select(z => (sbyte)z))).ToArray();
            
            for (j = 0; j < 32; j++)
                psqts[i, j] = psqt[j] * 2; // 2x quantisation
        }
    }
    
    public int Evaluate(Board board, Timer timer)
    {
        int ColorV(bool color)
            => color ? 1 : -1;

        // init variables
        bool stm = board.IsWhiteToMove;

        int score = 0,
            pieceCount = GetNumberOfSetBits(board.AllPiecesBitboard);

        // Material, PSQT & mobility
        foreach (bool color in new[] { true, false })
        {
            ulong pawnBB = board.GetPieceBitboard(PieceType.Pawn, !color); // opponent's pawns
            
            for (k = 1; k < 7; k++) // piece type
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)k, color);

                while (bitboard != 0) // iterate over every piece of that type
                {
                    scoreAccum = 0;

                    /* Material */
                    scoreAccum += pieceValues[k];

                    i = ClearAndGetIndexOfLSB(ref bitboard); // square

                    /* Mobility */
                    // The more squares you are able to attack, the more flexible your position is.
                    if (k > 2)
                    {
                        // skip pawns and knights
                        ulong mob = GetPieceAttacks((PieceType)k, new Square(i), board, color) &
                                    ~(color ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
                        j = mobilityValues[k - 3];
                        scoreAccum += j * GetNumberOfSetBits(mob)
                            // King attacks
                            + j + 1 >> 1
                            * GetNumberOfSetBits(
                                mob & GetKingAttacks(board.GetKingSquare(!color)));
                    }
                    
                    // /* Open file stuff from cj */
                    // if ((0x101010101010101UL << i % 8 & ~(1UL << i) & board.GetPieceBitboard(PieceType.Pawn, color)) ==
                    //     0)
                    //     score += ____;

                    /* PSQT */
                    if (!color) i ^= 56; // flip square if black
                    rank = i >> 3; file = i & 7;
                    scoreAccum += psqts[k - 1, // piece type
                        rank * 4 + Min(file, 7 - file) // map square to psqt index
                    ];

                    /* late endgame: incentivize king moving towards center */
                    if (pieceCount <= 14 && k == 6)
                        scoreAccum -= 20 * (Abs(4 - rank) + Abs(4 - file));

                    /* Passed Pawn */
                    // basic detection
                    // this is mainly to guide the engine to push pawns in the endgame.
                    if (k == 1)
                    {
                        // Observe: if we get the bit 8 bits from the current pawn, and it's set, then it's not a passed pawn.
                        bool is_passed = true;
                        for (j = i + 8; j < 64; j += 8) // j + 8 < 64
                            if (SquareIsSet(pawnBB, new Square(j)))
                                is_passed = false; // removed break statement to save tokens

                        if (is_passed)
                            // this is a passed pawn!
                            // note how i has already been flipped based on stm, in PSQT.
                            // scale based on rank and piece count.
                            scoreAccum += rank << 8 / Max(pieceCount, 14);  // rank * (256 / pieceCount)
                    }

                    score += scoreAccum * ColorV(color);
                }
            }
        }

        /* STM */
        score *= ColorV(stm);
        
        /* Tempo */
        // Give bonus to stm.
        // However if in check give a small penalty.
        score += board.IsInCheck() ? -5 : 15;

        return score;
    }
}
