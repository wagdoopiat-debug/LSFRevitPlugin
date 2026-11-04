using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>Papel de uma barra dentro de uma trelica.</summary>
    public enum MembroTrelicaTipo
    {
        BanzoSuperior,
        BanzoInferior,
        Montante,
        Diagonal
    }

    /// <summary>
    /// Uma barra da trelica em coordenadas locais do vao: u ao longo do vao (0 na primeira ponta,
    /// vaoMm na outra) e z vertical relativo ao centro da trelica (negativo = banzo inferior).
    /// Puro (sem Revit), para ser testado e reaproveitado pela geometria.
    /// </summary>
    public sealed class MembroTrelica
    {
        public MembroTrelica(
            MembroTrelicaTipo tipo, double inicioUMm, double inicioZMm, double fimUMm, double fimZMm)
        {
            if (double.IsNaN(inicioUMm) || double.IsInfinity(inicioUMm)
                || double.IsNaN(inicioZMm) || double.IsInfinity(inicioZMm)
                || double.IsNaN(fimUMm) || double.IsInfinity(fimUMm)
                || double.IsNaN(fimZMm) || double.IsInfinity(fimZMm))
            {
                throw new ArgumentException("Coordenadas do membro devem ser numeros finitos.");
            }

            Tipo = tipo;
            InicioUMm = inicioUMm;
            InicioZMm = inicioZMm;
            FimUMm = fimUMm;
            FimZMm = fimZMm;
        }

        public MembroTrelicaTipo Tipo { get; }

        public double InicioUMm { get; }

        public double InicioZMm { get; }

        public double FimUMm { get; }

        public double FimZMm { get; }

        public double ComprimentoMm
        {
            get
            {
                double du = FimUMm - InicioUMm;
                double dz = FimZMm - InicioZMm;
                return Math.Sqrt(du * du + dz * dz);
            }
        }

        /// <summary>Angulo da barra no plano do vao, em graus, em relacao ao eixo u.</summary>
        public double AnguloGraus
        {
            get
            {
                double du = FimUMm - InicioUMm;
                double dz = FimZMm - InicioZMm;
                return Math.Atan2(dz, du) * 180.0 / Math.PI;
            }
        }
    }

    /// <summary>
    /// Geometria pura de uma trelica: descreve banzos, diagonais e montantes a partir do vao,
    /// da altura (entre eixos dos banzos) e do numero de paineis. Nao dimensiona nada
    /// estruturalmente: e um padrao geometrico de partida, que o usuario confere/ajusta.
    ///
    /// Convencoes: eixos dos banzos em z = +altura/2 (superior) e z = -altura/2 (inferior);
    /// nos em u = i x (vao/n), i de 0 a n. Cada tipo liga esses nos de uma forma propria.
    /// </summary>
    public static class ComposicaoTrelica
    {
        /// <summary>Comprimento alvo de painel (mm) quando o usuario nao informa o numero de paineis.</summary>
        public const double PainelAlvoPadraoMm = 600.0;

        /// <summary>Altura padrao entre eixos dos banzos (mm) -- valor geometrico de partida, provisorio.</summary>
        public const double AlturaPadraoMm = 200.0;

        public const int NumeroPaineisMinimo = 2;
        public const int NumeroPaineisMaximo = 200;

        /// <summary>
        /// Numero de paineis derivado do vao e de um comprimento alvo de painel, limitado a
        /// [NumeroPaineisMinimo, NumeroPaineisMaximo].
        /// </summary>
        public static int NumeroPaineis(double vaoMm, double painelAlvoMm = PainelAlvoPadraoMm)
        {
            if (double.IsNaN(vaoMm) || double.IsInfinity(vaoMm) || vaoMm <= 0.0)
                return NumeroPaineisMinimo;

            if (double.IsNaN(painelAlvoMm) || double.IsInfinity(painelAlvoMm) || painelAlvoMm <= 0.0)
                painelAlvoMm = PainelAlvoPadraoMm;

            int n = (int)Math.Round(vaoMm / painelAlvoMm, MidpointRounding.AwayFromZero);
            if (n < NumeroPaineisMinimo) n = NumeroPaineisMinimo;
            if (n > NumeroPaineisMaximo) n = NumeroPaineisMaximo;
            return n;
        }

        /// <summary>
        /// Barras da trelica (banzos + diagonais/montantes do tipo escolhido). Lanca
        /// ArgumentOutOfRangeException se vao, altura ou numero de paineis forem invalidos.
        /// </summary>
        public static List<MembroTrelica> Membros(
            TrelicaTipo tipo, double vaoMm, double alturaMm, int numeroPaineis)
        {
            if (double.IsNaN(vaoMm) || double.IsInfinity(vaoMm) || vaoMm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(vaoMm), "Vao da trelica deve ser maior que zero.");
            if (double.IsNaN(alturaMm) || double.IsInfinity(alturaMm) || alturaMm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(alturaMm), "Altura da trelica deve ser maior que zero.");
            if (numeroPaineis < NumeroPaineisMinimo)
                throw new ArgumentOutOfRangeException(
                    nameof(numeroPaineis), "Trelica precisa de pelo menos " + NumeroPaineisMinimo + " paineis.");

            double topo = alturaMm / 2.0;
            double basez = -alturaMm / 2.0;
            int n = numeroPaineis;
            double passoU = vaoMm / n;

            var membros = new List<MembroTrelica>
            {
                new MembroTrelica(MembroTrelicaTipo.BanzoSuperior, 0.0, topo, vaoMm, topo),
                new MembroTrelica(MembroTrelicaTipo.BanzoInferior, 0.0, basez, vaoMm, basez)
            };

            Func<int, double> u = i => i * passoU;

            switch (tipo)
            {
                case TrelicaTipo.Warren:
                    for (int i = 0; i < n; i++)
                    {
                        bool par = (i % 2) == 0;
                        if (par)
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), basez, u(i + 1), topo));
                        else
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), topo, u(i + 1), basez));
                    }
                    break;

                case TrelicaTipo.Pratt:
                    AdicionarMontantes(membros, u, n, basez, topo);
                    for (int i = 0; i < n; i++)
                    {
                        // Diagonais inclinadas para o centro (metade esquerda e a imagem da direita).
                        if (i < n / 2.0)
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), topo, u(i + 1), basez));
                        else
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), basez, u(i + 1), topo));
                    }
                    break;

                case TrelicaTipo.Howe:
                    AdicionarMontantes(membros, u, n, basez, topo);
                    for (int i = 0; i < n; i++)
                    {
                        // Diagonais inclinadas para fora (oposto do Pratt).
                        if (i < n / 2.0)
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), basez, u(i + 1), topo));
                        else
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), topo, u(i + 1), basez));
                    }
                    break;

                case TrelicaTipo.DiagonaisAlternadas:
                    AdicionarMontantes(membros, u, n, basez, topo);
                    for (int i = 0; i < n; i++)
                    {
                        bool par = (i % 2) == 0;
                        if (par)
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), basez, u(i + 1), topo));
                        else
                            membros.Add(new MembroTrelica(
                                MembroTrelicaTipo.Diagonal, u(i), topo, u(i + 1), basez));
                    }
                    break;

                case TrelicaTipo.MontantesVerticais:
                    AdicionarMontantes(membros, u, n, basez, topo);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(tipo), "Tipo de trelica nao suportado.");
            }

            return membros;
        }

        /// <summary>
        /// Calcula o envelope vertical fisico da trelica, incluindo a projecao
        /// das secoes transversais de banzos, diagonais e montantes.
        /// </summary>
        public static (double TopoMm, double BaseMm) CalcularEnvelopeVertical(
            TrelicaTipo tipo,
            double vaoMm,
            double alturaMm,
            int numeroPaineis,
            MontagemTrelica montagem,
            double abaMm,
            double larguraAlmaMm)
        {
            if (double.IsNaN(abaMm) || double.IsInfinity(abaMm) || abaMm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(abaMm));

            if (double.IsNaN(larguraAlmaMm) ||
                double.IsInfinity(larguraAlmaMm) || larguraAlmaMm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(larguraAlmaMm));

            List<MembroTrelica> membros =
                Membros(tipo, vaoMm, alturaMm, numeroPaineis);

            double topo = double.NegativeInfinity;
            double basez = double.PositiveInfinity;

            foreach (MembroTrelica membro in membros)
            {
                double du = membro.FimUMm - membro.InicioUMm;
                double dz = membro.FimZMm - membro.InicioZMm;
                double comprimento = Math.Sqrt(du * du + dz * dz);

                if (comprimento <= 1e-9)
                    continue;

                bool banzo =
                    membro.Tipo == MembroTrelicaTipo.BanzoSuperior ||
                    membro.Tipo == MembroTrelicaTipo.BanzoInferior;

                double projecaoVertical;

                if (banzo && montagem == MontagemTrelica.Encaixada)
                {
                    projecaoVertical = abaMm / 2.0;
                }
                else
                {
                    projecaoVertical =
                        Math.Abs(du / comprimento) * larguraAlmaMm / 2.0;
                }

                double zMin =
                    Math.Min(membro.InicioZMm, membro.FimZMm)
                    - projecaoVertical;

                double zMax =
                    Math.Max(membro.InicioZMm, membro.FimZMm)
                    + projecaoVertical;

                topo = Math.Max(topo, zMax);
                basez = Math.Min(basez, zMin);
            }

            if (double.IsInfinity(topo) || double.IsInfinity(basez))
                throw new InvalidOperationException(
                    "Nao foi possivel calcular o envelope vertical da trelica.");

            return (topo, basez);
        }
        private static void AdicionarMontantes(
            List<MembroTrelica> membros, Func<int, double> u, int n, double basez, double topo)
        {
            for (int i = 1; i < n; i++)
            {
                membros.Add(new MembroTrelica(
                    MembroTrelicaTipo.Montante, u(i), basez, u(i), topo));
            }
        }
    }
}
