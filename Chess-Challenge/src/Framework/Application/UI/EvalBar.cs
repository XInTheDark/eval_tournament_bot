using System;
using System.Numerics;
using Raylib_cs;

namespace ChessChallenge.Application
{

    public class EvalBar
    {
        private static readonly Color Black = new (50, 50, 50, 255);
        private static readonly Color White = new (128, 128, 128, 255);
        private static readonly Color TextColor = new (255, 255, 255, 255);

        private const double Scale = 0.005;
        private int _eval;
 
        private readonly Rectangle _position;

        private static double Sigmoid(double x)
        {
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        public void SetEval(int eval)
        {
            _eval = eval;
        }
        
        public void Draw()
        {
            double adjustedEval = Sigmoid(_eval * Scale);
            Raylib.DrawRectangleRec(_position, Black);
            Rectangle whiteRectangle = _position;
            whiteRectangle.width *= (float)adjustedEval;
            Raylib.DrawRectangleRec(whiteRectangle, White);
            Vector2 textPos = new(_position.x + _position.width * 0.98f , _position.y + _position.height / 2);
            String text = _eval.ToString();
            if (Math.Abs(_eval) > 30_000)
            {
                int mateDist = 31_000 - Math.Abs(_eval);
                text = (_eval < 0 ? "-M" : "M") + mateDist.ToString();
            }
            UIHelper.DrawText(text, textPos, (int)(_position.height * 0.75), 1, TextColor, UIHelper.AlignH.Right);
        }

        public EvalBar(Rectangle position)
        {
            _position = position;
        }
    }
}