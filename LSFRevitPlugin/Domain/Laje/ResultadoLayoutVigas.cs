using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Trecho continuo de uma viga ao longo de u, em mm (coordenadas do OrientacaoVigas).
    /// </summary>
    public sealed class SegmentoViga
    {
        public SegmentoViga(double inicioUMm, double fimUMm)
        {
            if (fimUMm < inicioUMm)
                throw new ArgumentException("Fim do segmento nao pode ser menor que o inicio.", nameof(fimUMm));

            InicioUMm = inicioUMm;
            FimUMm = fimUMm;
        }

        public double InicioUMm { get; }

        public double FimUMm { get; }

        public double ComprimentoMm => FimUMm - InicioUMm;
    }

    /// <summary>
    /// Uma viga da laje: posicao transversal (coordenada v, mm) e os segmentos ao longo de u.
    /// Contorno concavo pode gerar mais de um segmento. A posicao e a do EIXO da viga.
    /// </summary>
    public sealed class VigaLaje
    {
        public VigaLaje(
            double posicaoTransversalMm, double posicaoGradeMm, bool alinhadaAoApoio, IEnumerable<SegmentoViga> segmentos)
        {
            if (segmentos == null) throw new ArgumentNullException(nameof(segmentos));

            PosicaoTransversalMm = posicaoTransversalMm;
            PosicaoGradeMm = posicaoGradeMm;
            AlinhadaAoApoio = alinhadaAoApoio;
            Segmentos = new List<SegmentoViga>(segmentos).AsReadOnly();
        }

        /// <summary>Posicao final do eixo da viga (coordenada v, mm), ja com o alinhamento aplicado.</summary>
        public double PosicaoTransversalMm { get; }

        /// <summary>Posicao que a viga teria na grade de espacamento, antes do alinhamento.</summary>
        public double PosicaoGradeMm { get; }

        /// <summary>true se a viga foi deslocada para um apoio (montante).</summary>
        public bool AlinhadaAoApoio { get; }

        public IReadOnlyList<SegmentoViga> Segmentos { get; }

        public double ComprimentoTotalMm
        {
            get
            {
                double soma = 0.0;
                foreach (SegmentoViga s in Segmentos) soma += s.ComprimentoMm;
                return soma;
            }
        }
    }

    /// <summary>
    /// Guia de borda: trecho transversal (ao longo de v) na posicao u, onde as vigas terminam.
    /// </summary>
    public sealed class GuiaBordaLaje
    {
        public GuiaBordaLaje(double uMm, double vInicioMm, double vFimMm)
        {
            if (vFimMm < vInicioMm)
                throw new ArgumentException("Fim da guia nao pode ser menor que o inicio.", nameof(vFimMm));

            UMm = uMm;
            VInicioMm = vInicioMm;
            VFimMm = vFimMm;
        }

        public double UMm { get; }

        public double VInicioMm { get; }

        public double VFimMm { get; }

        public double ComprimentoMm => VFimMm - VInicioMm;
    }

    /// <summary>
    /// Resultado do layout das vigas. Erros impedem o uso (lista de vigas vazia); Avisos nao.
    /// </summary>
    public sealed class ResultadoLayoutVigas
    {
        public List<VigaLaje> Vigas { get; } = new List<VigaLaje>();

        public List<GuiaBordaLaje> GuiasBorda { get; } = new List<GuiaBordaLaje>();

        public List<string> Avisos { get; } = new List<string>();

        public List<string> Erros { get; } = new List<string>();

        public bool EhValido => Erros.Count == 0;

        /// <summary>Maior distancia entre eixos de vigas vizinhas, em mm (0 se houver menos de duas vigas).</summary>
        public double MaiorVaoEntreVigasMm { get; internal set; }
    }
}
