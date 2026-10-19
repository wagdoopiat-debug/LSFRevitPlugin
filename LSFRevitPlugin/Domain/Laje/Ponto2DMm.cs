using System;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Ponto 2D em milimetros, no plano da planta (X para a direita, Y para cima), nas mesmas
    /// coordenadas XY do projeto Revit. O Domain nao conhece unidades internas do Revit: a
    /// conversao fica na borda (Geometry/Services), via RevitVersionAdapter.
    /// </summary>
    public readonly struct Ponto2DMm : IEquatable<Ponto2DMm>
    {
        public Ponto2DMm(double xMm, double yMm)
        {
            XMm = xMm;
            YMm = yMm;
        }

        public double XMm { get; }

        public double YMm { get; }

        public double DistanciaAte(Ponto2DMm outro)
        {
            double dx = outro.XMm - XMm;
            double dy = outro.YMm - YMm;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>true se os dois pontos distam no maximo a tolerancia (em mm).</summary>
        public bool EstaProximoDe(Ponto2DMm outro, double toleranciaMm)
        {
            return DistanciaAte(outro) <= toleranciaMm;
        }

        public bool Equals(Ponto2DMm outro)
        {
            return XMm == outro.XMm && YMm == outro.YMm;
        }

        public override bool Equals(object obj)
        {
            return obj is Ponto2DMm outro && Equals(outro);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (XMm.GetHashCode() * 397) ^ YMm.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F3}; {1:F3})", XMm, YMm);
        }
    }
}
