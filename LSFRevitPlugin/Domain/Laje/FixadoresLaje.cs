using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Nivel de detalhe dos fixadores (parafusos) gerados na laje. Oculto = nao gera nada;
    /// Simplificado = um solido pequeno por fixador; Detalhado = cabeca + haste.
    /// </summary>
    public enum NivelDetalheFixadores
    {
        Oculto,
        Simplificado,
        Detalhado
    }

    /// <summary>Tipo de ligacao de um fixador.</summary>
    public enum TipoFixador
    {
        /// <summary>Ligacao estrutural: ponta da viga encontrando o membro de borda.</summary>
        Estrutural,

        /// <summary>Fixacao da placa de cobertura na viga ao longo do vao.</summary>
        Placa
    }

    /// <summary>
    /// Direcao do eixo do fixador. Estruturais entram pela ponta da viga, ao longo do eixo do
    /// vao; de placa atravessam a placa verticalmente, entrando na viga. A direcao e geometrica
    /// de partida (nao normativa) e serve para orientar a haste no mundo.
    /// </summary>
    public enum DirecaoFixador
    {
        /// <summary>Vertical (atravessa a placa e entra na viga) -- tipico dos fixadores de placa.</summary>
        Vertical,

        /// <summary>Ao longo do eixo do vao (entra pela ponta da viga no membro de borda).</summary>
        AoLongoDaViga
    }

    /// <summary>
    /// Posicao de um fixador em coordenadas locais da laje (u ao longo da viga, v transversal,
    /// z absoluto no projeto, em mm). Puro, sem Revit.
    /// </summary>
    public sealed class PosicaoFixador
    {
        public PosicaoFixador(double uMm, double vMm, double zMm, TipoFixador tipo)
            : this(uMm, vMm, zMm, tipo, tipo == TipoFixador.Estrutural
                  ? DirecaoFixador.AoLongoDaViga
                  : DirecaoFixador.Vertical)
        {
        }

        public PosicaoFixador(double uMm, double vMm, double zMm, TipoFixador tipo, DirecaoFixador direcao)
        {
            UMm = uMm;
            VMm = vMm;
            ZMm = zMm;
            Tipo = tipo;
            Direcao = direcao;
        }

        public double UMm { get; }

        public double VMm { get; }

        public double ZMm { get; }

        public TipoFixador Tipo { get; }

        /// <summary>Direcao do eixo da haste (geometrica de partida, nao normativa).</summary>
        public DirecaoFixador Direcao { get; }
    }

    /// <summary>
    /// Posicoes dos fixadores de uma laje. Estruturais ficam nas pontas das vigas sobre as guias
    /// de borda; de placa ficam ao longo das vigas, na cota da placa, espacados pelo passo pedido.
    ///
    /// ATENCAO: os espacamentos padrao sao geometricos de partida, NAO valores normativos. A
    /// definicao final cabe ao projetista (ver pendencias de confirmacao profissional).
    /// </summary>
    public sealed class ResultadoFixadores
    {
        public List<PosicaoFixador> Estruturais { get; } = new List<PosicaoFixador>();

        public List<PosicaoFixador> Placa { get; } = new List<PosicaoFixador>();

        public bool Oculto { get; internal set; }

        public List<string> Avisos { get; } = new List<string>();

        public int Total => Estruturais.Count + Placa.Count;
    }

    /// <summary>
    /// Calculo puro das posicoes dos fixadores da laje, a partir do layout das vigas. Nao cria
    /// geometria: so devolve pontos, para serem testados sem Revit e convertidos em solidos pela
    /// camada de Geometry.
    /// </summary>
    public static class FixadoresLaje
    {
        /// <summary>Passo padrao entre fixadores de placa ao longo da viga (mm) -- provisorio.</summary>
        public const double EspacamentoPlacaPadraoMm = 300.0;

        /// <summary>Limite de seguranca de fixadores por segmento de viga, para nao explodir o modelo.</summary>
        public const int MaxPorSegmento = 500;

        /// <summary>Tolerancia (mm) para considerar que a ponta da viga encontra a guia de borda.</summary>
        public const double ToleranciaEncontroMm = 1.0;

        public static ResultadoFixadores Calcular(
            ResultadoLayoutVigas layout,
            NivelDetalheFixadores nivel,
            double zCentroVigaMm,
            double zPlacaMm,
            double espacamentoPlacaMm)
        {
            var resultado = new ResultadoFixadores();
            if (layout == null) return resultado;

            if (nivel == NivelDetalheFixadores.Oculto)
            {
                resultado.Oculto = true;
                return resultado;
            }

            resultado.Estruturais.AddRange(Estruturais(layout, zCentroVigaMm));

            if (double.IsNaN(espacamentoPlacaMm) || double.IsInfinity(espacamentoPlacaMm)
                || espacamentoPlacaMm <= 0.0)
            {
                espacamentoPlacaMm = EspacamentoPlacaPadraoMm;
            }

            resultado.Placa.AddRange(Placa(layout, zPlacaMm, espacamentoPlacaMm));
            return resultado;
        }

        /// <summary>Fixadores estruturais: em cada ponta de segmento que encontra uma guia de borda.</summary>
        public static List<PosicaoFixador> Estruturais(ResultadoLayoutVigas layout, double zCentroVigaMm)
        {
            var posicoes = new List<PosicaoFixador>();
            if (layout == null) return posicoes;

            foreach (VigaLaje viga in layout.Vigas)
            {
                foreach (SegmentoViga segmento in viga.Segmentos)
                {
                    AdicionarSeEncontraGuia(
                        posicoes, layout, segmento.InicioUMm, viga.PosicaoTransversalMm, zCentroVigaMm);
                    AdicionarSeEncontraGuia(
                        posicoes, layout, segmento.FimUMm, viga.PosicaoTransversalMm, zCentroVigaMm);
                }
            }

            return posicoes;
        }

        private static void AdicionarSeEncontraGuia(
            List<PosicaoFixador> posicoes,
            ResultadoLayoutVigas layout,
            double uMm,
            double vMm,
            double zMm)
        {
            foreach (GuiaBordaLaje guia in layout.GuiasBorda)
            {
                if (Math.Abs(guia.UMm - uMm) > ToleranciaEncontroMm) continue;
                if (vMm < guia.VInicioMm - ToleranciaEncontroMm) continue;
                if (vMm > guia.VFimMm + ToleranciaEncontroMm) continue;

                posicoes.Add(new PosicaoFixador(uMm, vMm, zMm, TipoFixador.Estrutural));
                return;
            }
        }

        /// <summary>Fixadores de placa: ao longo de cada segmento de viga, a partir do inicio, no passo dado.</summary>
        public static List<PosicaoFixador> Placa(
            ResultadoLayoutVigas layout, double zPlacaMm, double espacamentoMm)
        {
            var posicoes = new List<PosicaoFixador>();
            if (layout == null) return posicoes;

            if (double.IsNaN(espacamentoMm) || double.IsInfinity(espacamentoMm) || espacamentoMm <= 0.0)
                espacamentoMm = EspacamentoPlacaPadraoMm;

            foreach (VigaLaje viga in layout.Vigas)
            {
                foreach (SegmentoViga segmento in viga.Segmentos)
                {
                    if (segmento.ComprimentoMm <= 0.0) continue;

                    int n = (int)Math.Floor(segmento.ComprimentoMm / espacamentoMm) + 1;
                    if (n > MaxPorSegmento) n = MaxPorSegmento;

                    for (int i = 0; i < n; i++)
                    {
                        double u = segmento.InicioUMm + i * espacamentoMm;
                        if (u > segmento.FimUMm + ToleranciaEncontroMm) break;

                        posicoes.Add(new PosicaoFixador(
                            u, viga.PosicaoTransversalMm, zPlacaMm, TipoFixador.Placa));
                    }
                }
            }

            return posicoes;
        }
    }
}
