using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>Posicao de uma chapa gusset em coordenadas locais da trelica (u, z, v) em mm.</summary>
    public sealed class PosicaoGusset
    {
        public PosicaoGusset(double uMm, double zCentroMm, double comprimentoUMm, double alturaZMm, double espessuraMm)
        {
            UMm = uMm;
            ZCentroMm = zCentroMm;
            ComprimentoUMm = comprimentoUMm;
            AlturaZMm = alturaZMm;
            EspessuraMm = espessuraMm;
        }

        /// <summary>Coordenada ao longo do vao (eixo u), em mm.</summary>
        public double UMm { get; }

        /// <summary>Cota vertical central da chapa, em mm.</summary>
        public double ZCentroMm { get; }

        /// <summary>Comprimento da chapa ao longo do eixo u (mm).</summary>
        public double ComprimentoUMm { get; }

        /// <summary>Altura da chapa no eixo vertical z (mm).</summary>
        public double AlturaZMm { get; }

        /// <summary>Espessura da chapa (extrusao na direcao normal ao plano da trelica), em mm.</summary>
        public double EspessuraMm { get; }
    }

    /// <summary>Calculo puro das posicoes de gussets nas juntas da trelica. Valores geometricos de partida.</summary>
    public static class GussetLaje
    {
        public const double EspessuraPadraoMm = 1.5;
        public const double TamanhoPadraoMm = 80.0;

        public static List<PosicaoGusset> Calcular(
            List<MembroTrelica> membros,
            double vaoMm,
            double alturaMm,
            double tamanhoMm = TamanhoPadraoMm,
            double espessuraMm = EspessuraPadraoMm)
        {
            var resultado = new List<PosicaoGusset>();
            if (membros == null || membros.Count == 0) return resultado;

            var nos = new List<(double u, double z)>(16);
            void addNo(double u, double z)
            {
                foreach (var n in nos)
                {
                    if (Math.Abs(n.u - u) < 2.0 && Math.Abs(n.z - z) < 2.0) return;
                }
                nos.Add((u,z));
            }

            foreach (var m in membros)
            {
                addNo(m.InicioUMm, m.InicioZMm);
                addNo(m.FimUMm, m.FimZMm);
            }

            double tam = Math.Max(10.0, tamanhoMm);
            double esp = Math.Max(0.5, espessuraMm);
            double meioTam = tam / 2.0;

            foreach (var n in nos)
            {
                resultado.Add(new PosicaoGusset(n.u, n.z, tam, tam, esp));
            }
            return resultado;
        }
    }
}
