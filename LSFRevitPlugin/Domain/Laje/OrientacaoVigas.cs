using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Direcao das vigas da laje e o retangulo envolvente do contorno nesses eixos, em mm.
    /// Eixos: u ao longo da viga; v perpendicular, igual a u girado +90 graus (anti-horario).
    /// Direcao automatica: o retangulo envolvente fica alinhado a aresta mais longa do contorno e
    /// as vigas correm ao longo do lado MAIS CURTO dele (menor vao). Empate: direcao da aresta
    /// mais longa. Direcao manual: angulo em graus a partir de +X, normalizado para [0, 180).
    /// O vao maximo e o do retangulo envolvente; o vao real de cada viga sai do layout (LayoutVigas).
    /// </summary>
    public sealed class OrientacaoVigas
    {
        /// <summary>Diferenca (mm) entre as dimensoes do envolvente abaixo da qual ha empate.</summary>
        public const double ToleranciaEmpateMm = 1.0;

        private OrientacaoVigas(double anguloUGraus, bool automatica, IReadOnlyList<Ponto2DMm> pontos)
        {
            AnguloUGraus = anguloUGraus;
            EhAutomatica = automatica;

            double radianos = anguloUGraus * Math.PI / 180.0;
            UX = Limpar(Math.Cos(radianos));
            UY = Limpar(Math.Sin(radianos));
            VX = 0.0 - UY;
            VY = UX;

            double uMin = double.MaxValue;
            double uMax = double.MinValue;
            double vMin = double.MaxValue;
            double vMax = double.MinValue;

            foreach (Ponto2DMm p in pontos)
            {
                double u = CoordenadaU(p);
                double v = CoordenadaV(p);
                if (u < uMin) uMin = u;
                if (u > uMax) uMax = u;
                if (v < vMin) vMin = v;
                if (v > vMax) vMax = v;
            }

            UMinMm = uMin;
            UMaxMm = uMax;
            VMinMm = vMin;
            VMaxMm = vMax;
        }

        /// <summary>Angulo do eixo u (direcao das vigas) em graus, em [0, 180).</summary>
        public double AnguloUGraus { get; }

        /// <summary>true quando a direcao foi escolhida pelo menor vao; false quando o usuario informou.</summary>
        public bool EhAutomatica { get; }

        /// <summary>Componentes do vetor unitario u (ao longo das vigas).</summary>
        public double UX { get; }

        public double UY { get; }

        /// <summary>Componentes do vetor unitario v (transversal as vigas).</summary>
        public double VX { get; }

        public double VY { get; }

        public double UMinMm { get; }

        public double UMaxMm { get; }

        public double VMinMm { get; }

        public double VMaxMm { get; }

        /// <summary>Extensao do envolvente ao longo das vigas (o vao).</summary>
        public double ExtensaoUMm => UMaxMm - UMinMm;

        /// <summary>Extensao do envolvente transversal as vigas (onde elas se distribuem).</summary>
        public double ExtensaoVMm => VMaxMm - VMinMm;

        /// <summary>Vao maximo, medido no retangulo envolvente.</summary>
        public double VaoMaximoMm => ExtensaoUMm;

        public double CoordenadaU(Ponto2DMm p)
        {
            return p.XMm * UX + p.YMm * UY;
        }

        public double CoordenadaV(Ponto2DMm p)
        {
            return p.XMm * VX + p.YMm * VY;
        }

        /// <summary>Converte coordenadas (u, v) em mm de volta para o plano da planta.</summary>
        public Ponto2DMm ParaMundo(double uMm, double vMm)
        {
            return new Ponto2DMm(uMm * UX + vMm * VX, uMm * UY + vMm * VY);
        }

        /// <summary>
        /// Calcula a orientacao. Contorno nulo lanca ArgumentNullException; contorno invalido ou
        /// direcao nao finita lancam ArgumentException (valide antes com ContornoLaje.Validar).
        /// </summary>
        public static OrientacaoVigas Calcular(ContornoLaje contorno, double? direcaoGraus)
        {
            if (contorno == null) throw new ArgumentNullException(nameof(contorno));

            List<string> erros = contorno.Validar();
            if (erros.Count > 0)
            {
                throw new ArgumentException("Contorno invalido: " + string.Join(" ", erros), nameof(contorno));
            }

            if (direcaoGraus.HasValue)
            {
                double d = direcaoGraus.Value;
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    throw new ArgumentException("Direcao das vigas deve ser um numero finito.", nameof(direcaoGraus));
                }

                return new OrientacaoVigas(Normalizar180(d), false, contorno.Pontos);
            }

            return new OrientacaoVigas(DirecaoAutomatica(contorno.Pontos), true, contorno.Pontos);
        }

        private static double DirecaoAutomatica(IReadOnlyList<Ponto2DMm> pontos)
        {
            int n = pontos.Count;
            double maior = -1.0;
            double anguloLonga = 0.0;

            for (int i = 0; i < n; i++)
            {
                Ponto2DMm p = pontos[i];
                Ponto2DMm q = pontos[(i + 1) % n];
                double dx = q.XMm - p.XMm;
                double dy = q.YMm - p.YMm;
                double comprimento = Math.Sqrt(dx * dx + dy * dy);

                if (comprimento > maior + 1e-9)
                {
                    maior = comprimento;
                    anguloLonga = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                }
            }

            double a = Normalizar180(anguloLonga);
            double extensaoAoLongo = Extensao(pontos, a);
            double extensaoAcross = Extensao(pontos, a + 90.0);

            if (Math.Abs(extensaoAoLongo - extensaoAcross) <= ToleranciaEmpateMm) return a;

            // As vigas correm ao longo do lado mais curto.
            return extensaoAoLongo < extensaoAcross ? a : Normalizar180(a + 90.0);
        }

        private static double Extensao(IReadOnlyList<Ponto2DMm> pontos, double anguloGraus)
        {
            double radianos = anguloGraus * Math.PI / 180.0;
            double ux = Math.Cos(radianos);
            double uy = Math.Sin(radianos);

            double minimo = double.MaxValue;
            double maximo = double.MinValue;

            foreach (Ponto2DMm p in pontos)
            {
                double u = p.XMm * ux + p.YMm * uy;
                if (u < minimo) minimo = u;
                if (u > maximo) maximo = u;
            }

            return maximo - minimo;
        }

        private static double Normalizar180(double graus)
        {
            double a = graus % 180.0;
            if (a < 0.0) a += 180.0;
            if (a >= 180.0) a -= 180.0;
            return a;
        }

        private static double Limpar(double valor)
        {
            return Math.Abs(valor) < 1e-12 ? 0.0 : valor;
        }
    }
}
