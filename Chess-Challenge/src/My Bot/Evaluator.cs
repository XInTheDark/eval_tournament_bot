using ChessChallenge.API;
using static ChessChallenge.API.BitboardHelper;
using System;
using static System.Math;
using System.Linq;

public class Evaluator : IEvaluator
{
    public static int[,] psqts = new int[12, 32]; // piece type, square
    public int i, j, k, rank, file; // temp variable used to save tokens
    
    /* VALUES */
    public static readonly int[] evalValues  =
    {
        6, 5, 3, 0, // mobility
        150, 801, 852, 1307, 2581, 40000 // material
    };
    
    // Packed Psqt
    public static readonly decimal[] packedPsqt =
    {
        // x2 quantisation
        7136274781365992494962049024m, 3707723598570256830967641853m, 4260890361m, // pawn MG
        1853254782189190562434777088m, 8992052024079800685349501953m, 386923523m, // pawn EG
        1861707429364259273588855464m, 8081725682095510024512341230m, 17574407607416976094m, // knight MG
        4638586610520180804939145168m, 2776802959713810901776138222m, 17862636003402377181m, // knight EG
        2164014940775567779109404397m, 1237954096498980378498368766m, 17940934567057882106m, // bishop MG
        1237939928675561602158949868m, 620192910078788203406491385m, 17866890159820110325m, // bishop EG
        618946168289982142167512816m, 1861745559436170078891998713m, 360278036081805055m, // rook MG,
        78609173678942687852763019771m, 1852074429221265318044434685m, 434878881917960706m, // rook EG,
        935727437387641066513890561m, 1241595114195444967460307458m, 18374967950420804605m, // queen MG,
        614096186869342314756105181m, 303402252025252617224912628m, 17287883952131207911m, // queen EG,
        14003442467243394413613120101m, 3440858740848085864472069949m, 18379226457874902305m, // king MG,
        20192915250973596020264341504m, 22057154442866270888433564198m, 2097294258731691281m, // king EG
    };
    
    public Evaluator() // init
    {
        // init psqts - extract from packed
        for (; i < 12; i++) // i=0 since this is the constructor
        {
            /* extract */
            var decimals = new System.Collections.Generic.List<decimal>();
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
        int score = 0,
            pieceCount = GetNumberOfSetBits(board.AllPiecesBitboard),
            mgScore = 0, egScore = 0, phase = 0; // Material, PSQT & mobility
        
        foreach (bool color in new[] { true, false })
        {
            int mg = 0, eg = 0, scoreAccum = 0;
            ulong pawnBB = board.GetPieceBitboard(PieceType.Pawn, !color); // opponent's pawns
            
            /* Bishop pair */
            if (GetNumberOfSetBits(board.GetPieceBitboard(PieceType.Bishop, color)) > 1)
                scoreAccum += 40;
            
            k = 0;
            while (k++ < 6) // piece type, k -> 1 to 6
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)k, color);

                while (bitboard != 0) // iterate over every piece of that type
                {
                    /* Material */
                    scoreAccum += evalValues[k + 3];
                    phase += evalValues[k + 3];

                    i = ClearAndGetIndexOfLSB(ref bitboard); // square

                    /* Mobility */
                    // number of squares attacked
                    if (k > 2)
                    {
                        // skip pawns and knights
                        scoreAccum += evalValues[k - 3] * GetNumberOfSetBits(
                            GetPieceAttacks((PieceType)k, new Square(i), board, color) &
                                ~(color ? board.WhitePiecesBitboard : board.BlackPiecesBitboard)
                            );
                    }

                    /* PSQT */
                    if (!color) i ^= 56; // flip square if black
                    rank = i >> 3; file = i & 7;
                    j = rank * 4 + Min(file, 7 - file);
                    mg += psqts[k * 2 - 2, j]; eg += psqts[k * 2 - 1, j];
                    
                    /* Passed Pawn */
                    // basic detection
                    // this is mainly to guide the engine to push pawns in the endgame.
                    if (k == 1 && rank > 3 
                               && (0x101010101010101UL << file & pawnBB) == 0)
                            scoreAccum += rank * 224 / pieceCount;
                } // piece bitboard loop
            } // piece type loop

            j = color ? 1 : -1;
            score += scoreAccum * j;
            mgScore += mg * j; egScore += eg * j;
            
        } // color loop
        
        phase = phase / 75 - 1067; // (phase - 80000) / 75;
        score += (mgScore * phase + egScore * (256 - phase)) / 256; // from white POV
        
        /* STM, tempo */
        return score * (board.IsWhiteToMove ? 1 : -1) + 15;
    }
}
