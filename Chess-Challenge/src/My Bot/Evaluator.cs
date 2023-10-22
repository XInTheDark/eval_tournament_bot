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
    public static readonly int[] pieceValues = { 0, 150, 801, 852, 1307, 2581, 40000 };
    // bishop, rook, queen, king
    public static readonly int[] mobilityValues  = { 6, 5, 3, 0 };
    
    // Packed Psqt
    public static readonly decimal[,] packedPsqt =
    {
        // x2 quantisation
        {4034157052646293636858773504m, 2789001439998792313019365633m, 4277994750m}, // pawn (1)
        {1861712170249826389781631676m, 8081725700542254098221892847m, 17574408711223571423m}, // knight (2)
        {2164014959222311857113923310m, 1237954114945724452207920382m, 17940934567057882106m}, // bishop (3)
        {310674825101479749795575537m, 1861745559436171182698594042m, 288503020827182847m}, // rook (4)
        {935717900348897334573004544m, 1240376670144116481419706882m, 17504344892242065654m}, // queen (5)
        {7167995988612654671579075140m, 2500229339330604076760118057m, 51387926m}, // king (6)
    };
    
    public Evaluator() // init
    {
        int[] values =
        {
            -15, -10, -7, -2,
            -10, -6, -4, 3,
            -12, -5, 0, 1,
            -6, -2, -2, -3,
            -13, -7, -2, 1,
            -11, -1, 3, 6,
            -1, 6, 8, 9,
            -8, -9, 0, 4
        };
        // PACK PSQTS
        var decimals = new List<decimal>();
        var currentByte = 0;
        var bytes = new byte[12];
        for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            var value = values[valueIndex];
            if (value < sbyte.MinValue || value > sbyte.MaxValue)
            {
                throw new Exception($"Value {value} does not fit");
            }

            bytes[currentByte] = (byte)value;
            currentByte++;
            if (currentByte == 12 || valueIndex == values.Length - 1)
            {
                var int1 = BitConverter.ToInt32(bytes, 0);
                var int2 = BitConverter.ToInt32(bytes, 4);
                var int3 = BitConverter.ToInt32(bytes, 8);
                var ints = new[] { int1, int2, int3, 0 };
                var dec = new decimal(ints);
                decimals.Add(dec);
                bytes = new byte[12];
                currentByte = 0;
            }
        }
        
        foreach (var dec in decimals)
        {
            Console.WriteLine(dec);
        }
        //
        // // init psqts - extract from packed
        // for (; i < 6; i++) // i=0 since this is the constructor
        // {
        //     /* extract */
        //     var decimals = new List<decimal>();
        //     for (k = 0; k < 3; k++) // can't use i here since it's used in main init function
        //         decimals.Add(packedPsqt[i, k]);
        //     var psqt = decimals.SelectMany(x => decimal.GetBits(x).Take(3).SelectMany(y => 
        //         BitConverter.GetBytes(y).Select(z => (sbyte)z))).ToArray();
        //     
        //     for (j = 0; j < 32; j++)
        //         psqts[i, j] = psqt[j] * 2; // 2x quantisation
        // }
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
            k = 0; while (k++ < 6) // piece type, k -> 1 to 6
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
                        scoreAccum -= (26 - pieceCount) * (Abs(4 - rank) + Abs(4 - file));

                    /* Passed Pawn */
                    // basic detection
                    // this is mainly to guide the engine to push pawns in the endgame.
                    if (k == 1 && pieceCount <= 24)
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
                            scoreAccum += rank * 224 / pieceCount;
                    }

                    score += scoreAccum * ColorV(color);
                }
            }
        }

        /* STM */
        score *= ColorV(stm);
        
        /* Tempo */
        // Give bonus to stm if not in check.
        score += board.IsInCheck() ? 0 : 15;

        return score;
    }
}
