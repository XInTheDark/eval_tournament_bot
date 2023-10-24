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
    public static readonly int[] evalValues  =
    {
        6, 5, 3, 0, // mobility
        18, 1, 2, 26, 8, -13, // open file
        150, 801, 852, 1307, 2581, 40000 // material
    };
    
    // Packed Psqt
    public static readonly decimal[] packedPsqt =
    {
        // x2 quantisation
        4034157052646293636858773504m, 2789001439998792313019365633m, 4277994750m, // pawn (1)
        2478254930630260639978739380m, 6520955114482203651610772206m, 17646465265845333982m, // knight (2)
        1854520467773665165346274285m, 929677993905542949369152252m, 17940367219057883896m, // bishop (3)
        309465972997065300676048884m, 1549842771690800342854663930m, 361125785249514752m, // rook (4)
        933290603903513349640943094m, 928464345431493250755396094m, 18085039881020571383m, // queen (5)
        21147051397208103401407871327m, 12137975184157770585622600265m, 799995959288806438m, // king (6)
    };
    
    public Evaluator() // init
    {
        // init psqts - extract from packed
        for (; i < 6; i++) // i=0 since this is the constructor
        {
            /* extract */
            var decimals = new List<decimal>();
            for (k = 0; k < 3; k++)
                decimals.Add(packedPsqt[i * 3 + k]);
            var psqt = decimals.SelectMany(x => decimal.GetBits(x).Take(3).SelectMany(y => 
                BitConverter.GetBytes(y).Select(z => (sbyte)z))).ToArray();
            
            for (j = 0; j < 32; j++)
                psqts[i, j] = psqt[j] * 2; // 2x quantisation
        }
    }
    
    public int Evaluate(Board board, Timer timer)
    {
        // init variables
        bool stm = board.IsWhiteToMove;

        int score = 0,
            pieceCount = GetNumberOfSetBits(board.AllPiecesBitboard);

        // Material, PSQT & mobility
        foreach (bool color in new[] { true, false })
        {
            ulong pawnBB = board.GetPieceBitboard(PieceType.Pawn, !color); // opponent's pawns
            
            scoreAccum = k = 0; while (k++ < 6) // piece type, k -> 1 to 6
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)k, color);

                while (bitboard != 0) // iterate over every piece of that type
                {
                    /* Material */
                    scoreAccum += evalValues[k + 9];

                    i = ClearAndGetIndexOfLSB(ref bitboard); // square

                    /* Mobility */
                    // The more squares you are able to attack, the more flexible your position is.
                    if (k > 2)
                    {
                        // skip pawns and knights
                        ulong mob = GetPieceAttacks((PieceType)k, new Square(i), board, color) &
                                    ~(color ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
                        j = evalValues[k - 3];
                        scoreAccum += j * GetNumberOfSetBits(mob);
                    }

                    /* PSQT */
                    if (!color) i ^= 56; // flip square if black
                    rank = i >> 3; file = i & 7;
                    scoreAccum += psqts[k - 1, // piece type
                        rank * 4 + Min(file, 7 - file) // map square to psqt index
                    ];

                    /* late endgame: incentivize king moving towards center */
                    if (pieceCount <= 14 && k == 6)
                        scoreAccum -= (26 - pieceCount) * (Abs(4 - rank) + Abs(4 - file));
                    
                    /* Open files, doubled pawns */
                    if ((0x101010101010101UL << file & ~(1UL << i) &
                         board.GetPieceBitboard(PieceType.Pawn, color)) == 0)
                        scoreAccum += evalValues[3 + k];
                    
                    /* Passed Pawn */
                    // basic detection
                    // this is mainly to guide the engine to push pawns in the endgame.
                    if (k == 1 && rank > 3 
                               && (0x101010101010101UL << file & pawnBB) == 0)
                            scoreAccum += rank * 224 / pieceCount;
                } // piece bitboard loop
            } // piece type loop
            
            score += scoreAccum * (color ? 1 : -1);
        } // color loop

        /* STM */
        score *= stm ? 1 : -1;
        
        /* Tempo */
        // Give bonus to stm if not in check.
        score += board.IsInCheck() ? 0 : 15;

        return score;
    }
}
