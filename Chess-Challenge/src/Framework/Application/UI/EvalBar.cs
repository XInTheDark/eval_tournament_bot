using System;
using System.Numerics;
using Raylib_cs;

namespace ChessChallenge.Application
{

    public class EvalBar
    {
        private static readonly Color Black = new (64, 64, 64, 255);
        private static readonly Color White = new (150, 150, 150, 255);
        private static readonly Color TextColor = new (0, 0, 0, 255);

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
            Raylib.DrawRectangleRec(_position, White);
            Rectangle blackRectangle = _position;
            blackRectangle.width *= 1.0f - (float)adjustedEval;
            Raylib.DrawRectangleRec(blackRectangle, Black);
            Vector2 textPos = new(_position.x + _position.width * 0.98f , _position.y + _position.height / 2);
            UIHelper.DrawText(_eval.ToString(), textPos, (int)(_position.height * 0.75), 1, TextColor, UIHelper.AlignH.Right);
        }

        public EvalBar(Rectangle position)
        {
            _position = position;
        }
    }
}